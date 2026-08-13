using CinemaWebsite.Models;

namespace CinemaWebsite.Services;

public interface IReservationService
{
    Task<int> GetAvailableSeatsAsync(int screeningId);
    Task<(bool Success, string Error, Reservation? Reservation)> CreateReservationAsync(string userId, int screeningId, int ticketCount);
    Task<bool> CanAccessReservationAsync(Reservation reservation, string currentUserId, bool isAdmin);
    Task<(bool Success, string Error)> CancelReservationAsync(Reservation reservation, string currentUserId, bool isAdmin);
}
