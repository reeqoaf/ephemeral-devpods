using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Http;

/// <summary>
/// Manual JSON read/write over HttpRequestData.Body/HttpResponseData.Body — avoids depending on
/// convenience extension overloads whose exact shape varies across Functions Worker versions.
/// </summary>
public static class HttpJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<HttpResponseData> WriteJsonAsync(
        this HttpRequestData req, HttpStatusCode status, object value, CancellationToken ct)
    {
        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await JsonSerializer.SerializeAsync(response.Body, value, Options, ct);
        return response;
    }

    public static Task<HttpResponseData> BadRequestAsync(this HttpRequestData req, string message, CancellationToken ct) =>
        req.WriteJsonAsync(HttpStatusCode.BadRequest, new { error = message }, ct);

    public static Task<HttpResponseData> NotFoundAsync(this HttpRequestData req, CancellationToken ct) =>
        req.WriteJsonAsync(HttpStatusCode.NotFound, new { error = "Not found." }, ct);

    public static async Task<T?> ReadJsonAsync<T>(this HttpRequestData req, CancellationToken ct) =>
        await JsonSerializer.DeserializeAsync<T>(req.Body, Options, ct);
}
