using CinemaWebsite.Models;

namespace CinemaWebsite.Services;

public class SeatStatus
{
    public int SeatId { get; set; }
    public string Row { get; set; } = string.Empty;
    public int Number { get; set; }
    public bool IsReserved { get; set; }
}

public interface IReservationService
{
    Task<int> GetAvailableSeatsAsync(int screeningId);
    Task<(bool Success, string Error, Reservation? Reservation)> CreateReservationAsync(string? userId, int screeningId, int ticketCount);

    // create reservation for explicit seat ids
    Task<(bool Success, string Error, Reservation? Reservation)> CreateReservationAsync(string? userId, int screeningId, IEnumerable<int> seatIds);

    // get seat map for a screening (seat reserved state)
    Task<IEnumerable<SeatStatus>> GetSeatMapAsync(int screeningId);

    Task<bool> CanAccessReservationAsync(Reservation reservation, string? currentUserId, bool isAdmin);
    Task<(bool Success, string Error)> CancelReservationAsync(Reservation reservation, string? currentUserId, bool isAdmin);
}
