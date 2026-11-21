using System;
using System.Linq;
using System.Threading.Tasks;
using EVrtic.Controllers;
using EVrtic.Data;
using EVrtic.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.AspNetCore.Identity;

namespace EVrtic.Tests.Belma;

[TestClass]
public class DnevniIzvjestajControllerTests
{
    private ApplicationDbContext _db = null!;

    [TestInitialize]
    public void Priprema() => _db = BelmaTestPomocne.NoviKontekst();

    [TestCleanup]
    public void Ciscenje() => _db.Dispose();

    private DnevniIzvjestajController Kontroler(Korisnik? korisnik) =>
        BelmaTestPomocne.PripremiKontroler(new DnevniIzvjestajController(
            _db, BelmaTestPomocne.KorisnickiMenadzer(korisnik).Object));

    private static Korisnik Odgajatelj() => new Korisnik { Id = 81, ImePrezime = "Test Odgajatelj" };
    private static Korisnik Roditelj() => new Korisnik { Id = 91, ImePrezime = "Test Roditelj" };

    private void PripremiDijeteOdgajatelja()
    {
        BelmaTestPomocne.DodajOdgajateljaIGrupu(_db);
        BelmaTestPomocne.DodajDijete(_db);
    }

    private Task<IActionResult> Sacuvaj(DnevniIzvjestajController kontroler,
        string? pocetakSati = null, string? pocetakMinuta = null,
        string? krajSati = null, string? krajMinuta = null,
        string? dorucak = "  Kasa  ", string? rucak = "  Supa ") =>
        kontroler.SacuvajAktivnosti(101, "2026-11-18", dorucak, "2",
            rucak, "1", pocetakSati, pocetakMinuta, krajSati, krajMinuta, "Sve u redu");

    [TestMethod]
    public async Task AdministratorskeCRUDMetode_SveVracajuForbid()
    {
        var c = Kontroler(null);
        Assert.IsInstanceOfType(await c.Index(), typeof(ForbidResult));
        Assert.IsInstanceOfType(await c.Details(null), typeof(ForbidResult));
        Assert.IsInstanceOfType(await c.Details(3), typeof(ForbidResult));
        Assert.IsInstanceOfType(c.Create(), typeof(ForbidResult));
        Assert.IsInstanceOfType(await c.Create(new DnevniIzvjestaj()), typeof(ForbidResult));
        Assert.IsInstanceOfType(await c.Edit((int?)3), typeof(ForbidResult));
        Assert.IsInstanceOfType(await c.Edit(3, new DnevniIzvjestaj()), typeof(ForbidResult));
        Assert.IsInstanceOfType(await c.Delete((int?)3), typeof(ForbidResult));
    }

    [TestMethod]
    public async Task DeleteConfirmed_NepostojeciIzvjestaj_VracaPreusmjerenje()
    {
        var rezultat = await Kontroler(null).DeleteConfirmed(123);
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
        Assert.AreEqual(0, await _db.DnevniIzvjestaji.CountAsync());
    }

    [TestMethod]
    public async Task DeleteConfirmed_PostojeciIzvjestaj_BriseZapis()
    {
        _db.DnevniIzvjestaji.Add(new DnevniIzvjestaj { Id = 21, DijeteId = 101,
            Datum = BelmaTestPomocne.Datum });
        await _db.SaveChangesAsync();
        var rezultat = await Kontroler(null).DeleteConfirmed(21);
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
        Assert.AreEqual(0, await _db.DnevniIzvjestaji.CountAsync());
    }

    [TestMethod]
    public async Task EvidencijaAktivnosti_BezPrijavljenogKorisnika_PreusmjeravaNaLogin()
    {
        var rezultat = await Kontroler(null).EvidencijaAktivnosti(null, "2026-11-18");
        var stranica = rezultat as RedirectToPageResult;
        Assert.IsNotNull(stranica);
        Assert.AreEqual("/Account/Login", stranica.PageName);
    }

    [TestMethod]
    public async Task EvidencijaAktivnosti_BezProfilaOdgajatelja_PreusmjeravaNaHome()
    {
        var rezultat = await Kontroler(Odgajatelj()).EvidencijaAktivnosti(null, "2026-11-18");
        var redir = rezultat as RedirectToActionResult;
        Assert.IsNotNull(redir);
        Assert.AreEqual("OdgajateljHome", redir.ActionName);
    }

    [TestMethod]
    public async Task EvidencijaAktivnosti_DijeteIzTudjeGrupe_ZabranjujePregled()
    {
        PripremiDijeteOdgajatelja();
        var rezultat = await Kontroler(Odgajatelj()).EvidencijaAktivnosti(999, "2026-11-18");
        Assert.IsInstanceOfType(rezultat, typeof(ForbidResult));
    }

