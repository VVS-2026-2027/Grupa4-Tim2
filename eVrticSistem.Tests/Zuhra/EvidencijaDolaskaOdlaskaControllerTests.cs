using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EVrtic.Controllers;
using EVrtic.Data;
using EVrtic.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static eVrticSistem.Tests.Zuhra.ZuhraTestHelper;

namespace eVrticSistem.Tests.Zuhra
{
    [TestClass]
    public class EvidencijaDolaskaOdlaskaControllerTests
    {
        private static InMemoryDatabaseRoot korijen = null!;
        private ApplicationDbContext kontekst = null!;
        private Mock<UserManager<Korisnik>> userManager = null!;
        private EvidencijaDolaskaOdlaskaController kontroler = null!;

        [ClassInitialize]
        public static void ClassSetup(TestContext context) => korijen = NapraviKorijen();

        [ClassCleanup]
        public static void ClassCleanup() => korijen = null!;

        [TestInitialize]
        public void TestSetup()
        {
            kontekst = NapraviKontekst(korijen);
            NapuniOsnovnePodatke(kontekst);
            userManager = NapraviUserManager(NapraviRoditelja(RoditeljId, "roditelj1@test.ba"));
            kontroler = new EvidencijaDolaskaOdlaskaController(kontekst, userManager.Object);
            PripremiKontroler(kontroler);
        }

        [TestCleanup]
        public void TestCleanup() => ObrisiKontekst(kontekst);

        private static RoditeljEvidencijaDolaskaOdlaskaViewModel NapraviModel(string? kod, int? dijeteId, string tip) =>
            new RoditeljEvidencijaDolaskaOdlaskaViewModel { ScaniraniKod = kod, OdabranoDijeteId = dijeteId, TipDogadjaja = tip };

        // ---------- administratorske akcije (Forbid) ----------
        [TestMethod]
        public async Task Index_Uvijek_VracaForbid() =>
            Assert.IsInstanceOfType(await kontroler.Index(), typeof(ForbidResult));

        [TestMethod]
        public async Task Details_Uvijek_VracaForbid() =>
            Assert.IsInstanceOfType(await kontroler.Details(1), typeof(ForbidResult));

        [TestMethod]
        public void Create_Get_VracaForbid() =>
            Assert.IsInstanceOfType(kontroler.Create(), typeof(ForbidResult));

        [TestMethod]
        public async Task Create_Post_VracaForbid() =>
            Assert.IsInstanceOfType(await kontroler.Create(new EvidencijaDolaskaOdlaska()), typeof(ForbidResult));

        [TestMethod]
        public async Task Edit_Get_VracaForbid() =>
            Assert.IsInstanceOfType(await kontroler.Edit(1), typeof(ForbidResult));

        [TestMethod]
        public async Task Edit_Post_VracaForbid() =>
            Assert.IsInstanceOfType(await kontroler.Edit(1, new EvidencijaDolaskaOdlaska()), typeof(ForbidResult));

        [TestMethod]
        public async Task Delete_Get_VracaForbid() =>
            Assert.IsInstanceOfType(await kontroler.Delete(1), typeof(ForbidResult));

        // ---------- DeleteConfirmed ----------
        [TestMethod]
        public async Task DeleteConfirmed_PostojecaEvidencija_BrisePaPreusmjerava()
        {
            DodajEvidenciju(kontekst, DijeteId, TipDogadjaja.DOLAZAK, StatusEvidencije.EVIDENTIRANO);
            var id = kontekst.EvidencijeDolaskaOdlaska.Single().Id;

            var rezultat = await kontroler.DeleteConfirmed(id);

            Assert.AreEqual(0, kontekst.EvidencijeDolaskaOdlaska.Count());
            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
        }

        [TestMethod]
        public async Task DeleteConfirmed_NepostojecaEvidencija_NistaNeBriseIPreusmjerava()
        {
            DodajEvidenciju(kontekst, DijeteId, TipDogadjaja.DOLAZAK, StatusEvidencije.EVIDENTIRANO);

            var rezultat = await kontroler.DeleteConfirmed(999);

            Assert.AreEqual(1, kontekst.EvidencijeDolaskaOdlaska.Count());
            Assert.IsInstanceOfType(rezultat, typeof(RedirectToActionResult));
        }

        // ---------- RoditeljEvidencija GET ----------
        [TestMethod]
        public async Task RoditeljEvidencija_Get_PrijavljeniRoditelj_VracaDjecuIAktivniKod()
        {
            var vm = Model<RoditeljEvidencijaDolaskaOdlaskaViewModel>(await kontroler.RoditeljEvidencija());

            Assert.AreEqual(2, vm.Djeca.Count);
            Assert.AreEqual(DijeteId, vm.OdabranoDijeteId);
            Assert.AreEqual("DOLAZAK", vm.TipDogadjaja);
            Assert.AreEqual(SeedKodId, vm.DnevniQRCode!.Id);
        }

