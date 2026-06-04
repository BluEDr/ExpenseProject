using Expenses.Api.Data;
using Expenses.Api.Dtos;
using Expenses.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Expenses.Api.Services;

public sealed class MonthlySummaryService
{
    private readonly AppDbContext _db;

    public MonthlySummaryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<EnsureMonthlySummaryResponse> EnsureMonthAsync(string userId, DateOnly monthStart, CancellationToken cancellationToken = default)
    {
        var existing = await GetSummaryEntityAsync(userId, monthStart, asNoTracking: true, cancellationToken);
        if (existing != null)
        {
            return new EnsureMonthlySummaryResponse
            {
                Created = false,
                Summary = await BuildResponseAsync(userId, monthStart, cancellationToken)
            };
        }

        await EnsureMonthBuiltAsync(userId, monthStart, forceRebuild: true, cancellationToken);

        return new EnsureMonthlySummaryResponse
        {
            Created = true,
            Summary = await BuildResponseAsync(userId, monthStart, cancellationToken)
        };
    }

    public async Task<MonthlySummaryResponse> GetMonthAsync(string userId, DateOnly monthStart, CancellationToken cancellationToken = default)
    {
        await EnsureMonthBuiltAsync(userId, monthStart, forceRebuild: false, cancellationToken);
        return await BuildResponseAsync(userId, monthStart, cancellationToken);
    }

    public async Task<MonthlySummaryResponse> RebuildMonthAsync(string userId, DateOnly monthStart, CancellationToken cancellationToken = default)
    {
        await EnsureMonthBuiltAsync(userId, monthStart, forceRebuild: true, cancellationToken);
        return await BuildResponseAsync(userId, monthStart, cancellationToken);
    }

    public async Task<int> RebuildFromMonthForwardAsync(string userId, DateOnly monthStart, CancellationToken cancellationToken = default)
    {
        var current = new DateOnly(monthStart.Year, monthStart.Month, 1);
        var finalMonth = await GetLatestImpactedMonthAsync(userId, cancellationToken);
        var currentMonth = new DateOnly(DateTime.Now.Year, DateTime.Now.Month, 1);

        // Save operations should only rebuild up to the present month. Future income
        // source end dates can otherwise trigger decades of unnecessary summary work.
        if (finalMonth > currentMonth)
        {
            finalMonth = currentMonth;
        }

        if (finalMonth < current)
        {
            finalMonth = current;
        }

        var rebuiltCount = 0;
        while (current <= finalMonth)
        {
            await EnsureMonthBuiltAsync(userId, current, forceRebuild: true, cancellationToken);
            rebuiltCount++;
            current = current.AddMonths(1);
        }

        return rebuiltCount;
    }

    public async Task<int> RebuildRangeAsync(string userId, DateOnly fromMonth, DateOnly? toMonth, CancellationToken cancellationToken = default)
    {
        var start = new DateOnly(fromMonth.Year, fromMonth.Month, 1);
        var end = toMonth.HasValue
            ? new DateOnly(toMonth.Value.Year, toMonth.Value.Month, 1)
            : await GetLatestImpactedMonthAsync(userId, cancellationToken);

        if (end < start)
        {
            return 0;
        }

        var rebuiltCount = 0;
        var current = start;
        while (current <= end)
        {
            await EnsureMonthBuiltAsync(userId, current, forceRebuild: true, cancellationToken);
            rebuiltCount++;
            current = current.AddMonths(1);
        }

        return rebuiltCount;
    }

    public async Task<int> DeleteFromMonthAsync(string userId, DateOnly monthStart, CancellationToken cancellationToken = default)
    {
        var summaries = await _db.MonthlySummaries
            .Where(x => x.UserId == userId)
            .Where(x => x.Year > monthStart.Year || (x.Year == monthStart.Year && x.Month >= monthStart.Month))
            .ToListAsync(cancellationToken);

        if (summaries.Count == 0)
        {
            return 0;
        }

        _db.MonthlySummaries.RemoveRange(summaries);
        await _db.SaveChangesAsync(cancellationToken);

        return summaries.Count;
    }

