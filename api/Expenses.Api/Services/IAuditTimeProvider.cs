namespace Expenses.Api.Services;

public interface IAuditTimeProvider
{
    DateTime Now { get; }
}