        [TestMethod]
        public async Task RoditeljEvidencija_Get_NemaAktivnogKoda_DnevniQRCodeJeNull()
        {
            DeaktivirajSveKodove(kontekst);

            var vm = Model<RoditeljEvidencijaDolaskaOdlaskaViewModel>(await kontroler.RoditeljEvidencija());

            Assert.IsNull(vm.DnevniQRCode);
        }

        [TestMethod]
        public async Task RoditeljEvidencija_Get_NijePrijavljen_PreusmjeravaNaLogin()
        {
            PostaviKorisnika(userManager, null);

            var rezultat = await kontroler.RoditeljEvidencija();

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToPageResult));
        }

        [TestMethod]
        public async Task RoditeljEvidencija_UserManagerBacaIzuzetak_IzuzetakSePropagira()
        {
            userManager.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ThrowsAsync(new InvalidOperationException());

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => kontroler.RoditeljEvidencija());
        }

        // ---------- RoditeljEvidencija POST: greške ----------
        [TestMethod]
        public async Task RoditeljEvidencija_Post_NijePrijavljen_PreusmjeravaNaLogin()
        {
            PostaviKorisnika(userManager, null);

            var rezultat = await kontroler.RoditeljEvidencija(NapraviModel(ValidanKod(), DijeteId, "DOLAZAK"));

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToPageResult));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public async Task RoditeljEvidencija_Post_PrazanKod_DodajeGreskuONeskeniranom(string? kod)
        {
            await kontroler.RoditeljEvidencija(NapraviModel(kod, DijeteId, "DOLAZAK"));

            StringAssert.Contains(SvePoruke(kontroler.ModelState), "Niste skenirali");
        }

        [TestMethod]
        public async Task RoditeljEvidencija_Post_NepostojeciKod_DodajeGreskuONevalidnom()
        {
            await kontroler.RoditeljEvidencija(NapraviModel("NEPOSTOJECI-KOD", DijeteId, "DOLAZAK"));

            StringAssert.Contains(SvePoruke(kontroler.ModelState), "nije validan");
        }

        [TestMethod]
        public async Task RoditeljEvidencija_Post_IstekliKod_DodajeGreskuONevalidnom()
        {
            DeaktivirajSveKodove(kontekst);

            await kontroler.RoditeljEvidencija(NapraviModel(ValidanKod(), DijeteId, "DOLAZAK"));

            StringAssert.Contains(SvePoruke(kontroler.ModelState), "nije validan");
        }

        [TestMethod]
        public async Task RoditeljEvidencija_Post_BezOdabranogDjeteta_DodajeGresku()
        {
            await kontroler.RoditeljEvidencija(NapraviModel(ValidanKod(), null, "DOLAZAK"));

            StringAssert.Contains(SvePoruke(kontroler.ModelState), "Odaberite dijete");
        }

        [TestMethod]
        public async Task RoditeljEvidencija_Post_TudeDijete_DodajeGreskuONepovezanomDjetetu()
        {
            await kontroler.RoditeljEvidencija(NapraviModel(ValidanKod(), TudeDijeteId, "DOLAZAK"));

            StringAssert.Contains(SvePoruke(kontroler.ModelState), "nije povezano");
        }

        [TestMethod]
        public async Task RoditeljEvidencija_Post_DijeteBezGrupe_DodajeGreskuOPripadnostiKoda()
        {
            UkloniGrupuDjetetu(kontekst, DijeteId);

            await kontroler.RoditeljEvidencija(NapraviModel(ValidanKod(), DijeteId, "DOLAZAK"));

            StringAssert.Contains(SvePoruke(kontroler.ModelState), "ne pripada odgajatelju");
        }

        // Data driven (CSV): kod;ocekivanaPoruka
        [TestMethod]
        [DynamicData(nameof(KodoviPodaci), DynamicDataSourceType.Method)]
        public async Task RoditeljEvidencija_Post_NeispravanFormatKoda_DodajeOčekivanuGresku(string kod, string ocekivanaPoruka)
        {
            DodajKod(kontekst, kod);

            await kontroler.RoditeljEvidencija(NapraviModel(kod, DijeteId, "DOLAZAK"));

            StringAssert.Contains(SvePoruke(kontroler.ModelState), ocekivanaPoruka);
        }

        public static IEnumerable<object[]> KodoviPodaci() =>
            UcitajCsv("evidencija_kodovi.csv", typeof(string), typeof(string));

        // Data driven (JSON): tip, postojeciTipovi, ocekivanaPoruka
        [TestMethod]
        [DynamicData(nameof(EvidencijaSlucajevi), DynamicDataSourceType.Method)]
        public async Task RoditeljEvidencija_Post_NedozvoljenaEvidencija_DodajeOčekivanuGresku(
            string tip, string[] postojeciTipovi, string ocekivanaPoruka)
        {
            DodajEvidencijePoTipovima(kontekst, DijeteId, postojeciTipovi);

            await kontroler.RoditeljEvidencija(NapraviModel(ValidanKod(), DijeteId, tip));

            StringAssert.Contains(SvePoruke(kontroler.ModelState), ocekivanaPoruka);
        }

        public static IEnumerable<object[]> EvidencijaSlucajevi() =>
            UcitajJson<EvidencijaSlucaj>("evidencija_slucajevi.json",
                s => new object[] { s.Tip, s.PostojeciTipovi.ToArray(), s.OcekivanaPoruka });

        // ---------- RoditeljEvidencija POST: uspjeh ----------
        [TestMethod]
        public async Task RoditeljEvidencija_Post_ValidanDolazak_SpremaEvidencijuIVracaPoruku()
        {
            var vm = Model<RoditeljEvidencijaDolaskaOdlaskaViewModel>(
                await kontroler.RoditeljEvidencija(NapraviModel("  " + ValidanKod() + "  ", DijeteId, "DOLAZAK")));

            var spremljena = kontekst.EvidencijeDolaskaOdlaska.Single();
            Assert.AreEqual(TipDogadjaja.DOLAZAK, spremljena.TipDogadjaja);
            Assert.AreEqual(DijeteId, spremljena.DijeteId);
            Assert.AreEqual(StatusEvidencije.EVIDENTIRANO, spremljena.StatusEvidencije);
            StringAssert.Contains(vm.UspjesnaPoruka!, "uspješno evidentiran");
        }

        [TestMethod]
        public async Task RoditeljEvidencija_Post_OdlazakNakonDolaska_SpremaOdlazak()
        {
            DodajEvidenciju(kontekst, DijeteId, TipDogadjaja.DOLAZAK, StatusEvidencije.EVIDENTIRANO);

            var vm = Model<RoditeljEvidencijaDolaskaOdlaskaViewModel>(
                await kontroler.RoditeljEvidencija(NapraviModel(ValidanKod(), DijeteId, "ODLAZAK")));

            Assert.AreEqual(2, kontekst.EvidencijeDolaskaOdlaska.Count());
            StringAssert.StartsWith(vm.UspjesnaPoruka!, "Odlazak");
        }

        [TestMethod]
        public async Task RoditeljEvidencija_Post_NepoznatTip_KoristiPrvuVrijednostEnuma()
        {
            await kontroler.RoditeljEvidencija(NapraviModel(ValidanKod(), DijeteId, "NEPOZNATO"));

            Assert.AreEqual(TipDogadjaja.DOLAZAK, kontekst.EvidencijeDolaskaOdlaska.Single().TipDogadjaja);
        }

        // ---------- OdgajateljPregled ----------
        private void PripremiOdgajateljaIEvidencije()
        {
            PostaviKorisnika(userManager, NapraviOdgajatelja(OdgajateljId));
            DodajEvidenciju(kontekst, DijeteId, TipDogadjaja.DOLAZAK, StatusEvidencije.EVIDENTIRANO);
            DodajEvidenciju(kontekst, DijeteId, TipDogadjaja.ODLAZAK, StatusEvidencije.EVIDENTIRANO);
            DodajEvidenciju(kontekst, DrugoDijeteId, TipDogadjaja.DOLAZAK, StatusEvidencije.ODBIJENO);
            DodajEvidenciju(kontekst, TudeDijeteId, TipDogadjaja.DOLAZAK, StatusEvidencije.EVIDENTIRANO);
        }

        [TestMethod]
        public async Task OdgajateljPregled_NijePrijavljen_PreusmjeravaNaLogin()
        {
            PostaviKorisnika(userManager, null);

            var rezultat = await kontroler.OdgajateljPregled();

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToPageResult));
        }

        [TestMethod]
        public async Task OdgajateljPregled_BezFiltera_VracaEvidencijeSvojihGrupaISaBrojačima()
        {
            PripremiOdgajateljaIEvidencije();

            var vm = Model<OdgajateljEvidencijaPregledViewModel>(await kontroler.OdgajateljPregled());

            Assert.AreEqual(1, vm.Grupe.Count);
            Assert.AreEqual(2, vm.Djeca.Count);
            Assert.AreEqual(3, vm.Evidencije.Count);
            Assert.AreEqual(2, vm.BrojDolazaka);
            Assert.AreEqual(1, vm.BrojOdlazaka);
            Assert.AreEqual(1, vm.BrojOdbijenih);
            Assert.AreEqual(SeedKodId, vm.AktivniQRKod!.Id);
        }

        [TestMethod]
        public async Task OdgajateljPregled_FilterDijete_EvidencijeSamoZaDijeteADropdownSvaDjeca()
        {
            PripremiOdgajateljaIEvidencije();

            var vm = Model<OdgajateljEvidencijaPregledViewModel>(
                await kontroler.OdgajateljPregled(dijeteId: DijeteId));

            Assert.AreEqual(2, vm.Evidencije.Count);
            Assert.AreEqual(2, vm.Djeca.Count);
            Assert.AreEqual(DijeteId, vm.OdabranoDijeteId);
        }

        [TestMethod]
        public async Task OdgajateljPregled_FilterGrupa_VracaDjecuSamoTeGrupe()
        {
            PripremiOdgajateljaIEvidencije();

            var vm = Model<OdgajateljEvidencijaPregledViewModel>(
                await kontroler.OdgajateljPregled(grupaId: GrupaId));

            Assert.AreEqual(2, vm.Djeca.Count);
            Assert.AreEqual(GrupaId, vm.OdabranaGrupaId);
        }

        [TestMethod]
        public async Task OdgajateljPregled_TudaGrupa_VracaPrazneListe()
        {
            PripremiOdgajateljaIEvidencije();

            var vm = Model<OdgajateljEvidencijaPregledViewModel>(
                await kontroler.OdgajateljPregled(grupaId: TudaGrupaId));

            Assert.AreEqual(0, vm.Djeca.Count);
            Assert.AreEqual(0, vm.Evidencije.Count);
        }

        [TestMethod]
        public async Task OdgajateljPregled_DrugiDatum_NemaEvidencija()
        {
            PripremiOdgajateljaIEvidencije();

            var vm = Model<OdgajateljEvidencijaPregledViewModel>(
                await kontroler.OdgajateljPregled(datum: DateTime.Today.AddDays(-1)));

            Assert.AreEqual(0, vm.Evidencije.Count);
        }

        [TestMethod]
        [DataRow("ODLAZAK", "ODLAZAK")]
        [DataRow("DOLAZAK", "DOLAZAK")]
        [DataRow(null, "DOLAZAK")]
        [DataRow("nesto", "DOLAZAK")]
        public async Task OdgajateljPregled_RazlicitTip_OdabraniTipJeOčekivan(string? tip, string ocekivano)
        {
            PostaviKorisnika(userManager, NapraviOdgajatelja(OdgajateljId));

            var vm = Model<OdgajateljEvidencijaPregledViewModel>(await kontroler.OdgajateljPregled(tip: tip));

            Assert.AreEqual(ocekivano, vm.OdabraniTip);
        }

        // ---------- OdgajateljGenerisiQR ----------
        [TestMethod]
        public async Task OdgajateljGenerisiQR_NijePrijavljen_PreusmjeravaNaLogin()
        {
            PostaviKorisnika(userManager, null);

            var rezultat = await kontroler.OdgajateljGenerisiQR();

            Assert.IsInstanceOfType(rezultat, typeof(RedirectToPageResult));
        }

        [TestMethod]
        public async Task OdgajateljGenerisiQR_PostojiAktivanKodOdgajatelja_DeaktiviraStariIKreiraNovi()
        {
            PostaviKorisnika(userManager, NapraviOdgajatelja(OdgajateljId));

            await kontroler.OdgajateljGenerisiQR();

            Assert.IsFalse(kontekst.DnevniQRCodovi.Find(SeedKodId)!.Aktivan);
            var novi = kontekst.DnevniQRCodovi.Single(q => q.Aktivan);
            StringAssert.StartsWith(novi.VrijednostKoda, PrefiksOdgajatelja(OdgajateljId));
        }

        [TestMethod]
        public async Task OdgajateljGenerisiQR_KodDrugogOdgajatelja_OstajeAktivan()
        {
            PostaviKorisnika(userManager, NapraviOdgajatelja(OdgajateljId));
            var tudji = DodajKod(kontekst, PrefiksOdgajatelja(DrugiOdgajateljId) + "XYZ");

            await kontroler.OdgajateljGenerisiQR();

            Assert.IsTrue(kontekst.DnevniQRCodovi.Find(tudji.Id)!.Aktivan);
        }

        [TestMethod]
        public async Task OdgajateljGenerisiQR_Uspjeh_PostavljaPorukuIPreusmjeravaSaTipom()
        {
            PostaviKorisnika(userManager, NapraviOdgajatelja(OdgajateljId));

            var rezultat = (RedirectToActionResult)await kontroler.OdgajateljGenerisiQR("ODLAZAK");

            Assert.AreEqual(nameof(EvidencijaDolaskaOdlaskaController.OdgajateljPregled), rezultat.ActionName);
            Assert.AreEqual("ODLAZAK", rezultat.RouteValues!["tip"]);
            Assert.AreEqual("Novi dnevni QR kod je uspješno generisan.", TempPoruka(kontroler, "Poruka"));
        }
    }
}
