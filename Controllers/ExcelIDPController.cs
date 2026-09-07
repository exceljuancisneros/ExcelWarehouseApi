using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ExcelWarehouseApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ExcelIDPController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<ExcelIDPController> _logger;

    public ExcelIDPController(IConfiguration config, ILogger<ExcelIDPController> logger)
    {
        _config = config;
        _logger = logger;
    }

    [HttpPost("VerifyUserCredentials")]
    public IActionResult VerifyUserCredentials([FromBody] LoginRequest request)
    {
        if (string.IsNullOrEmpty(request.UserName) || string.IsNullOrEmpty(request.Password))
            return BadRequest(new { success = false, message = "Username and password are required." });

        var authConnectionString = _config["AuthConnectionString"];
        var jwtSecret = _config["JwtSecret"];

        if (string.IsNullOrEmpty(authConnectionString))
        {
            _logger.LogError("ExcelIDPController::VerifyUserCredentials - AuthConnectionString is null or empty");
            return StatusCode(500, new { success = false, message = "Server configuration error." });
        }

        if (string.IsNullOrEmpty(jwtSecret))
        {
            _logger.LogError("ExcelIDPController::VerifyUserCredentials - JwtSecret is null or empty");
            return StatusCode(500, new { success = false, message = "Server configuration error." });
        }

        try
        {
            using var connection = new SqlConnection(authConnectionString);
            connection.Open();
            _logger.LogInformation("SQL connection opened successfully");

            var query = "SELECT Id, UserName FROM _TempAppUsers WHERE UserName = @UserName AND [Password] = @Password AND IsEnabled = 1";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@UserName", request.UserName.Trim());
            command.Parameters.AddWithValue("@Password", request.Password.Trim());

            var result = command.ExecuteScalar();

            if (result == null)
            {
                _logger.LogInformation("ExcelIDPController::VerifyUserCredentials - Login failed for user: {UserName}", request.UserName);
                return Ok(new { success = false, message = "Invalid username or password." });
            }

            _logger.LogInformation("ExcelIDPController::VerifyUserCredentials - Login successful for user: {UserName}", request.UserName);

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
            _logger.LogError(ex, "ExcelIDPController::VerifyUserCredentials - Error for user: {UserName}", request.UserName);
            
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

    [HttpPost("GetUserPermissions")]
    public IActionResult GetUserPermissions([FromBody] GetUserPermissionsRequest request)
    {
        if (string.IsNullOrEmpty(request.UserId))
            return BadRequest(new { success = false, message = "UserId is required." });

        var authConnectionString = _config["AuthConnectionString"];
        if (string.IsNullOrEmpty(authConnectionString))
        {
            _logger.LogError("ExcelIDPController::GetUserPermissions - AuthConnectionString is null or empty");
            return StatusCode(500, new { success = false, message = "Server configuration error." });
        }

        try
        {
            using var connection = new SqlConnection(authConnectionString);
            connection.Open();

            // First, get the user's integer ID from _TempAppUsers
            var getUserIdQuery = "SELECT Id FROM _TempAppUsers WHERE UserName = @UserName AND IsEnabled = 1";

            using (var getUserCommand = new SqlCommand(getUserIdQuery, connection))
            {
                getUserCommand.Parameters.AddWithValue("@UserName", request.UserId.Trim());

                var userId = getUserCommand.ExecuteScalar();

                if (userId == null)
                {
                    _logger.LogInformation("ExcelIDPController::GetUserPermissions - User not found: {UserId}", request.UserId);
                    return Ok(new { success = false, message = "User not found.", count = 0, permissions = new List<object>() });
                }

                int parsedUserId = Convert.ToInt32(userId);

                // Now get permissions using the integer user ID
                var query = @"SELECT AP.PermissionName, UP.[Value]
                              FROM [ExcelIDP].[dbo].[User_Permissions] AS UP
                              JOIN [ExcelIDP].[dbo].[App_Permissions] AS AP ON UP.[PermissionId] = AP.Id
                              WHERE UP.[UserId] = @UserId";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", parsedUserId);

                    using var reader = command.ExecuteReader();
                    var permissions = new List<dynamic>();

                    while (reader.Read())
                    {
                        permissions.Add(new
                        {
                            permissionName = reader["PermissionName"]?.ToString() ?? "",
                            value = reader["Value"]?.ToString() ?? ""
                        });
                    }

                    _logger.LogInformation("ExcelIDPController::GetUserPermissions - Found {Count} permissions for user: {UserId}", permissions.Count, request.UserId);

                    return Ok(new { success = true, count = permissions.Count, permissions = permissions });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExcelIDPController::GetUserPermissions - Error for user: {UserId}", request.UserId);
            return StatusCode(500, new { success = false, message = "Server error. Please try again." });
        }
    }
}

public class LoginRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class GetUserPermissionsRequest
{
    public string UserId { get; set; } = string.Empty;
}
