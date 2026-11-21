
using EVrtic.Data;
using EVrtic.Models;
using EVrtic.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eVrticSistem.Tests.Merisa;

[TestClass]
public class AlergijaDjetetaControllerTests
{
    private ApplicationDbContext _baza = null!;
    private AlergijaDjetetaController _kontroler = null!;

    [TestInitialize]
    public void PripremiKontroler()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _baza = new ApplicationDbContext(opcije);
        _kontroler = new AlergijaDjetetaController(_baza);
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
        var alergija = new AlergijaDjeteta
        {
            Naziv = "Kikiriki"
        };

        var rezultat = await _kontroler.Create(alergija);

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
        var alergija = new AlergijaDjeteta
        {
            Id = 1,
            Naziv = "Mlijeko"
        };

        var rezultat = await _kontroler.Edit(1, alergija);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task DeleteGetVracaForbid()
    {
        var rezultat = await _kontroler.Delete(1);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task DeleteConfirmedBrisePostojecuAlergiju()
    {
        var alergija = new AlergijaDjeteta
        {
            Naziv = "Kikiriki"
        };

        _baza.AlergijeDjece.Add(alergija);
        await _baza.SaveChangesAsync();

        var id = alergija.Id;

        var rezultat = await _kontroler.DeleteConfirmed(id);

        Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);

        var obrisanaAlergija = await _baza.AlergijeDjece.FindAsync(id);

        Assert.IsNull(obrisanaAlergija);
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
        var alergija = new AlergijaDjeteta
        {
            Naziv = "Jaja"
        };

        _baza.AlergijeDjece.Add(alergija);
        await _baza.SaveChangesAsync();

        var rezultat = await _kontroler.DeleteConfirmed(alergija.Id);

        var preusmjeravanje =
            Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);

        Assert.AreEqual("Index", preusmjeravanje.ActionName);
    }
}
