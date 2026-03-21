namespace Expenses.Api.Models;

public class Income : IAuditable
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Confirmed;
    public string? Note { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
