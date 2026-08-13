using CinemaWebsite.Data;
using CinemaWebsite.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaWebsite.Services;

public class ReservationService(ApplicationDbContext context) : IReservationService
{
    public async Task<int> GetAvailableSeatsAsync(int screeningId)
    {
        var screening = await context.Screenings
            .Include(s => s.CinemaHall)
            .FirstOrDefaultAsync(s => s.Id == screeningId);

        if (screening is null)
        {
            return 0;
        }

        var reservedSeats = await context.Reservations
            .Where(r => r.ScreeningId == screeningId && r.Status != ReservationStatus.Cancelled)
            .SumAsync(r => (int?)r.NumberOfTickets) ?? 0;

        return Math.Max(0, screening.CinemaHall.Capacity - reservedSeats);
    }

    public async Task<(bool Success, string Error, Reservation? Reservation)> CreateReservationAsync(string userId, int screeningId, int ticketCount)
    {
        if (ticketCount <= 0)
        {
            return (false, "Ticket count must be greater than zero.", null);
        }

        var screening = await context.Screenings
            .Include(s => s.CinemaHall)
            .FirstOrDefaultAsync(s => s.Id == screeningId);

        if (screening is null)
        {
            return (false, "Screening not found.", null);
        }

        if (screening.StartTime <= DateTime.UtcNow)
        {
            return (false, "Cannot reserve tickets for past screenings.", null);
        }

        var available = await GetAvailableSeatsAsync(screeningId);
        if (ticketCount > available)
        {
            return (false, "Not enough available seats.", null);
        }

        var reservation = new Reservation
        {
            UserId = userId,
            ScreeningId = screeningId,
            ReservationDate = DateTime.UtcNow,
            NumberOfTickets = ticketCount,
            TotalPrice = screening.Price * ticketCount,
            Status = ReservationStatus.Confirmed
        };

        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        return (true, string.Empty, reservation);
    }

    public Task<bool> CanAccessReservationAsync(Reservation reservation, string currentUserId, bool isAdmin)
    {
        return Task.FromResult(isAdmin || reservation.UserId == currentUserId);
    }

    public async Task<(bool Success, string Error)> CancelReservationAsync(Reservation reservation, string currentUserId, bool isAdmin)
    {
        if (!await CanAccessReservationAsync(reservation, currentUserId, isAdmin))
        {
            return (false, "Forbidden");
        }

        if (reservation.Status == ReservationStatus.Cancelled)
        {
            return (false, "Reservation is already cancelled.");
        }

        reservation.Status = ReservationStatus.Cancelled;
        await context.SaveChangesAsync();
        return (true, string.Empty);
    }
}
