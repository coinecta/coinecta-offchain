using Coinecta.Data.Models;
using CardanoSharp.Wallet.CIPs.CIP2.Models;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Models;
using CardanoSharp.Wallet.Models.Addresses;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.Models.Transactions.TransactionWitness.PlutusScripts;
using CardanoSharp.Wallet.TransactionBuilding;
using Coinecta.Data.Extensions;
using Coinecta.Data.Utils;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Enums;
using PeterO.Cbor2;
using CardanoSharp.Wallet.Utilities;
using CardanoSharp.Wallet.Extensions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CardanoSharp.Wallet.Models.Keys;
using Chrysalis.Cbor;
using TransactionOutput = CardanoSharp.Wallet.Models.Transactions.TransactionOutput;
using TransactionInput = CardanoSharp.Wallet.Models.Transactions.TransactionInput;
using CSyncTransactionOutput = Cardano.Sync.Data.Models.TransactionOutput;
using Microsoft.EntityFrameworkCore;
using Coinecta.Data.Models.Entity;
using Cardano.Sync.Data.Models;
using Chrysalis.Cardano.Models.Coinecta.Vesting;
using Chrysalis.Cardano.Models.Sundae;
using Coinecta.Data.Extensions.Chrysalis;
using Coinecta.Data.Services;
using Coinecta.Data.Models.Api.Request;
using ClaimEntry = Chrysalis.Cardano.Models.Coinecta.Vesting.ClaimEntry;
using Chrysalis.Cardano.Models.Cbor;
using Chrysalis.Cardano.Models.Mpf;
using Coinecta.Data.Models.Api.Response;

namespace Coinecta.API.Modules.V1;

