namespace Expenses.Api.Dtos;

public record RegisterRequest(string Email, string Password, string? CurrencyCode, string? TimeZoneId);

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record LogoutRequest(string RefreshToken);

public record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);