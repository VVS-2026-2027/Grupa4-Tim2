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

        var primalacPostoji = await _baza.Roditelji.AnyAsync(roditelj => roditelj.Id == roditeljId);
        if (!primalacPostoji)
        {
            throw new InvalidOperationException("Roditelj kojem se šalje obavijest ne postoji.");
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

    public async Task<bool> OtkaziAsync(
        int odgajateljId,
        int najavaId,
        DateTime sadaUtc)
    {
        var obavijest = await _baza.ZakazaneObavijesti.SingleOrDefaultAsync(o =>
            o.Id == najavaId && o.OdgajateljId == odgajateljId);
        if (obavijest == null || obavijest.Status != StatusZakazaneObavijesti.CEKA)
        {
            return false;
        }

        if (KaoUtc(obavijest.VrijemeSlanjaUtc) <= KaoUtc(sadaUtc))
        {
            return false;
        }

        obavijest.Status = StatusZakazaneObavijesti.OTKAZANA;
        await _baza.SaveChangesAsync();
        return true;
    }

    private static DateTime KaoUtc(DateTime vrijednost) => vrijednost.Kind switch
    {
        DateTimeKind.Utc => vrijednost,
        DateTimeKind.Local => vrijednost.ToUniversalTime(),
        _ => DateTime.SpecifyKind(vrijednost, DateTimeKind.Utc)
    };
}
