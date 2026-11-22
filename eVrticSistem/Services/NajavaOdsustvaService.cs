using EVrtic.Data;
using EVrtic.Models;
using Microsoft.EntityFrameworkCore;

namespace EVrtic.Services;

public class NajavaOdsustvaService
{
    private readonly ApplicationDbContext _baza;

    public NajavaOdsustvaService(ApplicationDbContext baza)
    {
        _baza = baza;
    }

    public async Task<NajavaOdsustva> KreirajAsync(
        int roditeljId, int dijeteId, DateTime od, DateTime doDatuma, string razlog, DateTime danas)
    {
        var pocetak = od.Date;
        var kraj = doDatuma.Date;

        if (pocetak > kraj)
        {
            throw new ArgumentException("Datum početka mora biti prije ili na datum završetka.", nameof(od));
        }

        if (pocetak < danas.Date)
        {
            throw new ArgumentException("Datum početka ne može biti u prošlosti.", nameof(od));
        }

        var dijetePripadaRoditelju = await _baza.Djeca.AnyAsync(dijete =>
            dijete.Id == dijeteId && dijete.RoditeljId == roditeljId);

        if (!dijetePripadaRoditelju)
        {
            throw new UnauthorizedAccessException("Roditelj može najaviti odsustvo samo za svoje dijete.");
        }

        var najava = new NajavaOdsustva
        {
            RoditeljId = roditeljId,
            DijeteId = dijeteId,
            DatumOd = pocetak,
            DatumDo = kraj,
            Razlog = razlog,
            Status = StatusNajaveOdsustva.AKTIVNA,
            DatumKreiranja = danas
        };

        _baza.NajaveOdsustva.Add(najava);
        await _baza.SaveChangesAsync();
        return najava;
    }

    public Task<bool> OtkaziAsync(int roditeljId, int najavaId, DateTime danas)
    {
        throw new NotImplementedException();
    }

    public Task<List<NajavaOdsustva>> PregledZaGrupuAsync(
        int odgajateljId, int grupaId, DateTime od, DateTime doDatuma)
    {
        throw new NotImplementedException();
    }
}
