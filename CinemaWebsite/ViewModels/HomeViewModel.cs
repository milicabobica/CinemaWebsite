using CinemaWebsite.Models;

namespace CinemaWebsite.ViewModels;

public class HomeViewModel
{
    public List<Movie> NowShowingMovies { get; set; } = [];
    public List<Movie> ComingSoonMovies { get; set; } = [];
    public List<News> LatestNews { get; set; } = [];
    public List<Service> Services { get; set; } = [];
    public int CinemaHallCount { get; set; }
    public int MovieCount { get; set; }
}
