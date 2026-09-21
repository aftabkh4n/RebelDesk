using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RebelDesk.Server.Auth;
using RebelDesk.Server.Data;
using RebelDesk.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

// --- Options ---
var jwtOptions = new JwtOptions
{
    Issuer = builder.Configuration["Auth:Issuer"] ?? "rebeldesk",
    Audience = builder.Configuration["Auth:Audience"] ?? "rebeldesk",
    SigningKey = builder.Configuration["Auth:SigningKey"] ?? Environment.GetEnvironmentVariable("REBELDESK_JWT_KEY") ?? "",
    UserTokenMinutes = int.Parse(builder.Configuration["Auth:UserTokenMinutes"] ?? "480"),
    AgentTokenMinutes = int.Parse(builder.Configuration["Auth:AgentTokenMinutes"] ?? "43200"),
    SessionTokenMinutes = int.Parse(builder.Configuration["Auth:SessionTokenMinutes"] ?? "10"),
};

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
{
    if (builder.Environment.IsDevelopment())
    {
        jwtOptions = jwtOptions with { SigningKey = "dev-only-signing-key-CHANGE-BEFORE-PROD-32bytes" };
        Console.WriteLine("[warn] Auth:SigningKey missing, using development fallback. Set REBELDESK_JWT_KEY in production.");
    }
    else
    {
        throw new InvalidOperationException("Auth:SigningKey (or REBELDESK_JWT_KEY) must be set (>=32 bytes) in production.");
    }
}

var turnOptions = new TurnOptions
{
    StaticAuthSecret = builder.Configuration["Turn:StaticAuthSecret"] ?? Environment.GetEnvironmentVariable("REBELDESK_TURN_SECRET") ?? "dev-only-turn-secret-CHANGE",
    Realm = builder.Configuration["Turn:Realm"] ?? "rebeldesk",
    StunUris = builder.Configuration.GetSection("Turn:StunUris").Get<string[]>() ?? new[] { "stun:localhost:3478" },
    TurnUris = builder.Configuration.GetSection("Turn:TurnUris").Get<string[]>() ?? new[] { "turn:localhost:3478?transport=udp", "turn:localhost:3478?transport=tcp" },
    TtlSeconds = int.Parse(builder.Configuration["Turn:TtlSeconds"] ?? "600"),
};

// --- Services ---
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(turnOptions);
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<TurnCredentialService>();

var dbPath = builder.Configuration["Database:Path"] ?? "rebeldesk.db";
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var access = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(access) && path.StartsWithSegments("/hub/signaling"))
                    ctx.Token = access!;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:5173" })
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddControllers();
builder.Services.AddSignalR(o => { o.MaximumReceiveMessageSize = 256 * 1024; });
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }));

app.MapControllers();
app.MapHub<SignalingHub>("/hub/signaling");

app.Run();

public partial class Program { }
