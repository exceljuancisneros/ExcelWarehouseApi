using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ExcelWarehouseApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ItemController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<ItemController> _logger;

    public ItemController(IConfiguration config, ILogger<ItemController> logger)
    {
        _config = config;
        _logger = logger;
    }

    [HttpPost("search")]
    public IActionResult SearchItem([FromBody] ItemSearchRequest request)
    {
        if (string.IsNullOrEmpty(request.ItemCode))
            return BadRequest(new { success = false, message = "Item code is required." });

        var connectionString = _config["SqlConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogError("SqlConnectionString is null or empty");
            return StatusCode(500, new { success = false, message = "Server configuration error." });
        }

        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            _logger.LogInformation("SQL connection opened for item search: {ItemCode}", request.ItemCode);

            var query = @"SELECT TOP 1 * FROM Find_Label_Items 
                          WHERE ItemNumber = @ItemCode 
                             OR ItemCodeDesc = @ItemCode 
                             OR UDF_UPC = @ItemCode";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@ItemCode", request.ItemCode.Trim());

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var item = new
                {
                    success = true,
                    itemNumber = reader["ItemNumber"]?.ToString() ?? "",
                    itemCodeDesc = reader["ItemCodeDesc"]?.ToString() ?? "",
                    facility = reader["Facility"]?.ToString() ?? "",
                    warehouse = reader["Warehouse"]?.ToString() ?? "",
                    aisle = reader["Aisle"]?.ToString() ?? "",
                    column = reader["Column"]?.ToString() ?? "",
                    level = reader["Level"]?.ToString() ?? "",
                    arrow = reader["Arrow"]?.ToString() ?? "",
                    spot = reader["Spot"]?.ToString() ?? "",
                    comment = reader["Comment"]?.ToString() ?? "",
                    ver1 = reader["Ver1"]?.ToString() ?? "",
                    ver2 = reader["Ver2"]?.ToString() ?? "",
                    ver3 = reader["Ver3"]?.ToString() ?? "",
                    ver4 = reader["Ver4"]?.ToString() ?? "",
                    ver5 = reader["Ver5"]?.ToString() ?? "",
                    ver6 = reader["Ver6"]?.ToString() ?? "",
                    ver7 = reader["Ver7"]?.ToString() ?? ""
                };

                _logger.LogInformation("Item found: {ItemNumber}", item.itemNumber);
                return Ok(item);
            }

            _logger.LogInformation("Item not found for code: {ItemCode}", request.ItemCode);
            return Ok(new { success = false, message = "Item not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for item: {ItemCode}", request.ItemCode);
            return StatusCode(500, new { success = false, message = "Server error. Please try again." });
        }
    }
}

public class ItemSearchRequest
{
    public string ItemCode { get; set; } = string.Empty;
}
