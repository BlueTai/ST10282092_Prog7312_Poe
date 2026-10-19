using Microsoft.AspNetCore.Components;
using Smart_X_Ecosystem.Models;
using Smart_X_Ecosystem.Services;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Singletons maintain telemetry, queues, stacks, and dictionaries across view switches
builder.Services.AddSingleton<IngestionEngine>();
builder.Services.AddSingleton<CommandStreamService>();
builder.Services.AddScoped<FirebaseAuthService>();

// Dynamic base address ensures internal HTTP calls resolve correctly on any port or Docker container
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

// 1. Telemetry Ingestion Endpoint (Ingests packet and synchronizes DeviceRegistry & TelemetryQueue)
app.MapPost("/api/telemetry/power", async (TelemetryPacket<PowerMetric> packet, CommandStreamService streamService) =>
{
    await Task.Delay(50);
    double reading = packet.Payload.Voltage > 0
        ? packet.Payload.Voltage
        : (packet.Payload.Wattage > 0 ? packet.Payload.Wattage : 230.0);

    string status = streamService.IngestDeviceTelemetry(new TelemetrySimulationRequest
    {
        MacAddress = packet.MacAddress,
        Reading = reading,
        IsCriticalAlert = reading > 380.0
    });
    return Results.Ok(new { Message = status });
});

// 2. Upgraded File Upload Endpoint: Parses .txt IoT devices into DeviceRegistry AND writes AES-256 encrypted log
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

// 3. Live Device Telemetry Simulation Endpoint (Hits Queues, PriorityQueue, and DeviceRegistry)
app.MapPost("/api/stream/simulate", (TelemetrySimulationRequest request, CommandStreamService streamService) =>
{
    string result = streamService.IngestDeviceTelemetry(request);
    return Results.Ok(new { Message = result });
});

// 4. Manual Override Endpoint (Executes action and pushes previous state to CommandUndoStack)
app.MapPost("/api/stream/override", (OverrideCommand cmd, CommandStreamService streamService) =>
{
    streamService.IssueManualOverride(cmd.TargetMac, cmd.ActionDescription, true);
    return Results.Ok(new { Message = "Override executed and pushed to CommandUndoStack." });
});

// 5. Undo Command Endpoint (Pops CommandUndoStack and reverts device in O(1) time)
app.MapPost("/api/stream/undo", (CommandStreamService streamService) =>
{
    var undone = streamService.UndoLastCommand();
    return undone != null
        ? Results.Ok(new { Message = $"Reverted: {undone.ActionDescription} on {undone.DeviceName}" })
        : Results.Ok(new { Message = "CommandUndoStack is empty — nothing to undo." });
});

app.Run();
