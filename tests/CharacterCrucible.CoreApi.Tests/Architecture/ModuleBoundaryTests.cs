using System.Reflection;
using CharacterCrucible.CoreApi.Modules.Rulesets.Domain.Entities;
using NetArchTest.Rules;

namespace CharacterCrucible.CoreApi.Tests.Architecture;

/// <summary>
/// The Core API is a modular monolith: modules are folders and namespaces rather than projects,
/// so nothing structural stops one module referencing another's internal types. See
/// src/CharacterCrucible.CoreApi/Modules/README.md.
/// </summary>
/// <remarks>
/// What these tests CANNOT see, so green is not proof:
/// <list type="bullet">
/// <item>Transitive reaches. <c>HaveDependencyOn</c> is not transitive, so a type outside
/// <c>Modules/</c> — a shared DbContext, say — may reference one module's internals while
/// another module references that type. Neither module shows a dependency on the other.</item>
/// <item><c>public const</c> values, which the compiler inlines, leaving no IL reference.</item>
/// <item>Enum members cast to their underlying type, which compiles to a bare numeric load.</item>
/// <item>Reflection and service-locator lookups by string.</item>
/// </list>
/// </remarks>
public class ModuleBoundaryTests
{
    private const string ModulesRoot = "CharacterCrucible.CoreApi.Modules";

    private static readonly Assembly CoreApi = typeof(Ruleset).Assembly;

    /// <summary>
    /// Discovered from the assembly rather than hardcoded. A hardcoded roster means a module
    /// added later is silently never checked — the suite stays green while the boundary it
    /// claims to enforce goes unguarded. Modules with no types yet are absent by construction,
    /// so there are no cases that pass without asserting anything.
    /// </summary>
    private static readonly string[] DiscoveredModules = CoreApi.GetTypes()
        .Select(t => t.Namespace)
        .Where(ns => ns is not null && ns.StartsWith($"{ModulesRoot}.", StringComparison.Ordinal))
        .Select(ns => ns![(ModulesRoot.Length + 1)..].Split('.')[0])
        .Distinct(StringComparer.Ordinal)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    public static TheoryData<string> Modules()
    {
        var data = new TheoryData<string>();
        foreach (var module in DiscoveredModules)
        {
            data.Add(module);
        }
        return data;
    }

    /// <summary>
    /// Guards the premise of every other test here. If discovery returns nothing — a renamed
    /// root namespace, a moved folder — the boundary assertions would all pass over an empty
    /// set and report success for a rule they never evaluated.
    /// </summary>
    [Fact]
    public void Discovery_finds_at_least_one_module()
    {
        Assert.NotEmpty(DiscoveredModules);
    }

    /// <summary>
    /// NetArchTest matches namespaces by prefix, so a module named as a prefix of another would
    /// absorb it: <c>Modules.Rulesets</c> would match every type in a future
    /// <c>Modules.RulesetsImport</c>, and that module's reaches into Rulesets internals would
    /// read as intra-module and be allowed.
    /// </summary>
    [Fact]
    public void No_module_name_is_a_prefix_of_another()
    {
        var collisions = DiscoveredModules
            .SelectMany(a => DiscoveredModules
                .Where(b => !string.Equals(a, b, StringComparison.Ordinal)
                            && b.StartsWith(a, StringComparison.Ordinal))
                .Select(b => $"{a} is a prefix of {b}"))
            .ToArray();

        Assert.True(collisions.Length == 0,
            "Prefix matching would make the boundary check unreliable for: " +
            string.Join(", ", collisions));
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Module_does_not_reference_another_module(string module)
    {
        var sourceTypes = Types.InAssembly(CoreApi)
            .That().ResideInNamespace($"{ModulesRoot}.{module}")
            .GetTypes()
            .ToArray();

        // Without this, a module whose types all moved or were removed would pass this test
        // while asserting nothing at all.
        Assert.True(sourceTypes.Length > 0,
            $"No types found in {ModulesRoot}.{module}, so this boundary is unverified.");

        var others = DiscoveredModules
            .Where(other => !string.Equals(other, module, StringComparison.Ordinal))
            .ToArray();

        foreach (var other in others)
        {
            var result = Types.InAssembly(CoreApi)
                .That().ResideInNamespace($"{ModulesRoot}.{module}")
                .ShouldNot().HaveDependencyOn($"{ModulesRoot}.{other}")
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(module, other, result));
        }
    }

    /// <summary>
    /// A .cs file created outside the IDE can end up with no namespace declaration, putting the
    /// type in the global namespace. It compiles, it is visible to every module, and it is
    /// invisible to the test above — there is no namespace to assert against.
    /// </summary>
    [Fact]
    public void No_type_sits_in_the_global_namespace()
    {
        // The entry point class has no namespace and cannot be given one; excluded by identity
        // rather than by name so a renamed entry point stays excluded.
        var entryPoint = CoreApi.EntryPoint?.DeclaringType;

        var orphans = CoreApi.GetTypes()
            .Where(t => !t.IsNested && string.IsNullOrEmpty(t.Namespace))
            .Where(t => !t.Name.StartsWith('<'))          // compiler-generated
            .Where(t => t != entryPoint)
            .Select(t => t.Name)
            .ToArray();

        Assert.True(orphans.Length == 0,
            "Types with no namespace are visible to every module and invisible to the " +
            $"boundary test: {string.Join(", ", orphans)}");
    }

    private static string Describe(string from, string to, TestResult result)
    {
        var offenders = result.FailingTypeNames ?? [];
        return $"{from} must not depend on {to}. Offending types:{Environment.NewLine}" +
               string.Join(Environment.NewLine, offenders.Select(n => $"  {n}"));
    }
}
