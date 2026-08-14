using System.ComponentModel.DataAnnotations;

namespace CinemaWebsite.Models;

public enum ReservationStatus
{
    Pending,
    Confirmed,
    Cancelled
}

public class Reservation
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    [EmailAddress]
    public string? ReservationEmail { get; set; }

    public ApplicationUser? User { get; set; }

    [Required]
    public int ScreeningId { get; set; }

    public Screening Screening { get; set; } = null!;

    [Required]
    public DateTime ReservationDate { get; set; } = DateTime.UtcNow;

    [Range(1, 100)]
    public int NumberOfTickets { get; set; }

    [Range(0, 100000)] public decimal TotalPrice { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
}