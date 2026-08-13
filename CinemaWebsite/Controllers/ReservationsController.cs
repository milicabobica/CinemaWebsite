using CinemaWebsite.Data;
using CinemaWebsite.Models;
using CinemaWebsite.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaWebsite.Controllers;

[Authorize]
public class ReservationsController(
    ApplicationDbContext context,
    IReservationService reservationService,
    UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> MyReservations()
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var reservations = await context.Reservations
            .Include(r => r.Screening)
            .ThenInclude(s => s.Movie)
            .Include(r => r.Screening.CinemaHall)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ReservationDate)
            .ToListAsync();

        return View(reservations);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index()
    {
        var allReservations = await context.Reservations
            .Include(r => r.User)
            .Include(r => r.Screening)
            .ThenInclude(s => s.Movie)
            .OrderByDescending(r => r.ReservationDate)
            .ToListAsync();

        return View(allReservations);
    }

    public async Task<IActionResult> Create(int screeningId)
    {
        var screening = await context.Screenings
            .Include(s => s.Movie)
            .Include(s => s.CinemaHall)
            .FirstOrDefaultAsync(s => s.Id == screeningId);
        if (screening is null) return NotFound();

        var available = await reservationService.GetAvailableSeatsAsync(screeningId);
        ViewBag.Screening = screening;
        ViewBag.AvailableSeats = available;
        return View(new Reservation { ScreeningId = screeningId, NumberOfTickets = 1 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Reservation model)
    {
        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var result = await reservationService.CreateReservationAsync(userId, model.ScreeningId, model.NumberOfTickets);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View(model);
        }

        return RedirectToAction(nameof(Details), new { id = result.Reservation!.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var reservation = await context.Reservations
            .Include(r => r.User)
            .Include(r => r.Screening)
            .ThenInclude(s => s.Movie)
            .Include(r => r.Screening.CinemaHall)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (reservation is null) return NotFound();

        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var isAdmin = User.IsInRole("Admin");
        if (!await reservationService.CanAccessReservationAsync(reservation, userId, isAdmin))
        {
            return Forbid();
        }

        return View(reservation);
    }

    public async Task<IActionResult> Cancel(int id)
    {
        var reservation = await context.Reservations
            .Include(r => r.Screening)
            .ThenInclude(s => s.Movie)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (reservation is null) return NotFound();

        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var isAdmin = User.IsInRole("Admin");
        if (!await reservationService.CanAccessReservationAsync(reservation, userId, isAdmin))
        {
            return Forbid();
        }

        return View(reservation);
    }

    [HttpPost, ActionName("Cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelConfirmed(int id)
    {
        var reservation = await context.Reservations.FirstOrDefaultAsync(r => r.Id == id);
        if (reservation is null) return NotFound();

        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var isAdmin = User.IsInRole("Admin");
        var result = await reservationService.CancelReservationAsync(reservation, userId, isAdmin);
        if (!result.Success)
        {
            return result.Error == "Forbidden" ? Forbid() : BadRequest(result.Error);
        }

        return RedirectToAction(nameof(MyReservations));
    }
}