    private async Task EnsureMonthBuiltAsync(string userId, DateOnly monthStart, bool forceRebuild, CancellationToken cancellationToken)
    {
        var trackedSummary = await GetSummaryEntityAsync(userId, monthStart, asNoTracking: false, cancellationToken);
        if (trackedSummary != null && !forceRebuild)
        {
            return;
        }

        var earliestMonth = await GetEarliestRelevantMonthAsync(userId, cancellationToken);
        var normalizedMonth = new DateOnly(monthStart.Year, monthStart.Month, 1);

        decimal startingBalance = 0m;
        if (earliestMonth.HasValue && normalizedMonth > earliestMonth.Value)
        {
            var previousMonth = normalizedMonth.AddMonths(-1);
            await EnsureMonthBuiltAsync(userId, previousMonth, forceRebuild, cancellationToken);

            var previousSummary = await GetSummaryEntityAsync(userId, previousMonth, asNoTracking: true, cancellationToken);
            startingBalance = previousSummary?.ClosingBalance ?? 0m;
        }

        var monthlyTotals = await CalculateMonthTotalsAsync(userId, normalizedMonth, startingBalance, cancellationToken);

        var summary = trackedSummary ?? new MonthlySummary
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Year = normalizedMonth.Year,
            Month = normalizedMonth.Month
        };

        summary.StartingBalance = monthlyTotals.StartingBalance;
        summary.TotalIncome = monthlyTotals.TotalIncome;
        summary.TotalExpense = monthlyTotals.TotalExpense;
        summary.ClosingBalance = monthlyTotals.ClosingBalance;
        summary.DailyAllowance = monthlyTotals.DailyAllowance;

        if (trackedSummary == null)
        {
            _db.MonthlySummaries.Add(summary);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<MonthlySummaryResponse> BuildResponseAsync(string userId, DateOnly monthStart, CancellationToken cancellationToken)
    {
        var normalizedMonth = new DateOnly(monthStart.Year, monthStart.Month, 1);
        var summary = await GetSummaryEntityAsync(userId, normalizedMonth, asNoTracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Monthly summary should exist after ensure/rebuild.");
        var previousMonth = normalizedMonth.AddMonths(-1);
        var previousSummary = await GetSummaryEntityAsync(userId, previousMonth, asNoTracking: true, cancellationToken);

        var end = normalizedMonth.AddMonths(1).AddDays(-1);
        var daysInMonth = DateTime.DaysInMonth(normalizedMonth.Year, normalizedMonth.Month);

        var confirmedExpenses = await _db.Expenses
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Where(x => x.Status == TransactionStatus.Confirmed)
            .Where(x => x.Date >= normalizedMonth && x.Date <= end)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => new MonthlyExpenseResponse
            {
                Id = x.Id,
                CategoryId = x.CategoryId,
                Amount = x.Amount,
                Date = x.Date,
                Note = x.Note,
                Status = x.Status,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var confirmedIncomes = await _db.Incomes
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Where(x => x.Status == TransactionStatus.Confirmed)
            .Where(x => x.Date >= normalizedMonth && x.Date <= end)
            .Select(x => new { x.Date, x.Amount })
            .ToListAsync(cancellationToken);

        var expenseByDate = confirmedExpenses
            .GroupBy(x => x.Date)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));

        var incomeByDate = confirmedIncomes
            .GroupBy(x => x.Date)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));

        var incomesTotal = confirmedIncomes.Sum(x => x.Amount);
        var incomeSourcesTotal = decimal.Round(summary.TotalIncome - incomesTotal, 2, MidpointRounding.AwayFromZero);
        var openingAvailable = summary.StartingBalance + incomeSourcesTotal;

