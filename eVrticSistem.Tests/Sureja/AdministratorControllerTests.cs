using EVrtic.Data;
using EVrtic.Models;
using eVrticSistem.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eVrticSistem.Tests.Sureja;

[TestClass]
public class AdministratorControllerTests
{
    private ApplicationDbContext _baza = null!;
    private AdministratorController _kontroler = null!;

    [TestInitialize]
    public void PripremiKontroler()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _baza = new ApplicationDbContext(opcije);
        _kontroler = new AdministratorController(_baza);
    }

    [TestCleanup]
    public void OslobodiKontekst()
    {
        _baza.Dispose();
    }

    [TestMethod]
    public async Task DeleteConfirmedBriseOdabranogAdministratora()
    {
        var odabrani = new Administrator { ImePrezime = "Amina Kovač" };
        var drugi = new Administrator { ImePrezime = "Emina Kovač" };

        _baza.Administratori.AddRange(odabrani, drugi);
        await _baza.SaveChangesAsync();

        var rezultat = await _kontroler.DeleteConfirmed(odabrani.Id);

        var preusmjerenje = Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual(nameof(AdministratorController.Index), preusmjerenje.ActionName);
        Assert.IsFalse(await _baza.Administratori.AnyAsync(a => a.Id == odabrani.Id));
        Assert.IsTrue(await _baza.Administratori.AnyAsync(a => a.Id == drugi.Id));
    }

    [TestMethod]
    public async Task DeleteConfirmedZaNepostojeciIdPreusmjeravaNaIndex()
    {
        var administrator = new Administrator
        {
            Id = 42,
            ImePrezime = "Amina Kovač"
        };

        _baza.Administratori.Add(administrator);
        await _baza.SaveChangesAsync();

        var rezultat = await _kontroler.DeleteConfirmed(999);

        var preusmjerenje = Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual(nameof(AdministratorController.Index), preusmjerenje.ActionName);
        Assert.IsTrue(await _baza.Administratori.AnyAsync(a => a.Id == administrator.Id));
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
        var rezultat = await _kontroler.Details(42);

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
        var administrator = new Administrator { ImePrezime = "Amina Kovač" };

        var rezultat = await _kontroler.Create(administrator);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task EditGetVracaForbid()
    {
        var rezultat = await _kontroler.Edit(42);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task EditPostVracaForbid()
    {
        var administrator = new Administrator
        {
            Id = 42,
            ImePrezime = "Amina Kovač"
        };

        var rezultat = await _kontroler.Edit(administrator.Id, administrator);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }
    [TestMethod]
public async Task DeleteGetVracaNotFound()
{
    var rezultat = await _kontroler.Delete(42);

    Assert.IsInstanceOfType<NotFoundResult>(rezultat);
}
}