var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

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
