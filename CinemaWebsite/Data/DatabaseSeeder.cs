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
        if (!context.Seats.Any())
        {
            var halls = await context.CinemaHalls.ToListAsync();
            const int seatsPerRow = 12;
            foreach (var hall in halls)
            {
                var total = hall.Capacity;
                var rows = (int)Math.Ceiling(total / (double)seatsPerRow);
                for (int r = 0; r < rows && r < 26; r++) // limit to 26 rows (A-Z)
                {
                    var rowChar = ((char)('A' + r)).ToString();
                    for (int n = 1; n <= seatsPerRow; n++)
                    {
                        var seatIndex = r * seatsPerRow + n;
                        if (seatIndex > total) break;
                        context.Seats.Add(new Seat { CinemaHallId = hall.Id, Row = rowChar, Number = n });
                    }
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
