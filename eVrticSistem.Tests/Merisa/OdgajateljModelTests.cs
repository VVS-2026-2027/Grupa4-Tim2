using System.ComponentModel.DataAnnotations;
using EVrtic.Models;

namespace eVrticSistem.Tests.Merisa;

[TestClass]
public class OdgajateljModelTests
{
    private static bool Validan(Odgajatelj model)
    {
        var context = new ValidationContext(model);
        var greske = new List<ValidationResult>();
        return Validator.TryValidateObject(model, context, greske, true);
    }

    [TestMethod]
    public void NoviModelInicijaliziraUloguOdgajatelja()
    {
        var model = new Odgajatelj();

        Assert.AreEqual(Uloga.ODGAJATELJ, model.Uloga);
        Assert.AreEqual(StatusNaloga.AKTIVAN, model.StatusNaloga);
        Assert.AreEqual(string.Empty, model.ImePrezime);
        Assert.AreEqual(0, model.Grupe.Count);
    }

    [TestMethod]
    public void IspravanOdgajateljProlaziValidaciju()
    {
        var model = new Odgajatelj
        {
            ImePrezime = "Amina Čengić",
            Email = "amina@example.test",
            StatusNaloga = StatusNaloga.AKTIVAN
        };

        Assert.IsTrue(Validan(model));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void PraznoImePrezimeNeProlaziValidaciju(string? imePrezime)
    {
        var model = new Odgajatelj
        {
            ImePrezime = imePrezime!
        };

        Assert.IsFalse(Validan(model));
    }

    [TestMethod]
    public void ImePrezimeSaTacno100KarakteraProlaziValidaciju()
    {
        var model = new Odgajatelj
        {
            ImePrezime = new string('a', 100)
        };

        Assert.IsTrue(Validan(model));
    }

    [TestMethod]
    public void ImePrezimeSaViseOd100KarakteraNeProlaziValidaciju()
    {
        var model = new Odgajatelj
        {
            ImePrezime = new string('a', 101)
        };

        Assert.IsFalse(Validan(model));
    }

    [TestMethod]
    public void SvojstvaOdgajateljaSeIspravnoPostavljaju()
    {
        var model = new Odgajatelj
        {
            Id = 12,
            ImePrezime = "Merisa Hadžić",
            Email = "merisa@example.test",
            StatusNaloga = StatusNaloga.DEAKTIVIRAN
        };

        Assert.AreEqual(12, model.Id);
        Assert.AreEqual("Merisa Hadžić", model.ImePrezime);
        Assert.AreEqual("merisa@example.test", model.Email);
        Assert.AreEqual(StatusNaloga.DEAKTIVIRAN, model.StatusNaloga);
        Assert.AreEqual(Uloga.ODGAJATELJ, model.Uloga);
    }

    [TestMethod]
    public void ListaGrupaSeMozePopuniti()
    {
        var model = new Odgajatelj { ImePrezime = "Amina Čengić" };
        var grupa = new Grupa { Id = 3, ImeGrupe = "Leptirići" };

        model.Grupe.Add(grupa);

        Assert.AreEqual(1, model.Grupe.Count);
        Assert.AreSame(grupa, model.Grupe.Single());
    }

    [TestMethod]
    public void JednaListaMozeSadrzavatiViseGrupa()
    {
        var model = new Odgajatelj { ImePrezime = "Amina Čengić" };

        model.Grupe.Add(new Grupa { Id = 1, ImeGrupe = "Cvjetići" });
        model.Grupe.Add(new Grupa { Id = 2, ImeGrupe = "Zvezdice" });

        Assert.AreEqual(2, model.Grupe.Count);
        Assert.AreEqual("Cvjetići", model.Grupe.ElementAt(0).ImeGrupe);
        Assert.AreEqual("Zvezdice", model.Grupe.ElementAt(1).ImeGrupe);
    }
}
