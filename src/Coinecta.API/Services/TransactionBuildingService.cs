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
using Coinecta.API.Models;
using Coinecta.Data.Models;
using CardanoSharp.Wallet.Extensions;
using CardanoSharp.Wallet.CIPs.CIP30.Extensions.Models;
using CardanoSharp.Wallet.Common;
using System.Text;
using Blake2Fast;
using System.Numerics;

namespace Coinecta.API.Services;
public class TransactionBuildingService(IDbContextFactory<CoinectaDbContext> dbContextFactory, IConfiguration configuration)
{
    public async Task<string> AddStakeAsync(AddStakeRequest request)
    {
        using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();
        List<StakePoolByAddress> stakePools = await dbContext.StakePoolByAddresses
            .Where(s => s.Address == request.StakePool.Address)
            .OrderByDescending(s => s.Slot)
            .ToListAsync();

        StakePoolByAddress stakePoolData = stakePools
            .Where(sp => Convert.ToHexString(sp.StakePool.Owner.KeyHash).Equals(request.StakePool.OwnerPkh, StringComparison.InvariantCultureIgnoreCase))
            .Where(sp => sp.Amount.MultiAsset.ContainsKey(request.StakePool.PolicyId) && sp.Amount.MultiAsset[request.StakePool.PolicyId].ContainsKey(request.StakePool.AssetName))
            .FirstOrDefault() ?? throw new Exception("Stake pool not found");

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
            Coin = 5_000_000,
            MultiAsset = []
        };

