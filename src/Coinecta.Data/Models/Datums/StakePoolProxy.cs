using System.Formats.Cbor;
using CborSerialization;

namespace Coinecta.Data.Models.Datums;

public record StakePoolProxy(Signature Owner, ulong LockTime, Rational RewardMultiplier, byte[] PolicyId, byte[] AssetName, byte[] StakeMintingPolicyId) : IDatum;