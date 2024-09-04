using Chrysalis.Cardano.Models;
using Chrysalis.Cbor;

namespace Coinecta.Data.Models.Datums;

[CborSerializable(CborType.Constr, Index = 0)]
public record ClaimEntry(
    [CborProperty(0)]
    MultisigScript Claimant,

    [CborProperty(1)]
    MultiAsset VestingValue,

    [CborProperty(2)]
    MultiAsset DirectValue,

    [CborProperty(3)]
    CborBytes VestingParameters,

    [CborProperty(4)]
    CborBytes VestingProgram
) : ICbor;