        stakePoolProxyOutputValue.MultiAsset.Add(stakePoolData.StakePool.PolicyId, new()
        {
            Token = new()
            {
                { stakePoolData.StakePool.AssetName, (long)request.Amount }
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

        IEnumerable<string> outRefs = request.StakeUtxoOutputReferences.ToList().Select(o => o.TxHash + o.Index);
        List<StakePositionByStakeKey> stakePositions = await dbContext.StakePositionByStakeKeys
            .Where(s => outRefs.Contains(s.TxHash + s.TxIndex))
            .ToListAsync() ?? throw new Exception("No stake positions found");

        // Build transaction
        ITransactionBodyBuilder txBodyBuilder = TransactionBodyBuilder.Create;
        List<RedeemerBuilder> redeemerBuilders = [];
        ITokenBundleBuilder mintAssets = TokenBundleBuilder.Create;
        Dictionary<string, Dictionary<string, ulong>> multiAssetWalletOutput = [];
        ITokenBundleBuilder walletBurnAssets = TokenBundleBuilder.Create;

        // Total Wallet Output
        TransactionOutput walletOutput = new()
        {
            Address = walletAddress.GetBytes(),
            Value = new()
            {
                Coin = 0,
                MultiAsset = []
            }
        };

        ulong lowestLockTime = 0;
        stakePositions.ForEach(stakePosition =>
        {
            // Burn Assets
            string? stakeKey = Convert.ToHexString(stakePosition.StakePosition.Extra.TimeLockKey).ToLower();
            string assetName = stakeKey!.ToString()!.Replace(stakeMintingPolicy + stakeKeyPrefix, "");
            string stakeKeyAssetName = stakeKeyPrefix + assetName;
            string referenceAssetName = referenceKeyPrefix + assetName;

            // Add Inputs, Outputs, Redeemers and MintAssets
            Models.OutputReference stakePositionOutRef = new()
            {
                TxHash = stakePosition.TxHash,
                Index = (uint)stakePosition.TxIndex
            };

            ITokenBundleBuilder multiAssetInput = TokenBundleBuilder.Create;
            stakePosition.Amount.MultiAsset.Keys.ToList().ForEach((policyId) =>
            {
                Dictionary<string, ulong> asset = stakePosition.Amount.MultiAsset[policyId];
                asset.Keys.ToList().ForEach((assetName) =>
                {
                    byte[] policyIdBytes = Convert.FromHexString(policyId);
                    byte[] assetNameBytes = Convert.FromHexString(assetName);

                    multiAssetInput.AddToken(policyIdBytes, assetNameBytes, (long)asset[assetName]);

                    if (assetName != referenceAssetName)
                    {
                        bool exists = multiAssetWalletOutput.ContainsKey(policyId);

                        if (exists)
                        {
                            bool assetExists = multiAssetWalletOutput[policyId].ContainsKey(assetName);

                            if (assetExists)
                            {
                                multiAssetWalletOutput[policyId][assetName] += asset[assetName];
                            }
                            else
                            {
                                multiAssetWalletOutput[policyId].Add(assetName, asset[assetName]);
                            }
                        }
                        else
                        {
                            multiAssetWalletOutput.Add(policyId, new() { { assetName, asset[assetName] } });
                        }
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

            // Add stake position input
            txBodyBuilder.AddInput(CoinectaUtils.BuildTxInput(stakePositionOutRef, stakePositionOutput));

            // Burn stake token
            mintAssets.AddToken(Convert.FromHexString(stakeMintingPolicy), Convert.FromHexString(stakeKeyAssetName), -1);
            walletBurnAssets.AddToken(Convert.FromHexString(stakeMintingPolicy), Convert.FromHexString(stakeKeyAssetName), 1);

            // Burn reference token
            mintAssets.AddToken(Convert.FromHexString(stakeMintingPolicy), Convert.FromHexString(referenceAssetName), -1);

            lowestLockTime = Math.Max(lowestLockTime, stakePosition.StakePosition.Extra.Lockuntil);

            walletOutput.Value.Coin += stakePosition.Amount.Coin;
        });

        // Mint Redeemer
        RedeemerBuilder? mintRedeemerBuilder = RedeemerBuilder.Create
            .SetTag(RedeemerTag.Mint)
            .SetIndex(0)
            .SetPlutusData(CBORObject.DecodeFromBytes(CborConvertor.Serialize(new StakeKeyMintRedeemer(0, 0))).GetPlutusData())
            .SetExUnits(new ExUnits { Mem = 0, Steps = 0 }) as RedeemerBuilder;

        redeemerBuilders.Add(mintRedeemerBuilder!);

        ITokenBundleBuilder multiAssetOutputBuilder = TokenBundleBuilder.Create;

        multiAssetWalletOutput.Keys.ToList().ForEach((policyId) =>
        {
            Dictionary<string, ulong> asset = multiAssetWalletOutput[policyId];
            asset.Keys.ToList().ForEach((assetName) =>
            {
                byte[] policyIdBytes = Convert.FromHexString(policyId);
                byte[] assetNameBytes = Convert.FromHexString(assetName);
                multiAssetOutputBuilder.AddToken(policyIdBytes, assetNameBytes, (long)asset[assetName]);
            });
        });

        walletOutput.Value.MultiAsset = multiAssetOutputBuilder.Build();
        txBodyBuilder.AddOutput(walletOutput);
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
        uint validityFrom = (uint)SlotUtility.GetSlotFromUnixTime(SlotUtility.Preview, (long)lowestLockTime / 1_000);
        uint validityTo = (uint)currentSlot + 10_000;

        txBodyBuilder.SetValidBefore(validityTo);
        txBodyBuilder.SetValidAfter(validityFrom);

        // Coin Selection for Input with Stake Key NFT
        TransactionOutput nftOutput = new()
        {
            Address = walletAddress.GetBytes(),
            Value = new()
            {
                Coin = 0,
                MultiAsset = walletBurnAssets.Build()
            }
        };

        CoinSelection stakeKeyInputsResult = CoinectaUtils.GetCoinSelection([nftOutput], walletUtxos, walletAddress.ToString());

        stakeKeyInputsResult.SelectedUtxos.ForEach(input => txBodyBuilder.AddInput(input));
        stakeKeyInputsResult.ChangeOutputs.ForEach(output => txBodyBuilder.AddOutput(output));
        stakeKeyInputsResult.SelectedUtxos.ForEach(utxo => walletUtxos.Remove(item: utxo));

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

        CoinSelection collateralInputResult = CoinectaUtils.GetCoinSelection([collateralOutput], walletUtxos, walletAddress.ToString(), limit: 1);
        TransactionInput collateralInputUtxo = collateralInputResult.Inputs.First();
        txBodyBuilder.AddCollateralInput(collateralInputUtxo);

        List<TransactionInput> txInputs = [.. txBodyBuilder.Build().TransactionInputs];
        txInputs.Sort((a, b) =>
        {
            string aTxId = Convert.ToHexString(a.TransactionId) + a.TransactionIndex;
            string bTxId = Convert.ToHexString(b.TransactionId) + b.TransactionIndex;
            return aTxId.CompareTo(bTxId);
        });
        List<string> txInputOutrefs = txInputs.Select(i => Convert.ToHexString(i.TransactionId) + i.TransactionIndex).ToList();
        string timeLockValidatorScriptHash = configuration["CoinectaTimeLockValidatorScriptHash"]!;
        // Build Redeemers
        txInputs.ForEach(input =>
        {
            int index = txInputs.IndexOf(input);

            Address inputAddress = new(input.Output!.Address);
            string inputAddressHash = Convert.ToHexString(inputAddress.GetPublicKeyHash()).ToLower();
            if (inputAddressHash == timeLockValidatorScriptHash)
            {
                RedeemerBuilder? redeemerBuilder = RedeemerBuilder.Create
                    .SetTag(RedeemerTag.Spend)
                    .SetIndex((uint)index)
                    .SetPlutusData(CBORObject.DecodeFromBytes(CborConvertor.Serialize(new NoDatum())).GetPlutusData())
                    .SetExUnits(new ExUnits { Mem = 0, Steps = 0 }) as RedeemerBuilder;

                redeemerBuilders.Add(redeemerBuilder!);
            }
        });

        ITransactionWitnessSetBuilder txWitnesssetBuilder = TransactionWitnessSetBuilder.Create;
        redeemerBuilders.ForEach(redeemerBuilder => txWitnesssetBuilder.AddRedeemer(redeemerBuilder!.Build()));

        ITransactionBuilder txBuilder = TransactionBuilder.Create;
        txBuilder.SetBody(txBodyBuilder);
        txBuilder.SetWitnesses(txWitnesssetBuilder);


        Transaction tx = txBuilder.BuildAndSetExUnits(NetworkType.Preview);
        //Transaction tx = txBuilder.Build();
        uint fee = tx.CalculateAndSetFee(numberOfVKeyWitnessesToMock: 1);
        tx.TransactionBody.TransactionOutputs.Last().Value.Coin -= fee;
        string unsignedTxCbor = Convert.ToHexString(tx.Serialize());

        return unsignedTxCbor;
    }

    public async Task<string> ExecuteStakeAsync(ExecuteStakeRequest request)
    {
        using CoinectaDbContext dbContext = dbContextFactory.CreateDbContext();

        StakeRequestByAddress stakeRequestData = await dbContext.StakeRequestByAddresses
            .Where(s => s.TxHash == request.StakeRequestOutputReference.TxHash && s.TxIndex == request.StakeRequestOutputReference.Index)
            .FirstOrDefaultAsync() ?? throw new Exception("Stake request not found");

        StakePoolByAddress? stakePoolData = null;

        if (request.StakePoolData is not null)
        {
            stakePoolData = request.StakePoolData;
        }
        else
        {
            List<StakePoolByAddress> stakePools = await dbContext.StakePoolByAddresses
                .Where(s => s.Address == request.StakePool.Address)
                .OrderByDescending(s => s.Slot)
                .ToListAsync();

            stakePoolData = stakePools
                .Where(sp => Convert.ToHexString(sp.StakePool.Owner.KeyHash).Equals(request.StakePool.OwnerPkh, StringComparison.InvariantCultureIgnoreCase))
                .Where(sp => sp.Amount.MultiAsset.ContainsKey(request.StakePool.PolicyId) && sp.Amount.MultiAsset[request.StakePool.PolicyId].ContainsKey(request.StakePool.AssetName))
                .FirstOrDefault() ?? throw new Exception("Stake pool not found");
        }

        // Reference Scripts
        TransactionInput mintingRefInput = CoinectaUtils.GetStakeMintingValidatorScriptReferenceInput(configuration);
        TransactionInput proxyRefInput = CoinectaUtils.GetStakePoolProxyScriptReferenceInput(configuration);
        TransactionInput validatorRefInput = CoinectaUtils.GetStakePoolValidatorScriptReferenceInput(configuration);
        TransactionInput timelockRefInput = CoinectaUtils.GetTimeLockValidatorScriptReferenceInput(configuration);

        // Asset
        byte[] stakePoolPolicyIdBytes = stakePoolData.StakePool.PolicyId;
        byte[] stakePoolAssetNameBytes = stakePoolData.StakePool.AssetName;
        string stakePoolPolicyId = Convert.ToHexString(stakePoolPolicyIdBytes).ToLower();
        string stakePoolAssetName = Convert.ToHexString(stakePoolAssetNameBytes).ToLower();

        // Reward
        ulong stakeRequestAmount = stakeRequestData.Amount.MultiAsset[stakePoolPolicyId][stakePoolAssetName];
        ulong stakePoolBalance = stakePoolData.Amount.MultiAsset[stakePoolPolicyId][stakePoolAssetName];
        Rational stakeRequestRewardMultiplier = stakeRequestData.StakePoolProxy.RewardMultiplier;
        Rational stakeRequestAmountRational = new(stakeRequestAmount, 1);
        Rational stakeRequestTotalRewardRational = stakeRequestAmountRational * stakeRequestRewardMultiplier;
        ulong stakeRequestTotalReward = stakeRequestTotalRewardRational.Numerator / stakeRequestTotalRewardRational.Denominator;
        ulong rewardTotal = stakeRequestAmount + stakeRequestTotalReward;
        BigInteger stakeRewardTotalRewardWithDecimals = BigInteger.Divide(rewardTotal, BigInteger.Pow(10, (int)stakePoolData.StakePool.Decimals));
        ulong stakePoolRemainingBalance = stakePoolBalance - stakeRequestTotalReward;
        int rewardIndex = stakePoolData.StakePool.RewardSettings.ToList().Select(r => r.RewardMultiplier).ToList().IndexOf(stakeRequestRewardMultiplier);

        // Addresses
        byte[] destinationPkh = stakeRequestData.StakePoolProxy.Destination.Address.Credential.Hash;
        byte[]? destinationStakePkh = stakeRequestData.StakePoolProxy.Destination.Address.StakeCredential?.Credential.Hash;
        Address destinationAddress = AddressUtility.GetBaseAddress(destinationPkh, destinationStakePkh!, NetworkType.Preview);

        Address timeLockValidatorAddress = CoinectaUtils.ValidatorAddress(timelockRefInput.Output!.ScriptReference!.PlutusV2Script!);
        Address stakePoolValidatorAddress = CoinectaUtils.ValidatorAddress(validatorRefInput.Output!.ScriptReference!.PlutusV2Script!);
        Address stakeProxyAddress = CoinectaUtils.ValidatorAddress(proxyRefInput.Output!.ScriptReference!.PlutusV2Script!);

        // Time
        ulong currentSlot = (await dbContext.Blocks.OrderByDescending(b => b.Slot).FirstAsync()).Slot - 100;
        ulong currentTimeSeconds = (ulong)SlotUtility.GetPosixTimeSecondsFromSlot(SlotUtility.Preview, (long)currentSlot);
        ulong currentTimeMs = currentTimeSeconds * 1000;
        ulong validTimeInMs = currentTimeMs + (1000 * 60 * 7);
        ulong lockTime = validTimeInMs + stakeRequestData.StakePoolProxy.LockTime;
        ulong validTimeSlot = (ulong)SlotUtility.GetSlotFromUnixTime(SlotUtility.Preview, (long)validTimeInMs / 1000);
        ulong bufferDiff = validTimeInMs - currentTimeMs;

        // Metadata
        string abbreviatedReward = CoinectaUtils.AbbreviateAmount(stakeRewardTotalRewardWithDecimals, 0);
        string assetNameUtf8 = Encoding.UTF8.GetString(stakePoolAssetNameBytes);
        string lockTimeDateString = CoinectaUtils.TimeToDateString((long)lockTime);
        string metadataName = $"Stake NFT {abbreviatedReward} {assetNameUtf8} - {lockTimeDateString}";
        var outputRefCbor = CborConverter.Serialize(new Data.Models.Datums.OutputReference(Convert.FromHexString(stakePoolData.TxHash), stakePoolData.TxIndex));
        var outputRefCborHex = Convert.ToHexString(outputRefCbor).ToLower();
        string stakeNftAssetName = Convert.ToHexString(HashUtility.Blake2b256(Convert.FromHexString(outputRefCborHex)))[..56].ToLower();

        // Timelock Metadata
        TimelockMetadata metadata = new(stakeRequestTotalReward + stakeRequestAmount, Convert.ToHexString(Encoding.UTF8.GetBytes(metadataName)).ToLower());
        string stakeMintingPolicy = configuration["CoinectaStakeMintingPolicyId"]!;
        string stakeKeyPrefix = configuration["StakeKeyPrefix"]!;
        string referenceKeyPrefix = configuration["ReferenceKeyPrefix"]!;
        string stakeKeyUnit = stakeMintingPolicy + stakeKeyPrefix + stakeNftAssetName;

        // Timelock
        Timelock timelock = new(lockTime, Convert.FromHexString(stakeKeyUnit));

        // Build transaction
        ITransactionBodyBuilder txBodyBuilder = TransactionBodyBuilder.Create;
        ITransactionBuilder txBuilder = TransactionBuilder.Create;

        ulong attachedCoin = stakeRequestData.Amount.Coin;

        // Validity Interval
        txBodyBuilder.SetValidBefore((uint)validTimeSlot);
        txBodyBuilder.SetValidAfter((uint)currentSlot);

        // Reference Inputs
        txBodyBuilder.AddReferenceInput(mintingRefInput);
        txBodyBuilder.AddReferenceInput(proxyRefInput);
        txBodyBuilder.AddReferenceInput(validatorRefInput);

        // Datums
        byte[] timeLockDatum = CborConverter.Serialize(new ReferenceNft(metadata, 1, timelock));

        // Resolved Stake Pool Input
        var stakePoolTokenBundle = CoinectaUtils.GetTokenBundleFromAmount(stakePoolData.Amount.MultiAsset);
        var stakePoolOutRef = new Models.OutputReference()
        {
            TxHash = stakePoolData.TxHash,
            Index = (uint)stakePoolData.TxIndex
        };
        var stakePoolOutput = new TransactionOutput()
        {
            Address = stakePoolValidatorAddress.GetBytes(),
            Value = new TransactionOutputValue()
            {
                Coin = stakePoolData.Amount.Coin,
                MultiAsset = stakePoolTokenBundle.Build()
            },
            DatumOption = new DatumOption()
            {
                RawData = CborConverter.Serialize(stakePoolData.StakePool)
            }
        };
        var resolvedStakePoolInput = CoinectaUtils.BuildTxInput(stakePoolOutRef, stakePoolOutput);

        txBodyBuilder.AddInput(resolvedStakePoolInput);

        // Resolved Stake Proxy Input 
        var stakeProxyTokenBundle = CoinectaUtils.GetTokenBundleFromAmount(stakeRequestData.Amount.MultiAsset);
        var stakeProxyOutRef = new Models.OutputReference()
        {
            TxHash = stakeRequestData.TxHash,
            Index = (uint)stakeRequestData.TxIndex
        };
        var stakeProxyOutput = new TransactionOutput()
        {
            Address = stakeProxyAddress.GetBytes(),
            Value = new TransactionOutputValue()
            {
                Coin = stakeRequestData.Amount.Coin,
                MultiAsset = stakeProxyTokenBundle.Build()
            },
            DatumOption = new DatumOption()
            {
                RawData = CborConverter.Serialize(stakeRequestData.StakePoolProxy)
            }
        };
        var resolvedStakeProxyInput = CoinectaUtils.BuildTxInput(stakeProxyOutRef, stakeProxyOutput);

        txBodyBuilder.AddInput(resolvedStakeProxyInput);

        // Mint Assets
        ITokenBundleBuilder mintAssets = TokenBundleBuilder.Create;
        mintAssets.AddToken(Convert.FromHexString(stakeMintingPolicy), Convert.FromHexString(stakeKeyPrefix + stakeNftAssetName), 1);
        mintAssets.AddToken(Convert.FromHexString(stakeMintingPolicy), Convert.FromHexString(referenceKeyPrefix + stakeNftAssetName), 1);

        txBodyBuilder.SetMint(mintAssets);

        // Stake Pool Output
        ITokenBundleBuilder stakePoolRemainingTokens = TokenBundleBuilder.Create;
        stakePoolRemainingTokens.AddToken(stakePoolPolicyIdBytes, stakePoolAssetNameBytes, (long)stakePoolRemainingBalance);
        txBodyBuilder.AddOutput(new TransactionOutput()
        {
            Address = stakePoolValidatorAddress.GetBytes(),
            Value = new TransactionOutputValue()
            {
                Coin = stakePoolData.Amount.Coin,
                MultiAsset = stakePoolRemainingTokens.Build()
            },
            DatumOption = new()
            {
                RawData = CborConverter.Serialize(stakePoolData.StakePool)
            }
        });

        // Timelock Output
        ITokenBundleBuilder timelockTokenBundle = TokenBundleBuilder.Create;
        timelockTokenBundle.AddToken(stakePoolPolicyIdBytes, stakePoolAssetNameBytes, (long)stakeRequestTotalReward + (long)stakeRequestAmount);
        timelockTokenBundle.AddToken(Convert.FromHexString(stakeMintingPolicy), Convert.FromHexString(referenceKeyPrefix + stakeNftAssetName), 1);
        txBodyBuilder.AddOutput(new TransactionOutput()
        {
            Address = timeLockValidatorAddress.GetBytes(),
            Value = new TransactionOutputValue()
            {
                Coin = 2_200_000,
                MultiAsset = timelockTokenBundle.Build()
            },
            DatumOption = new()
            {
                RawData = timeLockDatum
            }
        });

        attachedCoin -= 2_200_000;

        // Destination Output
        ITokenBundleBuilder walletTokenBundle = TokenBundleBuilder.Create;
        walletTokenBundle.AddToken(Convert.FromHexString(stakeMintingPolicy), Convert.FromHexString(stakeKeyPrefix + stakeNftAssetName), 1);
        txBodyBuilder.AddOutput(new()
        {
            Address = destinationAddress.GetBytes(),
            Value = new()
            {
                Coin = 1_500_000,
                MultiAsset = walletTokenBundle.Build()
            }
        });

        attachedCoin -= 1_500_000;

        // Batcher Output 
        var walletUtxos = CoinectaUtils.ConvertUtxoListCbor(request.WalletUtxoListCbor).ToList();
        Address changeAddress = new(walletUtxos.First().OutputAddress);
        var batchingCertificatePolicyId = configuration["CoinectaBatchingCertificatePolicyId"]!;
        var batchingCertificateAssetName = configuration["CoinectaBatchingCertificateAssetName"]!;

        ITokenBundleBuilder batcherTokenBundle = TokenBundleBuilder.Create;
        batcherTokenBundle.AddToken(Convert.FromHexString(batchingCertificatePolicyId), Convert.FromHexString(batchingCertificateAssetName), 1);

        var batcherCertificateOutput = new TransactionOutput()
        {
            Address = changeAddress.GetBytes(),
            Value = new TransactionOutputValue()
            {
                MultiAsset = batcherTokenBundle.Build()
            }
        };
        var batcherCoinSelectionResult = CoinectaUtils.GetCoinSelection([batcherCertificateOutput], walletUtxos, changeAddress.ToString(), limit: 1);
        var batcherInput = batcherCoinSelectionResult.Inputs.First();
        txBodyBuilder.AddInput(batcherInput);

        if (attachedCoin <= 500_000)
        {
            throw new Exception("Stake request did not have enough funds to cover the transaction fees.");
        }

        batcherInput.Output!.Value.Coin += attachedCoin;
        txBodyBuilder.AddOutput(batcherInput.Output!);

        // Collateral Input
        walletUtxos.Remove(batcherCoinSelectionResult.SelectedUtxos.First());
        var collateralOutput = new TransactionOutput()
        {
            Address = changeAddress.GetBytes(),
            Value = new TransactionOutputValue()
            {
                Coin = 5_000_000,
                MultiAsset = []
            }
        };
        var batcherCollateralCoinSelectionResult = CoinectaUtils.GetCoinSelection([collateralOutput], walletUtxos, changeAddress.ToString(), limit: 3);
        var collateralInputs = batcherCollateralCoinSelectionResult.Inputs;
        collateralInputs.ForEach(input => txBodyBuilder.AddCollateralInput(input));

        // Add Redeemer Indices
        List<TransactionInput> txInputs = [.. txBodyBuilder.Build().TransactionInputs];
        List<string> txInputOutrefs = txInputs.Select(i => (Convert.ToHexString(i.TransactionId) + i.TransactionIndex).ToLower()).ToList();
        txInputOutrefs.Sort();
        var stakeProxyIndex = txInputOutrefs.IndexOf(stakeProxyOutRef.TxHash + stakeProxyOutRef.Index);
        var stakePoolIndex = txInputOutrefs.IndexOf(stakePoolOutRef.TxHash + stakePoolOutRef.Index);

        // Redeemers
        RedeemerBuilder? stakeProxyRedeemer = RedeemerBuilder.Create
            .SetTag(RedeemerTag.Spend)
            .SetIndex((uint)stakeProxyIndex)
            .SetPlutusData(CBORObject.DecodeFromBytes(CborConvertor.Serialize(new NoDatum())).GetPlutusData())
            .SetExUnits(new ExUnits { Mem = 730096, Steps = 268541674 }) as RedeemerBuilder;

        RedeemerBuilder? stakePoolRedeemer = RedeemerBuilder.Create
            .SetTag(RedeemerTag.Spend)
            .SetIndex((uint)stakePoolIndex)
            .SetPlutusData(CBORObject.DecodeFromBytes(CborConvertor.Serialize(new StakePoolRedeemer((ulong)rewardIndex))).GetPlutusData())
            .SetExUnits(new ExUnits { Mem = 730096, Steps = 268541674 }) as RedeemerBuilder;

        RedeemerBuilder? mintRedeemer = RedeemerBuilder.Create
            .SetTag(RedeemerTag.Mint)
            .SetIndex(0)
            .SetPlutusData(CBORObject.DecodeFromBytes(CborConvertor.Serialize(new StakeKeyMintRedeemer((ulong)stakePoolIndex, 1))).GetPlutusData())
            .SetExUnits(new ExUnits { Mem = 730096, Steps = 268541674 }) as RedeemerBuilder;

        txBodyBuilder.SetScriptDataHash(
            [stakePoolRedeemer!.Build(), stakeProxyRedeemer!.Build(), mintRedeemer!.Build()],
            [],
            CostModelUtility.PlutusV2CostModel.Serialize());

        // Witness Set
        ITransactionWitnessSetBuilder txWitnesssetBuilder = TransactionWitnessSetBuilder.Create;
        txWitnesssetBuilder.AddRedeemer(stakeProxyRedeemer!.Build());
        txWitnesssetBuilder.AddRedeemer(stakePoolRedeemer!.Build());
        txWitnesssetBuilder.AddRedeemer(mintRedeemer!.Build());

        Transaction tx = txBuilder
            .SetBody(txBodyBuilder)
            .SetWitnesses(txWitnesssetBuilder)
            //.Build();
            .BuildAndSetExUnits(NetworkType.Preview);

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
