using Chrysalis.Cardano.Models;
using Chrysalis.Cbor;

namespace Coinecta.Data.Models;

[CborSerializable(CborType.Constr, Index = 0)]
public record ClaimEntry(
    MultisigScript Claimant,
    MultiAsset VestingValue,
    MultiAsset DirectValue,
    CborBytes VestingParameters,
    CborBytes VestingProgram
) : ICbor;