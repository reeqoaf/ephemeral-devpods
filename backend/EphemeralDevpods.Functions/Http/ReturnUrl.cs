namespace EphemeralDevpods.Functions.Http;

/// <summary>Open-redirect guard for the post-login destination: only same-origin relative paths are honoured.</summary>
public static class ReturnUrl
{
    public static string Sanitize(string? candidate, string fallback = "/")
    {
        if (string.IsNullOrEmpty(candidate)
            || candidate[0] != '/'
            // "//host" and "/\host" are protocol-relative / backslash-normalised to another origin by browsers.
            || (candidate.Length > 1 && (candidate[1] == '/' || candidate[1] == '\\'))
            || candidate.Any(char.IsControl))
        {
            return fallback;
        }

        return candidate;
    }
}
