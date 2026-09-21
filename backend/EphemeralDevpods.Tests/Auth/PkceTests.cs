using EphemeralDevpods.Functions.Functions.Auth;

namespace EphemeralDevpods.Tests.Auth;

public class PkceTests
{
    [Fact]
    public void Challenge_matches_the_RFC_7636_appendix_B_vector() =>
        Assert.Equal(
            "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            Pkce.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));

    [Fact]
    public void Random_tokens_are_url_safe_and_unique()
    {
        var a = Pkce.RandomToken(32);
        var b = Pkce.RandomToken(32);

        Assert.NotEqual(a, b);
        Assert.Matches("^[A-Za-z0-9_-]+$", a);
        Assert.InRange(a.Length, 43, 43); // 32 bytes -> 43 base64url chars, the RFC's minimum verifier length
    }
}
