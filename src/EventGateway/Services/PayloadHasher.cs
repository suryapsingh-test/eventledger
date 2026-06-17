using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EventLedger.Contracts.Events;

namespace EventGateway.Services;

public static class PayloadHasher
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string ComputeHash(EventRequest request)
    {
        var canonical = new
        {
            request.EventId,
            request.AccountId,
            request.Type,
            Amount = decimal.Round(request.Amount, 2),
            request.Currency,
            request.EventTimestamp
        };

        var json = JsonSerializer.Serialize(canonical, CanonicalOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
