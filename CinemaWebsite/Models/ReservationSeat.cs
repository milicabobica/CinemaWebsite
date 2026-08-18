namespace CinemaWebsite.Models;

public class ReservationSeat
{
    public int ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;

    public int SeatId { get; set; }
    public Seat Seat { get; set; } = null!;
}