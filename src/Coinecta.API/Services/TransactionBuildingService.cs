using CardanoSharp.Wallet.CIPs.CIP2;
using CardanoSharp.Wallet.CIPs.CIP2.ChangeCreationStrategies;
using CardanoSharp.Wallet.CIPs.CIP2.Models;
using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Models;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.Models.Transactions.TransactionWitness;
using CardanoSharp.Wallet.Models.Transactions.TransactionWitness.PlutusScripts;
using CardanoSharp.Wallet.TransactionBuilding;
using CardanoSharp.Wallet.Utilities;
using Coinecta.API.Models.Request;
using Coinecta.API.Utils;
using Coinecta.Data;
using Coinecta.Data.Models.Datums;
using Coinecta.Data.Models.Reducers;
using Microsoft.EntityFrameworkCore;
using PeterO.Cbor2;
using Coinecta.API.Extensions;
using Address = CardanoSharp.Wallet.Models.Addresses.Address;
using CoinectaAddress = Cardano.Sync.Data.Models.Datums.Address;
using TransactionOutput = CardanoSharp.Wallet.Models.Transactions.TransactionOutput;
using RewardSetting = Coinecta.Data.Models.Datums.RewardSetting;
using CborSerialization;
using Cardano.Sync.Data.Models.Datums;

namespace Coinecta.API.Services;
public class TransactionBuildingService(IDbContextFactory<CoinectaDbContext> dbContextFactory, IConfiguration configuration)
{
    public async Task<string> AddStakeAsync(AddStakeRequest request)
    {
        using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

        // Fetch stake pool data
        StakePoolByAddress? stakePoolData = await dbContext.StakePoolByAddresses
            .Where(s => s.TxHash == request.PoolOutputReference.TxHash && s.TxIndex == request.PoolOutputReference.Index)
            .FirstOrDefaultAsync() ?? throw new Exception("Stake pool not found");

        // Stake details
        Address ownerAddress = new(request.OwnerAddress);
        byte[] ownerPkh = ownerAddress.GetPublicKeyHash();
        byte[]? ownerStakePkh = ownerAddress.GetStakeKeyHash();
        Signature ownerSignature = new(ownerPkh);
        CoinectaAddress destinationAddress = new(new(ownerPkh), new(new Credential(ownerStakePkh!)));
        Destination<NoDatum> destination = new(destinationAddress, new NoDatum());
        RewardSetting rewardSetting = stakePoolData.StakePool.RewardSettings[request.RewardSettingIndex];
        ulong lockTime = rewardSetting.LockDuration;
        Rational rewardMultiplier = rewardSetting.RewardMultiplier;
        string stakeMintingPolicyId = configuration["CoinectaStakeMintingPolicyId"]!;

        // Stake proxy datum
        StakePoolProxy<NoDatum> stakePoolProxyDatum = new(
            ownerSignature,
            destination,
            rewardSetting.LockDuration,
            rewardSetting.RewardMultiplier,
            stakePoolData.StakePool.PolicyId,
            stakePoolData.StakePool.AssetName,
            Convert.FromHexString(stakeMintingPolicyId));
        byte[] stakePoolProxyDatumCbor = CborConverter.Serialize(stakePoolProxyDatum);

        // Build transaction
        Address stakePoolProxyAddress = new(configuration["CoinectaStakePoolProxyAddress"]!);
        List<Utxo> utxos = CoinectaUtils.ConvertUtxoListCbor(request.WalletUtxoListCbor).ToList();

        // Validator output
        TransactionOutputValue stakePoolProxyOutputValue = new()
        {
            Coin = 2_500_000,
            MultiAsset = []
        };

        stakePoolProxyOutputValue.MultiAsset.Add(stakePoolData.StakePool.PolicyId, new()
        {
            Token = new()
            {
                { stakePoolData.StakePool.AssetName, request.Amount }
            }
        });

        TransactionOutput stakePoolProxyOutput = new()
        {
            Address = stakePoolProxyAddress.GetBytes(),
            Value = stakePoolProxyOutputValue,
            DatumOption = new()
            {
                RawData = stakePoolProxyDatumCbor
            }
        };

        // Wallet coin selection
        CoinSelection coinSelectionResult = CoinectaUtils
        .GetCoinSelection([stakePoolProxyOutput], utxos, ownerAddress.ToString())
            ?? throw new Exception("Coin selection failed");

        ITransactionBodyBuilder txBodyBuilder = TransactionBodyBuilder.Create;
        txBodyBuilder.AddOutput(stakePoolProxyOutput);
        coinSelectionResult.Inputs.ForEach(input => txBodyBuilder.AddInput(input));
        coinSelectionResult.ChangeOutputs.ForEach((change) => txBodyBuilder.AddOutput(change));

        ITransactionBuilder txBuilder = TransactionBuilder.Create;
        txBuilder.SetBody(txBodyBuilder);
        txBuilder.SetWitnesses(TransactionWitnessSetBuilder.Create);

        Transaction tx = txBuilder.Build();
        uint fee = tx.CalculateAndSetFee(numberOfVKeyWitnessesToMock: 1);
        tx.TransactionBody.TransactionOutputs.Last().Value.Coin -= fee;
        string unsignedTxCbor = Convert.ToHexString(tx.Serialize());

        return unsignedTxCbor;
    }

