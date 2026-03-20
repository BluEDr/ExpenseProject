namespace Expenses.Api.Models;

public class ExpenseAttachment
{
    public Guid Id { get; set; }
    public Guid ExpenseId { get; set; }
    public Expense? Expense { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredFilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}