using System.Security.Claims;
using Expenses.Api.Data;
using Expenses.Api.Dtos;
using Expenses.Api.Models;
using Expenses.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Expenses.Api.Controllers;

[ApiController]
[Route("api/v1/expenses")]
[Authorize]
public class ExpensesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly MonthlySummaryService _monthlySummaryService;

    public ExpensesController(AppDbContext db, MonthlySummaryService monthlySummaryService)
    {
        _db = db;
        _monthlySummaryService = monthlySummaryService;
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create(ExpenseCreateRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest("Amount must be greater than zero.");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (request.CategoryId != null)
        {
            var category = await _db.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.UserId == userId, cancellationToken);

            if (category == null)
            {
                return BadRequest("Category not found.");
            }

            if (category.Type != CategoryType.Expense)
            {
                return BadRequest("Category is not an expense category.");
            }
        }

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CategoryId = request.CategoryId,
            Amount = request.Amount,
            Date = request.Date,
            Note = request.Note,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(cancellationToken);
        await _monthlySummaryService.RebuildFromMonthForwardAsync(userId, ToMonthStart(expense.Date), cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = expense.Id }, new
        {
            expense.Id,
            expense.CategoryId,
            expense.Amount,
            expense.Date,
            expense.Note
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> GetById(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var expense = await _db.Expenses
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (expense == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            expense.Id,
            expense.CategoryId,
            expense.Amount,
            expense.Date,
            expense.Note,
            expense.Status,
            expense.CreatedAtUtc
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<object>> Update(Guid id, [FromBody] ExpenseUpdateRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest("Amount must be greater than zero.");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var expense = await _db.Expenses
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, cancellationToken);

        if (expense == null)
        {
            return NotFound();
        }

        var originalMonth = ToMonthStart(expense.Date);

        if (request.CategoryId != null)
        {
            var category = await _db.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.UserId == userId, cancellationToken);

            if (category == null)
            {
                return BadRequest("Category not found.");
            }

            if (category.Type != CategoryType.Expense)
            {
                return BadRequest("Category is not an expense category.");
            }
        }

        expense.CategoryId = request.CategoryId ?? expense.CategoryId;
        expense.Amount = request.Amount;
        expense.Date = request.Date ?? expense.Date;
        expense.Note = request.Note ?? expense.Note;
        expense.Status = request.Status ?? expense.Status;

        await _db.SaveChangesAsync(cancellationToken);

        var affectedMonth = MinMonth(originalMonth, ToMonthStart(expense.Date));
        await _monthlySummaryService.RebuildFromMonthForwardAsync(userId, affectedMonth, cancellationToken);

        return Ok(new
        {
            expense.Id,
            expense.CategoryId,
            expense.Amount,
            expense.Date,
            expense.Note,
            expense.Status,
            expense.CreatedAtUtc
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var expense = await _db.Expenses
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, cancellationToken);

        if (expense == null)
        {
            return NotFound();
        }

        if (expense.IsDeleted)
        {
            return NoContent();
        }

        expense.IsDeleted = true;
        await _db.SaveChangesAsync(cancellationToken);
        await _monthlySummaryService.RebuildFromMonthForwardAsync(userId, ToMonthStart(expense.Date), cancellationToken);

        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<object>> List(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? limit,
        [FromQuery] int? offset)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var take = limit.GetValueOrDefault(50);
        var skip = offset.GetValueOrDefault(0);
        if (take <= 0 || take > 500)
        {
            return BadRequest("Limit must be between 1 and 500.");
        }

        if (skip < 0)
        {
            return BadRequest("Offset must be 0 or greater.");
        }

        var query = _db.Expenses
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Where(e => !e.IsDeleted);

        if (from != null)
        {
            query = query.Where(e => e.Date >= from);
        }

        if (to != null)
        {
            query = query.Where(e => e.Date <= to);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(e => new
            {
                e.Id,
                e.CategoryId,
                e.Amount,
                e.Date,
                e.Note,
                e.Status,
                e.CreatedAtUtc
            })
            .ToListAsync();
        var pageCount = items.Count;

        return Ok(new { totalCount, pageCount, items });
    }

    [HttpGet("summary")]
    public async Task<ActionResult<object>> Summary(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        DateOnly start;
        DateOnly end;

        if (year.HasValue || month.HasValue)
        {
            if (!year.HasValue || !month.HasValue)
            {
                return BadRequest("Both year and month are required.");
            }

            if (month < 1 || month > 12)
            {
                return BadRequest("Month must be between 1 and 12.");
            }

            start = new DateOnly(year.Value, month.Value, 1);
            end = start.AddMonths(1).AddDays(-1);
        }
        else if (from.HasValue || to.HasValue)
        {
            start = from ?? DateOnly.MinValue;
            end = to ?? DateOnly.MaxValue;
        }
        else
        {
            var now = DateTime.Now;
            start = new DateOnly(now.Year, now.Month, 1);
            end = start.AddMonths(1).AddDays(-1);
        }

        var query = _db.Expenses
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Where(e => !e.IsDeleted)
            .Where(e => e.Date >= start && e.Date <= end);

        var total = await query.SumAsync(e => (decimal?)e.Amount) ?? 0m;
        var count = await query.CountAsync();

        return Ok(new { total, count, from = start, to = end });
    }

    private static DateOnly ToMonthStart(DateOnly date) => new(date.Year, date.Month, 1);

    private static DateOnly MinMonth(DateOnly left, DateOnly right) => left <= right ? left : right;
}
