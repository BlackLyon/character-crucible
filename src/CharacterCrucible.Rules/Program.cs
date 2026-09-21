// The Rules service is a policy decision point: it answers whether an advancement is
// legal, what it costs, and whether it needs a human. It holds no data and owns no
// database. If this service ever needs to read from Postgres, the boundary is wrong —
// see notes/architecture-decision.md.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");

// POST /evaluate lands here once the ruleset model exists.

app.Run();

// Exposed so WebApplicationFactory<Program> can boot the app in integration tests.
public partial class Program;
