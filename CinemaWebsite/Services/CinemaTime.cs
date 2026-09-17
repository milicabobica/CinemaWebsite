namespace CinemaWebsite.Services;

public static class CinemaTime
{
    // Screening form inputs and stored showtimes are wall-clock times in Skopje,
    // independent of the web server's time zone. Reservation expiry stays in UTC.
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Skopje");

    public static DateTime Now => FromUtc(DateTimeOffset.UtcNow);

    public static DateTime FromUtc(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, Zone).DateTime;
}
