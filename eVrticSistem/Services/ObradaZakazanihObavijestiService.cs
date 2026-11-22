using EVrtic.Data;
using EVrtic.Models;
using Microsoft.EntityFrameworkCore;

namespace EVrtic.Services;

public class ObradaZakazanihObavijestiService
{
    private readonly ApplicationDbContext _baza;
    private readonly IEmailService _emailService;

    public ObradaZakazanihObavijestiService(ApplicationDbContext db, IEmailService emailService)
    {
        _baza = db;
        _emailService = emailService;
    }

    public async Task<int> ObradiDospjeleAsync(DateTime sadaUtc)
    {
        var sadaZaPoredjenjeUtc = KaoUtc(sadaUtc);
        var dospjele = await _baza.ZakazaneObavijesti
            .Where(o =>
                o.Status == StatusZakazaneObavijesti.CEKA &&
                o.VrijemeSlanjaUtc <= sadaZaPoredjenjeUtc)
            .ToListAsync();

        var brojUspjesnih = 0;

        foreach (var obavijest in dospjele)
        {
            obavijest.BrojPokusaja++;

            var primalac = await _baza.Roditelji
                .SingleOrDefaultAsync(r => r.Id == obavijest.RoditeljId);

            if (primalac == null || string.IsNullOrWhiteSpace(primalac.Email))
            {
                obavijest.Status = StatusZakazaneObavijesti.NEUSPJELA;
                obavijest.Greska = "Email adresa primaoca nije dostupna.";
                await _baza.SaveChangesAsync();
                continue;
            }

            var uspjeh = await _emailService.SendAsync(
                primalac.Email.Trim(),
                obavijest.Naslov,
                obavijest.Sadrzaj);

            if (uspjeh)
            {
                obavijest.Status = StatusZakazaneObavijesti.POSLANA;
                obavijest.DatumSlanjaUtc = sadaZaPoredjenjeUtc;
                obavijest.Greska = null;
                brojUspjesnih++;
            }
            else
            {
                obavijest.Status = StatusZakazaneObavijesti.NEUSPJELA;
                obavijest.Greska = "Slanje email poruke nije uspjelo.";
            }

            await _baza.SaveChangesAsync();
        }

        return brojUspjesnih;
    }

    private static DateTime KaoUtc(DateTime vrijednost) => vrijednost.Kind switch
    {
        DateTimeKind.Utc => vrijednost,
        DateTimeKind.Local => vrijednost.ToUniversalTime(),
        _ => DateTime.SpecifyKind(vrijednost, DateTimeKind.Utc)
    };
}
