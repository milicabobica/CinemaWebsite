using CinemaWebsite.Data;
using CinemaWebsite.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaWebsite.Controllers;

[Authorize(Roles = "Admin")]
public class GenresController(ApplicationDbContext context) : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        return View(await context.Genres.Include(g => g.MovieGenres)
            .ThenInclude(mg => mg.Movie).AsNoTracking().OrderBy(g => g.Name).ToListAsync());
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var genre = await context.Genres
            .Include(g => g.MovieGenres)
            .ThenInclude(mg => mg.Movie)
            .FirstOrDefaultAsync(g => g.Id == id);
        return genre is null ? NotFound() : View(genre);
    }

    public IActionResult Create() => View(new Genre());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Genre genre)
    {
        if (!ModelState.IsValid) return View(genre);
        context.Genres.Add(genre);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var genre = await context.Genres.FindAsync(id);
        return genre is null ? NotFound() : View(genre);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Genre genre)
    {
        if (id != genre.Id) return BadRequest();
        if (!ModelState.IsValid) return View(genre);
        context.Entry(genre).State = EntityState.Modified;
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var genre = await context.Genres
            .Include(g => g.MovieGenres)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (genre is null) return NotFound();
        return View(genre);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var genre = await context.Genres
            .Include(g => g.MovieGenres)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (genre is null) return NotFound();

        if (genre.MovieGenres.Any())
        {
            context.MovieGenres.RemoveRange(genre.MovieGenres);
        }

        context.Genres.Remove(genre);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}