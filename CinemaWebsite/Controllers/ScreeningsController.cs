using CinemaWebsite.Data;
using CinemaWebsite.Models;
using CinemaWebsite.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CinemaWebsite.Controllers;

public class ScreeningsController(ApplicationDbContext context, IScreeningService screeningService) : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var screenings = await context.Screenings
            .Include(s => s.Movie)
            .Include(s => s.CinemaHall)
            .OrderBy(s => s.StartTime)
            .ToListAsync();
        return View(screenings);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var item = await context.Screenings
            .Include(s => s.Movie)
            .Include(s => s.CinemaHall)
            .FirstOrDefaultAsync(s => s.Id == id);
        return item is null ? NotFound() : View(item);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create()
    {
        var hasMovies = await context.Movies.AnyAsync();
        var hasAvailableHalls = await context.CinemaHalls.AnyAsync(h => h.IsAvailable);

        await PopulateSelectionsAsync();

        if (!hasMovies || !hasAvailableHalls)
        {
            ModelState.AddModelError(string.Empty,
                "To create a screening, add at least one movie and one available hall first.");
        }

        var start = CinemaTime.Now.Date;
        return View(new Screening
        {
            StartTime = start,
            EndTime = start.AddHours(2)
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Screening item)
    {
        ModelState.Remove(nameof(Screening.Movie));
        ModelState.Remove(nameof(Screening.CinemaHall));
        ModelState.Remove(nameof(Screening.Reservations));
        ModelState.Remove(nameof(Screening.EndTime));

        var hasMovies = await context.Movies.AnyAsync();
        var hasAvailableHalls = await context.CinemaHalls.AnyAsync(h => h.IsAvailable);
        if (!hasMovies || !hasAvailableHalls)
        {
            ModelState.AddModelError(string.Empty,
                "Cannot create screening because there are no movies or no available halls.");
        }

        await ApplyAutoEndTimeAsync(item);
        await ValidateScreeningAsync(item);
        if (!ModelState.IsValid)
        {
            await PopulateSelectionsAsync(item.MovieId, item.CinemaHallId);
            return View(item);
        }

        context.Screenings.Add(item);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await context.Screenings.FindAsync(id);
        if (item is null) return NotFound();
        await PopulateSelectionsAsync(item.MovieId, item.CinemaHallId);
        return View(item);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Screening item)
    {
        if (id != item.Id) return BadRequest();

        ModelState.Remove(nameof(Screening.Movie));
        ModelState.Remove(nameof(Screening.CinemaHall));
        ModelState.Remove(nameof(Screening.Reservations));
        ModelState.Remove(nameof(Screening.EndTime));

        await ApplyAutoEndTimeAsync(item);
        await ValidateScreeningAsync(item, id);
        if (!ModelState.IsValid)
        {
            await PopulateSelectionsAsync(item.MovieId, item.CinemaHallId);
            return View(item);
        }

        context.Entry(item).State = EntityState.Modified;
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await context.Screenings
            .Include(s => s.Movie)
            .Include(s => s.CinemaHall)
            .Include(s => s.Reservations)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (item is null) return NotFound();
        return View(item);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await context.Screenings
            .Include(s => s.Reservations)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (item is null) return NotFound();

        var hasActiveReservations = item.Reservations.Any(r => r.Status != ReservationStatus.Cancelled);
        if (hasActiveReservations)
        {
            ModelState.AddModelError(string.Empty, "Cannot delete screening with active reservations.");
            var viewModel = await context.Screenings
                .Include(s => s.Movie)
                .Include(s => s.CinemaHall)
                .Include(s => s.Reservations)
                .FirstOrDefaultAsync(s => s.Id == id);
            return View("Delete", viewModel!);
        }

        context.Screenings.Remove(item);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateSelectionsAsync(int? selectedMovieId = null, int? selectedHallId = null)
    {
        ViewBag.Movies = new SelectList(
            await context.Movies.AsNoTracking().OrderBy(m => m.Title).ToListAsync(),
            nameof(Movie.Id),
            nameof(Movie.Title),
            selectedMovieId);

        ViewBag.Halls = new SelectList(
            await context.CinemaHalls.AsNoTracking().OrderBy(h => h.Number).ToListAsync(),
            nameof(CinemaHall.Id),
            nameof(CinemaHall.Name),
            selectedHallId);
    }

    private async Task ValidateScreeningAsync(Screening item, int? ignoreScreeningId = null)
    {
        if (!await context.Movies.AnyAsync(m => m.Id == item.MovieId))
        {
            ModelState.AddModelError(nameof(Screening.MovieId), "Selected movie does not exist.");
        }

        var hall = await context.CinemaHalls.FirstOrDefaultAsync(h => h.Id == item.CinemaHallId);
        if (hall is null)
        {
            ModelState.AddModelError(nameof(Screening.CinemaHallId), "Selected hall does not exist.");
            return;
        }

        if (!hall.IsAvailable)
        {
            ModelState.AddModelError(nameof(Screening.CinemaHallId), "Selected hall is not available.");
        }

        if (item.StartTime >= item.EndTime)
        {
            ModelState.AddModelError(nameof(Screening.EndTime), "End time must be after start time.");
        }

        if (item.Price <= 0)
        {
            ModelState.AddModelError(nameof(Screening.Price), "Price must be greater than zero.");
        }

        if (await screeningService.HasOverlapAsync(item.CinemaHallId, item.StartTime, item.EndTime, ignoreScreeningId))
        {
            ModelState.AddModelError(string.Empty, "Screening overlaps with another screening in this hall.");
        }
    }

    private async Task ApplyAutoEndTimeAsync(Screening item)
    {
        var movie = await context.Movies
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == item.MovieId);

        if (movie is null)
        {
            return;
        }

        if (movie.Duration <= 0)
        {
            ModelState.AddModelError(nameof(Screening.MovieId), "Selected movie has invalid duration.");
            return;
        }

        item.EndTime = item.StartTime.AddMinutes(movie.Duration);
    }
}
