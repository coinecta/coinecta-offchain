using CborSerialization;
using Coinecta.Data.Models.Datums;

namespace Coinecta.Tests;

public class CborTests
{
    [Fact]
    public void SignatureCborTest()
    {
        var signature = CborConvertor.Deserialize<Signature>(Convert.FromHexString("d8799f581c0c61f135f652bc17994a5411d0a256de478ea24dbc19759d2ba14f03ff"));
        var signatureCborHex = Convert.ToHexString(CborConvertor.Serialize(signature)).ToLowerInvariant();
        Assert.Equal("d8799f581c0c61f135f652bc17994a5411d0a256de478ea24dbc19759d2ba14f03ff", signatureCborHex);
    }

    [Fact]
    public void RationalCborTest()
    {
        var rational = CborConvertor.Deserialize<Rational>(Convert.FromHexString("d8799f051864ff"));
        var rationalCborHex = Convert.ToHexString(CborConvertor.Serialize(rational)).ToLowerInvariant();
        Assert.Equal("d8799f051864ff", rationalCborHex);
    }

    [Fact]
    public void RewardSettingCborTest()
    {
        var rewardSetting = CborConvertor.Deserialize<RewardSetting>(Convert.FromHexString("d8799f1864d8799f051864ffff"));
        var rewardSettingCborHex = Convert.ToHexString(CborConvertor.Serialize(rewardSetting)).ToLowerInvariant();
        Assert.Equal("d8799f1864d8799f051864ffff", rewardSettingCborHex);
    }

    [Fact]
    public void StakePoolCborTest()
    {
        var stakePool = CborConvertor.Deserialize<StakePool>(Convert.FromHexString("d8799f9fd8799f1a000493e0d8799f051864ffffff581c8b05e87a51c1d4a0fa888d2bb14dbc25e8c343ea379a171b63aa84a044434e4354d8799f581c0c61f135f652bc17994a5411d0a256de478ea24dbc19759d2ba14f03ff00ff"));
        var stakePoolHex = Convert.ToHexString(CborConvertor.Serialize(stakePool)).ToLowerInvariant();
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
        var credential = CborConvertor.Deserialize<Credential>(Convert.FromHexString("d8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ff"));
        var credentialCborHex = Convert.ToHexString(CborConvertor.Serialize(credential)).ToLowerInvariant();
        Assert.Equal("d8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ff", credentialCborHex);
    }

    [Fact]
    public void StakeCredentialCborTest()
    {
        var stakeCredential = CborConvertor.Deserialize<StakeCredential>(Convert.FromHexString("d8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffff"));
        var stakeCredentialCborHex = Convert.ToHexString(CborConvertor.Serialize(stakeCredential)).ToLowerInvariant();
        Assert.Equal("d8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffff", stakeCredentialCborHex);
    }

    [Fact]
    public void AddressWithStakeCredentialCborTest()
    {
        var address = CborConvertor.Deserialize<Address>(Convert.FromHexString("d8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffffffff"));
        var addressCborHex = Convert.ToHexString(CborConvertor.Serialize(address)).ToLowerInvariant();
        Assert.Equal("d8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd8799fd8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffffffff", addressCborHex);
    }

    [Fact]
    public void AddressWithoutStakeCredentialCborTest()
    {
        var address = CborConvertor.Deserialize<Address>(Convert.FromHexString("d8799fd8799f581ccb84310092f8c3dae1ebf0ac456114e487297d3fe684d3236588d5b3ffd87a80ff"));
        var addressCborHex = Convert.ToHexString(CborConvertor.Serialize(address)).ToLowerInvariant();
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
    }
}