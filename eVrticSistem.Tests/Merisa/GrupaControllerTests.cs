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

namespace eVrticSistem.Tests.Merisa;

[TestClass]
public class GrupaControllerTests
{
    private ApplicationDbContext _baza = null!;
    private Mock<UserManager<Korisnik>> _korisnici = null!;
    private GrupaController _kontroler = null!;

    [TestInitialize]
    public void PripremiKontroler()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _baza = new ApplicationDbContext(opcije);
        _korisnici = new Mock<UserManager<Korisnik>>(
            Mock.Of<IUserStore<Korisnik>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);

        _kontroler = new GrupaController(_baza, _korisnici.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        _kontroler.TempData = new TempDataDictionary(
            _kontroler.HttpContext, Mock.Of<ITempDataProvider>());
    }

    [TestCleanup]
    public void OcistiBazu()
    {
        _korisnici?.Object.Dispose();
        _baza.Dispose();
    }

    [TestMethod]
    public async Task IndexVracaSortiraneGrupe()
    {
        await DodajGrupu("Zvezdice");
        await DodajGrupu("Cvjetići");

        var rezultat = await _kontroler.Index();

        var view = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var grupe = Assert.IsInstanceOfType<List<Grupa>>(view.Model);
        Assert.AreEqual("Cvjetići", grupe[0].ImeGrupe);
        Assert.AreEqual("Zvezdice", grupe[1].ImeGrupe);
    }

    [TestMethod]
    public async Task CreateGetPopunjavaOdgajateljeIDostupnuDjecu()
    {
        await DodajOdgajatelja("Amina Čengić", "amina@example.test");
        await DodajDijete("Amin", null);
        await DodajDijete(string.Empty, null, "D0000002");

        var rezultat = await _kontroler.Create();

        var view = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var vm = Assert.IsInstanceOfType<GrupaFormViewModel>(view.Model);
        Assert.AreEqual(1, vm.Odgajatelji.Count);
        Assert.AreEqual("Amina Čengić", vm.Odgajatelji[0].Text);
        Assert.AreEqual(2, vm.DostupnaDjeca.Count);
        Assert.IsTrue(vm.DostupnaDjeca.Any(d => d.ImePrezime == "(kod: D0000002)"));
    }

    [TestMethod]
    public async Task CreateGetKoristiEmailKadOdgajateljNemaIme()
    {
        await DodajOdgajatelja(string.Empty, "odgajatelj@example.test");

        var rezultat = await _kontroler.Create();

        var view = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var vm = Assert.IsInstanceOfType<GrupaFormViewModel>(view.Model);
        Assert.AreEqual("odgajatelj@example.test", vm.Odgajatelji[0].Text);
    }

    [TestMethod]
    [DataRow(true, false, false, false)]
    [DataRow(false, true, false, false)]
    [DataRow(false, false, true, false)]
    [DataRow(false, false, false, true)]
    public async Task CreatePostNeispravanModelVracaFormu(
        bool praznoIme, bool duplikat, bool nemaOdgajatelja, bool nedovoljnoDjece)
    {
        await DodajOdgajatelja("Amina Čengić");
        await DodajGrupu("Leptirići");

        var vm = new GrupaFormViewModel
        {
            ImeGrupe = praznoIme ? " " : duplikat ? " leptirići " : "Novi naziv",
            OdgajateljId = nemaOdgajatelja ? null : 1,
            OdabranaDjecaIds = nedovoljnoDjece ? new List<int> { 1 } : new List<int> { 1, 2 }
        };

        var rezultat = await _kontroler.Create(vm);

        Assert.IsInstanceOfType<ViewResult>(rezultat);
        Assert.IsFalse(_kontroler.ModelState.IsValid);
    }

    [TestMethod]
    public async Task CreatePostUspesnoKreiraGrupuIPovezujeDjecu()
    {
        var odgajatelj = await DodajOdgajatelja("Amina Čengić");
        var dijete1 = await DodajDijete("Prvo Dijete", null);
        var dijete2 = await DodajDijete("Drugo Dijete", null);

        var vm = new GrupaFormViewModel
        {
            ImeGrupe = "  Suncokreti  ",
            OdgajateljId = odgajatelj.Id,
            OdabranaDjecaIds = new List<int> { dijete1.Id, dijete2.Id }
        };

        var rezultat = await _kontroler.Create(vm);

        var preusmjerenje = Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual(nameof(GrupaController.Index), preusmjerenje.ActionName);

        var grupa = await _baza.Grupe.SingleAsync();
        Assert.AreEqual("Suncokreti", grupa.ImeGrupe);
        Assert.AreEqual(odgajatelj.Id, grupa.OdgajateljId);
        Assert.AreEqual(grupa.Id, (await _baza.Djeca.FindAsync(dijete1.Id))!.GrupaId);
        Assert.AreEqual(grupa.Id, (await _baza.Djeca.FindAsync(dijete2.Id))!.GrupaId);
        Assert.IsTrue(_kontroler.TempData.ContainsKey("Uspjeh"));
    }

