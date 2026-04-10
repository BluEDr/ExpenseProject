using Expenses.Api.Models;

namespace Expenses.Api.Dtos;

public sealed class MonthlySummaryResponse
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int DaysInMonth { get; set; }
    public decimal? PreviousMonthClosingBalance { get; set; }
    public decimal StartingBalance { get; set; }
    public decimal IncomeSourcesTotal { get; set; }
    public decimal IncomesTotal { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal ExpensesTotal { get; set; }
    public int ExpenseCount { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal MonthlyBalance { get; set; }
    public decimal DailyAllowance { get; set; }
    public List<MonthlyExpenseResponse> Expenses { get; set; } = [];
    public List<MonthlyDaySummaryResponse> Daily { get; set; } = [];
}

public sealed class MonthlyExpenseResponse
{
    public Guid Id { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string? Note { get; set; }
    public TransactionStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class MonthlyDaySummaryResponse
{
    public DateOnly Date { get; set; }
    public int DayNumber { get; set; }
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal CumulativeIncome { get; set; }
    public decimal CumulativeExpenses { get; set; }
    public decimal AllowedUntilDay { get; set; }
    public decimal RunningBalance { get; set; }
    public decimal Net { get; set; }
}

public sealed class EnsureMonthlySummaryResponse
{
    public bool Created { get; set; }
    public MonthlySummaryResponse Summary { get; set; } = new();
}

public sealed class DeleteMonthlySummaryResponse
{
    public int DeletedCount { get; set; }
}

public sealed class RebuildMonthlySummaryRangeRequest
{
    public string FromYearMonth { get; set; } = string.Empty;
    public string? ToYearMonth { get; set; }
}

public sealed class RebuildMonthlySummaryRangeResponse
{
    public int RebuiltCount { get; set; }
    public string FromYearMonth { get; set; } = string.Empty;
    public string ToYearMonth { get; set; } = string.Empty;
}
