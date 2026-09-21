using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace EphemeralDevpods.Functions.Functions.Auth;

public static class Pkce
{
    /// <summary>A URL-safe random token of <paramref name="byteCount"/> random bytes.</summary>
    public static string RandomToken(int byteCount) => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(byteCount));

    /// <summary>The S256 code challenge for a verifier (RFC 7636 §4.2).</summary>
    public static string Challenge(string verifier) =>
        Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
}
