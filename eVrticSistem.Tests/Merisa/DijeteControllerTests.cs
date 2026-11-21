using System.Security.Claims;
using EVrtic.Controllers;
using EVrtic.Data;
using EVrtic.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace eVrticSistem.Tests.Merisa;

[TestClass]
public class DijeteControllerTests
{
    private ApplicationDbContext _baza = null!;
    private Mock<UserManager<Korisnik>> _upraviteljKorisnika = null!;
    private DijeteController _kontroler = null!;

    [TestInitialize]
    public void PripremiKontroler()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _baza = new ApplicationDbContext(opcije);

        _upraviteljKorisnika = new Mock<UserManager<Korisnik>>(
            Mock.Of<IUserStore<Korisnik>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);

        _kontroler = new DijeteController(_baza, _upraviteljKorisnika.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        _kontroler.TempData = new TempDataDictionary(
            _kontroler.HttpContext, Mock.Of<ITempDataProvider>());
    }

    [TestCleanup]
    public void OcistiBazu()
    {
        _baza.Dispose();
    }

    private void PrijaviKorisnika(int id)
    {
        _upraviteljKorisnika
            .Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new Korisnik { Id = id });
    }

    private void NemaPrijavljenogKorisnika()
    {
        _upraviteljKorisnika
            .Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((Korisnik?)null);
    }

    private async Task<Dijete> DodajDijete(
        string ime = "Amar Hadžić",
        int? roditeljId = null,
        int? grupaId = null,
        bool aktivno = true,
        string kod = "ABCD1234")
    {
        var dijete = new Dijete
        {
            ImePrezime = ime,
            IdentifikacioniKod = kod,
            DatumRodjenja = DateTime.Today.AddYears(-4),
            DodatnaNapomena = string.Empty,
            RoditeljId = roditeljId,
            GrupaId = grupaId,
            Aktivno = aktivno
        };
        _baza.Djeca.Add(dijete);
        await _baza.SaveChangesAsync();
        return dijete;
    }

    private async Task<Grupa> DodajGrupu(string ime = "Leptirići", int? odgajateljId = null)
    {
        var grupa = new Grupa { ImeGrupe = ime, OdgajateljId = odgajateljId };
        _baza.Grupe.Add(grupa);
        await _baza.SaveChangesAsync();
        return grupa;
    }

    private static UnosPodatakaViewModel NapraviModel(int dijeteId) => new()
    {
        DijeteId = dijeteId,
        ImePrezime = "Amar Hadžić",
        DatumRodjenja = DateTime.Today.AddYears(-4),
        DodatnaNapomena = "Nema napomene",
        Alergije = new List<string> { "Kikiriki" },
        Bolesti = new List<string> { "Astma" }
    };

    public static IEnumerable<object[]> NevalidnaImena()
    {
        yield return new object[] { "Amar", "samo jedna riječ" };
        yield return new object[] { "amar hadžić", "prva riječ malim slovom" };
        yield return new object[] { "Amar hadžić", "druga riječ malim slovom" };
        yield return new object[] { "Amar Hadž1ć", "broj unutar riječi" };
        yield return new object[] { "Amar H.", "tačka u prezimenu" };
        yield return new object[] { "Amar Hadžić-Begić", "crtica nije dozvoljena" };
        yield return new object[] { "", "prazno" };
        yield return new object[] { "   ", "samo razmaci" };
    }

    public static IEnumerable<object[]> DatumiRodjenja()
    {
        yield return new object[] { "tačno 3 mjeseca", 0, 3, 0, true };
        yield return new object[] { "tačno 7 godina", 7, 0, 0, true };
        yield return new object[] { "dijete od 4 godine", 4, 0, 0, true };
        yield return new object[] { "dan mlađe od 3 mjeseca", 0, 3, -1, false };
        yield return new object[] { "dan starije od 7 godina", 7, 0, 1, false };
        yield return new object[] { "datum u budućnosti", 0, 0, -10, false };
        yield return new object[] { "odrasla osoba", 30, 0, 0, false };
    }

    public static IEnumerable<object[]> NevalidniKodovi()
    {
        yield return new object[] { "", "prazan kod" };
        yield return new object[] { "   ", "samo razmaci" };
        yield return new object[] { "ABC", "prekratak kod" };
        yield return new object[] { "ABCDEFG", "7 karaktera" };
        yield return new object[] { "ABCDEFGHI", "9 karaktera" };
        yield return new object[] { "ABCDEFGHIJKLMNOP", "predugačak kod" };
    }

    [TestMethod]
    public async Task Index_VracaViewSaSvomDjecom()
    {
        await DodajDijete(kod: "AAAAAAA1");
        await DodajDijete(kod: "AAAAAAA2");

        var rezultat = await _kontroler.Index();

        var view = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var model = Assert.IsInstanceOfType<List<Dijete>>(view.Model);
        Assert.AreEqual(2, model.Count);
    }

    [TestMethod]
    public async Task Index_PrazanaBaza_VracaPraznuListu()
    {
        var rezultat = await _kontroler.Index();

        var view = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var model = Assert.IsInstanceOfType<List<Dijete>>(view.Model);
        Assert.AreEqual(0, model.Count);
    }

    [TestMethod]
    public async Task Details_NullId_VracaNotFound()
    {
        var rezultat = await _kontroler.Details(null);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task Details_NepostojeciId_VracaNotFound()
    {
        var rezultat = await _kontroler.Details(999);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task Details_PostojeceDijete_VracaViewSaDjetetom()
    {
        var dijete = await DodajDijete();

        var rezultat = await _kontroler.Details(dijete.Id);

        var view = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var model = Assert.IsInstanceOfType<Dijete>(view.Model);
        Assert.AreEqual(dijete.Id, model.Id);
    }

    [TestMethod]
    public async Task Details_PostojeceDijete_PopunjavaListuGrupaSortiranuPoImenu()
    {
        await DodajGrupu("Zvjezdice");
        await DodajGrupu("Leptirići");
        var dijete = await DodajDijete();

        var rezultat = await _kontroler.Details(dijete.Id);

        var view = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var grupe = Assert.IsInstanceOfType<List<Grupa>>(view.ViewData["SveGrupe"]);
        Assert.AreEqual("Leptirići", grupe[0].ImeGrupe);
        Assert.AreEqual("Zvjezdice", grupe[1].ImeGrupe);
    }

    [TestMethod]
    public async Task Deaktiviraj_AktivnoDijete_PostavljaAktivnoNaFalse()
    {
        var dijete = await DodajDijete(aktivno: true);

        await _kontroler.Deaktiviraj(dijete.Id);

        var iz_baze = await _baza.Djeca.FindAsync(dijete.Id);
        Assert.IsFalse(iz_baze!.Aktivno);
    }

    [TestMethod]
    public async Task Deaktiviraj_PostojeceDijete_PreusmjeravaNaDetailsSaPorukom()
    {
        var dijete = await DodajDijete(ime: "Amar Hadžić");

        var rezultat = await _kontroler.Deaktiviraj(dijete.Id);

        var preusmjeravanje = Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual("Details", preusmjeravanje.ActionName);
        Assert.AreEqual("Profil djeteta \"Amar Hadžić\" je deaktiviran.", _kontroler.TempData["Uspjeh"]);
    }

    [TestMethod]
    public async Task Deaktiviraj_NepostojeceDijete_VracaNotFound()
    {
        var rezultat = await _kontroler.Deaktiviraj(999);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task Aktiviraj_NeaktivnoDijete_PostavljaAktivnoNaTrue()
    {
        var dijete = await DodajDijete(aktivno: false);

        await _kontroler.Aktiviraj(dijete.Id);

        var iz_baze = await _baza.Djeca.FindAsync(dijete.Id);
        Assert.IsTrue(iz_baze!.Aktivno);
    }

    [TestMethod]
    public async Task Aktiviraj_PostojeceDijete_PreusmjeravaNaDetailsSaPorukom()
    {
        var dijete = await DodajDijete(ime: "Amar Hadžić", aktivno: false);

        var rezultat = await _kontroler.Aktiviraj(dijete.Id);

        var preusmjeravanje = Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual("Details", preusmjeravanje.ActionName);
        Assert.AreEqual("Profil djeteta \"Amar Hadžić\" je ponovo aktiviran.", _kontroler.TempData["Uspjeh"]);
    }

    [TestMethod]
    public async Task Aktiviraj_NepostojeceDijete_VracaNotFound()
    {
        var rezultat = await _kontroler.Aktiviraj(999);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task PromijeniGrupu_ValidnaGrupa_DodjeljujeGrupuDjetetu()
    {
        var grupa = await DodajGrupu();
        var dijete = await DodajDijete();

        var rezultat = await _kontroler.PromijeniGrupu(dijete.Id, grupa.Id);

        Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        var iz_baze = await _baza.Djeca.FindAsync(dijete.Id);
        Assert.AreEqual(grupa.Id, iz_baze!.GrupaId);
        Assert.AreEqual("Grupa djeteta je uspješno ažurirana.", _kontroler.TempData["Uspjeh"]);
    }

    [TestMethod]
    public async Task PromijeniGrupu_NullGrupa_UklanjaDijeteIzGrupe()
    {
        var grupa = await DodajGrupu();
        var dijete = await DodajDijete(grupaId: grupa.Id);

        await _kontroler.PromijeniGrupu(dijete.Id, null);

        var iz_baze = await _baza.Djeca.FindAsync(dijete.Id);
        Assert.IsNull(iz_baze!.GrupaId);
        Assert.AreEqual("Dijete je uklonjeno iz grupe.", _kontroler.TempData["Uspjeh"]);
    }

    [TestMethod]
    public async Task PromijeniGrupu_NepostojeceDijete_VracaNotFound()
    {
        var rezultat = await _kontroler.PromijeniGrupu(999, 1);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task Obrisi_NepostojeceDijete_VracaNotFound()
    {
        var rezultat = await _kontroler.Obrisi(999);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task Obrisi_PostojeceDijete_BriseDijeteIPreusmjeravaNaIndex()
    {
        var dijete = await DodajDijete(ime: "Amar Hadžić");

        var rezultat = await _kontroler.Obrisi(dijete.Id);

        var preusmjeravanje = Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual("Index", preusmjeravanje.ActionName);
        Assert.IsNull(await _baza.Djeca.FindAsync(dijete.Id));
        Assert.AreEqual("Profil djeteta \"Amar Hadžić\" je trajno obrisan iz sistema.", _kontroler.TempData["Uspjeh"]);
    }

    [TestMethod]
    public async Task Obrisi_DijeteSaAlergijamaIBolestima_BrisePovezaneZapise()
    {
        var dijete = await DodajDijete();
        _baza.AlergijeDjece.Add(new AlergijaDjeteta { DijeteId = dijete.Id, Naziv = "Kikiriki" });
        _baza.BolestiDjece.Add(new BolestDjeteta { DijeteId = dijete.Id, Naziv = "Astma" });
        await _baza.SaveChangesAsync();

        await _kontroler.Obrisi(dijete.Id);

        Assert.AreEqual(0, await _baza.AlergijeDjece.CountAsync());
        Assert.AreEqual(0, await _baza.BolestiDjece.CountAsync());
    }

    [TestMethod]
    public async Task Obrisi_DijeteBezImena_UPorukiKoristiIdentifikacioniKod()
    {
        var dijete = await DodajDijete(ime: string.Empty, kod: "KOD12345");

        await _kontroler.Obrisi(dijete.Id);

        Assert.AreEqual("Profil djeteta \"(kod: KOD12345)\" je trajno obrisan iz sistema.", _kontroler.TempData["Uspjeh"]);
    }

    [TestMethod]
    public void DodajIdentifikacioniKodGet_VracaView()
    {
        var rezultat = _kontroler.DodajIdentifikacioniKod();

        Assert.IsInstanceOfType<ViewResult>(rezultat);
    }

    [TestMethod]
    [DynamicData(nameof(NevalidniKodovi), DynamicDataSourceType.Method)]
    public async Task DodajIdentifikacioniKodPost_NevalidanKod_VracaViewSaGreskom(string kod, string razlog)
    {
        var rezultat = await _kontroler.DodajIdentifikacioniKod(new Dijete { IdentifikacioniKod = kod });

        var view = Assert.IsInstanceOfType<ViewResult>(rezultat, razlog);
        Assert.IsInstanceOfType<Dijete>(view.Model);
        Assert.IsTrue(_kontroler.ModelState.ContainsKey("IdentifikacioniKod"), razlog);
        Assert.AreEqual(0, await _baza.Djeca.CountAsync(), razlog);
    }

    [TestMethod]
    public async Task DodajIdentifikacioniKodPost_NullKod_VracaViewSaGreskom()
    {
        var rezultat = await _kontroler.DodajIdentifikacioniKod(new Dijete { IdentifikacioniKod = null! });

        Assert.IsInstanceOfType<ViewResult>(rezultat);
        Assert.IsTrue(_kontroler.ModelState.ContainsKey("IdentifikacioniKod"));
    }

    [TestMethod]
    public async Task UnosPodatakaGet_NijePrijavljen_VracaChallenge()
    {
        NemaPrijavljenogKorisnika();

        var rezultat = await _kontroler.UnosPodataka(1);

        Assert.IsInstanceOfType<ChallengeResult>(rezultat);
    }

    [TestMethod]
    public async Task UnosPodatakaGet_TuđeDijete_VracaForbid()
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(roditeljId: 2);

        var rezultat = await _kontroler.UnosPodataka(dijete.Id);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task UnosPodatakaGet_VlastitoDijete_VracaViewSaModelom()
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(ime: "Amar Hadžić", roditeljId: 1);
        _baza.AlergijeDjece.Add(new AlergijaDjeteta { DijeteId = dijete.Id, Naziv = "Kikiriki" });
        _baza.BolestiDjece.Add(new BolestDjeteta { DijeteId = dijete.Id, Naziv = "Astma" });
        await _baza.SaveChangesAsync();

        var rezultat = await _kontroler.UnosPodataka(dijete.Id);

        var view = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var model = Assert.IsInstanceOfType<UnosPodatakaViewModel>(view.Model);
        Assert.AreEqual("Amar Hadžić", model.ImePrezime);
        CollectionAssert.AreEqual(new List<string> { "Kikiriki" }, model.Alergije);
        CollectionAssert.AreEqual(new List<string> { "Astma" }, model.Bolesti);
    }

    [TestMethod]
    public async Task UnosPodatakaGet_NovaRegistracija_PostavljaViewData()
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(roditeljId: 1);

        var rezultat = await _kontroler.UnosPodataka(dijete.Id, novaRegistracija: true);

        var view = Assert.IsInstanceOfType<ViewResult>(rezultat);
        Assert.AreEqual(true, view.ViewData["NovaRegistracija"]);
    }

    [TestMethod]
    public async Task UnosPodatakaGet_DijeteBezDatumaRodjenja_KoristiDanasnjiDatum()
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(roditeljId: 1);
        dijete.DatumRodjenja = default;
        await _baza.SaveChangesAsync();

        var rezultat = await _kontroler.UnosPodataka(dijete.Id);

        var view = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var model = Assert.IsInstanceOfType<UnosPodatakaViewModel>(view.Model);
        Assert.AreEqual(DateTime.Today, model.DatumRodjenja);
    }

    [TestMethod]
    public async Task UnosPodatakaGet_BezIdaIBezPraznogProfila_PreusmjeravaNaRoditeljHome()
    {
        PrijaviKorisnika(1);
        await DodajDijete(ime: "Amar Hadžić", roditeljId: 1);

        var rezultat = await _kontroler.UnosPodataka((int?)null);

        var preusmjeravanje = Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual("RoditeljHome", preusmjeravanje.ActionName);
        Assert.AreEqual("Home", preusmjeravanje.ControllerName);
    }

    [TestMethod]
    public async Task UnosPodatakaGet_BezIdaSaPraznimProfilom_VracaViewZaTajProfil()
    {
        PrijaviKorisnika(1);
        var prazno = await DodajDijete(ime: string.Empty, roditeljId: 1);

        var rezultat = await _kontroler.UnosPodataka((int?)null);

        var view = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var model = Assert.IsInstanceOfType<UnosPodatakaViewModel>(view.Model);
        Assert.AreEqual(prazno.Id, model.DijeteId);
    }

    [TestMethod]
    public async Task UnosPodatakaPost_NijePrijavljen_VracaChallenge()
    {
        NemaPrijavljenogKorisnika();

        var rezultat = await _kontroler.UnosPodataka(NapraviModel(1));

        Assert.IsInstanceOfType<ChallengeResult>(rezultat);
    }

    [TestMethod]
    public async Task UnosPodatakaPost_TuđeDijete_VracaForbid()
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(roditeljId: 2);

        var rezultat = await _kontroler.UnosPodataka(NapraviModel(dijete.Id));

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task UnosPodatakaPost_ValidniPodaci_SpremaPodatkeIPreusmjeravaNaRoditeljHome()
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(ime: string.Empty, roditeljId: 1);
        var model = NapraviModel(dijete.Id);

        var rezultat = await _kontroler.UnosPodataka(model);

        var preusmjeravanje = Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual("RoditeljHome", preusmjeravanje.ActionName);
        var iz_baze = await _baza.Djeca
            .Include(d => d.Alergije).Include(d => d.Bolesti)
            .SingleAsync(d => d.Id == dijete.Id);
        Assert.AreEqual("Amar Hadžić", iz_baze.ImePrezime);
        Assert.AreEqual("Nema napomene", iz_baze.DodatnaNapomena);
        Assert.AreEqual("Kikiriki", iz_baze.Alergije.Single().Naziv);
        Assert.AreEqual("Astma", iz_baze.Bolesti.Single().Naziv);
    }

    [TestMethod]
    public async Task UnosPodatakaPost_ImeSaViseRazmaka_SpremaNormalizovanoIme()
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(roditeljId: 1);
        var model = NapraviModel(dijete.Id);
        model.ImePrezime = "  Amar    Hadžić  ";

        await _kontroler.UnosPodataka(model);

        var iz_baze = await _baza.Djeca.FindAsync(dijete.Id);
        Assert.AreEqual("Amar Hadžić", iz_baze!.ImePrezime);
    }

    [TestMethod]
    public async Task UnosPodatakaPost_NoveAlergijeIBolesti_ZamjenjujuStare()
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(roditeljId: 1);
        _baza.AlergijeDjece.Add(new AlergijaDjeteta { DijeteId = dijete.Id, Naziv = "Stara alergija" });
        _baza.BolestiDjece.Add(new BolestDjeteta { DijeteId = dijete.Id, Naziv = "Stara bolest" });
        await _baza.SaveChangesAsync();
        var model = NapraviModel(dijete.Id);
        model.Alergije = new List<string> { "Mlijeko", "  ", "Jaja" };
        model.Bolesti = new List<string> { "Dijabetes" };

        await _kontroler.UnosPodataka(model);

        var alergije = await _baza.AlergijeDjece.Select(a => a.Naziv).OrderBy(n => n).ToListAsync();
        CollectionAssert.AreEqual(new List<string> { "Jaja", "Mlijeko" }, alergije);
        var bolesti = await _baza.BolestiDjece.Select(b => b.Naziv).ToListAsync();
        CollectionAssert.AreEqual(new List<string> { "Dijabetes" }, bolesti);
    }

    [TestMethod]
    public async Task UnosPodatakaPost_NullListeINapomena_SpremaPrazneVrijednosti()
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(roditeljId: 1);
        var model = NapraviModel(dijete.Id);
        model.Alergije = null!;
        model.Bolesti = null!;
        model.DodatnaNapomena = null;

        var rezultat = await _kontroler.UnosPodataka(model);

        Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual(0, await _baza.AlergijeDjece.CountAsync());
        Assert.AreEqual(0, await _baza.BolestiDjece.CountAsync());
        var iz_baze = await _baza.Djeca.FindAsync(dijete.Id);
        Assert.AreEqual(string.Empty, iz_baze!.DodatnaNapomena);
    }

    [TestMethod]
    [DynamicData(nameof(NevalidnaImena), DynamicDataSourceType.Method)]
    public async Task UnosPodatakaPost_NevalidnoIme_VracaViewSaGreskom(string ime, string opis)
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(ime: string.Empty, roditeljId: 1);
        var model = NapraviModel(dijete.Id);
        model.ImePrezime = ime;

        var rezultat = await _kontroler.UnosPodataka(model, novaRegistracija: true);

        var view = Assert.IsInstanceOfType<ViewResult>(rezultat, opis);
        Assert.AreSame(model, view.Model, opis);
        Assert.IsTrue(_kontroler.ModelState.ContainsKey(nameof(model.ImePrezime)), opis);
        Assert.AreEqual(true, view.ViewData["NovaRegistracija"], opis);
    }

    [TestMethod]
    [DynamicData(nameof(DatumiRodjenja), DynamicDataSourceType.Method)]
    public async Task UnosPodatakaPost_RazniDatumiRodjenja_ValidirajuSeIspravno(
        string opis, int godine, int mjeseci, int dani, bool ocekivanoValidno)
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(roditeljId: 1);
        var model = NapraviModel(dijete.Id);
        model.DatumRodjenja = DateTime.Today.AddYears(-godine).AddMonths(-mjeseci).AddDays(-dani);

        var rezultat = await _kontroler.UnosPodataka(model);

        Assert.AreEqual(ocekivanoValidno, rezultat is RedirectToActionResult, opis);
        Assert.AreEqual(!ocekivanoValidno, _kontroler.ModelState.ContainsKey(nameof(model.DatumRodjenja)), opis);
    }

    [TestMethod]
    public async Task UnosPodatakaPost_PredugackaAlergija_VracaViewSaGreskom()
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(roditeljId: 1);
        var model = NapraviModel(dijete.Id);
        model.Alergije = new List<string> { new string('a', 101) };

        var rezultat = await _kontroler.UnosPodataka(model);

        Assert.IsInstanceOfType<ViewResult>(rezultat);
        Assert.IsTrue(_kontroler.ModelState.ContainsKey("Alergije"));
    }

    [TestMethod]
    public async Task UnosPodatakaPost_PredugackaBolest_VracaViewSaGreskom()
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(roditeljId: 1);
        var model = NapraviModel(dijete.Id);
        model.Bolesti = new List<string> { new string('b', 101) };

        var rezultat = await _kontroler.UnosPodataka(model);

        Assert.IsInstanceOfType<ViewResult>(rezultat);
        Assert.IsTrue(_kontroler.ModelState.ContainsKey("Bolesti"));
    }

    [TestMethod]
    public async Task UnosPodatakaPost_AlergijaOd100Slova_JeDozvoljena()
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(roditeljId: 1);
        var model = NapraviModel(dijete.Id);
        model.Alergije = new List<string> { new string('a', 100) };

        var rezultat = await _kontroler.UnosPodataka(model);

        Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
    }

    [TestMethod]
    public async Task UnosPodatakaPost_DuplikatiAlergija_VracaViewSaGreskom()
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(roditeljId: 1);
        var model = NapraviModel(dijete.Id);
        model.Alergije = new List<string> { "Mlijeko", " mlijeko " };

        var rezultat = await _kontroler.UnosPodataka(model);

        Assert.IsInstanceOfType<ViewResult>(rezultat);
        Assert.IsTrue(_kontroler.ModelState.ContainsKey("Alergije"));
    }

    [TestMethod]
    public async Task UnosPodatakaPost_DuplikatiBolesti_VracaViewSaGreskom()
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(roditeljId: 1);
        var model = NapraviModel(dijete.Id);
        model.Bolesti = new List<string> { "Astma", "ASTMA" };

        var rezultat = await _kontroler.UnosPodataka(model);

        Assert.IsInstanceOfType<ViewResult>(rezultat);
        Assert.IsTrue(_kontroler.ModelState.ContainsKey("Bolesti"));
    }

    [TestMethod]
    public void DodajDijeteGet_VracaView()
    {
        var rezultat = _kontroler.DodajDijete();

        Assert.IsInstanceOfType<ViewResult>(rezultat);
    }

    [TestMethod]
    public async Task DodajDijetePost_NijePrijavljen_VracaChallenge()
    {
        NemaPrijavljenogKorisnika();

        var rezultat = await _kontroler.DodajDijete("ABCD1234");

        Assert.IsInstanceOfType<ChallengeResult>(rezultat);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task DodajDijetePost_PrazanKod_VracaViewSaGreskom(string? kod)
    {
        PrijaviKorisnika(1);

        var rezultat = await _kontroler.DodajDijete(kod!);

        Assert.IsInstanceOfType<ViewResult>(rezultat);
        Assert.IsTrue(_kontroler.ModelState.ContainsKey("identifikacioniKod"));
    }

    [TestMethod]
    [DynamicData(nameof(NevalidniKodovi), DynamicDataSourceType.Method)]
    public async Task DodajDijetePost_KodPogresneDuzine_VracaViewSaGreskom(string kod, string razlog)
    {
        PrijaviKorisnika(1);

        var rezultat = await _kontroler.DodajDijete(kod);

        Assert.IsInstanceOfType<ViewResult>(rezultat, razlog);
        Assert.IsTrue(_kontroler.ModelState.ContainsKey("identifikacioniKod"), razlog);
    }

    [TestMethod]
    public async Task MojeDijete_NijePrijavljen_VracaChallenge()
    {
        NemaPrijavljenogKorisnika();

        var rezultat = await _kontroler.MojeDijete(1);

        Assert.IsInstanceOfType<ChallengeResult>(rezultat);
    }

    [TestMethod]
    public async Task MojeDijete_TuđeDijete_VracaForbid()
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(roditeljId: 2);

        var rezultat = await _kontroler.MojeDijete(dijete.Id);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task MojeDijete_VlastitoDijete_VracaViewSaDjetetom()
    {
        PrijaviKorisnika(1);
        var dijete = await DodajDijete(roditeljId: 1);

        var rezultat = await _kontroler.MojeDijete(dijete.Id);

        var view = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var model = Assert.IsInstanceOfType<Dijete>(view.Model);
        Assert.AreEqual(dijete.Id, model.Id);
    }

    [TestMethod]
    public async Task PodaciODjetetu_NullId_VracaNotFound()
    {
        var rezultat = await _kontroler.PodaciODjetetu(null);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task PodaciODjetetu_NijePrijavljen_VracaChallenge()
    {
        NemaPrijavljenogKorisnika();

        var rezultat = await _kontroler.PodaciODjetetu(1);

        Assert.IsInstanceOfType<ChallengeResult>(rezultat);
    }

    [TestMethod]
    public async Task PodaciODjetetu_KorisnikNijeOdgajatelj_VracaForbid()
    {
        PrijaviKorisnika(3);

        var rezultat = await _kontroler.PodaciODjetetu(1);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task PodaciODjetetu_DijeteIzTuđeGrupe_VracaForbid()
    {
        PrijaviKorisnika(3);
        _baza.Odgajatelji.Add(new Odgajatelj { Id = 3 });
        var tudjaGrupa = await DodajGrupu("Tuđa", odgajateljId: 4);
        var dijete = await DodajDijete(grupaId: tudjaGrupa.Id);
        await _baza.SaveChangesAsync();

        var rezultat = await _kontroler.PodaciODjetetu(dijete.Id);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task PodaciODjetetu_DijeteBezGrupe_VracaForbid()
    {
        PrijaviKorisnika(3);
        _baza.Odgajatelji.Add(new Odgajatelj { Id = 3 });
        var dijete = await DodajDijete(grupaId: null);
        await _baza.SaveChangesAsync();

        var rezultat = await _kontroler.PodaciODjetetu(dijete.Id);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task PodaciODjetetu_DijeteIzVlastiteGrupe_VracaViewSaDjetetom()
    {
        PrijaviKorisnika(3);
        _baza.Odgajatelji.Add(new Odgajatelj { Id = 3 });
        var grupa = await DodajGrupu("Moja", odgajateljId: 3);
        var dijete = await DodajDijete(grupaId: grupa.Id);
        await _baza.SaveChangesAsync();

        var rezultat = await _kontroler.PodaciODjetetu(dijete.Id);

        var view = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var model = Assert.IsInstanceOfType<Dijete>(view.Model);
        Assert.AreEqual(dijete.Id, model.Id);
    }

    [TestMethod]
    public void CreateGet_VracaForbid()
    {
        var rezultat = _kontroler.Create();

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task CreatePost_VracaForbid()
    {
        var rezultat = await _kontroler.Create(new Dijete { ImePrezime = "Amar Hadžić" });

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task EditGet_VracaForbid()
    {
        var rezultat = await _kontroler.Edit(1);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task EditPost_VracaForbid()
    {
        var rezultat = await _kontroler.Edit(1, new Dijete { Id = 1 });

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task DeleteGet_VracaForbid()
    {
        var rezultat = await _kontroler.Delete(1);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task DeleteConfirmed_PostojeceDijete_BriseDijete()
    {
        var dijete = await DodajDijete();

        var rezultat = await _kontroler.DeleteConfirmed(dijete.Id);

        var preusmjeravanje = Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual("Index", preusmjeravanje.ActionName);
        Assert.IsNull(await _baza.Djeca.FindAsync(dijete.Id));
    }

    [TestMethod]
    public async Task DeleteConfirmed_NepostojeciId_PreusmjeravaNaIndex()
    {
        var rezultat = await _kontroler.DeleteConfirmed(999);

        var preusmjeravanje = Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual("Index", preusmjeravanje.ActionName);
    }
}