using System;
using System.Linq;
using System.Threading.Tasks;
using EVrtic.Data;
using EVrtic.Models;
using EVrtic.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EVrtic.Tests.Task3;
/// <summary>
/// TDD – funkcionalnost 1, Belmin dio 2.1:
/// otkazivanje najava odsustva, pregled za grupu i kontrola pristupa.
/// Ovi testovi se pisu prije implementacije OtkaziAsync i PregledZaGrupuAsync.
/// </summary>
[TestClass]
public class NajavaOdsustvaBelmaTests
{
    private ApplicationDbContext _db = null!;
    private NajavaOdsustvaService _servis = null!;

    // Fiksan datum: testovi ne zavise od dana na koji se izvrsavaju.
    private static readonly DateTime Danas = new(2030, 4, 1);

    [TestInitialize]
    public void Pripremi()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new ApplicationDbContext(opcije);

        // Grupa 10 pripada odgajatelju 70, a grupa 20 odgajatelju 71.
        _db.Grupe.AddRange(
            new Grupa { Id = 10, ImeGrupe = "Zvjezdice", OdgajateljId = 70 },
            new Grupa { Id = 20, ImeGrupe = "Pcelice", OdgajateljId = 71 });

        // Prvo dijete pripada roditelju 50, a drugo roditelju 51.
        _db.Djeca.AddRange(
            new Dijete
            {
                Id = 100, ImePrezime = "Prvo dijete", IdentifikacioniKod = "12345678",
                DatumRodjenja = new DateTime(2025, 5, 1),
                RoditeljId = 50, GrupaId = 10
            },
            new Dijete
            {
                Id = 101, ImePrezime = "Drugo dijete", IdentifikacioniKod = "87654321",
                DatumRodjenja = new DateTime(2025, 6, 1),
                RoditeljId = 51, GrupaId = 20
            });

        _db.SaveChanges();
        _servis = new NajavaOdsustvaService(_db);
    }

    [TestCleanup]
    public void Ocisti() => _db?.Dispose();

    private void DodajNajavu(
        int id, int dijeteId, int roditeljId,
        DateTime od, DateTime doDatuma,
        StatusNajaveOdsustva status = StatusNajaveOdsustva.AKTIVNA)
    {
        _db.NajaveOdsustva.Add(new NajavaOdsustva
        {
            Id = id,
            DijeteId = dijeteId,
            RoditeljId = roditeljId,
            DatumOd = od,
            DatumDo = doDatuma,
            Razlog = "Porodicne obaveze",
            Status = status,
            DatumKreiranja = Danas
        });
        _db.SaveChanges();
    }

    [TestMethod]
    public async Task OtkaziAsync_VlastitaAktivnaNajavaPrijePocetka_PostavljaOtkazana()
    {
        DodajNajavu(1, 100, 50, new DateTime(2030, 4, 10), new DateTime(2030, 4, 12));

        bool rezultat = await _servis.OtkaziAsync(50, 1, Danas);

        Assert.IsTrue(rezultat);
        _db.ChangeTracker.Clear();
        var najava = await _db.NajaveOdsustva.SingleAsync(n => n.Id == 1);
        Assert.AreEqual(StatusNajaveOdsustva.OTKAZANA, najava.Status);
    }

    [TestMethod]
    public async Task OtkaziAsync_NajavaDrugogRoditelja_NeMijenjaZapis()
    {
        DodajNajavu(2, 100, 50, new DateTime(2030, 4, 10), new DateTime(2030, 4, 12));

        // Roditelj 51 ne smije otkazati najavu roditelja 50.
        bool rezultat = await _servis.OtkaziAsync(51, 2, Danas);

        Assert.IsFalse(rezultat);
        _db.ChangeTracker.Clear();
        var najava = await _db.NajaveOdsustva.SingleAsync(n => n.Id == 2);
        Assert.AreEqual(StatusNajaveOdsustva.AKTIVNA, najava.Status);
    }

    [TestMethod]
    public async Task OtkaziAsync_VecOtkazanaNajava_VracaFalse()
    {
        DodajNajavu(3, 100, 50, new DateTime(2030, 4, 10),
            new DateTime(2030, 4, 12), StatusNajaveOdsustva.OTKAZANA);

        bool rezultat = await _servis.OtkaziAsync(50, 3, Danas);

        Assert.IsFalse(rezultat);
        Assert.AreEqual(StatusNajaveOdsustva.OTKAZANA,
            (await _db.NajaveOdsustva.SingleAsync(n => n.Id == 3)).Status);
    }

    [TestMethod]
    public async Task OtkaziAsync_NakonPocetkaNajave_VracaFalse()
    {
        DodajNajavu(4, 100, 50, new DateTime(2030, 4, 10), new DateTime(2030, 4, 12));

        bool rezultat = await _servis.OtkaziAsync(50, 4, new DateTime(2030, 4, 11));

        Assert.IsFalse(rezultat);
        _db.ChangeTracker.Clear();
        Assert.AreEqual(StatusNajaveOdsustva.AKTIVNA,
            (await _db.NajaveOdsustva.SingleAsync(n => n.Id == 4)).Status);
    }

    [TestMethod]
    public async Task OtkaziAsync_NepostojecaNajava_VracaFalse()
    {
        bool rezultat = await _servis.OtkaziAsync(50, 999, Danas);

        Assert.IsFalse(rezultat);
        Assert.AreEqual(0, await _db.NajaveOdsustva.CountAsync());
    }

    [TestMethod]
    public async Task PregledZaGrupuAsync_VlastitaGrupa_VracaSamoAktivneNajaveUPeriodu()
    {
        // Aktivna najava iz trazene grupe – pocetak prije perioda, kraj na granici.
        DodajNajavu(10, 100, 50, new DateTime(2030, 4, 12), new DateTime(2030, 4, 15));
        // Otkazana najava iz trazene grupe: ne smije se prikazati.
        DodajNajavu(11, 100, 50, new DateTime(2030, 4, 17),
            new DateTime(2030, 4, 19), StatusNajaveOdsustva.OTKAZANA);
        // Aktivna najava iz druge grupe: ne smije se prikazati.
        DodajNajavu(12, 101, 51, new DateTime(2030, 4, 16), new DateTime(2030, 4, 18));
        // Aktivna najava iste grupe van trazenog perioda.
        DodajNajavu(13, 100, 50, new DateTime(2030, 5, 1), new DateTime(2030, 5, 3));
        // Aktivna najava iste grupe s pocetkom na zavrsnoj granici perioda.
        DodajNajavu(14, 100, 50, new DateTime(2030, 4, 20), new DateTime(2030, 4, 21));

        var rezultat = await _servis.PregledZaGrupuAsync(
            70, 10, new DateTime(2030, 4, 15), new DateTime(2030, 4, 20));

        Assert.AreEqual(2, rezultat.Count);
        CollectionAssert.AreEquivalent(new[] { 10, 14 }, rezultat.Select(n => n.Id).ToArray());
    }

    [TestMethod]
    public async Task PregledZaGrupuAsync_TudjaGrupa_OdbijaPristup()
    {
        DodajNajavu(20, 100, 50, new DateTime(2030, 4, 15), new DateTime(2030, 4, 16));

        // Odgajatelj 71 vodi grupu 20, ne smije pregledati grupu 10.
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(async () =>
        {
            await _servis.PregledZaGrupuAsync(
                71, 10, new DateTime(2030, 4, 15), new DateTime(2030, 4, 20));
        });
    }
}
