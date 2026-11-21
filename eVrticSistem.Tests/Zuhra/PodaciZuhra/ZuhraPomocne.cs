using System;
using System.Security.Claims;
using EVrtic.Data;
using EVrtic.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace eVrticSistem.Tests.Zuhra;

// Pomoćne metode za ObavijestControllerTests (ne ponavljati kod)
public static class ZuhraPomocne
{
    public static T Kao<T>(IActionResult rezultat) where T : class =>
        (T)rezultat;

    public static T ModelIz<T>(IActionResult rezultat) =>
        (T)((ViewResult)rezultat).Model!;

    public static void NikoNijePrijavljen(this Mock<UserManager<Korisnik>> mock) =>
        mock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((Korisnik?)null);

    public static void PrijaviKorisnika(this Mock<UserManager<Korisnik>> mock, int id) =>
        mock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new Korisnik { Id = id });
}

// Zamjenski objekti i fake baza
public static class TestOkruzenje
{
    public static DbContextOptions<ApplicationDbContext> NapraviFakeBazu(string imeBaze) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(imeBaze)
            .Options;

    public static Mock<UserManager<Korisnik>> NapraviUserManagerMock()
    {
        var store = new Mock<IUserStore<Korisnik>>();
        return new Mock<UserManager<Korisnik>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    public static void PostaviKontekst(Controller kontroler)
    {
        var http = new DefaultHttpContext();
        kontroler.ControllerContext = new ControllerContext { HttpContext = http };
        kontroler.TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>());
    }
}

// Pravi i sprema testne entitete u fake bazu
public class GraditeljPodataka
{
    private readonly ApplicationDbContext _context;

    public GraditeljPodataka(ApplicationDbContext context) => _context = context;

    public Odgajatelj DodajOdgajatelja(int id, string ime)
    {
        var odgajatelj = new Odgajatelj
        {
            Id = id,
            ImePrezime = ime,
            Email = $"odgajatelj{id}@mail.ba",
            UserName = $"odgajatelj{id}"
        };
        _context.Odgajatelji.Add(odgajatelj);
        _context.SaveChanges();
        return odgajatelj;
    }

    public Roditelj DodajRoditelja(int id, string ime, string? email)
    {
        var roditelj = new Roditelj
        {
            Id = id,
            ImePrezime = ime,
            Email = email,
            UserName = $"roditelj{id}"
        };
        _context.Roditelji.Add(roditelj);
        _context.SaveChanges();
        return roditelj;
    }

    public Grupa DodajGrupu(int id, int odgajateljId)
    {
        var grupa = new Grupa { Id = id, ImeGrupe = $"Grupa {id}", OdgajateljId = odgajateljId };
        _context.Grupe.Add(grupa);
        _context.SaveChanges();
        return grupa;
    }

    public Dijete DodajDijete(int id, string ime, int roditeljId, Grupa grupa)
    {
        var dijete = new Dijete
        {
            Id = id,
            ImePrezime = ime,
            IdentifikacioniKod = $"D{id:000}",
            RoditeljId = roditeljId,
            GrupaId = grupa.Id,
            Aktivno = true
        };
        _context.Djeca.Add(dijete);
        _context.SaveChanges();
        return dijete;
    }

    public Obavijest DodajObavijest(
        Odgajatelj odgajatelj,
        Roditelj roditelj,
        string naslov,
        DateTime kreirano,
        StatusObavijesti status = StatusObavijesti.POSLANA,
        DateTime? slano = null)
    {
        var obavijest = new Obavijest
        {
            Naslov = naslov,
            Sadrzaj = "Sadržaj obavijesti",
            DatumKreiranja = kreirano,
            DatumSlanja = slano,
            KanalSlanja = KanalSlanja.APLIKACIJA,
            StatusObavijesti = status,
            RoditeljId = roditelj.Id,
            OdgajateljId = odgajatelj.Id
        };
        _context.Obavijesti.Add(obavijest);
        _context.SaveChanges();
        return obavijest;
    }
}