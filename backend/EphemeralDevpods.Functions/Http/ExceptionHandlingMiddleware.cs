using System.Net;
using System.Text.Json;
using EphemeralDevpods.Core;
using EphemeralDevpods.Core.Auth;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace EphemeralDevpods.Functions.Http;

/// <summary>
/// Single place that turns exceptions from HTTP-triggered functions into JSON error responses, so
/// individual functions don't need try/catch just to map failures to status codes.
/// Non-HTTP triggers (timers) are logged and rethrown.
/// </summary>
public sealed class ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger) : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var req = await context.GetHttpRequestDataAsync();
            if (req is null)
            {
                logger.LogError(ex, "Unhandled exception in {FunctionName}", context.FunctionDefinition.Name);
                throw;
            }

            var (status, message) = ex switch
            {
                UnauthorizedException => (HttpStatusCode.Unauthorized, "Authentication required."),
                ConflictException => (HttpStatusCode.Conflict, ex.Message),
                UserInputException => (HttpStatusCode.BadRequest, ex.Message),
                JsonException => (HttpStatusCode.BadRequest, "Request body is not valid JSON."),
                KeyNotFoundException => (HttpStatusCode.NotFound, "Not found."),
                _ => (HttpStatusCode.InternalServerError, "Internal server error."),
            };

            if (status == HttpStatusCode.InternalServerError)
            {
                logger.LogError(ex, "Unhandled exception in {FunctionName}", context.FunctionDefinition.Name);
            }

            var response = await req.WriteJsonAsync(status, new { error = message }, context.CancellationToken);
            context.GetInvocationResult().Value = response;
        }
    }
}
