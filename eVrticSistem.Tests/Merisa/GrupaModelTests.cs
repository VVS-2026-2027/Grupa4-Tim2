using System.ComponentModel.DataAnnotations;
using EVrtic.Models;

namespace eVrticSistem.Tests.Merisa;

[TestClass]
public class GrupaModelTests
{
    private static bool Validan(Grupa model)
    {
        var context = new ValidationContext(model);
        var greske = new List<ValidationResult>();
        return Validator.TryValidateObject(model, context, greske, true);
    }

    [TestMethod]
    public void NoviModelInicijaliziraPodrazumijevanaSvojstva()
    {
        var model = new Grupa();

        Assert.AreEqual(0, model.Id);
        Assert.AreEqual(string.Empty, model.ImeGrupe);
        Assert.IsNull(model.OdgajateljId);
        Assert.IsNull(model.Odgajatelj);
        Assert.AreEqual(0, model.Djeca.Count);
    }

    [TestMethod]
    public void IspravanNazivGrupeProlaziValidaciju()
    {
        var model = new Grupa { ImeGrupe = "Leptirići" };

        Assert.IsTrue(Validan(model));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void PrazanNazivGrupeNeProlaziValidaciju(string? naziv)
    {
        var model = new Grupa { ImeGrupe = naziv! };

        Assert.IsFalse(Validan(model));
    }

    [TestMethod]
    public void NazivGrupeSaTacno100KarakteraProlaziValidaciju()
    {
        var model = new Grupa { ImeGrupe = new string('a', 100) };

        Assert.IsTrue(Validan(model));
    }

    [TestMethod]
    public void NazivGrupeSaViseOd100KarakteraNeProlaziValidaciju()
    {
        var model = new Grupa { ImeGrupe = new string('a', 101) };

        Assert.IsFalse(Validan(model));
    }

    [TestMethod]
    public void SvojstvaSePovezujuSaOdgajateljem()
    {
        var odgajatelj = new Odgajatelj { Id = 5 };
        var model = new Grupa
        {
            Id = 2,
            ImeGrupe = "Cvjetići",
            OdgajateljId = odgajatelj.Id,
            Odgajatelj = odgajatelj
        };

        Assert.AreEqual(2, model.Id);
        Assert.AreEqual("Cvjetići", model.ImeGrupe);
        Assert.AreEqual(5, model.OdgajateljId);
        Assert.AreSame(odgajatelj, model.Odgajatelj);
    }

    [TestMethod]
    public void ListaDjeceSeMoraPopuniti()
    {
        var model = new Grupa { ImeGrupe = "Zvezdice" };
        var dijete = new Dijete
        {
            Id = 7,
            ImePrezime = "Amina Čengić",
            IdentifikacioniKod = "ABCD1234"
        };

        model.Djeca.Add(dijete);

        Assert.AreEqual(1, model.Djeca.Count);
        Assert.AreSame(dijete, model.Djeca.Single());
    }

    [TestMethod]
    public void JednaListaMozeSadrzavatiViseDjece()
    {
        var model = new Grupa { ImeGrupe = "Suncokreti" };

        model.Djeca.Add(new Dijete { Id = 1, ImePrezime = "Prvo Dijete", IdentifikacioniKod = "D0000001" });
        model.Djeca.Add(new Dijete { Id = 2, ImePrezime = "Drugo Dijete", IdentifikacioniKod = "D0000002" });

        Assert.AreEqual(2, model.Djeca.Count);
        Assert.AreEqual("Prvo Dijete", model.Djeca.ElementAt(0).ImePrezime);
        Assert.AreEqual("Drugo Dijete", model.Djeca.ElementAt(1).ImePrezime);
    }
}
