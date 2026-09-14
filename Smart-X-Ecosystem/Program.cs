using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Smart_X_Ecosystem.Models;
using Smart_X_Ecosystem.Services;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor services
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSingleton<IngestionEngine>();

// Register HttpClient for internal API calls
builder.Services.AddScoped(sp => new System.Net.Http.HttpClient { BaseAddress = new Uri("https://localhost:5001") });

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

// --- MINIMAL API ENDPOINTS --- //

// 1. Data Ingestion Endpoint
app.MapPost("/api/telemetry/power", async (TelemetryPacket<PowerMetric> packet) =>
{
    await Task.Delay(50); // Simulate processing
    return Results.Ok(new { Message = "Processed" });
});

// 2. Encrypted File Upload Endpoint
app.MapPost("/api/upload", async (IFormFile file) =>
{
    if (file == null || file.Length == 0) return Results.BadRequest("File is empty.");

    string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "EncryptedLogs");
    Directory.CreateDirectory(uploadPath);
    string filePath = Path.Combine(uploadPath, $"{Guid.NewGuid()}_{file.FileName}.enc");

    // Server-side AES Encryption
    using Aes aesAlg = Aes.Create();
    aesAlg.Key = Convert.FromBase64String("VFVUaGlzSXNBU2VjdXJlMzJCeXRlS2V5Rm9yQUVTQTI=");
    aesAlg.IV = Convert.FromBase64String("VFVUaGlzSXNJVjE2Qnl0ZXM=");

    using FileStream fs = new FileStream(filePath, FileMode.Create);
    using CryptoStream cs = new CryptoStream(fs, aesAlg.CreateEncryptor(), CryptoStreamMode.Write);
    await file.CopyToAsync(cs);

    return Results.Ok();
})
.DisableAntiforgery(); // Required for raw file uploads in this context

app.Run();