        var daily = new List<MonthlyDaySummaryResponse>(daysInMonth);
        decimal cumulativeExpenses = 0m;
        decimal cumulativeIncome = 0m;

        for (var day = 1; day <= daysInMonth; day++)
        {
            var currentDate = new DateOnly(normalizedMonth.Year, normalizedMonth.Month, day);
            var dayExpense = expenseByDate.GetValueOrDefault(currentDate, 0m);
            var dayIncome = incomeByDate.GetValueOrDefault(currentDate, 0m);

            cumulativeExpenses += dayExpense;
            cumulativeIncome += dayIncome;

            var allowedUntilDay = decimal.Round(summary.DailyAllowance * day, 2, MidpointRounding.AwayFromZero);
            var runningBalance = decimal.Round(openingAvailable + cumulativeIncome - cumulativeExpenses, 2, MidpointRounding.AwayFromZero);
            var net = decimal.Round(summary.StartingBalance + allowedUntilDay + cumulativeIncome - cumulativeExpenses, 2, MidpointRounding.AwayFromZero);

            daily.Add(new MonthlyDaySummaryResponse
            {
                Date = currentDate,
                DayNumber = day,
                Income = dayIncome,
                Expense = dayExpense,
                CumulativeIncome = cumulativeIncome,
                CumulativeExpenses = cumulativeExpenses,
                AllowedUntilDay = allowedUntilDay,
                RunningBalance = runningBalance,
                Net = net
            });
        }

