using System.Globalization;
using AccountService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AccountService.Data;

public sealed class AccountDbContext(DbContextOptions<AccountDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var decimalToString = new ValueConverter<decimal, string>(
            value => value.ToString("F2", CultureInfo.InvariantCulture),
            value => decimal.Parse(value, CultureInfo.InvariantCulture));

        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("Accounts");
            entity.HasKey(account => account.AccountId);
            entity.Property(account => account.AccountId).HasMaxLength(64);
            entity.Property(account => account.CreatedAt).IsRequired();
            entity.Property(account => account.Currency).IsRequired().HasMaxLength(8);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("Transactions");
            entity.HasKey(transaction => transaction.Id);
            entity.Property(transaction => transaction.Id).ValueGeneratedOnAdd();
            entity.Property(transaction => transaction.EventId).IsRequired().HasMaxLength(128);
            entity.Property(transaction => transaction.AccountId).IsRequired().HasMaxLength(64);
            entity.Property(transaction => transaction.Type).IsRequired().HasMaxLength(16);
            entity.Property(transaction => transaction.Amount)
                .IsRequired()
                .HasConversion(decimalToString);
            entity.Property(transaction => transaction.Currency).IsRequired().HasMaxLength(8);
            entity.Property(transaction => transaction.EventTimestamp).IsRequired();
            entity.Property(transaction => transaction.AppliedAt).IsRequired();

            entity.HasIndex(transaction => transaction.EventId).IsUnique();
            entity.HasIndex(transaction => new { transaction.AccountId, transaction.EventTimestamp });

            entity.HasOne(transaction => transaction.Account)
                .WithMany(account => account.Transactions)
                .HasForeignKey(transaction => transaction.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
