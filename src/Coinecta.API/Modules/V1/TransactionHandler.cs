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

namespace Coinecta.API.Modules.V1;

public class TransactionHandler(IConfiguration configuration)
{
    public string Finalize(FinalizeTransactionRequest request)
    {
        Transaction tx = Convert.FromHexString(request.UnsignedTxCbor).DeserializeTransaction();
        return tx.Sign(request.TxWitnessCbor);
    }

    // @TODO: NFT identifier minting
    public string CreateTreasury(CreateTreasuryRequest request)
    {
        Address ownerAddr = new(request.OwnerAddress);
        Address treasuryAddr = new(configuration["TreasuryAddress"] ?? throw new Exception("Treasury address not configured."));
        IEnumerable<Utxo> utxos = TransactionUtils.DeserializeUtxoCborHex(request.RawUtxos);

        // Build treasury output
        TransactionOutput treasuryOutput = new()
        {
            Address = treasuryAddr.GetBytes(),
            Value = new()
            {
                Coin = request.Amount.Coin,
                MultiAsset = request.Amount.MultiAsset.ToNativeAsset()
            },
            DatumOption = new()
            {
                RawData = Convert.FromHexString(request.Datum)
            }
        };

        // Build the transaction body
        ITransactionBodyBuilder txBodyBuilder = TransactionBodyBuilder.Create;

        CoinSelection changeOutputCoinSelection = TransactionUtils.GetCoinSelection(
            [treasuryOutput],
            utxos,
            request.OwnerAddress,
            null,
            null,
            null
        );

        // Inputs
        changeOutputCoinSelection.Inputs.ForEach(input => txBodyBuilder.AddInput(input));

        // Outputs
        txBodyBuilder.AddOutput(treasuryOutput);
        changeOutputCoinSelection.ChangeOutputs.ForEach(output => txBodyBuilder.AddOutput(output));

        // Build the transaction
        ITransactionBuilder txBuilder = TransactionBuilder.Create;
        txBuilder.SetBody(txBodyBuilder);
        txBuilder.SetWitnesses(TransactionWitnessSetBuilder.Create);

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
            .SetPlutusData(CBORObject.DecodeFromBytes(Convert.FromHexString(request.Redeemer)).GetPlutusData()) as RedeemerBuilder;

        txBodyBuilder.SetScriptDataHash([withdrawRedeemerBuilder!.Build()], [], CostModelUtility.PlutusV2CostModel.Serialize());

        // WitnessSet
        ITransactionWitnessSetBuilder txWitnessSetBuilder = TransactionWitnessSetBuilder.Create;
        txWitnessSetBuilder.AddRedeemer(withdrawRedeemerBuilder);

        // Required Signers
        txBodyBuilder.AddRequiredSigner(ownerAddr.GetPublicKeyHash());

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

        // @TODO: Vested claim output
        // For now this is not needed so we skip this part

        // Build the treasury return output

        // @TODO: Deduct both direct and vested values
        TransactionOutputValue treasuryReturnOutputValue = new()
        {
            Coin = request.LockedValue.Coin,
            MultiAsset = request.LockedValue.MultiAsset.ToNativeAsset()
        };

        TransactionOutput? treasuryReturnOutput = new()
        {
            Address = treasuryAddr.GetBytes(),
            Value = lockedTreasuryOutputValue,
            DatumOption = new()
            {
                RawData = Convert.FromHexString(request.ReturnDatum)
            }
        };

        return string.Empty;
    }
}