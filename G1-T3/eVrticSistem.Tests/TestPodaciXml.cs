using System.Xml.Linq;

namespace EVrtic.Tests;

public static class TestPodaciXml
{
    public static IEnumerable<object[]> HistorijaKodova()
    {
        var putanja = Path.Combine(AppContext.BaseDirectory, "TestData", "historija-kodova.xml");

        return XDocument.Load(putanja)
            .Descendants("slucaj")
            .Select(s => new object[]
            {
                (int)s.Element("ukupnoKodova")!,
                (int)s.Element("ocekivanoUHistoriji")!
            });
    }
}