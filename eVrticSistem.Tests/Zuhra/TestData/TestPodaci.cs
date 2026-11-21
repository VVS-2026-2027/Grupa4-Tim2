
using System.Text.Json;

namespace EVrtic.Tests;

public static class TestPodaci
{
    public static IEnumerable<object[]> IzCsv(string datoteka)
    {
        string putanja = Putanja(datoteka);

        if (!File.Exists(putanja))
            throw new FileNotFoundException(
                $"CSV datoteka nije pronađena: {putanja}");

        return File.ReadLines(putanja)
            .Skip(1)
            .Where(red => !string.IsNullOrWhiteSpace(red))
            .Select((red, indeks) =>
            {
                string[] vrijednosti = red.Split(';');

                if (vrijednosti.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidDataException(
                        $"Neispravan CSV red {indeks + 2} u datoteci {datoteka}.");

                return vrijednosti.Cast<object>().ToArray();
            });
    }

    public static IEnumerable<object?[]> IzJsonNiza(string datoteka)
    {
        string putanja = Putanja(datoteka);

        if (!File.Exists(putanja))
            throw new FileNotFoundException(
                $"JSON datoteka nije pronađena: {putanja}");

        var vrijednosti =
            JsonSerializer.Deserialize<List<string?>>(
                File.ReadAllText(putanja));

        if (vrijednosti is null)
            throw new InvalidDataException(
                $"JSON datoteka {datoteka} ne sadrži ispravan niz.");

        return vrijednosti
            .Select(v => new object?[] { v })
            .ToArray();
    }

    // Testni podaci za roditelje koji nemaju ispravnu email adresu.
    public static IEnumerable<object?[]> PrazneEmailAdrese()
    {
        return new[]
        {
            new object?[] { null },
            new object?[] { "" },
            new object?[] { "   " }
        };
    }

    private static string Putanja(string datoteka)
    {
        if (string.IsNullOrWhiteSpace(datoteka))
            throw new ArgumentException(
                "Naziv datoteke ne smije biti prazan.",
                nameof(datoteka));

        return Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            datoteka);
    }
}
