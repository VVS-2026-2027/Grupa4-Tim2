using System;
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

namespace EVrtic.Tests.Belma;

internal static class BelmaTestPomocne
{
    public static readonly DateTime Datum = new(2026, 11, 18);

    public static ApplicationDbContext NoviKontekst()
    {
        var opcije = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"Belma-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(opcije);
    }

    public static Mock<UserManager<Korisnik>> KorisnickiMenadzer(Korisnik? korisnik)
    {
        var mock = new Mock<UserManager<Korisnik>>(
            Mock.Of<IUserStore<Korisnik>>(), null!, null!, null!, null!,
            null!, null!, null!, null!);
        mock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(korisnik);
        return mock;
    }

    public static T PripremiKontroler<T>(T kontroler) where T : Controller
    {
        var http = new DefaultHttpContext();
        kontroler.ControllerContext = new ControllerContext { HttpContext = http };
        kontroler.TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>());
        return kontroler;
    }

    public static void DodajOdgajateljaIGrupu(ApplicationDbContext db)
    {
        db.Odgajatelji.Add(new Odgajatelj
        {
            Id = 81, ImePrezime = "Test Odgajatelj", UserName = "odgajatelj@test.local",
            Email = "odgajatelj@test.local"
        });
        db.Grupe.Add(new Grupa { Id = 7, ImeGrupe = "Plava grupa", OdgajateljId = 81 });
        db.SaveChanges();
    }

    public static Dijete DodajDijete(ApplicationDbContext db, int id = 101,
        int? grupaId = 7, int? roditeljId = null, bool aktivno = true)
    {
        var dijete = new Dijete
        {
            Id = id, ImePrezime = "Test Dijete", IdentifikacioniKod = id.ToString("D8"),
            DatumRodjenja = new DateTime(2021, 5, 5), GrupaId = grupaId,
            RoditeljId = roditeljId, Aktivno = aktivno
        };
        db.Djeca.Add(dijete);
        db.SaveChanges();
        return dijete;
    }

    public static void DodajDogadjaj(ApplicationDbContext db, int dijeteId,
        TipDogadjaja tip, DateTime? vrijeme = null,
        StatusEvidencije status = StatusEvidencije.EVIDENTIRANO)
    {
        db.EvidencijeDolaskaOdlaska.Add(new EvidencijaDolaskaOdlaska
        {
            DijeteId = dijeteId,
            TipDogadjaja = tip,
            VrijemeDogadjaja = vrijeme ?? Datum.AddHours(8),
            UneseniKodDjeteta = dijeteId.ToString("D8"),
            StatusEvidencije = status,
            KodDjetetaIspravan = true,
            ValidanQRKod = true
        });
        db.SaveChanges();
    }
}
