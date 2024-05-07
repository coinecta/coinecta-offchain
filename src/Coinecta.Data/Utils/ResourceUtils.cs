using System.Reflection;

namespace Coinecta.Data.Utils;

public static class ResourceUtils
{
    public static string GetEmbeddedResourceSql(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourcePath = $"Coinecta.Data.Migrations.Sql.{resourceName}";

        using var stream = assembly.GetManifestResourceStream(resourcePath) ?? throw new InvalidOperationException("Could not find embedded resource");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}