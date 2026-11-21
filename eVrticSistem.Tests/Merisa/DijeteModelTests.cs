using System.ComponentModel.DataAnnotations;
using EVrtic.Models;

namespace eVrticSistem.Tests.Merisa;

[TestClass]
public class DijeteModelTests
{
    private static bool Validan(Dijete model)
    {
        var context = new ValidationContext(model);
        var greske = new List<ValidationResult>();
        return Validator.TryValidateObject(model, context, greske, true);
    }

    [TestMethod]
    public void NoviModelInicijaliziraPodrazumijevanaSvojstva()
    {
        var model = new Dijete();

        Assert.AreEqual(string.Empty, model.ImePrezime);
        Assert.AreEqual(string.Empty, model.IdentifikacioniKod);
        Assert.AreEqual(string.Empty, model.DodatnaNapomena);
        Assert.AreEqual(default(DateTime), model.DatumRodjenja);
        Assert.IsTrue(model.Aktivno);
        Assert.IsNull(model.GrupaId);
        Assert.IsNull(model.RoditeljId);
        Assert.IsNull(model.Grupa);
        Assert.IsNull(model.Roditelj);
    }

    [TestMethod]
    public void NoviModelInicijaliziraSveListe()
    {
        var model = new Dijete();

        Assert.AreEqual(0, model.Alergije.Count);
        Assert.AreEqual(0, model.Bolesti.Count);
        Assert.AreEqual(0, model.DnevniIzvjestaji.Count);
        Assert.AreEqual(0, model.EvidencijeDolaskaOdlaska.Count);
        Assert.AreEqual(0, model.SazeciAktivnosti.Count);
    }

    [TestMethod]
    public void IspravanModelProlaziValidaciju()
    {
        var model = new Dijete
        {
            ImePrezime = "Amina Čengić",
            IdentifikacioniKod = "ABCD1234",
            DodatnaNapomena = "Nema posebnih napomena",
            DatumRodjenja = new DateTime(2020, 5, 10)
        };

        Assert.IsTrue(Validan(model));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void PraznoImePrezimeNeProlaziValidaciju(string? imePrezime)
    {
        var model = new Dijete
        {
            ImePrezime = imePrezime!,
            IdentifikacioniKod = "ABCD1234",
            DatumRodjenja = new DateTime(2020, 5, 10)
        };

        Assert.IsFalse(Validan(model));
    }

    [TestMethod]
    public void ImePrezimeSaViseOd100KarakteraNeProlaziValidaciju()
    {
        var model = new Dijete
        {
            ImePrezime = new string('a', 101),
            IdentifikacioniKod = "ABCD1234"
        };

        Assert.IsFalse(Validan(model));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("ABCD123")]
    [DataRow("ABCD12345")]
    [DataRow(null)]
    public void NeispravnaDuzinaIdentifikacionogKodaNeProlaziValidaciju(string? kod)
    {
        var model = new Dijete
        {
            ImePrezime = "Amina Čengić",
            IdentifikacioniKod = kod!
        };

        Assert.IsFalse(Validan(model));
    }

    [TestMethod]
    public void IdentifikacioniKodTacnoOsamKarakteraProlaziValidaciju()
    {
        var model = new Dijete
        {
            ImePrezime = "Amina Čengić",
            IdentifikacioniKod = "ABCD1234"
        };

        Assert.IsTrue(Validan(model));
    }

    [TestMethod]
    public void DodatnaNapomenaPreko500KarakteraNeProlaziValidaciju()
    {
        var model = new Dijete
        {
            ImePrezime = "Amina Čengić",
            IdentifikacioniKod = "ABCD1234",
            DodatnaNapomena = new string('x', 501)
        };

        Assert.IsFalse(Validan(model));
    }

    [TestMethod]
    public void SveNavigacioneListeMoguSePopuniti()
    {
        var dijete = new Dijete();
        var alergija = new AlergijaDjeteta { Naziv = "Kikiriki" };
        var bolest = new BolestDjeteta { Naziv = "Astma" };

        dijete.Alergije.Add(alergija);
        dijete.Bolesti.Add(bolest);

        Assert.AreEqual(1, dijete.Alergije.Count);
        Assert.AreSame(alergija, dijete.Alergije.Single());
        Assert.AreEqual(1, dijete.Bolesti.Count);
        Assert.AreSame(bolest, dijete.Bolesti.Single());
    }

    [TestMethod]
    public void SvojstvaSePovezujuSaGrupomIRoditeljom()
    {
        var grupa = new Grupa { Id = 2 };
        var roditelj = new Roditelj { Id = 4 };
        var dijete = new Dijete
        {
            Id = 7,
            ImePrezime = "Lejla Hasanović",
            IdentifikacioniKod = "LH123456",
            DatumRodjenja = new DateTime(2021, 3, 12),
            Aktivno = false,
            GrupaId = grupa.Id,
            Grupa = grupa,
            RoditeljId = roditelj.Id,
            Roditelj = roditelj
        };

        Assert.AreEqual(7, dijete.Id);
        Assert.AreEqual("Lejla Hasanović", dijete.ImePrezime);
        Assert.AreEqual("LH123456", dijete.IdentifikacioniKod);
        Assert.AreEqual(new DateTime(2021, 3, 12), dijete.DatumRodjenja);
        Assert.IsFalse(dijete.Aktivno);
        Assert.AreEqual(2, dijete.GrupaId);
        Assert.AreSame(grupa, dijete.Grupa);
        Assert.AreEqual(4, dijete.RoditeljId);
        Assert.AreSame(roditelj, dijete.Roditelj);
    }
}
