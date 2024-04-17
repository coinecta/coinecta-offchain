using Cardano.Sync.Data.Models.Datums;

namespace Coinecta.Data.Models;

public class RationalEqualityComparer : IEqualityComparer<Rational>
{
    public bool Equals(Rational? x, Rational? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x == null || y == null) return false;

        // Using decimal to avoid overflow and maintain precision
        decimal xValue = (decimal)x.Numerator / x.Denominator;
        decimal yValue = (decimal)y.Numerator / y.Denominator;

        return xValue == yValue;
    }

    public int GetHashCode(Rational obj)
    {
        if (obj == null) return 0;

        // Simplify the rational and hash based on the simplified form
        decimal simplifiedValue = (decimal)obj.Numerator / obj.Denominator;
        return simplifiedValue.GetHashCode();
    }
}