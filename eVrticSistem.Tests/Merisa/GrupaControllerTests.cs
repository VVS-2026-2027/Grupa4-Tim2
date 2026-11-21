using EVrtic.Data;
using EVrtic.Models;
using EVrtic.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace eVrticSistem.Tests.Merisa;

[TestClass]
public class GrupaControllerTests
{
    private ApplicationDbContext _baza = null!;
    private GrupaController _kontroler = null!;
    private Mock<UserManager<Korisnik>> _userManager = null!;

    [TestInitialize]
    public void PripremiKontroler()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _baza = new ApplicationDbContext(opcije);

        var store = new Mock<IUserStore<Korisnik>>();

        _userManager = new Mock<UserManager<Korisnik>>(
            store.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        _kontroler = new GrupaController(_baza, _userManager.Object);
        _kontroler.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            Mock.Of<ITempDataProvider>()
        );
    }

    [TestCleanup]
    public void OslobodiKontekst()
    {
        _baza.Dispose();
    }

    // INDEX

    [TestMethod]
    public async Task IndexVracaSveGrupe()
    {
        var grupa1 = new Grupa { ImeGrupe = "Leptirici" };
        var grupa2 = new Grupa { ImeGrupe = "Zabice" };

        _baza.Grupe.AddRange(grupa1, grupa2);
        await _baza.SaveChangesAsync();

        var rezultat = await _kontroler.Index();

        var prikaz = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var grupe = prikaz.Model as List<Grupa>;

        Assert.IsNotNull(grupe);

        Assert.AreEqual(2, grupe.Count);
        Assert.AreEqual("Leptirici", grupe[0].ImeGrupe);
        Assert.AreEqual("Zabice", grupe[1].ImeGrupe);
    }


    [TestMethod]
    public async Task CreateGetVracaViewSaModelom()
    {
        var rezultat = await _kontroler.Create();

        var prikaz = Assert.IsInstanceOfType<ViewResult>(rezultat);

        Assert.IsInstanceOfType<GrupaFormViewModel>(prikaz.Model);
    }

    // CREATE - POST

    [TestMethod]
    public async Task CreatePostUspjesnoKreiraGrupuIDodjeljujeDjeci()
    {
        var dijete1 = new Dijete { ImePrezime = "Amina Kovac" };
        var dijete2 = new Dijete { ImePrezime = "Emina Kovac" };

        _baza.Djeca.AddRange(dijete1, dijete2);
        await _baza.SaveChangesAsync();

        var vm = new GrupaFormViewModel
        {
            ImeGrupe = " Leptirici ",
            OdgajateljId = 1,
            OdabranaDjecaIds = new List<int>
            {
                dijete1.Id,
                dijete2.Id
            }
        };

        var rezultat = await _kontroler.Create(vm);

        var preusmjerenje =
            Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);

        Assert.AreEqual(nameof(GrupaController.Index), preusmjerenje.ActionName);

        var grupa = await _baza.Grupe.FirstOrDefaultAsync();

        Assert.IsNotNull(grupa);
        Assert.AreEqual("Leptirici", grupa.ImeGrupe);

        var sacuvanaDjeca = await _baza.Djeca
            .Where(d => d.GrupaId == grupa.Id)
            .ToListAsync();

        Assert.AreEqual(2, sacuvanaDjeca.Count);
    }

    [TestMethod]
    public async Task CreatePostBezImenaGrupeVracaView()
    {
        var vm = new GrupaFormViewModel
        {
            ImeGrupe = "",
            OdgajateljId = 1,
            OdabranaDjecaIds = new List<int> { 1, 2 }
        };

        var rezultat = await _kontroler.Create(vm);

        Assert.IsInstanceOfType<ViewResult>(rezultat);
        Assert.IsTrue(_kontroler.ModelState.ContainsKey(nameof(vm.ImeGrupe)));
        Assert.AreEqual(0, await _baza.Grupe.CountAsync());
    }

    [TestMethod]
    public async Task CreatePostBezOdgajateljaVracaView()
    {
        var vm = new GrupaFormViewModel
        {
            ImeGrupe = "Leptirici",
            OdgajateljId = null,
            OdabranaDjecaIds = new List<int> { 1, 2 }
        };

        var rezultat = await _kontroler.Create(vm);

        Assert.IsInstanceOfType<ViewResult>(rezultat);
        Assert.IsTrue(_kontroler.ModelState.ContainsKey(nameof(vm.OdgajateljId)));
        Assert.AreEqual(0, await _baza.Grupe.CountAsync());
    }

    [TestMethod]
    public async Task CreatePostSaManjeOdDvojeDjeceVracaView()
    {
        var vm = new GrupaFormViewModel
        {
            ImeGrupe = "Leptirici",
            OdgajateljId = 1,
            OdabranaDjecaIds = new List<int> { 1 }
        };

        var rezultat = await _kontroler.Create(vm);

        Assert.IsInstanceOfType<ViewResult>(rezultat);
        Assert.IsTrue(_kontroler.ModelState.ContainsKey(nameof(vm.OdabranaDjecaIds)));
        Assert.AreEqual(0, await _baza.Grupe.CountAsync());
    }

    [TestMethod]
    public async Task CreatePostSaPostojecimImenomGrupeVracaView()
    {
        _baza.Grupe.Add(new Grupa { ImeGrupe = "Leptirici" });
        await _baza.SaveChangesAsync();

        var vm = new GrupaFormViewModel
        {
            ImeGrupe = " LEPTIRICI ",
            OdgajateljId = 1,
            OdabranaDjecaIds = new List<int> { 1, 2 }
        };

        var rezultat = await _kontroler.Create(vm);

        Assert.IsInstanceOfType<ViewResult>(rezultat);
        Assert.IsTrue(_kontroler.ModelState.ContainsKey(nameof(vm.ImeGrupe)));
        Assert.AreEqual(1, await _baza.Grupe.CountAsync());
    }

    [TestMethod]
    public async Task EditGetZaNullIdVracaNotFound()
    {
        var rezultat = await _kontroler.Edit(null);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task EditGetZaNepostojeciIdVracaNotFound()
    {
        var rezultat = await _kontroler.Edit(42);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task EditGetZaPostojecuGrupuVracaViewModel()
    {
        var grupa = new Grupa { ImeGrupe = "Leptirici" };

        _baza.Grupe.Add(grupa);
        await _baza.SaveChangesAsync();

        var rezultat = await _kontroler.Edit(grupa.Id);

        var prikaz = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var vm = Assert.IsInstanceOfType<GrupaFormViewModel>(prikaz.Model);

        Assert.AreEqual(grupa.Id, vm.Id);
        Assert.AreEqual("Leptirici", vm.ImeGrupe);
    }

    [TestMethod]
    public async Task EditPostZaRazliciteIdVrijednostiVracaNotFound()
    {
        var vm = new GrupaFormViewModel
        {
            Id = 10,
            ImeGrupe = "Leptirici",
            OdgajateljId = 1
        };

        var rezultat = await _kontroler.Edit(20, vm);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task EditPostUspjesnoMijenjaImeGrupe()
    {
        var grupa = new Grupa { ImeGrupe = "Leptirici" };

        _baza.Grupe.Add(grupa);
        await _baza.SaveChangesAsync();

        var vm = new GrupaFormViewModel
        {
            Id = grupa.Id,
            ImeGrupe = " Zekani ",
            OdgajateljId = 1
        };

        var rezultat = await _kontroler.Edit(grupa.Id, vm);

        var preusmjerenje =
            Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);

        Assert.AreEqual(nameof(GrupaController.Index), preusmjerenje.ActionName);

        var izmijenjenaGrupa = await _baza.Grupe.FindAsync(grupa.Id);

        Assert.IsNotNull(izmijenjenaGrupa);
        Assert.AreEqual("Zekani", izmijenjenaGrupa.ImeGrupe);
    }

    [TestMethod]
    public async Task EditPostZaNepostojecuGrupuVracaNotFound()
    {
        var vm = new GrupaFormViewModel
        {
            Id = 42,
            ImeGrupe = "Leptirici",
            OdgajateljId = 1
        };

        var rezultat = await _kontroler.Edit(42, vm);

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task EditPostBezImenaGrupeVracaView()
    {
        var grupa = new Grupa { ImeGrupe = "Leptirici" };

        _baza.Grupe.Add(grupa);
        await _baza.SaveChangesAsync();

        var vm = new GrupaFormViewModel
        {
            Id = grupa.Id,
            ImeGrupe = "",
            OdgajateljId = 1
        };

        var rezultat = await _kontroler.Edit(grupa.Id, vm);

        Assert.IsInstanceOfType<ViewResult>(rezultat);
        Assert.IsTrue(_kontroler.ModelState.ContainsKey(nameof(vm.ImeGrupe)));
    }

    [TestMethod]
    public async Task EditPostBezOdgajateljaVracaView()
    {
        var grupa = new Grupa { ImeGrupe = "Leptirici" };

        _baza.Grupe.Add(grupa);
        await _baza.SaveChangesAsync();

        var vm = new GrupaFormViewModel
        {
            Id = grupa.Id,
            ImeGrupe = "Leptirici",
            OdgajateljId = null
        };

        var rezultat = await _kontroler.Edit(grupa.Id, vm);

        Assert.IsInstanceOfType<ViewResult>(rezultat);
        Assert.IsTrue(_kontroler.ModelState.ContainsKey(nameof(vm.OdgajateljId)));
    }

    [TestMethod]
    public async Task DeleteGetVracaForbid()
    {
        var rezultat = await _kontroler.Delete(42);

        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task DeleteConfirmedBriseGrupuIOslobadjaDjecu()
    {
        var grupa = new Grupa { ImeGrupe = "Leptirici" };

        var dijete1 = new Dijete { ImePrezime = "Amina Kovac" };
        var dijete2 = new Dijete { ImePrezime = "Emina Kovac" };

        grupa.Djeca.Add(dijete1);
        grupa.Djeca.Add(dijete2);

        _baza.Grupe.Add(grupa);
        await _baza.SaveChangesAsync();

        var rezultat = await _kontroler.DeleteConfirmed(grupa.Id);

        var preusmjerenje =
            Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);

        Assert.AreEqual(nameof(GrupaController.Index), preusmjerenje.ActionName);

        Assert.IsFalse(await _baza.Grupe.AnyAsync(g => g.Id == grupa.Id));

        var sacuvanaDjeca = await _baza.Djeca.ToListAsync();

        Assert.AreEqual(2, sacuvanaDjeca.Count);
        Assert.IsTrue(sacuvanaDjeca.All(d => d.GrupaId == null));
    }

    [TestMethod]
    public async Task DeleteConfirmedZaNepostojeciIdPreusmjeravaNaIndex()
    {
        var rezultat = await _kontroler.DeleteConfirmed(999);

        var preusmjerenje =
            Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);

        Assert.AreEqual(nameof(GrupaController.Index), preusmjerenje.ActionName);
        Assert.AreEqual(0, await _baza.Grupe.CountAsync());
    }
}
