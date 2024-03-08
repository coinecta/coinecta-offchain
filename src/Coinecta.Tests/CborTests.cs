using Cardano.Sync.Data.Models.Datums;
using CborSerialization;
using Coinecta.Data.Models.Datums;
using Dahomey.Cbor;
using Dahomey.Cbor.ObjectModel;
using PeterO.Cbor;
using PeterO.Cbor2;
using StakeCredential = Cardano.Sync.Data.Models.Datums.StakeCredential;

namespace Coinecta.Tests;

public class CborTests
{
    [Fact]
    public void SignatureCborTest()
    {
        var signature = CborConverter.Deserialize<Signature>(Convert.FromHexString("d8799f581c0c61f135f652bc17994a5411d0a256de478ea24dbc19759d2ba14f03ff"));
        var signatureCborHex = Convert.ToHexString(CborConverter.Serialize(signature)).ToLowerInvariant();
        Assert.Equal("d8799f581c0c61f135f652bc17994a5411d0a256de478ea24dbc19759d2ba14f03ff", signatureCborHex);
    }

    [Fact]
    public void RationalCborTest()
    {
        var rational = CborConverter.Deserialize<Rational>(Convert.FromHexString("d8799f051864ff"));
        var rationalCborHex = Convert.ToHexString(CborConverter.Serialize(rational)).ToLowerInvariant();
        Assert.Equal("d8799f051864ff", rationalCborHex);
    }

    [Fact]
    public void RewardSettingCborTest()
    {
        var rewardSetting = CborConverter.Deserialize<Cardano.Sync.Data.Models.Datums.RewardSetting>(Convert.FromHexString("d8799f1864d8799f051864ffff"));
        var rewardSettingCborHex = Convert.ToHexString(CborConverter.Serialize(rewardSetting)).ToLowerInvariant();
        Assert.Equal("d8799f1864d8799f051864ffff", rewardSettingCborHex);
    }

    [Fact]
    public void StakePoolCborTest()
    {
        var stakePool = CborConverter.Deserialize<StakePool>(Convert.FromHexString("d8799f9fd8799f1a000493e0d8799f051864ffffff581c8b05e87a51c1d4a0fa888d2bb14dbc25e8c343ea379a171b63aa84a044434e4354d8799f581c0c61f135f652bc17994a5411d0a256de478ea24dbc19759d2ba14f03ff00ff"));
        var stakePoolHex = Convert.ToHexString(CborConverter.Serialize(stakePool)).ToLowerInvariant();
        Assert.Equal("d8799f9fd8799f1a000493e0d8799f051864ffffff581c8b05e87a51c1d4a0fa888d2bb14dbc25e8c343ea379a171b63aa84a044434e4354d8799f581c0c61f135f652bc17994a5411d0a256de478ea24dbc19759d2ba14f03ff00ff", stakePoolHex);
    }

    [Fact]
    public void RationalMathTest()
    {
        var a = new Rational(1, 2);
        var b = new Rational(1, 2);
        var c = a + b;
        var d = a * b;

        Assert.Equal(2ul, c.Numerator);
        Assert.Equal(2ul, c.Denominator);
        Assert.Equal(1ul, d.Numerator);
        Assert.Equal(4ul, d.Denominator);
    }

    [Fact]
    public void CredentialCborTest()
    {
        var credential = CborConverter.Deserialize<Credential>(Convert.FromHexString("d8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ff"));
        var credentialCborHex = Convert.ToHexString(CborConverter.Serialize(credential)).ToLowerInvariant();
        Assert.Equal("d8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ff", credentialCborHex);
    }

    [Fact]
    public void StakeCredentialCborTest()
    {
        var stakeCredential = CborConverter.Deserialize<StakeCredential>(Convert.FromHexString("d8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffff"));
        var stakeCredentialCborHex = Convert.ToHexString(CborConverter.Serialize(stakeCredential)).ToLowerInvariant();
        Assert.Equal("d8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffff", stakeCredentialCborHex);
    }

