using CinemaWebsite.Data;
using Microsoft.EntityFrameworkCore;

namespace CinemaWebsite.Services;

public class ScreeningService(ApplicationDbContext context) : IScreeningService
{
    public async Task<bool> HasOverlapAsync(int hallId, DateTime startTime, DateTime endTime, int? ignoreScreeningId = null)
    {
        return await context.Screenings.AnyAsync(s =>
            s.CinemaHallId == hallId
            && (!ignoreScreeningId.HasValue || s.Id != ignoreScreeningId.Value)
            && startTime < s.EndTime
            && endTime > s.StartTime);
    }
}
