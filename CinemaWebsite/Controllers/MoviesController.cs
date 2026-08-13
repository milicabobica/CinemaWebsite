using CinemaWebsite.Data;
using CinemaWebsite.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaWebsite.Controllers;

public class MoviesController(ApplicationDbContext context) : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Index(string? search, int? genreId, string? status, string? language)
    {
        var query = context.Movies
            .Include(m => m.MovieGenres)
            .ThenInclude(mg => mg.Genre)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m => m.Title.Contains(search) || m.Description.Contains(search));
        }

        if (genreId.HasValue)
        {
            query = query.Where(m => m.MovieGenres.Any(g => g.GenreId == genreId.Value));
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            query = query.Where(m => m.Language == language);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var now = DateTime.UtcNow;
            if (status == "NowShowing") query = query.Where(m => m.ReleaseDate <= now);
            if (status == "ComingSoon") query = query.Where(m => m.ReleaseDate > now);
        }

        ViewBag.Genres = await context.Genres.AsNoTracking().OrderBy(g => g.Name).ToListAsync();
        return View(await query.OrderBy(m => m.Title).ToListAsync());
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var movie = await context.Movies
            .Include(m => m.MovieGenres)
            .ThenInclude(mg => mg.Genre)
            .Include(m => m.Screenings)
            .ThenInclude(s => s.CinemaHall)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (movie is null) return NotFound();
        return View(movie);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        return View(new Movie());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Movie movie)
    {
        if (!ModelState.IsValid) return View(movie);
        context.Movies.Add(movie);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var movie = await context.Movies.FindAsync(id);
        if (movie is null) return NotFound();
        return View(movie);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Movie movie)
    {
        if (id != movie.Id) return BadRequest();
        if (!ModelState.IsValid) return View(movie);
        context.Update(movie);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var movie = await context.Movies.FindAsync(id);
        if (movie is null) return NotFound();
        return View(movie);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var movie = await context.Movies.FindAsync(id);
        if (movie is null) return NotFound();
        context.Movies.Remove(movie);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}