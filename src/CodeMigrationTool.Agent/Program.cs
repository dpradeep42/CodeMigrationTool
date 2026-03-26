using CodeMigrationTool.Agent.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<InstrumentationService>();

app.MapGet("/", () => "CodeMigrationTool Instrumentation Agent is running. Use a gRPC client to communicate.");

await app.RunAsync();
