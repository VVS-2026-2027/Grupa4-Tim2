using EVrtic.Data;
using EVrtic.Models;
using EVrtic.Services;
using Microsoft.EntityFrameworkCore;

namespace EVrtic.Tests.Task3;

[TestClass]
public class NajavaOdsustvaServiceTests
{
    private const int RoditeljId = 1;
    private const int DrugiRoditeljId = 2;
    private const int DijeteId = 10;
    private const string Razlog = "Porodične obaveze";
    private static readonly DateTime Danas = new(2026, 11, 23);

    private ApplicationDbContext _baza = null!;
    private NajavaOdsustvaService _servis = null!;

    [TestInitialize]
    public async Task PripremiBazu()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _baza = new ApplicationDbContext(opcije);
        _servis = new NajavaOdsustvaService(_baza);

        _baza.Roditelji.AddRange(
            new Roditelj { Id = RoditeljId, ImePrezime = "Amra Kovač" },
            new Roditelj { Id = DrugiRoditeljId, ImePrezime = "Lejla Hasić" });

        _baza.Djeca.Add(new Dijete
        {
            Id = DijeteId,
            ImePrezime = "Ema Kovač",
            IdentifikacioniKod = "DIJETE01",
            DatumRodjenja = new DateTime(2021, 5, 10),
            RoditeljId = RoditeljId
        });

        await _baza.SaveChangesAsync();
    }

    [TestCleanup]
    public async Task OslobodiBazu()
    {
        await _baza.DisposeAsync();
    }

    [TestMethod]
    public async Task OdbijaPocetakPoslijeKraja()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            _servis.KreirajAsync(RoditeljId, DijeteId, Danas.AddDays(2), Danas.AddDays(1), Razlog, Danas));
        Assert.AreEqual(0, await _baza.NajaveOdsustva.CountAsync());
    }

    [TestMethod]
    public async Task OdbijaPocetakUProslosti()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            _servis.KreirajAsync(RoditeljId, DijeteId, Danas.AddDays(-1), Danas, Razlog, Danas));
        Assert.AreEqual(0, await _baza.NajaveOdsustva.CountAsync());
    }

    [TestMethod]
    public async Task RoditeljNeMozePrijavitiOdsustvoTudjegDjeteta()
    {
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() =>
            _servis.KreirajAsync(DrugiRoditeljId, DijeteId, Danas, Danas, Razlog, Danas));
        Assert.AreEqual(0, await _baza.NajaveOdsustva.CountAsync());
    }

    [TestMethod]
    public async Task OdbijaPreklapanjeSAktivnomNajavomIstogDjeteta()
    {
        await DodajPostojecuNajavuAsync(StatusNajaveOdsustva.AKTIVNA);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _servis.KreirajAsync(RoditeljId, DijeteId, Danas.AddDays(2), Danas.AddDays(3), Razlog, Danas));
        Assert.AreEqual(1, await _baza.NajaveOdsustva.CountAsync());
    }

    [TestMethod]
    public async Task CuvaJednodnevnoOdsustvoKaoAktivno()
    {
        var najava = await _servis.KreirajAsync(RoditeljId, DijeteId, Danas, Danas, Razlog, Danas);

        var sacuvana = await _baza.NajaveOdsustva.AsNoTracking().SingleAsync();
        Assert.AreEqual(najava.Id, sacuvana.Id);
        Assert.AreEqual(RoditeljId, sacuvana.RoditeljId);
        Assert.AreEqual(DijeteId, sacuvana.DijeteId);
        Assert.AreEqual(Danas, sacuvana.DatumOd);
        Assert.AreEqual(Danas, sacuvana.DatumDo);
        Assert.AreEqual(Razlog, sacuvana.Razlog);
        Assert.AreEqual(StatusNajaveOdsustva.AKTIVNA, sacuvana.Status);
    }

    [TestMethod]
    public async Task OtkazanaNajavaNeBlokiraNoviPeriod()
    {
        await DodajPostojecuNajavuAsync(StatusNajaveOdsustva.OTKAZANA);

        var najava = await _servis.KreirajAsync(
            RoditeljId, DijeteId, Danas.AddDays(1), Danas.AddDays(2), Razlog, Danas);

        Assert.AreEqual(StatusNajaveOdsustva.AKTIVNA, najava.Status);
        Assert.AreEqual(2, await _baza.NajaveOdsustva.CountAsync());
    }

    private async Task DodajPostojecuNajavuAsync(StatusNajaveOdsustva status)
    {
        _baza.NajaveOdsustva.Add(new NajavaOdsustva
        {
            RoditeljId = RoditeljId,
            DijeteId = DijeteId,
            DatumOd = Danas.AddDays(1),
            DatumDo = Danas.AddDays(2),
            DatumKreiranja = Danas,
            Razlog = Razlog,
            Status = status
        });
        await _baza.SaveChangesAsync();
    }
}
