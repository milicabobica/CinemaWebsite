namespace CinemaWebsite.ViewModels;

public class ReservationSeatViewModel
{
    public string Row { get; set; } = string.Empty;
    public int Number { get; set; }

    public string Label => $"{Row}{Number}";
}