    [TestMethod]
    public async Task EvidencijaAktivnosti_BezDolaska_ZakljucavaUnos()
    {
        PripremiDijeteOdgajatelja();
        var rezultat = await Kontroler(Odgajatelj()).EvidencijaAktivnosti(101, "2026-11-18");
        var vm = (EvidencijaAktivnostiViewModel)((ViewResult)rezultat).Model!;
        Assert.IsFalse(vm.MozeUnositiAktivnosti);
        StringAssert.Contains(vm.PorukaZakljucavanja!, "dolazak");
    }

    [TestMethod]
    public async Task EvidencijaAktivnosti_NakonDolaska_DozvoljavaUnos()
    {
        PripremiDijeteOdgajatelja();
        BelmaTestPomocne.DodajDogadjaj(_db, 101, TipDogadjaja.DOLAZAK);
        var rezultat = await Kontroler(Odgajatelj()).EvidencijaAktivnosti(101, "2026-11-18");
        var vm = (EvidencijaAktivnostiViewModel)((ViewResult)rezultat).Model!;
        Assert.IsTrue(vm.MozeUnositiAktivnosti);
        Assert.AreEqual(-1, vm.StatusDoruckaVal);
    }

    [TestMethod]
    public async Task EvidencijaAktivnosti_NakonOdlaska_ZakljucavaUnos()
    {
        PripremiDijeteOdgajatelja();
        BelmaTestPomocne.DodajDogadjaj(_db, 101, TipDogadjaja.DOLAZAK);
        BelmaTestPomocne.DodajDogadjaj(_db, 101, TipDogadjaja.ODLAZAK,
            BelmaTestPomocne.Datum.AddHours(15));
        var rezultat = await Kontroler(Odgajatelj()).EvidencijaAktivnosti(101, "2026-11-18");
        var vm = (EvidencijaAktivnostiViewModel)((ViewResult)rezultat).Model!;
        Assert.IsFalse(vm.MozeUnositiAktivnosti);
        StringAssert.Contains(vm.PorukaZakljucavanja!, "zaključane");
    }

    [TestMethod]
    public async Task EvidencijaAktivnosti_PostojeciIzvjestaj_PrimaVlastiteObroke()
    {
        PripremiDijeteOdgajatelja();
        BelmaTestPomocne.DodajDogadjaj(_db, 101, TipDogadjaja.DOLAZAK);
        _db.DnevniIzvjestaji.Add(new DnevniIzvjestaj { DijeteId = 101,
            Datum = BelmaTestPomocne.Datum, Dorucak = "Hljeb", Rucak = "Supa",
            StatusDorucka = StatusObroka.POTPUNO_POJEDENO });
        await _db.SaveChangesAsync();
        var rezultat = await Kontroler(Odgajatelj()).EvidencijaAktivnosti(101, "2026-11-18");
        var vm = (EvidencijaAktivnostiViewModel)((ViewResult)rezultat).Model!;
        Assert.AreEqual("Hljeb", vm.DorucakTekst);
        Assert.AreEqual((int)StatusObroka.POTPUNO_POJEDENO, vm.StatusDoruckaVal);
        Assert.IsFalse(vm.TekstJePrijedlog);
    }

    [TestMethod]
    public async Task EvidencijaAktivnosti_PrviUnos_PredlazeJelovnikDrugogDjeteta()
    {
        PripremiDijeteOdgajatelja();
        BelmaTestPomocne.DodajDijete(_db, 102);
        _db.DnevniIzvjestaji.Add(new DnevniIzvjestaj {
            DijeteId = 102, Datum = BelmaTestPomocne.Datum, Dorucak = "Palenta",
            Rucak = "Corba" });
        await _db.SaveChangesAsync();
        var rezultat = await Kontroler(Odgajatelj()).EvidencijaAktivnosti(101, "2026-11-18");
        var vm = (EvidencijaAktivnostiViewModel)((ViewResult)rezultat).Model!;
        Assert.AreEqual("Palenta", vm.DorucakTekst);
        Assert.IsTrue(vm.TekstJePrijedlog);
        Assert.AreEqual(-1, vm.StatusDoruckaVal);
    }

    [TestMethod]
    public async Task SacuvajAktivnosti_BezKorisnika_VracaUnauthorized()
    {
        Assert.IsInstanceOfType(await Sacuvaj(Kontroler(null)), typeof(UnauthorizedResult));
    }

    [TestMethod]
    public async Task SacuvajAktivnosti_BezProfilaOdgajatelja_VracaForbid()
    {
        Assert.IsInstanceOfType(await Sacuvaj(Kontroler(Odgajatelj())), typeof(ForbidResult));
    }

    [TestMethod]
    public async Task SacuvajAktivnosti_NepostojeceDijete_VracaNotFound()
    {
        BelmaTestPomocne.DodajOdgajateljaIGrupu(_db);
        Assert.IsInstanceOfType(await Sacuvaj(Kontroler(Odgajatelj())), typeof(NotFoundResult));
    }

