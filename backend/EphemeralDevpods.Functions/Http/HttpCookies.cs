using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Http;

public static class HttpCookies
{
    public static string? GetCookie(this HttpRequestData req, string name) =>
        req.Cookies.FirstOrDefault(c => c.Name == name)?.Value;

    public static void SetCookie(
        this HttpResponseData response, string name, string value, string path, TimeSpan maxAge, bool secure) =>
        response.Cookies.Append(new HttpCookie(name, value)
        {
            Path = path,
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSite.Lax, // Lax so the top-level redirect back from the provider still carries it.
            MaxAge = maxAge.TotalSeconds,
        });

    public static void ClearCookie(this HttpResponseData response, string name, string path, bool secure) =>
        response.SetCookie(name, "", path, TimeSpan.Zero, secure);
}
