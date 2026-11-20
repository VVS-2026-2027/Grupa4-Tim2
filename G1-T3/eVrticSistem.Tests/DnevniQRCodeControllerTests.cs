using EVrtic.Controllers;
using EVrtic.Data;
using EVrtic.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Text.RegularExpressions;

namespace EVrtic.Tests;

[TestClass]
public class DnevniQRCodeControllerTests
{
    private ApplicationDbContext _context = null!;
    private DnevniQRCodeController _controller = null!;

    public static IEnumerable<object[]> SlucajeviHistorije => TestPodaciXml.HistorijaKodova();

    [TestInitialize]
    public void PripremiOkruzenje()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(opcije);
        _controller = new DnevniQRCodeController(_context)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };
    }

    [TestCleanup]
    public void Pocisti() => _context.Dispose();

    // ===== Index =====

    [TestMethod]
    public async Task Index_NemaKodova_VracaPrazanPregled()
    {
        var model = ModelIz(await _controller.Index());

        Assert.IsNull(model.AktivniKod);
        Assert.AreEqual(0, model.HistorijaKodova.Count);
    }

    [TestMethod]
    public async Task Index_ImaAktivanDanasnjiKod_VracaGaKaoAktivni()
    {
        var kod = DodajKod("KOD-1");

        var model = ModelIz(await _controller.Index());

        Assert.AreEqual(kod.Id, model.AktivniKod!.Id);
    }

    [TestMethod]
    public async Task Index_ViseAktivnihKodova_VracaOnajSaNajkasnijimIstekom()
    {
        DodajKod("RANIJI", minutaDoIsteka: 10);
        var kasniji = DodajKod("KASNIJI", minutaDoIsteka: 300);

        var model = ModelIz(await _controller.Index());

        Assert.AreEqual(kasniji.Id, model.AktivniKod!.Id);
    }

    [TestMethod]
    [DataRow(false, 0, 60, DisplayName = "Neaktivan kod")]
    [DataRow(true, -1, 60, DisplayName = "Kod od jučer")]
    [DataRow(true, 0, -60, DisplayName = "Istekao kod")]
    public async Task Index_KodNijeVazeci_AktivniKodJeNull(bool aktivan, int danaOdDanas, int minutaDoIsteka)
    {
        DodajKod("KOD-1", aktivan, danaOdDanas, minutaDoIsteka);

        var model = ModelIz(await _controller.Index());

        Assert.IsNull(model.AktivniKod);
    }

    [TestMethod]
    public async Task Index_ViseKodova_HistorijaJeSortiranaPoDatumuPaPoIsteku()
    {
        DodajKod("JUCER", aktivan: false, danaOdDanas: -1, minutaDoIsteka: 500);
        DodajKod("DANAS-RANIJI", minutaDoIsteka: 10);
        DodajKod("DANAS-KASNIJI", minutaDoIsteka: 300);

        var model = ModelIz(await _controller.Index());

        var ocekivano = new[] { "DANAS-KASNIJI", "DANAS-RANIJI", "JUCER" };
        CollectionAssert.AreEqual(ocekivano, model.HistorijaKodova.Select(k => k.VrijednostKoda).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(SlucajeviHistorije))]
    public async Task Index_ProizvoljanBrojKodova_HistorijaImaNajvise10Zapisa(int ukupnoKodova, int ocekivano)
    {
        DodajViseKodova(ukupnoKodova);

        var model = ModelIz(await _controller.Index());

        Assert.AreEqual(ocekivano, model.HistorijaKodova.Count);
    }

    // ===== GenerisiDanas =====

    [TestMethod]
    public async Task GenerisiDanas_Poziv_PreusmjeravaNaIndex()
    {
        var rezultat = await _controller.GenerisiDanas();

        Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual(nameof(_controller.Index), ((RedirectToActionResult)rezultat).ActionName);
    }

    [TestMethod]
    public async Task GenerisiDanas_Poziv_PostavljaPorukuUTempData()
    {
        await _controller.GenerisiDanas();

        Assert.AreEqual("Novi dnevni QR kod je uspješno generisan.", _controller.TempData["Poruka"]);
    }

    [TestMethod]
    public async Task GenerisiDanas_Poziv_SpremaAktivanKodZaDanasDoKrajaDana()
    {
        await _controller.GenerisiDanas();

        var kod = _context.DnevniQRCodovi.Single();
        Assert.IsTrue(kod.Aktivan);
        Assert.AreEqual(DateTime.Today, kod.DatumVazenja);
        Assert.AreEqual(DateTime.Today.AddDays(1).AddSeconds(-1), kod.VrijemeIsteka);
    }

    [TestMethod]
    public async Task GenerisiDanas_Poziv_VrijednostKodaImaIspravanFormat()
    {
        await _controller.GenerisiDanas();

        var ocekivaniUzorak = new Regex($"^EVRTIC-{DateTime.Today:yyyyMMdd}-[0-9A-F]{{10}}$");
        StringAssert.Matches(_context.DnevniQRCodovi.Single().VrijednostKoda, ocekivaniUzorak);
    }

    [TestMethod]
    public async Task GenerisiDanas_PozvanDvaPuta_GenerisaRazliciteVrijednosti()
    {
        await _controller.GenerisiDanas();
        await _controller.GenerisiDanas();

        var vrijednosti = _context.DnevniQRCodovi.Select(k => k.VrijednostKoda).Distinct().Count();
        Assert.AreEqual(2, vrijednosti);
    }

    [TestMethod]
    public async Task GenerisiDanas_PostojiAktivanKodDanas_DeaktivirajGa()
    {
        var stari = DodajKod("STARI");

        await _controller.GenerisiDanas();

        Assert.IsFalse(_context.DnevniQRCodovi.Single(k => k.Id == stari.Id).Aktivan);
    }

    [TestMethod]
    public async Task GenerisiDanas_PostojiAktivanKodDanas_NoviKodJeJedinoAktivan()
    {
        DodajKod("STARI-1");
        DodajKod("STARI-2");

        await _controller.GenerisiDanas();

        Assert.AreEqual(1, _context.DnevniQRCodovi.Count(k => k.Aktivan));
    }

    [TestMethod]
    public async Task GenerisiDanas_PostojiAktivanKodOdJucer_OstavljaGaAktivnim()
    {
        var jucerasnji = DodajKod("JUCER", aktivan: true, danaOdDanas: -1);

        await _controller.GenerisiDanas();

        Assert.IsTrue(_context.DnevniQRCodovi.Single(k => k.Id == jucerasnji.Id).Aktivan);
    }

    // ===== Deaktiviraj =====

    [TestMethod]
    public async Task Deaktiviraj_PostojeciKod_PostavljaAktivanNaFalse()
    {
        var kod = DodajKod("KOD-1");

        await _controller.Deaktiviraj(kod.Id);

        Assert.IsFalse(_context.DnevniQRCodovi.Single().Aktivan);
    }

    [TestMethod]
    public async Task Deaktiviraj_PostojeciKod_PreusmjeravaNaIndexSaPorukom()
    {
        var kod = DodajKod("KOD-1");

        var rezultat = await _controller.Deaktiviraj(kod.Id);

        Assert.AreEqual(nameof(_controller.Index), ((RedirectToActionResult)rezultat).ActionName);
        Assert.AreEqual("QR kod je deaktiviran.", _controller.TempData["Poruka"]);
    }

    [TestMethod]
    public async Task Deaktiviraj_NepostojeciKod_VracaNotFound()
    {
        var rezultat = await _controller.Deaktiviraj(999);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task Deaktiviraj_JedanKodOdViseNjih_OstaleNeDira()
    {
        var meta = DodajKod("META");
        var ostali = DodajKod("OSTALI");

        await _controller.Deaktiviraj(meta.Id);

        Assert.IsTrue(_context.DnevniQRCodovi.Single(k => k.Id == ostali.Id).Aktivan);
    }

    // ===== ViewModel =====

    [TestMethod]
    public void PregledViewModel_NoviObjekat_ImaPrazneVrijednosti()
    {
        var model = new DnevniQRCodePregledViewModel();

        Assert.IsNull(model.AktivniKod);
        Assert.AreEqual(0, model.HistorijaKodova.Count);
    }

    // ===== Pomoćne metode =====

    private DnevniQRCode DodajKod(string vrijednost, bool aktivan = true, int danaOdDanas = 0, int minutaDoIsteka = 60)
    {
        var kod = new DnevniQRCode
        {
            VrijednostKoda = vrijednost,
            Aktivan = aktivan,
            DatumVazenja = DateTime.Today.AddDays(danaOdDanas),
            VrijemeIsteka = DateTime.Now.AddMinutes(minutaDoIsteka)
        };
        _context.DnevniQRCodovi.Add(kod);
        _context.SaveChanges();
        return kod;
    }

    private void DodajViseKodova(int broj)
    {
        for (var i = 0; i < broj; i++)
        {
            DodajKod($"KOD-{i}", aktivan: false, danaOdDanas: -i);
        }
    }

    private static DnevniQRCodePregledViewModel ModelIz(IActionResult rezultat)
    {
        Assert.IsInstanceOfType<ViewResult>(rezultat);
        var model = ((ViewResult)rezultat).Model;
        Assert.IsInstanceOfType<DnevniQRCodePregledViewModel>(model);
        return (DnevniQRCodePregledViewModel)model!;
    }
}