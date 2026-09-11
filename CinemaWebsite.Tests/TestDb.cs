using CinemaWebsite.Data;
using CinemaWebsite.Models;
using Microsoft.EntityFrameworkCore;

namespace CinemaWebsite.Tests;

/// <summary>
/// Every test gets its own SQLite file (not the InMemory provider - the concurrency
/// tests need real transaction/locking behavior, which InMemory does not provide).
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly string dbPath;

    public string ConnectionString { get; }
    public int ScreeningId { get; private set; }
    public int SeatId { get; private set; }
    public int OtherSeatId { get; private set; }
    public int CoupleSeatAId { get; private set; }
    public int CoupleSeatBId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string OtherUserId { get; private set; } = string.Empty;

    private TestDb()
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"cinemawebsite-tests-{Guid.NewGuid():N}.db");
        ConnectionString = $"Data Source={dbPath}";
    }

    public ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(ConnectionString).Options);

    public static async Task<TestDb> CreateAsync()
    {
        var db = new TestDb();

        await using var context = db.CreateContext();
        await context.Database.MigrateAsync();

        var hall = new CinemaHall { Name = "Test Hall", Number = new Random().Next(1, 100_000), Capacity = 50 };
        context.CinemaHalls.Add(hall);
        await context.SaveChangesAsync();

        var seat = new Seat { CinemaHallId = hall.Id, Row = "A", Number = 1, Type = SeatType.Standard };
        var otherSeat = new Seat { CinemaHallId = hall.Id, Row = "A", Number = 2, Type = SeatType.Standard };
        var coupleA = new Seat { CinemaHallId = hall.Id, Row = "B", Number = 1, Type = SeatType.Couple };
        var coupleB = new Seat { CinemaHallId = hall.Id, Row = "B", Number = 2, Type = SeatType.Couple };
        context.Seats.AddRange(seat, otherSeat, coupleA, coupleB);
        await context.SaveChangesAsync();

        coupleA.PairSeatId = coupleB.Id;
        coupleB.PairSeatId = coupleA.Id;
        await context.SaveChangesAsync();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "user1@test.local",
            NormalizedUserName = "USER1@TEST.LOCAL",
            Email = "user1@test.local",
            NormalizedEmail = "USER1@TEST.LOCAL",
            FirstName = "Test",
            LastName = "User"
        };
        var otherUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "user2@test.local",
            NormalizedUserName = "USER2@TEST.LOCAL",
            Email = "user2@test.local",
            NormalizedEmail = "USER2@TEST.LOCAL",
            FirstName = "Other",
            LastName = "User"
        };
        context.Users.AddRange(user, otherUser);
        await context.SaveChangesAsync();

        var movie = new Movie
        {
            Title = "Test Movie",
            Description = "d",
            Director = "d",
            Language = "en",
            AgeRating = "PG",
            Duration = 100,
            ReleaseDate = DateTime.UtcNow.AddDays(-1)
        };
        context.Movies.Add(movie);
        await context.SaveChangesAsync();

        var screening = new Screening
        {
            MovieId = movie.Id,
            CinemaHallId = hall.Id,
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(2),
            Price = 10m,
            Language = "en"
        };
        context.Screenings.Add(screening);
        await context.SaveChangesAsync();

        db.ScreeningId = screening.Id;
        db.SeatId = seat.Id;
        db.OtherSeatId = otherSeat.Id;
        db.CoupleSeatAId = coupleA.Id;
        db.CoupleSeatBId = coupleB.Id;
        db.UserId = user.Id;
        db.OtherUserId = otherUser.Id;

        return db;
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (var path in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
