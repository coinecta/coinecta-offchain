using PallasValue = PallasDotnet.Models.Value;


using PallasDotnet.Models;
using Value = Cardano.Sync.Data.Models.Value;

namespace Coinecta.Data.Extensions;

public static class PallasValueExtension
{
    public static Value ToValue(this PallasValue self) => new()
    {
        Coin = self.Coin,
        MultiAsset = self.MultiAsset.ToStringKeys()
    };

    public static Dictionary<string, Dictionary<string, ulong>> ToStringKeys(this Dictionary<Hash, Dictionary<Hash, ulong>> self) => self.ToDictionary(
            outerKvp => outerKvp.Key.ToHex(),
            outerKvp => outerKvp.Value
                .ToDictionary(
                    innerKvp => innerKvp.Key.ToHex(),
                    innerKvp => innerKvp.Value
                )
        );
}
