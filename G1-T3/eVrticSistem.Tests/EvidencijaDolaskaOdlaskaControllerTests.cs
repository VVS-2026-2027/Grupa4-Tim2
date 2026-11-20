using System.Security.Claims;
using EVrtic.Controllers;
using EVrtic.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace EVrtic.Tests;

[TestClass]
public class EvidencijaDolaskaOdlaskaControllerTests : EvidencijaTestBase
{
    public static IEnumerable<object[]> NeispravniKodovi => TestPodaci.IzCsv("neispravni-kodovi.csv");
    public static IEnumerable<object?[]> TipoviDolaska => TestPodaci.IzJsonNiza("tipovi-dolaska.json");

    // ===== Onemogućene akcije =====

    [TestMethod]
    public async Task Index_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(await Controller.Index());

    [TestMethod]
    public async Task Details_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(await Controller.Details(1));

    [TestMethod]
    public void CreateGet_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(Controller.Create());

    [TestMethod]
    public async Task CreatePost_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(await Controller.Create(new EvidencijaDolaskaOdlaska()));

    [TestMethod]
    public async Task EditGet_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(await Controller.Edit(1));

    [TestMethod]
    public async Task EditPost_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(await Controller.Edit(1, new EvidencijaDolaskaOdlaska()));

    [TestMethod]
    public async Task DeleteGet_AkcijaOnemogucena_VracaForbid() =>
        Assert.IsInstanceOfType<ForbidResult>(await Controller.Delete(1));

    // ===== DeleteConfirmed =====

    [TestMethod]
    public async Task DeleteConfirmed_PostojecaEvidencija_BrisePreusmjeravaNaIndex()
    {
        var grupa = DodajGrupu(1, OdgajateljId);
        var dijete = DodajDijete(1, "Amar", RoditeljId, grupa);
        var qr = DodajQr(KodOdgajatelja(OdgajateljId));
        var evidencija = DodajEvidenciju(dijete, TipDogadjaja.DOLAZAK, StatusEvidencije.EVIDENTIRANO, qr);

        var rezultat = await Controller.DeleteConfirmed(evidencija.Id);

        Assert.AreEqual(nameof(Controller.Index), Kao<RedirectToActionResult>(rezultat).ActionName);
        Assert.AreEqual(0, Context.EvidencijeDolaskaOdlaska.Count());
    }

    [TestMethod]
    public async Task DeleteConfirmed_NepostojecaEvidencija_PreusmjeravaNaIndexBezBrisanja()
    {
        var rezultat = await Controller.DeleteConfirmed(999);

        Assert.AreEqual(nameof(Controller.Index), Kao<RedirectToActionResult>(rezultat).ActionName);
    }

    // ===== RoditeljEvidencija (GET) =====

    [TestMethod]
    public async Task RoditeljEvidencijaGet_BezPrijavljenogKorisnika_PreusmjeravaNaLogin()
    {
        NikoNijePrijavljen();

        var rezultat = await Controller.RoditeljEvidencija();

        var redirect = Kao<RedirectToPageResult>(rezultat);
        Assert.AreEqual("/Account/Login", redirect.PageName);
        Assert.AreEqual("Identity", redirect.RouteValues!["area"]);
    }

    [TestMethod]
    public async Task RoditeljEvidencijaGet_RoditeljSaDjecom_VracaSamoAktivnuDjecuAbecednimRedom()
    {
        PrijaviKorisnika(RoditeljId);
        DodajDijete(1, "Zlatan", RoditeljId);
        DodajDijete(2, "Amar", RoditeljId);
        DodajDijete(3, "Neaktivna", RoditeljId, aktivno: false);
        DodajDijete(4, "Tuđe", 99);

        var rezultat = await Controller.RoditeljEvidencija();

        var model = ModelIz<RoditeljEvidencijaDolaskaOdlaskaViewModel>(rezultat);
        CollectionAssert.AreEqual(new[] { "Amar", "Zlatan" }, model.Djeca.Select(d => d.ImePrezime).ToArray());
    }

