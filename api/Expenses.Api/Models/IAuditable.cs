namespace Expenses.Api.Models;

public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    DateTime UpdatedAtUtc { get; set; }
}