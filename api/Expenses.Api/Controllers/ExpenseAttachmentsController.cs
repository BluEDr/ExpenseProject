using System.Security.Claims;
using Expenses.Api.Data;
using Expenses.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Expenses.Api.Controllers;

[ApiController]
[Route("api/v1/expenses/{expenseId:guid}/attachments")]
[Authorize]
public class ExpenseAttachmentsController : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png"
    };

    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public ExpenseAttachmentsController(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<object>> Upload(Guid expenseId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return BadRequest("Unsupported file type.");
        }

        if (file.Length > 20 * 1024 * 1024)
        {
            return BadRequest("File is too large.");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var expenseExists = await _db.Expenses
            .AsNoTracking()
            .AnyAsync(e => e.Id == expenseId && e.UserId == userId);

        if (!expenseExists)
        {
            return NotFound("Expense not found.");
        }

        var root = _configuration["Storage:AttachmentsPath"] ?? "uploads";
        var userFolder = Path.Combine(root, userId, "expenses", expenseId.ToString());
        Directory.CreateDirectory(userFolder);

        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(userFolder, fileName);

        await using var stream = System.IO.File.Create(filePath);
        await file.CopyToAsync(stream);

        var attachment = new ExpenseAttachment
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            UserId = userId,
            FileName = file.FileName,
            StoredFilePath = filePath,
            ContentType = file.ContentType ?? "application/octet-stream",
            FileSize = file.Length,
            UploadedAtUtc = DateTime.UtcNow
        };

        _db.ExpenseAttachments.Add(attachment);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            attachment.Id,
            attachment.ExpenseId,
            attachment.FileName,
            attachment.StoredFilePath,
            attachment.ContentType,
            attachment.FileSize,
            attachment.UploadedAtUtc
        });
    }
}