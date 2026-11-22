using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using EVrtic.Data;
using EVrtic.Models;
using EVrtic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EVrtic.Controllers;

public class NajavaOdsustvaController : Controller
{
    private readonly ApplicationDbContext _baza;
    private readonly NajavaOdsustvaService _servis;
    private readonly UserManager<Korisnik> _userManager;

    public NajavaOdsustvaController(
        ApplicationDbContext baza,
        NajavaOdsustvaService servis,
        UserManager<Korisnik> userManager)
    {
        _baza = baza;
        _servis = servis;
        _userManager = userManager;
    }

    // Roditelj bira samo između svoje djece.
    private async Task PopuniDjecuAsync(KreirajNajavuViewModel model, int roditeljId)
    {
        var djeca = await _baza.Djeca.AsNoTracking()
            .Where(d => d.RoditeljId == roditeljId)
            .OrderBy(d => d.ImePrezime)
            .Select(d => new { d.Id, d.ImePrezime })
            .ToListAsync();

        model.Djeca = djeca.Select(d => new SelectListItem
        {
            Value = d.Id.ToString(), Text = d.ImePrezime
        }).ToList();
    }

    [Authorize(Roles = "RODITELJ")]
    [HttpGet]
    public async Task<IActionResult> Kreiraj()
    {
        var korisnik = await _userManager.GetUserAsync(User);
        if (korisnik == null) return Challenge();

        var model = new KreirajNajavuViewModel();
        await PopuniDjecuAsync(model, korisnik.Id);
        return View(model);
    }

    [Authorize(Roles = "RODITELJ")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Kreiraj(KreirajNajavuViewModel model)
    {
        var korisnik = await _userManager.GetUserAsync(User);
        if (korisnik == null) return Challenge();

        // RoditeljId se nikad ne preuzima iz forme.
        if (ModelState.IsValid)
        {
            try
            {
                await _servis.KreirajAsync(
                    korisnik.Id, model.DijeteId, model.DatumOd, model.DatumDo,
                    model.Razlog.Trim(), DateTime.Today);
                TempData["Uspjeh"] = "Najava odsustva je uspješno sačuvana.";
                return RedirectToAction(nameof(MojeNajave));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        await PopuniDjecuAsync(model, korisnik.Id);
        return View(model);
    }

    [Authorize(Roles = "RODITELJ")]
    [HttpGet]
    public async Task<IActionResult> MojeNajave()
    {
        var korisnik = await _userManager.GetUserAsync(User);
        if (korisnik == null) return Challenge();

        var najave = await _baza.NajaveOdsustva.AsNoTracking()
            .Where(n => n.RoditeljId == korisnik.Id)
            .OrderByDescending(n => n.DatumKreiranja)
            .ThenByDescending(n => n.Id)
            .ToListAsync();

        return View(najave);
    }

    [Authorize(Roles = "RODITELJ")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Otkazi(int id)
    {
        var korisnik = await _userManager.GetUserAsync(User);
        if (korisnik == null) return Challenge();

        var uspjeh = await _servis.OtkaziAsync(korisnik.Id, id, DateTime.Today);
        TempData[uspjeh ? "Uspjeh" : "Greska"] = uspjeh
            ? "Najava je uspješno otkazana."
            : "Najava nije pronađena ili je nije moguće otkazati.";

        return RedirectToAction(nameof(MojeNajave));
    }

    [Authorize(Roles = "ODGAJATELJ")]
    [HttpGet]
    public async Task<IActionResult> PregledGrupe(int? grupaId, DateTime? od, DateTime? doDatuma)
    {
        var korisnik = await _userManager.GetUserAsync(User);
        if (korisnik == null) return Challenge();

        var grupe = await _baza.Grupe.AsNoTracking()
            .Where(g => g.OdgajateljId == korisnik.Id)
            .OrderBy(g => g.ImeGrupe)
            .Select(g => new { g.Id, g.ImeGrupe })
            .ToListAsync();

        var model = new PregledNajavaGrupeViewModel
        {
            GrupaId = grupaId ?? (grupe.Count == 0 ? 0 : grupe[0].Id),
            Od = od ?? DateTime.Today,
            DoDatuma = doDatuma ?? DateTime.Today.AddDays(30),
            Grupe = grupe.Select(g => new SelectListItem
            {
                Value = g.Id.ToString(), Text = g.ImeGrupe
            }).ToList()
        };

        if (model.GrupaId == 0) return View(model);

        try
        {
            model.Najave = await _servis.PregledZaGrupuAsync(
                korisnik.Id, model.GrupaId, model.Od, model.DoDatuma);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        return View(model);
    }
}

// Samo polja koja smije poslati roditelj; bez RoditeljId, Status ili DatumKreiranja.
public class KreirajNajavuViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Odaberite dijete.")]
    public int DijeteId { get; set; }

    [DataType(DataType.Date)]
    public DateTime DatumOd { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    public DateTime DatumDo { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Unesite razlog odsustva.")]
    [StringLength(200, ErrorMessage = "Razlog može imati najviše 200 znakova.")]
    public string Razlog { get; set; } = string.Empty;

    public List<SelectListItem> Djeca { get; set; } = new();
}

public class PregledNajavaGrupeViewModel
{
    public int GrupaId { get; set; }
    public DateTime Od { get; set; }
    public DateTime DoDatuma { get; set; }
    public List<SelectListItem> Grupe { get; set; } = new();
    public List<NajavaOdsustva> Najave { get; set; } = new();
}
