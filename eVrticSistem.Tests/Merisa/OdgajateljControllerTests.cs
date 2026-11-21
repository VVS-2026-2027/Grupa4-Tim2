
using EVrtic.Data;
using EVrtic.Models;
using EVrtic.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
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
    public async Task IndexVracaForbid()
    {
        var rezultat = await _kontroler.Index();

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
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
    public async Task DetailsVracaForbid()
    {
        var rezultat = await _kontroler.Details(1);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    // CREATE

    [TestMethod]
    public void CreateGetVracaForbid()
    {
        var rezultat = _kontroler.Create();

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
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

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    // EDIT

    [TestMethod]
    public async Task EditGetVracaForbid()
    {
        var rezultat = await _kontroler.Edit(1);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
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

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    // DELETE

    [TestMethod]
    public async Task DeleteGetVracaForbid()
    {
        var rezultat = await _kontroler.Delete(1);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
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
}
