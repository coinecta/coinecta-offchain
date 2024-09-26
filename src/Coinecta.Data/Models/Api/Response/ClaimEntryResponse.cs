namespace Coinecta.Data.Models.Api.Response;

public record ClaimEntryResponse
(
    string Id,
    string RootHash, 
    string ClaimantPkh,
    Dictionary<string, Dictionary<string, ulong>>? VestingValue,
    Dictionary<string, Dictionary<string, ulong>>? DirectValue
);