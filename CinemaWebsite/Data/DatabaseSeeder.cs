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

        if (!context.Genres.Any())
        {
            context.Genres.AddRange(
                new Genre { Name = "Action" },
                new Genre { Name = "Drama" },
                new Genre { Name = "Comedy" },
                new Genre { Name = "Sci-Fi" },
                new Genre { Name = "Thriller" },
                new Genre { Name = "Animation" },
                new Genre { Name = "Adventure" }
            );
            await context.SaveChangesAsync();
        }

        if (!context.CinemaHalls.Any())
        {
            context.CinemaHalls.AddRange(
                new CinemaHall { Name = "Hall 1", Number = 1, Capacity = 120, HallType = HallType.Standard, IsAvailable = true },
                new CinemaHall { Name = "Hall 2", Number = 2, Capacity = 160, HallType = HallType.IMAX, IsAvailable = true },
                new CinemaHall { Name = "Hall 3", Number = 3, Capacity = 90, HallType = HallType.VIP, IsAvailable = true },
                new CinemaHall { Name = "Hall 4", Number = 4, Capacity = 80, HallType = HallType.ThreeD, IsAvailable = true }
            );
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
    }
}