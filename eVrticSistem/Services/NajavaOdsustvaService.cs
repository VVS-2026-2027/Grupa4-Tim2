using EVrtic.Data;
using EVrtic.Models;

namespace EVrtic.Services;

public class NajavaOdsustvaService
{
    private readonly ApplicationDbContext _baza;

    public NajavaOdsustvaService(ApplicationDbContext baza)
    {
        _baza = baza;
    }

    public Task<NajavaOdsustva> KreirajAsync(
        int roditeljId, int dijeteId, DateTime od, DateTime doDatuma, string razlog, DateTime danas)
    {
        throw new NotImplementedException();
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
