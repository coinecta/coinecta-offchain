using CardanoSharp.Wallet.Extensions.Models;
using Chrysalis.Cardano.Models.Cbor;
using Chrysalis.Cardano.Models.Coinecta.Vesting;
using Chrysalis.Cardano.Models.Core;
using Chrysalis.Cardano.Models.Sundae;
using Chrysalis.Cbor;
using Coinecta.Data.Models;
using Coinecta.Data.Models.Api.Request;
using CAddress = CardanoSharp.Wallet.Models.Addresses.Address;
using ClaimEntry = Chrysalis.Cardano.Models.Coinecta.Vesting.ClaimEntry;
using Signature = Chrysalis.Cardano.Models.Sundae.Signature;

namespace Coinecta.Data.Extensions;

public static class TreasuryTrieDataExtension
{
    public static CreateMpfRequest ToMpfRequest(this TreasuryTrieData self)
    {
        CborBytes vestingProgram = new(Convert.FromHexString(self.VestingProgramScriptHash));
        CborBytes vestingParams = new(Convert.FromHexString(self.VestingParameters));
        Dictionary<string, string> claimEntries = self.ClaimEntries.ToDictionary(
            kvp =>
            {
                CAddress claimantAddress = new(kvp.Key);
                MultisigScript claimantSignature = new Signature(new(claimantAddress.GetPublicKeyHash()));
                string serializedClaimantSignature = Convert.ToHexString(CborSerializer.Serialize(claimantSignature)).ToLowerInvariant();
                return serializedClaimantSignature;
            },
            kvp =>
            {
                CAddress claimantAddress = new(kvp.Key);
                MultisigScript claimant = new Signature(new(claimantAddress.GetPublicKeyHash()));
                MultiAsset directValue = kvp.Value.DirectValue?.ToChrysalisValue() ?? new([]);
                MultiAsset vestingValue = kvp.Value.VestingValue?.ToChrysalisValue() ?? new([]);
                ClaimEntry claimEntry = new(claimant, vestingValue, directValue, vestingParams, vestingProgram);
                string serializedClaimEntry = Convert.ToHexString(CborSerializer.Serialize(claimEntry)).ToLowerInvariant();
                return serializedClaimEntry;
            }
        );

        return new CreateMpfRequest(claimEntries);
    }
}