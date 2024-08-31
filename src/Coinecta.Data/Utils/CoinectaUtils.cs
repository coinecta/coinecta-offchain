using Cardano.Sync.Data.Models.Datums;
using CardanoSharp.Wallet;
using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Models.Derivations;
using CardanoSharp.Wallet.Models.Keys;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.Models.Transactions.TransactionWitness;
using CardanoSharp.Wallet.TransactionBuilding;
using CardanoSharp.Wallet.Utilities;
using Microsoft.Extensions.Configuration;
using Address = CardanoSharp.Wallet.Models.Addresses.Address;

namespace Coinecta.Data.Utils;

public static class CoinectaUtils
{
    public static TransactionInput GetTreasuryReferenceInput(IConfiguration configuration)
    {
        string txOutputCbor = configuration["TreasuryValidatorRefScriptTxOutCbor"] ?? throw new Exception("Treasury validator reference script tx output cbor not configured");
        string txHash = configuration["TreasuryValidatorRefScriptTxHash"] ?? throw new Exception("Treasury validator reference script tx hash not configured");
        int txIndex = configuration.GetValue("TreasuryValidatorRefScriptTxIndex", 0);
        TransactionOutput txOutput = TransactionUtils.DeserializeTxOutput(txOutputCbor);

        return new()
        {
            TransactionId = Convert.FromHexString(txHash),
            TransactionIndex = (uint)txIndex,
            Output = txOutput
        }; ;
    }

    public static (Address, PublicKey, PrivateKey) GetTreasuryIdMintingScriptWallet(IConfiguration configuration)
    {
        MnemonicService mnemonicService = new();
        Mnemonic mnemonic = mnemonicService.Restore(configuration["TreasuryIdMintingMnemonic"] ?? throw new Exception("Treasury ID minting mnemonic/seed not configured"));
        PrivateKey rootKey = mnemonic.GetRootKey();

        // Derive down to our Account Node
        IAccountNodeDerivation accountNode = rootKey.Derive()
            .Derive(PurposeType.Shelley)
            .Derive(CoinType.Ada)
            .Derive(0);

        IIndexNodeDerivation paymentNode = accountNode
            .Derive(RoleType.ExternalChain)
            .Derive(0);

        IIndexNodeDerivation stakeNode = accountNode
            .Derive(RoleType.Staking)
            .Derive(0);

        Address address = AddressUtility.GetBaseAddress(paymentNode.PublicKey, stakeNode.PublicKey, NetworkUtils.GetNetworkType(configuration));

        return (address, paymentNode.PublicKey, paymentNode.PrivateKey);
    }

    public static INativeScriptBuilder GetTreasuryIdMintingScriptBuilder(IConfiguration configuration)
    {
        (Address mintingIdWalletAddress, PublicKey _, PrivateKey _) = GetTreasuryIdMintingScriptWallet(configuration);
        INativeScriptBuilder mintingScriptBuilder = NativeScriptBuilder.Create.SetKeyHash(mintingIdWalletAddress.GetPublicKeyHash());

        return mintingScriptBuilder;
    }
}