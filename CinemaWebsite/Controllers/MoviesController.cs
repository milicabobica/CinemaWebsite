using CinemaWebsite.Data;
using CinemaWebsite.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaWebsite.Controllers;

public class MoviesController(ApplicationDbContext context) : Controller {
    [AllowAnonymous]
    public async Task<IActionResult> Index(string? search, int? genreId, string? status, string? language,
        string? day) {
        var now = DateTime.UtcNow;
        await DeletePastScreeningsAsync(now);

        var query = context.Movies
            .Include(m => m.MovieGenres)
            .ThenInclude(mg => mg.Genre)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search)) {
            query = query.Where(m =>
                m.Title.Contains(search) ||
                m.Description.Contains(search));
        }

        if (genreId.HasValue) {
            query = query.Where(m =>
                m.MovieGenres.Any(g => g.GenreId == genreId.Value));
        }

        if (!string.IsNullOrWhiteSpace(language)) {
            query = query.Where(m => m.Language == language);
        }

        if (!string.IsNullOrWhiteSpace(status)) {
            if (status == "NowShowing") {
                query = query.Where(m => m.ReleaseDate <= now);
            }
            else if (status == "ComingSoon") {
                query = query.Where(m => m.ReleaseDate > now);
            }
        }


        // if (!string.IsNullOrWhiteSpace(day))
        // {
        //     if (Enum.TryParse<DayOfWeek>(day, true, out var selectedDay))
        //     {
        //         query = query.Where(m =>
        //             m.ReleaseDate.DayOfWeek == selectedDay);
        //     }
        // }

        if (!string.IsNullOrWhiteSpace(day) && Enum.TryParse<DayOfWeek>(day, true, out var selectedDay)) {
            query = query.Where(m => context.Screenings.Any(s =>
                s.MovieId == m.Id &&
                s.StartTime >= now &&
                s.StartTime.DayOfWeek == selectedDay));
            ViewBag.SelectedDay = selectedDay;
        }
        else {
            ViewBag.SelectedDay = null;
        }


        ViewBag.Genres = await context.Genres
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .ToListAsync();

        return View(await query
            .OrderBy(m => m.Title)
            .ToListAsync());
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id, string? day) {
        var now = DateTime.UtcNow;

        var movie = await context.Movies
            .Include(m => m.MovieGenres)
            .ThenInclude(mg => mg.Genre)
            .Include(m => m.Screenings)
            .ThenInclude(s => s.CinemaHall)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (movie is null) return NotFound();

        movie.Screenings = movie.Screenings
            .Where(s => s.StartTime >= now)
            .OrderBy(s => s.StartTime)
            .ToList();

        if (!string.IsNullOrWhiteSpace(day) && Enum.TryParse<DayOfWeek>(day, true, out var selectedDay)) {
            movie.Screenings = movie.Screenings
                .Where(s => s.StartTime.DayOfWeek == selectedDay)
                .OrderBy(s => s.StartTime)
                .ToList();
            ViewBag.FilteredDay = selectedDay;
        }

        return View(movie);
    }

    private async Task DeletePastScreeningsAsync(DateTime now) {
        var pastScreeningIds = await context.Screenings
            .Where(s => s.StartTime < now)
            .Select(s => s.Id)
            .ToListAsync();

        if (!pastScreeningIds.Any()) {
            return;
        }

        var reservationsToDelete = await context.Reservations
            .Where(r => pastScreeningIds.Contains(r.ScreeningId))
            .ToListAsync();

        if (reservationsToDelete.Any()) {
            context.Reservations.RemoveRange(reservationsToDelete);
        }

        var screeningsToDelete = await context.Screenings
            .Where(s => pastScreeningIds.Contains(s.Id))
            .ToListAsync();

        context.Screenings.RemoveRange(screeningsToDelete);
        await context.SaveChangesAsync();
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create() {
        ViewBag.Genres = await context.Genres
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .ToListAsync();
        return View(new Movie {
            ReleaseDate = DateTime.Now.Date
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Movie movie, int[] selectedGenreIds) {
        if (selectedGenreIds.Length == 0) {
            ModelState.AddModelError("selectedGenreIds", "Please select at least one genre");
        }

        if (!ModelState.IsValid) {
            ViewBag.Genres = await context.Genres
                .AsNoTracking()
                .OrderBy(g => g.Name)
                .ToListAsync();
            ViewBag.SelectedGenreIds = selectedGenreIds;
            return View(movie);
        }

        context.Movies.Add(movie);
        await context.SaveChangesAsync();

        foreach (var genreId in selectedGenreIds.Distinct()) {
            context.MovieGenres.Add(new MovieGenre{
                MovieId = movie.Id,
                GenreId = genreId});
        }

        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id) {
        var movie = await context.Movies
            .Include(m => m.MovieGenres)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movie is null) return NotFound();

        ViewBag.Genres = await context.Genres
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .ToListAsync();

        ViewBag.SelectedGenreIds = movie.MovieGenres
            .Select(mg => mg.GenreId)
            .ToArray();

        return View(movie);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Movie movie, int[] selectedGenreIds) {
        if (id != movie.Id) return BadRequest();

        if (selectedGenreIds.Length == 0) {
            ModelState.AddModelError("selectedGenreIds", "Please select at least one genre.");
        }

        if (!ModelState.IsValid) {
            ViewBag.Genres = await context.Genres
                .AsNoTracking()
                .OrderBy(g => g.Name)
                .ToListAsync();

            ViewBag.SelectedGenreIds = selectedGenreIds;
            return View(movie);
        }

        var existingMovie = await context.Movies
            .Include(m => m.MovieGenres)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (existingMovie is null) {
            return NotFound();
        }
        context.Entry(existingMovie).CurrentValues.SetValues(movie);
        var selectedIds = selectedGenreIds.Distinct().ToHashSet();

        var genresToRemove = existingMovie.MovieGenres
            .Where(mg => !selectedIds.Contains(mg.GenreId))
            .ToList();
        
        context.MovieGenres.RemoveRange(genresToRemove);

        var existingGenreIds = existingMovie.MovieGenres
            .Select(mg => mg.GenreId)
            .ToHashSet();
        
        foreach (var genreId in selectedIds) {
            if (!existingGenreIds.Contains(genreId)) {
                existingMovie.MovieGenres.Add(new MovieGenre{
                    MovieId = existingMovie.Id,
                    GenreId = genreId
                    });
            }
        }
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id) {
        var movie = await context.Movies.FindAsync(id);
        if (movie is null) return NotFound();
        return View(movie);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id) {
        var movie = await context.Movies.FindAsync(id);
        if (movie is null) return NotFound();
        context.Movies.Remove(movie);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}