    public async Task<string> CancelStakeAsync(CancelStakeRequest request)
    {
        using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

        StakeRequestByAddress stakeRequestData = await dbContext.StakeRequestByAddresses
            .Where(s => s.TxHash == request.StakeRequestOutputReference.TxHash && s.TxIndex == request.StakeRequestOutputReference.Index)
            .FirstOrDefaultAsync() ?? throw new Exception("Stake request not found");

        // Wallet output
        TransactionInput stakeProxyReferenceInput = CoinectaUtils.GetStakePoolProxyScriptReferenceInput(configuration);
        KeyValuePair<string, Dictionary<string, ulong>> stakeProxyUtxoTokenValue = stakeRequestData.Amount.MultiAsset.First();
        TransactionOutputValue walletOutputValue = new()
        {
            Coin = stakeRequestData.Amount.Coin,
            MultiAsset = []
        };

        walletOutputValue.MultiAsset.Add(Convert.FromHexString(stakeProxyUtxoTokenValue.Key), new()
        {
            Token = new()
            {
                { Convert.FromHexString(stakeProxyUtxoTokenValue.Value.First().Key), (long)stakeProxyUtxoTokenValue.Value.First().Value }
            }
        });

        TransactionInput stakePoolProxyInput = new()
        {
            TransactionId = Convert.FromHexString(request.StakeRequestOutputReference.TxHash),
            TransactionIndex = (uint)request.StakeRequestOutputReference.Index,
            Output = new()
            {
                Address = new Address(stakeRequestData.Address).GetBytes(),
                Value = walletOutputValue,
                DatumOption = new()
                {
                    RawData = CborConverter.Serialize(stakeRequestData.StakePoolProxy)
                }
            }
        };

        List<Utxo> walletUtxos = CoinectaUtils.ConvertUtxoListCbor(request.WalletUtxoListCbor).ToList();

        // Wallet coin selection

        Address changeAddress = new(walletUtxos.First().OutputAddress);

        TransactionOutput collateralOutput = new()
        {
            Address = changeAddress.GetBytes(),
            Value = new()
            {
                Coin = 5_000_000,
                MultiAsset = []
            }
        };

        TransactionOutput walletOutput = new()
        {
            Address = changeAddress.GetBytes(),
            Value = walletOutputValue,
        };

        CoinSelection coinSelectionResult = CoinectaUtils
            .GetCoinSelection([collateralOutput], walletUtxos, changeAddress.ToString(), limit: 1)
                ?? throw new Exception("Coin selection failed");
        TransactionInput collateralInput = coinSelectionResult.Inputs.First();

        RedeemerBuilder? redeemerBuilder = RedeemerBuilder.Create
            .SetTag(RedeemerTag.Spend)
            .SetIndex(0)
            .SetPlutusData(CBORObject.DecodeFromBytes(CborConvertor.Serialize(new NoDatum())).GetPlutusData())
            .SetExUnits(new() { Mem = 0, Steps = 0 }) as RedeemerBuilder;

        ITransactionBodyBuilder txBodyBuilder = TransactionBodyBuilder.Create;
        txBodyBuilder.AddInput(stakePoolProxyInput);
        txBodyBuilder.AddOutput(walletOutput);
        txBodyBuilder.SetScriptDataHash(
            [redeemerBuilder!.Build()],
            [],
            CostModelUtility.PlutusV2CostModel.Serialize());
        txBodyBuilder.AddReferenceInput(stakeProxyReferenceInput);
        txBodyBuilder.AddRequiredSigner(stakeRequestData.StakePoolProxy.Owner.KeyHash);
        txBodyBuilder.AddCollateralInput(collateralInput);

        ITransactionWitnessSetBuilder txWitnesssetBuilder = TransactionWitnessSetBuilder.Create;
        txWitnesssetBuilder.AddRedeemer(redeemerBuilder!.Build());

        ITransactionBuilder txBuilder = TransactionBuilder.Create;
        txBuilder.SetBody(txBodyBuilder);
        txBuilder.SetWitnesses(txWitnesssetBuilder);

        Transaction tx = txBuilder.BuildAndSetExUnits(NetworkType.Preview);
        uint fee = tx.CalculateAndSetFee(numberOfVKeyWitnessesToMock: 1);
        tx.TransactionBody.TransactionOutputs.Last().Value.Coin -= fee;
        string unsignedTxCbor = Convert.ToHexString(tx.Serialize());

        return unsignedTxCbor;
    }

