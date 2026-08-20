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

        var reservedSeats = await context.ReservationSeats
            .Where(rs => rs.Reservation.ScreeningId == screeningId && rs.Reservation.Status != ReservationStatus.Cancelled)
            .CountAsync();

        return Math.Max(0, screening.CinemaHall.Capacity - reservedSeats);
    }

    public async Task<IEnumerable<SeatStatus>> GetSeatMapAsync(int screeningId)
    {
        var screening = await context.Screenings
            .Include(s => s.CinemaHall)
            .FirstOrDefaultAsync(s => s.Id == screeningId);
        if (screening is null) return Enumerable.Empty<SeatStatus>();

        var hallId = screening.CinemaHallId;

        var seats = await context.Seats
            .Where(s => s.CinemaHallId == hallId)
            .OrderBy(s => s.Row)
            .ThenBy(s => s.Number)
            .ToListAsync();

        var reservedSeatIds = await context.ReservationSeats
            .Where(rs => rs.Reservation.ScreeningId == screeningId && rs.Reservation.Status != ReservationStatus.Cancelled)
            .Select(rs => rs.SeatId)
            .ToListAsync();

        return seats.Select(s => new SeatStatus
        {
            SeatId = s.Id,
            Row = s.Row,
            Number = s.Number,
            IsReserved = reservedSeatIds.Contains(s.Id),
            SeatType = s.Type,
            PairSeatId = s.PairSeatId
        });
    }

    public async Task<(bool Success, string Error, Reservation? Reservation)> CreateReservationAsync(string? userId, int screeningId, IEnumerable<int> seatIds)
    {
        var seatList = seatIds?.ToList() ?? new List<int>();
        if (!seatList.Any())
        {
            return (false, "No seats selected.", null);
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

        // validate seats belong to hall
        var seats = await context.Seats.Where(s => seatList.Contains(s.Id)).ToListAsync();
        if (seats.Count != seatList.Count)
        {
            return (false, "One or more selected seats do not exist.", null);
        }

        if (seats.Any(s => s.CinemaHallId != screening.CinemaHallId))
        {
            return (false, "One or more selected seats do not belong to the screening hall.", null);
        }

        // Check availability atomically
        using var tx = await context.Database.BeginTransactionAsync();
        try
        {
            var alreadyReserved = await context.ReservationSeats
                .Where(rs => seatList.Contains(rs.SeatId) && rs.Reservation.ScreeningId == screeningId && rs.Reservation.Status != ReservationStatus.Cancelled)
                .Select(rs => rs.SeatId)
                .ToListAsync();

            if (alreadyReserved.Any())
            {
                await tx.RollbackAsync();
                return (false, "One or more selected seats are already reserved.", null);
            }

            var reservation = new Reservation
            {
                UserId = userId,
                ScreeningId = screeningId,
                ReservationDate = DateTime.UtcNow,
                NumberOfTickets = seatList.Count,
                TotalPrice = screening.Price * seatList.Count,
                Status = ReservationStatus.Pending
            };

            context.Reservations.Add(reservation);
            await context.SaveChangesAsync();

            foreach (var sid in seatList)
            {
                context.ReservationSeats.Add(new ReservationSeat { ReservationId = reservation.Id, SeatId = sid });
            }

            await context.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, string.Empty, reservation);
        }
        catch
        {
            await tx.RollbackAsync();
            return (false, "Failed to create reservation.", null);
        }
    }
    
    public async Task<(bool Success, string Error, Reservation? Reservation)> CreateReservationAsync(string? userId, int screeningId, int ticketCount)
    {
        if (ticketCount <= 0)
        {
            return (false, "Ticket count must be greater than zero.", null);
        }

        var seatMap = (await GetSeatMapAsync(screeningId)).Where(s => !s.IsReserved).ToList();
        if (seatMap.Count < ticketCount)
        {
            return (false, "Not enough available seats.", null);
        }

        var seatIds = seatMap.Take(ticketCount).Select(s => s.SeatId);
        return await CreateReservationAsync(userId, screeningId, seatIds);
    }

    public Task<bool> CanAccessReservationAsync(Reservation reservation, string? currentUserId, bool isAdmin)
    {
        if (isAdmin)
        {
            return Task.FromResult(true);
        }

        if (string.IsNullOrWhiteSpace(currentUserId) || string.IsNullOrWhiteSpace(reservation.UserId))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(reservation.UserId == currentUserId);
    }

    public async Task<(bool Success, string Error)> CancelReservationAsync(Reservation reservation, string? currentUserId, bool isAdmin)
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