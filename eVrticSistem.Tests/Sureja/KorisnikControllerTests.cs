using System.Security.Claims;
using EVrtic.Data;
using EVrtic.Models;
using eVrticSistem.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace eVrticSistem.Tests.Sureja;

[TestClass]
public class KorisnikControllerTests
{
    private ApplicationDbContext _baza = null!;
    private KorisnikController _kontroler = null!;
    private Mock<UserManager<Korisnik>> _userManager = null!;

    [TestInitialize]
    public void PripremiKontroler()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _baza = new ApplicationDbContext(opcije);
        _userManager = new Mock<UserManager<Korisnik>>(
            Mock.Of<IUserStore<Korisnik>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);

        var httpContext = new DefaultHttpContext();
        _kontroler = new KorisnikController(_baza, _userManager.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
        };
    }

    [TestCleanup]
    public void OslobodiKontekst()
    {
        _userManager?.Object.Dispose();
        _baza?.Dispose();
    }

    [TestMethod]
    public async Task IndexSortiraKorisnikePoImenu()
    {
        _baza.Users.AddRange(
            new Korisnik { ImePrezime = "Zora Kovač" },
            new Korisnik { ImePrezime = "Amra Kovač" });
        await _baza.SaveChangesAsync();

        var rezultat = await _kontroler.Index();

        var pogled = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var korisnici = Assert.IsInstanceOfType<List<Korisnik>>(pogled.Model);
        var imena = korisnici.Select(korisnik => korisnik.ImePrezime).ToArray();
        CollectionAssert.AreEqual(new[] { "Amra Kovač", "Zora Kovač" }, imena);
    }

    [TestMethod]
    public async Task ProfilBezKorisnikaTraziPrijavu()
    {
        PostaviTrenutnogKorisnika(null);

        var rezultat = await _kontroler.Profil(null);

        Assert.IsInstanceOfType<ChallengeResult>(rezultat);
    }

    [TestMethod]
    public async Task ProfilRoditeljuPrikazujeVlastitiProfil()
    {
        var prijavljeni = new Roditelj { ImePrezime = "Amra Kovač" };
        var drugi = new Roditelj { ImePrezime = "Emina Kovač" };
        _baza.Roditelji.AddRange(prijavljeni, drugi);
        await _baza.SaveChangesAsync();
        PostaviTrenutnogKorisnika(prijavljeni);

        var rezultat = await _kontroler.Profil(drugi.Id);

        var pogled = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var profil = Assert.IsInstanceOfType<Roditelj>(pogled.Model);
        Assert.AreEqual(prijavljeni.Id, profil.Id);
    }

    [TestMethod]
    public async Task ProfilZaNepostojeciIdVracaNotFound()
    {
        var administrator = new Administrator { Id = 42, ImePrezime = "Amina Kovač" };
        _baza.Administratori.Add(administrator);
        await _baza.SaveChangesAsync();
        PostaviTrenutnogKorisnika(administrator, jeAdministrator: true);

        var rezultat = await _kontroler.Profil(999);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task ProfilAdminuPrikazujeOdgajatelja()
    {
        var administrator = new Administrator { ImePrezime = "Amina Kovač" };
        var odgajatelj = new Odgajatelj { ImePrezime = "Emina Kovač" };
        _baza.AddRange(administrator, odgajatelj);
        await _baza.SaveChangesAsync();
        PostaviTrenutnogKorisnika(administrator, jeAdministrator: true);

        var rezultat = await _kontroler.Profil(odgajatelj.Id);

        var pogled = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var profil = Assert.IsInstanceOfType<Odgajatelj>(pogled.Model);
        Assert.AreEqual(odgajatelj.Id, profil.Id);
        Assert.IsFalse((bool)_kontroler.ViewBag.JeVlastitiProfil);
    }

    [TestMethod]
    public async Task ProfilBezIdPrikazujeVlastitiProfil()
    {
        var administrator = new Administrator { ImePrezime = "Amina Kovač" };
        _baza.Administratori.Add(administrator);
        await _baza.SaveChangesAsync();
        PostaviTrenutnogKorisnika(administrator, jeAdministrator: true);

        var rezultat = await _kontroler.Profil(null);

        var pogled = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var profil = Assert.IsInstanceOfType<Administrator>(pogled.Model);
        Assert.AreEqual(administrator.Id, profil.Id);
        Assert.IsTrue((bool)_kontroler.ViewBag.JeVlastitiProfil);
    }

    [TestMethod]
    public async Task DeaktivirajMijenjaStatusNaloga()
    {
        var korisnik = await SpremiRoditelja(StatusNaloga.AKTIVAN);

        var rezultat = await _kontroler.Deaktiviraj(korisnik.Id);

        ProvjeriPreusmjerenjeNaIndex(rezultat);
        await ProvjeriSpremljeniStatus(korisnik.Id, StatusNaloga.DEAKTIVIRAN);
    }

    [TestMethod]
    public async Task AktivirajMijenjaStatusNaloga()
    {
        var korisnik = await SpremiRoditelja(StatusNaloga.DEAKTIVIRAN);

        var rezultat = await _kontroler.Aktiviraj(korisnik.Id);

        ProvjeriPreusmjerenjeNaIndex(rezultat);
        await ProvjeriSpremljeniStatus(korisnik.Id, StatusNaloga.AKTIVAN);
    }

    [TestMethod]
    public async Task DeaktivirajNepostojeciNalogVracaNotFound()
    {
        var rezultat = await _kontroler.Deaktiviraj(999);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task AktivirajNepostojeciNalogVracaNotFound()
    {
        var rezultat = await _kontroler.Aktiviraj(999);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task ObrisiVlastitiNalogVracaGresku()
    {
        var administrator = new Administrator { ImePrezime = "Amina Kovač" };
        _baza.Administratori.Add(administrator);
        await _baza.SaveChangesAsync();
        PostaviTrenutnogKorisnika(administrator, jeAdministrator: true);

        var rezultat = await _kontroler.Obrisi(administrator.Id);

        ProvjeriPreusmjerenjeNaIndex(rezultat);
        Assert.AreEqual("Ne možete obrisati vlastiti nalog.", _kontroler.TempData["Greska"]);
        _userManager.Verify(m => m.DeleteAsync(It.IsAny<Korisnik>()), Times.Never());
    }

    [TestMethod]
    public async Task ObrisiNepostojeciNalogVracaNotFound()
    {
        PostaviTrenutnogKorisnika(new Administrator { Id = 42 }, jeAdministrator: true);

        var rezultat = await _kontroler.Obrisi(999);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
        _userManager.Verify(m => m.DeleteAsync(It.IsAny<Korisnik>()), Times.Never());
    }

    [TestMethod]
    public async Task ObrisiRoditeljaOdvajaDijete()
    {
        var roditelj = new Roditelj { ImePrezime = "Amra Kovač" };
        var dijete = new Dijete
        {
            ImePrezime = "Dijete 1",
            IdentifikacioniKod = "D0000001",
            Roditelj = roditelj
        };

        _baza.Djeca.Add(dijete);
        await _baza.SaveChangesAsync();
        PostaviTrenutnogKorisnika(new Administrator { Id = 42 }, jeAdministrator: true);
        _userManager.Setup(m => m.DeleteAsync(roditelj)).ReturnsAsync(IdentityResult.Success);

        var rezultat = await _kontroler.Obrisi(roditelj.Id);

        ProvjeriPreusmjerenjeNaIndex(rezultat);
        _userManager.Verify(m => m.DeleteAsync(roditelj), Times.Once());
        Assert.IsNotNull(_kontroler.TempData["Uspjeh"]);

        _baza.ChangeTracker.Clear();
        var spremljenoDijete = await _baza.Djeca.SingleAsync(d => d.Id == dijete.Id);
        Assert.IsNull(spremljenoDijete.RoditeljId);
    }

    [TestMethod]
    public async Task ObrisiOdgajateljaOdvajaGrupu()
    {
        var odgajatelj = new Odgajatelj { ImePrezime = "Emina Kovač" };
        var grupa = new Grupa { ImeGrupe = "Pčelice", Odgajatelj = odgajatelj };

        _baza.Grupe.Add(grupa);
        await _baza.SaveChangesAsync();
        PostaviTrenutnogKorisnika(new Administrator { Id = 42 }, jeAdministrator: true);
        _userManager.Setup(m => m.DeleteAsync(odgajatelj)).ReturnsAsync(IdentityResult.Success);

        var rezultat = await _kontroler.Obrisi(odgajatelj.Id);

        ProvjeriPreusmjerenjeNaIndex(rezultat);
        _userManager.Verify(m => m.DeleteAsync(odgajatelj), Times.Once());

        _baza.ChangeTracker.Clear();
        var spremljenaGrupa = await _baza.Grupe.SingleAsync(g => g.Id == grupa.Id);
        Assert.IsNull(spremljenaGrupa.OdgajateljId);
    }

    [TestMethod]
    public async Task ObrisiKadIdentityOdbijePrikazujeGresku()
    {
        var korisnik = new Administrator { ImePrezime = "Emina Kovač" };
        _baza.Administratori.Add(korisnik);
        await _baza.SaveChangesAsync();
        PostaviTrenutnogKorisnika(new Administrator { Id = 42 }, jeAdministrator: true);

        var greska = new IdentityError { Description = "Brisanje odbijeno." };
        _userManager
            .Setup(m => m.DeleteAsync(korisnik))
            .ReturnsAsync(IdentityResult.Failed(greska));

        var rezultat = await _kontroler.Obrisi(korisnik.Id);

        ProvjeriPreusmjerenjeNaIndex(rezultat);
        Assert.AreEqual("Brisanje neuspješno: Brisanje odbijeno.", _kontroler.TempData["Greska"]);
        Assert.IsNull(_kontroler.TempData["Uspjeh"]);
    }

    private void PostaviTrenutnogKorisnika(Korisnik? korisnik, bool jeAdministrator = false)
    {
        _userManager
            .Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(korisnik);

        if (korisnik != null)
        {
            _userManager
                .Setup(m => m.IsInRoleAsync(korisnik, "ADMINISTRATOR"))
                .ReturnsAsync(jeAdministrator);
        }
    }

    private async Task<Roditelj> SpremiRoditelja(StatusNaloga status)
    {
        var roditelj = new Roditelj
        {
            ImePrezime = "Amra Kovač",
            StatusNaloga = status
        };

        _baza.Roditelji.Add(roditelj);
        await _baza.SaveChangesAsync();
        return roditelj;
    }

    private async Task ProvjeriSpremljeniStatus(int id, StatusNaloga ocekivaniStatus)
    {
        _baza.ChangeTracker.Clear();
        var roditelj = await _baza.Roditelji.SingleAsync(r => r.Id == id);
        Assert.AreEqual(ocekivaniStatus, roditelj.StatusNaloga);
    }

    private static void ProvjeriPreusmjerenjeNaIndex(IActionResult rezultat)
    {
        var preusmjerenje = Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual(nameof(KorisnikController.Index), preusmjerenje.ActionName);
    }
}