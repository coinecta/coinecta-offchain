using Cardano.Sync.Data.Models;
using Cardano.Sync.Data.Models.Datums;
using Chrysalis.Cardano.Models.Coinecta.Vesting;
using Chrysalis.Cardano.Models.Core;
using Chrysalis.Cbor;
using TransactionOutput = Chrysalis.Cardano.Models.Core.TransactionOutput;
namespace Coinecta.Data.Models.Entity;

public record VestingTreasuryById
{
    public string Id { get; init; } = default!;
    public uint Slot { get; init; } = 0;
    public string TxHash { get; init; } = default!;
    public uint TxIndex { get; init; } = 0;
    public string OwnerPkh { get; init; } = default!;
    public string RootHash { get; init; } = default!;
    public byte[] UtxoRaw { get; init; } = default!;

    public TransactionOutput? Utxo => CborSerializer.Deserialize<TransactionOutput>(UtxoRaw);
    public Treasury? TreasuryDatum => Utxo switch
    {
        BabbageTransactionOutput babbage => babbage.Datum switch
        {
            InlineDatumOption inlineDatum => CborSerializer.Deserialize<Treasury>(inlineDatum.Data.Value),
            _ => null
        },
        _ => null
    };
}


