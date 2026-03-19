namespace Expenses.Api.Dtos;

public class FileUploadRequest
{
    public IFormFile File { get; set; } = null!;
}