using System.Diagnostics;
using CinemaWebsite.Data;
using Microsoft.AspNetCore.Mvc;
using CinemaWebsite.Models;
using CinemaWebsite.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

namespace CinemaWebsite.Controllers;

[Authorize(Roles = "Admin")]
public class HomeController(ApplicationDbContext context) : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        var now = DateTime.UtcNow;
        var viewModel = new HomeViewModel
        {
            NowShowingMovies = await context.Movies
                .Where(m => m.ReleaseDate <= now)
                .OrderBy(m => m.Title)
                .Take(8)
                .ToListAsync(),
            ComingSoonMovies = await context.Movies
                .Where(m => m.ReleaseDate > now)
                .OrderBy(m => m.ReleaseDate)
                .Take(8)
                .ToListAsync(),
            LatestNews = await context.News
                .Where(n => n.IsPublished)
                .OrderByDescending(n => n.PublishedDate)
                .Take(5)
                .ToListAsync(),
            Services = await context.Services
                .OrderBy(s => s.Name)
                .Take(6)
                .ToListAsync(),
            MovieCount = await context.Movies.CountAsync(),
            CinemaHallCount = await context.CinemaHalls.CountAsync()
        };

        return View(viewModel);
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
    public IActionResult SetLanguage(string culture, string? returnUrl = null)
    {
        var supportedCultures = new[] { "en", "mk" };
        if (!supportedCultures.Contains(culture))
        {
            culture = "en";
        }

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

        return LocalRedirect(returnUrl ?? Url.Action(nameof(Index))!);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
    public IActionResult SetCookieConsent(string consent, string? returnUrl = null)
    {
        var safeConsent = consent == "AcceptAll" ? "AcceptAll" : "NecessaryOnly";
        Response.Cookies.Append("CookieConsent", safeConsent, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true
        });

        return LocalRedirect(returnUrl ?? Url.Action(nameof(Index))!);
    }

    public async Task<IActionResult> AdminDashboard()
    {
        var now = DateTime.UtcNow;
        var vm = new AdminDashboardViewModel
        {
            TotalMovies = await context.Movies.CountAsync(),
            NowShowingMovies = await context.Movies.CountAsync(m => m.ReleaseDate <= now),
            ComingSoonMovies = await context.Movies.CountAsync(m => m.ReleaseDate > now),
            TotalGenres = await context.Genres.CountAsync(),
            TotalHalls = await context.CinemaHalls.CountAsync(),
            TotalScreenings = await context.Screenings.CountAsync(),
            TotalReservations = await context.Reservations.CountAsync(),
            ConfirmedReservations = await context.Reservations.CountAsync(r => r.Status == ReservationStatus.Confirmed),
            CancelledReservations = await context.Reservations.CountAsync(r => r.Status == ReservationStatus.Cancelled),
            UnreadContactMessages = await context.ContactMessages.CountAsync(c => !c.IsRead),
            PublishedNews = await context.News.CountAsync(n => n.IsPublished)
        };

        return View(vm);
    }
}