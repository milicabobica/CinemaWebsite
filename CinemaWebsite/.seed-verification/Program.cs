using Microsoft.Data.Sqlite;

using var connection = new SqliteConnection("Data Source=../fresh-catalog.db");
connection.Open();

var checks = new Dictionary<string, string>
{
    ["Migrations"] = "SELECT COUNT(*) FROM __EFMigrationsHistory",
    ["Roles"] = "SELECT COUNT(*) FROM AspNetRoles",
    ["AdminUsers"] = "SELECT COUNT(*) FROM AspNetUsers WHERE Email = 'admin@cineverse.local'",
    ["Genres"] = "SELECT COUNT(*) FROM Genres",
    ["Halls"] = "SELECT COUNT(*) FROM CinemaHalls",
    ["Seats"] = "SELECT COUNT(*) FROM Seats",
    ["Movies"] = "SELECT COUNT(*) FROM Movies",
    ["MovieGenres"] = "SELECT COUNT(*) FROM MovieGenres",
    ["Screenings"] = "SELECT COUNT(*) FROM Screenings",
    ["ForeignKeyViolations"] = "SELECT COUNT(*) FROM pragma_foreign_key_check",
    ["DuplicateMovies"] = "SELECT COUNT(*) FROM (SELECT lower(Title) FROM Movies GROUP BY lower(Title) HAVING COUNT(*) > 1)",
    ["DuplicateScreenings"] = "SELECT COUNT(*) FROM (SELECT MovieId, CinemaHallId, StartTime FROM Screenings GROUP BY MovieId, CinemaHallId, StartTime HAVING COUNT(*) > 1)",
    ["OverlappingScreenings"] = "SELECT COUNT(*) FROM Screenings first JOIN Screenings second ON first.CinemaHallId = second.CinemaHallId AND first.Id < second.Id AND first.StartTime < second.EndTime AND first.EndTime > second.StartTime"
};

foreach (var (name, sql) in checks)
{
    using var command = connection.CreateCommand();
    command.CommandText = sql;
    Console.WriteLine($"{name}={command.ExecuteScalar()}");
}
