using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace CinemaWebsite.Services;

public enum ReservationTokenStatus { Invalid, Valid, Expired }

public sealed class ReservationGuestTokens(
    IDataProtectionProvider provider,
    TimeProvider? timeProvider = null,
    TimeSpan? postScreeningLifetime = null)
{
    private readonly IDataProtector protector = provider.CreateProtector("ReservationGuestAccessToken");
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly TimeSpan lifetime = postScreeningLifetime ?? TimeSpan.FromHours(24);

    public string Create(int reservationId) => protector.Protect(
        FormattableString.Invariant($"{reservationId}|{clock.GetUtcNow().UtcTicks}"));

    public ReservationTokenStatus Validate(int reservationId, DateTime screeningStart, string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return ReservationTokenStatus.Invalid;

        try
        {
            // Keep the existing protected format so links already sent to guests also expire.
            var parts = protector.Unprotect(token).Split('|');
            if (parts.Length != 2
                || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                || id != reservationId
                || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var issuedTicks)
                || issuedTicks < DateTime.MinValue.Ticks || issuedTicks > DateTime.MaxValue.Ticks)
                return ReservationTokenStatus.Invalid;

            var now = clock.GetUtcNow().UtcDateTime;
            if (issuedTicks > now.Ticks) return ReservationTokenStatus.Invalid;

            // Screening times follow the application's UTC convention, including values
            // materialized from SQLite with DateTimeKind.Unspecified.
            var expiresAt = DateTime.SpecifyKind(screeningStart, DateTimeKind.Utc).Add(lifetime);
            var maximumAge = expiresAt.Ticks - issuedTicks;
            return now.Ticks - issuedTicks >= maximumAge
                ? ReservationTokenStatus.Expired
                : ReservationTokenStatus.Valid;
        }
        catch (CryptographicException)
        {
            return ReservationTokenStatus.Invalid;
        }
    }
}
