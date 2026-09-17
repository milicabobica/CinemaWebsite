using CinemaWebsite.Models;
using CinemaWebsite.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CinemaWebsite.Tests;

public class ReservationServiceTests
{
    [Theory]
    [InlineData(-1, false)]
    [InlineData(5, true)]
    public async Task CreateReservationAsync_UsesCinemaLocalStartTime(int minutesFromNow, bool expectedSuccess)
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateContext();
        var screening = await context.Screenings.SingleAsync(s => s.Id == db.ScreeningId);
        screening.StartTime = CinemaTime.Now.AddMinutes(minutesFromNow);
        screening.EndTime = screening.StartTime.AddHours(2);
        await context.SaveChangesAsync();

        var service = new ReservationService(context);
        var result = await service.CreateReservationAsync(null, db.ScreeningId, [db.SeatId]);

        Assert.Equal(expectedSuccess, result.Success);
        Assert.Equal(expectedSuccess ? 1 : 0, await context.Reservations.CountAsync());
        Assert.Equal(expectedSuccess ? 1 : 0, await context.ReservationSeats.CountAsync());
    }

    [Fact]
    public async Task CreateReservationAsync_ConcurrentRequestsForSameSeat_OnlyOneSucceeds()
    {
        using var db = await TestDb.CreateAsync();

        const int attempts = 8;
        var tasks = Enumerable.Range(0, attempts).Select(async _ =>
        {
            await using var context = db.CreateContext();
            var service = new ReservationService(context);
            return await service.CreateReservationAsync(userId: null, screeningId: db.ScreeningId, seatIds: [db.SeatId]);
        });

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Equal(attempts - 1, results.Count(r => !r.Success && r.Error == "One or more selected seats are already reserved."));

        await using var verifyContext = db.CreateContext();
        var reservationSeatCount = await verifyContext.ReservationSeats.CountAsync(rs => rs.SeatId == db.SeatId);
        Assert.Equal(1, reservationSeatCount);
    }

    [Fact]
    public async Task CreateReservationAsync_SeatAlreadyConfirmed_IsRejected()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateContext();
        var service = new ReservationService(context);

        var first = await service.CreateReservationAsync(userId: null, screeningId: db.ScreeningId, seatIds: [db.SeatId]);
        Assert.True(first.Success);
        first.Reservation!.Status = ReservationStatus.Confirmed;
        await context.SaveChangesAsync();

        var second = await service.CreateReservationAsync(userId: null, screeningId: db.ScreeningId, seatIds: [db.SeatId]);

        Assert.False(second.Success);
        Assert.Equal("One or more selected seats are already reserved.", second.Error);
    }

    [Fact]
    public async Task ExpirePendingReservationsAsync_ReleasesExpiredPendingReservation()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateContext();
        var service = new ReservationService(context);

        var result = await service.CreateReservationAsync(userId: null, screeningId: db.ScreeningId, seatIds: [db.SeatId]);
        Assert.True(result.Success);

        result.Reservation!.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await context.SaveChangesAsync();

        var expiredCount = await service.ExpirePendingReservationsAsync();
        Assert.Equal(1, expiredCount);

        await context.Entry(result.Reservation).ReloadAsync();
        Assert.Equal(ReservationStatus.Cancelled, result.Reservation.Status);

        var seatMap = (await service.GetSeatMapAsync(db.ScreeningId)).ToList();
        var seat = seatMap.Single(s => s.SeatId == db.SeatId);
        Assert.False(seat.IsReserved);
    }

    [Fact]
    public async Task ExpirePendingReservationsAsync_LeavesNonExpiredPendingReservationAlone()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateContext();
        var service = new ReservationService(context);

        var result = await service.CreateReservationAsync(userId: null, screeningId: db.ScreeningId, seatIds: [db.SeatId]);
        Assert.True(result.Success);

        var expiredCount = await service.ExpirePendingReservationsAsync();

        Assert.Equal(0, expiredCount);
        await context.Entry(result.Reservation!).ReloadAsync();
        Assert.Equal(ReservationStatus.Pending, result.Reservation!.Status);
    }

    [Fact]
    public async Task CancelReservationAsync_RestoresSeatAvailability()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateContext();
        var service = new ReservationService(context);

        var created = await service.CreateReservationAsync(userId: db.UserId, screeningId: db.ScreeningId, seatIds: [db.SeatId]);
        Assert.True(created.Success);

        var seatMapBefore = (await service.GetSeatMapAsync(db.ScreeningId)).ToList();
        Assert.True(seatMapBefore.Single(s => s.SeatId == db.SeatId).IsReserved);

        var cancelResult = await service.CancelReservationAsync(created.Reservation!, currentUserId: db.UserId, isAdmin: false);
        Assert.True(cancelResult.Success);

        var seatMapAfter = (await service.GetSeatMapAsync(db.ScreeningId)).ToList();
        Assert.False(seatMapAfter.Single(s => s.SeatId == db.SeatId).IsReserved);
    }

    [Fact]
    public async Task CancelReservationAsync_AlreadyCancelled_ReturnsFriendlyError()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateContext();
        var service = new ReservationService(context);

        var created = await service.CreateReservationAsync(userId: db.UserId, screeningId: db.ScreeningId, seatIds: [db.SeatId]);
        Assert.True(created.Success);
        await service.CancelReservationAsync(created.Reservation!, db.UserId, isAdmin: false);

        var secondCancel = await service.CancelReservationAsync(created.Reservation!, db.UserId, isAdmin: false);

        Assert.False(secondCancel.Success);
        Assert.Equal("Reservation is already cancelled.", secondCancel.Error);
    }

    [Fact]
    public async Task CancelReservationAsync_WrongUser_IsForbidden()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateContext();
        var service = new ReservationService(context);

        var created = await service.CreateReservationAsync(userId: db.UserId, screeningId: db.ScreeningId, seatIds: [db.SeatId]);
        Assert.True(created.Success);

        var result = await service.CancelReservationAsync(created.Reservation!, currentUserId: db.OtherUserId, isAdmin: false);

        Assert.False(result.Success);
        Assert.Equal("Forbidden", result.Error);
    }

    [Fact]
    public async Task GetSeatMapAsync_SurfacesPairSeatId_ForCoupleSeats()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateContext();
        var service = new ReservationService(context);

        var seatMap = (await service.GetSeatMapAsync(db.ScreeningId)).ToList();

        var seatA = seatMap.Single(s => s.SeatId == db.CoupleSeatAId);
        var seatB = seatMap.Single(s => s.SeatId == db.CoupleSeatBId);
        Assert.Equal(SeatType.Couple, seatA.SeatType);
        Assert.Equal(db.CoupleSeatBId, seatA.PairSeatId);
        Assert.Equal(db.CoupleSeatAId, seatB.PairSeatId);
    }

    /// <summary>
    /// Characterization test: "selecting one couple seat must take both" is a client-side
    /// behavior in seatmap.js only. CreateReservationAsync never reads PairSeatId, so nothing
    /// stops a caller (or a client that skips the JS) from booking exactly one half of a
    /// couple seat, leaving its partner independently bookable. This documents that gap rather
    /// than asserting enforcement that doesn't exist - if server-side pairing enforcement is
    /// ever added, this test should start failing and needs to be rewritten to assert the pair
    /// is reserved together.
    /// </summary>
    [Fact]
    public async Task CreateReservationAsync_BookingOneCoupleSeat_DoesNotAlsoReserveItsPair()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateContext();
        var service = new ReservationService(context);

        var result = await service.CreateReservationAsync(userId: null, screeningId: db.ScreeningId, seatIds: [db.CoupleSeatAId]);

        Assert.True(result.Success);
        var seatMap = (await service.GetSeatMapAsync(db.ScreeningId)).ToList();
        Assert.True(seatMap.Single(s => s.SeatId == db.CoupleSeatAId).IsReserved);
        Assert.False(seatMap.Single(s => s.SeatId == db.CoupleSeatBId).IsReserved);
    }

    [Fact]
    public async Task CreateReservationAsync_NoSeatsSelected_Fails()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateContext();
        var service = new ReservationService(context);

        var result = await service.CreateReservationAsync(userId: null, screeningId: db.ScreeningId, seatIds: Array.Empty<int>());

        Assert.False(result.Success);
        Assert.Equal("No seats selected.", result.Error);
    }

    [Fact]
    public async Task CreateReservationAsync_MultipleDistinctSeats_ReservesAllOfThem()
    {
        using var db = await TestDb.CreateAsync();
        await using var context = db.CreateContext();
        var service = new ReservationService(context);

        var result = await service.CreateReservationAsync(userId: null, screeningId: db.ScreeningId, seatIds: [db.SeatId, db.OtherSeatId]);

        Assert.True(result.Success);
        Assert.Equal(2, result.Reservation!.NumberOfTickets);
        var seatMap = (await service.GetSeatMapAsync(db.ScreeningId)).ToList();
        Assert.True(seatMap.Single(s => s.SeatId == db.SeatId).IsReserved);
        Assert.True(seatMap.Single(s => s.SeatId == db.OtherSeatId).IsReserved);
    }
}
