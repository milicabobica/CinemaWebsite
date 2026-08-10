using System.ComponentModel.DataAnnotations;

namespace CinemaWebsite.Models;
//najava na glavna strana
public class News
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Summary { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    [Required]
    public DateTime PublishedDate { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(100)]
    public string Author { get; set; } = string.Empty;

    public bool IsPublished { get; set; } = false;
}