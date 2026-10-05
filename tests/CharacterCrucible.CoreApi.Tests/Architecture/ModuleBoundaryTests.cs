using System.Reflection;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using NetArchTest.Rules;

namespace CharacterCrucible.CoreApi.Tests.Architecture;

/// <summary>
/// The Core API is a modular monolith: modules are folders and namespaces, not projects, so
/// nothing but these tests stops one module referencing another's internal types. See
/// src/CharacterCrucible.CoreApi/Modules/README.md.
/// </summary>
public class ModuleBoundaryTests
{
    private const string ModulesRoot = "CharacterCrucible.CoreApi.Modules";

    private static readonly Assembly CoreApi = typeof(Ruleset).Assembly;

    private static readonly string[] Modules = ["Characters", "Rulesets", "Advancement"];

    public static TheoryData<string, string> ModulePairs()
    {
        var data = new TheoryData<string, string>();
        foreach (var from in Modules)
        {
            foreach (var to in Modules.Where(m => m != from))
            {
                data.Add(from, to);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ModulePairs))]
    public void Module_does_not_reference_another_module(string from, string to)
    {
        var result = Types.InAssembly(CoreApi)
            .That().ResideInNamespace($"{ModulesRoot}.{from}")
            .ShouldNot().HaveDependencyOn($"{ModulesRoot}.{to}")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(from, to, result));
    }

    /// <summary>
    /// A .cs file created outside the IDE can end up with no namespace declaration, putting the
    /// type in the global namespace. It compiles, it is visible to every module, and it is
    /// invisible to the test above — there is no namespace to assert against.
    /// </summary>
    [Fact]
    public void No_type_sits_in_the_global_namespace()
    {
        // Top-level statements compile to a namespace-less entry point class, which is correct
        // and not something we can change.
        var entryPoint = CoreApi.EntryPoint?.DeclaringType;

        var orphans = CoreApi.GetTypes()
            .Where(t => !t.IsNested && string.IsNullOrEmpty(t.Namespace))
            .Where(t => !t.Name.StartsWith('<'))          // compiler-generated
            .Where(t => t != entryPoint)
            .Select(t => t.Name)
            .ToArray();

        Assert.True(orphans.Length == 0,
            $"Types with no namespace are visible to every module and invisible to the " +
            $"boundary test: {string.Join(", ", orphans)}");
    }

    private static string Describe(string from, string to, TestResult result)
    {
        var offenders = result.FailingTypeNames ?? [];
        return $"{from} must not depend on {to}. Offending types:{Environment.NewLine}" +
               string.Join(Environment.NewLine, offenders.Select(n => $"  {n}"));
    }
}
