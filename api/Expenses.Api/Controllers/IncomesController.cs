using System.Security.Claims;
using Expenses.Api.Data;
using Expenses.Api.Models;
using Expenses.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Expenses.Api.Controllers;

[ApiController]
[Route("api/v1/incomes")]
[Authorize]
public class IncomesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly MonthlySummaryService _monthlySummaryService;

    public IncomesController(AppDbContext db, MonthlySummaryService monthlySummaryService)
    {
        _db = db;
        _monthlySummaryService = monthlySummaryService;
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

        var query = _db.Incomes
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

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> GetById(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var income = await _db.Incomes
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (income == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            income.Id,
            income.CategoryId,
            income.Amount,
            income.Date,
            income.Note,
            income.Status,
            income.CreatedAtUtc
        });
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] IncomeCreateRequest request, CancellationToken cancellationToken)
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

            if (category.Type != CategoryType.Income)
            {
                return BadRequest("Category is not an income category.");
            }
        }

        var income = new Income
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CategoryId = request.CategoryId,
            Amount = request.Amount,
            Date = request.Date,
            Note = request.Note,
            Status = request.Status ?? TransactionStatus.Confirmed
        };

        _db.Incomes.Add(income);
        await _db.SaveChangesAsync(cancellationToken);
        await _monthlySummaryService.DeleteFromMonthAsync(userId, ToMonthStart(income.Date), cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = income.Id }, new
        {
            income.Id,
            income.CategoryId,
            income.Amount,
            income.Date,
            income.Note,
            income.Status,
            income.CreatedAtUtc
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<object>> Update(Guid id, [FromBody] IncomeUpdateRequest request, CancellationToken cancellationToken)
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

        var income = await _db.Incomes
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, cancellationToken);

        if (income == null)
        {
            return NotFound();
        }

        var originalMonth = ToMonthStart(income.Date);

        if (request.CategoryId != null)
        {
            var category = await _db.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.UserId == userId, cancellationToken);

            if (category == null)
            {
                return BadRequest("Category not found.");
            }

            if (category.Type != CategoryType.Income)
            {
                return BadRequest("Category is not an income category.");
            }
        }

        income.CategoryId = request.CategoryId;
        income.Amount = request.Amount;
        income.Date = request.Date;
        income.Note = request.Note;
        income.Status = request.Status ?? income.Status;

        await _db.SaveChangesAsync(cancellationToken);

        var affectedMonth = MinMonth(originalMonth, ToMonthStart(income.Date));
        await _monthlySummaryService.DeleteFromMonthAsync(userId, affectedMonth, cancellationToken);

        return Ok(new
        {
            income.Id,
            income.CategoryId,
            income.Amount,
            income.Date,
            income.Note,
            income.Status,
            income.CreatedAtUtc
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

        var income = await _db.Incomes
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, cancellationToken);

        if (income == null)
        {
            return NotFound();
        }

        if (income.IsDeleted)
        {
            return NoContent();
        }

        income.IsDeleted = true;
        await _db.SaveChangesAsync(cancellationToken);
        await _monthlySummaryService.DeleteFromMonthAsync(userId, ToMonthStart(income.Date), cancellationToken);

        return NoContent();
    }

    private static DateOnly ToMonthStart(DateOnly date) => new(date.Year, date.Month, 1);

    private static DateOnly MinMonth(DateOnly left, DateOnly right) => left <= right ? left : right;
}

public record IncomeCreateRequest(
    Guid? CategoryId,
    decimal Amount,
    DateOnly Date,
    string? Note,
    TransactionStatus? Status
);

public record IncomeUpdateRequest(
    Guid? CategoryId,
    decimal Amount,
    DateOnly Date,
    string? Note,
    TransactionStatus? Status
);
