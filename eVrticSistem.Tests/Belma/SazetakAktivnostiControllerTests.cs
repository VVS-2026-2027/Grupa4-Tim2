using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EVrtic.Controllers;
using EVrtic.Data;
using EVrtic.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EVrtic.Tests.Belma;

[TestClass]
public class SazetakAktivnostiControllerTests
{
    private ApplicationDbContext _db = null!;

    [TestInitialize]
    public void Priprema() => _db = BelmaTestPomocne.NoviKontekst();

    [TestCleanup]
    public void Ciscenje() => _db.Dispose();

    private SazetakAktivnostiController Kontroler(Korisnik? korisnik) =>
        BelmaTestPomocne.PripremiKontroler(new SazetakAktivnostiController(
            _db, BelmaTestPomocne.KorisnickiMenadzer(korisnik).Object));

    private static Korisnik Roditelj() => new Korisnik { Id = 91, ImePrezime = "Test Roditelj" };
    private static Korisnik Odgajatelj() => new Korisnik { Id = 81, ImePrezime = "Test Odgajatelj" };

    public sealed class PeriodSlucaj
    {
        public string Period { get; set; } = "";
        public DateTime Pocetak { get; set; }
        public string OcekivaniPeriod { get; set; } = "";
        public DateTime Od { get; set; }
        public DateTime Do { get; set; }
    }

    [TestMethod]
    public async Task AdministratorskeCRUDMetode_SveVracajuForbid()
    {
        var c = Kontroler(null);
        Assert.IsInstanceOfType(await c.Index(), typeof(ForbidResult));
        Assert.IsInstanceOfType(await c.Details(null), typeof(ForbidResult));
        Assert.IsInstanceOfType(await c.Details(2), typeof(ForbidResult));
        Assert.IsInstanceOfType(c.Create(), typeof(ForbidResult));
        Assert.IsInstanceOfType(await c.Create(new SazetakAktivnosti()), typeof(ForbidResult));
        Assert.IsInstanceOfType(await c.Edit((int?)2), typeof(ForbidResult));
        Assert.IsInstanceOfType(await c.Edit(2, new SazetakAktivnosti()), typeof(ForbidResult));
        Assert.IsInstanceOfType(await c.Delete((int?)2), typeof(ForbidResult));
    }

    [TestMethod]
    public async Task DeleteConfirmed_NepostojeciZapis_VracaRedirect()
    {
        var rezultat = await Kontroler(null).DeleteConfirmed(42);
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
        Assert.AreEqual(0, await _db.SazeciAktivnosti.CountAsync());
    }

    [TestMethod]
    public async Task DeleteConfirmed_PostojeciZapis_BriseSazetak()
    {
        _db.SazeciAktivnosti.Add(new SazetakAktivnosti { Id = 17, DijeteId = 101,
            DatumPocetka = new DateTime(2026, 11, 16), DatumKraja = new DateTime(2026, 11, 20) });
        await _db.SaveChangesAsync();
        var rezultat = await Kontroler(null).DeleteConfirmed(17);
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
        Assert.AreEqual(0, await _db.SazeciAktivnosti.CountAsync());
    }

    [TestMethod]
    public async Task RoditeljPregled_BezKorisnika_VracaLogin()
    {
        var rezultat = await Kontroler(null).RoditeljPregled();
        var redir = rezultat as RedirectToPageResult;
        Assert.IsNotNull(redir);
        Assert.AreEqual("/Account/Login", redir.PageName);
    }

