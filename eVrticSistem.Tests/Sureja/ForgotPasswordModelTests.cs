using EVrtic.Models;
using EVrtic.Services;
using eVrticSistem.Areas.Identity.Pages.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace eVrticSistem.Tests.Sureja;

[TestClass]
public class ForgotPasswordModelTests
{
    private const string Email = "amra@example.test";
    private const string NaslovPoruke = "eVrtić — Resetovanje lozinke";
    private const string LinkZaReset =
        "https://vrtic.example.test/Identity/Account/ResetPassword";

    private Mock<UserManager<Korisnik>> _userManager = null!;
    private Mock<IEmailService> _emailService = null!;
    private ForgotPasswordModel _stranica = null!;

    [TestInitialize]
    public void PripremiStranicu()
    {
        _userManager = new Mock<UserManager<Korisnik>>(
            Mock.Of<IUserStore<Korisnik>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);

        _emailService = new Mock<IEmailService>(MockBehavior.Strict);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";

        var pageContext = new PageContext
        {
            HttpContext = httpContext,
            RouteData = new RouteData()
        };

        var url = new Mock<IUrlHelper>();
        url.SetupGet(u => u.ActionContext).Returns(pageContext);
        url.Setup(u => u.RouteUrl(It.IsAny<UrlRouteContext>())).Returns(LinkZaReset);

        _stranica = new ForgotPasswordModel(_userManager.Object, _emailService.Object)
        {
            Input = new ForgotPasswordModel.InputModel { Email = Email },
            PageContext = pageContext,
            Url = url.Object
        };
    }

    [TestCleanup]
    public void OslobodiUserManager()
    {
        _userManager?.Object.Dispose();
    }

    [TestMethod]
    public void OnGetNePokreceReset()
    {
        _stranica.OnGet();

        _userManager.Verify(m => m.FindByEmailAsync(It.IsAny<string>()), Times.Never());
        _emailService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task OnPostNeispravanUnosVracaFormu()
    {
        _stranica.ModelState.AddModelError("Input.Email", "Neispravna adresa.");

        var rezultat = await _stranica.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(rezultat);
        _userManager.Verify(m => m.FindByEmailAsync(It.IsAny<string>()), Times.Never());
        _emailService.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task OnPostNepostojeciNalogVracaPotvrdu()
    {
        _userManager.Setup(m => m.FindByEmailAsync(Email))
            .ReturnsAsync((Korisnik?)null);

        var rezultat = await _stranica.OnPostAsync();

        ProvjeriPreusmjerenjeNaPotvrdu(rezultat);
        _userManager.Verify(
            m => m.GeneratePasswordResetTokenAsync(It.IsAny<Korisnik>()),
            Times.Never());
        _emailService.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task OnPostPostojeciNalogPozivaSlanjeIVracaPotvrdu(bool slanjeUspjelo)
    {
        var korisnik = new Roditelj { Id = 7, Email = Email };

        _userManager.Setup(m => m.FindByEmailAsync(Email)).ReturnsAsync(korisnik);
        _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(korisnik))
            .ReturnsAsync("token-za-test");

        _emailService
            .Setup(s => s.SendAsync(Email, NaslovPoruke, It.IsAny<string>()))
            .ReturnsAsync(slanjeUspjelo);

        var rezultat = await _stranica.OnPostAsync();

        ProvjeriPreusmjerenjeNaPotvrdu(rezultat);
        _userManager.Verify(m => m.GeneratePasswordResetTokenAsync(korisnik), Times.Once());
        _emailService.Verify(
            s => s.SendAsync(Email, NaslovPoruke,
                It.Is<string>(tekst => tekst.Contains(LinkZaReset))),
            Times.Once());
    }

    private static void ProvjeriPreusmjerenjeNaPotvrdu(IActionResult rezultat)
    {
        var preusmjerenje = Assert.IsInstanceOfType<RedirectToPageResult>(rezultat);
        Assert.AreEqual("./ForgotPasswordConfirmation", preusmjerenje.PageName);
    }
}