using System.Reflection;
using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker;

namespace EphemeralDevpods.Tests.Auth;

public class AuthMiddlewareTests
{
    // The complete list of functions reachable without a session. Adding a name here is a deliberate
    // security decision — every other HTTP function must be default-denied by AuthMiddleware.
    private static readonly string[] ExpectedAnonymous = ["Logout", "OAuthCallback", "StartLogin"];

    private static IEnumerable<(string FunctionName, MethodInfo Method)> HttpFunctions() =>
        typeof(AuthMiddleware).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(m => (Name: m.GetCustomAttribute<FunctionAttribute>()?.Name, Method: m))
            .Where(x => x.Name is not null
                && x.Method.GetParameters().Any(p => p.GetCustomAttribute<HttpTriggerAttribute>() is not null))
            .Select(x => (x.Name!, x.Method));

    [Fact]
    public void Only_the_expected_http_functions_allow_anonymous_access()
    {
        var anonymous = HttpFunctions()
            .Where(f => f.Method.GetCustomAttribute<AllowAnonymousAccessAttribute>() is not null)
            .Select(f => f.FunctionName)
            .Order()
            .ToArray();

        Assert.Equal(ExpectedAnonymous, anonymous);
    }

    [Fact]
    public void The_middleware_resolves_entry_points_the_same_way_the_attribute_is_declared()
    {
        foreach (var (name, method) in HttpFunctions())
        {
            var entryPoint = $"{method.DeclaringType!.FullName}.{method.Name}";
            var expected = method.GetCustomAttribute<AllowAnonymousAccessAttribute>() is not null;

            Assert.True(
                expected == AuthMiddleware.IsAnonymousAllowed(entryPoint),
                $"{name} ({entryPoint}) should be anonymous={expected}");
        }
    }

    [Theory]
    [InlineData("Some.Unknown.Type.Run")]
    [InlineData("NoDots")]
    [InlineData("")]
    public void Unresolvable_entry_points_are_denied(string entryPoint) =>
        Assert.False(AuthMiddleware.IsAnonymousAllowed(entryPoint));

    [Fact]
    public void Every_environment_and_me_endpoint_requires_a_session()
    {
        var protectedFunctions = HttpFunctions()
            .Where(f => f.FunctionName is not ("Logout" or "OAuthCallback" or "StartLogin"))
            .Select(f => f.FunctionName)
            .ToHashSet();

        Assert.Superset(
            new HashSet<string>
            {
                "CreateEnvironment", "ListEnvironments", "GetEnvironment", "ExtendEnvironment", "DeleteEnvironment",
                "CheckRepository", "StopEnvironment", "StartEnvironment", "RestartEnvironment",
                "GetMe", "UnlinkIdentity", "StartLink",
            },
            protectedFunctions);
    }
}
