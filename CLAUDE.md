# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

GasopperCRM is a gas station management system built with .NET 7 and PostgreSQL. The system manages leads, opportunities, gas stations, and users with role-based access control.

## Development Commands

### Running the Application
```bash
dotnet run                    # Start development server (runs on http://localhost:5211)
dotnet build                  # Build the project
dotnet clean                  # Clean build artifacts
```

### Database Migrations
```bash
dotnet ef migrations add <MigrationName>          # Create new migration
dotnet ef database update                         # Apply migrations to database
dotnet ef migrations remove                       # Remove last migration
dotnet ef database drop                           # Drop database (requires confirmation)
```

### Testing API Endpoints
- Use [api-tests.http](api-tests.http) with REST Client or similar VS Code extensions
- Default test credentials in seeded database:
  - Admin: `phanisri444@gmail.com` / `Admin123!`
  - Test users follow pattern: `Role@123` for password

## Architecture

### Technology Stack
- **.NET 7** with Entity Framework Core 7
- **PostgreSQL** database with Npgsql provider
- **JWT Bearer authentication** with OTP-based login
- **BCrypt** for password hashing
- **AutoMapper** for DTO transformations
- **MailKit** for email services
- **Swagger/OpenAPI** for API documentation

### Project Structure

**Controllers/** - API endpoints following RESTful patterns
- Each controller uses dependency injection for services
- Authorization via `[Authorize]` attributes with role-based access

**Services/** - Business logic layer
- All services use interface-based dependency injection
- Pattern: `IServiceName` interface with `ServiceName` implementation
- Database seeding services in `Services/Database/`:
  - `SmartSeeder.cs` - Handles reference data seeding
  - `DataMigrationService.cs` - Manages data migrations
  - `DatabaseHealthService.cs` - Database health checks

**Models/** - Domain entities with EF Core annotations
- All entities inherit from `BaseEntity` or `SoftDeleteEntity`
- Snake_case column naming convention (`[Column("column_name")]`)
- Soft delete pattern: `is_deleted` boolean flag

**DTOs/** - Data Transfer Objects for API contracts
- Separate DTOs for requests and responses
- Located in `DTOs/` directory by feature

**Data/** - Database context and configurations
- `GasopperDbContext.cs` - Main EF Core context with fluent API configurations
- Automatic timestamp updates on `SaveChanges`

### Domain Model

**Core Entities:**
- **Users** - Role-based (Admin/Manager/Salesperson) with hierarchical manager relationships
- **Leads** - Potential customers with assignment and status tracking
- **Opportunities** - Converted leads with estimated gas station counts
- **GasStations** - Physical stations linked to opportunities with sign-off tracking

**Reference Data:**
- **Roles** - Admin, Manager, Salesperson
- **LeadStatuses** - New, Qualified, Converted
- **OpportunityStatuses** - Planning, Active, Complete
- **StationTypes** - Only Gas, Gas and Booth Sales, Gas and Convenience Store, Gas/Booth/Store combo

**Relationships:**
- Lead → Opportunity (one-to-one)
- Opportunity → GasStations (one-to-many)
- User → Manager (self-referencing)
- All entities track creator and assignment via User foreign keys

### Authentication Flow

The system uses **OTP-based passwordless authentication**:

1. **Send OTP** - POST `/api/Auth/send-otp` with email
2. **Verify OTP** - POST `/api/Auth/verify-otp` with email + code
3. **JWT Token** - Returned on successful OTP verification
4. **Token Usage** - Include as `Authorization: Bearer <token>` header

Legacy password authentication also supported via `/api/Auth/login`.

### Database Seeding

**Automatic on startup** - No manual seeding required:
- Program.cs runs migrations and seeding at application start
- SmartSeeder checks if data exists before inserting
- Creates default admin user if no users exist
- Health checks ensure database readiness before accepting requests

### Key Configuration

**appsettings.json** contains:
- `ConnectionStrings:DefaultConnection` - PostgreSQL connection
- `Jwt:Key/Issuer/Audience` - JWT token configuration
- `EmailSettings:*` - SMTP configuration for OTP emails
- `OtpSettings:*` - OTP expiry and rate limiting

**Important**: JWT validation is intentionally relaxed in development (see Program.cs:71-80) for debugging.

## Development Patterns

### Service Registration
All services must be registered in Program.cs:
```csharp
builder.Services.AddScoped<IServiceName, ServiceImplementation>();
```

### Entity Framework Conventions
- Use snake_case for all database identifiers
- Apply `[Column("name")]` attributes explicitly
- Soft delete pattern: set `is_deleted = true`, don't remove from DB
- UTC timestamps: `created_at`, `last_updated` automatically managed

### DTOs and AutoMapper
- Create separate DTOs for Create, Update, and Response operations
- Configure AutoMapper profiles for entity-DTO mappings
- Never expose entities directly in API responses

### Authorization
- Use `[Authorize]` for authenticated endpoints
- Access current user via `User.FindFirst(ClaimTypes.NameIdentifier)`
- Role-based authorization enforced at service layer

### API Response Patterns
Return consistent response objects:
```csharp
// Success
return Ok(new { success = true, data = result });

// Error
return BadRequest(new { success = false, message = "Error description" });
```

## Common Operations

### Adding a New Entity
1. Create model in `Models/` with proper attributes and navigation properties
2. Add `DbSet<Entity>` to GasopperDbContext
3. Configure in `OnModelCreating` with fluent API
4. Create migration: `dotnet ef migrations add Add<Entity>`
5. Apply migration: `dotnet ef database update`
6. Create DTOs in `DTOs/`
7. Create service interface and implementation in `Services/`
8. Register service in Program.cs
9. Create controller in `Controllers/`

### Working with Migrations
- Always review generated migrations before applying
- Use `dotnet ef migrations script` to generate SQL for review
- Never modify applied migrations - create new ones instead
- Database seeding runs automatically on startup

### API Documentation
- Swagger UI available at: http://localhost:5211/swagger
- Use "Authorize" button in Swagger to set JWT token
- Health check endpoint: http://localhost:5211/health

## Troubleshooting

### Database Connection Issues
- Verify PostgreSQL is running
- Check connection string in appsettings.json
- Review logs for EF Core migration errors

### JWT Authentication Issues
- Token validation is intentionally relaxed in development
- Check Program.cs JWT events for detailed logging
- Use `/api/Auth/debug-claims` endpoint to inspect token claims

### Email/OTP Issues
- Use `/api/Auth/debug-config` to verify email configuration
- Check SMTP credentials in appsettings.json
- Review EmailService logs for detailed error messages
