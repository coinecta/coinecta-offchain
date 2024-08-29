using Cardano.Sync.Data.Models;

namespace Coinecta.Data.Models;

public record CreateTreasuryRequest(string OwnerAddress, Value Amount, string Datum, IEnumerable<string> RawUtxos);