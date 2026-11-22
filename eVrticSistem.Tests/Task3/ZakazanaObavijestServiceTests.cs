using EVrtic.Data;
using EVrtic.Models;
using EVrtic.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace EVrtic.Tests.Task3.Zakazana;

[TestClass]
public sealed class ZakazanaObavijestServiceTests
{
    private const int OdgajateljId = 70;
    private const int DrugiOdgajateljId = 71;
    private const int RoditeljId = 50;
    private static readonly DateTime SadaUtc = new(2030, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    private ApplicationDbContext _baza = null!;
    private ZakazanaObavijestService _servis = null!;
    private Mock<IEmailService> _emailService = null!;

    [TestInitialize]
    public async Task PripremiBazu()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _baza = new ApplicationDbContext(opcije);
        _servis = new ZakazanaObavijestService(_baza);
        _emailService = new Mock<IEmailService>();

        _baza.Roditelji.Add(new Roditelj
        {
            Id = RoditeljId,
            ImePrezime = "Roditelj Test",
            Email = "roditelj@example.com",
            UserName = "roditelj@example.com"
        });
        _baza.Odgajatelji.AddRange(
            new Odgajatelj
            {
                Id = OdgajateljId,
                ImePrezime = "Odgajatelj Test",
                Email = "odgajatelj@example.com",
                UserName = "odgajatelj@example.com"
            },
            new Odgajatelj
            {
                Id = DrugiOdgajateljId,
                ImePrezime = "Drugi Odgajatelj",
                Email = "drugi.odgajatelj@example.com",
                UserName = "drugi.odgajatelj@example.com"
            });
        await _baza.SaveChangesAsync();
    }

    [TestCleanup]
    public async Task OslobodiBazu()
    {
        await _baza.DisposeAsync();
    }

    [TestMethod]
    public async Task ZakaziAsync_BuducaPoruka_SpremaStatusCeka()
    {
        var obavijest = await _servis.ZakaziAsync(
            OdgajateljId,
            RoditeljId,
            "Sastanak",
            "Danas imamo roditeljski sastanak.",
            SadaUtc.AddHours(2),
            SadaUtc);

        Assert.AreEqual(StatusZakazaneObavijesti.CEKA, obavijest.Status);
        Assert.AreEqual(SadaUtc.AddHours(2), obavijest.VrijemeSlanjaUtc);
        Assert.AreEqual(1, await _baza.ZakazaneObavijesti.CountAsync());
        _emailService.Verify(
            email => email.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ZakaziAsync_VrijemeJednakoSadasnjemVremenu_Odbija()
    {
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
            _servis.ZakaziAsync(OdgajateljId, RoditeljId, "Naslov", "Tekst", SadaUtc, SadaUtc));

        Assert.AreEqual(0, await _baza.ZakazaneObavijesti.CountAsync());
    }

    [TestMethod]
    public async Task ZakaziAsync_VrijemeUProslosti_Odbija()
    {
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
            _servis.ZakaziAsync(OdgajateljId, RoditeljId, "Naslov", "Tekst", SadaUtc.AddMinutes(-1), SadaUtc));

        Assert.AreEqual(0, await _baza.ZakazaneObavijesti.CountAsync());
    }

    [TestMethod]
    [DataRow("", "Tekst")]
    [DataRow("   ", "Tekst")]
    [DataRow("Naslov", "")]
    [DataRow("Naslov", "   ")]
    public async Task ZakaziAsync_PrazanNaslovIliSadrzaj_Odbija(string naslov, string sadrzaj)
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
            _servis.ZakaziAsync(OdgajateljId, RoditeljId, naslov, sadrzaj, SadaUtc.AddHours(1), SadaUtc));

        Assert.AreEqual(0, await _baza.ZakazaneObavijesti.CountAsync());
    }

    [TestMethod]
    public async Task ZakaziAsync_NepostojeciPrimalac_Odbija()
    {
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            _servis.ZakaziAsync(OdgajateljId, 999, "Naslov", "Tekst", SadaUtc.AddHours(1), SadaUtc));

        Assert.AreEqual(0, await _baza.ZakazaneObavijesti.CountAsync());
    }

    [TestMethod]
    public async Task OtkaziAsync_VlastitaCekaPoruka_PostavljaOtkazana()
    {
        var obavijest = await DodajObavijestAsync(StatusZakazaneObavijesti.CEKA);

        var rezultat = await _servis.OtkaziAsync(OdgajateljId, obavijest.Id, SadaUtc);

        Assert.IsTrue(rezultat);
        _baza.ChangeTracker.Clear();
        var sacuvana = await _baza.ZakazaneObavijesti.SingleAsync(o => o.Id == obavijest.Id);
        Assert.AreEqual(StatusZakazaneObavijesti.OTKAZANA, sacuvana.Status);
    }

    [TestMethod]
    public async Task OtkaziAsync_PorukaJeVecDospjela_NeOtkazuje()
    {
        var obavijest = await DodajObavijestAsync(
            StatusZakazaneObavijesti.CEKA,
            vrijemeUtc: SadaUtc);

        var rezultat = await _servis.OtkaziAsync(OdgajateljId, obavijest.Id, SadaUtc);

        Assert.IsFalse(rezultat);
        _baza.ChangeTracker.Clear();
        var sacuvana = await _baza.ZakazaneObavijesti.SingleAsync(o => o.Id == obavijest.Id);
        Assert.AreEqual(StatusZakazaneObavijesti.CEKA, sacuvana.Status);
    }