    [TestMethod]
    public async Task RoditeljPregled_PeriodiIzJson_KoristiTacanRasponDatuma()
    {
        // Podaci se kopiraju u izlazni folder prema postavci u README-u.
        string putanja = Path.Combine(AppContext.BaseDirectory, "Podaci", "Belma", "periodi.json");
        Assert.IsTrue(File.Exists(putanja), $"Nedostaje JSON: {putanja}");
        var scenariji = JsonSerializer.Deserialize<PeriodSlucaj[]>(
            await File.ReadAllTextAsync(putanja), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.IsNotNull(scenariji);
        foreach (var slucaj in scenariji)
        {
            var rezultat = await Kontroler(Roditelj()).RoditeljPregled(
                slucaj.Period, slucaj.Pocetak, null);
            var vm = (RoditeljSazetakPregledViewModel)((ViewResult)rezultat).Model!;
            Assert.AreEqual(slucaj.OcekivaniPeriod, vm.Period, slucaj.Period);
            Assert.AreEqual(slucaj.Od.Date, vm.DatumPocetka.Date, slucaj.Period);
            Assert.AreEqual(slucaj.Do.Date, vm.DatumKraja.Date, slucaj.Period);
        }
    }

    [TestMethod]
    public async Task RoditeljPregled_BezDjece_VracaPrazanPregled()
    {
        var rezultat = await Kontroler(Roditelj()).RoditeljPregled("Sedmica", BelmaTestPomocne.Datum);
        var vm = (RoditeljSazetakPregledViewModel)((ViewResult)rezultat).Model!;
        Assert.AreEqual(0, vm.Djeca.Count);
        Assert.IsNull(vm.Dijete);
        Assert.IsTrue(vm.SedmicneOpcije.Count > 0);
        Assert.IsTrue(vm.MjesecneOpcije.Count > 0);
    }

    [TestMethod]
    public async Task RoditeljPregled_TudjeDijete_NePrikazujeTudjePodatke()
    {
        BelmaTestPomocne.DodajDijete(_db, 101, null, 91);
        BelmaTestPomocne.DodajDijete(_db, 102, null, 92);
        var rezultat = await Kontroler(Roditelj()).RoditeljPregled("Sedmica", BelmaTestPomocne.Datum, 102);
        var vm = (RoditeljSazetakPregledViewModel)((ViewResult)rezultat).Model!;
        Assert.AreEqual(1, vm.Djeca.Count);
        Assert.IsNull(vm.Dijete);
        Assert.AreEqual(0, vm.BrojDolazaka);
    }

    [TestMethod]
    public async Task RoditeljPregled_Sedmica_BrojiSamoRadneDaneISamoVlastitoDijete()
    {
        BelmaTestPomocne.DodajDijete(_db, 101, null, 91);
        BelmaTestPomocne.DodajDijete(_db, 102, null, 92);
        var pon = new DateTime(2026, 11, 16);
        var uto = pon.AddDays(1);
        var sub = pon.AddDays(5);
        _db.DnevniIzvjestaji.AddRange(
            new DnevniIzvjestaj { DijeteId = 101, Datum = pon, Dorucak = "Jaja",
                StatusDorucka = StatusObroka.POTPUNO_POJEDENO, Rucak = "Supa",
                StatusRucka = StatusObroka.DJELIMICNO_POJEDENO,
                SpavanjeMinuta = 60, NapomenaAktivnosti = "Veselo dijete" },
            new DnevniIzvjestaj { DijeteId = 101, Datum = uto, Dorucak = "Kasa",
                StatusDorucka = StatusObroka.NIJE_POJEDENO,
                SpavanjeMinuta = 120 },
            new DnevniIzvjestaj { DijeteId = 101, Datum = sub, Dorucak = "Vikend",
                SpavanjeMinuta = 900 },
            new DnevniIzvjestaj { DijeteId = 102, Datum = pon, Dorucak = "Tudje" }
        );
        await _db.SaveChangesAsync();
        BelmaTestPomocne.DodajDogadjaj(_db, 101, TipDogadjaja.DOLAZAK, pon.AddHours(8));
        BelmaTestPomocne.DodajDogadjaj(_db, 101, TipDogadjaja.DOLAZAK, uto.AddHours(8));
        BelmaTestPomocne.DodajDogadjaj(_db, 101, TipDogadjaja.ODLAZAK, pon.AddHours(15));
        BelmaTestPomocne.DodajDogadjaj(_db, 101, TipDogadjaja.DOLAZAK, sub.AddHours(8));
        BelmaTestPomocne.DodajDogadjaj(_db, 102, TipDogadjaja.DOLAZAK, pon.AddHours(8));
        var rezultat = await Kontroler(Roditelj()).RoditeljPregled("Sedmica", BelmaTestPomocne.Datum, 101);
        var vm = (RoditeljSazetakPregledViewModel)((ViewResult)rezultat).Model!;
        Assert.AreEqual(2, vm.BrojDolazaka);
        Assert.AreEqual(1, vm.BrojOdlazaka);
        Assert.AreEqual(2, vm.BrojDorucaka);
        Assert.AreEqual(1, vm.BrojRucaka);
        Assert.AreEqual(180, vm.UkupnoSpavanjeMinuta);
        Assert.AreEqual(90, vm.ProsjecnoSpavanjeMinuta);
        Assert.AreEqual(2.0, vm.ProsjekDorucka!.Value, 0.001);
        Assert.AreEqual(1, vm.NapomenePoDatumima.Count);
    }

    [TestMethod]
    public async Task RoditeljPregled_BezDolazaka_NemaProsjekaSpavanja()
    {
        BelmaTestPomocne.DodajDijete(_db, 101, null, 91);
        _db.DnevniIzvjestaji.Add(new DnevniIzvjestaj { DijeteId = 101,
            Datum = BelmaTestPomocne.Datum, SpavanjeMinuta = 100 });
        await _db.SaveChangesAsync();
        var rezultat = await Kontroler(Roditelj()).RoditeljPregled("Sedmica", BelmaTestPomocne.Datum, 101);
        var vm = (RoditeljSazetakPregledViewModel)((ViewResult)rezultat).Model!;
        Assert.AreEqual(0, vm.ProsjecnoSpavanjeMinuta);
        StringAssert.Contains(vm.OcjenaSpavanja, "Nema dovoljno podataka");
        StringAssert.Contains(vm.OcjenaDorucka, "Nema dovoljno podataka");
    }

    [TestMethod]
    public async Task OdgajateljPregled_BezKorisnika_PreusmjeravaNaLogin()
    {
        Assert.IsInstanceOfType(await Kontroler(null).OdgajateljPregled(), typeof(RedirectToPageResult));
    }

    [TestMethod]
    public async Task OdgajateljPregled_BezGenerisanja_PrikazujeFormuBezSazetaka()
    {
        BelmaTestPomocne.DodajOdgajateljaIGrupu(_db);
        BelmaTestPomocne.DodajDijete(_db);
        var rezultat = await Kontroler(Odgajatelj()).OdgajateljPregled(
            "Sedmica", BelmaTestPomocne.Datum, null, false);
        var vm = (OdgajateljSazetakPregledViewModel)((ViewResult)rezultat).Model!;
        Assert.IsFalse(vm.PrikaziSazetak);
        Assert.AreEqual(0, vm.SazeciDjece.Count);
        Assert.AreEqual(1, vm.Djeca.Count);
    }

    [TestMethod]
    public async Task OdgajateljPregled_BezDjece_NemaSazetaka()
    {
        BelmaTestPomocne.DodajOdgajateljaIGrupu(_db);
        var rezultat = await Kontroler(Odgajatelj()).OdgajateljPregled(
            "Mjesec", BelmaTestPomocne.Datum, 7, true);
        var vm = (OdgajateljSazetakPregledViewModel)((ViewResult)rezultat).Model!;
        Assert.IsTrue(vm.PrikaziSazetak);
        Assert.AreEqual(0, vm.SazeciDjece.Count);
        Assert.AreEqual("Mjesec", vm.Period);
    }

    [TestMethod]
    public async Task OdgajateljPregled_Generisanje_RacunaSazetakPoDjetetu()
    {
        BelmaTestPomocne.DodajOdgajateljaIGrupu(_db);
        BelmaTestPomocne.DodajDijete(_db);
        var pon = new DateTime(2026, 11, 16);
        _db.DnevniIzvjestaji.Add(new DnevniIzvjestaj { DijeteId = 101,
            Datum = pon, Dorucak = "Jaja", StatusDorucka = StatusObroka.POTPUNO_POJEDENO,
            SpavanjeMinuta = 80, NapomenaAktivnosti = "Dobar dan" });
        await _db.SaveChangesAsync();
        BelmaTestPomocne.DodajDogadjaj(_db, 101, TipDogadjaja.DOLAZAK, pon.AddHours(8));
        var rezultat = await Kontroler(Odgajatelj()).OdgajateljPregled(
            "Sedmica", BelmaTestPomocne.Datum, 7, true);
        var vm = (OdgajateljSazetakPregledViewModel)((ViewResult)rezultat).Model!;
        Assert.AreEqual(1, vm.SazeciDjece.Count);
        var jedan = vm.SazeciDjece.Single();
        Assert.AreEqual(1, jedan.BrojDolazaka);
        Assert.AreEqual(1, jedan.BrojDorucaka);
        Assert.AreEqual(80, jedan.UkupnoSpavanjeMinuta);
        Assert.AreEqual(80, jedan.ProsjecnoSpavanjeMinuta);
        Assert.AreEqual(1, jedan.NapomenePoDatumima.Count);
    }
}
