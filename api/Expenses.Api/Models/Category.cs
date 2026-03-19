namespace Expenses.Api.Models;

public class Category
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public string Name { get; set; } = string.Empty;
    public CategoryType Type { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}