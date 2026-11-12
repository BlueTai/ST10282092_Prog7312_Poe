using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Smart_X_Ecosystem.Models;
using Smart_X_Ecosystem.Services;
using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// All state services registered as Singletons so state is never lost across modules or routes
builder.Services.AddSingleton<IngestionEngine>();
builder.Services.AddSingleton<MeshTopologyService>();
builder.Services.AddSingleton<CommandStreamService>();
builder.Services.AddSingleton<FirebaseAuthService>();

// Hosted BackgroundService that continuously empties CriticalAlertQueue first, then TelemetryQueue
builder.Services.AddHostedService<TelemetryQueueWorker>();

builder.Services.AddScoped(sp =>
{
    var navManager = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navManager.BaseUri) };
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<Smart_X_Ecosystem.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/api/telemetry/power", async (TelemetryPacket<PowerMetric> packet, CommandStreamService streamService) =>
{
    await Task.Delay(25);
    string status = streamService.IngestDeviceTelemetry(new TelemetrySimulationRequest
    {
        MacAddress = packet.MacAddress,
        Reading = packet.Payload.Voltage > 0 ? packet.Payload.Voltage : 230.0,
        IsCriticalAlert = packet.Payload.Voltage > 380.0
    });
    return Results.Ok(new { Message = status });
});

app.MapPost("/api/upload", async (IFormFile file, CommandStreamService streamService) =>
{
    if (file == null || file.Length == 0) return Results.BadRequest("File is empty.");

    string fileContent;
    using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
    {
        fileContent = await reader.ReadToEndAsync();
    }

    int importedDevices = streamService.ImportDevicesFromText(fileContent);

    string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "EncryptedLogs");
    Directory.CreateDirectory(uploadPath);
    string filePath = Path.Combine(uploadPath, $"{Guid.NewGuid()}_{file.FileName}.enc");

    using Aes aesAlg = Aes.Create();
    aesAlg.Key = Convert.FromBase64String("VFVUaGlzSXNBU2VjdXJlMzJCeXRlS2V5Rm9yQUVTQTI=");
    aesAlg.IV = Convert.FromBase64String("VFVUaGlzSXNJVjE2Qnl0ZXM=");

    using FileStream fs = new FileStream(filePath, FileMode.Create);
    using CryptoStream cs = new CryptoStream(fs, aesAlg.CreateEncryptor(), CryptoStreamMode.Write);
    byte[] rawBytes = Encoding.UTF8.GetBytes(fileContent);
    await cs.WriteAsync(rawBytes, 0, rawBytes.Length);

    return Results.Ok(new { ImportedCount = importedDevices });
}).DisableAntiforgery();

app.MapPost("/api/stream/simulate", (TelemetrySimulationRequest request, CommandStreamService streamService) =>
{
    string result = streamService.IngestDeviceTelemetry(request);
    return Results.Ok(new { Message = result });
});

app.MapPost("/api/stream/override", (OverrideCommand cmd, CommandStreamService streamService) =>
{
    streamService.IssueManualOverride(cmd.TargetMac, cmd.ActionDescription, true);
    return Results.Ok(new { Message = "Override executed and pushed to CommandUndoStack." });
});

app.MapPost("/api/stream/undo", (CommandStreamService streamService) =>
{
    var undone = streamService.UndoLastCommand();
    return undone != null
        ? Results.Ok(new { Message = $"Reverted: {undone.ActionDescription} on {undone.DeviceName}" })
        : Results.Ok(new { Message = "CommandUndoStack is empty — nothing to undo." });
});

app.Run();
