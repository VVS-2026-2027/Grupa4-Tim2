using EVrtic.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eVrticSistem.Tests.Sureja;

[TestClass]
public class EmailServiceTests
{
    private const string Primalac = "amra@example.test";
    private const string Naslov = "Testna poruka";
    private const string Sadrzaj = "Sadržaj testne poruke";

    private static IConfiguration _praznaKonfiguracija = null!;

    [ClassInitialize]
    public static void PripremiKonfiguraciju(TestContext context)
    {
        _praznaKonfiguracija = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("amra-bez-adrese")]
    public async Task SendAsyncNeispravnaAdresaVracaFalse(string adresa)
    {
        var servis = NapraviServis(_praznaKonfiguracija);

        var rezultat = await servis.SendAsync(adresa, Naslov, Sadrzaj);

        Assert.IsFalse(rezultat);
    }

    [TestMethod]
    public async Task SendAsyncBezSmtpPostavkiVracaFalse()
    {
        var servis = NapraviServis(_praznaKonfiguracija);

        var rezultat = await servis.SendAsync(Primalac, Naslov, Sadrzaj);

        Assert.IsFalse(rezultat);
    }

    [TestMethod]
    public async Task SendAsyncNeispravanPortVracaFalse()
    {
        var konfiguracija = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailSettings:SmtpPort"] = "neispravan"
            })
            .Build();

        var servis = NapraviServis(konfiguracija);

        var rezultat = await servis.SendAsync(Primalac, Naslov, Sadrzaj);

        Assert.IsFalse(rezultat);
    }

    [TestMethod]
    public async Task SendAsyncNeispravanPosiljalacVracaFalse()
    {
        var konfiguracija = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailSettings:SmtpServer"] = "localhost",
                ["EmailSettings:SenderEmail"] = "nije-email",
                ["EmailSettings:SenderPassword"] = "testna-vrijednost"
            })
            .Build();

        var servis = NapraviServis(konfiguracija);

        var rezultat = await servis.SendAsync(Primalac, Naslov, Sadrzaj);

        Assert.IsFalse(rezultat);
    }

    private static EmailService NapraviServis(IConfiguration konfiguracija)
    {
        return new EmailService(
            konfiguracija, NullLogger<EmailService>.Instance);
    }
}