using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using EVrtic.Data;
using EVrtic.Models;
using EVrtic.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace eVrticSistem.Tests.Zuhra
{
    public class EvidencijaSlucaj
    {
        public string Tip { get; set; } = "";
        public List<string> PostojeciTipovi { get; set; } = new();
        public string OcekivanaPoruka { get; set; } = "";
    }

    public class EmailSlucaj
    {
        public string Prima { get; set; } = "";
        public bool PutemEmaila { get; set; }
        public bool EmailUspjeh { get; set; }
        public string OcekivanaPoruka { get; set; } = "";
    }

    // Pomoćne funkcije (da se kod u testovima ne ponavlja)
    public static class ZuhraTestHelper
    {
        public const int OdgajateljId = 10;
        public const int DrugiOdgajateljId = 20;
        public const int RoditeljId = 1;
        public const int DrugiRoditeljId = 2;
        public const int GrupaId = 1;
        public const int TudaGrupaId = 2;
        public const int DijeteId = 1;
        public const int DrugoDijeteId = 2;
        public const int TudeDijeteId = 3;
        public const int SeedKodId = 1;

        public static readonly DateTime FiksniDatum = new DateTime(2026, 11, 20, 10, 0, 0);

        public static string DanasnjiPrefiks() => $"EVRTIC-{DateTime.Today:yyyyMMdd}-";
        public static string PrefiksOdgajatelja(int id) => $"EVRTIC-{DateTime.Today:yyyyMMdd}-O{id}-";
        public static string ValidanKod() => PrefiksOdgajatelja(OdgajateljId) + "ABC123";

        // ---------- fake baza ----------
        public static InMemoryDatabaseRoot NapraviKorijen() => new InMemoryDatabaseRoot();

        public static ApplicationDbContext NapraviKontekst(InMemoryDatabaseRoot korijen)
        {
            var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString(), korijen)
                .Options;
            return new ApplicationDbContext(opcije);
        }

        public static void ObrisiKontekst(ApplicationDbContext kontekst)
        {
            kontekst.Database.EnsureDeleted();
            kontekst.Dispose();
        }

        // ---------- zamjenski objekti ----------
        public static Mock<UserManager<Korisnik>> NapraviUserManager(Korisnik? korisnik)
        {
            var store = new Mock<IUserStore<Korisnik>>();
            var mock = new Mock<UserManager<Korisnik>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
            PostaviKorisnika(mock, korisnik);
            return mock;
        }

        public static void PostaviKorisnika(Mock<UserManager<Korisnik>> mock, Korisnik? korisnik)
        {
            mock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(korisnik);
        }

        public static Mock<IEmailService> NapraviEmailServis(bool uspjeh)
        {
            var mock = new Mock<IEmailService>();
            mock.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(uspjeh);
            return mock;
        }

        public static void PripremiKontroler(Controller kontroler)
        {
            var http = new DefaultHttpContext();
            kontroler.ControllerContext = new ControllerContext { HttpContext = http };
            kontroler.TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>());
        }

        // ---------- čitanje rezultata ----------
        public static T Model<T>(IActionResult rezultat) => (T)((ViewResult)rezultat).Model!;

        public static string SvePoruke(ModelStateDictionary stanje) =>
            string.Join(" | ", stanje.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));

        public static string TempPoruka(Controller kontroler, string kljuc) =>
            kontroler.TempData[kljuc]?.ToString() ?? string.Empty;

        // ---------- pravljenje entiteta ----------
        public static Odgajatelj NapraviOdgajatelja(int id) =>
            new Odgajatelj { Id = id, ImePrezime = $"Odgajatelj {id}", Email = $"odgajatelj{id}@test.ba", UserName = $"odgajatelj{id}@test.ba" };

        public static Roditelj NapraviRoditelja(int id, string email) =>
            new Roditelj { Id = id, ImePrezime = $"Roditelj {id}", Email = email, UserName = $"roditelj{id}" };

        public static Dijete NapraviDijete(int id, string ime, string kod, int? grupaId, int roditeljId) =>
            new Dijete { Id = id, ImePrezime = ime, IdentifikacioniKod = kod, GrupaId = grupaId, RoditeljId = roditeljId, Aktivno = true };

        public static DnevniQRCode DodajKod(ApplicationDbContext k, string vrijednost, bool aktivan = true)
        {
            var kod = new DnevniQRCode
            {
                VrijednostKoda = vrijednost,
                DatumVazenja = DateTime.Today,
                VrijemeIsteka = DateTime.Today.AddDays(1).AddSeconds(-1),
                Aktivan = aktivan
            };
            k.DnevniQRCodovi.Add(kod);
            k.SaveChanges();
            return kod;
        }

        public static void DodajViseKodova(ApplicationDbContext k, int broj)
        {
            Enumerable.Range(0, broj).ToList().ForEach(i => DodajKod(k, $"EVRTIC-TEST-{i}"));
        }

        public static void DodajEvidenciju(ApplicationDbContext k, int dijeteId, TipDogadjaja tip, StatusEvidencije status)
        {
            k.EvidencijeDolaskaOdlaska.Add(new EvidencijaDolaskaOdlaska
            {
                VrijemeDogadjaja = DateTime.Now,
                TipDogadjaja = tip,
                UneseniKodDjeteta = "D001",
                ValidanQRKod = true,
                KodDjetetaIspravan = true,
                StatusEvidencije = status,
                DijeteId = dijeteId,
                DnevniQRCodeId = SeedKodId
            });
            k.SaveChanges();
        }

        public static void DodajEvidencijePoTipovima(ApplicationDbContext k, int dijeteId, IEnumerable<string> tipovi)
        {
            foreach (var tip in tipovi)
                DodajEvidenciju(k, dijeteId, Enum.Parse<TipDogadjaja>(tip), StatusEvidencije.EVIDENTIRANO);
        }

        public static void DodajObavijest(ApplicationDbContext k, int id, string naslov, int roditeljId,
            int odgajateljId, DateTime kreirana, StatusObavijesti status)
        {
            k.Obavijesti.Add(new Obavijest
            {
                Id = id,
                Naslov = naslov,
                Sadrzaj = "Sadržaj " + naslov,
                DatumKreiranja = kreirana,
                DatumSlanja = kreirana,
                KanalSlanja = KanalSlanja.APLIKACIJA,
                StatusObavijesti = status,
                RoditeljId = roditeljId,
                OdgajateljId = odgajateljId
            });
            k.SaveChanges();
        }

        public static void UkloniGrupuDjetetu(ApplicationDbContext k, int dijeteId)
        {
            k.Djeca.Find(dijeteId)!.GrupaId = null;
            k.SaveChanges();
        }

        public static void DeaktivirajSveKodove(ApplicationDbContext k)
        {
            k.DnevniQRCodovi.ToList().ForEach(q => q.Aktivan = false);
            k.SaveChanges();
        }

        // Osnovni podaci: 2 odgajatelja, 2 roditelja, 2 grupe, 3 djeteta, 1 aktivan QR kod odgajatelja 10
        public static void NapuniOsnovnePodatke(ApplicationDbContext k)
        {
            k.Odgajatelji.AddRange(NapraviOdgajatelja(OdgajateljId), NapraviOdgajatelja(DrugiOdgajateljId));
            k.Roditelji.AddRange(NapraviRoditelja(RoditeljId, "roditelj1@test.ba"), NapraviRoditelja(DrugiRoditeljId, ""));
            k.Grupe.AddRange(
                new Grupa { Id = GrupaId, ImeGrupe = "Leptirići", OdgajateljId = OdgajateljId },
                new Grupa { Id = TudaGrupaId, ImeGrupe = "Bubamare", OdgajateljId = DrugiOdgajateljId });
            k.Djeca.AddRange(
                NapraviDijete(DijeteId, "Amar Test", "D001", GrupaId, RoditeljId),
                NapraviDijete(DrugoDijeteId, "Lejla Test", "D002", GrupaId, RoditeljId),
                NapraviDijete(TudeDijeteId, "Tuđe Dijete", "D003", TudaGrupaId, DrugiRoditeljId));
            k.DnevniQRCodovi.Add(new DnevniQRCode
            {
                Id = SeedKodId,
                VrijednostKoda = ValidanKod(),
                DatumVazenja = DateTime.Today,
                VrijemeIsteka = DateTime.Today.AddDays(1).AddSeconds(-1),
                Aktivan = true
            });
            k.SaveChanges();
        }

        // ---------- data driven učitavanje ----------
        private static string Putanja(string datoteka) =>
            Path.Combine(AppContext.BaseDirectory, "Zuhra", "PodaciZuhra", datoteka);

        // CSV (separator ';', prvi red je zaglavlje); tipovi kolona se zadaju eksplicitno
        public static IEnumerable<object[]> UcitajCsv(string datoteka, params Type[] tipovi)
        {
            return File.ReadAllLines(Putanja(datoteka))
                .Skip(1)
                .Where(red => !string.IsNullOrWhiteSpace(red))
                .Select(red => red.Split(';')
                    .Select((v, i) => Convert.ChangeType(v, tipovi[i], CultureInfo.InvariantCulture))
                    .ToArray());
        }

        // JSON (lista objekata)
        public static IEnumerable<object[]> UcitajJson<T>(string datoteka, Func<T, object[]> pretvori)
        {
            var opcije = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var lista = JsonSerializer.Deserialize<List<T>>(File.ReadAllText(Putanja(datoteka)), opcije)!;
            return lista.Select(pretvori);
        }
    }
}
