using CinemaWebsite.Data;
using CinemaWebsite.Models;
using CinemaWebsite.Services;
using CinemaWebsite.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CinemaWebsite.Controllers;

public class ReservationsController(
    ApplicationDbContext context,
    IReservationService reservationService,
    UserManager<ApplicationUser> userManager,
    IDataProtectionProvider dataProtectionProvider) : Controller
{
    private readonly ReservationGuestTokens reservationTokens = new(dataProtectionProvider);
    public async Task<IActionResult> MyReservations()
    {
        await reservationService.ExpirePendingReservationsAsync();

        var userId = userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var reservations = await context.Reservations
            .Include(r => r.Screening)
            .ThenInclude(s => s.Movie)
            .Include(r => r.Screening.CinemaHall)
            .Include(r => r.ReservationSeats)
            .ThenInclude(rs => rs.Seat)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ReservationDate)
            .ToListAsync();

        return View(reservations.Select(MapReservationToDisplayModel).ToList());
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index()
    {
        await reservationService.ExpirePendingReservationsAsync();

        var allReservations = await context.Reservations
            .Include(r => r.User)
            .Include(r => r.Screening)
            .ThenInclude(s => s.Movie)
            .Include(r => r.Screening.CinemaHall)
            .Include(r => r.ReservationSeats)
            .ThenInclude(rs => rs.Seat)
            .OrderByDescending(r => r.ReservationDate)
            .ToListAsync();

        return View(allReservations.Select(MapReservationToDisplayModel).ToList());
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
            TempData["ScreeningStartedMessage"] = "This screening has already started";
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
            TempData["ScreeningStartedMessage"] = "This screening has already started";
            return RedirectToAction("Details", "Movies", new { id = screening.MovieId });
        }

        if (selectedSeatIds is null || selectedSeatIds.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Please select at least one seat.");
        }

        var distinctSeatIds = selectedSeatIds?.Distinct().ToArray() ?? [];
        if (selectedSeatIds is not null && distinctSeatIds.Length != selectedSeatIds.Length)
        {
            ModelState.AddModelError(string.Empty, "Duplicate seat selection detected. Please select seats again.");
        }

        if (distinctSeatIds.Length > 0)
        {
            model.NumberOfTickets = distinctSeatIds.Length;
            ModelState.Remove(nameof(Reservation.NumberOfTickets));
            ModelState.Remove($"{nameof(model)}.{nameof(Reservation.NumberOfTickets)}");
        }

        ModelState.Remove(nameof(Reservation.Screening));
        ModelState.Remove($"{nameof(model)}.{nameof(Reservation.Screening)}");

        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e =>
                    $"{x.Key}: {e.ErrorMessage} {e.Exception?.Message}"
                ))
                .ToList();

            foreach (var error in errors)
            {
                Console.WriteLine("MODELSTATE ERROR: " + error);
            }
            
            screening = await context.Screenings
                .Include(s => s.Movie)
                .Include(s => s.CinemaHall)
                .FirstOrDefaultAsync(s => s.Id == model.ScreeningId);

            if (screening is null) return NotFound();

            var seatMapForValidation = await reservationService.GetSeatMapAsync(model.ScreeningId);
            ViewBag.Screening = screening;
            ViewBag.SeatMap = seatMapForValidation;
            return View(model);
        }

        var result = await reservationService.CreateReservationAsync(userId, model.ScreeningId, distinctSeatIds);
        
        

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
            var guestToken = reservationTokens.Create(reservationId);
            return RedirectToAction(nameof(Payment), new { id = reservationId, token = guestToken });
        }

        return RedirectToAction(nameof(Payment), new { id = reservationId });
    }

    
    public async Task<IActionResult> Payment(int id, string? token)
    {
        await reservationService.ExpirePendingReservationsAsync();

        var reservation = await context.Reservations
            .Include(r => r.User)
            .Include(r => r.Screening)
            .ThenInclude(s => s.Movie)
            .Include(r => r.Screening.CinemaHall)
            .Include(r => r.ReservationSeats)
            .ThenInclude(rs => rs.Seat)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (reservation is null) return NotFound();

        var userId = userManager.GetUserId(User);
        var isAdmin = User.IsInRole("Admin");
        var accessFailure = await GetReservationAccessFailureAsync(reservation, token, userId, isAdmin);
        if (accessFailure is not null) return accessFailure;

        if (reservation.Status != ReservationStatus.Pending)
        {
            return RedirectToAction(nameof(Details), new { id, token });
        }

        var currentUser = await userManager.GetUserAsync(User);
        var userEmail = currentUser?.Email ?? reservation.ReservationEmail ?? string.Empty;
        var model = new PaymentViewModel
        {
            ReservationId = reservation.Id,
            ScreeningId = reservation.ScreeningId,
            ExpiresAtUtc = reservation.ExpiresAtUtc,
            TotalPrice = reservation.TotalPrice,
            Email = userEmail,
            IsEmailRequired = string.IsNullOrWhiteSpace(userEmail),
            AccessToken = token ?? string.Empty,
            MovieTitle = reservation.Screening.Movie.Title,
            HallName = reservation.Screening.CinemaHall.Name,
            ScreeningStartTime = reservation.Screening.StartTime,
            Seats = MapSeats(reservation)
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
            .Include(r => r.Screening)
            .ThenInclude(s => s.Movie)
            .Include(r => r.Screening.CinemaHall)
            .Include(r => r.ReservationSeats)
            .ThenInclude(rs => rs.Seat)
            .FirstOrDefaultAsync(r => r.Id == model.ReservationId);
        if (reservation is null) return NotFound();

        var userId = userManager.GetUserId(User);
        var isAdmin = User.IsInRole("Admin");
        var accessFailure = await GetReservationAccessFailureAsync(reservation, model.AccessToken, userId, isAdmin);
        if (accessFailure is not null) return accessFailure;

        if (reservation.Status != ReservationStatus.Pending)
        {
            TempData["ReservationExpiredMessage"] = "Your seats were released because payment wasn't completed in time. Please select seats again.";
            return RedirectToAction(nameof(Create), new { screeningId = reservation.ScreeningId });
        }

        model.ScreeningId = reservation.ScreeningId;
        model.ExpiresAtUtc = reservation.ExpiresAtUtc;
        model.TotalPrice = reservation.TotalPrice;
        model.MovieTitle = reservation.Screening.Movie.Title;
        model.HallName = reservation.Screening.CinemaHall.Name;
        model.ScreeningStartTime = reservation.Screening.StartTime;
        model.Seats = MapSeats(reservation);

        model.CardNumber = new string((model.CardNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        model.Cvv = new string((model.Cvv ?? string.Empty).Where(char.IsDigit).ToArray());
        model.ExpirationDate = NormalizeExpiration(model.ExpirationDate);

        var currentUser = await userManager.GetUserAsync(User);
        var storedEmail = currentUser?.Email ?? reservation.ReservationEmail ?? string.Empty;
        model.IsEmailRequired = string.IsNullOrWhiteSpace(storedEmail);
        if (!model.IsEmailRequired)
        {
            model.Email = storedEmail;
        }
        else
        {
            model.Email = (model.Email ?? string.Empty).Trim();
        }

        ModelState.Clear();
        TryValidateModel(model);

        if (!model.IsEmailRequired)
        {
            ModelState.Remove(nameof(PaymentViewModel.Email));
        }

        if (model.IsEmailRequired && string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(nameof(PaymentViewModel.Email), "Email is required.");
        }
        
        if (!IsExpirationValid(model.ExpirationDate))
        {
            ModelState.AddModelError(nameof(model.ExpirationDate), "Card expiration date is invalid or already expired.");
        }
        
        foreach (var state in ModelState)
        {
            foreach (var error in state.Value.Errors)
            {
                Console.WriteLine(
                    $"PAYMENT MODELSTATE ERROR: {state.Key} - {error.ErrorMessage}"
                );

                if (error.Exception != null)
                {
                    Console.WriteLine(
                        $"EXCEPTION: {error.Exception.Message}"
                    );
                }
            }
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
                ? reservationTokens.Create(reservation.Id)
                : model.AccessToken;
            return RedirectToAction(nameof(Details), new { id = reservation.Id, token = guestToken });
        }

        return RedirectToAction(nameof(Details), new { id = reservation.Id });
    }

    private static string NormalizeExpiration(string expiration)
    {
        if (string.IsNullOrWhiteSpace(expiration)) return string.Empty;

        var input = expiration.Trim();
        var slashMatch = Regex.Match(input, "^(0[1-9]|1[0-2])\\/(\\d{2}|\\d{4})$");
        if (slashMatch.Success)
        {
            var month = slashMatch.Groups[1].Value;
            var year = slashMatch.Groups[2].Value;
            return year.Length == 4 ? $"{month}/{year[^2..]}" : $"{month}/{year}";
        }

        var digitsOnly = new string(input.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length == 6)
        {
            var month = digitsOnly[..2];
            var year = digitsOnly[2..];
            return $"{month}/{year[^2..]}";
        }

        if (digitsOnly.Length == 4)
        {
            var month = digitsOnly[..2];
            var year = digitsOnly[2..];
            return $"{month}/{year}";
        }

        return input;
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
            .Include(r => r.ReservationSeats)
            .ThenInclude(rs => rs.Seat)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (reservation is null) return NotFound();

        var userId = userManager.GetUserId(User);
        var isAdmin = User.IsInRole("Admin");
        var accessFailure = await GetReservationAccessFailureAsync(reservation, token, userId, isAdmin);
        if (accessFailure is not null) return accessFailure;

        ViewBag.GuestToken = token;
        return View(MapReservationToDisplayModel(reservation));
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
        var accessFailure = await GetReservationAccessFailureAsync(reservation, token, userId, isAdmin);
        if (accessFailure is not null) return accessFailure;

        ViewBag.GuestToken = token;
        return View(reservation);
    }

    [HttpPost, ActionName("Cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelConfirmed(int id, string? token)
    {
        await reservationService.ExpirePendingReservationsAsync();

        var reservation = await context.Reservations.Include(r => r.Screening).FirstOrDefaultAsync(r => r.Id == id);
        if (reservation is null) return NotFound();

        var userId = userManager.GetUserId(User);
        var isAdmin = User.IsInRole("Admin");
        var accessFailure = await GetReservationAccessFailureAsync(reservation, token, userId, isAdmin);
        if (accessFailure is not null) return accessFailure;

        var result = await reservationService.CancelReservationAsync(reservation, userId, isAdmin);
        if (!result.Success)
        {
            if (result.Error == "Forbidden") return Forbid();

            TempData["ReservationNoticeMessage"] = result.Error;
            return RedirectToAction(nameof(Details), new { id, token });
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return RedirectToAction("Index", "Home");
        }

        return RedirectToAction(nameof(MyReservations));
    }

    private async Task<IActionResult?> GetReservationAccessFailureAsync(
        Reservation reservation, string? token, string? userId, bool isAdmin)
    {
        if (await reservationService.CanAccessReservationAsync(reservation, userId, isAdmin))
            return null;

        var status = reservationTokens.Validate(reservation.Id, reservation.Screening.StartTime, token);
        if (status == ReservationTokenStatus.Valid) return null;
        if (status == ReservationTokenStatus.Expired)
        {
            Response.StatusCode = StatusCodes.Status410Gone;
            return View("LinkExpired");
        }

        return Forbid();
    }

    private static ReservationDisplayViewModel MapReservationToDisplayModel(Reservation reservation)
    {
        return new ReservationDisplayViewModel
        {
            Id = reservation.Id,
            CustomerEmail = reservation.User?.Email ?? reservation.User?.UserName ?? reservation.ReservationEmail,
            MovieTitle = reservation.Screening.Movie.Title,
            HallName = reservation.Screening.CinemaHall.Name,
            ScreeningStartTime = reservation.Screening.StartTime,
            NumberOfTickets = reservation.NumberOfTickets,
            TotalPrice = reservation.TotalPrice,
            Status = reservation.Status,
            ExpiresAtUtc = reservation.ExpiresAtUtc,
            Seats = MapSeats(reservation)
        };
    }

    private static List<ReservationSeatViewModel> MapSeats(Reservation reservation)
    {
        return reservation.ReservationSeats
            .Select(rs => new ReservationSeatViewModel
            {
                Row = rs.Seat.Row,
                Number = rs.Seat.Number
            })
            .OrderBy(s => s.Row)
            .ThenBy(s => s.Number)
            .ToList();
    }
}
