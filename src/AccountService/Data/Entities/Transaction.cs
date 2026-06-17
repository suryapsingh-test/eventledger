namespace AccountService.Data.Entities;

public sealed class Transaction
{
    public long Id { get; set; }

    public string EventId { get; set; } = string.Empty;

    public string AccountId { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string EventTimestamp { get; set; } = string.Empty;

    public string AppliedAt { get; set; } = string.Empty;

    public Account Account { get; set; } = null!;
}
