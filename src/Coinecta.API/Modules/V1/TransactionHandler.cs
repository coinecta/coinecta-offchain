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
using Chrysalis.Cardano.Models;
using Chrysalis.Cbor;
using TransactionOutput = CardanoSharp.Wallet.Models.Transactions.TransactionOutput;
using TransactionInput = CardanoSharp.Wallet.Models.Transactions.TransactionInput;
using Chrysalis.Utils;

namespace Coinecta.API.Modules.V1;

public class TransactionHandler(IConfiguration configuration)
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
        var treasuryIdMintingScript = CoinectaUtils.GetTreasuryIdMintingScriptBuilder(configuration);
        byte[] treasuryIdMintingPolicyBytes = treasuryIdMintingScript.Build().GetPolicyId();
        string treasuryIdMintingPolicy = Convert.ToHexString(treasuryIdMintingPolicyBytes).ToLowerInvariant();
        (Address treasuryIdMintingWalletAddr, PublicKey vkey, PrivateKey skey) = CoinectaUtils.GetTreasuryIdMintingScriptWallet(configuration);

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

        TreasuryDatum datum = new(
            new Signature(new(ownerAddr.GetPublicKeyHash())),
            new(Convert.FromHexString(request.TreasuryRootHash)),
            new(request.UnlockTime)
        );

        TransactionOutput treasuryOutput = new()
        {
            Address = treasuryAddr.GetBytes(),
            Value = new()
            {
                Coin = request.Amount.Coin,
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

    public string TreasuryWithdraw(TreasuryWithdrawRequest request)
    {
        // Prepare needed data
        Address ownerAddr = new(request.OwnerAddress);
        Address treasuryAddr = new(configuration["TreasuryAddress"] ?? throw new Exception("Treasury address not configured."));
        Utxo collateralUtxo = TransactionUtils.DeserializeUtxoCborHex([request.RawCollateralUtxo]).First();
        TransactionInput treasuryValidatorReferenceInput = CoinectaUtils.GetTreasuryReferenceInput(configuration);

        // @TODO: Use datum from database once it's available
        // in the meantime, we temporarily pass the datum in the request body
        TreasuryDatum datum = CborSerializer.Deserialize<TreasuryDatum>(Convert.FromHexString(request.Datum)) ?? throw new Exception("Invalid datum");
        byte[] treasuryOwnerPkh = datum.Owner switch
        {
            Signature sig => sig.KeyHash.Value,
            _ => throw new Exception("Only signature is currently supported")
        };

        // Redeemer
        TreasuryWithdrawRedeemer redeemer = new();

        // @TODO: Use value from database when available
        // in the meantime, we temporarily pass the value in the request body
        // Rebuild locked UTxO input with value
        TransactionOutputValue lockedTreasuryOutputValue = new()
        {
            Coin = request.LockedValue.Coin,
            MultiAsset = request.LockedValue.MultiAsset.ToNativeAsset()
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
            Value = lockedTreasuryOutputValue
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

        // Redeemers
        RedeemerBuilder? withdrawRedeemerBuilder = RedeemerBuilder.Create
            .SetTag(RedeemerTag.Spend)
            .SetIndex(0)
            .SetPlutusData(CBORObject.DecodeFromBytes(CborSerializer.Serialize(redeemer)).GetPlutusData()) as RedeemerBuilder;

        txBodyBuilder.SetScriptDataHash([withdrawRedeemerBuilder!.Build()], [], CostModelUtility.PlutusV2CostModel.Serialize());

        // WitnessSet
        ITransactionWitnessSetBuilder txWitnessSetBuilder = TransactionWitnessSetBuilder.Create;
        txWitnessSetBuilder.AddRedeemer(withdrawRedeemerBuilder);

        // Required Signers
        txBodyBuilder.AddRequiredSigner(treasuryOwnerPkh);

        // Validity Interval
        NetworkType network = NetworkUtils.GetNetworkType(configuration);
        long currentSlot = SlotUtility.GetSlotFromUTCTime(SlotUtility.GetSlotNetworkConfig(network), DateTime.UtcNow);
        txBodyBuilder.SetValidAfter((uint)currentSlot);
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

    public string TreasuryClaim(TreasuryClaimRequest request)
    {
        // Prepare needed data
        Address ownerAddr = new(request.OwnerAddress);
        Address treasuryAddr = new(configuration["TreasuryAddress"] ?? throw new Exception("Treasury address not configured."));
        IEnumerable<Utxo> utxos = TransactionUtils.DeserializeUtxoCborHex(request.RawUtxos);
        Utxo collateralUtxo = TransactionUtils.DeserializeUtxoCborHex([request.RawCollateralUtxo]).First();
        TransactionInput treasuryValidatorReferenceInput = CoinectaUtils.GetTreasuryReferenceInput(configuration);

        // Rebuild locked UTxO input with value
        TransactionOutputValue lockedTreasuryOutputValue = new()
        {
            Coin = request.LockedValue.Coin,
            MultiAsset = request.LockedValue.MultiAsset.ToNativeAsset()
        };

        TransactionOutput lockedTreasuryOutput = new()
        {
            Address = treasuryAddr.GetBytes(),
            Value = lockedTreasuryOutputValue,
            DatumOption = new()
            {
                RawData = Convert.FromHexString(request.Datum)
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
            Coin = request.LockedValue.Coin - (request.DirectClaimValue?.Coin ?? 0) - (request.VestedClaimValue?.Coin ?? 0),
            MultiAsset = request.LockedValue.MultiAsset
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
            txBodyBuilder.AddOutput(treasuryReturnOutput);

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