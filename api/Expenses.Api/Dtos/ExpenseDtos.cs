namespace Expenses.Api.Dtos;

public record ExpenseCreateRequest(
    Guid? CategoryId,
    decimal Amount,
    DateOnly Date,
    string? Note
);
