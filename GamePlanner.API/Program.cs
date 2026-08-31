using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using GamePlanner.API.Context;
using GamePlanner.API.Managers;
using GamePlanner.API.Services;
using GamePlanner.API.Workers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<GamePlannerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<GameManager>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SteamAuthService>();
builder.Services.AddSingleton<SteamTokenStore>();
builder.Services.AddScoped<SessionManager>();
builder.Services.AddScoped<SessionPlannerManager>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IgdbService>();
builder.Services.AddSingleton<SteamService>();
builder.Services.AddScoped<GameSyncManager>();
builder.Services.AddScoped<GameImportManager>();
builder.Services.AddScoped<ImportJobManager>();
builder.Services.AddScoped<GameEnrichmentManager>();
builder.Services.AddScoped<GameImageManager>();
builder.Services.AddScoped<WorkerStatusManager>();
builder.Services.AddSingleton<CsvGameService>();
builder.Services.AddScoped<GameHLTBManager>();
builder.Services.AddScoped<UpcomingGamesManager>();
builder.Services.AddScoped<NowPlayingManager>();
builder.Services.AddScoped<TrackedGamesManager>();
builder.Services.AddScoped<BacklogPrioritizerManager>();
builder.Services.AddScoped<GameJournalManager>();
builder.Services.AddScoped<StatsDashboardManager>();
builder.Services.AddScoped<DashboardManager>();
builder.Services.AddScoped<SteamPlaytimeSyncManager>();
builder.Services.AddHostedService<GameSyncWorker>();
builder.Services.AddHostedService<GameImageSyncWorker>();
builder.Services.AddHostedService<SteamImportWorker>();
builder.Services.AddHostedService<GameHLTBSyncWorker>();
builder.Services.AddHostedService<UpcomingGamesSyncWorker>();
builder.Services.AddHostedService<SteamPlaytimeSyncWorker>();
builder.Services.AddHostedService<SteamTokenCleanupWorker>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwt["Issuer"],
            ValidAudience            = jwt["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwt["Secret"]!)),
            RequireExpirationTime    = true,
            ValidAlgorithms          = [SecurityAlgorithms.HmacSha256]
        };
    });

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit         = 10;
        o.Window              = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        o.QueueLimit          = 0;
    });

    options.AddPolicy<string>("mutations", httpContext =>
    {
        var key = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit           = 100,
            Window                 = TimeSpan.FromHours(1),
            QueueProcessingOrder   = QueueProcessingOrder.OldestFirst,
            QueueLimit             = 0
        });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = (context, _) =>
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("RateLimiting");

        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        var path = context.HttpContext.Request.Path;
        var policyName = context.HttpContext.GetEndpoint()?
            .Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;

        logger.LogWarning("Rate limit rejected for {UserId} on {Path} by policy {PolicyName}", userId, path, policyName);

        return ValueTask.CompletedTask;
    };
});

var frontendUrl = builder.Configuration["App:FrontendUrl"];
if (string.IsNullOrWhiteSpace(frontendUrl))
    throw new InvalidOperationException("Missing required configuration key 'App:FrontendUrl'.");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins(frontendUrl)
              .WithHeaders("Authorization", "Content-Type")
              .WithMethods("GET", "POST", "PUT", "DELETE"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<GamePlanner.API.Middleware.CorsRejectionLoggingMiddleware>();
app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

app.Run();
