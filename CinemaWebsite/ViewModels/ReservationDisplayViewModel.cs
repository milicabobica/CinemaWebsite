using CinemaWebsite.Models;

namespace CinemaWebsite.ViewModels;

public class ReservationDisplayViewModel
{
    public int Id { get; set; }
    public string? CustomerEmail { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string HallName { get; set; } = string.Empty;
    public DateTime ScreeningStartTime { get; set; }
    public int NumberOfTickets { get; set; }
    public decimal TotalPrice { get; set; }
    public ReservationStatus Status { get; set; }
    public List<ReservationSeatViewModel> Seats { get; set; } = new();
}