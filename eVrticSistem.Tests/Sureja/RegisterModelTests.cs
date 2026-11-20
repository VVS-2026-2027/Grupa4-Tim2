using System.Linq.Expressions;
using EVrtic.Areas.Identity.Pages.Account;
using EVrtic.Data;
using EVrtic.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace eVrticSistem.Tests.Sureja;

[TestClass]
public class RegisterModelTests
{
    private const string Email = "amina@example.test";
    private const string Lozinka = "Test-lozinka-058";
    private const string KodDjeteta = "ABCD1234";
    private const string PoljeKoda = "Input.IdentifikacioniKodDjeteta";

    private ApplicationDbContext _baza = null!;
    private Mock<UserManager<Korisnik>> _userManager = null!;
    private Mock<SignInManager<Korisnik>> _signInManager = null!;
    private RegisterModel _stranica = null!;

    [TestInitialize]
    public void PripremiStranicu()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new PoredjenjeTestnihKodova())
            .Options;

        _baza = new ApplicationDbContext(opcije);

        _userManager = new Mock<UserManager<Korisnik>>(
            Mock.Of<IUserStore<Korisnik>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);

        _userManager.Setup(m => m.FindByEmailAsync(Email)).ReturnsAsync((Korisnik?)null);

        _signInManager = new Mock<SignInManager<Korisnik>>(
            _userManager.Object,
            new HttpContextAccessor(),
            Mock.Of<IUserClaimsPrincipalFactory<Korisnik>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<Korisnik>>.Instance,
            Mock.Of<IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<Korisnik>>());

        _signInManager
            .Setup(m => m.GetExternalAuthenticationSchemesAsync())
            .ReturnsAsync(Array.Empty<AuthenticationScheme>());

        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.Content("~/Home/RedirectByRole"))
            .Returns("/Home/RedirectByRole");

        _stranica = new RegisterModel(
            _userManager.Object,
            _signInManager.Object,
            NullLogger<RegisterModel>.Instance,
            _baza)
        {
            Input = new RegisterModel.InputModel
            {
                ImePrezime = "  Amina   Čengić  ",
                Email = $" {Email} ",
                Password = Lozinka,
                ConfirmPassword = Lozinka,
                Uloga = Uloga.ODGAJATELJ
            },
            Url = url.Object
        };
    }

    [TestCleanup]
    public void OslobodiKontekst()
    {
        _userManager?.Object.Dispose();
        _baza?.Dispose();
    }

    [TestMethod]
    public async Task OnGetPamtiPovratnuPutanju()
    {
        await _stranica.OnGetAsync("/Home");

        Assert.AreEqual("/Home", _stranica.ReturnUrl);
        Assert.IsEmpty(_stranica.ExternalLogins);
    }

    [TestMethod]
    [DataRow(Uloga.ADMINISTRATOR, null, "Amina Čengić", "Input.Uloga")]
    [DataRow(Uloga.RODITELJ, null, "Amina Čengić", PoljeKoda)]
    [DataRow(Uloga.RODITELJ, "ABC", "Amina Čengić", PoljeKoda)]
    [DataRow(Uloga.ODGAJATELJ, null, " ", "Input.ImePrezime")]
    [DataRow(Uloga.ODGAJATELJ, null, "amina Čengić", "Input.ImePrezime")]
    public async Task OnPostNeispravanUnosVracaFormu(
        Uloga uloga, string? kod, string ime, string poljeGreske)
    {
        _stranica.Input.Uloga = uloga;
        _stranica.Input.IdentifikacioniKodDjeteta = kod;
        _stranica.Input.ImePrezime = ime;

        var rezultat = await _stranica.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(rezultat);
        Assert.IsNotEmpty(_stranica.ModelState[poljeGreske]!.Errors);
        ProvjeriDaNalogNijeKreiran();
    }

    [TestMethod]
    public async Task OnPostPostojeciEmailVracaGresku()
    {
        _userManager.Setup(m => m.FindByEmailAsync(Email))
            .ReturnsAsync(new Odgajatelj { Email = Email });

        var rezultat = await _stranica.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(rezultat);
        Assert.IsNotEmpty(_stranica.ModelState["Input.Email"]!.Errors);
        ProvjeriDaNalogNijeKreiran();
    }

    [TestMethod]
    public async Task OnPostRegistrujeOdgajatelja()
    {
        PostaviUspjesnoKreiranjeNaloga("ODGAJATELJ");

        var rezultat = await _stranica.OnPostAsync();

        var preusmjerenje = Assert.IsInstanceOfType<LocalRedirectResult>(rezultat);
        Assert.AreEqual("/Home/RedirectByRole", preusmjerenje.Url);

        _userManager.Verify(m => m.CreateAsync(It.Is<Korisnik>(k =>
            k is Odgajatelj &&
            k.ImePrezime == "Amina Čengić" &&
            k.Email == Email &&
            k.UserName == Email &&
            k.StatusNaloga == StatusNaloga.AKTIVAN), Lozinka), Times.Once());

        _userManager.Verify(
            m => m.AddToRoleAsync(It.IsAny<Korisnik>(), "ODGAJATELJ"),
            Times.Once());
        _signInManager.Verify(
            m => m.SignInAsync(It.IsAny<Korisnik>(), false, null),
            Times.Once());
    }

    [TestMethod]
    public async Task OnPostNeuspjesnoKreiranjeVracaIdentityGresku()
    {
        var greska = new IdentityError { Description = "Lozinka je odbijena." };
        _userManager.Setup(m => m.CreateAsync(It.IsAny<Korisnik>(), Lozinka))
            .ReturnsAsync(IdentityResult.Failed(greska));

        var rezultat = await _stranica.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(rezultat);
        Assert.AreEqual(greska.Description,
            _stranica.ModelState[string.Empty]!.Errors.Single().ErrorMessage);
        _signInManager.Verify(
            m => m.SignInAsync(It.IsAny<Korisnik>(), false, null),
            Times.Never());
    }

    [TestMethod]
    [DataRow("abcd1234", false, "Dijete sa unesenim identifikacionim kodom nije pronađeno.")]
    [DataRow(KodDjeteta, true, "Dijete je već povezano sa roditeljem.")]
    public async Task OnPostZaNedostupanKodVracaGresku(
        string kod, bool dijeteVecImaRoditelja, string ocekivanaGreska)
    {
        await SpremiDijete(dijeteVecImaRoditelja);
        _stranica.Input.Uloga = Uloga.RODITELJ;
        _stranica.Input.IdentifikacioniKodDjeteta = kod;

        var rezultat = await _stranica.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(rezultat);
        Assert.AreEqual(ocekivanaGreska,
            _stranica.ModelState[PoljeKoda]!.Errors.Single().ErrorMessage);
        ProvjeriDaNalogNijeKreiran();
    }

    [TestMethod]
    public async Task OnPostRegistrujeRoditeljaIPovezujeDijete()
    {
        const int roditeljId = 42;
        var dijete = await SpremiDijete();
        _stranica.Input.Uloga = Uloga.RODITELJ;
        _stranica.Input.IdentifikacioniKodDjeteta = $" {KodDjeteta} ";
        _stranica.Input.KontaktTelefon = "061 234 567";
        PostaviUspjesnoKreiranjeNaloga("RODITELJ", roditeljId);

        var rezultat = await _stranica.OnPostAsync();

        var preusmjerenje = Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual("Dijete", preusmjerenje.ControllerName);
        Assert.AreEqual("UnosPodataka", preusmjerenje.ActionName);

        _userManager.Verify(m => m.CreateAsync(It.Is<Korisnik>(k =>
            k is Roditelj && ((Roditelj)k).KontaktTelefon == "061234567"),
            Lozinka), Times.Once());

        _userManager.Verify(
            m => m.AddToRoleAsync(It.IsAny<Korisnik>(), "RODITELJ"),
            Times.Once());

        _baza.ChangeTracker.Clear();
        var spremljenoDijete = await _baza.Djeca.SingleAsync(d => d.Id == dijete.Id);
        Assert.AreEqual(roditeljId, spremljenoDijete.RoditeljId);
    }

    private void PostaviUspjesnoKreiranjeNaloga(string uloga, int noviId = 42)
    {
        _userManager.Setup(m => m.CreateAsync(It.IsAny<Korisnik>(), Lozinka))
            .Callback<Korisnik, string>((korisnik, _) => korisnik.Id = noviId)
            .ReturnsAsync(IdentityResult.Success);

        _userManager.Setup(m => m.AddToRoleAsync(It.IsAny<Korisnik>(), uloga))
            .ReturnsAsync(IdentityResult.Success);

        _signInManager.Setup(m => m.SignInAsync(It.IsAny<Korisnik>(), false, null))
            .Returns(Task.CompletedTask);
    }

    private void ProvjeriDaNalogNijeKreiran()
    {
        _userManager.Verify(
            m => m.CreateAsync(It.IsAny<Korisnik>(), It.IsAny<string>()),
            Times.Never());
    }

    private async Task<Dijete> SpremiDijete(bool vecImaRoditelja = false)
    {
        var dijete = new Dijete
        {
            ImePrezime = "Dijete 1",
            IdentifikacioniKod = KodDjeteta,
            Roditelj = vecImaRoditelja ? new Roditelj { ImePrezime = "Amra Kovač" } : null
        };

        _baza.Djeca.Add(dijete);
        await _baza.SaveChangesAsync();
        return dijete;
    }

    private sealed class PoredjenjeTestnihKodova : ExpressionVisitor, IQueryExpressionInterceptor
    {
        public Expression QueryCompilationStarting(
            Expression queryExpression, QueryExpressionEventData eventData)
        {
            return Visit(queryExpression)!;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(RelationalDbFunctionsExtensions)
                && node.Method.Name == nameof(RelationalDbFunctionsExtensions.Collate)
                && node.Arguments[2] is ConstantExpression { Value: "Latin1_General_100_CS_AS" })
            {
                return Visit(node.Arguments[1])!;
            }

            return base.VisitMethodCall(node);
        }
    }
}