using CinemaWebsite.Data;
using CinemaWebsite.Models;
using CinemaWebsite.Services;
using CinemaWebsite.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaWebsite.Controllers;

public class ReservationsController(
    ApplicationDbContext context,
    IReservationService reservationService,
    UserManager<ApplicationUser> userManager,
    IDataProtectionProvider dataProtectionProvider) : Controller
{
    private readonly IDataProtector reservationTokenProtector =
        dataProtectionProvider.CreateProtector("ReservationGuestAccessToken");
    public async Task<IActionResult> MyReservations()
    {
        await reservationService.ExpirePendingReservationsAsync();

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
        await reservationService.ExpirePendingReservationsAsync();

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
        await reservationService.ExpirePendingReservationsAsync();

        var screening = await context.Screenings
            .Include(s => s.Movie)
            .Include(s => s.CinemaHall)
            .FirstOrDefaultAsync(s => s.Id == screeningId);
        if (screening is null) return NotFound();
        if (screening.StartTime < DateTime.UtcNow)
        {
            return RedirectToAction("Details", "Movies", new { id = screening.MovieId });
        }

        var seatMap = await reservationService.GetSeatMapAsync(screeningId);
        ViewBag.Screening = screening;
        ViewBag.SeatMap = seatMap;
        return View(new Reservation { ScreeningId = screeningId, NumberOfTickets = 1 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Reservation model, int[]? selectedSeatIds)
    {
        await reservationService.ExpirePendingReservationsAsync();

        var userId = userManager.GetUserId(User);

        var screening = await context.Screenings
            .FirstOrDefaultAsync(s => s.Id == model.ScreeningId);
        if (screening is null) return NotFound();
        if (screening.StartTime < DateTime.UtcNow)
        {
            ModelState.AddModelError(string.Empty, "This screening has already started or passed.");
            return RedirectToAction(nameof(Create), new { screeningId = model.ScreeningId });
        }

        (bool Success, string Error, Reservation? Reservation) result;
        if (selectedSeatIds != null && selectedSeatIds.Length > 0)
        {
            result = await reservationService.CreateReservationAsync(userId, model.ScreeningId, selectedSeatIds);
        }
        else
        {
            result = await reservationService.CreateReservationAsync(userId, model.ScreeningId, model.NumberOfTickets);
        }

        if (!result.Success)
        {
            screening = await context.Screenings
                .Include(s => s.Movie)
                .Include(s => s.CinemaHall)
                .FirstOrDefaultAsync(s => s.Id == model.ScreeningId);

            if (screening is null) return NotFound();

            var seatMap = await reservationService.GetSeatMapAsync(model.ScreeningId);
            ViewBag.Screening = screening;
            ViewBag.SeatMap = seatMap;
            ModelState.AddModelError(string.Empty, result.Error);
            return View(model);
        }

        var reservationId = result.Reservation!.Id;
        if (string.IsNullOrWhiteSpace(userId))
        {
            var guestToken = CreateReservationToken(reservationId);
            return RedirectToAction(nameof(Payment), new { id = reservationId, token = guestToken });
        }

        return RedirectToAction(nameof(Payment), new { id = reservationId });
    }

    
    public async Task<IActionResult> Payment(int id, string? token)
    {
        await reservationService.ExpirePendingReservationsAsync();

        var reservation = await context.Reservations
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (reservation is null) return NotFound();

        if (reservation.Status != ReservationStatus.Pending)
        {
            return RedirectToAction(nameof(Details), new { id, token });
        }

        var userId = userManager.GetUserId(User);
        var isAdmin = User.IsInRole("Admin");
        if (!await CanAccessReservationAsync(reservation, token, userId, isAdmin))
        {
            return Forbid();
        }

        var currentUser = await userManager.GetUserAsync(User);
        var userEmail = currentUser?.Email ?? reservation.ReservationEmail ?? string.Empty;
        var model = new PaymentViewModel
        {
            ReservationId = reservation.Id,
            TotalPrice = reservation.TotalPrice,
            Email = userEmail,
            IsEmailRequired = string.IsNullOrWhiteSpace(userEmail),
            AccessToken = token ?? string.Empty
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Payment(PaymentViewModel model)
    {
        await reservationService.ExpirePendingReservationsAsync();

        var reservation = await context.Reservations
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == model.ReservationId);
        if (reservation is null) return NotFound();

        if (reservation.Status != ReservationStatus.Pending)
        {
            return BadRequest("Reservation is no longer pending.");
        }

        var userId = userManager.GetUserId(User);
        var isAdmin = User.IsInRole("Admin");
        if (!await CanAccessReservationAsync(reservation, model.AccessToken, userId, isAdmin))
        {
            return Forbid();
        }

        model.TotalPrice = reservation.TotalPrice;
        var currentUser = await userManager.GetUserAsync(User);
        var storedEmail = currentUser?.Email ?? reservation.ReservationEmail ?? string.Empty;
        model.IsEmailRequired = string.IsNullOrWhiteSpace(storedEmail);
        if (!model.IsEmailRequired)
        {
            model.Email = storedEmail;
        }
        else if (string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(nameof(PaymentViewModel.Email), "Email is required.");
        }
        
        if (!IsExpirationValid(model.ExpirationDate))
        {
            ModelState.AddModelError(nameof(model.ExpirationDate), "Card expiration date is invalid or already expired.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        reservation.Status = ReservationStatus.Confirmed;
        reservation.ReservationEmail = model.Email;
        await context.SaveChangesAsync();

        TempData["PaymentSuccessMessage"] = $"Payment successful. Your ticket will arrive on mail: {model.Email}.";
        if (string.IsNullOrWhiteSpace(userId))
        {
            var guestToken = string.IsNullOrWhiteSpace(model.AccessToken)
                ? CreateReservationToken(reservation.Id)
                : model.AccessToken;
            return RedirectToAction(nameof(Details), new { id = reservation.Id, token = guestToken });
        }

        return RedirectToAction(nameof(Details), new { id = reservation.Id });
    }
    
    private static bool IsExpirationValid(string expiration)
    {
        if (string.IsNullOrWhiteSpace(expiration)) return false;
        var parts = expiration.Split('/');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out var month) || !int.TryParse(parts[1], out var year)) return false;
        if (month is < 1 or > 12) return false;

        var fullYear = 2000 + year;
        var now = DateTime.UtcNow;
        if (fullYear < now.Year) return false;
        if (fullYear == now.Year && month < now.Month) return false;

        return true;
    }

    public async Task<IActionResult> Details(int id, string? token)
    {
        await reservationService.ExpirePendingReservationsAsync();

        var reservation = await context.Reservations
            .Include(r => r.User)
            .Include(r => r.Screening)
            .ThenInclude(s => s.Movie)
            .Include(r => r.Screening.CinemaHall)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (reservation is null) return NotFound();

        var userId = userManager.GetUserId(User);
        var isAdmin = User.IsInRole("Admin");
        if (!await CanAccessReservationAsync(reservation, token, userId, isAdmin))
        {
            return Forbid();
        }

        return View(reservation);
    }

    public async Task<IActionResult> Cancel(int id, string? token)
    {
        await reservationService.ExpirePendingReservationsAsync();

        var reservation = await context.Reservations
            .Include(r => r.Screening)
            .ThenInclude(s => s.Movie)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (reservation is null) return NotFound();

        var userId = userManager.GetUserId(User);
        var isAdmin = User.IsInRole("Admin");
        if (!await CanAccessReservationAsync(reservation, token, userId, isAdmin))
        {
            return Forbid();
        }

        ViewBag.GuestToken = token;
        return View(reservation);
    }

    [HttpPost, ActionName("Cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelConfirmed(int id, string? token)
    {
        await reservationService.ExpirePendingReservationsAsync();

        var reservation = await context.Reservations.FirstOrDefaultAsync(r => r.Id == id);
        if (reservation is null) return NotFound();

        var userId = userManager.GetUserId(User);
        var isAdmin = User.IsInRole("Admin");
        if (!await CanAccessReservationAsync(reservation, token, userId, isAdmin))
        {
            return Forbid();
        }

        var result = await reservationService.CancelReservationAsync(reservation, userId, isAdmin);
        if (!result.Success)
        {
            return result.Error == "Forbidden" ? Forbid() : BadRequest(result.Error);
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return RedirectToAction("Index", "Home");
        }

        return RedirectToAction(nameof(MyReservations));
    }

    private async Task<bool> CanAccessReservationAsync(Reservation reservation, string? token, string? userId, bool isAdmin)
    {
        if (await reservationService.CanAccessReservationAsync(reservation, userId, isAdmin))
        {
            return true;
        }

        return IsValidReservationToken(reservation.Id, token);
    }

    private string CreateReservationToken(int reservationId)
    {
        var payload = $"{reservationId}|{DateTime.UtcNow.Ticks}";
        return reservationTokenProtector.Protect(payload);
    }

    private bool IsValidReservationToken(int reservationId, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var payload = reservationTokenProtector.Unprotect(token);
            var parts = payload.Split('|');
            if (parts.Length != 2)
            {
                return false;
            }

            return int.TryParse(parts[0], out var tokenReservationId) && tokenReservationId == reservationId;
        }
        catch
        {
            return false;
        }
    }
}