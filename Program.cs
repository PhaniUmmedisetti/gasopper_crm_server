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

// Add custom services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ILeadService, LeadService>();
// Add more services as you implement them

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey))
{
    throw new InvalidOperationException("JWT Key not configured in appsettings.json");
}

var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Set to true in production
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
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
        Description = "Gas Station Customer Relationship Management System"
    });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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

// Test database connection on startup
try
{
    Console.WriteLine("Testing GasopperCRM database connection...");
    
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<GasopperDbContext>();
        var canConnect = await context.Database.CanConnectAsync();
        Console.WriteLine($"✅ Database connection: {canConnect}");

        if (canConnect)
        {
            // Test basic queries
            var roleCount = await context.Roles.CountAsync();
            var userCount = await context.Users.CountAsync();
            var leadCount = await context.Leads.CountAsync();
            var opportunityCount = await context.Opportunities.CountAsync();
            var stationCount = await context.GasStations.CountAsync();
            
            Console.WriteLine($"📊 Database verification:");
            Console.WriteLine($"   Roles: {roleCount}");
            Console.WriteLine($"   Users: {userCount}");
            Console.WriteLine($"   Leads: {leadCount}");
            Console.WriteLine($"   Opportunities: {opportunityCount}");
            Console.WriteLine($"   Gas Stations: {stationCount}");

            // Test admin user exists
            var adminUser = await context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.role_id == 1);
            
            if (adminUser != null)
            {
                Console.WriteLine($"👤 Admin User: {adminUser.first_name} {adminUser.last_name} ({adminUser.Role?.role_name})");
            }
            
            Console.WriteLine("✅ GasopperCRM database connection successful!");
        }
        else
        {
            Console.WriteLine("❌ Database connection failed!");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Database connection error: {ex.Message}");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "GasopperCRM API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseAuthentication(); // Add this BEFORE UseAuthorization
app.UseAuthorization();

app.MapControllers();

Console.WriteLine("🚀 GasopperCRM API Server starting...");
Console.WriteLine("📱 Swagger UI available at: http://localhost:5211/swagger");
Console.WriteLine("🔐 JWT Authentication configured");
Console.WriteLine("✅ API Server ready!");

app.Run();