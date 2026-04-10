using Expenses.Api.Models;

namespace Expenses.Api.Dtos;

public record ExpenseCreateRequest(
    Guid? CategoryId,
    decimal Amount,
    DateOnly Date,
    string? Note,
    TransactionStatus? Status
);

public record ExpenseUpdateRequest(
    Guid? CategoryId,
    decimal Amount,
    DateOnly? Date,
    string? Note,
    TransactionStatus? Status
);
