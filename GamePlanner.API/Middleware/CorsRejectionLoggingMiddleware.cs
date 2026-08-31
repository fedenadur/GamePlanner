namespace GamePlanner.API.Middleware;

public class CorsRejectionLoggingMiddleware(RequestDelegate next, ILogger<CorsRejectionLoggingMiddleware> logger, IConfiguration config)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        var allowedOrigin = config["App:FrontendUrl"];

        if (!string.IsNullOrEmpty(origin) && !string.Equals(origin, allowedOrigin, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "CORS rejection: Origin {Origin} does not match configured {AllowedOrigin} for {Method} {Path}",
                origin, allowedOrigin, context.Request.Method, context.Request.Path);
        }

        await next(context);
    }
}
