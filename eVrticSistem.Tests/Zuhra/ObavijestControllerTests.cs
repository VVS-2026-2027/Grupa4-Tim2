
using EVrtic.Controllers;
using EVrtic.Data;
using EVrtic.Models;
using EVrtic.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using static eVrticSistem.Tests.Zuhra.ZuhraPomocne;
namespace eVrticSistem.Tests.Zuhra;

[TestClass]
[DoNotParallelize]
public class ObavijestControllerTests
{
    private const int OdgajateljId = 5;
    private const int AnaId = 10;
    private const int BosaId = 11;
    private const int CanaId = 12;

    private static readonly DateTime Trenutak =
        new(2026, 5, 4, 10, 30, 15);

    private static DbContextOptions<ApplicationDbContext> _opcije = null!;

    private ApplicationDbContext _context = null!;
    private GraditeljPodataka _podaci = null!;
    private Mock<UserManager<Korisnik>> _userManagerMock = null!;
    private Mock<IEmailService> _emailMock = null!;
    private ObavijestController _controller = null!;


    [ClassInitialize]
    public static void PripremiKlasu(TestContext testContext)
    {
        _opcije = TestOkruzenje.NapraviFakeBazu(
            testContext.FullyQualifiedTestClassName!);
    }

    [TestInitialize]
    public void PripremiTest()
    {
        _context = new ApplicationDbContext(_opcije);
        _podaci = new GraditeljPodataka(_context);
        _userManagerMock = TestOkruzenje.NapraviUserManagerMock();

        _emailMock = new Mock<IEmailService>();

        _emailMock
            .Setup(e => e.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(true);

        _controller = new ObavijestController(
            _context,
            _userManagerMock.Object,
            _emailMock.Object);

        TestOkruzenje.PostaviKontekst(_controller);
    }

    [TestCleanup]
    public void PocistiTest()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [ClassCleanup]
    public static void PocistiKlasu()
    {
        _opcije = null!;
    }

    // ===== Onemogućene akcije =====

    [TestMethod]
    public async Task Index_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(
            await _controller.Index());

    [TestMethod]
    public async Task Details_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(
            await _controller.Details(1));

    [TestMethod]
    public void CreateGet_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(
            _controller.Create());

    [TestMethod]
    public async Task CreatePost_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(
            await _controller.Create(new Obavijest()));

    [TestMethod]
    public async Task EditGet_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(
            await _controller.Edit(1));

    [TestMethod]
    public async Task EditPost_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(
            await _controller.Edit(1, new Obavijest()));

    [TestMethod]
    public async Task DeleteGet_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(
            await _controller.Delete(1));

    // ===== DeleteConfirmed =====

    [TestMethod]
    public async Task DeleteConfirmed_PostojecaObavijest_BriseJeIPreusmjeravaNaIndex()
    {
        var (odgajatelj, roditelj) = DodajOdgajateljaIRoditelja();

        var obavijest = _podaci.DodajObavijest(
            odgajatelj, roditelj, "Naslov", Trenutak);

        var rezultat = await _controller.DeleteConfirmed(obavijest.Id);

        Assert.AreEqual(
            nameof(_controller.Index),
            Kao<RedirectToActionResult>(rezultat).ActionName);

        Assert.AreEqual(0, _context.Obavijesti.Count());
    }

    [TestMethod]
    public async Task DeleteConfirmed_NepostojecaObavijest_PreusmjeravaNaIndexBezBrisanja()
    {
        var rezultat = await _controller.DeleteConfirmed(999);

        Assert.AreEqual(
            nameof(_controller.Index),
            Kao<RedirectToActionResult>(rezultat).ActionName);
    }

    // ===== RoditeljPregled =====

    [TestMethod]
    public async Task RoditeljPregled_BezPrijavljenogKorisnika_PreusmjeravaNaLogin()
    {
        _userManagerMock.NikoNijePrijavljen();

        var rezultat = await _controller.RoditeljPregled();

        var redirect = Kao<RedirectToPageResult>(rezultat);

        Assert.AreEqual("/Account/Login", redirect.PageName);
        Assert.AreEqual("Identity", redirect.RouteValues!["area"]);
    }

    [TestMethod]
    public async Task RoditeljPregled_RoditeljSaObavijestima_VracaSamoNjegoveObavijesti()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();

        var bosa = _podaci.DodajRoditelja(
            BosaId, "Bosa", "bosa@mail.ba");

        _podaci.DodajObavijest(odgajatelj, ana, "Anina", Trenutak);
        _podaci.DodajObavijest(odgajatelj, bosa, "Bosina", Trenutak);

        _userManagerMock.PrijaviKorisnika(AnaId);

        var model = ModelIz<RoditeljObavijestiViewModel>(
            await _controller.RoditeljPregled());

        CollectionAssert.AreEqual(
            new[] { "Anina" },
            model.Obavijesti.Select(o => o.Naslov).ToArray());
    }

