using CinemaWebsite.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CinemaWebsite.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();

        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var roles = new[] { "Admin", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        const string adminEmail = "admin@cineverse.local";
        var admin = await userManager.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Dev",
                LastName = "Admin",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, "DevAdmin123!");
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("Failed to create development admin account.");
            }
        }

        if (!await userManager.IsInRoleAsync(admin, "Admin"))
        {
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        var existingGenreNames = await context.Genres
            .Select(genre => genre.Name)
            .ToListAsync();
        var genresToAdd = SeedGenreNames
            .Where(name => !existingGenreNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Select(name => new Genre { Name = name })
            .ToList();
        if (genresToAdd.Count > 0)
        {
            context.Genres.AddRange(genresToAdd);
            await context.SaveChangesAsync();
        }

        var existingHallNumbers = await context.CinemaHalls
            .Select(hall => hall.Number)
            .ToListAsync();
        var hallsToAdd = SeedHalls
            .Where(hall => !existingHallNumbers.Contains(hall.Number))
            .Select(hall => new CinemaHall
            {
                Name = hall.Name,
                Number = hall.Number,
                Capacity = hall.Capacity,
                HallType = hall.HallType,
                IsAvailable = true
            })
            .ToList();
        if (hallsToAdd.Count > 0)
        {
            context.CinemaHalls.AddRange(hallsToAdd);
            await context.SaveChangesAsync();
        }

        // seed seats for halls (row/number grid). Uses default 12 seats per row and fills up to hall.Capacity.
        // last row of each hall is seeded as Couple seats (2 seats per couple unit).
        if (!context.Seats.Any())
        {
            var halls = await context.CinemaHalls.ToListAsync();
            const int seatsPerRow = 12;
            foreach (var hall in halls)
            {
                var total = hall.Capacity;
                var rows = (int)Math.Ceiling(total / (double)seatsPerRow);
                var lastRowIndex = Math.Min(rows, 26) - 1;

                for (int r = 0; r < rows && r < 26; r++) // limit to 26 rows (A-Z)
                {
                    var rowChar = ((char)('A' + r)).ToString();
                    var isCoupleRow = r == lastRowIndex;

                    for (int n = 1; n <= seatsPerRow; n++)
                    {
                        var seatIndex = r * seatsPerRow + n;
                        if (seatIndex > total) break;

                        context.Seats.Add(new Seat
                        {
                            CinemaHallId = hall.Id,
                            Row = rowChar,
                            Number = n,
                            Type = isCoupleRow ? SeatType.Couple : SeatType.Standard
                        });
                    }
                }
            }

            await context.SaveChangesAsync();

            // second pass: link couple seats into pairs now that they have real IDs.
            // pairs adjacent seat numbers within the same row: (1,2), (3,4), (5,6)...
            var coupleSeats = await context.Seats
                .Where(s => s.Type == SeatType.Couple)
                .OrderBy(s => s.CinemaHallId)
                .ThenBy(s => s.Row)
                .ThenBy(s => s.Number)
                .ToListAsync();

            var groupedByHallRow = coupleSeats.GroupBy(s => new { s.CinemaHallId, s.Row });
            foreach (var group in groupedByHallRow)
            {
                var seatsInRow = group.OrderBy(s => s.Number).ToList();
                for (int i = 0; i + 1 < seatsInRow.Count; i += 2)
                {
                    var left = seatsInRow[i];
                    var right = seatsInRow[i + 1];
                    left.PairSeatId = right.Id;
                    right.PairSeatId = left.Id;
                }
            }

            await context.SaveChangesAsync();
        }
        
        if (context.Seats.Any() && !context.Seats.Any(s => s.Type == SeatType.Couple))
        {
            var halls = await context.CinemaHalls.ToListAsync();
            const int seatsPerRow = 12;
            foreach (var hall in halls)
            {
                var total = await context.Seats.CountAsync(s => s.CinemaHallId == hall.Id);
                if (total == 0) continue;
                var rows = (int)Math.Ceiling(total / (double)seatsPerRow);
                var lastRowIndex = Math.Min(rows, 26) - 1;
                var rowChar = ((char)('A' + lastRowIndex)).ToString();

                var seatsInLastRow = await context.Seats
                    .Where(s => s.CinemaHallId == hall.Id && s.Row == rowChar)
                    .OrderBy(s => s.Number)
                    .ToListAsync();

                if (!seatsInLastRow.Any()) continue;

                // mark as couple seats
                foreach (var s in seatsInLastRow)
                {
                    s.Type = SeatType.Couple;
                }

                await context.SaveChangesAsync();

                // pair adjacent seats: (1,2), (3,4)...
                for (int i = 0; i + 1 < seatsInLastRow.Count; i += 2)
                {
                    var left = seatsInLastRow[i];
                    var right = seatsInLastRow[i + 1];
                    left.PairSeatId = right.Id;
                    right.PairSeatId = left.Id;
                }

                await context.SaveChangesAsync();
            }
        }

        await SeedMoviesAndScreeningsAsync(context);
    }

    private static async Task SeedMoviesAndScreeningsAsync(ApplicationDbContext context)
    {
        var existingMovieTitles = await context.Movies
            .Select(movie => movie.Title)
            .ToListAsync();
        var moviesToAdd = MovieSeeds
            .Where(seed => !existingMovieTitles.Contains(seed.Title, StringComparer.OrdinalIgnoreCase))
            .Select(seed => new Movie
            {
                Title = seed.Title,
                Description = seed.Description,
                PosterUrl = seed.PosterUrl,
                TrailerUrl = seed.TrailerUrl,
                Duration = seed.Duration,
                ReleaseDate = seed.ReleaseDate,
                Director = seed.Director,
                Cast = seed.Cast,
                Language = seed.Language,
                SubtitleLanguage = seed.SubtitleLanguage,
                AgeRating = seed.AgeRating,
                Rating = seed.Rating
            })
            .ToList();
        if (moviesToAdd.Count > 0)
        {
            context.Movies.AddRange(moviesToAdd);
            await context.SaveChangesAsync();
        }

        var existingMoviesToUpdate = await context.Movies
            .Where(m => existingMovieTitles.Contains(m.Title))
            .ToListAsync();
        var existingMoviesByTitle = existingMoviesToUpdate
            .ToDictionary(m => m.Title, StringComparer.OrdinalIgnoreCase);

        foreach (var seed in MovieSeeds) {
            if (existingMoviesByTitle.TryGetValue(seed.Title, out var existing)) {
                existing.PosterUrl = seed.PosterUrl;
                existing.TrailerUrl = seed.TrailerUrl;
                existing.Description = seed.Description;
            }
        }

        if (context.ChangeTracker.HasChanges()) {
            await context.SaveChangesAsync();
        }
        
        var moviesByTitle = (await context.Movies.ToListAsync())
            .ToDictionary(movie => movie.Title, StringComparer.OrdinalIgnoreCase);
        var genresByName = (await context.Genres.ToListAsync())
            .ToDictionary(genre => genre.Name, StringComparer.OrdinalIgnoreCase);
        var existingMovieGenreKeys = (await context.MovieGenres.ToListAsync())
            .Select(movieGenre => (movieGenre.MovieId, movieGenre.GenreId))
            .ToHashSet();

        foreach (var movieSeed in MovieSeeds)
        {
            var movie = moviesByTitle[movieSeed.Title];
            foreach (var genreName in movieSeed.Genres)
            {
                var genre = genresByName[genreName];
                if (existingMovieGenreKeys.Add((movie.Id, genre.Id)))
                {
                    context.MovieGenres.Add(new MovieGenre
                    {
                        MovieId = movie.Id,
                        GenreId = genre.Id
                    });
                }
            }
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }

        await SeedScreeningsAsync(context, moviesByTitle);
    }

    private static async Task SeedScreeningsAsync(
        ApplicationDbContext context,
        IReadOnlyDictionary<string, Movie> moviesByTitle)
    {
        var hallsByNumber = (await context.CinemaHalls.ToListAsync())
            .ToDictionary(hall => hall.Number);
        var scheduleDate = DateTime.Today.AddDays(1);
        var scheduleEnd = scheduleDate.AddDays(1);
        var existingScreenings = await context.Screenings
            .Where(screening => screening.StartTime < scheduleEnd && screening.EndTime > scheduleDate)
            .ToListAsync();

        foreach (var screeningSeed in ScreeningSeeds)
        {
            var movie = moviesByTitle[screeningSeed.MovieTitle];
            var hall = hallsByNumber[screeningSeed.HallNumber];
            var startTime = scheduleDate.Add(screeningSeed.StartTime.ToTimeSpan());
            var endTime = startTime.AddMinutes(movie.Duration);

            var matchingScreeningExists = existingScreenings.Any(screening =>
                screening.MovieId == movie.Id &&
                screening.CinemaHallId == hall.Id &&
                screening.StartTime == startTime);
            if (matchingScreeningExists)
            {
                continue;
            }

            var overlapsExistingScreening = existingScreenings.Any(screening =>
                screening.CinemaHallId == hall.Id &&
                screening.StartTime < endTime &&
                screening.EndTime > startTime);
            if (overlapsExistingScreening)
            {
                continue;
            }

            var screening = new Screening
            {
                MovieId = movie.Id,
                CinemaHallId = hall.Id,
                StartTime = startTime,
                EndTime = endTime,
                Price = screeningSeed.Price,
                Language = movie.Language,
                SubtitleLanguage = movie.SubtitleLanguage
            };
            context.Screenings.Add(screening);
            existingScreenings.Add(screening);
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }

    private static readonly string[] SeedGenreNames =
    [
        "Action",
        "Drama",
        "Comedy",
        "Sci-Fi",
        "Thriller",
        "Animation",
        "Adventure"
    ];

    private static readonly CinemaHallSeed[] SeedHalls =
    [
        new("Hall 1", 1, 120, HallType.Standard),
        new("Hall 2", 2, 160, HallType.IMAX),
        new("Hall 3", 3, 90, HallType.VIP),
        new("Hall 4", 4, 80, HallType.ThreeD)
    ];

    private static readonly MovieSeed[] MovieSeeds =
    [
        new(
            "Spider-Man: Brand New Day",
            "Peter Parker faces a dangerous new enemy as he tries to build a life after the world forgot who he is.",
            "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcTix1JTOha0Q8jsn-Lgzm3ypzfTYkdEAdJ2ZP-YKemZBQ&s=10",
            "https://youtu.be/8TZMtslA3UY?si=KKQoj2o9rCFqct6E",
            145,
            new DateTime(2026, 7, 31),
            "Destin Daniel Cretton",
            "Tom Holland, Zendaya, Mark Ruffalo, Sadie Sink, Jacob Batalon",
            "English",
            "Macedonian",
            "PG-13",
            8.2,
            ["Action", "Adventure", "Sci-Fi"]),
        new(
            "Obsession",
            "A hopeless romantic gets his wish for love, only to find that the supernatural bargain turns sinister.",
            "https://www.dolby.com/globalassets/cinema/obsession_focus-dolby_ka_1080x1600_r05_13_fin02-2.jpg",
            "https://youtu.be/gMC8kkwbIQQ?si=piZcSOvweJ7jJLo5",
            109,
            new DateTime(2026, 5, 15),
            "Curry Barker",
            "Michael Johnston, Inde Navarrette, Cooper Tomlinson, Megan Lawless",
            "English",
            "Macedonian",
            "R",
            7.9,
            ["Drama", "Thriller"]),
        new(
            "The Super Mario Galaxy Movie",
            "Mario journeys into space to explore cosmic worlds and face a new galactic challenge.",
            "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcSRPeIY9GblWzgYRrVY9hGWdCXPZoyvjerKflUeSLEQgg&s=10",
            "https://youtu.be/GuCejewteF8?si=3MgYOYJcuDNRtom2",
            98,
            new DateTime(2026, 4, 3),
            "Aaron Horvath, Michael Jelenic, Pierre Leduc",
            "Chris Pratt, Anya Taylor-Joy, Charlie Day, Jack Black, Keegan-Michael Key",
            "English",
            "Macedonian",
            "PG",
            6.3,
            ["Animation", "Comedy", "Adventure"]),
        new(
            "28 Years Later: The Bone Temple",
            "As Spike joins Jimmy Crystal's gang, Dr. Kelson makes a discovery that could alter the world.",
            "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcRg6CLZkNbr_jxq-JdM_bsnPBc4ly2Dgi1LBTDa--v1gA&s=10",
            "https://youtu.be/EOwTdTZA8D8?si=MnkQkzJ_qU_sly3z",
            109,
            new DateTime(2026, 1, 16),
            "Nia DaCosta",
            "Jack O'Connell, Alfie Williams, Connor Newall, Ralph Fiennes",
            "English",
            "Macedonian",
            "R",
            7.2,
            ["Sci-Fi", "Thriller"]),
        new(
            "Sinners",
            "Twin brothers return to their Mississippi hometown to open a juke joint, where a supernatural threat awaits.",
            "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcQpshglnUmTMiFVzHwivz0ZEi8qS2n7KS9yiE8nstI0Xg&s",
            "https://youtu.be/bKGxHflevuk?si=eS7IIv4T1o54jxZr",
            137,
            new DateTime(2025, 4, 18),
            "Ryan Coogler",
            "Michael B. Jordan, Hailee Steinfeld, Miles Caton, Jack O'Connell",
            "English",
            "Macedonian",
            "R",
            7.6,
            ["Drama", "Thriller"]),
        new(
            "Superman",
            "Clark Kent works to reconcile his Kryptonian heritage with the human values that shaped him.",
            "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcQL9NsMcmw27U1CFPV7B_LR5uArh_J6fW9t-RyGsn6UQA&s=10",
            "https://youtu.be/Ox8ZLF6cGM0?si=v8xKaB0BHcjLnCXi",
            129,
            new DateTime(2025, 7, 11),
            "James Gunn",
            "David Corenswet, Rachel Brosnahan, Nicholas Hoult, Edi Gathegi",
            "English",
            "Macedonian",
            "PG-13",
            7.1,
            ["Action", "Adventure", "Sci-Fi"]),
        new(
            "Mission: Impossible - The Final Reckoning",
            "Ethan Hunt and his team race to stop a malevolent artificial intelligence from destroying the world.",
            "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcTNhksm2lJqJgWJx7TehizJZ8r-C0hmj8xGRLI7O3OdiA&s=10",
            "https://youtu.be/fsQgc9pCyDU?si=Hd7J-LIVZlx7zbDE",
            170,
            new DateTime(2025, 5, 23),
            "Christopher McQuarrie",
            "Tom Cruise, Hayley Atwell, Ving Rhames, Simon Pegg",
            "English",
            "Macedonian",
            "PG-13",
            7.3,
            ["Action", "Adventure", "Thriller"]),
        new(
            "F1 The Movie",
            "A former Formula One driver returns to the grid to help an underdog team compete at the highest level.",
            "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcSJErwO84LfxkNk9N-cuaggTexkeoGkC-qlbaE3oyPb1A&s=10",
            "https://youtu.be/8yh9BPUBbbQ?si=gkoDiyqvOPPPRc9x",
            155,
            new DateTime(2025, 6, 27),
            "Joseph Kosinski",
            "Brad Pitt, Damson Idris, Kerry Condon, Javier Bardem",
            "English",
            "Macedonian",
            "PG-13",
            7.8,
            ["Action", "Drama"]),
        new(
            "Jurassic World Rebirth",
            "A covert team travels to an isolated island to secure dinosaur DNA with lifesaving potential.",
            "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcRw4MaySNvMV_cA3wh0FsTb-E8g-euL7tv-CFZG-WVtKg&s=10",
            "https://www.youtube.com/watch?v=jan5CFWs9ic",
            133,
            new DateTime(2025, 7, 2),
            "Gareth Edwards",
            "Scarlett Johansson, Mahershala Ali, Jonathan Bailey, Rupert Friend",
            "English",
            "Macedonian",
            "PG-13",
            5.9,
            ["Action", "Adventure", "Sci-Fi"]),
        new(
            "How to Train Your Dragon",
            "A young Viking forms an unexpected friendship with a dragon and challenges generations of tradition.",
            "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcS9KgwTgm6PBrD7HpGAidEYYTx9mn2LuJ2195ZfZGbXiA&s=10",
            "https://youtu.be/22w7z_lT6YM?si=9d75IqKY3EKuk8cs",
            125,
            new DateTime(2025, 6, 13),
            "Dean DeBlois",
            "Mason Thames, Nico Parker, Gerard Butler, Nick Frost",
            "English",
            "Macedonian",
            "PG",
            7.8,
            ["Action", "Adventure"]),
        new(
            "Lilo & Stitch",
            "A lonely Hawaiian girl befriends a mischievous alien fugitive and discovers the meaning of family.",
            "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcTKzV2BwcxU0tkgczbZ-eCzbxT1eWlb0Qpmlw3l4ak-hQ&s=10",
            "https://youtu.be/VWqJifMMgZE?si=FxviAkfPRL21q0VF",
            108,
            new DateTime(2025, 5, 23),
            "Dean Fleischer Camp",
            "Maia Kealoha, Sydney Agudong, Chris Sanders, Zach Galifianakis",
            "English",
            "Macedonian",
            "PG",
            6.7,
            ["Comedy", "Adventure"]),
        new(
            "The Fantastic Four: First Steps",
            "Marvel's first family must protect their world from an intergalactic threat.",
            "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcT4Fmky6DTsjNM3Ug1ZYjaShsCI0Y3y6eAdfOmrkNSoIbU6G_3juKpjJR-m&s=10",
            "https://youtu.be/pAsmrKyMqaA?si=NCG20BTz50dphQnm",
            115,
            new DateTime(2025, 7, 25),
            "Matt Shakman",
            "Pedro Pascal, Vanessa Kirby, Joseph Quinn, Ebon Moss-Bachrach",
            "English",
            "Macedonian",
            "PG-13",
            7.0,
            ["Action", "Adventure", "Sci-Fi"])
    ];

    private static readonly ScreeningSeed[] ScreeningSeeds =
    [
        new("Lilo & Stitch", 1, new TimeOnly(10, 0), 7.50m),
        new("The Super Mario Galaxy Movie", 1, new TimeOnly(12, 15), 7.50m),
        new("Spider-Man: Brand New Day", 1, new TimeOnly(14, 20), 7.50m),
        new("Obsession", 1, new TimeOnly(17, 15), 7.50m),
        new("Mission: Impossible - The Final Reckoning", 1, new TimeOnly(19, 30), 7.50m),

        new("F1 The Movie", 2, new TimeOnly(10, 0), 10.50m),
        new("Jurassic World Rebirth", 2, new TimeOnly(13, 10), 10.50m),
        new("Spider-Man: Brand New Day", 2, new TimeOnly(16, 0), 10.50m),
        new("Mission: Impossible - The Final Reckoning", 2, new TimeOnly(19, 0), 10.50m),
        new("Obsession", 2, new TimeOnly(22, 15), 10.50m),

        new("The Fantastic Four: First Steps", 3, new TimeOnly(10, 30), 12.00m),
        new("Sinners", 3, new TimeOnly(13, 0), 12.00m),
        new("28 Years Later: The Bone Temple", 3, new TimeOnly(16, 0), 12.00m),
        new("Superman", 3, new TimeOnly(18, 30), 12.00m),
        new("Obsession", 3, new TimeOnly(21, 15), 12.00m),

        new("The Super Mario Galaxy Movie", 4, new TimeOnly(10, 15), 9.00m),
        new("How to Train Your Dragon", 4, new TimeOnly(12, 30), 9.00m),
        new("Jurassic World Rebirth", 4, new TimeOnly(15, 0), 9.00m),
        new("The Fantastic Four: First Steps", 4, new TimeOnly(17, 45), 9.00m),
        new("Spider-Man: Brand New Day", 4, new TimeOnly(20, 15), 9.00m)
    ];

    private sealed record CinemaHallSeed(string Name, int Number, int Capacity, HallType HallType);

    private sealed record MovieSeed(
        string Title,
        string Description,
        string? PosterUrl,
        string? TrailerUrl,
        int Duration,
        DateTime ReleaseDate,
        string Director,
        string Cast,
        string Language,
        string? SubtitleLanguage,
        string AgeRating,
        double Rating,
        string[] Genres);

    private sealed record ScreeningSeed(string MovieTitle, int HallNumber, TimeOnly StartTime, decimal Price);
}
