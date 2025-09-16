using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace SysJaky_N.Services;

public class WaitlistTokenService
{
    private const string ProtectorPurpose = "waitlist-reservation-token";

    private readonly ITimeLimitedDataProtector _protector;
    private readonly ILogger<WaitlistTokenService> _logger;

    public WaitlistTokenService(IDataProtectionProvider dataProtectionProvider, ILogger<WaitlistTokenService> logger)
    {
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
        _logger = logger;
    }

    public string CreateToken(int entryId, Guid reservationId, TimeSpan lifetime)
    {
        var payload = $"{entryId}:{reservationId}";
        return _protector.Protect(payload, lifetime);
    }

    public bool TryValidateToken(string token, out int entryId, out Guid reservationId)
    {
        entryId = default;
        reservationId = default;

        try
        {
            var payload = _protector.Unprotect(token);
            var parts = payload.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                return false;
            }

            if (!int.TryParse(parts[0], out entryId))
            {
                return false;
            }

            if (!Guid.TryParse(parts[1], out reservationId))
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate waitlist token.");
            return false;
        }
    }
}
