using EVrtic.Data;

namespace EVrtic.Services;

public class ObradaZakazanihObavijestiService
{
    public ObradaZakazanihObavijestiService(ApplicationDbContext db, IEmailService emailService)
    {
    }

    public Task<int> ObradiDospjeleAsync(DateTime sadaUtc)
    {
        throw new NotImplementedException();
    }
}
