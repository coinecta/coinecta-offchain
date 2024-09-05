using Chrysalis.Cardano.Models.Core;
using CSyncTransactionOutput = Cardano.Sync.Data.Models.TransactionOutput;
using CWalletAddress = CardanoSharp.Wallet.Models.Addresses.Address;
using CSyncDatum = Cardano.Sync.Data.Models.Datum;
using CSyncDatumType = Cardano.Sync.Data.Models.DatumType;

namespace Coinecta.Data.Extensions.Chrysalis;

public static class TransactionOutputExtension
{
    public static LovelaceWithMultiAsset GetAmount(this TransactionOutput transactionOutput) => (transactionOutput switch
    {
        BabbageTransactionOutput babbage => babbage.Amount as LovelaceWithMultiAsset,
        AlonzoTransactionOutput alonzo => alonzo.Amount as LovelaceWithMultiAsset,
        MaryTransactionOutput mary => mary.Amount as LovelaceWithMultiAsset,
        ShellyTransactionOutput shelly => new LovelaceWithMultiAsset(shelly.Amount, new MultiAsset([])),
        _ => throw new NotSupportedException("Unsupported transaction output type")
    })!;

    public static CSyncTransactionOutput ToCardanoSync(this TransactionOutput transactionOutput) => (transactionOutput switch
    {
        BabbageTransactionOutput babbage => new CSyncTransactionOutput()
        {
            Address = new CWalletAddress(babbage.Address.Value).ToString(),
            Amount = new()
            {
                Coin = babbage.GetAmount().Lovelace.Value,
                MultiAsset = babbage.GetAmount().MultiAsset.ToDictionary()
            },
            Datum = babbage.Datum switch
            {
                InlineDatumOption inline => new CSyncDatum(CSyncDatumType.InlineDatum, inline.Data.Value),
                DatumHashOption hash => new CSyncDatum(CSyncDatumType.DatumHash, hash.DatumHash.Value),
                _ => null
            }
        },
        AlonzoTransactionOutput alonzo => new CSyncTransactionOutput()
        {
            Address = new CWalletAddress(alonzo.Address.Value).ToString(),
            Amount = new()
            {
                Coin = alonzo.GetAmount().Lovelace.Value,
                MultiAsset = alonzo.GetAmount().MultiAsset.ToDictionary()
            },
            Datum = new CSyncDatum(CSyncDatumType.DatumHash, alonzo.DatumHash.Value),
        },
        MaryTransactionOutput mary => new CSyncTransactionOutput()
        {
            Address = new CWalletAddress(mary.Address.Value).ToString(),
            Amount = new()
            {
                Coin = mary.GetAmount().Lovelace.Value,
                MultiAsset = mary.GetAmount().MultiAsset.ToDictionary()
            }
        },
        ShellyTransactionOutput shelly => new CSyncTransactionOutput()
        {
            Address = new CWalletAddress(shelly.Address.Value).ToString(),
            Amount = new()
            {
                Coin = shelly.GetAmount().Lovelace.Value,
                MultiAsset = shelly.GetAmount().MultiAsset.ToDictionary()
            }
        },
        _ => throw new NotImplementedException()
    })!;

}