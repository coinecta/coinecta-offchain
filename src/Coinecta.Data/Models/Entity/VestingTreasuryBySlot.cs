using Cardano.Sync.Data.Models;
using Chrysalis.Cardano.Models.Coinecta.Vesting;
using Chrysalis.Cardano.Models.Core;
using Chrysalis.Cbor;
using Coinecta.Data.Models.Enums;
using TransactionOutput = Chrysalis.Cardano.Models.Core.TransactionOutput;

namespace Coinecta.Data.Models.Entity;

public record VestingTreasuryBySlot
{
    public uint Slot { get; init; } = 0;
    public string Id { get; init; } = default!;
    public string BlockHash { get; init; } = default!;
    public string TxHash { get; init; } = default!;
    public uint TxIndex { get; init; } = 0;
    public string OwnerPkh { get; init; } = default!;
    public UtxoStatus UtxoStatus { get; init; } = default!;
    public TreasuryActionType Type { get; init; } = TreasuryActionType.Create;
    public byte[] UtxoRaw { get; init; } = default!;


    public TransactionOutput? Utxo => CborSerializer.Deserialize<TransactionOutput>(UtxoRaw);
    public Treasury? TreasuryDatum => Utxo switch {
        BabbageTransactionOutput babbage => babbage.Datum switch {
            InlineDatumOption inlineDatum => CborSerializer.Deserialize<Treasury>(inlineDatum.Data.Value),
            _ => null
        },
        _ => null
    };
}