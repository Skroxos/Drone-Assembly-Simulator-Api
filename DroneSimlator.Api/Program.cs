using Microsoft.EntityFrameworkCore;
using DroneSimlator.Api.Data;
using DroneSimlator.Api.Models;
using DroneSimlator.Api.DTOs;
using System.Threading.RateLimiting;
using System.Text;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<SimulatorDbContext>(options => 
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("StrictPolicy", context =>
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromSeconds(10),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SimulatorDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRateLimiter();


app.MapPost("/api/assembly/save", async (SaveResultRequest request, SimulatorDbContext dbContext, IConfiguration config) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest("Name is required.");
    }
    if (request.FinishTime <= 0)
    {
        return Results.BadRequest("Finish time must be greater than zero.");
    }

    var secretKey = config["ApiSecretKey"];
    if (string.IsNullOrEmpty(secretKey))
    {
        return Results.StatusCode(500);
    }
    
    string timeString = request.FinishTime.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
    string rawData = request.Name + timeString + secretKey;

    byte[] inputBytes = Encoding.UTF8.GetBytes(rawData);
    byte[] hashBytes = SHA256.HashData(inputBytes);

    string expectedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

    if (request.Hash != expectedHash)
    {
        return Results.Unauthorized();
    }

    var session = new DroneAssemblySession
    {
        Name = request.Name,
        FinishTime = request.FinishTime,
        CreatedAt = DateTime.UtcNow
    };
    dbContext.AssemblySessions.Add(session);
    await dbContext.SaveChangesAsync();
    return Results.Created($"/api/assembly/{session.Id}",new { session.Id });
})
    .RequireRateLimiting("StrictPolicy");



app.MapGet("/api/assembly/top10", async (SimulatorDbContext dbContext) =>
{
    var topSessions = await dbContext.AssemblySessions
        .OrderBy(s => s.FinishTime)
        .ThenBy(s => s.CreatedAt)
        .Take(10)
        .Select(s => new LeaderboardResponse(s.Name, s.FinishTime))
        .ToListAsync();
    return Results.Ok(topSessions);
}).RequireRateLimiting("StrictPolicy");

app.Run();