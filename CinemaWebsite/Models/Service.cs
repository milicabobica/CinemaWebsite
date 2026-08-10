using System.ComponentModel.DataAnnotations;

namespace CinemaWebsite.Models;

public class Service
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;
}