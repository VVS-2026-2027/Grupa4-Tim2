using System.Security.Claims;
using EVrtic.Controllers;
using EVrtic.Data;
using EVrtic.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace EVrtic.Tests;

public abstract class EvidencijaTestBase
{
    protected const int RoditeljId = 10;
    protected const int OdgajateljId = 5;

    protected ApplicationDbContext Context = null!;
    protected Mock<UserManager<Korisnik>> UserManagerMock = null!;
    protected EvidencijaDolaskaOdlaskaController Controller = null!;

    [TestInitialize]
    public void PripremiOkruzenje()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Context = new ApplicationDbContext(opcije);
        UserManagerMock = new Mock<UserManager<Korisnik>>(
            Mock.Of<IUserStore<Korisnik>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        Controller = new EvidencijaDolaskaOdlaskaController(Context, UserManagerMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };
    }

    [TestCleanup]
    public void Pocisti() => Context.Dispose();

    // ---------- stubovi korisnika ----------
    protected Korisnik PrijaviKorisnika(int id)
    {
        var korisnik = new Korisnik { Id = id };
        UserManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(korisnik);
        return korisnik;
    }

    protected void NikoNijePrijavljen() =>
        UserManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((Korisnik?)null);

    // ---------- graditelji podataka ----------
    protected static string KodOdgajatelja(int odgajateljId) =>
        $"EVRTIC-{DateTime.Today:yyyyMMdd}-O{odgajateljId}-ABC123DEF0";

    protected Grupa DodajGrupu(int id, int odgajateljId, string ime = "Leptirići")
    {
        var grupa = new Grupa { Id = id, OdgajateljId = odgajateljId, ImeGrupe = ime };
        Context.Grupe.Add(grupa);
        Context.SaveChanges();
        return grupa;
    }

    protected Dijete DodajDijete(int id, string imePrezime, int roditeljId, Grupa? grupa = null, bool aktivno = true)
    {
        var dijete = new Dijete
        {
            Id = id, ImePrezime = imePrezime, IdentifikacioniKod = $"KOD-{id}",
            RoditeljId = roditeljId, Grupa = grupa, GrupaId = grupa?.Id, Aktivno = aktivno
        };
        Context.Djeca.Add(dijete);
        Context.SaveChanges();
        return dijete;
    }

    protected DnevniQRCode DodajQr(string vrijednost, bool aktivan = true, int danaOdDanas = 0, int minutaDoIsteka = 60)
    {
        var qr = new DnevniQRCode
        {
            VrijednostKoda = vrijednost, Aktivan = aktivan,
            DatumVazenja = DateTime.Today.AddDays(danaOdDanas),
            VrijemeIsteka = DateTime.Now.AddMinutes(minutaDoIsteka)
        };
        Context.DnevniQRCodovi.Add(qr);
        Context.SaveChanges();
        return qr;
    }

    protected EvidencijaDolaskaOdlaska DodajEvidenciju(Dijete dijete, TipDogadjaja tip,
        StatusEvidencije status, DnevniQRCode qr, int danaOdDanas = 0)
    {
        var evidencija = new EvidencijaDolaskaOdlaska
        {
            DijeteId = dijete.Id, DnevniQRCodeId = qr.Id, TipDogadjaja = tip, StatusEvidencije = status,
            VrijemeDogadjaja = DateTime.Today.AddDays(danaOdDanas).AddHours(8),
            UneseniKodDjeteta = dijete.IdentifikacioniKod, ValidanQRKod = true, KodDjetetaIspravan = true
        };
        Context.EvidencijeDolaskaOdlaska.Add(evidencija);
        Context.SaveChanges();
        return evidencija;
    }

    // ---------- pomoćne provjere ----------
    protected static T Kao<T>(IActionResult rezultat) where T : class
    {
        Assert.IsInstanceOfType<T>(rezultat);
        return (T)rezultat;
    }

    protected static TModel ModelIz<TModel>(IActionResult rezultat) where TModel : class
    {
        var view = Kao<ViewResult>(rezultat);
        Assert.IsInstanceOfType<TModel>(view.Model);
        return (TModel)view.Model!;
    }

    protected void ProvjeriGresku(IActionResult rezultat, string dioPoruke, string kljuc = "")
    {
        Assert.IsInstanceOfType<ViewResult>(rezultat);
        var greska = Controller.ModelState[kljuc]!.Errors.Single().ErrorMessage;
        StringAssert.Contains(greska, dioPoruke);
    }
}