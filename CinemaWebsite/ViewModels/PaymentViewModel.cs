namespace CinemaWebsite.ViewModels;

using System.ComponentModel.DataAnnotations;


public class PaymentViewModel
{
    public int ReservationId { get; set; }

    public string? AccessToken { get; set; } = string.Empty;

    [Display(Name = "Email")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public bool IsEmailRequired { get; set; }

    [Required]
    [Display(Name = "Cardholder Name")]
    public string CardholderName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Card Number")]
    [RegularExpression(@"^\d{16}$", ErrorMessage = "Card number must be 16 digits.")]
    public string CardNumber { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Expiration Date (MM/YY)")]
    [RegularExpression(@"^(0[1-9]|1[0-2])\/\d{2}$", ErrorMessage = "Use MM/YY format.")]
    public string ExpirationDate { get; set; } = string.Empty;

    [Required]
    [Display(Name = "CVV")]
    [RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVV must be 3 or 4 digits.")]
    public string Cvv { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Billing Address")]
    public string BillingAddress { get; set; } = string.Empty;

    public decimal TotalPrice { get; set; }

    public string MovieTitle { get; set; } = string.Empty;

    public string HallName { get; set; } = string.Empty;

    public DateTime ScreeningStartTime { get; set; }

    public List<ReservationSeatViewModel> Seats { get; set; } = new();
}