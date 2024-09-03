using Chrysalis.Cardano.Models;
using Chrysalis.Cbor;

namespace Coinecta.Data.Models;

[CborSerializable(CborType.Constr, Index = 1)]
public record TreasuryWithdrawRedeemer() : ICbor;