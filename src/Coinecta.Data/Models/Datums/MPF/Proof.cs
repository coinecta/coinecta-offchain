using Chrysalis.Cardano.Models;
using Chrysalis.Cbor;

namespace Coinecta.Data.Models.Datums.MPF;


[CborSerializable(CborType.Union)]
[CborUnionTypes([
    typeof(Branch),
    typeof(Fork),
    typeof(Leaf)
])]
public record ProofStep : ICbor;

[CborSerializable(CborType.Constr, Index = 0)]
public record Branch(
    [CborProperty(0)]
    CborInt Skip,

    [CborProperty(1)]
    CborBytes Neighbors
);

[CborSerializable(CborType.Constr, Index = 1)]
public record Fork(
    [CborProperty(0)]
    CborInt Skip,

    [CborProperty(1)]
    CborBytes Neighbor
);

[CborSerializable(CborType.Constr, Index = 2)]
public record Leaf(
    [CborProperty(0)]
    CborInt Skip,

    [CborProperty(1)]
    CborBytes Key,

    [CborProperty(2)]
    CborBytes Value
);

public record Proof(ProofStep[] ProofSteps) : CborIndefiniteList<ProofStep>(ProofSteps);