    [TestMethod]
    public async Task RoditeljEvidencijaGet_RoditeljSaDjecom_OdabireDolazakIPrvoDijete()
    {
        PrijaviKorisnika(RoditeljId);
        DodajDijete(1, "Zlatan", RoditeljId);
        DodajDijete(2, "Amar", RoditeljId);

        var rezultat = await Controller.RoditeljEvidencija();

        var model = ModelIz<RoditeljEvidencijaDolaskaOdlaskaViewModel>(rezultat);
        Assert.AreEqual(2, model.OdabranoDijeteId);
        Assert.AreEqual("DOLAZAK", model.TipDogadjaja);
    }

    [TestMethod]
    public async Task RoditeljEvidencijaGet_ImaAktivanQrKod_PostavljaGaUModel()
    {
        PrijaviKorisnika(RoditeljId);
        var qr = DodajQr(KodOdgajatelja(OdgajateljId));

        var rezultat = await Controller.RoditeljEvidencija();

        var model = ModelIz<RoditeljEvidencijaDolaskaOdlaskaViewModel>(rezultat);
        Assert.AreEqual(qr.Id, model.DnevniQRCode!.Id);
    }

    [TestMethod]
    public async Task RoditeljEvidencijaGet_BezDjeceIBezQrKoda_VracaPrazanModel()
    {
        PrijaviKorisnika(RoditeljId);

        var rezultat = await Controller.RoditeljEvidencija();

        var model = ModelIz<RoditeljEvidencijaDolaskaOdlaskaViewModel>(rezultat);
        Assert.IsNull(model.OdabranoDijeteId);
        Assert.IsNull(model.DnevniQRCode);
    }

    // ===== RoditeljEvidencija (POST) — validacija =====

    [TestMethod]
    public async Task RoditeljEvidencijaPost_BezPrijavljenogKorisnika_PreusmjeravaNaLogin()
    {
        NikoNijePrijavljen();

        var rezultat = await Controller.RoditeljEvidencija(NoviModel("kod", 1));

        Assert.AreEqual("/Account/Login", Kao<RedirectToPageResult>(rezultat).PageName);
    }

