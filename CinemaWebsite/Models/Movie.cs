using System.ComponentModel.DataAnnotations;

namespace CinemaWebsite.Models;

// public enum MovieStatus
// {
//     NowShowing,
//     ComingSoon,
//     Finished
// }

public class Movie
{
    public int Id { get; set; }

    [Microsoft.Build.Framework.Required]
    [StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Microsoft.Build.Framework.Required]
    public string Description { get; set; } = string.Empty;

    public string? PosterUrl { get; set; }

    public string? TrailerUrl { get; set; }

    [Range(1, 500)]
    public int Duration { get; set; }

    [Microsoft.Build.Framework.Required]
    public DateTime ReleaseDate { get; set; }

    [Microsoft.Build.Framework.Required]
    [StringLength(100)]
    public string Director { get; set; } = string.Empty;

    public string? Cast { get; set; }

    [Microsoft.Build.Framework.Required]
    [StringLength(50)]
    public string Language { get; set; } = string.Empty;

    [StringLength(50)]
    public string? SubtitleLanguage { get; set; }

    [Microsoft.Build.Framework.Required]
    [StringLength(20)]
    public string AgeRating { get; set; } = string.Empty;

    [Range(0, 10)]
    public double Rating { get; set; }

    // public MovieStatus Status { get; set; } = MovieStatus.ComingSoon;

    public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();

    public ICollection<Screening> Screenings { get; set; } = new List<Screening>();
}