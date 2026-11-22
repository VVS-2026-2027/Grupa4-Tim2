using EVrtic.Data;
using EVrtic.Models;
using Microsoft.EntityFrameworkCore;

namespace EVrtic.Services;

public class ZakazanaObavijestService
{
    private readonly ApplicationDbContext _baza;

    public ZakazanaObavijestService(ApplicationDbContext db)
    {
        _baza = db;
    }

    public async Task<ZakazanaObavijest> ZakaziAsync(
        int odgajateljId,
        int roditeljId,
        string naslov,
        string sadrzaj,
        DateTime vrijemeUtc,
        DateTime sadaUtc)
    {
        if (string.IsNullOrWhiteSpace(naslov))
        {
            throw new ArgumentException("Naslov obavijesti je obavezan.", nameof(naslov));
        }

        if (string.IsNullOrWhiteSpace(sadrzaj))
        {
            throw new ArgumentException("Sadržaj obavijesti je obavezan.", nameof(sadrzaj));
        }

        var vrijemeZaSlanjeUtc = KaoUtc(vrijemeUtc);
        var sadaZaPoredjenjeUtc = KaoUtc(sadaUtc);
        if (vrijemeZaSlanjeUtc <= sadaZaPoredjenjeUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(vrijemeUtc),
                "Vrijeme zakazanog slanja mora biti u budućnosti.");
        }

        var obavijest = new ZakazanaObavijest
        {
            OdgajateljId = odgajateljId,
            RoditeljId = roditeljId,
            Naslov = naslov.Trim(),
            Sadrzaj = sadrzaj.Trim(),
            VrijemeSlanjaUtc = vrijemeZaSlanjeUtc,
            Status = StatusZakazaneObavijesti.CEKA,
            BrojPokusaja = 0
        };

        _baza.ZakazaneObavijesti.Add(obavijest);
        await _baza.SaveChangesAsync();
        return obavijest;
    }

    public Task<bool> OtkaziAsync(
        int odgajateljId,
        int najavaId,
        DateTime sadaUtc)
    {
        throw new NotImplementedException();
    }

    private static DateTime KaoUtc(DateTime vrijednost) => vrijednost.Kind switch
    {
        DateTimeKind.Utc => vrijednost,
        DateTimeKind.Local => vrijednost.ToUniversalTime(),
        _ => DateTime.SpecifyKind(vrijednost, DateTimeKind.Utc)
    };
}
