
using EVrtic.Data;
using EVrtic.Models;
using EVrtic.Controllers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace eVrticSistem.Tests.Merisa;

[TestClass]
public class OdgajateljControllerTests
{
    private ApplicationDbContext _baza = null!;
    private OdgajateljController _kontroler = null!;

    [TestInitialize]
    public void PripremiKontroler()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _baza = new ApplicationDbContext(opcije);
        _kontroler = new OdgajateljController(_baza);

        _kontroler.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            Mock.Of<ITempDataProvider>()
        );
    }

    [TestCleanup]
    public void OcistiBazu()
    {
        _baza.Dispose();
    }

    // INDEX

    [TestMethod]
    public async Task IndexVracaForbidSaStatusom403()
    {
        var rezultat = await _kontroler.Index();

        await AssertForbidStatus403(rezultat);
    }

    // DEAKTIVIRAJ

    [TestMethod]
    public async Task DeaktivirajMijenjaStatusOdgajatelja()
    {
        var odgajatelj = new Odgajatelj
        {
            ImePrezime = "Test Odgajatelj",
            Email = "test@example.com",
            StatusNaloga = StatusNaloga.AKTIVAN
        };

        _baza.Odgajatelji.Add(odgajatelj);
        await _baza.SaveChangesAsync();

        var rezultat = await _kontroler.Deaktiviraj(odgajatelj.Id);

        Assert.AreEqual(
            StatusNaloga.DEAKTIVIRAN,
            odgajatelj.StatusNaloga
        );

        Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);

        var preusmjeravanje = (RedirectToActionResult)rezultat;
        Assert.AreEqual("Index", preusmjeravanje.ActionName);

        Assert.IsNotNull(_kontroler.TempData["Uspjeh"]);
    }

    [TestMethod]
    public async Task DeaktivirajZaNepostojeciIdVracaNotFound()
    {
        var rezultat = await _kontroler.Deaktiviraj(999);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    // AKTIVIRAJ

    [TestMethod]
    public async Task AktivirajMijenjaStatusOdgajatelja()
    {
        var odgajatelj = new Odgajatelj
        {
            ImePrezime = "Test Odgajatelj",
            Email = "test@example.com",
            StatusNaloga = StatusNaloga.DEAKTIVIRAN
        };

        _baza.Odgajatelji.Add(odgajatelj);
        await _baza.SaveChangesAsync();

        var rezultat = await _kontroler.Aktiviraj(odgajatelj.Id);

        Assert.AreEqual(
            StatusNaloga.AKTIVAN,
            odgajatelj.StatusNaloga
        );

        Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);

        var preusmjeravanje = (RedirectToActionResult)rezultat;
        Assert.AreEqual("Index", preusmjeravanje.ActionName);

        Assert.IsNotNull(_kontroler.TempData["Uspjeh"]);
    }

    [TestMethod]
    public async Task AktivirajZaNepostojeciIdVracaNotFound()
    {
        var rezultat = await _kontroler.Aktiviraj(999);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    // DETAILS

    [TestMethod]
    public async Task DetailsVracaForbidSaStatusom403()
    {
        var rezultat = await _kontroler.Details(1);

        await AssertForbidStatus403(rezultat);
    }

    // CREATE

    [TestMethod]
    public async Task CreateGetVracaForbidSaStatusom403()
    {
        var rezultat = _kontroler.Create();

        await AssertForbidStatus403(rezultat);
    }

    [TestMethod]
    public async Task CreatePostVracaForbid()
    {
        var odgajatelj = new Odgajatelj
        {
            ImePrezime = "Novi Odgajatelj",
            Email = "novi@example.com",
            StatusNaloga = StatusNaloga.AKTIVAN
        };

        var rezultat = await _kontroler.Create(odgajatelj);

        await AssertForbidStatus403(rezultat);
    }

    // EDIT

    [TestMethod]
    public async Task EditGetVracaForbidSaStatusom403()
    {
        var rezultat = await _kontroler.Edit(1);

        await AssertForbidStatus403(rezultat);
    }

    [TestMethod]
    public async Task EditPostVracaForbid()
    {
        var odgajatelj = new Odgajatelj
        {
            Id = 1,
            ImePrezime = "Izmijenjeni Odgajatelj",
            Email = "izmijenjeni@example.com",
            StatusNaloga = StatusNaloga.AKTIVAN
        };

        var rezultat = await _kontroler.Edit(1, odgajatelj);

        await AssertForbidStatus403(rezultat);
    }


    [TestMethod]
    public async Task OdgajateljExistsVracaTrueAkoOdgajateljPostoji()
    {
        // Arrange
        var odgajatelj = new Odgajatelj
        {
            ImePrezime = "Test Odgajatelj",
            Email = "test@example.com",
            StatusNaloga = StatusNaloga.AKTIVAN
        };

        _baza.Odgajatelji.Add(odgajatelj);
        await _baza.SaveChangesAsync();

        // Act
        var metoda = typeof(OdgajateljController).GetMethod(
            "OdgajateljExists",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance
        );

        Assert.IsNotNull(metoda);

        var rezultat = metoda.Invoke(
            _kontroler,
            new object[] { odgajatelj.Id }
        );

        // Assert
        Assert.AreEqual(true, rezultat);
    }

    [TestMethod]
    public void OdgajateljExistsVracaFalseAkoOdgajateljNePostoji()
    {
        // Arrange
        var nepostojeciId = 999;

        // Act
        var metoda = typeof(OdgajateljController).GetMethod(
            "OdgajateljExists",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance
        );

        Assert.IsNotNull(metoda);

        var rezultat = metoda.Invoke(
            _kontroler,
            new object[] { nepostojeciId }
        );

        // Assert
        Assert.AreEqual(false, rezultat);
    }


    // DELETE

    [TestMethod]
    public async Task DeleteGetVracaForbidSaStatusom403()
    {
        var rezultat = await _kontroler.Delete(1);

        await AssertForbidStatus403(rezultat);
    }

    [TestMethod]
    public async Task DeleteConfirmedBrisePostojecegOdgajatelja()
    {
        var odgajatelj = new Odgajatelj
        {
            ImePrezime = "Odgajatelj Za Brisanje",
            Email = "brisanje@example.com",
            StatusNaloga = StatusNaloga.AKTIVAN
        };

        _baza.Odgajatelji.Add(odgajatelj);
        await _baza.SaveChangesAsync();

        var id = odgajatelj.Id;

        var rezultat = await _kontroler.DeleteConfirmed(id);

        Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);

        var obrisaniOdgajatelj =
            await _baza.Odgajatelji.FindAsync(id);

        Assert.IsNull(obrisaniOdgajatelj);
    }

    [TestMethod]
    public async Task DeleteConfirmedZaNepostojeciIdPreusmjeravaNaIndex()
    {
        var rezultat = await _kontroler.DeleteConfirmed(999);

        var preusmjeravanje =
            Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);

        Assert.AreEqual("Index", preusmjeravanje.ActionName);
    }

    private static async Task AssertForbidStatus403(IActionResult rezultat)
    {
        var forbid = Assert.IsInstanceOfType<ForbidResult>(rezultat);
        var autentifikacija = new Mock<IAuthenticationService>();
        autentifikacija
            .Setup(a => a.ForbidAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string?>(),
                It.IsAny<AuthenticationProperties?>()))
            .Callback<HttpContext, string?, AuthenticationProperties?>(
                (httpContext, _, _) => httpContext.Response.StatusCode = StatusCodes.Status403Forbidden)
            .Returns(Task.CompletedTask);

        using var servisi = new ServiceCollection()
            .AddLogging()
            .AddSingleton(autentifikacija.Object)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = servisi };
        var actionContext = new ActionContext(
            httpContext, new RouteData(), new ActionDescriptor());

        await forbid.ExecuteResultAsync(actionContext);

        Assert.AreEqual(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        autentifikacija.Verify(a => a.ForbidAsync(
            httpContext,
            It.IsAny<string?>(),
            It.IsAny<AuthenticationProperties?>()), Times.Once());
    }
}
