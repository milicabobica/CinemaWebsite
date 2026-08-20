namespace CinemaWebsite.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalMovies { get; set; }
    public int NowShowingMovies { get; set; }
    public int ComingSoonMovies { get; set; }
    public int TotalGenres { get; set; }
    public int TotalHalls { get; set; }
    public int TotalScreenings { get; set; }
    public int TotalReservations { get; set; }
    public int ConfirmedReservations { get; set; }
    public int CancelledReservations { get; set; }
    public int UnreadContactMessages { get; set; }
    public int PublishedNews { get; set; }
}
