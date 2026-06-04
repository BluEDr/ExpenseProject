using System.Security.Claims;
using Expenses.Api.Data;
using Expenses.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Expenses.Api.Controllers;

[ApiController]
[Route("api/v1/activities")]
[Authorize]
public class ActivitiesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ActivitiesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("latest")]
    public async Task<ActionResult<IEnumerable<ActivityItemDto>>> GetLatest(
        [FromQuery] int limit = 14,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (limit <= 0 || limit > 100)
        {
            return BadRequest("Limit must be between 1 and 100.");
        }

        var expenseItems = _db.Expenses
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Date)
            .Select(x => new ActivityItemDto
            {
                Type = "expense",
                Id = x.Id,
                Amount = x.Amount,
                Date = x.Date,
                CreatedAtUtc = x.CreatedAtUtc,
                Note = x.Note
            })
            .Take(limit);

        var incomeItems = _db.Incomes
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Date)
            .Select(x => new ActivityItemDto
            {
                Type = "income",
                Id = x.Id,
                Amount = x.Amount,
                Date = x.Date,
                CreatedAtUtc = x.CreatedAtUtc,
                Note = x.Note
            })
            .Take(limit);

        var items = await expenseItems
            .Concat(incomeItems)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Date)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return Ok(items);
    }
}
