using System.Security.Claims;
using EVrtic.Controllers;
using EVrtic.Data;
using EVrtic.Models;
using EVrtic.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace EVrtic.Tests;

[TestClass]
public class ObavijestControllerTests
{
    private const int OdgajateljId = 5;
    private const int AnaId = 10;
    private const int BosaId = 11;
    private const int CanaId = 12;
    private static readonly DateTime Trenutak = new(2026, 5, 4, 10, 30, 15);

    private ApplicationDbContext _context = null!;
    private Mock<UserManager<Korisnik>> _userManagerMock = null!;
    private Mock<IEmailService> _emailMock = null!;
    private ObavijestController _controller = null!;

    public static IEnumerable<object[]> NeispravniOdabiriPrimalaca => TestPodaci.IzCsv("neispravan-odabir-primalaca.csv");
    public static IEnumerable<object?[]> PraznaEmailAdrese => TestPodaci.IzJsonNiza("prazne-email-adrese.json");

    [TestInitialize]
    public void PripremiOkruzenje()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(opcije);
        _userManagerMock = new Mock<UserManager<Korisnik>>(
            Mock.Of<IUserStore<Korisnik>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        _emailMock = new Mock<IEmailService>();
        _emailMock.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .ReturnsAsync(true);
        _controller = new ObavijestController(_context, _userManagerMock.Object, _emailMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };
    }

    [TestCleanup]
    public void Pocisti() => _context.Dispose();

    // ===== Onemogućene akcije =====

    [TestMethod]
    public async Task Index_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(await _controller.Index());

    [TestMethod]
    public async Task Details_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(await _controller.Details(1));

    [TestMethod]
    public void CreateGet_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(_controller.Create());

    [TestMethod]
    public async Task CreatePost_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(await _controller.Create(new Obavijest()));

    [TestMethod]
    public async Task EditGet_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(await _controller.Edit(1));

    [TestMethod]
    public async Task EditPost_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(await _controller.Edit(1, new Obavijest()));

    [TestMethod]
    public async Task DeleteGet_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(await _controller.Delete(1));

    // ===== DeleteConfirmed =====

    [TestMethod]
    public async Task DeleteConfirmed_PostojecaObavijest_BriseJeIPreusmjeravaNaIndex()
    {
        var (odgajatelj, roditelj) = DodajOdgajateljaIRoditelja();
        var obavijest = DodajObavijest(odgajatelj, roditelj, "Naslov", Trenutak);

        var rezultat = await _controller.DeleteConfirmed(obavijest.Id);

        Assert.AreEqual(nameof(_controller.Index), Kao<RedirectToActionResult>(rezultat).ActionName);
        Assert.AreEqual(0, _context.Obavijesti.Count());
    }

    [TestMethod]
    public async Task DeleteConfirmed_NepostojecaObavijest_PreusmjeravaNaIndexBezBrisanja()
    {
        var rezultat = await _controller.DeleteConfirmed(999);

        Assert.AreEqual(nameof(_controller.Index), Kao<RedirectToActionResult>(rezultat).ActionName);
    }

    // ===== RoditeljPregled =====

    [TestMethod]
    public async Task RoditeljPregled_BezPrijavljenogKorisnika_PreusmjeravaNaLogin()
    {
        NikoNijePrijavljen();

        var rezultat = await _controller.RoditeljPregled();

        var redirect = Kao<RedirectToPageResult>(rezultat);
        Assert.AreEqual("/Account/Login", redirect.PageName);
        Assert.AreEqual("Identity", redirect.RouteValues!["area"]);
    }

    [TestMethod]
    public async Task RoditeljPregled_RoditeljSaObavijestima_VracaSamoNjegoveObavijesti()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();
        var bosa = DodajRoditelja(BosaId, "Bosa", "bosa@mail.ba");
        DodajObavijest(odgajatelj, ana, "Anina", Trenutak);
        DodajObavijest(odgajatelj, bosa, "Bosina", Trenutak);
        PrijaviKorisnika(AnaId);

        var model = ModelIz<RoditeljObavijestiViewModel>(await _controller.RoditeljPregled());

