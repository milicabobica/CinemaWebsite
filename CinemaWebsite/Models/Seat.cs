using System.ComponentModel.DataAnnotations;

namespace CinemaWebsite.Models;

public class Seat
{
    public int Id { get; set; }

    [Required]
    public int CinemaHallId { get; set; }
    public CinemaHall CinemaHall { get; set; } = null!;

    [StringLength(2)]
    public string Row { get; set; } = string.Empty; // e.g. "A"

    public int Number { get; set; } // e.g. 1, 2, 3

    public bool IsAvailable { get; set; } = true;

    public ICollection<ReservationSeat> ReservationSeats { get; set; } = new List<ReservationSeat>();
}