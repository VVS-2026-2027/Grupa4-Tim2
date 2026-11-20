using System.Diagnostics;
using System.Security.Claims;
using EVrtic.Controllers;
using EVrtic.Data;
using EVrtic.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace eVrticSistem.Tests.Sureja;

[TestClass]
public class HomeControllerTests
{
    private HomeController _kontroler = null!;

    [TestInitialize]
    public void PripremiKontroler()
    {
        _kontroler = NapraviKontroler();
    }

    [TestMethod]
    public void IndexNeprijavljenIdeNaPrijavu()
    {
        var rezultat = _kontroler.Index();

        var preusmjerenje = Assert.IsInstanceOfType<RedirectResult>(rezultat);
        Assert.AreEqual("/Identity/Account/Login", preusmjerenje.Url);
    }

    [TestMethod]
    public void IndexPrijavljenIdeNaRedirectByRole()
    {
        _kontroler.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity("Test"));

        var rezultat = _kontroler.Index();

        var preusmjerenje = Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual(nameof(HomeController.RedirectByRole), preusmjerenje.ActionName);
    }

    [TestMethod]
    [DataRow("ADMINISTRATOR", nameof(HomeController.AdministratorHome))]
    [DataRow("ODGAJATELJ", nameof(HomeController.OdgajateljHome))]
    [DataRow("RODITELJ", nameof(HomeController.RoditeljHome))]
    public void RedirectByRolePoznataUlogaIdeNaPocetnu(string uloga, string ocekivanaAkcija)
    {
        var identitet = new ClaimsIdentity("Test");
        identitet.AddClaim(new Claim(ClaimTypes.Role, uloga));
        _kontroler.HttpContext.User = new ClaimsPrincipal(identitet);

        var rezultat = _kontroler.RedirectByRole();

        var preusmjerenje = Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual(ocekivanaAkcija, preusmjerenje.ActionName);
    }

    [TestMethod]
    public void RedirectByRoleBezUlogeIdeNaPrijavu()
    {
        _kontroler.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity("Test"));

        var rezultat = _kontroler.RedirectByRole();

        var preusmjerenje = Assert.IsInstanceOfType<RedirectResult>(rezultat);
        Assert.AreEqual("/Identity/Account/Login", preusmjerenje.Url);
    }

    [TestMethod]
    public void RoditeljHomeVracaPogled()
    {
        var rezultat = _kontroler.RoditeljHome();

        Assert.IsInstanceOfType<ViewResult>(rezultat);
    }

    [TestMethod]
    public void PrivacyVracaPogled()
    {
        var rezultat = _kontroler.Privacy();

        Assert.IsInstanceOfType<ViewResult>(rezultat);
    }

    [TestMethod]
    public void ErrorKoristiIdAktivnosti()
    {
        _kontroler.HttpContext.TraceIdentifier = "http-test-123";
        using var aktivnost = new Activity("Test greske").Start();

        var rezultat = _kontroler.Error();

        var pogled = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var model = Assert.IsInstanceOfType<ErrorViewModel>(pogled.Model);
        Assert.AreEqual(aktivnost.Id, model.RequestId);
    }

    [TestMethod]
    public void ErrorBezAktivnostiKoristiTraceId()
    {
        var prethodnaAktivnost = Activity.Current;
        _kontroler.HttpContext.TraceIdentifier = "http-test-123";

        try
        {
            Activity.Current = null;

            var rezultat = _kontroler.Error();

            var pogled = Assert.IsInstanceOfType<ViewResult>(rezultat);
            var model = Assert.IsInstanceOfType<ErrorViewModel>(pogled.Model);
            Assert.AreEqual("http-test-123", model.RequestId);
        }
        finally
        {
            Activity.Current = prethodnaAktivnost;
        }
    }

    [TestMethod]
    public async Task AdministratorHomePrikazujeTacneBrojeve()
    {
        using var baza = NapraviBazu();

        baza.Odgajatelji.Add(new Odgajatelj { ImePrezime = "Odgajatelj 1" });

        baza.Roditelji.AddRange(
            new Roditelj { ImePrezime = "Roditelj 1" },
            new Roditelj { ImePrezime = "Roditelj 2" });

        baza.Djeca.AddRange(
            new Dijete { ImePrezime = "Dijete 1", IdentifikacioniKod = "D0000001" },
            new Dijete { ImePrezime = "Dijete 2", IdentifikacioniKod = "D0000002" },
            new Dijete { ImePrezime = "Dijete 3", IdentifikacioniKod = "D0000003" });

        baza.Grupe.AddRange(
            new Grupa { ImeGrupe = "Pčelice" },
            new Grupa { ImeGrupe = "Bubamare" },
            new Grupa { ImeGrupe = "Zeke" },
            new Grupa { ImeGrupe = "Leptiri" });

        await baza.SaveChangesAsync();
        var kontroler = NapraviKontroler(baza);

        var rezultat = await kontroler.AdministratorHome();

        var pogled = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var model = Assert.IsInstanceOfType<AdministratorHomeViewModel>(pogled.Model);
        Assert.AreEqual(1, model.BrojOdgajatelja);
        Assert.AreEqual(2, model.BrojRoditelja);
        Assert.AreEqual(3, model.BrojDjece);
        Assert.AreEqual(4, model.BrojGrupa);
    }

    [TestMethod]
    public async Task OdgajateljHomeBezKorisnikaTraziPrijavu()
    {
        using var userManager = NapraviUserManager(null);
        var kontroler = NapraviKontroler(userManager: userManager);

        var rezultat = await kontroler.OdgajateljHome();

        Assert.IsInstanceOfType<ChallengeResult>(rezultat);
    }

    [TestMethod]
    public async Task OdgajateljHomeBezProfilaVracaNotFound()
    {
        using var baza = NapraviBazu();
        using var userManager = NapraviUserManager(new Korisnik { Id = 42 });
        var kontroler = NapraviKontroler(baza, userManager);

        var rezultat = await kontroler.OdgajateljHome();

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task OdgajateljHomePrikazujeGrupeIObavijesti()
    {
        using var baza = NapraviBazu();

        var odgajatelj = new Odgajatelj { ImePrezime = "Emina Kovač" };
        odgajatelj.Grupe.Add(new Grupa { ImeGrupe = "Zeke" });
        odgajatelj.Grupe.Add(new Grupa { ImeGrupe = "Bubamare" });

        var roditelj = new Roditelj { ImePrezime = "Amra Kovač" };
        baza.Odgajatelji.Add(odgajatelj);
        baza.Obavijesti.AddRange(
            new Obavijest { Roditelj = roditelj, StatusObavijesti = StatusObavijesti.KREIRANA },
            new Obavijest { Roditelj = roditelj, StatusObavijesti = StatusObavijesti.POSLANA },
            new Obavijest { Roditelj = roditelj, StatusObavijesti = StatusObavijesti.PROCITANA });

        await baza.SaveChangesAsync();
        using var userManager = NapraviUserManager(odgajatelj);
        var kontroler = NapraviKontroler(baza, userManager);

        var rezultat = await kontroler.OdgajateljHome();

        var pogled = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var model = Assert.IsInstanceOfType<OdgajateljHomeViewModel>(pogled.Model);
        Assert.AreEqual("Emina", model.Ime);
        Assert.AreEqual("Emina Kovač", model.ImePrezime);
        Assert.AreEqual(2, model.BrojNovihObavijesti);

        var naziviGrupa = model.Grupe.Select(grupa => grupa.ImeGrupe).ToArray();
        CollectionAssert.AreEqual(new[] { "Bubamare", "Zeke" }, naziviGrupa);
    }

    private static HomeController NapraviKontroler(
        ApplicationDbContext? baza = null,
        UserManager<Korisnik>? userManager = null)
    {
        return new HomeController(NullLogger<HomeController>.Instance, baza!, userManager!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static ApplicationDbContext NapraviBazu()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(opcije);
    }

    private static UserManager<Korisnik> NapraviUserManager(Korisnik? korisnik)
    {
        var userManager = new Mock<UserManager<Korisnik>>(
            Mock.Of<IUserStore<Korisnik>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);

        userManager
            .Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(korisnik);

        return userManager.Object;
    }
}