public class TransactionHandler(
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    IConfiguration configuration,
    MpfService mpfService,
    S3Service s3Service,
    TreasuryHandler treasuryHandler
)
{
    public string Finalize(FinalizeTransactionRequest request)
    {
        Transaction tx = Convert.FromHexString(request.UnsignedTxCbor).DeserializeTransaction();
        return tx.Sign(request.TxWitnessCbor);
    }

    public string CreateTreasury(CreateTreasuryRequest request)
    {
        Address ownerAddr = new(request.OwnerAddress);
        Address treasuryAddr = new(configuration["TreasuryAddress"] ?? throw new Exception("Treasury address not configured."));
        IEnumerable<Utxo> utxos = TransactionUtils.DeserializeUtxoCborHex(request.RawUtxos);
        INativeScriptBuilder treasuryIdMintingScript = CoinectaUtils.GetTreasuryIdMintingScriptBuilder(configuration);
        byte[] treasuryIdMintingPolicyBytes = treasuryIdMintingScript.Build().GetPolicyId();
        string treasuryIdMintingPolicy = Convert.ToHexString(treasuryIdMintingPolicyBytes).ToLowerInvariant();
        (Address treasuryIdMintingWalletAddr, PublicKey vkey, PrivateKey skey) = CoinectaUtils.GetTreasuryIdMintingScriptWallet(configuration);
        ulong treasuryExtraFund = configuration.GetValue("TreasuryExtraLovelaceFund", 5_000_000UL);

        // Build treasury output
        byte[] idBytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request)));
        string idHex = Convert.ToHexString(idBytes).ToLowerInvariant();

        ITokenBundleBuilder idAssetBundleBuilder = TokenBundleBuilder.Create;
        idAssetBundleBuilder.AddToken(treasuryIdMintingPolicyBytes, Convert.FromHexString(idHex[..32]), 1);

        Dictionary<string, ulong> idAsset = new(){
            {idHex[..32], 1}
        };

        Dictionary<string, Dictionary<string, ulong>> updatedTreasuryOutputAsset = request.Amount.MultiAsset;
        updatedTreasuryOutputAsset.Add(treasuryIdMintingPolicy, idAsset);

        Treasury datum = new(
            new Signature(new(ownerAddr.GetPublicKeyHash())),
            new(Convert.FromHexString(request.TreasuryRootHash)),
            new(request.UnlockTime)
        );

        TransactionOutput treasuryOutput = new()
        {
            Address = treasuryAddr.GetBytes(),
            Value = new()
            {
                Coin = request.Amount.Coin + treasuryExtraFund,
                MultiAsset = updatedTreasuryOutputAsset.ToNativeAsset()
            },
            DatumOption = new()
            {
                RawData = CborSerializer.Serialize((ICbor)datum)
            }
        };

        // Build the transaction body
        ITransactionBodyBuilder txBodyBuilder = TransactionBodyBuilder.Create;

        CoinSelection changeOutputCoinSelection = TransactionUtils.GetCoinSelection(
            [treasuryOutput],
            utxos,
            request.OwnerAddress,
            idAssetBundleBuilder,
            null,
            null
        );

        // Inputs
        changeOutputCoinSelection.Inputs.ForEach(input => txBodyBuilder.AddInput(input));

        // Outputs
        txBodyBuilder.AddOutput(treasuryOutput);
        changeOutputCoinSelection.ChangeOutputs.ForEach(output => txBodyBuilder.AddOutput(output));

        // Mint
        txBodyBuilder.SetMint(idAssetBundleBuilder);

        // Witness set
        ITransactionWitnessSetBuilder txWitnessSetBuilder = TransactionWitnessSetBuilder.Create;
        txWitnessSetBuilder.AddNativeScript(treasuryIdMintingScript);
        txWitnessSetBuilder.AddVKeyWitness(vkey, skey);

        // Build the transaction
        ITransactionBuilder txBuilder = TransactionBuilder.Create;
        txBuilder.SetBody(txBodyBuilder);
        txBuilder.SetWitnesses(txWitnessSetBuilder);

        try
        {
            Transaction tx = txBuilder.Build();
            uint fee = tx.CalculateAndSetFee(numberOfVKeyWitnessesToMock: 1);
            tx.TransactionBody.TransactionOutputs.Last().Value.Coin -= fee;

            return Convert.ToHexString(tx.Serialize());
        }
        catch (Exception ex)
        {
            throw new Exception($"Error building transaction: {ex.Message}. Please contact support for assistance.");
        }
    }

    public async Task<string> TreasuryWithdrawAsync(TreasuryWithdrawRequest request)
    {
        // Prepare needed data
        using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();
        VestingTreasuryById? vestingTreasuryById = request switch
        {
            { Id: not null } => await dbContext.VestingTreasuryById.FetchId(request.Id),
            { SpendOutRef: not null } => await dbContext.VestingTreasuryById.FetchOutref(request.SpendOutRef),
            _ => null
        } ?? throw new Exception("Treasury not found");

        Treasury datum = vestingTreasuryById.TreasuryDatum!;

        // Addresses
        Address ownerAddr = new(request.OwnerAddress);
        Address treasuryAddr = new(configuration["TreasuryAddress"] ?? throw new Exception("Treasury address not configured."));
        (Address treasuryIdMintingWalletAddr, PublicKey vkey, PrivateKey skey) = CoinectaUtils.GetTreasuryIdMintingScriptWallet(configuration);

        // Utxos
        CSyncTransactionOutput utxo = vestingTreasuryById.Utxo!.ToCardanoSync();
        Utxo collateralUtxo = TransactionUtils.DeserializeUtxoCborHex([request.RawCollateralUtxo]).First();
        Value? lockedValue = utxo.Amount;

        // Minting details
        INativeScriptBuilder treasuryIdMintingScript = CoinectaUtils.GetTreasuryIdMintingScriptBuilder(configuration);
        byte[] treasuryIdMintingPolicyBytes = treasuryIdMintingScript.Build().GetPolicyId();
        string treasuryIdMintingPolicy = Convert.ToHexString(treasuryIdMintingPolicyBytes).ToLowerInvariant();

        // Reference Inputs
        TransactionInput treasuryValidatorReferenceInput = CoinectaUtils.GetTreasuryReferenceInput(configuration);


        // in the meantime, we temporarily pass the datum in the request body
        byte[] treasuryOwnerPkh = datum.Owner switch
        {
            Signature sig => sig.KeyHash.Value,
            _ => throw new Exception("Only signature is currently supported")
        };

        // Redeemer
        TreasuryWithdrawRedeemer redeemer = new();

        // Burn asset
        string assetName = lockedValue?.MultiAsset.GetValueOrDefault(treasuryIdMintingPolicy)?.Keys.First() ?? throw new Exception("Id asset not found");

        ITokenBundleBuilder idAssetBurnTokenBundleBuilder = TokenBundleBuilder.Create;
        idAssetBurnTokenBundleBuilder.AddToken(treasuryIdMintingPolicyBytes, Convert.FromHexString(assetName), -1);

        Dictionary<string, Dictionary<string, ulong>> burnValue = new()
        {
            { treasuryIdMintingPolicy, new Dictionary<string, ulong>() {
                { assetName, 1 }
            }}
        };

        TransactionOutputValue lockedTreasuryOutputValue = new()
        {
            Coin = lockedValue!.Coin,
            MultiAsset = lockedValue.MultiAsset.ToNativeAsset()
        };

        var test = lockedValue.MultiAsset.Subtract(burnValue);

        TransactionOutputValue withdrawalOutputValue = new()
        {
            Coin = lockedValue!.Coin,
            MultiAsset = lockedValue.MultiAsset.Subtract(burnValue).ToNativeAsset()
        };

        TransactionOutput lockedTreasuryOutput = new()
        {
            Address = treasuryAddr.GetBytes(),
            Value = lockedTreasuryOutputValue,
            DatumOption = new()
            {
                RawData = CborSerializer.Serialize(datum)
            }
        };

        TransactionInput lockedTreasuryInput = new()
        {
            TransactionId = Convert.FromHexString(vestingTreasuryById.TxHash),
            TransactionIndex = vestingTreasuryById.TxIndex,
            Output = lockedTreasuryOutput
        };

        // Build Output sent to user/admin wallet
        TransactionOutput withdrawalOutput = new()
        {
            Address = ownerAddr.GetBytes(),
            Value = withdrawalOutputValue
        };

        // Build Transaction Body
        ITransactionBodyBuilder txBodyBuilder = TransactionBodyBuilder.Create;

        // Inputs
        txBodyBuilder.AddInput(lockedTreasuryInput);

        // Outputs
        txBodyBuilder.AddOutput(withdrawalOutput);

        // Reference Script Inputs
        txBodyBuilder.AddReferenceInput(treasuryValidatorReferenceInput);

        // Collateral Input
        txBodyBuilder.AddCollateralInput(new()
        {
            TransactionId = Convert.FromHexString(collateralUtxo.TxHash),
            TransactionIndex = collateralUtxo.TxIndex
        });

        // Burn
        txBodyBuilder.SetMint(idAssetBurnTokenBundleBuilder);

        // Redeemers
        RedeemerBuilder? withdrawRedeemerBuilder = RedeemerBuilder.Create
            .SetTag(RedeemerTag.Spend)
            .SetIndex(0)
            .SetPlutusData(CBORObject.DecodeFromBytes(CborSerializer.Serialize(redeemer)).GetPlutusData()) as RedeemerBuilder;

        txBodyBuilder.SetScriptDataHash([withdrawRedeemerBuilder!.Build()], [], CostModelUtility.PlutusV2CostModel.Serialize());

        // WitnessSet
        ITransactionWitnessSetBuilder txWitnessSetBuilder = TransactionWitnessSetBuilder.Create;
        txWitnessSetBuilder.AddRedeemer(withdrawRedeemerBuilder);
        txWitnessSetBuilder.AddNativeScript(treasuryIdMintingScript);
        txWitnessSetBuilder.AddVKeyWitness(vkey, skey);

        // Required Signers
        txBodyBuilder.AddRequiredSigner(treasuryOwnerPkh);

        // Validity Interval
        NetworkType network = NetworkUtils.GetNetworkType(configuration);
        long currentSlot = SlotUtility.GetSlotFromUTCTime(SlotUtility.GetSlotNetworkConfig(network), DateTime.UtcNow);
        txBodyBuilder.SetValidAfter((uint)currentSlot - 100);
        txBodyBuilder.SetValidBefore((uint)(currentSlot + 1000));

        // Build Transaction
        ITransactionBuilder txBuilder = TransactionBuilder.Create;
        txBuilder.SetBody(txBodyBuilder);
        txBuilder.SetWitnesses(txWitnessSetBuilder);

        try
        {
            Transaction tx = txBuilder.BuildAndSetExUnits(network);
            uint fee = tx.CalculateAndSetFee(numberOfVKeyWitnessesToMock: 1);
            tx.TransactionBody.TransactionOutputs.Last().Value.Coin -= fee;

            return Convert.ToHexString(tx.Serialize());
        }
        catch (Exception ex)
        {
            throw new Exception($"Error building transaction: {ex.Message}. Please contact support for assistance.");
        }
    }

    public async Task<TreasuryClaimResponse> TreasuryClaimAsync(TreasuryClaimRequest request)
    {
        if (request.Id is null && request.SpendOutRef is null)
            throw new Exception("Please provide an id or outref to claim");

        // Addresses
        Address ownerAddr = new(request.OwnerAddress);
        Address treasuryAddr = new(configuration["TreasuryAddress"] ?? throw new Exception("Treasury address not configured."));
        (Address treasuryIdMintingWalletAddr, PublicKey vkey, PrivateKey skey) = CoinectaUtils.GetTreasuryIdMintingScriptWallet(configuration);

        // Fetch treasury from the database
        using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();
        VestingTreasuryById? vestingTreasuryById = request switch
        {
            { Id: not null } => await dbContext.VestingTreasuryById.FetchId(request.Id),
            { SpendOutRef: not null } => await dbContext.VestingTreasuryById.FetchOutref(request.SpendOutRef),
            _ => null
        } ?? throw new Exception("Treasury not found");

        // Extract needed data from treasury datum
        Treasury datum = vestingTreasuryById.TreasuryDatum!;
        string ownerPkh = Convert.ToHexString(ownerAddr.GetPublicKeyHash()).ToLower();

        // Fetch the claim entry
        string claimEntryId = vestingTreasuryById.RootHash + ownerPkh;
        VestingClaimEntryByRootHash? vestingClaimEntryByRootHash =
            await dbContext.VestingClaimEntryByRootHash.FetchIdAsync(claimEntryId) ?? throw new Exception("Claim entry not found");

        // If claim entry exists, get latest mpf data, proof and updated roothash
        string mpfBucket = configuration["MpfBucket"]!;
        string? mpfRawData = await s3Service.DownloadJsonAsync(mpfBucket, vestingTreasuryById.RootHash);
        CreateTreasuryTrieRequest treasuryTrieRequest = JsonSerializer.Deserialize<CreateTreasuryTrieRequest>(mpfRawData!) ?? throw new Exception("Invalid MPF data");

        // Fetch the proof and the original mpf data
        Dictionary<string, string> mpfData = treasuryTrieRequest.Data.ToMpfRequest().Data;
        byte[] claimRawKey = CborSerializer.Serialize(vestingClaimEntryByRootHash.ClaimEntry?.Claimant!);
        string claimKey = Convert.ToHexString(claimRawKey).ToLowerInvariant();
        string proofRaw = await mpfService.GetProofAsync(new(mpfData, claimKey));

        // Create updated mpf trie
        string originalRawClaimEntry = mpfData[claimKey];
        ClaimEntry claimEntry = CborSerializer.Deserialize<ClaimEntry>(Convert.FromHexString(originalRawClaimEntry))!;
        ClaimEntry updatedClaimEntry = claimEntry with
        {
            DirectValue = new([]),
            VestingValue = new([])
        };
        string updatedRawClaimEntry = Convert.ToHexString(CborSerializer.Serialize(updatedClaimEntry));
        mpfData[claimKey] = updatedRawClaimEntry;

        // @TODO: Remove this
        treasuryTrieRequest.Data.ClaimEntries[ownerAddr.ToString()] = treasuryTrieRequest.Data.ClaimEntries[ownerAddr.ToString()] with
        {
            DirectValue = new(),
            VestingValue = new()
        };

        CreateTreasuryTrieRequest updatedreasuryTrieRequest = treasuryTrieRequest with
        {
            Data = treasuryTrieRequest.Data
        };

        string updatedRootHash = await treasuryHandler.ExecuteCreateTrieAsync(updatedreasuryTrieRequest);

        Treasury updatedDatum = datum with
        {
            TreasuryRootHash = new(Convert.FromHexString(updatedRootHash))
        };
        TreasuryClaimRedeemer redeemer = new(
            CborSerializer.Deserialize<Proof>(Convert.FromHexString(proofRaw))!,
            claimEntry
        );

        // Utxos
        IEnumerable<Utxo> utxos = TransactionUtils.DeserializeUtxoCborHex(request.RawUtxos);
        Utxo collateralUtxo = TransactionUtils.DeserializeUtxoCborHex([request.RawCollateralUtxo]).First();

        // Reference Inputs
        TransactionInput treasuryValidatorReferenceInput = CoinectaUtils.GetTreasuryReferenceInput(configuration);

        // Minting scripts
        INativeScriptBuilder treasuryIdMintingScript = CoinectaUtils.GetTreasuryIdMintingScriptBuilder(configuration);
        byte[] treasuryIdMintingPolicyBytes = treasuryIdMintingScript.Build().GetPolicyId();
        string treasuryIdMintingPolicy = Convert.ToHexString(treasuryIdMintingPolicyBytes).ToLowerInvariant();

        CSyncTransactionOutput utxo = vestingTreasuryById.Utxo!.ToCardanoSync();
        Value? lockedValue = utxo.Amount;
        Datum? lockedDatum = utxo.Datum;

        // Rebuild locked UTxO input with value
        TransactionOutputValue lockedTreasuryOutputValue = new()
        {
            Coin = lockedValue!.Coin,
            MultiAsset = lockedValue.MultiAsset.ToNativeAsset()
        };

        TransactionOutput lockedTreasuryOutput = new()
        {
            Address = treasuryAddr.GetBytes(),
            Value = lockedTreasuryOutputValue,
            DatumOption = new()
            {
                RawData = lockedDatum?.Data
            }
        };

        TransactionInput lockedTreasuryInput = new()
        {
            TransactionId = Convert.FromHexString(vestingTreasuryById.TxHash),
            TransactionIndex = vestingTreasuryById.TxIndex,
            Output = lockedTreasuryOutput
        };

        // There could be up to 3 outputs: direct claim, vested claim and output returned back to treasury
        // Initially it would be 2 outputs only: direct claim and returned back to treasury
        // But it is possible that there would only be one output, and that is when there's no more assets left in the treasury?
        // @TODO: Handle all scenarios

        // Build the direct claim output
        Dictionary<string, Dictionary<string, ulong>> directValue = claimEntry.DirectValue.ToDictionary();
        ulong directCoin = 0;
        if (directValue.TryGetValue(string.Empty, out var directLovelace) && directLovelace != null)
        {
            if (directLovelace.TryGetValue(string.Empty, out var directLovelaceValue))
            {
                directCoin = directLovelaceValue;
            }

            directValue.Remove(string.Empty);
        }

        TransactionOutput? directClaimOutput = claimEntry.DirectValue is not null ? new()
        {
            Address = ownerAddr.GetBytes(),
            Value = new()
            {
                Coin = directCoin,
                MultiAsset = directValue.ToNativeAsset()
            }
        } : null;

        // Make sure the min utxo is met, in case user is claiming low ADA or non-ADA asset
        if (directClaimOutput is not null)
        {
            ulong minUtxoLovelace = directClaimOutput.CalculateMinUtxoLovelace();
            directClaimOutput.Value.Coin = Math.Max(directClaimOutput.Value.Coin, minUtxoLovelace);
        }

        // @TODO: Vested claim output
        Dictionary<string, Dictionary<string, ulong>> vestingValue = claimEntry.VestingValue.ToDictionary();
        ulong vestingCoin = 0;
        if (vestingValue.TryGetValue(string.Empty, out var vestingLovelace) && vestingLovelace != null)
        {
            if (vestingLovelace.TryGetValue(string.Empty, out var vestingLovelaceValue))
            {
                // Safely assign the value to directCoin
                vestingCoin = vestingLovelaceValue;
            }

            vestingValue.Remove(string.Empty);
        }

        // Build the treasury return output
        TransactionOutputValue treasuryReturnOutputValue = new()
        {
            Coin = lockedValue.Coin - directCoin - vestingCoin,
            MultiAsset = lockedValue.MultiAsset
                .Subtract(directValue)
                .Subtract(vestingValue)
                .ToNativeAsset()
        };

        TransactionOutput? treasuryReturnOutput = new()
        {
            Address = treasuryAddr.GetBytes(),
            Value = treasuryReturnOutputValue,
            DatumOption = new()
            {
                RawData = CborSerializer.Serialize(updatedDatum)
            }
        };

        string treasuryOutputRaw = Convert.ToHexString(treasuryReturnOutput.GetCBOR().EncodeToBytes()).ToLowerInvariant();

        // Build Transaction Body
        ITransactionBodyBuilder txBodyBuilder = TransactionBodyBuilder.Create;
        // Inputs
        txBodyBuilder.AddInput(lockedTreasuryInput);

        // Add return output if any
        if (treasuryReturnOutputValue.Coin > 0)
        {
            txBodyBuilder.AddOutput(treasuryReturnOutput);
        }
        else
        {
            // If there is no output to return, then we burn the id token
            string assetName = lockedValue?.MultiAsset.GetValueOrDefault(treasuryIdMintingPolicy)?.Keys.First() ?? throw new Exception("Id asset not found");
            ITokenBundleBuilder idAssetBurnTokenBundleBuilder = TokenBundleBuilder.Create;
            idAssetBurnTokenBundleBuilder.AddToken(treasuryIdMintingPolicyBytes, Convert.FromHexString(assetName), -1);

            // Burn
            txBodyBuilder.SetMint(idAssetBurnTokenBundleBuilder);
        }

        // Add direct claim output if any
        // @TODO: handle where to get fees when there's no direct claim
        // or if there's no ADA to claim

        // Add change output
        if (directClaimOutput is not null)
            txBodyBuilder.AddOutput(directClaimOutput);

        // Reference Script Inputs
        txBodyBuilder.AddReferenceInput(treasuryValidatorReferenceInput);

        // Collateral Input
        txBodyBuilder.AddCollateralInput(new()
        {
            TransactionId = Convert.FromHexString(collateralUtxo.TxHash),
            TransactionIndex = collateralUtxo.TxIndex
        });

        // Validity Interval
        NetworkType network = NetworkUtils.GetNetworkType(configuration);
        long currentSlot = SlotUtility.GetSlotFromUTCTime(SlotUtility.GetSlotNetworkConfig(network), DateTime.UtcNow);
        txBodyBuilder.SetValidAfter((uint)currentSlot - 100);
        txBodyBuilder.SetValidBefore((uint)(currentSlot + 1000));

        // Redeemers
        List<TransactionInput> txInputs = [.. txBodyBuilder.Build().TransactionInputs];
        List<string> txInputOutrefs = txInputs.Select(i => (Convert.ToHexString(i.TransactionId) + i.TransactionIndex).ToLower()).ToList();
        txInputOutrefs.Sort();

        uint redeemerIndex = (uint)txInputOutrefs.IndexOf(lockedTreasuryInput.TransactionId.ToStringHex().ToLowerInvariant() + lockedTreasuryInput.TransactionIndex);
        RedeemerBuilder? claimRedeemer = RedeemerBuilder.Create
            .SetTag(RedeemerTag.Spend)
            .SetIndex(redeemerIndex)
            .SetPlutusData(CBORObject.DecodeFromBytes(CborSerializer.Serialize(redeemer)).GetPlutusData()) as RedeemerBuilder;

        txBodyBuilder.SetScriptDataHash([claimRedeemer!.Build()], [], CostModelUtility.PlutusV2CostModel.Serialize());

        // Witness set
        ITransactionWitnessSetBuilder txWitnessSetBuilder = TransactionWitnessSetBuilder.Create;
        txWitnessSetBuilder.AddRedeemer(claimRedeemer);

        // Add id minting wallet signature if final claim
        if (treasuryReturnOutputValue.Coin <= 0)
        {
            txWitnessSetBuilder.AddNativeScript(treasuryIdMintingScript);
            txWitnessSetBuilder.AddVKeyWitness(vkey, skey);
        }

        // Required Signers
        txBodyBuilder.AddRequiredSigner(ownerAddr.GetPublicKeyHash());

        // Build Transaction
        ITransactionBuilder txBuilder = TransactionBuilder.Create;
        txBuilder.SetBody(txBodyBuilder);
        txBuilder.SetWitnesses(txWitnessSetBuilder);

        try
        {
            Transaction tx = txBuilder.BuildAndSetExUnits(network);
            uint fee = tx.CalculateAndSetFee(numberOfVKeyWitnessesToMock: 1);
            tx.TransactionBody.TransactionOutputs.Last().Value.Coin -= fee;

            return new(Convert.ToHexString(tx.Serialize()), treasuryOutputRaw);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error building transaction: {ex.Message}. Please contact support for assistance.");
        }
    }
}