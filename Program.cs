using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using gasopper_crm_server.Data;
using gasopper_crm_server.Services;
using gasopper_crm_server.Services.Database;

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
builder.Services.AddScoped<IGasStationService, GasStationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IOtpService, OtpService>();

// 🆕 REGISTER DATABASE SEEDING SERVICES
builder.Services.AddScoped<IDatabaseSeeder, SmartSeeder>();
builder.Services.AddScoped<DataMigrationService>();
builder.Services.AddScoped<DatabaseHealthService>();

// ADD CORS FOR FRONTEND - FIXED
builder.Services.AddCors(options =>
{
   options.AddPolicy("AllowFrontend", policy =>
   {
       policy.AllowAnyOrigin()
             .AllowAnyMethod()
             .AllowAnyHeader();
   });
});

// 🆕 ADD HEALTH CHECKS
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthService>("database");

// Configure JWT Authentication
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
       ValidateIssuer = false,
       ValidateAudience = false,
       ValidateLifetime = false,
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

// Configure Swagger/OpenAPI with JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
   c.SwaggerDoc("v1", new OpenApiInfo
   {
       Title = "GasopperCRM API",
       Version = "v1",
       Description = "Complete Gas Station CRM API with Automated Database Seeding"
   });

   // JWT Bearer token configuration
   c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
   {
       Description = "JWT Authorization header using the Bearer scheme. Enter just your token below (Bearer will be added automatically).",
       Name = "Authorization",
       In = ParameterLocation.Header,
       Type = SecuritySchemeType.Http,
       Scheme = "bearer",
       BearerFormat = "JWT"
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

// 🚀 AUTO-SEED DATABASE ON STARTUP - CRITICAL SECTION
Console.WriteLine("🌱 Starting automated database seeding process...");
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        logger.LogInformation("🔄 Starting database initialization...");
        
        var context = scope.ServiceProvider.GetRequiredService<GasopperDbContext>();
        
        // Apply EF Core migrations first
        logger.LogInformation("📦 Applying Entity Framework migrations...");
        await context.Database.MigrateAsync();
        logger.LogInformation("✅ Entity Framework migrations completed");
        
        // Apply data migrations and seeding
        logger.LogInformation("🌱 Starting data migration and seeding...");
        var migrationService = scope.ServiceProvider.GetRequiredService<DataMigrationService>();
        var success = await migrationService.ApplyDataMigrationsAsync();
        
        if (!success)
        {
            logger.LogError("❌ Data migration failed - application startup aborted");
            throw new InvalidOperationException("Database seeding failed - cannot start application");
        }
        
        // Perform health check
        var healthService = scope.ServiceProvider.GetRequiredService<DatabaseHealthService>();
        var healthResult = await healthService.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());
        
        if (healthResult.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy)
        {
            logger.LogInformation("✅ Database is ready and fully seeded");
            logger.LogInformation("📊 Health Status: {Status}", healthResult.Description);
        }
        else
        {
            logger.LogWarning("⚠️ Database health check: {Status} - {Description}", 
                healthResult.Status, healthResult.Description);
            
            if (healthResult.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy)
            {
                logger.LogError("❌ Database is unhealthy - application startup aborted");
                throw new InvalidOperationException("Database health check failed - cannot start application");
            }
        }
        
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Database initialization failed");
        throw; // Fail fast if database setup fails
    }
}

// Test database connection (keeping existing functionality)
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
           var leadCount = await context.Leads.CountAsync();
           var opportunityCount = await context.Opportunities.CountAsync();
           var gasStationCount = await context.GasStations.CountAsync();
           var stationTypeCount = await context.StationTypes.CountAsync();

           Console.WriteLine($"📊 Users in database: {userCount}");
           Console.WriteLine($"📊 Leads in database: {leadCount}");
           Console.WriteLine($"📊 Opportunities in database: {opportunityCount}");
           Console.WriteLine($"📊 Gas Stations in database: {gasStationCount}");
           Console.WriteLine($"📊 Station Types in database: {stationTypeCount}");
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

// 🆕 ADD HEALTH CHECK ENDPOINT
app.MapHealthChecks("/health");

// CRITICAL: CORS MUST COME FIRST
app.UseCors("AllowFrontend");

// CRITICAL: Correct middleware order
app.UseAuthentication(); // MUST come before UseAuthorization
app.UseAuthorization();

app.MapControllers();

Console.WriteLine("🚀 API Server starting...");
Console.WriteLine("📱 Swagger: http://localhost:5211/swagger");
Console.WriteLine("🔍 Health Check: http://localhost:5211/health");
Console.WriteLine("🌐 CORS: Enabled for all origins");
Console.WriteLine("🔐 JWT debugging enabled - All validations disabled for debugging");
Console.WriteLine("⛽ Gas Station Management: READY FOR TESTING");
Console.WriteLine("🌱 Database Seeding: FULLY AUTOMATED - NO MANUAL STEPS REQUIRED");

app.Run();