    [TestMethod]
    public async Task SacuvajAktivnosti_BezDolaska_NeKreiraIzvjestaj()
    {
        PripremiDijeteOdgajatelja();
        var rezultat = await Sacuvaj(Kontroler(Odgajatelj()));
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
        Assert.AreEqual(0, await _db.DnevniIzvjestaji.CountAsync());
    }

    [DataTestMethod]
    [DataRow("07", "30", "09", "00", "između")]
    [DataRow("10", "30", "09", "30", "poslije")]
    [DataRow("10", null, "11", "00", "ispravno")]
    [DataRow("10", "30", null, null, "početak i kraj")]
    public async Task SacuvajAktivnosti_NeispravnoSpavanje_OdbijaUnos(
        string? pocS, string? pocM, string? krajS, string? krajM, string poruka)
    {
        PripremiDijeteOdgajatelja();
        BelmaTestPomocne.DodajDogadjaj(_db, 101, TipDogadjaja.DOLAZAK);
        var c = Kontroler(Odgajatelj());
        var rezultat = await Sacuvaj(c, pocS, pocM, krajS, krajM);
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
        StringAssert.Contains((string)c.TempData["Greska"]!, poruka);
        Assert.AreEqual(0, await _db.DnevniIzvjestaji.CountAsync());
    }

    [TestMethod]
    public async Task SacuvajAktivnosti_ValidniPodaci_SpremaObrokeISpavanje()
    {
        PripremiDijeteOdgajatelja();
        BelmaTestPomocne.DodajDogadjaj(_db, 101, TipDogadjaja.DOLAZAK);
        var rezultat = await Sacuvaj(Kontroler(Odgajatelj()), "12", "00", "13", "30");
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
        var izvjestaj = await _db.DnevniIzvjestaji.SingleAsync();
        Assert.AreEqual("Kasa", izvjestaj.Dorucak);
        Assert.AreEqual("Supa", izvjestaj.Rucak);
        Assert.AreEqual(StatusObroka.POTPUNO_POJEDENO, izvjestaj.StatusDorucka);
        Assert.AreEqual(90, izvjestaj.SpavanjeMinuta);
        Assert.AreEqual(new TimeSpan(12, 0, 0), izvjestaj.VrijemeDolaska);
    }

    [TestMethod]
    public async Task SacuvajAktivnosti_PonovniUnos_AzuriraPostojeciBezDuplikata()
    {
        PripremiDijeteOdgajatelja();
        BelmaTestPomocne.DodajDogadjaj(_db, 101, TipDogadjaja.DOLAZAK);
        var c = Kontroler(Odgajatelj());
        await Sacuvaj(c, "12", "00", "13", "00");
        await Sacuvaj(c, "13", "00", "14", "00", "  Novi dorucak  ", "Novi rucak");
        Assert.AreEqual(1, await _db.DnevniIzvjestaji.CountAsync());
        Assert.AreEqual("Novi dorucak", (await _db.DnevniIzvjestaji.SingleAsync()).Dorucak);
    }

    [TestMethod]
    public async Task RoditeljPregled_Neprijavljen_PreusmjeravaNaLogin()
    {
        var rezultat = await Kontroler(null).RoditeljPregled(null, null);
        Assert.IsInstanceOfType(rezultat, typeof(RedirectToPageResult));
    }

    [TestMethod]
    public async Task RoditeljPregled_BezDjece_VracaPrazanPrikaz()
    {
        var rezultat = await Kontroler(Roditelj()).RoditeljPregled(BelmaTestPomocne.Datum, null);
        var vm = (RoditeljDnevniIzvjestajViewModel)((ViewResult)rezultat).Model!;
        Assert.AreEqual(0, vm.Djeca.Count);
        Assert.AreEqual(BelmaTestPomocne.Datum, vm.Datum);
    }

    [TestMethod]
    public async Task RoditeljPregled_TudjeDijete_VracaForbid()
    {
        BelmaTestPomocne.DodajDijete(_db, 101, null, 91);
        var rezultat = await Kontroler(Roditelj()).RoditeljPregled(BelmaTestPomocne.Datum, 999);
        Assert.IsInstanceOfType(rezultat, typeof(ForbidResult));
    }

    [TestMethod]
    public async Task RoditeljPregled_VlastitoDijete_VracaNjegovIzvjestaj()
    {
        BelmaTestPomocne.DodajDijete(_db, 101, null, 91);
        _db.DnevniIzvjestaji.Add(new DnevniIzvjestaj {
            DijeteId = 101, Datum = BelmaTestPomocne.Datum, Dorucak = "Jaja" });
        await _db.SaveChangesAsync();
        var rezultat = await Kontroler(Roditelj()).RoditeljPregled(BelmaTestPomocne.Datum, 101);
        var vm = (RoditeljDnevniIzvjestajViewModel)((ViewResult)rezultat).Model!;
        Assert.AreEqual(101, vm.OdabraniDijeteId);
        Assert.AreEqual("Jaja", vm.Izvjestaj!.Dorucak);
    }
}
