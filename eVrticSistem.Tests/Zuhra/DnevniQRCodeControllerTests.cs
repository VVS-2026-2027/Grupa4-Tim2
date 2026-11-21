using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EVrtic.Controllers;
using EVrtic.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static eVrticSistem.Tests.Zuhra.ZuhraTestHelper;

namespace eVrticSistem.Tests.Zuhra
{
    [TestClass]
    public class DnevniQRCodeControllerTests
    {
        private static InMemoryDatabaseRoot korijen = null!;
        private ApplicationDbContext kontekst = null!;
        private DnevniQRCodeController kontroler = null!;

        [ClassInitialize]
        public static void ClassSetup(TestContext context) => korijen = NapraviKorijen();

        [ClassCleanup]
        public static void ClassCleanup() => korijen = null!;

        [TestInitialize]
        public void TestSetup()
        {
            kontekst = NapraviKontekst(korijen);
            kontroler = new DnevniQRCodeController(kontekst);
            PripremiKontroler(kontroler);
        }

        [TestCleanup]
        public void TestCleanup() => ObrisiKontekst(kontekst);

        // ---------- Index ----------
        [TestMethod]
        public async Task Index_PostojiAktivanKodZaDanas_VracaGaUViewModelu()
        {
            var kod = DodajKod(kontekst, "EVRTIC-TEST-AKTIVAN");

            var vm = Model<DnevniQRCodePregledViewModel>(await kontroler.Index());

            Assert.AreEqual(kod.Id, vm.AktivniKod!.Id);
        }

        [TestMethod]
        public async Task Index_NemaKodova_AktivniKodJeNullAHistorijaPrazna()
        {
            var vm = Model<DnevniQRCodePregledViewModel>(await kontroler.Index());

            Assert.IsNull(vm.AktivniKod);
            Assert.AreEqual(0, vm.HistorijaKodova.Count);
        }

        [TestMethod]
        public async Task Index_VišeOdDesetKodova_HistorijaJeOgraničenaNaDeset()
        {
            DodajViseKodova(kontekst, 12);

            var vm = Model<DnevniQRCodePregledViewModel>(await kontroler.Index());

            Assert.AreEqual(10, vm.HistorijaKodova.Count);
        }

        // Data driven (CSV): aktivan;pomakDana;pomakSati;ocekujeAktivan
        [TestMethod]
        [DynamicData(nameof(QrAktivnostPodaci), DynamicDataSourceType.Method)]
        public async Task Index_RazlicitaStanjaKoda_AktivniKodOdgovaraOčekivanom(
            bool aktivan, int pomakDana, int pomakSati, bool ocekujeAktivan)
        {
            var kod = DodajKod(kontekst, "EVRTIC-TEST-CSV", aktivan);
            kod.DatumVazenja = DateTime.Today.AddDays(pomakDana);
            kod.VrijemeIsteka = DateTime.Now.AddHours(pomakSati);
            kontekst.SaveChanges();

            var vm = Model<DnevniQRCodePregledViewModel>(await kontroler.Index());

            Assert.AreEqual(ocekujeAktivan, vm.AktivniKod != null);
        }

        public static IEnumerable<object[]> QrAktivnostPodaci() =>
            UcitajCsv("qr_aktivnost.csv", typeof(bool), typeof(int), typeof(int), typeof(bool));

        // ---------- GenerisiDanas ----------
        [TestMethod]
        public async Task GenerisiDanas_NemaKodova_KreiraJedanAktivanKodSaIstekomNaKrajuDana()
        {
            await kontroler.GenerisiDanas();

            var novi = kontekst.DnevniQRCodovi.Single();
            Assert.IsTrue(novi.Aktivan);
            StringAssert.StartsWith(novi.VrijednostKoda, DanasnjiPrefiks());
            Assert.AreEqual(DateTime.Today.AddDays(1).AddSeconds(-1), novi.VrijemeIsteka);
        }

        [TestMethod]
        public async Task GenerisiDanas_PostojiAktivanKod_DeaktiviraStariIDodajeNovi()
        {
            var stari = DodajKod(kontekst, "EVRTIC-TEST-STARI");

            await kontroler.GenerisiDanas();

            Assert.IsFalse(kontekst.DnevniQRCodovi.Find(stari.Id)!.Aktivan);
            Assert.AreEqual(1, kontekst.DnevniQRCodovi.Count(q => q.Aktivan));
            Assert.AreEqual(2, kontekst.DnevniQRCodovi.Count());
        }

        [TestMethod]
        public async Task GenerisiDanas_Uspjeh_PostavljaPorukuIPreusmjeravaNaIndex()
        {
            var rezultat = await kontroler.GenerisiDanas();

            Assert.AreEqual("Novi dnevni QR kod je uspješno generisan.", TempPoruka(kontroler, "Poruka"));
            Assert.AreEqual(nameof(DnevniQRCodeController.Index), ((RedirectToActionResult)rezultat).ActionName);
        }

        // ---------- Deaktiviraj ----------
        [TestMethod]
        public async Task Deaktiviraj_PostojeciKod_PostavljaAktivanNaFalse()
        {
            var kod = DodajKod(kontekst, "EVRTIC-TEST-DEAKT");

            var rezultat = await kontroler.Deaktiviraj(kod.Id);

            Assert.IsFalse(kontekst.DnevniQRCodovi.Find(kod.Id)!.Aktivan);
            Assert.AreEqual("QR kod je deaktiviran.", TempPoruka(kontroler, "Poruka"));
            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
        }

        [TestMethod]
        public async Task Deaktiviraj_NepostojeciId_VracaNotFound()
        {
            var rezultat = await kontroler.Deaktiviraj(999);

            Assert.IsInstanceOfType(rezultat, typeof(NotFoundResult));
        }
    }
}
