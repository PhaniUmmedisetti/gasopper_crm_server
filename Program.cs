using Microsoft.EntityFrameworkCore;
// using gasopper_crm_server.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// // Add DbContext for testing
// builder.Services.AddDbContext<TestDbContext>(options =>
//     options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// // Test database connection on startup
// try
// {
//     using (var scope = app.Services.CreateScope())
//     {
//         var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        
//         Console.WriteLine("Testing database connection...");
        
//         // Test connection by checking if we can connect
//         var canConnect = await context.Database.CanConnectAsync();
//         Console.WriteLine($"Can connect to database: {canConnect}");
        
//         if (canConnect)
//         {
//             // Try to read from roles table
//             var roles = await context.Roles.ToListAsync();
//             Console.WriteLine($"Successfully read {roles.Count} roles from database:");
            
//             foreach (var role in roles)
//             {
//                 Console.WriteLine($"  - Role ID: {role.role_id}, Name: {role.role_name}");
//             }
//         }
//     }
// }
// catch (Exception ex)
// {
//     Console.WriteLine($"Database connection failed: {ex.Message}");
//     Console.WriteLine($"Full error: {ex}");
// }

app.Run();