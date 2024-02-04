using CborSerialization;
using Coinecta.Data.Models.Datums;

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
        var rewardSetting = CborConverter.Deserialize<RewardSetting>(Convert.FromHexString("d8799f1864d8799f051864ffff"));
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
}