    [TestMethod]
    [DataRow((string?)null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task RoditeljEvidencijaPost_KodNijeSkeniran_VracaGresku(string? skeniraniKod)
    {
        PrijaviKorisnika(RoditeljId);

        var rezultat = await Controller.RoditeljEvidencija(NoviModel(skeniraniKod, 1));

        ProvjeriGresku(rezultat, "Niste skenirali QR kod");
    }

    [TestMethod]
    public async Task RoditeljEvidencijaPost_KodNePostoji_VracaGresku()
    {
        PrijaviKorisnika(RoditeljId);

        var rezultat = await Controller.RoditeljEvidencija(NoviModel("NEPOSTOJECI", 1));

        ProvjeriGresku(rezultat, "nije validan ili je istekao");
    }

    [TestMethod]
    [DataRow(false, 0, 60, DisplayName = "Neaktivan kod")]
    [DataRow(true, -1, 60, DisplayName = "Kod od jučer")]
    [DataRow(true, 0, -60, DisplayName = "Istekao kod")]
    public async Task RoditeljEvidencijaPost_KodNijeVazeci_VracaGresku(bool aktivan, int danaOdDanas, int minutaDoIsteka)
    {
        PrijaviKorisnika(RoditeljId);
        var qr = DodajQr(KodOdgajatelja(OdgajateljId), aktivan, danaOdDanas, minutaDoIsteka);

        var rezultat = await Controller.RoditeljEvidencija(NoviModel(qr.VrijednostKoda, 1));

        ProvjeriGresku(rezultat, "nije validan ili je istekao");
    }

    [TestMethod]
    public async Task RoditeljEvidencijaPost_DijeteNijeOdabrano_VracaGresku()
    {
        PrijaviKorisnika(RoditeljId);
        var qr = DodajQr(KodOdgajatelja(OdgajateljId));

        var rezultat = await Controller.RoditeljEvidencija(NoviModel(qr.VrijednostKoda, null));

        ProvjeriGresku(rezultat, "Odaberite dijete", nameof(RoditeljEvidencijaDolaskaOdlaskaViewModel.OdabranoDijeteId));
    }

    [TestMethod]
    [DataRow(99, true, DisplayName = "Dijete drugog roditelja")]
    [DataRow(RoditeljId, false, DisplayName = "Neaktivno dijete")]
    public async Task RoditeljEvidencijaPost_DijeteNijeDostupnoRoditelju_VracaGresku(int roditeljDjetetaId, bool aktivno)
    {
        PrijaviKorisnika(RoditeljId);
        var grupa = DodajGrupu(1, OdgajateljId);
        var dijete = DodajDijete(1, "Amar", roditeljDjetetaId, grupa, aktivno);
        var qr = DodajQr(KodOdgajatelja(OdgajateljId));

        var rezultat = await Controller.RoditeljEvidencija(NoviModel(qr.VrijednostKoda, dijete.Id));

        ProvjeriGresku(rezultat, "nije povezano s vašim profilom", nameof(RoditeljEvidencijaDolaskaOdlaskaViewModel.OdabranoDijeteId));
    }

    [TestMethod]
    [DynamicData(nameof(NeispravniKodovi))]
    public async Task RoditeljEvidencijaPost_KodNeispravnogFormata_VracaGresku(string vrijednostKoda)
    {
        PrijaviKorisnika(RoditeljId);
        var grupa = DodajGrupu(1, OdgajateljId);
        var dijete = DodajDijete(1, "Amar", RoditeljId, grupa);
        DodajQr(vrijednostKoda);

        var rezultat = await Controller.RoditeljEvidencija(NoviModel(vrijednostKoda, dijete.Id));

        ProvjeriGresku(rezultat, "QR kod nije ispravan");
    }

    [TestMethod]
    public async Task RoditeljEvidencijaPost_KodDrugogOdgajatelja_VracaGresku()
    {
        PrijaviKorisnika(RoditeljId);
        var grupa = DodajGrupu(1, 99);
        var dijete = DodajDijete(1, "Amar", RoditeljId, grupa);
        var qr = DodajQr(KodOdgajatelja(OdgajateljId));

        var rezultat = await Controller.RoditeljEvidencija(NoviModel(qr.VrijednostKoda, dijete.Id));

        ProvjeriGresku(rezultat, "ne pripada odgajatelju grupe");
    }

    [TestMethod]
    public async Task RoditeljEvidencijaPost_DijeteBezGrupe_VracaGresku()
    {
        PrijaviKorisnika(RoditeljId);
        var dijete = DodajDijete(1, "Amar", RoditeljId, grupa: null);
        var qr = DodajQr(KodOdgajatelja(OdgajateljId));

        var rezultat = await Controller.RoditeljEvidencija(NoviModel(qr.VrijednostKoda, dijete.Id));

        ProvjeriGresku(rezultat, "ne pripada odgajatelju grupe");
    }

    // ===== RoditeljEvidencija (POST) — evidentiranje =====

    [TestMethod]
    public async Task RoditeljEvidencijaPost_ValidanDolazak_VracaPorukuUspjeha()
    {
        var (dijete, qr) = PripremiValidanScenarij();

        var rezultat = await Controller.RoditeljEvidencija(NoviModel(qr.VrijednostKoda, dijete.Id));

        var model = ModelIz<RoditeljEvidencijaDolaskaOdlaskaViewModel>(rezultat);
        Assert.AreEqual("Dolazak je uspješno evidentiran za dijete Amar Hodžić.", model.UspjesnaPoruka);
    }

    [TestMethod]
    public async Task RoditeljEvidencijaPost_ValidanDolazak_SpremaEvidencijuUBazu()
    {
        var (dijete, qr) = PripremiValidanScenarij();

        await Controller.RoditeljEvidencija(NoviModel(qr.VrijednostKoda, dijete.Id));

        var evidencija = Context.EvidencijeDolaskaOdlaska.Single();
        Assert.AreEqual(dijete.Id, evidencija.DijeteId);
        Assert.AreEqual(qr.Id, evidencija.DnevniQRCodeId);
        Assert.AreEqual(TipDogadjaja.DOLAZAK, evidencija.TipDogadjaja);
        Assert.AreEqual(StatusEvidencije.EVIDENTIRANO, evidencija.StatusEvidencije);
        Assert.IsTrue(evidencija.ValidanQRKod);
    }

    [TestMethod]
    public async Task RoditeljEvidencijaPost_KodSaRazmacima_BivaPrihvacen()
    {
        var (dijete, qr) = PripremiValidanScenarij();

        var rezultat = await Controller.RoditeljEvidencija(NoviModel($"  {qr.VrijednostKoda}  ", dijete.Id));

        Assert.IsNotNull(ModelIz<RoditeljEvidencijaDolaskaOdlaskaViewModel>(rezultat).UspjesnaPoruka);
    }

    [TestMethod]
    [DynamicData(nameof(TipoviDolaska))]
    public async Task RoditeljEvidencijaPost_TipKojiSeTumaciKaoDolazak_EvidentiraDolazak(string? tip)
    {
        var (dijete, qr) = PripremiValidanScenarij();

        await Controller.RoditeljEvidencija(NoviModel(qr.VrijednostKoda, dijete.Id, tip!));

        Assert.AreEqual(TipDogadjaja.DOLAZAK, Context.EvidencijeDolaskaOdlaska.Single().TipDogadjaja);
    }

    [TestMethod]
    public async Task RoditeljEvidencijaPost_DvostrukiDolazak_VracaGresku()
    {
        var (dijete, qr) = PripremiValidanScenarij();
        DodajEvidenciju(dijete, TipDogadjaja.DOLAZAK, StatusEvidencije.EVIDENTIRANO, qr);

        var rezultat = await Controller.RoditeljEvidencija(NoviModel(qr.VrijednostKoda, dijete.Id));

        ProvjeriGresku(rezultat, "Dolazak za dijete Amar Hodžić je već evidentiran danas");
    }

    [TestMethod]
    public async Task RoditeljEvidencijaPost_OdlazakBezDolaska_VracaGresku()
    {
        var (dijete, qr) = PripremiValidanScenarij();

        var rezultat = await Controller.RoditeljEvidencija(NoviModel(qr.VrijednostKoda, dijete.Id, "ODLAZAK"));

        ProvjeriGresku(rezultat, "danas nije evidentiran");
    }

    [TestMethod]
    public async Task RoditeljEvidencijaPost_OdlazakSaOdbijenimDolaskom_VracaGresku()
    {
        var (dijete, qr) = PripremiValidanScenarij();
        DodajEvidenciju(dijete, TipDogadjaja.DOLAZAK, StatusEvidencije.ODBIJENO, qr);

        var rezultat = await Controller.RoditeljEvidencija(NoviModel(qr.VrijednostKoda, dijete.Id, "ODLAZAK"));

        ProvjeriGresku(rezultat, "danas nije evidentiran");
    }

    [TestMethod]
    public async Task RoditeljEvidencijaPost_OdlazakNakonDolaska_VracaPorukuUspjeha()
    {
        var (dijete, qr) = PripremiValidanScenarij();
        DodajEvidenciju(dijete, TipDogadjaja.DOLAZAK, StatusEvidencije.EVIDENTIRANO, qr);

        var rezultat = await Controller.RoditeljEvidencija(NoviModel(qr.VrijednostKoda, dijete.Id, "ODLAZAK"));

        var model = ModelIz<RoditeljEvidencijaDolaskaOdlaskaViewModel>(rezultat);
        Assert.AreEqual("Odlazak je uspješno evidentiran za dijete Amar Hodžić.", model.UspjesnaPoruka);
    }

    [TestMethod]
    public async Task RoditeljEvidencijaPost_DvostrukiOdlazak_VracaGresku()
    {
        var (dijete, qr) = PripremiValidanScenarij();
        DodajEvidenciju(dijete, TipDogadjaja.DOLAZAK, StatusEvidencije.EVIDENTIRANO, qr);
        DodajEvidenciju(dijete, TipDogadjaja.ODLAZAK, StatusEvidencije.EVIDENTIRANO, qr);

        var rezultat = await Controller.RoditeljEvidencija(NoviModel(qr.VrijednostKoda, dijete.Id, "ODLAZAK"));

        ProvjeriGresku(rezultat, "Odlazak za dijete Amar Hodžić je već evidentiran danas");
    }

    // ===== OdgajateljPregled =====

    [TestMethod]
    public async Task OdgajateljPregled_BezPrijavljenogKorisnika_PreusmjeravaNaLogin()
    {
        NikoNijePrijavljen();

        var rezultat = await Controller.OdgajateljPregled();

        Assert.AreEqual("/Account/Login", Kao<RedirectToPageResult>(rezultat).PageName);
    }

    [TestMethod]
    public async Task OdgajateljPregled_Poziv_JednomDohvataPrijavljenogKorisnika()
    {
        PrijaviKorisnika(OdgajateljId);

        await Controller.OdgajateljPregled();

        UserManagerMock.Verify(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()), Times.Once);
    }

    [TestMethod]
    public async Task OdgajateljPregled_BezFiltera_VracaSamoGrupeOdgajatelja()
    {
        PripremiPregledScenarij();

        var model = ModelIz<OdgajateljEvidencijaPregledViewModel>(await Controller.OdgajateljPregled());

        CollectionAssert.AreEqual(new[] { "A grupa", "B grupa" }, model.Grupe.Select(g => g.ImeGrupe).ToArray());
    }

    [TestMethod]
    public async Task OdgajateljPregled_BezFiltera_VracaSamoAktivnuDjecuSvojihGrupa()
    {
        PripremiPregledScenarij();

        var model = ModelIz<OdgajateljEvidencijaPregledViewModel>(await Controller.OdgajateljPregled());

        CollectionAssert.AreEqual(new[] { "Amar", "Berina", "Cemal" }, model.Djeca.Select(d => d.ImePrezime).ToArray());
    }

    [TestMethod]
    public async Task OdgajateljPregled_BezFiltera_VracaDanasnjeEvidencijeSvojeDjece()
    {
        PripremiPregledScenarij();

        var model = ModelIz<OdgajateljEvidencijaPregledViewModel>(await Controller.OdgajateljPregled());

        Assert.AreEqual(4, model.Evidencije.Count);
        Assert.AreEqual(DateTime.Today, model.Datum);
    }

    [TestMethod]
    public async Task OdgajateljPregled_BezFiltera_ISpravnoBrojiDolaskeOdlaskeIOdbijene()
    {
        PripremiPregledScenarij();

        var model = ModelIz<OdgajateljEvidencijaPregledViewModel>(await Controller.OdgajateljPregled());

        Assert.AreEqual(2, model.BrojDolazaka);
        Assert.AreEqual(2, model.BrojOdlazaka);
        Assert.AreEqual(1, model.BrojOdbijenih);
    }

    [TestMethod]
    public async Task OdgajateljPregled_FilterPoGrupi_VracaSamoDjecuTeGrupe()
    {
        PripremiPregledScenarij();

        var model = ModelIz<OdgajateljEvidencijaPregledViewModel>(await Controller.OdgajateljPregled(grupaId: 2));

        Assert.AreEqual(2, model.OdabranaGrupaId);
        CollectionAssert.AreEqual(new[] { "Cemal" }, model.Djeca.Select(d => d.ImePrezime).ToArray());
    }

    [TestMethod]
    public async Task OdgajateljPregled_FilterPoDjetetu_ZadrzavaCijeluListuZaDropdown()
    {
        PripremiPregledScenarij();

        var model = ModelIz<OdgajateljEvidencijaPregledViewModel>(await Controller.OdgajateljPregled(dijeteId: 1));

        Assert.AreEqual(3, model.Djeca.Count);
        Assert.AreEqual(1, model.OdabranoDijeteId);
    }

    [TestMethod]
    public async Task OdgajateljPregled_FilterPoDjetetu_VracaSamoEvidencijeTogDjeteta()
    {
        PripremiPregledScenarij();

        var model = ModelIz<OdgajateljEvidencijaPregledViewModel>(await Controller.OdgajateljPregled(dijeteId: 1));

        Assert.AreEqual(2, model.Evidencije.Count);
    }

    [TestMethod]
    public async Task OdgajateljPregled_FilterPoDatumu_VracaEvidencijeTogDana()
    {
        PripremiPregledScenarij();
        var jucer = DateTime.Today.AddDays(-1);

        var model = ModelIz<OdgajateljEvidencijaPregledViewModel>(await Controller.OdgajateljPregled(datum: jucer));

        Assert.AreEqual(1, model.Evidencije.Count);
        Assert.AreEqual(jucer, model.Datum);
    }

    [TestMethod]
    [DataRow("ODLAZAK", "ODLAZAK")]
    [DataRow(null, "DOLAZAK")]
    [DataRow("nesto-drugo", "DOLAZAK")]
    public async Task OdgajateljPregled_ZadaniTip_OdredujeOdabraniTip(string? tip, string ocekivaniTip)
    {
        PrijaviKorisnika(OdgajateljId);

        var model = ModelIz<OdgajateljEvidencijaPregledViewModel>(await Controller.OdgajateljPregled(tip: tip));

        Assert.AreEqual(ocekivaniTip, model.OdabraniTip);
    }

    [TestMethod]
    public async Task OdgajateljPregled_ImaSvojAktivanQr_VracaGaUModelu()
    {
        var qr = PripremiPregledScenarij();

        var model = ModelIz<OdgajateljEvidencijaPregledViewModel>(await Controller.OdgajateljPregled());

        Assert.AreEqual(qr.Id, model.AktivniQRKod!.Id);
    }

    [TestMethod]
    public async Task OdgajateljPregled_QrKodSamoDrugogOdgajatelja_AktivniQrJeNull()
    {
        PrijaviKorisnika(OdgajateljId);
        DodajQr(KodOdgajatelja(99));

        var model = ModelIz<OdgajateljEvidencijaPregledViewModel>(await Controller.OdgajateljPregled());

        Assert.IsNull(model.AktivniQRKod);
    }

    // ===== OdgajateljGenerisiQR =====

    [TestMethod]
    public async Task OdgajateljGenerisiQR_BezPrijavljenogKorisnika_PreusmjeravaNaLogin()
    {
        NikoNijePrijavljen();

        var rezultat = await Controller.OdgajateljGenerisiQR();

        Assert.AreEqual("/Account/Login", Kao<RedirectToPageResult>(rezultat).PageName);
    }

    [TestMethod]
    public async Task OdgajateljGenerisiQR_Poziv_PreusmjeravaNaPregledSaTipom()
    {
        PrijaviKorisnika(OdgajateljId);

        var rezultat = await Controller.OdgajateljGenerisiQR("ODLAZAK");

        var redirect = Kao<RedirectToActionResult>(rezultat);
        Assert.AreEqual(nameof(Controller.OdgajateljPregled), redirect.ActionName);
        Assert.AreEqual("ODLAZAK", redirect.RouteValues!["tip"]);
    }

    [TestMethod]
    public async Task OdgajateljGenerisiQR_Poziv_SpremaAktivanKodSaIspravnimPrefiksomIVazenjem()
    {
        PrijaviKorisnika(OdgajateljId);

        await Controller.OdgajateljGenerisiQR();

        var kod = Context.DnevniQRCodovi.Single();
        Assert.IsTrue(kod.Aktivan);
        Assert.IsTrue(kod.VrijednostKoda.StartsWith($"EVRTIC-{DateTime.Today:yyyyMMdd}-O{OdgajateljId}-"));
        Assert.AreEqual(DateTime.Today, kod.DatumVazenja);
        Assert.AreEqual(DateTime.Today.AddDays(1).AddSeconds(-1), kod.VrijemeIsteka);
    }

    [TestMethod]
    public async Task OdgajateljGenerisiQR_Poziv_PostavljaPorukuUTempData()
    {
        PrijaviKorisnika(OdgajateljId);

        await Controller.OdgajateljGenerisiQR();

        Assert.AreEqual("Novi dnevni QR kod je uspješno generisan.", Controller.TempData["Poruka"]);
    }

    [TestMethod]
    public async Task OdgajateljGenerisiQR_PostojiStariKodOdgajatelja_DeaktivirajGa()
    {
        PrijaviKorisnika(OdgajateljId);
        var stari = DodajQr(KodOdgajatelja(OdgajateljId));

        await Controller.OdgajateljGenerisiQR();

        Assert.IsFalse(Context.DnevniQRCodovi.Single(q => q.Id == stari.Id).Aktivan);
    }

    [TestMethod]
    public async Task OdgajateljGenerisiQR_PostojiKodDrugogOdgajatelja_OstavljaGaAktivnim()
    {
        PrijaviKorisnika(OdgajateljId);
        var tudji = DodajQr(KodOdgajatelja(99));

        await Controller.OdgajateljGenerisiQR();

        Assert.IsTrue(Context.DnevniQRCodovi.Single(q => q.Id == tudji.Id).Aktivan);
    }

    // ===== ViewModeli =====

    [TestMethod]
    public void RoditeljViewModel_NoviObjekat_ImaPodrazumijevaneVrijednosti()
    {
        var model = new RoditeljEvidencijaDolaskaOdlaskaViewModel();

        Assert.AreEqual(string.Empty, model.UneseniKodDjeteta);
        Assert.AreEqual("DOLAZAK", model.TipDogadjaja);
        Assert.AreEqual(0, model.Djeca.Count);
    }

    [TestMethod]
    public void RoditeljViewModel_PostavljenaSvojstva_VracajuPostavljeneVrijednosti()
    {
        var model = new RoditeljEvidencijaDolaskaOdlaskaViewModel { UneseniKodDjeteta = "K1" };

        Assert.AreEqual("K1", model.UneseniKodDjeteta);
    }

    [TestMethod]
    public void OdgajateljViewModel_NoviObjekat_ImaPodrazumijevaneVrijednosti()
    {
        var model = new OdgajateljEvidencijaPregledViewModel();

        Assert.AreEqual(DateTime.Today, model.Datum);
        Assert.AreEqual("DOLAZAK", model.OdabraniTip);
        Assert.AreEqual(0, model.Grupe.Count);
        Assert.AreEqual(0, model.Evidencije.Count);
    }

    // ===== Pomoćne metode =====

    private (Dijete dijete, DnevniQRCode qr) PripremiValidanScenarij()
    {
        PrijaviKorisnika(RoditeljId);
        var grupa = DodajGrupu(1, OdgajateljId);
        var dijete = DodajDijete(1, "Amar Hodžić", RoditeljId, grupa);
        var qr = DodajQr(KodOdgajatelja(OdgajateljId));
        return (dijete, qr);
    }

    private DnevniQRCode PripremiPregledScenarij()
    {
        PrijaviKorisnika(OdgajateljId);
        var grupaA = DodajGrupu(1, OdgajateljId, "A grupa");
        var grupaB = DodajGrupu(2, OdgajateljId, "B grupa");
        var tudjaGrupa = DodajGrupu(3, 99, "Tuđa grupa");
        var amar = DodajDijete(1, "Amar", RoditeljId, grupaA);
        var berina = DodajDijete(2, "Berina", RoditeljId, grupaA);
        DodajDijete(3, "Cemal", RoditeljId, grupaB);
        var dino = DodajDijete(4, "Dino", RoditeljId, tudjaGrupa);
        DodajDijete(5, "Emina", RoditeljId, grupaA, aktivno: false);
        var qr = DodajQr(KodOdgajatelja(OdgajateljId));
        DodajEvidenciju(amar, TipDogadjaja.DOLAZAK, StatusEvidencije.EVIDENTIRANO, qr);
        DodajEvidenciju(amar, TipDogadjaja.ODLAZAK, StatusEvidencije.EVIDENTIRANO, qr);
        DodajEvidenciju(berina, TipDogadjaja.DOLAZAK, StatusEvidencije.EVIDENTIRANO, qr);
        DodajEvidenciju(berina, TipDogadjaja.ODLAZAK, StatusEvidencije.ODBIJENO, qr);
        DodajEvidenciju(dino, TipDogadjaja.DOLAZAK, StatusEvidencije.EVIDENTIRANO, qr);
        DodajEvidenciju(amar, TipDogadjaja.DOLAZAK, StatusEvidencije.EVIDENTIRANO, qr, danaOdDanas: -1);
        return qr;
    }

    private static RoditeljEvidencijaDolaskaOdlaskaViewModel NoviModel(string? kod, int? dijeteId, string tip = "DOLAZAK") =>
        new() { ScaniraniKod = kod, OdabranoDijeteId = dijeteId, TipDogadjaja = tip };
}