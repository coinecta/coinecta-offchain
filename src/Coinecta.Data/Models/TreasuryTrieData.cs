using Cardano.Sync.Data.Models;

namespace Coinecta.Data.Models;

public record ClaimEntry(Value? VestingValue, Value? DirectValue, string? VestingProgramScriptHash, string? VestingParameters);

public record TreasuryTrieData(
    Dictionary<string, ClaimEntry> ClaimEntries,
    string VestingProgramScriptHash,
    string VestingParameters
);