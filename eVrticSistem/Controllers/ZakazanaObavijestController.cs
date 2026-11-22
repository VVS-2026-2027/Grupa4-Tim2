using EVrtic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVrtic.Controllers;

[Authorize(Roles = "ADMINISTRATOR")]
public class ZakazanaObavijestController : Controller
{
    private readonly ObradaZakazanihObavijestiService _obrada;

    public ZakazanaObavijestController(ObradaZakazanihObavijestiService obrada)
    {
        _obrada = obrada;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ObradiDospjele()
    {
        var brojUspjesnih = await _obrada.ObradiDospjeleAsync(DateTime.UtcNow);
        TempData["Uspjeh"] = $"Obradeno uspješnih slanja: {brojUspjesnih}.";
        return RedirectToAction("Index", "Home");
    }
}
