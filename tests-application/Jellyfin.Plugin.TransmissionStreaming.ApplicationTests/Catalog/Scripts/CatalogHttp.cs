#!/usr/bin/env dotnet
#:sdk Microsoft.NET.Sdk.Web
#:property PublishAot=false

var root = Environment.GetEnvironmentVariable("CATALOG_ROOT") ?? throw new InvalidOperationException("Catalog root was not configured.");
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
var app = builder.Build();
app.MapGet("/health", () => Results.Ok());
app.MapGet("/manifest.json", () => Results.File(Path.Combine(root, "manifest.json"), "application/json"));
app.MapGet("/package.zip", () => Results.File(Path.Combine(root, "package.zip"), "application/zip"));
await app.RunAsync();
