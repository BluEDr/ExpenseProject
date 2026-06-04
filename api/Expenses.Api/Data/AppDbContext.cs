using Expenses.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Expenses.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseAttachment> ExpenseAttachments => Set<ExpenseAttachment>();
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
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(x => new { x.UserId, x.Name, x.Type }).IsUnique();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Expense>(entity =>
        {
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.Property(x => x.Status).HasDefaultValue(TransactionStatus.Confirmed);
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(x => new { x.UserId, x.Date });
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<ExpenseAttachment>(entity =>
        {
            entity.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.StoredFilePath).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.ExpenseId });
            entity.HasOne(x => x.Expense)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.ExpenseId);
        });

        builder.Entity<Income>(entity =>
        {
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.Property(x => x.Status).HasDefaultValue(TransactionStatus.Confirmed);
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(x => new { x.UserId, x.Date });
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<IncomeSource>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.MonthlyAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<MonthlySummary>(entity =>
        {
            entity.Property(x => x.StartingBalance).HasColumnType("decimal(18,2)");
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
        var serverNow = DateTime.Now;

        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = serverNow;
                entry.Entity.UpdatedAtUtc = serverNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = serverNow;
            }
        }

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified)
            {
                continue;
            }

            var isDeletedProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsDeleted");
            var deletedAtProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "DeletedAtUtc");

            if (isDeletedProperty == null || deletedAtProperty == null)
            {
                continue;
            }

            var isDeleted = isDeletedProperty.CurrentValue as bool?;
            var deletedAt = deletedAtProperty.CurrentValue as DateTime?;

            if (isDeleted == true && deletedAt == null)
            {
                deletedAtProperty.CurrentValue = serverNow;
            }
            else if (isDeleted == false && deletedAt != null)
            {
                deletedAtProperty.CurrentValue = null;
            }
        }
    }
}