//amin
    [TestMethod]
    [DataRow(StatusZakazaneObavijesti.POSLANA, OdgajateljId)]
    [DataRow(StatusZakazaneObavijesti.OTKAZANA, OdgajateljId)]
    [DataRow(StatusZakazaneObavijesti.NEUSPJELA, OdgajateljId)]
    [DataRow(StatusZakazaneObavijesti.CEKA, DrugiOdgajateljId)]
    public async Task OtkaziAsync_TudjaPoslanaOtkazanaIliNeaktivnaPoruka_BezPromjene(
        StatusZakazaneObavijesti status,
        int autorId)
    {
        var obavijest = await DodajObavijestAsync(status, autorId);

        var rezultat = await _servis.OtkaziAsync(OdgajateljId, obavijest.Id, SadaUtc);

        Assert.IsFalse(rezultat);
        _baza.ChangeTracker.Clear();
        var sacuvana = await _baza.ZakazaneObavijesti.SingleAsync(o => o.Id == obavijest.Id);
        Assert.AreEqual(status, sacuvana.Status);
    }

    [TestMethod]
    public async Task ObradiDospjeleAsync_UspjesnoSaljeJednuPoruku()
    {
        var processor = new ObradaZakazanihObavijestiService(_baza, _emailService.Object);
        var obavijest = await DodajObavijestAsync(StatusZakazaneObavijesti.CEKA, vrijemeUtc: SadaUtc);
        _emailService.Setup(e => e.SendAsync("roditelj@example.com", obavijest.Naslov, obavijest.Sadrzaj))
            .ReturnsAsync(true);

        var brojUspjesnih = await processor.ObradiDospjeleAsync(SadaUtc);

        Assert.AreEqual(1, brojUspjesnih);
        _emailService.Verify(e => e.SendAsync("roditelj@example.com", obavijest.Naslov, obavijest.Sadrzaj), Times.Once);
        _baza.ChangeTracker.Clear();
        var sacuvana = await _baza.ZakazaneObavijesti.SingleAsync(o => o.Id == obavijest.Id);
        Assert.AreEqual(StatusZakazaneObavijesti.POSLANA, sacuvana.Status);
        Assert.AreEqual(1, sacuvana.BrojPokusaja);
    }

    [TestMethod]
    public async Task ObradiDospjeleAsync_NedospjelaPoruka_NeSalje()
    {
        var processor = new ObradaZakazanihObavijestiService(_baza, _emailService.Object);
        var obavijest = await DodajObavijestAsync(
            StatusZakazaneObavijesti.CEKA,
            vrijemeUtc: SadaUtc.AddMinutes(1));

        var brojUspjesnih = await processor.ObradiDospjeleAsync(SadaUtc);

        Assert.AreEqual(0, brojUspjesnih);
        _emailService.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.AreEqual(StatusZakazaneObavijesti.CEKA,
            (await _baza.ZakazaneObavijesti.SingleAsync(o => o.Id == obavijest.Id)).Status);
    }

    [TestMethod]
    public async Task ObradiDospjeleAsync_OtkazanaPoruka_NeSalje()
    {
        var processor = new ObradaZakazanihObavijestiService(_baza, _emailService.Object);
        var obavijest = await DodajObavijestAsync(StatusZakazaneObavijesti.OTKAZANA, vrijemeUtc: SadaUtc);

        var brojUspjesnih = await processor.ObradiDospjeleAsync(SadaUtc);

        Assert.AreEqual(0, brojUspjesnih);
        _emailService.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.AreEqual(StatusZakazaneObavijesti.OTKAZANA,
            (await _baza.ZakazaneObavijesti.SingleAsync(o => o.Id == obavijest.Id)).Status);
    }

    [TestMethod]
    public async Task ObradiDospjeleAsync_PozivaSePonovo_NeSaljePoslanuPorukuOpet()
    {
        var processor = new ObradaZakazanihObavijestiService(_baza, _emailService.Object);
        var obavijest = await DodajObavijestAsync(StatusZakazaneObavijesti.CEKA, vrijemeUtc: SadaUtc);
        _emailService.Setup(e => e.SendAsync("roditelj@example.com", obavijest.Naslov, obavijest.Sadrzaj))
            .ReturnsAsync(true);

        var prviRezultat = await processor.ObradiDospjeleAsync(SadaUtc);
        var drugiRezultat = await processor.ObradiDospjeleAsync(SadaUtc);

        Assert.AreEqual(1, prviRezultat);
        Assert.AreEqual(0, drugiRezultat);
        _emailService.Verify(e => e.SendAsync("roditelj@example.com", obavijest.Naslov, obavijest.Sadrzaj), Times.Once);
    }

    [TestMethod]
    public async Task ObradiDospjeleAsync_NeuspjeloSlanje_BiljeziGreskuIPokusaj()
    {
        var processor = new ObradaZakazanihObavijestiService(_baza, _emailService.Object);
        var obavijest = await DodajObavijestAsync(StatusZakazaneObavijesti.CEKA, vrijemeUtc: SadaUtc);
        _emailService.Setup(e => e.SendAsync("roditelj@example.com", obavijest.Naslov, obavijest.Sadrzaj))
            .ReturnsAsync(false);

        var brojUspjesnih = await processor.ObradiDospjeleAsync(SadaUtc);

        Assert.AreEqual(0, brojUspjesnih);
        _emailService.Verify(e => e.SendAsync("roditelj@example.com", obavijest.Naslov, obavijest.Sadrzaj), Times.Once);
        _baza.ChangeTracker.Clear();
        var sacuvana = await _baza.ZakazaneObavijesti.SingleAsync(o => o.Id == obavijest.Id);
        Assert.AreEqual(StatusZakazaneObavijesti.NEUSPJELA, sacuvana.Status);
        Assert.AreEqual(1, sacuvana.BrojPokusaja);
    }

    [TestMethod]
    public async Task ObradiDospjeleAsync_MijesaniTermini_ObradjujeSamoDospjele()
    {
        var processor = new ObradaZakazanihObavijestiService(_baza, _emailService.Object);
        var dospjela = await DodajObavijestAsync(StatusZakazaneObavijesti.CEKA, vrijemeUtc: SadaUtc);
        var buduca = await DodajObavijestAsync(
            StatusZakazaneObavijesti.CEKA,
            vrijemeUtc: SadaUtc.AddHours(1),
            naslov: "Buduca poruka");
        _emailService.Setup(e => e.SendAsync("roditelj@example.com", dospjela.Naslov, dospjela.Sadrzaj))
            .ReturnsAsync(true);

        var brojUspjesnih = await processor.ObradiDospjeleAsync(SadaUtc);

        Assert.AreEqual(1, brojUspjesnih);
        _emailService.Verify(e => e.SendAsync("roditelj@example.com", dospjela.Naslov, dospjela.Sadrzaj), Times.Once);
        _emailService.Verify(e => e.SendAsync("roditelj@example.com", buduca.Naslov, buduca.Sadrzaj), Times.Never);
    }

    private async Task<ZakazanaObavijest> DodajObavijestAsync(
        StatusZakazaneObavijesti status,
        int odgajateljId = OdgajateljId,
        DateTime? vrijemeUtc = null,
        string naslov = "Naslov")
    {
        var obavijest = new ZakazanaObavijest
        {
            OdgajateljId = odgajateljId,
            RoditeljId = RoditeljId,
            Naslov = naslov,
            Sadrzaj = "Tekst",
            VrijemeSlanjaUtc = vrijemeUtc ?? SadaUtc.AddHours(1),
            Status = status
        };

        _baza.ZakazaneObavijesti.Add(obavijest);
        await _baza.SaveChangesAsync();
        return obavijest;
    }
}
