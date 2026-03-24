using System.Security.Claims;
using Expenses.Api.Data;
using Expenses.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Expenses.Api.Controllers;

[ApiController]
[Route("api/v1/income-sources")]
[Authorize]
public class IncomeSourcesController : ControllerBase
{
    private readonly AppDbContext _db;

    public IncomeSourcesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> List()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var items = await _db.IncomeSources
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.MonthlyAmount,
                x.StartDate,
                x.EndDate,
                x.Note,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> GetById(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var item = await _db.IncomeSources
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (item == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            item.Id,
            item.Name,
            item.MonthlyAmount,
            item.StartDate,
            item.EndDate,
            item.Note,
            item.CreatedAtUtc,
            item.UpdatedAtUtc
        });
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] IncomeSourceCreateRequest request)
    {
        if (request.MonthlyAmount <= 0)
        {
            return BadRequest("Monthly amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var exists = await _db.IncomeSources
            .AnyAsync(x => x.UserId == userId && x.Name == request.Name);

        if (exists)
        {
            return BadRequest("Income source name already exists.");
        }

        var item = new IncomeSource
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            MonthlyAmount = request.MonthlyAmount,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Note = request.Note
        };

        _db.IncomeSources.Add(item);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, new
        {
            item.Id,
            item.Name,
            item.MonthlyAmount,
            item.StartDate,
            item.EndDate,
            item.Note,
            item.CreatedAtUtc,
            item.UpdatedAtUtc
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<object>> Update(Guid id, [FromBody] IncomeSourceUpdateRequest request)
    {
        if (request.MonthlyAmount <= 0)
        {
            return BadRequest("Monthly amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var item = await _db.IncomeSources
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (item == null)
        {
            return NotFound();
        }

        var nameExists = await _db.IncomeSources
            .AnyAsync(x => x.UserId == userId && x.Name == request.Name && x.Id != id);

        if (nameExists)
        {
            return BadRequest("Income source name already exists.");
        }

        item.Name = request.Name.Trim();
        item.MonthlyAmount = request.MonthlyAmount;
        item.StartDate = request.StartDate;
        item.EndDate = request.EndDate;
        item.Note = request.Note;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            item.Id,
            item.Name,
            item.MonthlyAmount,
            item.StartDate,
            item.EndDate,
            item.Note,
            item.CreatedAtUtc,
            item.UpdatedAtUtc
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> SoftDelete(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var item = await _db.IncomeSources
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (item == null)
        {
            return NotFound();
        }

        if (item.IsDeleted)
        {
            return NoContent();
        }

        item.IsDeleted = true;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}

public record IncomeSourceCreateRequest(
    string Name,
    decimal MonthlyAmount,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Note
);

public record IncomeSourceUpdateRequest(
    string Name,
    decimal MonthlyAmount,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Note
);
