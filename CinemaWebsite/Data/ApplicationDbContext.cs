using CinemaWebsite.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CinemaWebsite.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Movie> Movies { get; set; }
    public DbSet<Genre> Genres { get; set; }
    public DbSet<MovieGenre> MovieGenres { get; set; }
    public DbSet<CinemaHall> CinemaHalls { get; set; }
    public DbSet<Screening> Screenings { get; set; }
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<News> News { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<ContactMessage> ContactMessages { get; set; }
    public DbSet<Seat> Seats { get; set; }
    public DbSet<ReservationSeat> ReservationSeats { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Movie ↔ Genre
        builder.Entity<MovieGenre>()
            .HasKey(mg => new { mg.MovieId, mg.GenreId });

        builder.Entity<MovieGenre>()
            .HasOne(mg => mg.Movie)
            .WithMany(m => m.MovieGenres)
            .HasForeignKey(mg => mg.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MovieGenre>()
            .HasOne(mg => mg.Genre)
            .WithMany(g => g.MovieGenres)
            .HasForeignKey(mg => mg.GenreId)
            .OnDelete(DeleteBehavior.Cascade);

        // Movie → Screening
        builder.Entity<Screening>()
            .HasOne(s => s.Movie)
            .WithMany(m => m.Screenings)
            .HasForeignKey(s => s.MovieId)
            .OnDelete(DeleteBehavior.Restrict);

        // CinemaHall → Screening
        builder.Entity<Screening>()
            .HasOne(s => s.CinemaHall)
            .WithMany(h => h.Screenings)
            .HasForeignKey(s => s.CinemaHallId)
            .OnDelete(DeleteBehavior.Restrict);

        // ApplicationUser → Reservation
        builder.Entity<Reservation>()
            .HasOne(r => r.User)
            .WithMany(u => u.Reservations)
            .HasForeignKey(r => r.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Screening → Reservation
        builder.Entity<Reservation>()
            .HasOne(r => r.Screening)
            .WithMany(s => s.Reservations)
            .HasForeignKey(r => r.ScreeningId)
            .OnDelete(DeleteBehavior.Restrict);

        // Reservation ↔ Seat (many-to-many)
        builder.Entity<ReservationSeat>()
            .HasKey(rs => new { rs.ReservationId, rs.SeatId });

        builder.Entity<ReservationSeat>()
            .HasOne(rs => rs.Reservation)
            .WithMany(r => r.ReservationSeats)
            .HasForeignKey(rs => rs.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ReservationSeat>()
            .HasOne(rs => rs.Seat)
            .WithMany(s => s.ReservationSeats)
            .HasForeignKey(rs => rs.SeatId)
            .OnDelete(DeleteBehavior.Restrict);

        // CinemaHall → Seat
        builder.Entity<Seat>()
            .HasOne(s => s.CinemaHall)
            .WithMany(h => h.Seats)
            .HasForeignKey(s => s.CinemaHallId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cinema Hall number should be unique
        builder.Entity<CinemaHall>()
            .HasIndex(h => h.Number)
            .IsUnique();

        // Decimal precision
        builder.Entity<Screening>()
            .Property(s => s.Price)
            .HasPrecision(10, 2);

        builder.Entity<Reservation>()
            .Property(r => r.TotalPrice)
            .HasPrecision(10, 2);
    }
}