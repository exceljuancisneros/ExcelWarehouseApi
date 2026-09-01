using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ExcelWarehouseApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration config, ILogger<AuthController> logger)
    {
        _config = config;
        _logger = logger;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrEmpty(request.UserName) || string.IsNullOrEmpty(request.Password))
            return BadRequest(new { success = false, message = "Username and password are required." });

        var connectionString = _config["SqlConnectionString"];
        var jwtSecret = _config["JwtSecret"];

        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogError("SqlConnectionString is null or empty");
            return StatusCode(500, new { success = false, message = "Server configuration error." });
        }

        if (string.IsNullOrEmpty(jwtSecret))
        {
            _logger.LogError("JwtSecret is null or empty");
            return StatusCode(500, new { success = false, message = "Server configuration error." });
        }

        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            _logger.LogInformation("SQL connection opened successfully");

            var query = "SELECT Id, UserName FROM _TempAppUsers WHERE UserName = @UserName AND [Password] = @Password AND IsEnabled = 1";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@UserName", request.UserName.Trim());
            command.Parameters.AddWithValue("@Password", request.Password.Trim());

            var result = command.ExecuteScalar();

            if (result == null)
            {
                _logger.LogInformation("Login failed - invalid credentials for user: {UserName}", request.UserName);
                return Ok(new { success = false, message = "Invalid username or password." });
            }

            _logger.LogInformation("Login successful for user: {UserName}", request.UserName);

            // Generate JWT Token
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(jwtSecret);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, request.UserName.Trim()),
                new Claim(ClaimTypes.Name, request.UserName.Trim())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(8),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return Ok(new
            {
                success = true,
                token = tokenString,
                message = "Login successful."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error for user: {UserName}", request.UserName);
            
            // Write error to file for debugging
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
                System.IO.File.AppendAllText(logPath, $"{DateTime.UtcNow:O} - Error: {ex.Message}\n\nStack: {ex.StackTrace}\n\n");
            }
            catch { }

            return StatusCode(500, new { success = false, message = "Server error. Please try again." });
        }
    }
}

public class LoginRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
