using EVrtic.Models;
using eVrticSistem.Areas.Identity.Pages.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace eVrticSistem.Tests.Sureja;

[TestClass]
public class LoginModelTests
{
    private const string Email = "amra@example.test";
    private const string Lozinka = "Test-lozinka-058";

    private Mock<UserManager<Korisnik>> _userManager = null!;
    private Mock<SignInManager<Korisnik>> _signInManager = null!;
    private LoginModel _stranica = null!;

    [TestInitialize]
    public void PripremiStranicu()
    {
        _userManager = new Mock<UserManager<Korisnik>>(
            Mock.Of<IUserStore<Korisnik>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);

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
        url.Setup(u => u.Content("~/")).Returns("/");

        _stranica = new LoginModel(
            _signInManager.Object,
            NullLogger<LoginModel>.Instance)
        {
            Input = new LoginModel.InputModel
            {
                Email = Email,
                Password = Lozinka,
                RememberMe = true
            },
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext()
            },
            Url = url.Object
        };
    }

    [TestCleanup]
    public void OslobodiUserManager()
    {
        _userManager?.Object.Dispose();
    }

    [TestMethod]
    public async Task OnGetPostavljaPodrazumijevanuPutanju()
    {
        var autentifikacija = NapraviAutentifikaciju();
        using var servisi = PripremiServise(autentifikacija.Object);

        await _stranica.OnGetAsync();

        Assert.AreEqual("/", _stranica.ReturnUrl);
        Assert.IsEmpty(_stranica.ExternalLogins);
        autentifikacija.Verify(
            a => a.SignOutAsync(_stranica.HttpContext, IdentityConstants.ExternalScheme, null),
            Times.Once());
    }

    [TestMethod]
    public async Task OnGetPrenosiPorukuOGresci()
    {
        var autentifikacija = NapraviAutentifikaciju();
        using var servisi = PripremiServise(autentifikacija.Object);
        _stranica.ErrorMessage = "Vanjska prijava nije uspjela.";

        await _stranica.OnGetAsync("/Home");

        Assert.AreEqual("/Home", _stranica.ReturnUrl);
        ProvjeriGresku("Vanjska prijava nije uspjela.");
    }

    [TestMethod]
    public async Task OnPostSaLosimUnosomVracaFormu()
    {
        _stranica.ModelState.AddModelError("Input.Email", "Unesite email.");

        var rezultat = await _stranica.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(rezultat);
        _signInManager.Verify(
            m => m.PasswordSignInAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Never());
    }

    [TestMethod]
    public async Task OnPostUspjesnaPrijavaPreusmjerava()
    {
        PostaviRezultatPrijave(SignInResult.Success);
        PostaviStatusNaloga(StatusNaloga.AKTIVAN);

        var rezultat = await _stranica.OnPostAsync("/Home");

        var preusmjerenje = Assert.IsInstanceOfType<LocalRedirectResult>(rezultat);
        Assert.AreEqual("/Home", preusmjerenje.Url);
        _signInManager.Verify(
            m => m.PasswordSignInAsync(Email, Lozinka, true, false),
            Times.Once());
    }

    [TestMethod]
    public async Task OnPostDeaktiviranNalogOdjavljujeKorisnika()
    {
        PostaviRezultatPrijave(SignInResult.Success);
        PostaviStatusNaloga(StatusNaloga.DEAKTIVIRAN);
        _signInManager.Setup(m => m.SignOutAsync()).Returns(Task.CompletedTask);

        var rezultat = await _stranica.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(rezultat);
        ProvjeriGresku("Vaš nalog je deaktiviran. Obratite se administratoru.");
        _signInManager.Verify(m => m.SignOutAsync(), Times.Once());
    }

    [TestMethod]
    public async Task OnPostDvofaktorskaPrijavaPreusmjerava()
    {
        PostaviRezultatPrijave(SignInResult.TwoFactorRequired);

        var rezultat = await _stranica.OnPostAsync("/Home");

        var preusmjerenje = Assert.IsInstanceOfType<RedirectToPageResult>(rezultat);
        Assert.AreEqual("./LoginWith2fa", preusmjerenje.PageName);
        Assert.AreEqual("/Home", preusmjerenje.RouteValues!["ReturnUrl"]);
        Assert.IsTrue((bool?)preusmjerenje.RouteValues["RememberMe"]);
    }

    [TestMethod]
    public async Task OnPostZakljucanNalogPreusmjeravaNaLockout()
    {
        PostaviRezultatPrijave(SignInResult.LockedOut);

        var rezultat = await _stranica.OnPostAsync();

        var preusmjerenje = Assert.IsInstanceOfType<RedirectToPageResult>(rezultat);
        Assert.AreEqual("./Lockout", preusmjerenje.PageName);
    }

    [TestMethod]
    public async Task OnPostPogresnaLozinkaVracaGresku()
    {
        PostaviRezultatPrijave(SignInResult.Failed);

        var rezultat = await _stranica.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(rezultat);
        ProvjeriGresku("Invalid login attempt.");
    }

    private void PostaviRezultatPrijave(SignInResult rezultat)
    {
        _signInManager
            .Setup(m => m.PasswordSignInAsync(Email, Lozinka, true, false))
            .ReturnsAsync(rezultat);
    }

    private void PostaviStatusNaloga(StatusNaloga status)
    {
        var korisnik = new Roditelj { Email = Email, StatusNaloga = status };

        _userManager
            .Setup(m => m.FindByEmailAsync(Email))
            .ReturnsAsync(korisnik);
    }

    private void ProvjeriGresku(string ocekivanaPoruka)
    {
        var stvarnaPoruka = _stranica.ModelState[string.Empty]!
            .Errors.Single().ErrorMessage;

        Assert.AreEqual(ocekivanaPoruka, stvarnaPoruka);
    }

    private static Mock<IAuthenticationService> NapraviAutentifikaciju()
    {
        var autentifikacija = new Mock<IAuthenticationService>();
        autentifikacija
            .Setup(a => a.SignOutAsync(
                It.IsAny<HttpContext>(), IdentityConstants.ExternalScheme, null))
            .Returns(Task.CompletedTask);

        return autentifikacija;
    }

    private ServiceProvider PripremiServise(IAuthenticationService autentifikacija)
    {
        var servisi = new ServiceCollection()
            .AddSingleton(autentifikacija)
            .BuildServiceProvider();

        _stranica.HttpContext.RequestServices = servisi;
        return servisi;
    }
}