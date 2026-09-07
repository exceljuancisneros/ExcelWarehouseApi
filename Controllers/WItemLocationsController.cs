using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ExcelWarehouseApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class WItemLocationsController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<WItemLocationsController> _logger;

    public WItemLocationsController(IConfiguration config, ILogger<WItemLocationsController> logger)
    {
        _config = config;
        _logger = logger;
    }

    [HttpPost("ExcelWarehouse_PrintLabel_SearchItems")]
    public IActionResult ExcelWarehouse_PrintLabel_SearchItems([FromBody] ItemSearchRequest request)
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
            var results = new List<dynamic>();
            
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            _logger.LogInformation("ExcelWarehouse_PrintLabel_SearchItems called for: {ItemCode}", request.ItemCode);

            // Remove TOP 1 to return ALL matching items
            // Use exact match for ItemNumber, LIKE for description and UPC
            var query = @"SELECT ItemNumber, ItemCodeDesc, Facility, Warehouse, Aisle, [Column], 
                                  Level, Arrow, Spot, Comment, Ver1, Ver2, Ver3, Ver4, Ver5, Ver6, Ver7 
                          FROM Find_Label_Items 
                          WHERE ItemNumber = @ExactCode 
                             OR ItemCodeDesc LIKE @LikeCode 
                             OR UDF_UPC LIKE @LikeCode";

            var trimmedCode = request.ItemCode.Trim();
            
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@ExactCode", trimmedCode);
            command.Parameters.AddWithValue("@LikeCode", "%" + trimmedCode + "%");

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new
                {
                    ItemNumber = reader["ItemNumber"]?.ToString() ?? "",
                    ItemCodeDesc = reader["ItemCodeDesc"]?.ToString() ?? "",
                    Facility = reader["Facility"]?.ToString() ?? "",
                    Warehouse = reader["Warehouse"]?.ToString() ?? "",
                    Aisle = reader["Aisle"]?.ToString() ?? "",
                    Column = reader["Column"]?.ToString() ?? "",
                    Level = reader["Level"]?.ToString() ?? "",
                    Arrow = reader["Arrow"]?.ToString() ?? "",
                    Spot = reader["Spot"]?.ToString() ?? "",
                    Comment = reader["Comment"]?.ToString() ?? "",
                    Ver1 = reader["Ver1"]?.ToString() ?? "",
                    Ver2 = reader["Ver2"]?.ToString() ?? "",
                    Ver3 = reader["Ver3"]?.ToString() ?? "",
                    Ver4 = reader["Ver4"]?.ToString() ?? "",
                    Ver5 = reader["Ver5"]?.ToString() ?? "",
                    Ver6 = reader["Ver6"]?.ToString() ?? "",
                    Ver7 = reader["Ver7"]?.ToString() ?? ""
                });
            }

            if (results.Count > 0)
            {
                _logger.LogInformation("ExcelWarehouse_PrintLabel_SearchItems found {Count} items for: {ItemCode}", results.Count, request.ItemCode);
                return Ok(new { success = true, count = results.Count, items = results });
            }

            _logger.LogInformation("ExcelWarehouse_PrintLabel_SearchItems - item not found for: {ItemCode}", request.ItemCode);
            return Ok(new { success = false, count = 0, items = new List<object>(), message = "Item not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExcelWarehouse_PrintLabel_SearchItems error for: {ItemCode}", request.ItemCode);
            return StatusCode(500, new { success = false, message = "Server error. Please try again." });
        }
    }
}

public class ItemSearchRequest
{
    public string ItemCode { get; set; } = string.Empty;
}
