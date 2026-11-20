using System.Text.Json;

namespace EVrtic.Tests;

public static class TestPodaci
{
    public static IEnumerable<object[]> IzCsv(string datoteka) =>
        File.ReadLines(Putanja(datoteka))
            .Skip(1)
            .Where(red => !string.IsNullOrWhiteSpace(red))
            .Select(red => red.Split(';').Cast<object>().ToArray());

    public static IEnumerable<object?[]> IzJsonNiza(string datoteka)
    {
        var vrijednosti = JsonSerializer.Deserialize<List<string?>>(File.ReadAllText(Putanja(datoteka)))!;
        return vrijednosti.Select(v => new object?[] { v });
    }

    private static string Putanja(string datoteka) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", datoteka);
}