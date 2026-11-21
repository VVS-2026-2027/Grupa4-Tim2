
using EVrtic.Data;
using EVrtic.Models;
using EVrtic.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eVrticSistem.Tests.Merisa;

[TestClass]
public class BolestDjetetaControllerTests
{
    private ApplicationDbContext _baza = null!;
    private BolestDjetetaController _kontroler = null!;

    [TestInitialize]
    public void PripremiKontroler()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _baza = new ApplicationDbContext(opcije);
        _kontroler = new BolestDjetetaController(_baza);
    }

    [TestCleanup]
    public void OcistiBazu()
    {
        _baza.Dispose();
    }

    [TestMethod]
    public async Task IndexVracaForbid()
    {
        var rezultat = await _kontroler.Index();

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task DetailsVracaForbid()
    {
        var rezultat = await _kontroler.Details(1);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public void CreateGetVracaForbid()
    {
        var rezultat = _kontroler.Create();

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task CreatePostVracaForbid()
    {
        var bolest = new BolestDjeteta
        {
            Naziv = "Astma"
        };

        var rezultat = await _kontroler.Create(bolest);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task EditGetVracaForbid()
    {
        var rezultat = await _kontroler.Edit(1);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task EditPostVracaForbid()
    {
        var bolest = new BolestDjeteta
        {
            Id = 1,
            Naziv = "Alergijski rinitis"
        };

        var rezultat = await _kontroler.Edit(1, bolest);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task DeleteGetVracaForbid()
    {
        var rezultat = await _kontroler.Delete(1);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task DeleteConfirmedBrisePostojecuBolest()
    {
        var bolest = new BolestDjeteta
        {
            Naziv = "Astma"
        };

        _baza.BolestiDjece.Add(bolest);
        await _baza.SaveChangesAsync();

        var id = bolest.Id;

        var rezultat = await _kontroler.DeleteConfirmed(id);

        Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);

        var obrisanaBolest = await _baza.BolestiDjece.FindAsync(id);

        Assert.IsNull(obrisanaBolest);
    }

    [TestMethod]
    public async Task DeleteConfirmedZaNepostojeciIdPreusmjeravaNaIndex()
    {
        var rezultat = await _kontroler.DeleteConfirmed(999);

        var preusmjeravanje =
            Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);

        Assert.AreEqual("Index", preusmjeravanje.ActionName);
    }

    [TestMethod]
    public async Task DeleteConfirmedPreusmjeravaNaIndexNakonBrisanja()
    {
        var bolest = new BolestDjeteta
        {
            Naziv = "Dijabetes"
        };

        _baza.BolestiDjece.Add(bolest);
        await _baza.SaveChangesAsync();

        var rezultat = await _kontroler.DeleteConfirmed(bolest.Id);

        var preusmjeravanje =
            Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);

        Assert.AreEqual("Index", preusmjeravanje.ActionName);
    }
}
