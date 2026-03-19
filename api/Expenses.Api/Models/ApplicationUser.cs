using Microsoft.AspNetCore.Identity;

namespace Expenses.Api.Models;

public class ApplicationUser : IdentityUser
{
    public string CurrencyCode { get; set; } = "EUR";
    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}