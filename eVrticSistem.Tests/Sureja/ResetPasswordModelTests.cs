using System.Text;
using EVrtic.Models;
using eVrticSistem.Areas.Identity.Pages.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace eVrticSistem.Tests.Sureja;

[TestClass]
public class ResetPasswordModelTests
{
    private const string KorisnikId = "7";
    private const string Email = "amra@example.test";
    private const string Token = "token-za-test";
    private const string NovaLozinka = "Test-lozinka-058";
    private const string Potvrda = "./ResetPasswordConfirmation";

    private Mock<UserManager<Korisnik>> _userManager = null!;
    private Roditelj _korisnik = null!;
    private ResetPasswordModel _stranica = null!;

    [TestInitialize]
    public void PripremiStranicu()
    {
        _korisnik = new Roditelj { Id = 7, Email = Email };

        _userManager = new Mock<UserManager<Korisnik>>(
            Mock.Of<IUserStore<Korisnik>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);

        _userManager.Setup(m => m.FindByIdAsync(KorisnikId))
            .ReturnsAsync(_korisnik);
        _userManager.Setup(m => m.FindByIdAsync("999"))
            .ReturnsAsync((Korisnik?)null);

        _stranica = new ResetPasswordModel(_userManager.Object)
        {
            Input = new ResetPasswordModel.InputModel
            {
                UserId = KorisnikId,
                Code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(Token)),
                Password = NovaLozinka,
                ConfirmPassword = NovaLozinka
            }
        };
    }

    [TestCleanup]
    public void OslobodiUserManager()
    {
        _userManager?.Object.Dispose();
    }

    [TestMethod]
    [DataRow(null, KorisnikId)]
    [DataRow("test-kod", null)]
    public async Task OnGetNepotpunLinkVracaBadRequest(string? kod, string? id)
    {
        var rezultat = await _stranica.OnGetAsync(kod!, id!);

        Assert.IsInstanceOfType<BadRequestObjectResult>(rezultat);
        _userManager.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never());
    }

    [TestMethod]
    [DataRow(KorisnikId, Email)]
    [DataRow("999", "")]
    public async Task OnGetPopunjavaModel(string id, string ocekivaniEmail)
    {
        const string kod = "test-kod";

        var rezultat = await _stranica.OnGetAsync(kod, id);

        Assert.IsInstanceOfType<PageResult>(rezultat);
        Assert.AreEqual(id, _stranica.Input.UserId);
        Assert.AreEqual(kod, _stranica.Input.Code);
        Assert.AreEqual(ocekivaniEmail, _stranica.Email);
    }

    [TestMethod]
    public async Task OnPostNeispravanUnosVracaFormu()
    {
        _stranica.ModelState.AddModelError("Input.Password", "Lozinka je obavezna.");

        var rezultat = await _stranica.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(rezultat);
        ProvjeriDaResetNijePozvan();
    }

    [TestMethod]
    public async Task OnPostNepostojeciNalogVracaPotvrdu()
    {
        _stranica.Input.UserId = "999";

        var rezultat = await _stranica.OnPostAsync();

        ProvjeriPreusmjerenjeNaPotvrdu(rezultat);
        ProvjeriDaResetNijePozvan();
    }

    [TestMethod]
    public async Task OnPostUspjesanResetVracaPotvrdu()
    {
        _userManager
            .Setup(m => m.ResetPasswordAsync(_korisnik, Token, NovaLozinka))
            .ReturnsAsync(IdentityResult.Success);

        var rezultat = await _stranica.OnPostAsync();

        ProvjeriPreusmjerenjeNaPotvrdu(rezultat);
        _userManager.Verify(
            m => m.ResetPasswordAsync(_korisnik, Token, NovaLozinka),
            Times.Once());
    }

    [TestMethod]
    public async Task OnPostOdbijenResetPrikazujeGresku()
    {
        var greska = new IdentityError { Description = "Token je istekao." };
        _userManager
            .Setup(m => m.ResetPasswordAsync(_korisnik, Token, NovaLozinka))
            .ReturnsAsync(IdentityResult.Failed(greska));

        var rezultat = await _stranica.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(rezultat);
        var poruka = _stranica.ModelState[string.Empty]!.Errors.Single().ErrorMessage;
        Assert.AreEqual(greska.Description, poruka);
    }

    private void ProvjeriDaResetNijePozvan()
    {
        _userManager.Verify(m => m.ResetPasswordAsync(
            It.IsAny<Korisnik>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never());
    }

    private static void ProvjeriPreusmjerenjeNaPotvrdu(IActionResult rezultat)
    {
        var preusmjerenje = Assert.IsInstanceOfType<RedirectToPageResult>(rezultat);
        Assert.AreEqual(Potvrda, preusmjerenje.PageName);
    }
}