        CollectionAssert.AreEqual(new[] { "Anina" }, model.Obavijesti.Select(o => o.Naslov).ToArray());
    }

    [TestMethod]
    public async Task RoditeljPregled_ViseObavijesti_SortiraPoDatumuSlanjaPaKreiranja()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();
        DodajObavijest(odgajatelj, ana, "KreiranaRanije", Trenutak, slano: Trenutak.AddDays(5));
        DodajObavijest(odgajatelj, ana, "KreiranaKasnije", Trenutak.AddDays(3), slano: null);
        PrijaviKorisnika(AnaId);

        var model = ModelIz<RoditeljObavijestiViewModel>(await _controller.RoditeljPregled());

        var ocekivano = new[] { "KreiranaRanije", "KreiranaKasnije" };
        CollectionAssert.AreEqual(ocekivano, model.Obavijesti.Select(o => o.Naslov).ToArray());
    }

    [TestMethod]
    [DataRow(StatusObavijesti.POSLANA, 1)]
    [DataRow(StatusObavijesti.KREIRANA, 1)]
    [DataRow(StatusObavijesti.PROCITANA, 0)]
    public async Task RoditeljPregled_ObavijestSaStatusom_ISpravnoBrojiNove(StatusObavijesti status, int ocekivanoNovih)
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();
        DodajObavijest(odgajatelj, ana, "Naslov", Trenutak, status);
        PrijaviKorisnika(AnaId);

        var model = ModelIz<RoditeljObavijestiViewModel>(await _controller.RoditeljPregled());

        Assert.AreEqual(ocekivanoNovih, model.BrojNovih);
    }

    [TestMethod]
    public async Task RoditeljPregled_RoditeljBezObavijesti_VracaPrazanModel()
    {
        DodajOdgajateljaIRoditelja();
        PrijaviKorisnika(AnaId);

        var model = ModelIz<RoditeljObavijestiViewModel>(await _controller.RoditeljPregled());

        Assert.AreEqual(0, model.Obavijesti.Count);
        Assert.AreEqual(0, model.BrojNovih);
    }

    // ===== RoditeljDetalji =====

    [TestMethod]
    public async Task RoditeljDetalji_IdJeNull_VracaNotFound()
    {
        var rezultat = await _controller.RoditeljDetalji(null);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task RoditeljDetalji_BezPrijavljenogKorisnika_PreusmjeravaNaLogin()
    {
        NikoNijePrijavljen();

        var rezultat = await _controller.RoditeljDetalji(1);

        Assert.AreEqual("/Account/Login", Kao<RedirectToPageResult>(rezultat).PageName);
    }

    [TestMethod]
    public async Task RoditeljDetalji_NepostojecaObavijest_VracaNotFound()
    {
        PrijaviKorisnika(AnaId);

        var rezultat = await _controller.RoditeljDetalji(999);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task RoditeljDetalji_ObavijestDrugogRoditelja_VracaNotFound()
    {
        var (odgajatelj, _) = DodajOdgajateljaIRoditelja();
        var bosa = DodajRoditelja(BosaId, "Bosa", "bosa@mail.ba");
        var tudja = DodajObavijest(odgajatelj, bosa, "Bosina", Trenutak);
        PrijaviKorisnika(AnaId);

        var rezultat = await _controller.RoditeljDetalji(tudja.Id);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task RoditeljDetalji_NeprocitanaObavijest_OznacavaJeProcitanom()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();
        var obavijest = DodajObavijest(odgajatelj, ana, "Naslov", Trenutak, StatusObavijesti.POSLANA);
        PrijaviKorisnika(AnaId);

        await _controller.RoditeljDetalji(obavijest.Id);

        Assert.AreEqual(StatusObavijesti.PROCITANA, _context.Obavijesti.Single().StatusObavijesti);
    }

    [TestMethod]
    public async Task RoditeljDetalji_PostojecaObavijest_VracaViewSaObavijesti()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();
        var obavijest = DodajObavijest(odgajatelj, ana, "Naslov", Trenutak, StatusObavijesti.PROCITANA);
        PrijaviKorisnika(AnaId);

        var model = ModelIz<Obavijest>(await _controller.RoditeljDetalji(obavijest.Id));

        Assert.AreEqual(obavijest.Id, model.Id);
    }

    // ===== OdgajateljObavijesti =====

    [TestMethod]
    public async Task OdgajateljObavijesti_BezPrijavljenogKorisnika_VracaChallenge()
    {
        NikoNijePrijavljen();

        var rezultat = await _controller.OdgajateljObavijesti();

        Assert.IsInstanceOfType<ChallengeResult>(rezultat);
    }

    [TestMethod]
    public async Task OdgajateljObavijesti_PrijavljenOdgajatelj_VracaModelSaNjegovimId()
    {
        PrijaviKorisnika(OdgajateljId);

        var model = ModelIz<OdgajateljObavijestViewModel>(await _controller.OdgajateljObavijesti());

        Assert.AreEqual(OdgajateljId, model.TrenutniKorisnikId);
    }

    [TestMethod]
    public async Task OdgajateljObavijesti_ObavijestPoslanaViseRoditelja_PrikazujeJedanRed()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();
        var bosa = DodajRoditelja(BosaId, "Bosa", "bosa@mail.ba");
        DodajObavijest(odgajatelj, ana, "Izlet", Trenutak);
        DodajObavijest(odgajatelj, bosa, "Izlet", Trenutak);
        PrijaviKorisnika(OdgajateljId);

        var model = ModelIz<OdgajateljObavijestViewModel>(await _controller.OdgajateljObavijesti());

        Assert.AreEqual(1, model.Obavijesti.Count);
    }

    [TestMethod]
    public async Task OdgajateljObavijesti_RazlicitiNaslovi_PrikazujeSveGrupeSortiranePoDatumu()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();
        DodajObavijest(odgajatelj, ana, "Starija", Trenutak);
        DodajObavijest(odgajatelj, ana, "Novija", Trenutak.AddDays(1));
        PrijaviKorisnika(OdgajateljId);

        var model = ModelIz<OdgajateljObavijestViewModel>(await _controller.OdgajateljObavijesti());

        CollectionAssert.AreEqual(new[] { "Novija", "Starija" }, model.Obavijesti.Select(o => o.Naslov).ToArray());
    }

    [TestMethod]
    public async Task OdgajateljObavijesti_IstiNaslovAliRazlicitaSekunda_PrikazujeObaReda()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();
        DodajObavijest(odgajatelj, ana, "Izlet", Trenutak);
        DodajObavijest(odgajatelj, ana, "Izlet", Trenutak.AddSeconds(5));
        PrijaviKorisnika(OdgajateljId);

        var model = ModelIz<OdgajateljObavijestViewModel>(await _controller.OdgajateljObavijesti());

        Assert.AreEqual(2, model.Obavijesti.Count);
    }

    // ===== PosaljiObavijest (GET) =====

    [TestMethod]
    public async Task PosaljiObavijestGet_BezPrijavljenogKorisnika_VracaChallenge()
    {
        NikoNijePrijavljen();

        var rezultat = await _controller.PosaljiObavijest();

        Assert.IsInstanceOfType<ChallengeResult>(rezultat);
    }

    [TestMethod]
    public async Task PosaljiObavijestGet_PrijavljenOdgajatelj_VracaModelSaOdgajateljemIGrupama()
    {
        PripremiScenarijSlanja();

        var model = ModelIz<PosaljiObavijestViewModel>(await _controller.PosaljiObavijest());

        Assert.AreEqual(OdgajateljId, model.Odgajatelj!.Id);
        Assert.AreEqual(2, model.Odgajatelj.Grupe.Count);
    }

    [TestMethod]
    public async Task PosaljiObavijestGet_KorisnikNijeOdgajatelj_OdgajateljUModeluJeNull()
    {
        PrijaviKorisnika(99);

        var model = ModelIz<PosaljiObavijestViewModel>(await _controller.PosaljiObavijest());

        Assert.IsNull(model.Odgajatelj);
    }

    // ===== PosaljiObavijest (POST) — autorizacija =====

    [TestMethod]
    public async Task PosaljiObavijestPost_BezPrijavljenogKorisnika_VracaUnauthorized()
    {
        NikoNijePrijavljen();

        var rezultat = await _controller.PosaljiObavijest(NoviUnos());

        Assert.IsInstanceOfType<UnauthorizedResult>(rezultat);
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_KorisnikNijeOdgajatelj_VracaForbid()
    {
        PrijaviKorisnika(99);

        var rezultat = await _controller.PosaljiObavijest(NoviUnos());

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    // ===== PosaljiObavijest (POST) — primaoci =====

    [TestMethod]
    public async Task PosaljiObavijestPost_PrimaSvi_ObavijestiSveRoditelje()
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(NoviUnos(prima: "svi"));

        CollectionAssert.AreEquivalent(new[] { AnaId, BosaId, CanaId }, PrimaociIzBaze());
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_PrimaVlastitaGrupa_ObavijestiSamoRoditeljeTeGrupeBezDuplikata()
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(NoviUnos(prima: "grupa:1"));

        CollectionAssert.AreEqual(new[] { AnaId }, PrimaociIzBaze());
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_PrimaTudjaGrupa_NemaPrimalacaIVracaGresku()
    {
        PripremiScenarijSlanja();

        var rezultat = await _controller.PosaljiObavijest(NoviUnos(prima: "grupa:3"));

        Assert.AreEqual("Nema roditelja koji se mogu obavijestiti.", _controller.TempData["Greska"]);
        Assert.AreEqual(0, _context.Obavijesti.Count());
        Assert.AreEqual(nameof(_controller.OdgajateljObavijesti), Kao<RedirectToActionResult>(rezultat).ActionName);
    }

    [TestMethod]
    [DynamicData(nameof(NeispravniOdabiriPrimalaca))]
    public async Task PosaljiObavijestPost_NeispravanOdabirGrupe_NemaPrimalacaIVracaGresku(string prima)
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(NoviUnos(prima: prima));

        Assert.AreEqual("Nema roditelja koji se mogu obavijestiti.", _controller.TempData["Greska"]);
        Assert.AreEqual(0, _context.Obavijesti.Count());
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_PodrazumijevaniOdabir_ObavijestiRoditeljeDjeceIzSvihVlastitihGrupa()
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(NoviUnos(prima: "roditelji"));

        CollectionAssert.AreEquivalent(new[] { AnaId, BosaId }, PrimaociIzBaze());
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_OdgajateljBezGrupa_VracaGresku()
    {
        DodajOdgajatelja(OdgajateljId, "Edina");
        PrijaviKorisnika(OdgajateljId);

        await _controller.PosaljiObavijest(NoviUnos(prima: "roditelji"));

        Assert.AreEqual("Nema roditelja koji se mogu obavijestiti.", _controller.TempData["Greska"]);
    }

    // ===== PosaljiObavijest (POST) — sadržaj zapisa =====

    [TestMethod]
    public async Task PosaljiObavijestPost_ValidanUnos_SpremaZapiseSaPodacimaIzUnosa()
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(NoviUnos(prima: "grupa:1"));

        var obavijest = _context.Obavijesti.Single();
        Assert.AreEqual("Izlet", obavijest.Naslov);
        Assert.AreEqual("Idemo u park.", obavijest.Sadrzaj);
        Assert.AreEqual(OdgajateljId, obavijest.OdgajateljId);
        Assert.AreEqual(StatusObavijesti.POSLANA, obavijest.StatusObavijesti);
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_ViseRoditelja_SviZapisiImajuIstiTrenutak()
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(NoviUnos(prima: "svi"));

        Assert.AreEqual(1, _context.Obavijesti.Select(o => o.DatumKreiranja).Distinct().Count());
        Assert.IsTrue(_context.Obavijesti.All(o => o.DatumSlanja == o.DatumKreiranja));
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_NaslovIPorukaNull_SpremaPrazneTekstove()
    {
        PripremiScenarijSlanja();
        var unos = NoviUnos(prima: "grupa:1");
        unos.Naslov = null;
        unos.Poruka = null;

        await _controller.PosaljiObavijest(unos);

        var obavijest = _context.Obavijesti.Single();
        Assert.AreEqual(string.Empty, obavijest.Naslov);
        Assert.AreEqual(string.Empty, obavijest.Sadrzaj);
    }

    [TestMethod]
    [DataRow(true, true, KanalSlanja.EMAIL)]
    [DataRow(true, false, KanalSlanja.EMAIL)]
    [DataRow(false, true, KanalSlanja.APLIKACIJA)]
    [DataRow(false, false, KanalSlanja.APLIKACIJA)]
    public async Task PosaljiObavijestPost_OdabraniKanali_ODredujuKanalSlanja(bool email, bool aplikacija, KanalSlanja ocekivani)
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(NoviUnos(prima: "grupa:1", email: email, aplikacija: aplikacija));

        Assert.AreEqual(ocekivani, _context.Obavijesti.Single().KanalSlanja);
    }

    // ===== PosaljiObavijest (POST) — email =====

    [TestMethod]
    public async Task PosaljiObavijestPost_BezEmaila_NeSaljeEmailIPostavljaPorukuUspjeha()
    {
        PripremiScenarijSlanja();

        var rezultat = await _controller.PosaljiObavijest(NoviUnos(prima: "svi", email: false));

        Assert.AreEqual("Obavijest uspješno poslana.", _controller.TempData["Uspjeh"]);
        Assert.AreEqual(nameof(_controller.OdgajateljObavijesti), Kao<RedirectToActionResult>(rezultat).ActionName);
        _emailMock.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_SaEmailom_SaljeJedanEmailPoRoditelju()
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(NoviUnos(prima: "svi", email: true));

        _emailMock.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(3));
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_SaEmailom_EmailImaIspravanNaslovIPrimaoca()
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(NoviUnos(prima: "grupa:1", email: true));

        _emailMock.Verify(e => e.SendAsync("ana@mail.ba", "[eVrtić] Izlet", It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_SaEmailom_TijeloEmailaSadrziImenaIPoruku()
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(NoviUnos(prima: "grupa:1", email: true));

        _emailMock.Verify(e => e.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<string>(tijelo => tijelo.Contains("Ana") && tijelo.Contains("Idemo u park.") && tijelo.Contains("Edina"))),
            Times.Once);
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_SviEmailoviDostavljeni_PrijavljujeBrojAdresa()
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(NoviUnos(prima: "svi", email: true));

        Assert.AreEqual("Obavijest poslana. Email dostavljen na 3 adresa.", _controller.TempData["Uspjeh"]);
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_JedanEmailNeuspjesan_PrijavljujeDjelimicanUspjeh()
    {
        PripremiScenarijSlanja();
        _emailMock.Setup(e => e.SendAsync("bosa@mail.ba", It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);

        await _controller.PosaljiObavijest(NoviUnos(prima: "svi", email: true));

        var ocekivano = "Obavijest poslana. Email dostavljen na 2 adresa. (1 adresa nije bilo moguće obavijestiti emailom.)";
        Assert.AreEqual(ocekivano, _controller.TempData["Uspjeh"]);
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_SviEmailoviNeuspjesni_PrijavljujeSlanjePutemAplikacije()
    {
        PripremiScenarijSlanja();
        _emailMock.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);

        await _controller.PosaljiObavijest(NoviUnos(prima: "svi", email: true));

        var ocekivano = "Obavijest poslana putem aplikacije. (Email slanje nije uspjelo ni za jednu adresu.)";
        Assert.AreEqual(ocekivano, _controller.TempData["Uspjeh"]);
    }

    [TestMethod]
    [DynamicData(nameof(PraznaEmailAdrese))]
    public async Task PosaljiObavijestPost_RoditeljBezEmailAdrese_NeSaljeEmailIPrijavljujeNeuspjeh(string? email)
    {
        PripremiScenarijSlanja(emailAne: email);

        await _controller.PosaljiObavijest(NoviUnos(prima: "grupa:1", email: true));

        _emailMock.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        StringAssert.Contains((string)_controller.TempData["Uspjeh"]!, "Email slanje nije uspjelo ni za jednu adresu.");
    }

    // ===== ObrisiBatch =====

    [TestMethod]
    public async Task ObrisiBatch_BezPrijavljenogKorisnika_VracaUnauthorized()
    {
        NikoNijePrijavljen();

        var rezultat = await _controller.ObrisiBatch(1);

        Assert.IsInstanceOfType<UnauthorizedResult>(rezultat);
    }

    [TestMethod]
    public async Task ObrisiBatch_NepostojecaObavijest_VracaNotFound()
    {
        PrijaviKorisnika(OdgajateljId);

        var rezultat = await _controller.ObrisiBatch(999);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task ObrisiBatch_ObavijestDrugogOdgajatelja_VracaForbidBezBrisanja()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();
        var obavijest = DodajObavijest(odgajatelj, ana, "Naslov", Trenutak);
        PrijaviKorisnika(99);

        var rezultat = await _controller.ObrisiBatch(obavijest.Id);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
        Assert.AreEqual(1, _context.Obavijesti.Count());
    }

    [TestMethod]
    public async Task ObrisiBatch_ViseZapisaIsteAkcije_BriseSveZapiseTeAkcije()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();
        var bosa = DodajRoditelja(BosaId, "Bosa", "bosa@mail.ba");
        var prva = DodajObavijest(odgajatelj, ana, "Izlet", Trenutak);
        DodajObavijest(odgajatelj, bosa, "Izlet", Trenutak);
        PrijaviKorisnika(OdgajateljId);

        await _controller.ObrisiBatch(prva.Id);

        Assert.AreEqual(0, _context.Obavijesti.Count());
    }

    [TestMethod]
    public async Task ObrisiBatch_PostojiDrugaAkcija_NjenZapisNeBrise()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();
        var meta = DodajObavijest(odgajatelj, ana, "Izlet", Trenutak);
        DodajObavijest(odgajatelj, ana, "Izlet", Trenutak.AddSeconds(5));
        DodajObavijest(odgajatelj, ana, "Drugi naslov", Trenutak);
        PrijaviKorisnika(OdgajateljId);

        await _controller.ObrisiBatch(meta.Id);

        Assert.AreEqual(2, _context.Obavijesti.Count());
    }

    [TestMethod]
    public async Task ObrisiBatch_UspjesnoBrisanje_PreusmjeravaNaPregledSaPorukom()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();
        var obavijest = DodajObavijest(odgajatelj, ana, "Izlet", Trenutak);
        PrijaviKorisnika(OdgajateljId);

        var rezultat = await _controller.ObrisiBatch(obavijest.Id);

        Assert.AreEqual(nameof(_controller.OdgajateljObavijesti), Kao<RedirectToActionResult>(rezultat).ActionName);
        Assert.AreEqual("Obavijest je obrisana.", _controller.TempData["Uspjeh"]);
    }

    // ===== ViewModeli =====

    [TestMethod]
    public void PosaljiObavijestInputModel_NoviObjekat_ImaPodrazumijevaneVrijednosti()
    {
        var unos = new PosaljiObavijestInputModel();

        Assert.AreEqual("roditelji", unos.Prima);
        Assert.IsTrue(unos.PutemAplikacije);
        Assert.IsFalse(unos.PutemEmaila);
    }

    [TestMethod]
    public void ViewModeli_NoviObjekti_ImajuPrazneKolekcije()
    {
        Assert.AreEqual(0, new OdgajateljObavijestViewModel().Obavijesti.Count);
        Assert.AreEqual(0, new RoditeljObavijestiViewModel().Obavijesti.Count);
        Assert.IsNull(new PosaljiObavijestViewModel().Odgajatelj);
    }

    // ===== Pomoćne metode =====

    private void PrijaviKorisnika(int id) =>
        _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                        .ReturnsAsync(new Korisnik { Id = id });

    private void NikoNijePrijavljen() =>
        _userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                        .ReturnsAsync((Korisnik?)null);

    private Odgajatelj DodajOdgajatelja(int id, string imePrezime)
    {
        var odgajatelj = new Odgajatelj { Id = id, ImePrezime = imePrezime, Email = $"odgajatelj{id}@mail.ba" };
        _context.Odgajatelji.Add(odgajatelj);
        _context.SaveChanges();
        return odgajatelj;
    }

    private Roditelj DodajRoditelja(int id, string imePrezime, string? email)
    {
        var roditelj = new Roditelj { Id = id, ImePrezime = imePrezime, Email = email };
        _context.Roditelji.Add(roditelj);
        _context.SaveChanges();
        return roditelj;
    }

    private (Odgajatelj odgajatelj, Roditelj ana) DodajOdgajateljaIRoditelja() =>
        (DodajOdgajatelja(OdgajateljId, "Edina"), DodajRoditelja(AnaId, "Ana", "ana@mail.ba"));

    private Obavijest DodajObavijest(Odgajatelj odgajatelj, Roditelj roditelj, string naslov, DateTime kreirano,
        StatusObavijesti status = StatusObavijesti.POSLANA, DateTime? slano = null)
    {
        var obavijest = new Obavijest
        {
            Naslov = naslov, Sadrzaj = "Sadržaj", DatumKreiranja = kreirano, DatumSlanja = slano,
            KanalSlanja = KanalSlanja.APLIKACIJA, StatusObavijesti = status,
            RoditeljId = roditelj.Id, OdgajateljId = odgajatelj.Id
        };
        _context.Obavijesti.Add(obavijest);
        _context.SaveChanges();
        return obavijest;
    }

    // Odgajatelj 5 ima grupe 1 i 2; odgajatelj 6 ima grupu 3.
    // Ana (10) ima dvoje djece u grupi 1, Bosa (11) dijete u grupi 2, Cana (12) dijete u tuđoj grupi 3.
    private void PripremiScenarijSlanja(string? emailAne = "ana@mail.ba")
    {
        var odgajatelj = DodajOdgajatelja(OdgajateljId, "Edina");
        var tudjiOdgajatelj = DodajOdgajatelja(6, "Drugi");
        var grupa1 = DodajGrupu(1, odgajatelj);
        var grupa2 = DodajGrupu(2, odgajatelj);
        var tudjaGrupa = DodajGrupu(3, tudjiOdgajatelj);
        var ana = DodajRoditelja(AnaId, "Ana", emailAne);
        var bosa = DodajRoditelja(BosaId, "Bosa", "bosa@mail.ba");
        var cana = DodajRoditelja(CanaId, "Cana", "cana@mail.ba");
        DodajDijete(1, ana, grupa1);
        DodajDijete(2, ana, grupa1);
        DodajDijete(3, bosa, grupa2);
        DodajDijete(4, cana, tudjaGrupa);
        PrijaviKorisnika(OdgajateljId);
    }

    private Grupa DodajGrupu(int id, Odgajatelj odgajatelj)
    {
        var grupa = new Grupa { Id = id, ImeGrupe = $"Grupa {id}", OdgajateljId = odgajatelj.Id };
        _context.Grupe.Add(grupa);
        _context.SaveChanges();
        return grupa;
    }

    private void DodajDijete(int id, Roditelj roditelj, Grupa grupa)
    {
        _context.Djeca.Add(new Dijete
        {
            Id = id, ImePrezime = $"Dijete {id}", IdentifikacioniKod = $"KOD-{id}",
            RoditeljId = roditelj.Id, GrupaId = grupa.Id, Aktivno = true
        });
        _context.SaveChanges();
    }

    private int[] PrimaociIzBaze() =>
        _context.Obavijesti.Select(o => o.RoditeljId).Distinct().OrderBy(id => id).ToArray();

    private static PosaljiObavijestInputModel NoviUnos(string prima = "roditelji", bool email = false, bool aplikacija = true) =>
        new() { Naslov = "Izlet", Poruka = "Idemo u park.", Prima = prima, PutemEmaila = email, PutemAplikacije = aplikacija };

    private static T Kao<T>(IActionResult rezultat) where T : class
    {
        Assert.IsInstanceOfType<T>(rezultat);
        return (T)rezultat;
    }

    private static T ModelIz<T>(IActionResult rezultat) where T : class
    {
        var view = Kao<ViewResult>(rezultat);
        Assert.IsInstanceOfType<T>(view.Model);
        return (T)view.Model!;
    }
}