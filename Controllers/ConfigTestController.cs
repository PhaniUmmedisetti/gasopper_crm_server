using Microsoft.AspNetCore.Mvc;

namespace gasopper_crm_server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigTestController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ConfigTestController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("jwt-config")]
        public IActionResult TestJwtConfig()
        {
            var jwtKey = _configuration["Jwt:Key"];
            var jwtIssuer = _configuration["Jwt:Issuer"];
            var jwtAudience = _configuration["Jwt:Audience"];
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            return Ok(new
            {
                message = "Configuration Test",
                jwt = new
                {
                    keyConfigured = !string.IsNullOrEmpty(jwtKey),
                    keyLength = jwtKey?.Length ?? 0,
                    issuerConfigured = !string.IsNullOrEmpty(jwtIssuer),
                    audienceConfigured = !string.IsNullOrEmpty(jwtAudience),
                    issuer = jwtIssuer,
                    audience = jwtAudience
                },
                database = new
                {
                    connectionStringConfigured = !string.IsNullOrEmpty(connectionString),
                    connectionStringLength = connectionString?.Length ?? 0
                },
                timestamp = DateTime.UtcNow
            });
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "healthy",
                server = "GasopperCRM API",
                timestamp = DateTime.UtcNow,
                version = "1.0"
            });
        }
    }
}