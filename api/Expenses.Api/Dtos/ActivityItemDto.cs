namespace Expenses.Api.Dtos
{
    public class ActivityItemDto
    {
        public string Type { get; set; } = string.Empty; // "expense" or "income"
        public Guid Id { get; set; }
        public Guid? CategoryId { get; set; }
        public decimal Amount { get; set; }
        public DateOnly Date { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public string? Note { get; set; }
    }
}
