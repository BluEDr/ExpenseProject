using System.Security.Claims;
using Expenses.Api.Data;
using Expenses.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Expenses.Api.Controllers;

[ApiController]
[Route("api/v1/summaries")]
[Authorize]
public class SummaryController : ControllerBase
{
    private readonly AppDbContext _db;

    public SummaryController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("{yearMonth}")]
    public async Task<ActionResult<object>> GetMonthSummary(string yearMonth)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (!TryParseYearMonth(yearMonth, out var start))
        {
            return BadRequest("yearMonth must be in YYYYMM format.");
        }

        var summary = await BuildMonthSummaryAsync(userId, start);
        return Ok(summary);
    }

    [HttpGet("{yearMonth}/day/{dayNumber:int}")]
    public async Task<ActionResult<object>> GetRunningDaySummary(string yearMonth, int dayNumber)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (!TryParseYearMonth(yearMonth, out var start))
        {
            return BadRequest("yearMonth must be in YYYYMM format.");
        }

        var daysInMonth = DateTime.DaysInMonth(start.Year, start.Month);
        if (dayNumber < 1 || dayNumber > daysInMonth)
        {
            return BadRequest($"dayNumber must be between 1 and {daysInMonth}.");
        }

        var summary = await BuildMonthSummaryAsync(userId, start);
        var day = summary.Daily.Single(x => x.DayNumber == dayNumber);

        return Ok(new
        {
            year = summary.Year,
            month = summary.Month,
            dayNumber = day.DayNumber,
            date = day.Date,
            expense = day.Expense,
            cumulativeExpenses = day.CumulativeExpenses,
            allowedUntilDay = day.AllowedUntilDay,
            net = day.Net,
            dailyAllowance = summary.DailyAllowance,
            totalIncome = summary.TotalIncome,
            expensesTotal = summary.ExpensesTotal,
            monthlyBalance = summary.MonthlyBalance
        });
    }

    private async Task<MonthSummaryResult> BuildMonthSummaryAsync(string userId, DateOnly start)
    {
        var end = start.AddMonths(1).AddDays(-1);
        var daysInMonth = DateTime.DaysInMonth(start.Year, start.Month);

        var confirmedExpenses = await _db.Expenses
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Where(x => x.Status == TransactionStatus.Confirmed)
            .Where(x => x.Date >= start && x.Date <= end)
            .Select(x => new { x.Date, x.Amount })
            .ToListAsync();

        var confirmedIncomesTotal = await _db.Incomes
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Where(x => x.Status == TransactionStatus.Confirmed)
            .Where(x => x.Date >= start && x.Date <= end)
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;

        var activeIncomeSourcesTotal = await _db.IncomeSources
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Where(x => x.StartDate <= end)
            .Where(x => x.EndDate == null || x.EndDate >= start)
            .SumAsync(x => (decimal?)x.MonthlyAmount) ?? 0m;

        var expensesTotal = confirmedExpenses.Sum(x => x.Amount);
        var totalIncome = activeIncomeSourcesTotal + confirmedIncomesTotal;
        var monthlyBalance = totalIncome - expensesTotal;
        var dailyAllowance = daysInMonth == 0
            ? 0m
            : decimal.Round(totalIncome / daysInMonth, 3, MidpointRounding.AwayFromZero);

        var expenseByDate = confirmedExpenses
            .GroupBy(x => x.Date)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));

        var daily = new List<DaySummaryResult>(daysInMonth);
        decimal cumulativeExpenses = 0m;

        for (var day = 1; day <= daysInMonth; day++)
        {
            var currentDate = new DateOnly(start.Year, start.Month, day);
            var dayExpense = expenseByDate.GetValueOrDefault(currentDate, 0m);
            cumulativeExpenses += dayExpense;

            var allowedUntilDay = decimal.Round(dailyAllowance * day, 3, MidpointRounding.AwayFromZero);
            var net = decimal.Round(allowedUntilDay - cumulativeExpenses, 3, MidpointRounding.AwayFromZero);

            daily.Add(new DaySummaryResult
            {
                Date = currentDate,
                DayNumber = day,
                Expense = dayExpense,
                CumulativeExpenses = cumulativeExpenses,
                AllowedUntilDay = allowedUntilDay,
                Net = net
            });
        }

        return new MonthSummaryResult
        {
            Year = start.Year,
            Month = start.Month,
            From = start,
            To = end,
            DaysInMonth = daysInMonth,
            IncomeSourcesTotal = activeIncomeSourcesTotal,
            IncomesTotal = confirmedIncomesTotal,
            TotalIncome = totalIncome,
            ExpensesTotal = expensesTotal,
            MonthlyBalance = monthlyBalance,
            DailyAllowance = dailyAllowance,
            Daily = daily
        };
    }

    private static bool TryParseYearMonth(string yearMonth, out DateOnly start)
    {
        start = default;

        if (yearMonth.Length != 6 || !int.TryParse(yearMonth, out var value))
        {
            return false;
        }

        var year = value / 100;
        var month = value % 100;
        if (month < 1 || month > 12)
        {
            return false;
        }

        start = new DateOnly(year, month, 1);
        return true;
    }
}

public sealed class MonthSummaryResult
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int DaysInMonth { get; set; }
    public decimal IncomeSourcesTotal { get; set; }
    public decimal IncomesTotal { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal ExpensesTotal { get; set; }
    public decimal MonthlyBalance { get; set; }
    public decimal DailyAllowance { get; set; }
    public List<DaySummaryResult> Daily { get; set; } = [];
}

public sealed class DaySummaryResult
{
    public DateOnly Date { get; set; }
    public int DayNumber { get; set; }
    public decimal Expense { get; set; }
    public decimal CumulativeExpenses { get; set; }
    public decimal AllowedUntilDay { get; set; }
    public decimal Net { get; set; }
}