    [Fact]
    public void AddressWithStakeCredentialCborTest()
    {
        var address = CborConverter.Deserialize<Address>(Convert.FromHexString("d8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffffffff"));
        var addressCborHex = Convert.ToHexString(CborConverter.Serialize(address)).ToLowerInvariant();
        Assert.Equal("d8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffffffff", addressCborHex);
    }

    [Fact]
    public void AddressWithoutStakeCredentialCborTest()
    {
        var address = CborConverter.Deserialize<Address>(Convert.FromHexString("d8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd87a80ff"));
        var addressCborHex = Convert.ToHexString(CborConverter.Serialize(address)).ToLowerInvariant();
        Assert.Equal("d8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd87a80ff", addressCborHex);
    }

    [Fact]
    public void OutputDatumCborTest()
    {
        var inlineDatum = CborConverter.Deserialize<InlineDatum<Credential>>(
            Convert.FromHexString("d87b9fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffff")
        );

        var inlineDatumCborHex = Convert.ToHexString(CborConverter.Serialize(inlineDatum)).ToLowerInvariant();
        Assert.Equal("d87b9fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffff", inlineDatumCborHex);

        var inlineDatumWithAddress = CborConverter.Deserialize<InlineDatum<Address>>(
            Convert.FromHexString("d87b9fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffffffffff")
        );

        var inlineDatumWithAddressCborHex = Convert.ToHexString(CborConverter.Serialize(inlineDatumWithAddress)).ToLowerInvariant();
        Assert.Equal("d87b9fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffffffffff", inlineDatumWithAddressCborHex);
    }

    [Fact]
    public void DestinationCborTest()
    {
        var destination = CborConverter.Deserialize<Destination<InlineDatum<Credential>>>(
            Convert.FromHexString(
                "d8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffffffffd87b9fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffffff"
            )
        );

        var destinationCborHex = Convert.ToHexString(CborConverter.Serialize(destination)).ToLowerInvariant();
        Assert.Equal("d8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffffffffd87b9fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffffff", destinationCborHex);
    }

    [Fact]
    public void StakePoolProxyCborTest()
    {
        var stakePoolProxy = CborConverter.Deserialize<StakePoolProxy<NoDatum>>(
            Convert.FromHexString(
                "d8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffffffffd87980ff1903e8d8799f011864ff581c8b05e87a51c1d4a0fa888d2bb14dbc25e8c343ea379a171b63aa84a044434e4354581c5496b3318f8ca933bbfdf19b8faa7f948d044208e0278d62c24ee73eff"
            )
        );

        var stakePoolProxyCborHex = Convert.ToHexString(CborConverter.Serialize(stakePoolProxy)).ToLowerInvariant();
        Assert.Equal("d8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffffffffd87980ff1903e8d8799f011864ff581c8b05e87a51c1d4a0fa888d2bb14dbc25e8c343ea379a171b63aa84a044434e4354581c5496b3318f8ca933bbfdf19b8faa7f948d044208e0278d62c24ee73eff", stakePoolProxyCborHex);

        var stakePoolProxyWithInlineDatumCredential = CborConverter.Deserialize<StakePoolProxy<InlineDatum<Credential>>>(
            Convert.FromHexString(
                "d8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffffffffd87b9fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffffff1903e8d8799f011864ff581c8b05e87a51c1d4a0fa888d2bb14dbc25e8c343ea379a171b63aa84a044434e4354581c5496b3318f8ca933bbfdf19b8faa7f948d044208e0278d62c24ee73eff"
            )
        );

        var stakePoolProxyWithInlineDatumCredentialCborHex = Convert.ToHexString(CborConverter.Serialize(stakePoolProxyWithInlineDatumCredential)).ToLowerInvariant();
        Assert.Equal("d8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffffffffd87b9fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffffff1903e8d8799f011864ff581c8b05e87a51c1d4a0fa888d2bb14dbc25e8c343ea379a171b63aa84a044434e4354581c5496b3318f8ca933bbfdf19b8faa7f948d044208e0278d62c24ee73eff", stakePoolProxyWithInlineDatumCredentialCborHex);
    }

