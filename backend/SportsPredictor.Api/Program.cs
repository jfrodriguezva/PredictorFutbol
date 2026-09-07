using Microsoft.EntityFrameworkCore;
using SportsPredictor.Api.Middleware;
using SportsPredictor.Application;
using SportsPredictor.Infrastructure;
using SportsPredictor.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
    options.SingleLine = true;
});

// Default port for local/installed use: 20050. ASPNETCORE_URLS (set by launchSettings.json
// in dev, or by the installer's service config) always wins over this fallback.
if (Environment.GetEnvironmentVariable("ASPNETCORE_URLS") is null)
{
    builder.WebHost.UseUrls("http://localhost:20050");
}

const string frontendCorsPolicy = "FrontendCorsPolicy";
var frontendOrigin = builder.Configuration["Frontend:BaseUrl"] ?? "http://localhost:20051";

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendCorsPolicy, policy =>
    {
        policy.WithOrigins(frontendOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Applies pending EF Core migrations at startup — the installed/desktop-launcher
// scenario has no one around to run `dotnet ef database update` by hand. Only runs
// when a connection string is actually configured (same guard AddInfrastructure uses),
// and never crashes the app if it fails — it just logs, so a bad DB state is visible
// in the logs instead of silently blocking every other endpoint from starting.
var migrationConnectionString = builder.Configuration.GetConnectionString("SportsPredictorDb");
if (!string.IsNullOrWhiteSpace(migrationConnectionString))
{
    try
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SportsPredictorDbContext>().Database.Migrate();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to apply EF Core migrations at startup.");
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(frontendCorsPolicy);
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program
{
}
