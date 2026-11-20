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
public class RoditeljControllerTests
{
    private const string PocetnoIme = "Amra Kovač";
    private const string PocetniTelefon = "061111111";
    private const string PocetniEmail = "amra@example.test";
    private const int NepostojeciId = 999;

    private ApplicationDbContext _baza = null!;
    private RoditeljController _kontroler = null!;
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
        _kontroler = new RoditeljController(_baza, _userManager.Object)
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
    public async Task MojProfilPrikazujeRoditeljaIDijete()
    {
        var roditelj = await SpremiRoditelja();
        var dijete = new Dijete
        {
            ImePrezime = "Dijete 1",
            IdentifikacioniKod = "D0000001",
            RoditeljId = roditelj.Id
        };
        _baza.Djeca.Add(dijete);
        await _baza.SaveChangesAsync();
        _baza.ChangeTracker.Clear();
        PostaviTrenutnogKorisnika(roditelj);

        var rezultat = await _kontroler.MojProfil();

        var profil = ModelIzPogleda<Roditelj>(rezultat);
        Assert.AreEqual(roditelj.Id, profil.Id);
        Assert.AreEqual(dijete.Id, profil.Djeca.Single().Id);
    }

    [TestMethod]
    public async Task MojProfilBezKorisnikaTraziPrijavu()
    {
        PostaviTrenutnogKorisnika(null);

        var rezultat = await _kontroler.MojProfil();

        Assert.IsInstanceOfType<ChallengeResult>(rezultat);
    }

    [TestMethod]
    public async Task MojProfilBezRoditeljaTraziPrijavu()
    {
        PostaviTrenutnogKorisnika(new Korisnik { Id = NepostojeciId });

        var rezultat = await _kontroler.MojProfil();

        Assert.IsInstanceOfType<ChallengeResult>(rezultat);
    }

    [TestMethod]
    public async Task UrediProfilGetPopunjavaFormu()
    {
        var roditelj = await SpremiRoditelja();
        PostaviTrenutnogKorisnika(roditelj);

        var rezultat = await _kontroler.UrediProfil();

        var model = ModelIzPogleda<UrediProfilViewModel>(rezultat);
        Assert.AreEqual(PocetnoIme, model.ImePrezime);
        Assert.AreEqual(PocetniTelefon, model.KontaktTelefon);
        Assert.AreEqual(PocetniEmail, model.Email);
    }

    [TestMethod]
    [DataRow("061 234 567", "061234567")]
    [DataRow("   ", null)]
    public async Task UrediProfilPostSpremaIspravanUnos(
        string telefon, string? ocekivaniTelefon)
    {
        var roditelj = await SpremiRoditelja();
        PostaviTrenutnogKorisnika(roditelj);

        var unos = new UrediProfilViewModel
        {
            ImePrezime = "  Emina   Kovač  ",
            KontaktTelefon = telefon,
            Email = "drugi@example.test"
        };

        var rezultat = await _kontroler.UrediProfil(unos);

        ProvjeriPreusmjerenje(rezultat, nameof(RoditeljController.MojProfil));
        var spremljeni = await UcitajRoditelja(roditelj.Id);
        Assert.AreEqual("Emina Kovač", spremljeni.ImePrezime);
        Assert.AreEqual(ocekivaniTelefon, spremljeni.KontaktTelefon);
        Assert.AreEqual(PocetniEmail, spremljeni.Email);
    }

    [TestMethod]
    [DataRow(" ", PocetniTelefon, nameof(UrediProfilViewModel.ImePrezime))]
    [DataRow("emina Kovač", PocetniTelefon, nameof(UrediProfilViewModel.ImePrezime))]
    [DataRow("Emina Kovač", "071234567", nameof(UrediProfilViewModel.KontaktTelefon))]
    public async Task UrediProfilPostZaLosUnosVracaFormu(
        string ime, string telefon, string polje)
    {
        var roditelj = await SpremiRoditelja();
        PostaviTrenutnogKorisnika(roditelj);

        var unos = new UrediProfilViewModel
        {
            ImePrezime = ime,
            KontaktTelefon = telefon
        };

        var rezultat = await _kontroler.UrediProfil(unos);

        var model = ModelIzPogleda<UrediProfilViewModel>(rezultat);
        Assert.AreEqual(PocetniEmail, model.Email);
        Assert.IsNotEmpty(_kontroler.ModelState[polje]!.Errors);

        var spremljeni = await UcitajRoditelja(roditelj.Id);
        Assert.AreEqual(PocetnoIme, spremljeni.ImePrezime);
        Assert.AreEqual(PocetniTelefon, spremljeni.KontaktTelefon);
    }

    [TestMethod]
    public async Task DeaktivirajSpremaDeaktiviranStatus()
    {
        var roditelj = await SpremiRoditelja(StatusNaloga.AKTIVAN);

        var rezultat = await _kontroler.Deaktiviraj(roditelj.Id);

        ProvjeriPreusmjerenje(rezultat, nameof(RoditeljController.Index));
        var spremljeni = await UcitajRoditelja(roditelj.Id);
        Assert.AreEqual(StatusNaloga.DEAKTIVIRAN, spremljeni.StatusNaloga);
    }

    [TestMethod]
    public async Task AktivirajSpremaAktivanStatus()
    {
        var roditelj = await SpremiRoditelja(StatusNaloga.DEAKTIVIRAN);

        var rezultat = await _kontroler.Aktiviraj(roditelj.Id);

        ProvjeriPreusmjerenje(rezultat, nameof(RoditeljController.Index));
        var spremljeni = await UcitajRoditelja(roditelj.Id);
        Assert.AreEqual(StatusNaloga.AKTIVAN, spremljeni.StatusNaloga);
    }

