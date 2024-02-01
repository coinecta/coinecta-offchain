

using System.Formats.Cbor;
using CborSerialization;
using Coinecta.Data.Models.Datums;

namespace Coinecta.Tests;

public class UnitTest1
{
    [Fact]
    public void SignatureCborTest()
    {
        var signature = CborConvertor.Deserialize<Signature>(Convert.FromHexString("d8799f581c0c61f135f652bc17994a5411d0a256de478ea24dbc19759d2ba14f03ff"));
        var signatureHex = Convert.ToHexString(CborConvertor.Serialize(signature)).ToLowerInvariant();
        Assert.Equal("d8799f581c0c61f135f652bc17994a5411d0a256de478ea24dbc19759d2ba14f03ff", signatureHex);
    }

    [Fact]
    public void RationalCborTest()
    {
        var rational = CborConvertor.Deserialize<Rational>(Convert.FromHexString("d8799f051864ff"));
        var rationalHex = Convert.ToHexString(CborConvertor.Serialize(rational)).ToLowerInvariant();
        Assert.Equal("d8799f051864ff", rationalHex);
    }

    [Fact]
    public void RewardSettingCborTest()
    {
        var rewardSetting = CborConvertor.Deserialize<RewardSetting>(Convert.FromHexString("d8799f1864d8799f051864ffff"));
        var rewardSettingHex = Convert.ToHexString(CborConvertor.Serialize(rewardSetting)).ToLowerInvariant();
        Assert.Equal("d8799f1864d8799f051864ffff", rewardSettingHex);
    }

    [Fact]
    public void StakePoolCborTest()
    {
        var stakePool = CborConvertor.Deserialize<StakePool>(Convert.FromHexString("d8799f9fd8799f1a000493e0d8799f051864ffffff581c8b05e87a51c1d4a0fa888d2bb14dbc25e8c343ea379a171b63aa84a044434e4354d8799f581c0c61f135f652bc17994a5411d0a256de478ea24dbc19759d2ba14f03ff00ff"));
        var stakePoolHex = Convert.ToHexString(CborConvertor.Serialize(stakePool)).ToLowerInvariant();
        Assert.Equal("d8799f9fd8799f1a000493e0d8799f051864ffffff581c8b05e87a51c1d4a0fa888d2bb14dbc25e8c343ea379a171b63aa84a044434e4354d8799f581c0c61f135f652bc17994a5411d0a256de478ea24dbc19759d2ba14f03ff00ff", stakePoolHex);
    }
}