    public async Task<string> ClaimStakeAsync(ClaimStakeRequest request)
    {
        using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

        string referenceKeyPrefix = configuration["ReferenceKeyPrefix"]!;
        string stakeKeyPrefix = configuration["StakeKeyPrefix"]!;
        string stakeMintingPolicy = configuration["CoinectaStakeMintingPolicyId"]!;

        // Validator addresses
        Address stakePoolValidatorAddress = new(configuration["CoinectaStakePoolValidatorAddress"]!);
        Address timeLockValidatorAddress = new(configuration["CoinectaTimeLockValidatorAddress"]!);

        // Resolved Script Reference Inputs
        TransactionInput timeLockValidatorScriptReferenceInput = CoinectaUtils.GetTimeLockValidatorScriptReferenceInput(configuration);
        TransactionInput stakeMintingValidatorScriptReferenceInput = CoinectaUtils.GetStakeMintingValidatorScriptReferenceInput(configuration);

        // Wallet utxos
        List<Utxo> walletUtxos = CoinectaUtils.ConvertUtxoListCbor(request.WalletUtxoListCbor).ToList();
        Address walletAddress = new(walletUtxos.First().OutputAddress);

        StakePositionByStakeKey stakePosition = await dbContext.StakePositionByStakeKeys
            .Where(s => s.TxHash == request.StakeUtxoOutputReference.TxHash && s.TxIndex == request.StakeUtxoOutputReference.Index)
            .FirstOrDefaultAsync() ?? throw new Exception("No stake position found");

        // Burn Assets
        string? stakeKey = Convert.ToHexString(stakePosition.StakePosition.Extra.TimeLockKey).ToLower();
        string assetName = stakeKey!.ToString()!.Replace(stakeMintingPolicy + stakeKeyPrefix, "");
        string stakeKeyAssetName = stakeKeyPrefix + assetName;
        string referenceAssetName = referenceKeyPrefix + assetName;

        // Build transaction
        ITransactionBodyBuilder txBodyBuilder = TransactionBodyBuilder.Create;
        List<RedeemerBuilder> redeemerBuilders = [];
        ITokenBundleBuilder mintAssets = TokenBundleBuilder.Create;

        // Add Inputs, Outputs, Redeemers and MintAssets
        OutputReference stakePositionOutRef = new()
        {
            TxHash = stakePosition.TxHash,
            Index = (uint)stakePosition.TxIndex
        };

        ITokenBundleBuilder multiAssetInput = TokenBundleBuilder.Create;
        ITokenBundleBuilder multiAssetOutput = TokenBundleBuilder.Create;
        stakePosition.Amount.MultiAsset.Keys.ToList().ForEach((policyId) =>
        {
            Dictionary<string, ulong> asset = stakePosition.Amount.MultiAsset[policyId];
            asset.Keys.ToList().ForEach((assetName) =>
            {
                multiAssetInput.AddToken(Convert.FromHexString(policyId), Convert.FromHexString(assetName), (long)asset[assetName]);

                if (assetName != referenceAssetName)
                {
                    multiAssetOutput.AddToken(Convert.FromHexString(policyId), Convert.FromHexString(assetName), (long)asset[assetName]);
                }
            });
        });

        TransactionOutput stakePositionOutput = new()
        {
            Address = timeLockValidatorAddress.GetBytes(),
            Value = new()
            {
                Coin = stakePosition.Amount.Coin,
                MultiAsset = multiAssetInput.Build()
            },
            DatumOption = new()
            {
                RawData = CborConverter.Serialize(stakePosition.StakePosition)
            }
        };


        RedeemerBuilder? spendRedeemerBuilder = RedeemerBuilder.Create
            .SetTag(RedeemerTag.Spend)
            .SetIndex(0)
            .SetPlutusData(CBORObject.DecodeFromBytes(CborConvertor.Serialize(new NoDatum())).GetPlutusData())
            .SetExUnits(new ExUnits { Mem = 0, Steps = 0 }) as RedeemerBuilder;

        // Add stake position input
        txBodyBuilder.AddInput(CoinectaUtils.BuildTxInput(stakePositionOutRef, stakePositionOutput));

        // Add output to destination wallet
        TransactionOutput walletOutput = new()
        {
            Address = walletAddress.GetBytes(),
            Value = new()
            {
                Coin = stakePosition.Amount.Coin,
                MultiAsset = multiAssetOutput.Build()
            }
        };

        txBodyBuilder.AddOutput(walletOutput);

        // Add redeemer
        redeemerBuilders.Add(spendRedeemerBuilder!);

        // Burn stake token
        mintAssets.AddToken(Convert.FromHexString(stakeMintingPolicy), Convert.FromHexString(stakeKeyAssetName), -1);

        // Burn reference token
        mintAssets.AddToken(Convert.FromHexString(stakeMintingPolicy), Convert.FromHexString(referenceAssetName), -1);

        // Mint Redeemer
        RedeemerBuilder? mintRedeemerBuilder = RedeemerBuilder.Create
            .SetTag(RedeemerTag.Mint)
            .SetIndex(0)
            .SetPlutusData(CBORObject.DecodeFromBytes(CborConvertor.Serialize(new StakeKeyMintRedeemer(0, 0))).GetPlutusData())
            .SetExUnits(new ExUnits { Mem = 0, Steps = 0 }) as RedeemerBuilder;

        redeemerBuilders.Add(mintRedeemerBuilder!);
        txBodyBuilder.SetMint(mintAssets);

        txBodyBuilder.SetScriptDataHash(
            redeemerBuilders.Select(redeemerBuilder => redeemerBuilder!.Build()).ToList(),
            [],
            CostModelUtility.PlutusV2CostModel.Serialize());

        // Add Reference inputs
        txBodyBuilder.AddReferenceInput(timeLockValidatorScriptReferenceInput);
        txBodyBuilder.AddReferenceInput(stakeMintingValidatorScriptReferenceInput);

        // Set Validity Interval
        ulong currentSlot = dbContext.Blocks.OrderByDescending(b => b.Slot).First().Slot;
        uint validityFrom = (uint)SlotUtility.GetSlotFromUnixTime(SlotUtility.Preview, (long)stakePosition.StakePosition.Extra.Lockuntil / 1_000);
        uint validityTo = (uint)currentSlot + 10_000;

        txBodyBuilder.SetValidBefore(validityTo);
        txBodyBuilder.SetValidAfter(validityFrom);

        // Coin Selection for Input with Stake Key NFT
        ITokenBundleBuilder stakeKeyTokenBundle = TokenBundleBuilder.Create;
        stakeKeyTokenBundle.AddToken(Convert.FromHexString(stakeMintingPolicy), Convert.FromHexString(stakeKeyAssetName), 1);
        TransactionOutput nftOutput = new()
        {
            Address = timeLockValidatorAddress.GetBytes(),
            Value = new()
            {
                Coin = 0,
                MultiAsset = stakeKeyTokenBundle.Build()
            }
        };

        CoinSelection stakeKeyInputResult = CoinectaUtils.GetCoinSelection([nftOutput], walletUtxos, walletAddress.ToString(), limit: 1);
        Utxo stakeKeyInput = stakeKeyInputResult.SelectedUtxos.First();

        txBodyBuilder.AddInput(stakeKeyInput);
        stakeKeyInputResult.ChangeOutputs.ForEach(output => txBodyBuilder.AddOutput(output));

        // Coin Selection for Collateral
        TransactionOutput collateralOutput = new()
        {
            Address = walletAddress.GetBytes(),
            Value = new()
            {
                Coin = 5_000_000,
                MultiAsset = []
            }
        };

        walletUtxos.Remove(stakeKeyInput);

        CoinSelection collateralInputResult = CoinectaUtils.GetCoinSelection([collateralOutput], walletUtxos, walletAddress.ToString(), limit: 1);
        TransactionInput collateralInputUtxo = collateralInputResult.Inputs.First();
        txBodyBuilder.AddCollateralInput(collateralInputUtxo);

        ITransactionWitnessSetBuilder txWitnesssetBuilder = TransactionWitnessSetBuilder.Create;
        redeemerBuilders.ForEach(redeemerBuilder => txWitnesssetBuilder.AddRedeemer(redeemerBuilder!.Build()));

        ITransactionBuilder txBuilder = TransactionBuilder.Create;
        txBuilder.SetBody(txBodyBuilder);
        txBuilder.SetWitnesses(txWitnesssetBuilder);

        Transaction tx = txBuilder.BuildAndSetExUnits(NetworkType.Preview);
        uint fee = tx.CalculateAndSetFee(numberOfVKeyWitnessesToMock: 1);
        tx.TransactionBody.TransactionOutputs.Last().Value.Coin -= fee;
        string unsignedTxCbor = Convert.ToHexString(tx.Serialize());

        return unsignedTxCbor;
    }

    public string FinalizeTx(FinalizeTransactionRequest request)
    {
        Transaction tx = Convert.FromHexString(request.UnsignedTxCbor).DeserializeTransaction();

        TransactionWitnessSet witnessSet = CoinectaUtils.ConvertTxWitnessSetCbor(request.TxWitnessCbor);
        ITransactionWitnessSetBuilder witnessSetBuilder = TransactionWitnessSetBuilder.Create;
        witnessSet.VKeyWitnesses.ToList().ForEach((witness) => witnessSetBuilder.AddVKeyWitness(witness));

        if (tx.TransactionWitnessSet is null)
        {
            tx.TransactionWitnessSet = witnessSetBuilder.Build();
        }
        else
        {
            tx.TransactionWitnessSet.VKeyWitnesses = witnessSet.VKeyWitnesses;
        }

        string signedTxCbor = Convert.ToHexString(tx.Serialize());

        return signedTxCbor;
    }
}
