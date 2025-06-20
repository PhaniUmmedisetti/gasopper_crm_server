using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using gasopper_crm_server.Data;
using gasopper_crm_server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add Entity Framework
builder.Services.AddDbContext<GasopperDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// REGISTER ALL SERVICES
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ILeadService, LeadService>();
builder.Services.AddScoped<IOpportunityService, OpportunityService>();

// Configure JWT Authentication - FIXED VERSION
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

Console.WriteLine($"🔍 JWT Configuration Debug:");
Console.WriteLine($"   Key: {jwtKey}");
Console.WriteLine($"   Key Length: {jwtKey?.Length ?? 0}");
Console.WriteLine($"   Issuer: {jwtIssuer}");
Console.WriteLine($"   Audience: {jwtAudience}");

if (string.IsNullOrEmpty(jwtKey))
{
    throw new InvalidOperationException("JWT Key not configured in appsettings.json");
}

var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false, // DISABLE ISSUER VALIDATION
        ValidateAudience = false, // DISABLE AUDIENCE VALIDATION
        ValidateLifetime = false, // DISABLE LIFETIME VALIDATION FOR DEBUGGING
        RequireExpirationTime = false,
        ClockSkew = TimeSpan.Zero
    };

    // DEBUG: Log JWT events
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"❌ JWT Auth Failed: {context.Exception.Message}");
            Console.WriteLine($"❌ JWT Auth Stack: {context.Exception.StackTrace}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine($"✅ JWT Token Validated successfully");
            var userIdClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            Console.WriteLine($"✅ User ID from token: {userIdClaim}");
            return Task.CompletedTask;
        },
        OnMessageReceived = context =>
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            Console.WriteLine($"🔍 Authorization Header: {authHeader}");
            
            var token = authHeader?.Split(" ").Last();
            if (!string.IsNullOrEmpty(token))
            {
                Console.WriteLine($"🔍 JWT Token Received: {token.Substring(0, Math.Min(50, token.Length))}...");
            }
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            Console.WriteLine($"❌ JWT Challenge triggered: {context.Error}");
            Console.WriteLine($"❌ JWT Error Description: {context.ErrorDescription}");
            return Task.CompletedTask;
        }
    };
});

// Configure Swagger/OpenAPI with JWT support - FIXED
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "GasopperCRM API", 
        Version = "v1"
    });

    // FIXED: Proper Bearer token configuration
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter just your token below (Bearer will be added automatically).",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http, // CHANGED from ApiKey to Http
        Scheme = "bearer", // ADDED: This makes Swagger add "Bearer " automatically
        BearerFormat = "JWT" // ADDED: Indicates JWT format
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Test database connection
try
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<GasopperDbContext>();
        var canConnect = await context.Database.CanConnectAsync();
        Console.WriteLine($"📊 Database connection: {canConnect}");
        
        if (canConnect)
        {
            var userCount = await context.Users.CountAsync();
            Console.WriteLine($"📊 Users in database: {userCount}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Database error: {ex.Message}");
}

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CRITICAL: Correct middleware order
app.UseAuthentication(); // MUST come before UseAuthorization
app.UseAuthorization();

app.MapControllers();

Console.WriteLine("🚀 API Server starting...");
Console.WriteLine("📱 Swagger: http://localhost:5211/swagger");
Console.WriteLine("🔐 JWT debugging enabled - All validations disabled for debugging");

app.Run();