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

namespace Coinecta.API.Modules.V1;

public class TransactionHandler(
    IDbContextFactory<CoinectaDbContext> dbContextFactory,
    IConfiguration configuration
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

        string spendOutRef = request.SpendOutRef.TxHash.ToLowerInvariant() + request.SpendOutRef.Index;
        VestingTreasuryById vestingTreasuryById = await dbContext.VestingTreasuryById
            .AsNoTracking()
            .Where(vtbi => vtbi.TxHash + vtbi.TxIndex == spendOutRef)
            .FirstOrDefaultAsync() ?? throw new Exception("Vesting treasury not found");
        Treasury datum = vestingTreasuryById.TreasuryDatum!;
        CSyncTransactionOutput utxo = vestingTreasuryById.Utxo!.ToCardanoSync();
        Value? lockedValue = utxo.Amount;
        (Address treasuryIdMintingWalletAddr, PublicKey vkey, PrivateKey skey) = CoinectaUtils.GetTreasuryIdMintingScriptWallet(configuration);
        INativeScriptBuilder treasuryIdMintingScript = CoinectaUtils.GetTreasuryIdMintingScriptBuilder(configuration);
        byte[] treasuryIdMintingPolicyBytes = treasuryIdMintingScript.Build().GetPolicyId();
        string treasuryIdMintingPolicy = Convert.ToHexString(treasuryIdMintingPolicyBytes).ToLowerInvariant();

        Address ownerAddr = new(request.OwnerAddress);
        Address treasuryAddr = new(configuration["TreasuryAddress"] ?? throw new Exception("Treasury address not configured."));
        Utxo collateralUtxo = TransactionUtils.DeserializeUtxoCborHex([request.RawCollateralUtxo]).First();
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
            TransactionId = Convert.FromHexString(request.SpendOutRef.TxHash),
            TransactionIndex = request.SpendOutRef.Index,
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

    public async Task<string> TreasuryClaimAsync(TreasuryClaimRequest request)
    {
        /// Prepare needed data
        using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

        Address ownerAddr = new(request.OwnerAddress);
        Address treasuryAddr = new(configuration["TreasuryAddress"] ?? throw new Exception("Treasury address not configured."));
        IEnumerable<Utxo> utxos = TransactionUtils.DeserializeUtxoCborHex(request.RawUtxos);
        Utxo collateralUtxo = TransactionUtils.DeserializeUtxoCborHex([request.RawCollateralUtxo]).First();
        TransactionInput treasuryValidatorReferenceInput = CoinectaUtils.GetTreasuryReferenceInput(configuration);
        (Address treasuryIdMintingWalletAddr, PublicKey vkey, PrivateKey skey) = CoinectaUtils.GetTreasuryIdMintingScriptWallet(configuration);
        INativeScriptBuilder treasuryIdMintingScript = CoinectaUtils.GetTreasuryIdMintingScriptBuilder(configuration);
        byte[] treasuryIdMintingPolicyBytes = treasuryIdMintingScript.Build().GetPolicyId();
        string treasuryIdMintingPolicy = Convert.ToHexString(treasuryIdMintingPolicyBytes).ToLowerInvariant();

        string spendOutRef = request.SpendOutRef.TxHash.ToLowerInvariant() + request.SpendOutRef.Index;
        VestingTreasuryById vestingTreasuryById = await dbContext.VestingTreasuryById
            .AsNoTracking()
            .Where(vtbi => vtbi.TxHash + vtbi.TxIndex == spendOutRef)
            .FirstOrDefaultAsync() ?? throw new Exception("Vesting treasury not found");
        Treasury datum = vestingTreasuryById.TreasuryDatum!;

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
            TransactionId = Convert.FromHexString(request.SpendOutRef.TxHash),
            TransactionIndex = request.SpendOutRef.Index,
            Output = lockedTreasuryOutput
        };

        // There could be up to 3 outputs: direct claim, vested claim and output returned back to treasury
        // Initially it would be 2 outputs only: direct claim and returned back to treasury
        // But it is possible that there would only be one output, and that is when there's no more assets left in the treasury?
        // @TODO: Handle all scenarios

        // Build the direct claim output
        TransactionOutput? directClaimOutput = request.DirectClaimValue is not null ? new()
        {
            Address = ownerAddr.GetBytes(),
            Value = new()
            {
                Coin = request.DirectClaimValue.Coin,
                MultiAsset = request.DirectClaimValue.MultiAsset.ToNativeAsset()
            }
        } : null;

        // Make sure the min utxo is met, in case user is claiming low ADA or non-ADA asset
        if (directClaimOutput is not null)
        {
            ulong minUtxoLovelace = directClaimOutput.CalculateMinUtxoLovelace();
            directClaimOutput.Value.Coin = Math.Max(directClaimOutput.Value.Coin, minUtxoLovelace);
        }

        // @TODO: Vested claim output

        // Build the treasury return output
        TransactionOutputValue treasuryReturnOutputValue = new()
        {
            Coin = lockedValue.Coin - (request.DirectClaimValue?.Coin ?? 0) - (request.VestedClaimValue?.Coin ?? 0),
            MultiAsset = lockedValue.MultiAsset
                .Subtract(request.DirectClaimValue?.MultiAsset ?? [])
                .Subtract(request.VestedClaimValue?.MultiAsset ?? [])
                .ToNativeAsset()
        };

        TransactionOutput? treasuryReturnOutput = new()
        {
            Address = treasuryAddr.GetBytes(),
            Value = treasuryReturnOutputValue,
            DatumOption = new()
            {
                RawData = Convert.FromHexString(request.ReturnDatum)
            }
        };

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
            .SetPlutusData(CBORObject.DecodeFromBytes(Convert.FromHexString(request.Redeemer)).GetPlutusData()) as RedeemerBuilder;

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

            return Convert.ToHexString(tx.Serialize());
        }
        catch (Exception ex)
        {
            throw new Exception($"Error building transaction: {ex.Message}. Please contact support for assistance.");
        }
    }
}