using System.Reflection;
using System.Text.RegularExpressions;
using MyApp.Api.Common.Abstractions;
using MyApp.Api.Persistence;
using NetArchTest.Rules;

namespace MyApp.Architecture.Tests;

public sealed class ArchitectureTests
{
    private static readonly Assembly Api = typeof(AppDbContext).Assembly;

    private const string Domain = "MyApp.Api.Domain";
    private const string Common = "MyApp.Api.Common";
    private const string Persistence = "MyApp.Api.Persistence";
    private const string Features = "MyApp.Api.Features";

    private static readonly Type[] HandlerInterfaces =
        [typeof(ICommandHandler<>), typeof(ICommandHandler<,>), typeof(IQueryHandler<,>)];

    /// <summary>Feature namespaces, e.g. MyApp.Api.Features.Products.</summary>
    private static readonly string[] FeatureNamespaces = NamespacesAtDepth(4);

    /// <summary>Slice namespaces, e.g. MyApp.Api.Features.Products.CreateProduct.</summary>
    private static readonly string[] SliceNamespaces = NamespacesAtDepth(5);

    public static TheoryData<string> Slices => new(SliceNamespaces);

    public static TheoryData<string> FeatureNames => new(FeatureNamespaces);

    [Fact]
    public void Domain_depends_on_nothing()
    {
        var result = Types.InAssembly(Api)
            .That().ResideInNamespace(Domain)
            .ShouldNot().HaveDependencyOnAny(Common, Persistence, Features, "Microsoft")
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void Common_does_not_depend_on_Features_or_Persistence()
    {
        var result = Types.InAssembly(Api)
            .That().ResideInNamespace(Common)
            .ShouldNot().HaveDependencyOnAny(Features, Persistence)
            .GetResult();

        AssertSuccess(result);
    }

    [Theory]
    [MemberData(nameof(FeatureNames))]
    public void Feature_does_not_reference_another_feature(string feature)
    {
        var others = FeatureNamespaces.Where(f => f != feature).ToArray();
        if (others.Length == 0)
        {
            return;
        }

        var result = Types.InAssembly(Api)
            .That().ResideInNamespaceMatching($"^{Regex.Escape(feature)}(\\..*)?$")
            .ShouldNot().HaveDependencyOnAny(others)
            .GetResult();

        AssertSuccess(result);
    }

    [Theory]
    [MemberData(nameof(Slices))]
    public void Slice_does_not_reference_another_slice(string slice)
    {
        // NetArchTest matches whole namespace segments, so GetProducts does not also match GetProductById.
        // Don't add a trailing dot: that silently matches nothing.
        var others = SliceNamespaces.Where(s => s != slice).ToArray();

        var result = Types.InAssembly(Api)
            .That().ResideInNamespaceMatching($"^{Regex.Escape(slice)}$")
            .ShouldNot().HaveDependencyOnAny(others)
            .GetResult();

        AssertSuccess(result);
    }

    [Fact]
    public void Handlers_are_sealed_and_named_Handler()
    {
        var offenders = ConcreteTypes()
            .Where(t => t.GetInterfaces().Any(IsHandlerInterface))
            .Where(t => !t.IsSealed || !NameWithoutArity(t).EndsWith("Handler", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(offenders.Count == 0, "Handlers must be sealed and end with 'Handler': " + string.Join(", ", offenders));
    }

    [Fact]
    public void Endpoints_implement_IEndpoint_and_live_in_a_slice()
    {
        var types = ConcreteTypes().ToList();

        var namedButNotEndpoint = types
            .Where(t => t.Name.EndsWith("Endpoint", StringComparison.Ordinal) && !typeof(IEndpoint).IsAssignableFrom(t))
            .Select(t => t.FullName);

        var endpointOutsideSlice = types
            .Where(t => typeof(IEndpoint).IsAssignableFrom(t) && !SliceNamespaces.Contains(t.Namespace))
            .Select(t => t.FullName);

        var offenders = namedButNotEndpoint.Concat(endpointOutsideSlice).ToList();
        Assert.True(offenders.Count == 0, "Endpoint rule broken by: " + string.Join(", ", offenders));
    }

    [Fact]
    public void No_mediator_mapper_or_validation_libraries_are_referenced()
    {
        string[] forbidden = ["MediatR", "Mediator", "Wolverine", "MassTransit", "AutoMapper", "FluentValidation", "Carter", "Scrutor"];

        var offenders = Api.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => forbidden.Any(f => name.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Slices_are_discovered()
    {
        // Guards the theories above from passing vacuously if namespaces change.
        Assert.Contains("MyApp.Api.Features.Products.CreateProduct", SliceNamespaces);
        Assert.Contains("MyApp.Api.Features.Auth.Login", SliceNamespaces);
    }

    private static string[] NamespacesAtDepth(int depth) =>
        Api.GetTypes()
            .Select(t => t.Namespace)
            .OfType<string>()
            .Where(ns => ns.StartsWith(Features + ".", StringComparison.Ordinal))
            .Select(ns => string.Join('.', ns.Split('.').Take(depth)))
            .Where(ns => ns.Split('.').Length == depth)
            .Distinct()
            .Order()
            .ToArray();

    private static IEnumerable<Type> ConcreteTypes() =>
        Api.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)));

    private static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && HandlerInterfaces.Contains(type.GetGenericTypeDefinition());

    private static string NameWithoutArity(Type type) => type.Name.Split('`')[0];

    private static void AssertSuccess(NetArchTest.Rules.TestResult result) =>
        Assert.True(result.IsSuccessful, "Violations: " + string.Join(", ", result.FailingTypeNames ?? []));
}
