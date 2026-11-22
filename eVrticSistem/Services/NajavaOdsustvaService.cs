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

        // Preklapanje je zabranjeno samo za aktivne najave istog djeteta.
        // Granični datumi ulaze u period, zbog čega se koriste <= i >=.
        var preklapaSe = await _baza.NajaveOdsustva.AnyAsync(najava =>
            najava.DijeteId == dijeteId &&
            najava.Status == StatusNajaveOdsustva.AKTIVNA &&
            najava.DatumOd <= kraj &&
            najava.DatumDo >= pocetak);

        if (preklapaSe)
        {
            throw new InvalidOperationException("Dijete već ima aktivnu najavu u odabranom periodu.");
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

    public async Task<bool> OtkaziAsync(int roditeljId, int najavaId, DateTime danas)
    {
        var najava = await _baza.NajaveOdsustva.SingleOrDefaultAsync(n =>
            n.Id == najavaId && n.RoditeljId == roditeljId);

        // Nepostojeća/tuđa najava, već otkazana najava ili započeto odsustvo.
        if (najava == null ||
            najava.Status != StatusNajaveOdsustva.AKTIVNA ||
            najava.DatumOd.Date <= danas.Date)
        {
            return false;
        }

        najava.Status = StatusNajaveOdsustva.OTKAZANA;
        await _baza.SaveChangesAsync();
        return true;
    }

    public async Task<List<NajavaOdsustva>> PregledZaGrupuAsync(
        int odgajateljId, int grupaId, DateTime od, DateTime doDatuma)
    {
        var pocetak = od.Date;
        var kraj = doDatuma.Date;

        if (pocetak > kraj)
        {
            throw new ArgumentException("Datum početka mora biti prije ili na datum završetka.", nameof(od));
        }

        var vodiGrupu = await _baza.Grupe.AnyAsync(grupa =>
            grupa.Id == grupaId && grupa.OdgajateljId == odgajateljId);

        if (!vodiGrupu)
        {
            throw new UnauthorizedAccessException("Odgajatelj nema pristup odabranoj grupi.");
        }

        // Povezujemo najavu s djetetom, kako bi prikaz uključio samo djecu
        // odabrane grupe, aktivne najave i one koje presijecaju traženi period.
        return await (
            from najava in _baza.NajaveOdsustva.AsNoTracking()
            join dijete in _baza.Djeca.AsNoTracking() on najava.DijeteId equals dijete.Id
            where dijete.GrupaId == grupaId
               && najava.Status == StatusNajaveOdsustva.AKTIVNA
               && najava.DatumOd <= kraj
               && najava.DatumDo >= pocetak
            orderby najava.DatumOd, najava.Id
            select najava
        ).ToListAsync();
    }
}
