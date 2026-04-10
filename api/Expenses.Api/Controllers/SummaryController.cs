using System.Security.Claims;
using Expenses.Api.Dtos;
using Expenses.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Expenses.Api.Controllers;

[ApiController]
[Route("api/v1/summaries")]
[Route("api/v1/monthly-summaries")]
[Authorize]
public class SummaryController : ControllerBase
{
    private readonly MonthlySummaryService _monthlySummaryService;

    public SummaryController(MonthlySummaryService monthlySummaryService)
    {
        _monthlySummaryService = monthlySummaryService;
    }

    [HttpGet("{yearMonth}")]
    public async Task<ActionResult<MonthlySummaryResponse>> GetMonthSummary(string yearMonth, CancellationToken cancellationToken)
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

        var summary = await _monthlySummaryService.GetMonthAsync(userId, start, cancellationToken);
        return Ok(summary);
    }

    [HttpPost("{yearMonth}")]
    public async Task<ActionResult<EnsureMonthlySummaryResponse>> CreateMonthSummary(string yearMonth, CancellationToken cancellationToken)
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

        var result = await _monthlySummaryService.EnsureMonthAsync(userId, start, cancellationToken);
        if (result.Created)
        {
            return CreatedAtAction(nameof(GetMonthSummary), new { yearMonth }, result);
        }

        return Ok(result);
    }

    [HttpPut("{yearMonth}")]
    public async Task<ActionResult<MonthlySummaryResponse>> RebuildMonthSummary(string yearMonth, CancellationToken cancellationToken)
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

        var summary = await _monthlySummaryService.RebuildMonthAsync(userId, start, cancellationToken);
        await _monthlySummaryService.RebuildFromMonthForwardAsync(userId, start.AddMonths(1), cancellationToken);

        return Ok(summary);
    }

    [HttpDelete("{yearMonth}")]
    public async Task<ActionResult<DeleteMonthlySummaryResponse>> DeleteMonthSummary(string yearMonth, CancellationToken cancellationToken)
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

        var deletedCount = await _monthlySummaryService.DeleteFromMonthAsync(userId, start, cancellationToken);
        return Ok(new DeleteMonthlySummaryResponse
        {
            DeletedCount = deletedCount
        });
    }

    [HttpPost("rebuild-range")]
    public async Task<ActionResult<RebuildMonthlySummaryRangeResponse>> RebuildRange(
        [FromBody] RebuildMonthlySummaryRangeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (!TryParseYearMonth(request.FromYearMonth, out var fromMonth))
        {
            return BadRequest("fromYearMonth must be in YYYYMM format.");
        }

        DateOnly? toMonth = null;
        if (!string.IsNullOrWhiteSpace(request.ToYearMonth))
        {
            if (!TryParseYearMonth(request.ToYearMonth, out var parsedToMonth))
            {
                return BadRequest("toYearMonth must be in YYYYMM format.");
            }

            toMonth = parsedToMonth;
        }

        if (toMonth.HasValue && toMonth.Value < fromMonth)
        {
            return BadRequest("toYearMonth must be greater than or equal to fromYearMonth.");
        }

        var rebuiltCount = await _monthlySummaryService.RebuildRangeAsync(userId, fromMonth, toMonth, cancellationToken);
        var responseToMonth = toMonth ?? new DateOnly(DateTime.Now.Year, DateTime.Now.Month, 1);

        return Ok(new RebuildMonthlySummaryRangeResponse
        {
            RebuiltCount = rebuiltCount,
            FromYearMonth = FormatYearMonth(fromMonth),
            ToYearMonth = FormatYearMonth(responseToMonth)
        });
    }

    [HttpGet("{yearMonth}/day/{dayNumber:int}")]
    public async Task<ActionResult<object>> GetRunningDaySummary(string yearMonth, int dayNumber, CancellationToken cancellationToken)
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

        var summary = await _monthlySummaryService.GetMonthAsync(userId, start, cancellationToken);
        var day = summary.Daily.Single(x => x.DayNumber == dayNumber);

        return Ok(new
        {
            year = summary.Year,
            month = summary.Month,
            dayNumber = day.DayNumber,
            date = day.Date,
            startingBalance = summary.StartingBalance,
            income = day.Income,
            expense = day.Expense,
            cumulativeIncome = day.CumulativeIncome,
            cumulativeExpenses = day.CumulativeExpenses,
            allowedUntilDay = day.AllowedUntilDay,
            runningBalance = day.RunningBalance,
            net = day.Net,
            dailyAllowance = summary.DailyAllowance,
            totalIncome = summary.TotalIncome,
            expensesTotal = summary.ExpensesTotal,
            closingBalance = summary.ClosingBalance,
            monthlyBalance = summary.MonthlyBalance
        });
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

    private static string FormatYearMonth(DateOnly value) => $"{value.Year:D4}{value.Month:D2}";
}
