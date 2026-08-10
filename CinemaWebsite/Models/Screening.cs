using System.ComponentModel.DataAnnotations;

namespace CinemaWebsite.Models;

public class Screening
{
    public int Id { get; set; }

    [Required]
    public int MovieId { get; set; }

    public Movie Movie { get; set; } = null!;

    [Required]
    public int CinemaHallId { get; set; }

    public CinemaHall CinemaHall { get; set; } = null!;

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    [Range(0.01, 10000)]
    public decimal Price { get; set; }

    [Required]
    [StringLength(50)]
    public string Language { get; set; } = string.Empty;

    [StringLength(50)]
    public string? SubtitleLanguage { get; set; }

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}