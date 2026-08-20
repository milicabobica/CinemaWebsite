using System.ComponentModel.DataAnnotations;

namespace CinemaWebsite.Models;

public enum HallType
{
    Standard,
    IMAX,
    VIP,
    ThreeD,
    FourDX
}

public class CinemaHall
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 100)]
    public int Number { get; set; }

    [Range(1, 5000)]
    public int Capacity { get; set; }

    public HallType HallType { get; set; } = HallType.Standard;

    public string? Description { get; set; }

    public bool IsAvailable { get; set; } = true;

    public ICollection<Seat> Seats { get; set; } = new List<Seat>();

    public ICollection<Screening> Screenings { get; set; } = new List<Screening>();
}