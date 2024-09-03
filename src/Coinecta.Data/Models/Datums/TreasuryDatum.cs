using Chrysalis.Cardano.Models;
using Chrysalis.Cbor;

namespace Coinecta.Data.Models;

[CborSerializable(CborType.Constr, Index = 0)]
public record TreasuryDatum(
    MultisigScript Owner,
    CborBytes TreasuryRootHash,
    PosixTime UnlockTime
) : ICbor;