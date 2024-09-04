
using Chrysalis.Cardano.Models;
using Chrysalis.Cbor;
using Coinecta.Data.Models.Datums.MPF;

namespace Coinecta.Data.Models.Datums;

[CborSerializable(CborType.Union)]
[CborUnionTypes([typeof(Proof), typeof(TreasuryWithdrawRedeemer)])]
public record TreasuryRedeemer : ICbor;

[CborSerializable(CborType.Constr, Index = 0)]
public record ClaimRedeemer(
    [CborProperty(0)]
    Proof Proof,

    [CborProperty(1)]
    ClaimEntry ClaimEntry
) : TreasuryRedeemer;

[CborSerializable(CborType.Constr, Index = 1)]
public record TreasuryWithdrawRedeemer() : TreasuryRedeemer;