using System.IO;
using System.Text.Json;
using EVrtic.Controllers;
using EVrtic.Data;
using EVrtic.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eVrticSistem.Tests.Merisa;

[TestClass]
public class BolestDjetetaControllerTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    public class BolestTestData
    {
        public int Id { get; set; }
        public bool Exists { get; set; }
    }

    public static IEnumerable<object[]> GetIdsFromCsv()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Podaci", "Merisa", "bolesti.csv");

        foreach (var line in File.ReadLines(path))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return new object[] { int.Parse(line) };
            }
        }
    }

    public static IEnumerable<object[]> GetDeleteDataFromJson()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Podaci", "Merisa", "bolesti.json");
        var json = File.ReadAllText(path);
        var podaci = JsonSerializer.Deserialize<List<BolestTestData>>(json)
            ?? throw new InvalidDataException("JSON testni podaci za bolesti su prazni ili neispravni.");

        return podaci.Select(podatak => new object[] { podatak });
    }

    [TestMethod]
    public async Task IndexVracaForbid()
    {
        using var context = CreateContext();
        var controller = new BolestDjetetaController(context);

        var result = await controller.Index();

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [DataTestMethod]
    [DynamicData(nameof(GetIdsFromCsv), DynamicDataSourceType.Method)]
    public async Task DetailsVracaForbidZaRazliciteIdVrijednosti(int id)
    {
        using var context = CreateContext();
        var controller = new BolestDjetetaController(context);

        var result = await controller.Details(id);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task DetailsNullIdVracaForbid()
    {
        using var context = CreateContext();
        var controller = new BolestDjetetaController(context);

        var result = await controller.Details(null);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public void CreateGetVracaForbid()
    {
        using var context = CreateContext();
        var controller = new BolestDjetetaController(context);

        var result = controller.Create();

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task CreatePostVracaForbid()
    {
        using var context = CreateContext();
        var controller = new BolestDjetetaController(context);
        var bolest = new BolestDjeteta { Naziv = "Astma", DijeteId = 1 };

        var result = await controller.Create(bolest);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [DataTestMethod]
    [DynamicData(nameof(GetIdsFromCsv), DynamicDataSourceType.Method)]
    public async Task EditGetVracaForbidZaRazliciteIdVrijednosti(int id)
    {
        using var context = CreateContext();
        var controller = new BolestDjetetaController(context);

        var result = await controller.Edit(id);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task EditGetNullIdVracaForbid()
    {
        using var context = CreateContext();
        var controller = new BolestDjetetaController(context);

        var result = await controller.Edit(null);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task EditPostVracaForbid()
    {
        using var context = CreateContext();
        var controller = new BolestDjetetaController(context);
        var bolest = new BolestDjeteta { Id = 1, Naziv = "Astma", DijeteId = 1 };

        var result = await controller.Edit(1, bolest);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [DataTestMethod]
    [DynamicData(nameof(GetIdsFromCsv), DynamicDataSourceType.Method)]
    public async Task DeleteGetVracaForbidZaRazliciteIdVrijednosti(int id)
    {
        using var context = CreateContext();
        var controller = new BolestDjetetaController(context);

        var result = await controller.Delete(id);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }

    [TestMethod]
    public async Task DeleteGetNullIdVracaForbid()
    {
        using var context = CreateContext();
        var controller = new BolestDjetetaController(context);

        var result = await controller.Delete(null);

        Assert.IsInstanceOfType<ForbidResult>(result);
    }


    [TestMethod]
    public async Task BolestDjetetaExistsVracaTrueAkoBolestPostoji()
    {
        using var context = CreateContext();

        context.BolestiDjece.Add(new BolestDjeteta
        {
            Id = 1,
            Naziv = "Astma",
            DijeteId = 1
        });

        await context.SaveChangesAsync();

        var controller = new BolestDjetetaController(context);

        var metoda = typeof(BolestDjetetaController)
            .GetMethod(
                "BolestDjetetaExists",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance
            );

        Assert.IsNotNull(metoda);

        var rezultat = metoda.Invoke(controller, new object[] { 1 });

        Assert.AreEqual(true, rezultat);
    }

    [TestMethod]
    public async Task BolestDjetetaExistsVracaFalseAkoBolestNePostoji()
    {
        using var context = CreateContext();

        var controller = new BolestDjetetaController(context);

        var metoda = typeof(BolestDjetetaController)
            .GetMethod(
                "BolestDjetetaExists",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance
            );

        Assert.IsNotNull(metoda);

        var rezultat = metoda.Invoke(controller, new object[] { 100 });

        Assert.AreEqual(false, rezultat);
    }


    [DataTestMethod]
    [DynamicData(nameof(GetDeleteDataFromJson), DynamicDataSourceType.Method)]
    public async Task DeleteConfirmedPreusmjeravaNaIndexIBrisePostojecuBolest(BolestTestData podatak)
    {
        using var context = CreateContext();

        if (podatak.Exists)
        {
            context.BolestiDjece.Add(new BolestDjeteta
            {
                Id = podatak.Id,
                Naziv = "Testna bolest",
                DijeteId = 1
            });
            await context.SaveChangesAsync();
        }

        var controller = new BolestDjetetaController(context);

        var result = await controller.DeleteConfirmed(podatak.Id);

        var redirectResult = Assert.IsInstanceOfType<RedirectToActionResult>(result);
        Assert.AreEqual(nameof(BolestDjetetaController.Index), redirectResult.ActionName);
        Assert.IsFalse(await context.BolestiDjece.AnyAsync(b => b.Id == podatak.Id));
    }
}
