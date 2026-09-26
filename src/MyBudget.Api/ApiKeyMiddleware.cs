using MyBudget.Contracts;

namespace MyBudget.Api;

/// <summary>Single shared API key on every request (ADR-0006). No key configured means the API refuses to start.</summary>
public static class ApiKeyMiddleware
{
    public static IApplicationBuilder UseApiKey(this WebApplication app, string? expectedKey, string[] allowAnonymousPaths)
    {
        if (string.IsNullOrWhiteSpace(expectedKey))
            throw new InvalidOperationException("Api:Key must be configured.");

        return app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            if (allowAnonymousPaths.Any(p => path.StartsWithSegments(p)) ||
                (app.Environment.IsDevelopment() && path.StartsWithSegments("/openapi")))
            {
                await next(context);
                return;
            }

            if (!context.Request.Headers.TryGetValue(ApiKeyHeader.Name, out var provided) || provided != expectedKey)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Missing or invalid API key.");
                return;
            }

            await next(context);
        });
    }
}