    [TestMethod]
    public async Task DeaktivirajNepostojeciProfilVracaNotFound()
    {
        var rezultat = await _kontroler.Deaktiviraj(NepostojeciId);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task AktivirajNepostojeciProfilVracaNotFound()
    {
        var rezultat = await _kontroler.Aktiviraj(NepostojeciId);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task IndexVracaForbid()
    {
        var rezultat = await _kontroler.Index();

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task DetailsVracaForbid()
    {
        var rezultat = await _kontroler.Details(42);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public void CreateGetVracaForbid()
    {
        var rezultat = _kontroler.Create();

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task EditGetVracaForbid()
    {
        var rezultat = await _kontroler.Edit(42);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task DeleteGetVracaForbid()
    {
        var rezultat = await _kontroler.Delete(42);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task CreatePostSpremaRoditelja()
    {
        var roditelj = new Roditelj { ImePrezime = "Emina Kovač" };

        var rezultat = await _kontroler.Create(roditelj);

        ProvjeriPreusmjerenje(rezultat, nameof(RoditeljController.Index));
        var spremljeni = await UcitajRoditelja(roditelj.Id);
        Assert.AreEqual("Emina Kovač", spremljeni.ImePrezime);
    }

    [TestMethod]
    public async Task CreatePostZaNeispravanModelVracaFormu()
    {
        var roditelj = new Roditelj();
        _kontroler.ModelState.AddModelError(nameof(roditelj.ImePrezime), "Obavezno polje.");

        var rezultat = await _kontroler.Create(roditelj);

        Assert.AreSame(roditelj, ModelIzPogleda<Roditelj>(rezultat));
        Assert.IsFalse(await _baza.Roditelji.AnyAsync());
    }

    [TestMethod]
    public async Task EditPostSpremaIzmjene()
    {
        var roditelj = await SpremiRoditelja();
        _baza.ChangeTracker.Clear();
        roditelj.ImePrezime = "Emina Kovač";

        var rezultat = await _kontroler.Edit(roditelj.Id, roditelj);

        ProvjeriPreusmjerenje(rezultat, nameof(RoditeljController.Index));
        var spremljeni = await UcitajRoditelja(roditelj.Id);
        Assert.AreEqual("Emina Kovač", spremljeni.ImePrezime);
    }

    [TestMethod]
    public async Task EditPostZaRazliciteIdVrijednostiVracaNotFound()
    {
        var roditelj = new Roditelj { Id = 42 };

        var rezultat = await _kontroler.Edit(NepostojeciId, roditelj);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task EditPostZaNeispravanModelVracaFormu()
    {
        var roditelj = await SpremiRoditelja();
        _baza.ChangeTracker.Clear();
        roditelj.ImePrezime = "";
        _kontroler.ModelState.AddModelError(nameof(roditelj.ImePrezime), "Obavezno polje.");

        var rezultat = await _kontroler.Edit(roditelj.Id, roditelj);

        Assert.AreSame(roditelj, ModelIzPogleda<Roditelj>(rezultat));
        var spremljeni = await UcitajRoditelja(roditelj.Id);
        Assert.AreEqual(PocetnoIme, spremljeni.ImePrezime);
    }

    [TestMethod]
    public async Task DeleteConfirmedBriseRoditelja()
    {
        var roditelj = await SpremiRoditelja();

        var rezultat = await _kontroler.DeleteConfirmed(roditelj.Id);

        ProvjeriPreusmjerenje(rezultat, nameof(RoditeljController.Index));
        Assert.IsFalse(await _baza.Roditelji.AnyAsync(r => r.Id == roditelj.Id));
    }

    [TestMethod]
    public async Task DeleteConfirmedZaNepostojeciIdOstavljaZapis()
    {
        var roditelj = await SpremiRoditelja();

        var rezultat = await _kontroler.DeleteConfirmed(NepostojeciId);

        ProvjeriPreusmjerenje(rezultat, nameof(RoditeljController.Index));
        Assert.IsTrue(await _baza.Roditelji.AnyAsync(r => r.Id == roditelj.Id));
    }

    private void PostaviTrenutnogKorisnika(Korisnik? korisnik)
    {
        _userManager
            .Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(korisnik);
    }

    private async Task<Roditelj> SpremiRoditelja(
        StatusNaloga status = StatusNaloga.AKTIVAN)
    {
        var roditelj = new Roditelj
        {
            ImePrezime = PocetnoIme,
            KontaktTelefon = PocetniTelefon,
            Email = PocetniEmail,
            StatusNaloga = status
        };

        _baza.Roditelji.Add(roditelj);
        await _baza.SaveChangesAsync();
        return roditelj;
    }

    private async Task<Roditelj> UcitajRoditelja(int id)
    {
        _baza.ChangeTracker.Clear();
        return await _baza.Roditelji.SingleAsync(r => r.Id == id);
    }

    private static T ModelIzPogleda<T>(IActionResult rezultat)
    {
        var pogled = Assert.IsInstanceOfType<ViewResult>(rezultat);
        return Assert.IsInstanceOfType<T>(pogled.Model);
    }

    private static void ProvjeriPreusmjerenje(IActionResult rezultat, string akcija)
    {
        var preusmjerenje = Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual(akcija, preusmjerenje.ActionName);
    }
}