        return new MonthlySummaryResponse
        {
            Year = normalizedMonth.Year,
            Month = normalizedMonth.Month,
            From = normalizedMonth,
            To = end,
            DaysInMonth = daysInMonth,
            PreviousMonthClosingBalance = previousSummary?.ClosingBalance,
            StartingBalance = summary.StartingBalance,
            IncomeSourcesTotal = incomeSourcesTotal,
            IncomesTotal = incomesTotal,
            TotalIncome = summary.TotalIncome,
            ExpensesTotal = summary.TotalExpense,
            ExpenseCount = confirmedExpenses.Count,
            ClosingBalance = summary.ClosingBalance,
            MonthlyBalance = summary.ClosingBalance,
            DailyAllowance = summary.DailyAllowance,
            Expenses = confirmedExpenses,
            Daily = daily
        };
    }

    private async Task<MonthTotals> CalculateMonthTotalsAsync(string userId, DateOnly monthStart, decimal startingBalance, CancellationToken cancellationToken)
    {
        var end = monthStart.AddMonths(1).AddDays(-1);
        var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);

        var confirmedExpensesTotal = await _db.Expenses
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Where(x => x.Status == TransactionStatus.Confirmed)
            .Where(x => x.Date >= monthStart && x.Date <= end)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var confirmedIncomesTotal = await _db.Incomes
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Where(x => x.Status == TransactionStatus.Confirmed)
            .Where(x => x.Date >= monthStart && x.Date <= end)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var activeIncomeSourcesTotal = await _db.IncomeSources
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Where(x => x.StartDate <= end)
            .Where(x => x.EndDate == null || x.EndDate >= monthStart)
            .SumAsync(x => (decimal?)x.MonthlyAmount, cancellationToken) ?? 0m;

        var totalIncome = decimal.Round(activeIncomeSourcesTotal + confirmedIncomesTotal, 2, MidpointRounding.AwayFromZero);
        var totalExpense = decimal.Round(confirmedExpensesTotal, 2, MidpointRounding.AwayFromZero);
        var closingBalance = decimal.Round(startingBalance + totalIncome - totalExpense, 2, MidpointRounding.AwayFromZero);
        // One-off incomes are applied on their exact dates in the daily summary.
        // The monthly allowance should reflect only the money planned for the month.
        var availableToSpend = activeIncomeSourcesTotal;
        var dailyAllowance = daysInMonth == 0
            ? 0m
            : decimal.Round(availableToSpend / daysInMonth, 2, MidpointRounding.AwayFromZero);

        return new MonthTotals(startingBalance, totalIncome, totalExpense, closingBalance, dailyAllowance);
    }

    private async Task<MonthlySummary?> GetSummaryEntityAsync(string userId, DateOnly monthStart, bool asNoTracking, CancellationToken cancellationToken)
    {
        var query = _db.MonthlySummaries
            .Where(x => x.UserId == userId && x.Year == monthStart.Year && x.Month == monthStart.Month);

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<DateOnly?> GetEarliestRelevantMonthAsync(string userId, CancellationToken cancellationToken)
    {
        var candidates = new List<DateOnly>();

        var earliestExpenseDate = await _db.Expenses
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Date)
            .Select(x => (DateOnly?)x.Date)
            .FirstOrDefaultAsync(cancellationToken);
        if (earliestExpenseDate.HasValue)
        {
            candidates.Add(new DateOnly(earliestExpenseDate.Value.Year, earliestExpenseDate.Value.Month, 1));
        }

        var earliestIncomeDate = await _db.Incomes
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Date)
            .Select(x => (DateOnly?)x.Date)
            .FirstOrDefaultAsync(cancellationToken);
        if (earliestIncomeDate.HasValue)
        {
            candidates.Add(new DateOnly(earliestIncomeDate.Value.Year, earliestIncomeDate.Value.Month, 1));
        }

        var earliestIncomeSourceDate = await _db.IncomeSources
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.StartDate)
            .Select(x => (DateOnly?)x.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (earliestIncomeSourceDate.HasValue)
        {
            candidates.Add(new DateOnly(earliestIncomeSourceDate.Value.Year, earliestIncomeSourceDate.Value.Month, 1));
        }

        var earliestSummary = await _db.MonthlySummaries
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .Select(x => (DateOnly?)new DateOnly(x.Year, x.Month, 1))
            .FirstOrDefaultAsync(cancellationToken);
        if (earliestSummary.HasValue)
        {
            candidates.Add(earliestSummary.Value);
        }

        return candidates.Count == 0 ? null : candidates.Min();
    }

    private async Task<DateOnly> GetLatestImpactedMonthAsync(string userId, CancellationToken cancellationToken)
    {
        var currentMonth = new DateOnly(DateTime.Now.Year, DateTime.Now.Month, 1);
        var candidates = new List<DateOnly> { currentMonth };

        var latestExpenseDate = await _db.Expenses
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Date)
            .Select(x => (DateOnly?)x.Date)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestExpenseDate.HasValue)
        {
            candidates.Add(new DateOnly(latestExpenseDate.Value.Year, latestExpenseDate.Value.Month, 1));
        }

        var latestIncomeDate = await _db.Incomes
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Date)
            .Select(x => (DateOnly?)x.Date)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestIncomeDate.HasValue)
        {
            candidates.Add(new DateOnly(latestIncomeDate.Value.Year, latestIncomeDate.Value.Month, 1));
        }

        var incomeSourceRanges = await _db.IncomeSources
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.StartDate, x.EndDate })
            .ToListAsync(cancellationToken);
        foreach (var range in incomeSourceRanges)
        {
            var finalDate = range.EndDate ?? currentMonth;
            candidates.Add(new DateOnly(finalDate.Year, finalDate.Month, 1));
            candidates.Add(new DateOnly(range.StartDate.Year, range.StartDate.Month, 1));
        }

        var latestSummary = await _db.MonthlySummaries
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .Select(x => (DateOnly?)new DateOnly(x.Year, x.Month, 1))
            .FirstOrDefaultAsync(cancellationToken);
        if (latestSummary.HasValue)
        {
            candidates.Add(latestSummary.Value);
        }

        return candidates.Max();
    }

    private sealed record MonthTotals(
        decimal StartingBalance,
        decimal TotalIncome,
        decimal TotalExpense,
        decimal ClosingBalance,
        decimal DailyAllowance
    );
}
