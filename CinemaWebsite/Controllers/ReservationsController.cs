using CinemaWebsite.Data;
using CinemaWebsite.Models;
using CinemaWebsite.Services;
using CinemaWebsite.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaWebsite.Controllers;

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

    [HttpGet]
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
            var screening = await context.Screenings
                .Include(s => s.Movie)
                .Include(s => s.CinemaHall)
                .FirstOrDefaultAsync(s => s.Id == model.ScreeningId);

            if (screening is null) return NotFound();

            var available = await reservationService.GetAvailableSeatsAsync(model.ScreeningId);
            ViewBag.Screening = screening;
            ViewBag.AvailableSeats = available;
            ModelState.AddModelError(string.Empty, result.Error);
            return View(model);
        }

        return RedirectToAction(nameof(Payment), new {  id = result.Reservation!.Id });
    }

    
    public async Task<IActionResult> Payment(int id)
    {
        var reservation = await context.Reservations
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (reservation is null) return NotFound();

        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var isAdmin = User.IsInRole("Admin");
        if (!await reservationService.CanAccessReservationAsync(reservation, userId, isAdmin))
        {
            return Forbid();
        }

        var userEmail = reservation.User?.Email ?? string.Empty;
        var model = new PaymentViewModel
        {
            ReservationId = reservation.Id,
            TotalPrice = reservation.TotalPrice,
            Email = userEmail,
            IsEmailRequired = string.IsNullOrWhiteSpace(userEmail)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Payment(PaymentViewModel model)
    {
        var reservation = await context.Reservations
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == model.ReservationId);
        if (reservation is null) return NotFound();

        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var isAdmin = User.IsInRole("Admin");
        if (!await reservationService.CanAccessReservationAsync(reservation, userId, isAdmin))
        {
            return Forbid();
        }

        model.TotalPrice = reservation.TotalPrice;
        var storedEmail = reservation.User?.Email ?? string.Empty;
        model.IsEmailRequired = string.IsNullOrWhiteSpace(storedEmail);
        if (!model.IsEmailRequired)
        {
            model.Email = storedEmail;
        }
        else if (string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(nameof(PaymentViewModel.Email), "Email is required.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        reservation.Status = ReservationStatus.Confirmed;
        await context.SaveChangesAsync();

        TempData["PaymentSuccessMessage"] = $"Payment successful. Your ticket will arrive on mail: {model.Email}.";
        return RedirectToAction(nameof(Details), new { id = reservation.Id });
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