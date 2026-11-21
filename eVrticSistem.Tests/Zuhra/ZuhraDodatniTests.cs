using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EVrtic.Controllers;
using EVrtic.Data;
using EVrtic.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static eVrticSistem.Tests.Zuhra.ZuhraTestHelper;

namespace eVrticSistem.Tests.Zuhra
{
    // Dodatni testovi za povećanje pokrivenosti: privatne pomoćne metode i ViewModel klase
    [TestClass]
    public class ZuhraDodatniTests
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
            userManager = NapraviUserManager(null);
            kontroler = new EvidencijaDolaskaOdlaskaController(kontekst, userManager.Object);
            PripremiKontroler(kontroler);
        }

        [TestCleanup]
        public void TestCleanup() => ObrisiKontekst(kontekst);

        private static MethodInfo Privatna(Type tip, string ime, bool statika) =>
            tip.GetMethod(ime, BindingFlags.NonPublic | (statika ? BindingFlags.Static : BindingFlags.Instance))!;

        // ---------- EvidencijaDolaskaOdlaskaExists ----------
        [TestMethod]
        public void EvidencijaDolaskaOdlaskaExists_PostojecaEvidencija_VracaTrue()
        {
            DodajEvidenciju(kontekst, DijeteId, TipDogadjaja.DOLAZAK, StatusEvidencije.EVIDENTIRANO);
            var id = kontekst.EvidencijeDolaskaOdlaska.Single().Id;

            var rezultat = (bool)Privatna(typeof(EvidencijaDolaskaOdlaskaController),
                "EvidencijaDolaskaOdlaskaExists", false).Invoke(kontroler, new object[] { id })!;

            Assert.IsTrue(rezultat);
        }

        [TestMethod]
        public void EvidencijaDolaskaOdlaskaExists_NepostojecaEvidencija_VracaFalse()
        {
            var rezultat = (bool)Privatna(typeof(EvidencijaDolaskaOdlaskaController),
                "EvidencijaDolaskaOdlaskaExists", false).Invoke(kontroler, new object[] { 999 })!;

            Assert.IsFalse(rezultat);
        }

        // ---------- ParsirajOdgajateljaIzKoda ----------
        [TestMethod]
        [DataRow("EVRTIC-20260101-O10-ABC", 10)]
        [DataRow("EVRTIC-20260101-O7-ABC", 7)]
        [DataRow(null, null)]
        [DataRow("", null)]
        [DataRow("   ", null)]
        [DataRow("A-B-C", null)]
        [DataRow("EVRTIC-20260101-X10-ABC", null)]
        [DataRow("EVRTIC-20260101-O-ABC", null)]
        [DataRow("EVRTIC-20260101-Oxx-ABC", null)]
        public void ParsirajOdgajateljaIzKoda_RazlicitiKodovi_VracaOcekivaniId(string? kod, int? ocekivano)
        {
            var rezultat = (int?)Privatna(typeof(EvidencijaDolaskaOdlaskaController),
                "ParsirajOdgajateljaIzKoda", true).Invoke(null, new object?[] { kod });

            Assert.AreEqual(ocekivano, rezultat);
        }

        // ---------- ParsirajTipDogadjaja ----------
        [TestMethod]
        [DataRow("DOLAZAK", TipDogadjaja.DOLAZAK)]
        [DataRow("ODLAZAK", TipDogadjaja.ODLAZAK)]
        [DataRow("odlazak", TipDogadjaja.ODLAZAK)]
        [DataRow(null, TipDogadjaja.DOLAZAK)]
        [DataRow("nepoznato", TipDogadjaja.DOLAZAK)]
        public void ParsirajTipDogadjaja_RazlicitiUlazi_VracaOcekivaniTip(string? vrijednost, TipDogadjaja ocekivano)
        {
            var rezultat = (TipDogadjaja)Privatna(typeof(EvidencijaDolaskaOdlaskaController),
                "ParsirajTipDogadjaja", true).Invoke(null, new object?[] { vrijednost })!;

            Assert.AreEqual(ocekivano, rezultat);
        }

        // ---------- GenerisiVrijednostKoda ----------
        [TestMethod]
        public void GenerisiVrijednostKoda_Odgajatelj_KodImaPrefiksOdgajateljaIToken()
        {
            var kod = (string)Privatna(typeof(EvidencijaDolaskaOdlaskaController),
                "GenerisiVrijednostKoda", true).Invoke(null, new object[] { 7 })!;

            StringAssert.StartsWith(kod, PrefiksOdgajatelja(7));
            Assert.AreEqual(PrefiksOdgajatelja(7).Length + 10, kod.Length);
        }

        [TestMethod]
        public void GenerisiVrijednostKoda_DnevniKod_KodImaDanasnjiPrefiksIToken()
        {
            var kod = (string)Privatna(typeof(DnevniQRCodeController),
                "GenerisiVrijednostKoda", true).Invoke(null, null)!;

            StringAssert.StartsWith(kod, DanasnjiPrefiks());
            Assert.AreEqual(DanasnjiPrefiks().Length + 10, kod.Length);
        }

        [TestMethod]
        public void GenerisiVrijednostKoda_DvaPoziva_VracajuRazlicitKod()
        {
            var metoda = Privatna(typeof(DnevniQRCodeController), "GenerisiVrijednostKoda", true);

            var prvi = (string)metoda.Invoke(null, null)!;
            var drugi = (string)metoda.Invoke(null, null)!;

            Assert.AreNotEqual(prvi, drugi);
        }

        // ---------- ViewModeli ----------
        [TestMethod]
        public void RoditeljEvidencijaViewModel_NoviObjekat_ImaPodrazumijevaneVrijednosti()
        {
            var vm = new RoditeljEvidencijaDolaskaOdlaskaViewModel();

            Assert.AreEqual(string.Empty, vm.UneseniKodDjeteta);
            Assert.AreEqual("DOLAZAK", vm.TipDogadjaja);
            Assert.IsNull(vm.DnevniQRCode);
            Assert.IsNull(vm.UspjesnaPoruka);
            Assert.IsNull(vm.ScaniraniKod);
            Assert.IsNull(vm.OdabranoDijeteId);
            Assert.AreEqual(0, vm.Djeca.Count);
        }

        [TestMethod]
        public void RoditeljEvidencijaViewModel_PostavljenaSvojstva_VracajuPostavljeneVrijednosti()
        {
            var kod = new DnevniQRCode { VrijednostKoda = "K" };
            var djeca = new List<Dijete> { new Dijete { Id = 1 } };

            var vm = new RoditeljEvidencijaDolaskaOdlaskaViewModel
            {
                UneseniKodDjeteta = "D001",
                TipDogadjaja = "ODLAZAK",
                DnevniQRCode = kod,
                UspjesnaPoruka = "Poruka",
                ScaniraniKod = "SKEN",
                OdabranoDijeteId = 1,
                Djeca = djeca
            };

            Assert.AreEqual("D001", vm.UneseniKodDjeteta);
            Assert.AreEqual("ODLAZAK", vm.TipDogadjaja);
            Assert.AreSame(kod, vm.DnevniQRCode);
            Assert.AreEqual("Poruka", vm.UspjesnaPoruka);
            Assert.AreEqual("SKEN", vm.ScaniraniKod);
            Assert.AreEqual(1, vm.OdabranoDijeteId);
            Assert.AreSame(djeca, vm.Djeca);
        }

        [TestMethod]
        public void OdgajateljEvidencijaPregledViewModel_NoviObjekat_ImaPodrazumijevaneVrijednosti()
        {
            var vm = new OdgajateljEvidencijaPregledViewModel();

            Assert.AreEqual(DateTime.Today, vm.Datum);
            Assert.AreEqual("DOLAZAK", vm.OdabraniTip);
            Assert.AreEqual(0, vm.Grupe.Count);
            Assert.AreEqual(0, vm.Djeca.Count);
            Assert.AreEqual(0, vm.Evidencije.Count);
            Assert.IsNull(vm.AktivniQRKod);
        }

        [TestMethod]
        public void OdgajateljEvidencijaPregledViewModel_PostavljenaSvojstva_VracajuPostavljeneVrijednosti()
        {
            var datum = new DateTime(2026, 1, 15);

            var vm = new OdgajateljEvidencijaPregledViewModel
            {
                Datum = datum,
                Grupe = new List<Grupa> { new Grupa() },
                Djeca = new List<Dijete> { new Dijete() },
                Evidencije = new List<EvidencijaDolaskaOdlaska> { new EvidencijaDolaskaOdlaska() },
                OdabranaGrupaId = 2,
                OdabranoDijeteId = 3,
                BrojDolazaka = 4,
                BrojOdlazaka = 5,
                BrojOdbijenih = 6,
                AktivniQRKod = new DnevniQRCode(),
                OdabraniTip = "ODLAZAK"
            };

            Assert.AreEqual(datum, vm.Datum);
            Assert.AreEqual(1, vm.Grupe.Count);
            Assert.AreEqual(1, vm.Djeca.Count);
            Assert.AreEqual(1, vm.Evidencije.Count);
            Assert.AreEqual(2, vm.OdabranaGrupaId);
            Assert.AreEqual(3, vm.OdabranoDijeteId);
            Assert.AreEqual(4, vm.BrojDolazaka);
            Assert.AreEqual(5, vm.BrojOdlazaka);
            Assert.AreEqual(6, vm.BrojOdbijenih);
            Assert.IsNotNull(vm.AktivniQRKod);
            Assert.AreEqual("ODLAZAK", vm.OdabraniTip);
        }

        [TestMethod]
        public void DnevniQRCodePregledViewModel_NoviObjekat_ImaPodrazumijevaneVrijednosti()
        {
            var vm = new DnevniQRCodePregledViewModel();

            Assert.IsNull(vm.AktivniKod);
            Assert.AreEqual(0, vm.HistorijaKodova.Count);
        }

        [TestMethod]
        public void DnevniQRCodePregledViewModel_PostavljenaSvojstva_VracajuPostavljeneVrijednosti()
        {
            var kod = new DnevniQRCode { VrijednostKoda = "K" };

            var vm = new DnevniQRCodePregledViewModel
            {
                AktivniKod = kod,
                HistorijaKodova = new List<DnevniQRCode> { kod }
            };

            Assert.AreSame(kod, vm.AktivniKod);
            Assert.AreEqual(1, vm.HistorijaKodova.Count);
        }

        [TestMethod]
        public void ObavijestViewModeli_PostavljenaSvojstva_VracajuPostavljeneVrijednosti()
        {
            var odgajatelj = NapraviOdgajatelja(OdgajateljId);
            var lista = new List<Obavijest> { new Obavijest() };

            var pregled = new OdgajateljObavijestViewModel { Obavijesti = lista, TrenutniKorisnikId = 5 };
            var slanje = new PosaljiObavijestViewModel { Odgajatelj = odgajatelj };
            var unos = new PosaljiObavijestInputModel
            {
                Naslov = "N", Poruka = "P", Prima = "svi", PutemAplikacije = false, PutemEmaila = true
            };
            var roditelj = new RoditeljObavijestiViewModel { Obavijesti = lista, BrojNovih = 3 };

            Assert.AreSame(lista, pregled.Obavijesti);
            Assert.AreEqual(5, pregled.TrenutniKorisnikId);
            Assert.AreSame(odgajatelj, slanje.Odgajatelj);
            Assert.AreEqual("N", unos.Naslov);
            Assert.AreEqual("P", unos.Poruka);
            Assert.AreEqual("svi", unos.Prima);
            Assert.IsFalse(unos.PutemAplikacije);
            Assert.IsTrue(unos.PutemEmaila);
            Assert.AreSame(lista, roditelj.Obavijesti);
            Assert.AreEqual(3, roditelj.BrojNovih);
        }

        // ---------- dodatne grane RoditeljEvidencija ----------
        [TestMethod]
        public async Task RoditeljEvidencija_Get_RoditeljBezDjece_OdabranoDijeteJeNull()
        {
            PostaviKorisnika(userManager, NapraviRoditelja(99, "bezdjece@test.ba"));

            var vm = Model<RoditeljEvidencijaDolaskaOdlaskaViewModel>(await kontroler.RoditeljEvidencija());

            Assert.IsNull(vm.OdabranoDijeteId);
            Assert.AreEqual(0, vm.Djeca.Count);
        }

        [TestMethod]
        public async Task RoditeljEvidencija_Post_DijeteSaNepostojecomGrupom_DodajeGreskuOPripadnostiKoda()
        {
            PostaviKorisnika(userManager, NapraviRoditelja(RoditeljId, "roditelj1@test.ba"));
            kontekst.Djeca.Find(DijeteId)!.GrupaId = 99;
            kontekst.SaveChanges();
            var model = new RoditeljEvidencijaDolaskaOdlaskaViewModel
            {
                ScaniraniKod = ValidanKod(),
                OdabranoDijeteId = DijeteId,
                TipDogadjaja = "DOLAZAK"
            };

            await kontroler.RoditeljEvidencija(model);

            StringAssert.Contains(SvePoruke(kontroler.ModelState), "ne pripada odgajatelju");
        }
    }
}
