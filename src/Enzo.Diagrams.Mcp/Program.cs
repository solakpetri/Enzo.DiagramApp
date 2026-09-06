using Enzo.Diagrams.Mcp;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IEnzoDiagramRenderer, EnzoDiagramRenderer>();

builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new HealthResponse("healthy", "Enzo.Diagrams.Mcp")))
    .WithName("Health");

app.MapMcp("/mcp");

app.Run();

public partial class Program;