    [TestMethod]
    public async Task RoditeljPregled_ViseObavijesti_SortiraPoDatumuSlanjaPaKreiranja()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();

        _podaci.DodajObavijest(
            odgajatelj, ana, "KreiranaRanije",
            Trenutak, slano: Trenutak.AddDays(5));

        _podaci.DodajObavijest(
            odgajatelj, ana, "KreiranaKasnije",
            Trenutak.AddDays(3), slano: null);

        _userManagerMock.PrijaviKorisnika(AnaId);

        var model = ModelIz<RoditeljObavijestiViewModel>(
            await _controller.RoditeljPregled());

        var ocekivano = new[]
        {
            "KreiranaRanije",
            "KreiranaKasnije"
        };

        CollectionAssert.AreEqual(
            ocekivano,
            model.Obavijesti.Select(o => o.Naslov).ToArray());
    }

    [TestMethod]
    [DataRow(StatusObavijesti.POSLANA, 1)]
    [DataRow(StatusObavijesti.KREIRANA, 1)]
    [DataRow(StatusObavijesti.PROCITANA, 0)]
    public async Task RoditeljPregled_ObavijestSaStatusom_ISpravnoBrojiNove(
        StatusObavijesti status,
        int ocekivanoNovih)
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();

        _podaci.DodajObavijest(
            odgajatelj, ana, "Naslov", Trenutak, status);

        _userManagerMock.PrijaviKorisnika(AnaId);

        var model = ModelIz<RoditeljObavijestiViewModel>(
            await _controller.RoditeljPregled());

        Assert.AreEqual(ocekivanoNovih, model.BrojNovih);
    }

    [TestMethod]
    public async Task RoditeljPregled_RoditeljBezObavijesti_VracaPrazanModel()
    {
        DodajOdgajateljaIRoditelja();
        _userManagerMock.PrijaviKorisnika(AnaId);

        var model = ModelIz<RoditeljObavijestiViewModel>(
            await _controller.RoditeljPregled());

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
        _userManagerMock.NikoNijePrijavljen();

        var rezultat = await _controller.RoditeljDetalji(1);

        Assert.AreEqual(
            "/Account/Login",
            Kao<RedirectToPageResult>(rezultat).PageName);
    }

    [TestMethod]
    public async Task RoditeljDetalji_NepostojecaObavijest_VracaNotFound()
    {
        _userManagerMock.PrijaviKorisnika(AnaId);

        var rezultat = await _controller.RoditeljDetalji(999);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task RoditeljDetalji_ObavijestDrugogRoditelja_VracaNotFound()
    {
        var (odgajatelj, _) = DodajOdgajateljaIRoditelja();

        var bosa = _podaci.DodajRoditelja(
            BosaId, "Bosa", "bosa@mail.ba");

        var tudja = _podaci.DodajObavijest(
            odgajatelj, bosa, "Bosina", Trenutak);

        _userManagerMock.PrijaviKorisnika(AnaId);

        var rezultat = await _controller.RoditeljDetalji(tudja.Id);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task RoditeljDetalji_NeprocitanaObavijest_OznacavaJeProcitanom()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();

        var obavijest = _podaci.DodajObavijest(
            odgajatelj, ana, "Naslov",
            Trenutak, StatusObavijesti.POSLANA);

        _userManagerMock.PrijaviKorisnika(AnaId);

        await _controller.RoditeljDetalji(obavijest.Id);

        Assert.AreEqual(
            StatusObavijesti.PROCITANA,
            _context.Obavijesti.Single().StatusObavijesti);
    }

    [TestMethod]
    public async Task RoditeljDetalji_PostojecaObavijest_VracaViewSaObavijesti()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();

        var obavijest = _podaci.DodajObavijest(
            odgajatelj, ana, "Naslov",
            Trenutak, StatusObavijesti.PROCITANA);

        _userManagerMock.PrijaviKorisnika(AnaId);

        var model = ModelIz<Obavijest>(
            await _controller.RoditeljDetalji(obavijest.Id));

        Assert.AreEqual(obavijest.Id, model.Id);
    }

    // ===== OdgajateljObavijesti =====

    [TestMethod]
    public async Task OdgajateljObavijesti_BezPrijavljenogKorisnika_VracaChallenge()
    {
        _userManagerMock.NikoNijePrijavljen();

        var rezultat = await _controller.OdgajateljObavijesti();

        Assert.IsInstanceOfType<ChallengeResult>(rezultat);
    }

    [TestMethod]
    public async Task OdgajateljObavijesti_PrijavljenOdgajatelj_VracaModelSaNjegovimId()
    {
        _userManagerMock.PrijaviKorisnika(OdgajateljId);

        var model = ModelIz<OdgajateljObavijestViewModel>(
            await _controller.OdgajateljObavijesti());

        Assert.AreEqual(OdgajateljId, model.TrenutniKorisnikId);
    }

    [TestMethod]
    public async Task OdgajateljObavijesti_ObavijestPoslanaViseRoditelja_PrikazujeJedanRed()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();

        var bosa = _podaci.DodajRoditelja(
            BosaId, "Bosa", "bosa@mail.ba");

        _podaci.DodajObavijest(odgajatelj, ana, "Izlet", Trenutak);
        _podaci.DodajObavijest(odgajatelj, bosa, "Izlet", Trenutak);

        _userManagerMock.PrijaviKorisnika(OdgajateljId);

        var model = ModelIz<OdgajateljObavijestViewModel>(
            await _controller.OdgajateljObavijesti());

        Assert.AreEqual(1, model.Obavijesti.Count);
    }

    [TestMethod]
    public async Task OdgajateljObavijesti_RazlicitiNaslovi_PrikazujeSveGrupeSortiranePoDatumu()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();

        _podaci.DodajObavijest(odgajatelj, ana, "Starija", Trenutak);
        _podaci.DodajObavijest(
            odgajatelj, ana, "Novija", Trenutak.AddDays(1));

        _userManagerMock.PrijaviKorisnika(OdgajateljId);

        var model = ModelIz<OdgajateljObavijestViewModel>(
            await _controller.OdgajateljObavijesti());

        CollectionAssert.AreEqual(
            new[] { "Novija", "Starija" },
            model.Obavijesti.Select(o => o.Naslov).ToArray());
    }

    [TestMethod]
    public async Task OdgajateljObavijesti_IstiNaslovAliRazlicitaSekunda_PrikazujeObaReda()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();

        _podaci.DodajObavijest(odgajatelj, ana, "Izlet", Trenutak);
        _podaci.DodajObavijest(
            odgajatelj, ana, "Izlet", Trenutak.AddSeconds(5));

        _userManagerMock.PrijaviKorisnika(OdgajateljId);

        var model = ModelIz<OdgajateljObavijestViewModel>(
            await _controller.OdgajateljObavijesti());

        Assert.AreEqual(2, model.Obavijesti.Count);
    }

    // ===== PosaljiObavijest (GET) =====

    [TestMethod]
    public async Task PosaljiObavijestGet_BezPrijavljenogKorisnika_VracaChallenge()
    {
        _userManagerMock.NikoNijePrijavljen();

        var rezultat = await _controller.PosaljiObavijest();

        Assert.IsInstanceOfType<ChallengeResult>(rezultat);
    }

    [TestMethod]
    public async Task PosaljiObavijestGet_PrijavljenOdgajatelj_VracaModelSaOdgajateljemIGrupama()
    {
        PripremiScenarijSlanja();

        var model = ModelIz<PosaljiObavijestViewModel>(
            await _controller.PosaljiObavijest());

        Assert.AreEqual(OdgajateljId, model.Odgajatelj!.Id);
        Assert.AreEqual(2, model.Odgajatelj.Grupe.Count);
    }

    [TestMethod]
    public async Task PosaljiObavijestGet_KorisnikNijeOdgajatelj_OdgajateljUModeluJeNull()
    {
        _userManagerMock.PrijaviKorisnika(99);

        var model = ModelIz<PosaljiObavijestViewModel>(
            await _controller.PosaljiObavijest());

        Assert.IsNull(model.Odgajatelj);
    }

    // ===== PosaljiObavijest (POST): autorizacija =====

    [TestMethod]
    public async Task PosaljiObavijestPost_BezPrijavljenogKorisnika_VracaUnauthorized()
    {
        _userManagerMock.NikoNijePrijavljen();

        var rezultat = await _controller.PosaljiObavijest(NoviUnos());

        Assert.IsInstanceOfType<UnauthorizedResult>(rezultat);
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_KorisnikNijeOdgajatelj_VracaForbid()
    {
        _userManagerMock.PrijaviKorisnika(99);

        var rezultat = await _controller.PosaljiObavijest(NoviUnos());

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    // ===== PosaljiObavijest (POST): primaoci =====

    [TestMethod]
    public async Task PosaljiObavijestPost_PrimaSvi_ObavijestiSveRoditelje()
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(NoviUnos(prima: "svi"));

        CollectionAssert.AreEquivalent(
            new[] { AnaId, BosaId, CanaId },
            PrimaociIzBaze());
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_PrimaVlastitaGrupa_ObavijestiSamoRoditeljeTeGrupeBezDuplikata()
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(NoviUnos(prima: "grupa:1"));

        CollectionAssert.AreEqual(
            new[] { AnaId },
            PrimaociIzBaze());
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_PrimaTudjaGrupa_NemaPrimalacaIVracaGresku()
    {
        PripremiScenarijSlanja();

        var rezultat = await _controller.PosaljiObavijest(
            NoviUnos(prima: "grupa:3"));

        Assert.AreEqual(
            "Nema roditelja koji se mogu obavijestiti.",
            _controller.TempData["Greska"]);

        Assert.AreEqual(0, _context.Obavijesti.Count());

        Assert.AreEqual(
            nameof(_controller.OdgajateljObavijesti),
            Kao<RedirectToActionResult>(rezultat).ActionName);
    }

    // Zamjena za DynamicData: podaci su direktno u DataRow atributima.

    [TestMethod]
  
    [DataRow("grupa:abc")]
    [DataRow("grupa:999")]
    [DataRow("grupa:-1")]
    public async Task PosaljiObavijestPost_NeispravanOdabirGrupe_NemaPrimalacaIVracaGresku(
        string prima)
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(NoviUnos(prima: prima));

        Assert.AreEqual(
            "Nema roditelja koji se mogu obavijestiti.",
            _controller.TempData["Greska"]);

        Assert.AreEqual(0, _context.Obavijesti.Count());
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_PodrazumijevaniOdabir_ObavijestiRoditeljeDjeceIzSvihVlastitihGrupa()
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(NoviUnos(prima: "roditelji"));

        CollectionAssert.AreEquivalent(
            new[] { AnaId, BosaId },
            PrimaociIzBaze());
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_OdgajateljBezGrupa_VracaGresku()
    {
        _podaci.DodajOdgajatelja(OdgajateljId, "Edina");
        _userManagerMock.PrijaviKorisnika(OdgajateljId);

        await _controller.PosaljiObavijest(NoviUnos(prima: "roditelji"));

        Assert.AreEqual(
            "Nema roditelja koji se mogu obavijestiti.",
            _controller.TempData["Greska"]);
    }

    // ===== PosaljiObavijest (POST): sadržaj zapisa =====

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

        Assert.AreEqual(
            1,
            _context.Obavijesti
                .Select(o => o.DatumKreiranja)
                .Distinct()
                .Count());

        Assert.IsTrue(
            _context.Obavijesti.All(
                o => o.DatumSlanja == o.DatumKreiranja));
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
    public async Task PosaljiObavijestPost_OdabraniKanali_ODredujuKanalSlanja(
        bool email,
        bool aplikacija,
        KanalSlanja ocekivani)
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(
            NoviUnos(
                prima: "grupa:1",
                email: email,
                aplikacija: aplikacija));

        Assert.AreEqual(
            ocekivani,
            _context.Obavijesti.Single().KanalSlanja);
    }

    // ===== PosaljiObavijest (POST): email =====

    [TestMethod]
    public async Task PosaljiObavijestPost_BezEmaila_NeSaljeEmailIPostavljaPorukuUspjeha()
    {
        PripremiScenarijSlanja();

        var rezultat = await _controller.PosaljiObavijest(
            NoviUnos(prima: "svi", email: false));

        Assert.AreEqual(
            "Obavijest uspješno poslana.",
            _controller.TempData["Uspjeh"]);

        Assert.AreEqual(
            nameof(_controller.OdgajateljObavijesti),
            Kao<RedirectToActionResult>(rezultat).ActionName);

        _emailMock.Verify(
            e => e.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_SaEmailom_SaljeJedanEmailPoRoditelju()
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(
            NoviUnos(prima: "svi", email: true));

        _emailMock.Verify(
            e => e.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Exactly(3));
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_SaEmailom_EmailImaIspravanNaslovIPrimaoca()
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(
            NoviUnos(prima: "grupa:1", email: true));

        _emailMock.Verify(
            e => e.SendAsync(
                "ana@mail.ba",
                "[eVrtić] Izlet",
                It.IsAny<string>()),
            Times.Once);
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_SaEmailom_TijeloEmailaSadrziImenaIPoruku()
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(
            NoviUnos(prima: "grupa:1", email: true));

        _emailMock.Verify(
            e => e.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(
                    tijelo =>
                        tijelo.Contains("Ana") &&
                        tijelo.Contains("Idemo u park.") &&
                        tijelo.Contains("Edina"))),
            Times.Once);
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_SviEmailoviDostavljeni_PrijavljujeBrojAdresa()
    {
        PripremiScenarijSlanja();

        await _controller.PosaljiObavijest(
            NoviUnos(prima: "svi", email: true));

        Assert.AreEqual(
            "Obavijest poslana. Email dostavljen na 3 adresa.",
            _controller.TempData["Uspjeh"]);
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_JedanEmailNeuspjesan_PrijavljujeDjelimicanUspjeh()
    {
        PripremiScenarijSlanja();

        _emailMock
            .Setup(e => e.SendAsync(
                "bosa@mail.ba",
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(false);

        await _controller.PosaljiObavijest(
            NoviUnos(prima: "svi", email: true));

        var ocekivano =
            "Obavijest poslana. Email dostavljen na 2 adresa. " +
            "(1 adresa nije bilo moguće obavijestiti emailom.)";

        Assert.AreEqual(ocekivano, _controller.TempData["Uspjeh"]);
    }

    [TestMethod]
    public async Task PosaljiObavijestPost_SviEmailoviNeuspjesni_PrijavljujeSlanjePutemAplikacije()
    {
        PripremiScenarijSlanja();

        _emailMock
            .Setup(e => e.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(false);

        await _controller.PosaljiObavijest(
            NoviUnos(prima: "svi", email: true));

        var ocekivano =
            "Obavijest poslana putem aplikacije. " +
            "(Email slanje nije uspjelo ni za jednu adresu.)";

        Assert.AreEqual(ocekivano, _controller.TempData["Uspjeh"]);
    }

    // Zamjena za DynamicData: svi slučajevi su navedeni kroz DataRow.

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task PosaljiObavijestPost_RoditeljBezEmailAdrese_NeSaljeEmailIPrijavljujeNeuspjeh(
        string? email)
    {
        PripremiScenarijSlanja(emailAne: email);

        await _controller.PosaljiObavijest(
            NoviUnos(prima: "grupa:1", email: true));

        _emailMock.Verify(
            e => e.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);

        StringAssert.Contains(
            (string)_controller.TempData["Uspjeh"]!,
            "Email slanje nije uspjelo ni za jednu adresu.");
    }

    // ===== ObrisiBatch =====

    [TestMethod]
    public async Task ObrisiBatch_BezPrijavljenogKorisnika_VracaUnauthorized()
    {
        _userManagerMock.NikoNijePrijavljen();

        var rezultat = await _controller.ObrisiBatch(1);

        Assert.IsInstanceOfType<UnauthorizedResult>(rezultat);
    }

    [TestMethod]
    public async Task ObrisiBatch_NepostojecaObavijest_VracaNotFound()
    {
        _userManagerMock.PrijaviKorisnika(OdgajateljId);

        var rezultat = await _controller.ObrisiBatch(999);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task ObrisiBatch_ObavijestDrugogOdgajatelja_VracaForbidBezBrisanja()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();

        var obavijest = _podaci.DodajObavijest(
            odgajatelj, ana, "Naslov", Trenutak);

        _userManagerMock.PrijaviKorisnika(99);

        var rezultat = await _controller.ObrisiBatch(obavijest.Id);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
        Assert.AreEqual(1, _context.Obavijesti.Count());
    }

    [TestMethod]
    public async Task ObrisiBatch_ViseZapisaIsteAkcije_BriseSveZapiseTeAkcije()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();

        var bosa = _podaci.DodajRoditelja(
            BosaId, "Bosa", "bosa@mail.ba");

        var prva = _podaci.DodajObavijest(
            odgajatelj, ana, "Izlet", Trenutak);

        _podaci.DodajObavijest(
            odgajatelj, bosa, "Izlet", Trenutak);

        _userManagerMock.PrijaviKorisnika(OdgajateljId);

        await _controller.ObrisiBatch(prva.Id);

        Assert.AreEqual(0, _context.Obavijesti.Count());
    }

    [TestMethod]
    public async Task ObrisiBatch_PostojiDrugaAkcija_NjenZapisNeBrise()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();

        var meta = _podaci.DodajObavijest(
            odgajatelj, ana, "Izlet", Trenutak);

        _podaci.DodajObavijest(
            odgajatelj, ana, "Izlet", Trenutak.AddSeconds(5));

        _podaci.DodajObavijest(
            odgajatelj, ana, "Drugi naslov", Trenutak);

        _userManagerMock.PrijaviKorisnika(OdgajateljId);

        await _controller.ObrisiBatch(meta.Id);

        Assert.AreEqual(2, _context.Obavijesti.Count());
    }

    [TestMethod]
    public async Task ObrisiBatch_UspjesnoBrisanje_PreusmjeravaNaPregledSaPorukom()
    {
        var (odgajatelj, ana) = DodajOdgajateljaIRoditelja();

        var obavijest = _podaci.DodajObavijest(
            odgajatelj, ana, "Izlet", Trenutak);

        _userManagerMock.PrijaviKorisnika(OdgajateljId);

        var rezultat = await _controller.ObrisiBatch(obavijest.Id);

        Assert.AreEqual(
            nameof(_controller.OdgajateljObavijesti),
            Kao<RedirectToActionResult>(rezultat).ActionName);

        Assert.AreEqual(
            "Obavijest je obrisana.",
            _controller.TempData["Uspjeh"]);
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
        Assert.AreEqual(
            0,
            new OdgajateljObavijestViewModel().Obavijesti.Count);

        Assert.AreEqual(
            0,
            new RoditeljObavijestiViewModel().Obavijesti.Count);

        Assert.IsNull(new PosaljiObavijestViewModel().Odgajatelj);
    }

    // ===== Pomoćne metode =====

    private (Odgajatelj odgajatelj, Roditelj ana)
        DodajOdgajateljaIRoditelja() =>
        (
            _podaci.DodajOdgajatelja(OdgajateljId, "Edina"),
            _podaci.DodajRoditelja(AnaId, "Ana", "ana@mail.ba")
        );

    // Odgajatelj 5 ima grupe 1 i 2.
    // Odgajatelj 6 ima grupu 3.
    // Ana (10) ima dvoje djece u grupi 1.
    // Bosa (11) ima dijete u grupi 2.
    // Cana (12) ima dijete u tuđoj grupi 3.
    private void PripremiScenarijSlanja(string? emailAne = "ana@mail.ba")
    {
        var odgajatelj =
            _podaci.DodajOdgajatelja(OdgajateljId, "Edina");

        var tudjiOdgajatelj =
            _podaci.DodajOdgajatelja(6, "Drugi");

        var grupa1 =
            _podaci.DodajGrupu(1, odgajatelj.Id);

        var grupa2 =
            _podaci.DodajGrupu(2, odgajatelj.Id);

        var tudjaGrupa =
            _podaci.DodajGrupu(3, tudjiOdgajatelj.Id);

        var ana =
            _podaci.DodajRoditelja(AnaId, "Ana", emailAne);

        var bosa =
            _podaci.DodajRoditelja(BosaId, "Bosa", "bosa@mail.ba");

        var cana =
            _podaci.DodajRoditelja(CanaId, "Cana", "cana@mail.ba");

        _podaci.DodajDijete(1, "Dijete 1", ana.Id, grupa1);
        _podaci.DodajDijete(2, "Dijete 2", ana.Id, grupa1);
        _podaci.DodajDijete(3, "Dijete 3", bosa.Id, grupa2);
        _podaci.DodajDijete(4, "Dijete 4", cana.Id, tudjaGrupa);

        _userManagerMock.PrijaviKorisnika(OdgajateljId);
    }

    private int[] PrimaociIzBaze() =>
        _context.Obavijesti
            .Select(o => o.RoditeljId)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

    private static PosaljiObavijestInputModel NoviUnos(
        string prima = "roditelji",
        bool email = false,
        bool aplikacija = true) =>
        new()
        {
            Naslov = "Izlet",
            Poruka = "Idemo u park.",
            Prima = prima,
            PutemEmaila = email,
            PutemAplikacije = aplikacija
        };
}
