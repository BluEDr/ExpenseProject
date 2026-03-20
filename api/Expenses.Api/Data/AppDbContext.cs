using Expenses.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Expenses.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<IncomeSource> IncomeSources => Set<IncomeSource>();
    public DbSet<MonthlySummary> MonthlySummaries => Set<MonthlySummary>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public override int SaveChanges()
    {
        ApplyAuditTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Category>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.Name, x.Type }).IsUnique();
        });

        builder.Entity<Expense>(entity =>
        {
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.Property(x => x.AttachmentPath).HasMaxLength(500);
            entity.Property(x => x.AttachmentFileName).HasMaxLength(255);
            entity.Property(x => x.AttachmentContentType).HasMaxLength(100);
            entity.Property(x => x.Status).HasDefaultValue(TransactionStatus.Confirmed);
            entity.HasIndex(x => new { x.UserId, x.Date });
        });

        builder.Entity<Income>(entity =>
        {
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.Property(x => x.Status).HasDefaultValue(TransactionStatus.Confirmed);
            entity.HasIndex(x => new { x.UserId, x.Date });
        });

        builder.Entity<IncomeSource>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.MonthlyAmount).HasColumnType("decimal(18,2)");
            entity.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
        });

        builder.Entity<MonthlySummary>(entity =>
        {
            entity.Property(x => x.TotalIncome).HasColumnType("decimal(18,2)");
            entity.Property(x => x.TotalExpense).HasColumnType("decimal(18,2)");
            entity.Property(x => x.ClosingBalance).HasColumnType("decimal(18,2)");
            entity.Property(x => x.DailyAllowance).HasColumnType("decimal(18,2)");
            entity.HasIndex(x => new { x.UserId, x.Year, x.Month }).IsUnique();
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.TokenHash }).IsUnique();
        });
    }

    private void ApplyAuditTimestamps()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = utcNow;
                entry.Entity.UpdatedAtUtc = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = utcNow;
            }
        }
    }
}
