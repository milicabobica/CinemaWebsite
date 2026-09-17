using CinemaWebsite.Controllers;
using CinemaWebsite.Models;
using CinemaWebsite.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CinemaWebsite.Tests;

public class ScreeningTimeTests
{
    [Theory]
    [InlineData(1, 8)]
    [InlineData(9, 9)]
    public void CinemaTime_UsesSkopjeWinterAndSummerOffsets(int month, int localHour)
    {
        var utc = new DateTimeOffset(2026, month, 17, 7, 35, 0, TimeSpan.Zero);

        var local = CinemaTime.FromUtc(utc);

        Assert.Equal(new DateTime(2026, month, 17, localHour, 35, 0), local);
        Assert.Equal(DateTimeKind.Unspecified, local.Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_StartedLocalScreening_RedirectsWithMessage(bool post)
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateContext();
        var screening = await context.Screenings.SingleAsync(s => s.Id == db.ScreeningId);
        screening.StartTime = CinemaTime.Now.AddMinutes(-1);
        await context.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(context);
        services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<CinemaWebsite.Data.ApplicationDbContext>();
        using var provider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext();
        var controller = new ReservationsController(context, new ReservationService(context),
            provider.GetRequiredService<UserManager<ApplicationUser>>(), new EphemeralDataProtectionProvider())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new MemoryTempDataProvider())
        };

        var result = post
            ? await controller.Create(new Reservation { ScreeningId = db.ScreeningId, NumberOfTickets = 1 }, [db.SeatId])
            : await controller.Create(db.ScreeningId);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Movies", redirect.ControllerName);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal(screening.MovieId, redirect.RouteValues!["id"]);
        Assert.Equal("This screening has already started", controller.TempData["ScreeningStartedMessage"]);
        Assert.Empty(await context.Reservations.ToListAsync());
    }

    [Fact]
    public async Task MovieDetails_ExcludesStartedLocalScreeningAndKeepsFutureScreening()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateContext();
        var started = await context.Screenings.SingleAsync(s => s.Id == db.ScreeningId);
        started.StartTime = CinemaTime.Now.AddMinutes(-1);
        var upcoming = new Screening
        {
            MovieId = started.MovieId,
            CinemaHallId = started.CinemaHallId,
            StartTime = CinemaTime.Now.AddHours(3),
            EndTime = CinemaTime.Now.AddHours(5),
            Price = started.Price,
            Language = started.Language
        };
        context.Screenings.Add(upcoming);
        await context.SaveChangesAsync();

        var controller = new MoviesController(context);
        var result = Assert.IsType<ViewResult>(await controller.Details(started.MovieId, null));
        var movie = Assert.IsType<Movie>(result.Model);

        Assert.Equal(upcoming.Id, Assert.Single(movie.Screenings).Id);
    }

    private sealed class MemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
