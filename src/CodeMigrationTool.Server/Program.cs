using CodeMigrationTool.Sandbox;
using CodeMigrationTool.Server.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;

var builder = Host.CreateApplicationBuilder(args);

// Register application services
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<SnapshotManager>();
builder.Services.AddSingleton<SandboxManager>();
builder.Services.AddSingleton<SandboxPool>();

// Register MCP server with stdio transport and auto-discovered tools
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var host = builder.Build();
await host.RunAsync();
