using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using EVrtic.Controllers;
using EVrtic.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EVrtic.Tests.Belma;

[TestClass]
public class ModeliIzvjestajaTests
{
    private static bool Validan(object model)
    {
        var context = new ValidationContext(model);
        var greske = new List<ValidationResult>();
        return Validator.TryValidateObject(model, context, greske, true);
    }

    [TestMethod]
    public void DnevniIzvjestaj_NoviModel_InicijalizujeTekstualnaPolja()
    {
        var model = new DnevniIzvjestaj();
        Assert.AreEqual(string.Empty, model.Dorucak);
        Assert.AreEqual(string.Empty, model.Rucak);
        Assert.AreEqual(string.Empty, model.NapomenaAktivnosti);
        Assert.AreEqual(0, model.SpavanjeMinuta);
        Assert.IsNull(model.VrijemeDolaska);
        Assert.IsNull(model.VrijemeOdlaska);
    }

    [TestMethod]
    public void DnevniIzvjestaj_PredugDorucak_NeProlaziValidaciju()
    {
        Assert.IsFalse(Validan(new DnevniIzvjestaj { Dorucak = new string('x', 201) }));
    }

    [TestMethod]
    public void DnevniIzvjestaj_PredugaNapomena_NeProlaziValidaciju()
    {
        Assert.IsFalse(Validan(new DnevniIzvjestaj { NapomenaAktivnosti = new string('x', 1001) }));
    }

    [TestMethod]
    public void DnevniIzvjestaj_GranicneDuzine_TacnoDozvoljene()
    {
        Assert.IsTrue(Validan(new DnevniIzvjestaj {
            Dorucak = new string('a', 200), Rucak = new string('b', 200),
            NapomenaAktivnosti = new string('n', 1000) }));
    }

    [TestMethod]
    public void SazetakAktivnosti_NoviModel_InicijalizujeNapomenu()
    {
        var model = new SazetakAktivnosti();
        Assert.AreEqual(string.Empty, model.OsnovneNapomene);
        Assert.AreEqual(0, model.BrojDolazaka);
        Assert.AreEqual(0, model.AgregiranoSpavanjeMinuta);
    }

    [TestMethod]
    public void SazetakAktivnosti_PredugeNapomene_NeProlazeValidaciju()
    {
        Assert.IsFalse(Validan(new SazetakAktivnosti { OsnovneNapomene = new string('x', 2001) }));
    }

    [TestMethod]
    public void SazetakAktivnosti_GranicneDuzine_ProlazeValidaciju()
    {
        Assert.IsTrue(Validan(new SazetakAktivnosti { OsnovneNapomene = new string('x', 2000) }));
    }

    [TestMethod]
    public void ViewModeli_NoviModeli_InicijalizujuListe()
    {
        var roditelj = new RoditeljDnevniIzvjestajViewModel();
        var unos = new EvidencijaAktivnostiViewModel();
        var sedmica = new RoditeljSazetakPregledViewModel();
        var odgajatelj = new OdgajateljSazetakPregledViewModel();
        Assert.AreEqual(0, roditelj.Djeca.Count);
        Assert.AreEqual(0, unos.Djeca.Count);
        Assert.AreEqual(-1, unos.StatusDoruckaVal);
        Assert.AreEqual(0, sedmica.SedmicneOpcije.Count);
        Assert.AreEqual(0, odgajatelj.SazeciDjece.Count);
    }
}
