using System.Reflection;
using System.Text.Json;

namespace CharacterCrucible.Rules.Tests.Architecture;

/// <summary>The Rules service is handed everything it decides on, so it never reads a database.</summary>
public class NoDatabaseTests
{
    // Libraries rather than NetArchTest's namespace rules: the question is what ships with Rules.
    private static readonly string[] DataAccess =
    [
        "Npgsql", "Microsoft.EntityFrameworkCore", "Microsoft.Data.", "System.Data", "Dapper",
        "MySqlConnector", "MongoDB.", "Microsoft.Azure.Cosmos", "Azure.Data.Tables", "StackExchange.Redis",
    ];

    [Fact]
    public void The_Rules_service_ships_no_data_access_package()
    {
        var packages = PackagesIn("CharacterCrucible.Rules.deps.json");

        // A known package, so a file that failed to parse can't pass as an empty graph.
        Assert.Contains("Microsoft.AspNetCore.OpenApi", packages);
        AssertNoDataAccess(packages);
    }

    [Fact]
    public void The_Rules_service_code_uses_no_data_access_library()
    {
        // Catches framework assemblies, which aren't packages: raw ADO.NET is System.Data.Common.
        var references = Assembly.Load("CharacterCrucible.Rules").GetReferencedAssemblies()
            .Select(r => r.Name).OfType<string>().ToList();

        Assert.Contains(references, r => r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        AssertNoDataAccess(references);
    }

    private static void AssertNoDataAccess(IEnumerable<string> names)
    {
        var found = names.Where(n => DataAccess.Any(d => n.StartsWith(d, StringComparison.Ordinal))).ToList();
        Assert.True(found.Count == 0, "Rules depends on data access: " + string.Join(", ", found));
    }

    // Every library in the deps file is in the Rules deploy, transitive dependencies included.
    private static HashSet<string> PackagesIn(string depsFile)
    {
        using var deps = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, depsFile)));
        return deps.RootElement.GetProperty("libraries").EnumerateObject()
            .Select(library => library.Name.Split('/')[0])
            .ToHashSet();
    }
}
