using System.ComponentModel.DataAnnotations;
using EVrtic.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eVrticSistem.Tests.Merisa;

[TestClass]
public class AlergijaDjetetaModelTests
{
    private static bool Validan(AlergijaDjeteta model)
    {
        var context = new ValidationContext(model);
        var greske = new List<ValidationResult>();
        return Validator.TryValidateObject(model, context, greske, true);
    }

    [TestMethod]
    public void NoviModelInicijaliziraNazivKaPraznomTekstu()
    {
        var model = new AlergijaDjeteta();

        Assert.AreEqual(string.Empty, model.Naziv);
        Assert.AreEqual(0, model.Id);
        Assert.AreEqual(0, model.DijeteId);
        Assert.IsNull(model.Dijete);
    }

    [TestMethod]
    public void NazivSaTacno100KarakteraProlaziValidaciju()
    {
        var model = new AlergijaDjeteta
        {
            Naziv = new string('a', 100)
        };

        Assert.IsTrue(Validan(model));
    }

    [TestMethod]
    public void NazivSaViseOd100KarakteraNeProlaziValidaciju()
    {
        var model = new AlergijaDjeteta
        {
            Naziv = new string('a', 101)
        };

        Assert.IsFalse(Validan(model));
    }

    [TestMethod]
    public void PrazanNazivNeProlaziValidaciju()
    {
        var model = new AlergijaDjeteta
        {
            Naziv = string.Empty
        };

        Assert.IsFalse(Validan(model));
    }

    [TestMethod]
    public void SvojstvaSePovezujuSaDjetetom()
    {
        var dijete = new Dijete { Id = 7 };
        var model = new AlergijaDjeteta
        {
            Id = 3,
            Naziv = "Kikiriki",
            DijeteId = dijete.Id,
            Dijete = dijete
        };

        Assert.AreEqual(3, model.Id);
        Assert.AreEqual("Kikiriki", model.Naziv);
        Assert.AreEqual(dijete.Id, model.DijeteId);
        Assert.AreSame(dijete, model.Dijete);
    }
}