    [TestMethod]
    public async Task CreatePostSaNulListomDjecaPostavljaPraznuListu()
    {
        var odgajatelj = await DodajOdgajatelja("Amina Čengić");
        var vm = new GrupaFormViewModel
        {
            ImeGrupe = "Nova grupa",
            OdgajateljId = odgajatelj.Id,
            OdabranaDjecaIds = null!
        };

        var rezultat = await _kontroler.Create(vm);

        Assert.IsInstanceOfType<ViewResult>(rezultat);
        Assert.IsTrue(_kontroler.ModelState[nameof(vm.OdabranaDjecaIds)]!.Errors.Count > 0);
    }

    [TestMethod]
    public async Task EditGetVracaNotFoundZaNullId()
    {
        var rezultat = await _kontroler.Edit((int?)null);
        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task EditGetVracaNotFoundZaNepostojecuGrupu()
    {
        var rezultat = await _kontroler.Edit(999);
        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task EditGetVracaModelSaTrenutnomDjecom()
    {
        var grupa = await DodajGrupu("Leptirići");
        var dijete = await DodajDijete("Amina", null, grupaId: grupa.Id);

        var rezultat = await _kontroler.Edit(grupa.Id);

        var view = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var vm = Assert.IsInstanceOfType<GrupaFormViewModel>(view.Model);
        Assert.AreEqual(grupa.Id, vm.Id);
        Assert.AreEqual(grupa.ImeGrupe, vm.ImeGrupe);
        Assert.AreEqual(dijete.Id, vm.OdabranaDjecaIds[0]);
        Assert.IsTrue(vm.DostupnaDjeca.Any(d => d.Id == dijete.Id));
    }

    [TestMethod]
    public async Task EditPostSaRazlicitimIdEvimaVracaNotFound()
    {
        var rezultat = await _kontroler.Edit(1, new GrupaFormViewModel { Id = 2 });
        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task EditPostNeispravanModelVracaFormu()
    {
        var grupa = await DodajGrupu("Leptirići");
        var vm = new GrupaFormViewModel
        {
            Id = grupa.Id,
            ImeGrupe = " ",
            OdgajateljId = null,
            OdabranaDjecaIds = null!
        };

        var rezultat = await _kontroler.Edit(grupa.Id, vm);

        Assert.IsInstanceOfType<ViewResult>(rezultat);
        Assert.IsFalse(_kontroler.ModelState.IsValid);
    }

    [TestMethod]
    public async Task EditPostSaDuplikatomImenaVracaGresku()
    {
        await DodajGrupu("Prva grupa");
        var druga = await DodajGrupu("Druga grupa");

        var rezultat = await _kontroler.Edit(druga.Id, new GrupaFormViewModel
        {
            Id = druga.Id,
            ImeGrupe = " prva GRUPA ",
            OdgajateljId = 1,
            OdabranaDjecaIds = new List<int>()
        });

        Assert.IsInstanceOfType<ViewResult>(rezultat);
        Assert.IsTrue(_kontroler.ModelState[nameof(GrupaFormViewModel.ImeGrupe)]!.Errors.Count > 0);
    }

    [TestMethod]
    public async Task EditPostVracaNotFoundKadGrupaNijePronadjena()
    {
        var rezultat = await _kontroler.Edit(999, new GrupaFormViewModel
        {
            Id = 999,
            ImeGrupe = "Nova grupa",
            OdgajateljId = 1,
            OdabranaDjecaIds = new List<int>()
        });

        Assert.IsInstanceOfType<NotFoundResult>(rezultat);
    }

    [TestMethod]
    public async Task EditPostUklanjaINoveDodajeDjecu()
    {
        var grupa = await DodajGrupu("Leptirići", 1);
        var ukloniti = await DodajDijete("Ukloniti", null, grupaId: grupa.Id);
        var dodati = await DodajDijete("Dodati", null);

        var rezultat = await _kontroler.Edit(grupa.Id, new GrupaFormViewModel
        {
            Id = grupa.Id,
            ImeGrupe = "  Zvezdice  ",
            OdgajateljId = 2,
            OdabranaDjecaIds = new List<int> { dodati.Id }
        });

        Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.IsNull((await _baza.Djeca.FindAsync(ukloniti.Id))!.GrupaId);
        Assert.AreEqual(grupa.Id, (await _baza.Djeca.FindAsync(dodati.Id))!.GrupaId);
        Assert.AreEqual("Zvezdice", (await _baza.Grupe.FindAsync(grupa.Id))!.ImeGrupe);
    }

    [TestMethod]
    public async Task DeleteVracaForbid()
    {
        var rezultat = await _kontroler.Delete(1);
        Assert.IsInstanceOfType<ForbidResult>(rezultat);
    }

    [TestMethod]
    public async Task DeleteConfirmedPostojecaGrupaOslobadjaDjecuIBriseGrupu()
    {
        var grupa = await DodajGrupu("Leptirići");
        var dijete = await DodajDijete("Amina", null, grupaId: grupa.Id);

        var rezultat = await _kontroler.DeleteConfirmed(grupa.Id);

        Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.IsNull(await _baza.Grupe.FindAsync(grupa.Id));
        Assert.IsNull((await _baza.Djeca.FindAsync(dijete.Id))!.GrupaId);
    }

    [TestMethod]
    public async Task DeleteConfirmedNepostojecaGrupaPreusmjeravaNaIndex()
    {
        var rezultat = await _kontroler.DeleteConfirmed(999);

        var preusmjerenje = Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual(nameof(GrupaController.Index), preusmjerenje.ActionName);
    }

    [TestMethod]
    public async Task MojaGrupaBezPrijavljenogKorisnikaPreusmjeravaNaLogin()
    {
        NemaPrijavljenogKorisnika();

        var rezultat = await _kontroler.MojaGrupa();

        var redirect = Assert.IsInstanceOfType<RedirectToPageResult>(rezultat);
        Assert.AreEqual("/Account/Login", redirect.PageName);
    }

    [TestMethod]
    public async Task MojaGrupaBezOdgajateljaPreusmjeravaNaHome()
    {
        PrijaviKorisnika(999);

        var rezultat = await _kontroler.MojaGrupa();

        var redirect = Assert.IsInstanceOfType<RedirectToActionResult>(rezultat);
        Assert.AreEqual("OdgajateljHome", redirect.ActionName);
        Assert.AreEqual("Home", redirect.ControllerName);
    }

    [TestMethod]
    public async Task MojaGrupaVracaGrupeIOdgajatelja()
    {
        var odgajatelj = await DodajOdgajatelja("Amina Čengić");
        var grupa = await DodajGrupu("Zvezdice", odgajatelj.Id);
        var dijete = await DodajDijete("Amin", null, grupaId: grupa.Id);
        _baza.AlergijeDjece.Add(new AlergijaDjeteta { Naziv = "Polen", DijeteId = dijete.Id });
        _baza.BolestiDjece.Add(new BolestDjeteta { Naziv = "Astma", DijeteId = dijete.Id });
        await _baza.SaveChangesAsync();
        PrijaviKorisnika(odgajatelj.Id);

        var rezultat = await _kontroler.MojaGrupa();

        var view = Assert.IsInstanceOfType<ViewResult>(rezultat);
        var grupe = Assert.IsInstanceOfType<List<Grupa>>(view.Model);
        Assert.AreEqual(1, grupe.Count);
        Assert.AreEqual("Zvezdice", grupe[0].ImeGrupe);
        Assert.AreSame(odgajatelj, _kontroler.ViewBag.Odgajatelj);
        Assert.AreEqual(1, grupe[0].Djeca.Single().Alergije.Count);
        Assert.AreEqual(1, grupe[0].Djeca.Single().Bolesti.Count);
    }

    private async Task<Grupa> DodajGrupu(string ime, int? odgajateljId = null)
    {
        var grupa = new Grupa { ImeGrupe = ime, OdgajateljId = odgajateljId };
        _baza.Grupe.Add(grupa);
        await _baza.SaveChangesAsync();
        return grupa;
    }

    private async Task<Odgajatelj> DodajOdgajatelja(string ime, string? email = null)
    {
        var odgajatelj = new Odgajatelj
        {
            ImePrezime = ime,
            Email = email,
            UserName = email
        };
        _baza.Odgajatelji.Add(odgajatelj);
        await _baza.SaveChangesAsync();
        return odgajatelj;
    }

    private async Task<Dijete> DodajDijete(string ime, int? roditeljId, string kod = "ABCD1234", int? grupaId = null)
    {
        var dijete = new Dijete
        {
            ImePrezime = ime,
            IdentifikacioniKod = kod,
            DatumRodjenja = DateTime.Today.AddYears(-4),
            RoditeljId = roditeljId,
            GrupaId = grupaId
        };
        _baza.Djeca.Add(dijete);
        await _baza.SaveChangesAsync();
        return dijete;
    }

    private void PrijaviKorisnika(int id)
    {
        _korisnici.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new Korisnik { Id = id });
    }

    private void NemaPrijavljenogKorisnika()
    {
        _korisnici.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((Korisnik?)null);
    }
}
