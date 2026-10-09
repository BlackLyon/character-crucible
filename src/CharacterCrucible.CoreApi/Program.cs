using CharacterCrucible.CoreApi.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Fail at startup, not on the first request. UseNpgsql(null) builds without complaint and the
// host starts clean, so a missing setting surfaced as "The ConnectionString property has not
// been initialized" from whichever endpoint happened to be hit first — naming no key, no
// environment and no context.
var connectionString = builder.Configuration.GetConnectionString("CharacterCrucible")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:CharacterCrucible is not configured. Set it in " +
        "appsettings.Development.json locally, or the ConnectionStrings__CharacterCrucible " +
        "environment variable when deployed.");

// snake_case rather than EF's PascalCase: Postgres folds unquoted identifiers, so PascalCase
// means every hand-written query needs quoting. pgAdmin is in the compose file, so raw queries
// will happen. Migrations are applied explicitly, never on startup.
builder.Services.AddDbContext<CharacterCrucibleDbContext>(options => options
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention());

// The database check tests for PENDING MIGRATIONS, not just connectivity. CanConnectAsync --
// what AddDbContextCheck does by default -- returns true against a connected but unmigrated
// database, so the default would report healthy on an empty schema while every query failed
// with "relation does not exist".
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CharacterCrucibleDbContext>(
        "database",
        customTestQuery: async (db, ct) => !(await db.Database.GetPendingMigrationsAsync(ct)).Any());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// No UseHttpsRedirection: TLS terminates at the ingress in Container Apps,
// and redirecting inside the container causes loops.

// Liveness and readiness are split deliberately. /health keeps the meaning it had before this
// service had a database — "the process is up" — so a database blip does not pull every replica
// out of rotation. /health/ready includes the database, and is what a deployment gate or an
// alert should watch.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

// Modules map their own endpoints here. See Modules/README.md for the rules.

app.Run();

// Exposed so WebApplicationFactory<Program> can boot the app in integration tests.
public partial class Program;