    [Fact]
    public void TimelockCborTest()
    {
        var timelock = CborConverter.Deserialize<Timelock>(
            Convert.FromHexString(
                "d8799f1903e858206c00ac8ecdbfad86c9287b2aec257f2e3875b572de8d8df27fd94dd650671c94ff"
            )
        );

        var timelockCborHex = Convert.ToHexString(CborConverter.Serialize(timelock)).ToLowerInvariant();
        Assert.Equal("d8799f1903e858206c00ac8ecdbfad86c9287b2aec257f2e3875b572de8d8df27fd94dd650671c94ff", timelockCborHex);
    }

    [Fact]
    public void CIP68MetdataCborTest()
    {
        var timelockMetadata = CborConverter.Deserialize<CIP68Metdata>(
            Convert.FromHexString(
                "a24d6c6f636b65645f616d6f756e744431303030446e616d65581a5374616b65204e465420314b20434e4354202d20323430313233"
            )
        );

        var timelockMetadataCborHex = Convert.ToHexString(CborConverter.Serialize(timelockMetadata)).ToLowerInvariant();
        Assert.Equal("a24d6c6f636b65645f616d6f756e744431303030446e616d65581a5374616b65204e465420314b20434e4354202d20323430313233", timelockMetadataCborHex);
    }

    [Fact]
    public void CIP68TimelockCborTest()
    {
        var timelock = CborConverter.Deserialize<CIP68<Timelock>>(
            Convert.FromHexString(
                "d8799fa24d6c6f636b65645f616d6f756e744431303030446e616d65581a5374616b65204e465420314b20434e4354202d2032343031323301d8799f1903e858206c00ac8ecdbfad86c9287b2aec257f2e3875b572de8d8df27fd94dd650671c94ffff"
            )
        );

        var timelockCborHex = Convert.ToHexString(CborConverter.Serialize(timelock)).ToLowerInvariant();
        Assert.Equal("d8799fa24d6c6f636b65645f616d6f756e744431303030446e616d65581a5374616b65204e465420314b20434e4354202d2032343031323301d8799f1903e858206c00ac8ecdbfad86c9287b2aec257f2e3875b572de8d8df27fd94dd650671c94ffff", timelockCborHex);
    }

    [Fact]
    public void StakeKeyMintRedeemerCborTest()
    {
        var mintRedeemer = new StakeKeyMintRedeemer(0, 1, true);
        var serializedMintRedeemer = CborConverter.Serialize(mintRedeemer);

    }

    [Fact]
    public void CIP68MetataLongValuesCborTest()
    {
        var cip68Metadata = new CIP68Metdata(
            new Dictionary<string, string>
            {
                { "locked_assets", "[(8b05e87a51c1d4a0fa888d2bb14dbc25e8c343ea379a171b63aa84a0,434e4354,1050)]" },
            }
        );
        var timelock = new Timelock(0, []);
        var cip68 = new CIP68<Timelock>(
            cip68Metadata,
            1,
            timelock
        );

        var cip68MetadataCborHex = Convert.ToHexString(CborConverter.Serialize(cip68Metadata)).ToLowerInvariant();
        var cip68CborHex = Convert.ToHexString(CborConverter.Serialize(cip68)).ToLowerInvariant();
        var testCb = "5f58405b2838623035653837613531633164346130666138383864326262313464626332356538633334336561333739613137316236336161383461302c34333465344a3335342c31303530295dff";
        var test = PeterO.Cbor2.CBORObject
            .DecodeFromBytes(
                Convert.FromHexString(testCb), new PeterO.Cbor2.CBOREncodeOptions("useindeflengthstrings=true"));
        var testCbor = Convert.ToHexString(test.EncodeToBytes(new PeterO.Cbor2.CBOREncodeOptions("useindeflengthstrings=true"))).ToLowerInvariant();
    }
}