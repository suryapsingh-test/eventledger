namespace AccountService.Data.Entities;

public sealed class Account
{
    public string AccountId { get; set; } = string.Empty;

    public string CreatedAt { get; set; } = string.Empty;

    public string Currency { get; set; } = string.Empty;

    public decimal Balance { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
