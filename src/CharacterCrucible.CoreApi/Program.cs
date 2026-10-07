using CharacterCrucible.CoreApi.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// snake_case rather than EF's PascalCase: Postgres folds unquoted identifiers, so PascalCase
// means every hand-written query needs quoting. pgAdmin is in the compose file, so raw queries
// will happen. Migrations are applied explicitly, never on startup.
builder.Services.AddDbContext<CharacterCrucibleDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("CharacterCrucible"))
    .UseSnakeCaseNamingConvention());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// No UseHttpsRedirection: TLS terminates at the ingress in Container Apps,
// and redirecting inside the container causes loops.

app.MapHealthChecks("/health");

// Modules map their own endpoints here. See Modules/README.md for the rules.

app.Run();

// Exposed so WebApplicationFactory<Program> can boot the app in integration tests.
public partial class Program;
