using Cardano.Sync.Data.Models.Datums;
using Coinecta.Data.Models;

namespace Coinecta.Tests;

public class ComparisonTests
{
    [Fact]
    public void RationalEqualityComparerSameProperties()
    {
        var x = new Rational(1, 2);
        var y = new Rational(1, 2);
        var comparer = new RationalEqualityComparer();

        Assert.True(comparer.Equals(x, y));
    }

    [Fact]
    public void RationalEqualityComparerSameValue()
    {
        var x = new Rational(1, 2);
        var y = new Rational(2, 4);
        var comparer = new RationalEqualityComparer();

        Assert.True(comparer.Equals(x, y));
    }

    [Fact]
    public void RationalEqualityComparerDifferentProperties()
    {
        var x = new Rational(1, 2);
        var y = new Rational(3, 4);
        var comparer = new RationalEqualityComparer();

        Assert.False(comparer.Equals(x, y));
    }
}