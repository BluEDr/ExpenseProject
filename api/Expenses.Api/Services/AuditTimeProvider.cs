using Microsoft.Extensions.Options;

namespace Expenses.Api.Services;

public class AuditTimeProvider : IAuditTimeProvider
{
    private readonly TimeZoneInfo _timeZone;

    public AuditTimeProvider(IOptions<AuditTimeOptions> options)
    {
        var configuredTimeZoneId = options.Value.TimeZoneId;

        if (string.IsNullOrWhiteSpace(configuredTimeZoneId))
        {
            _timeZone = TimeZoneInfo.Local;
            return;
        }

        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(configuredTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            _timeZone = TimeZoneInfo.Local;
        }
        catch (InvalidTimeZoneException)
        {
            _timeZone = TimeZoneInfo.Local;
        }
    }

    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);
}
