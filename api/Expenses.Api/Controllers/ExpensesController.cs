using System.Security.Claims;
using Expenses.Api.Data;
using Expenses.Api.Dtos;
using Expenses.Api.Models;
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

    public ExpensesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create(ExpenseCreateRequest request)
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
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.UserId == userId);

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
        await _db.SaveChangesAsync();

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
            expense.Note
        });
    }

    [HttpGet("total")]
    public async Task<ActionResult<object>> GetTotal()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var total = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Where(e => !e.IsDeleted)
            .SumAsync(e => (decimal?)e.Amount) ?? 0m;

        return Ok(new { total });
    }
}
