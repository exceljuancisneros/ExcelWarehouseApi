using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

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
            var results = new List<dynamic>();
            
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            _logger.LogInformation("SQL connection opened for item search: {ItemCode}", request.ItemCode);

            // Remove TOP 1 to return ALL matching items
            var query = @"SELECT ItemNumber, ItemCodeDesc, Facility, Warehouse, Aisle, [Column], 
                                  Level, Arrow, Spot, Comment, Ver1, Ver2, Ver3, Ver4, Ver5, Ver6, Ver7 
                          FROM Find_Label_Items 
                          WHERE ItemNumber = @ItemCode 
                             OR ItemCodeDesc LIKE @ItemCode 
                             OR UDF_UPC LIKE @ItemCode";

            // Add wildcards for LIKE queries
            var itemCodePattern = "%" + request.ItemCode.Trim() + "%";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@ItemCode", itemCodePattern);

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
                _logger.LogInformation("Found {Count} items for code: {ItemCode}", results.Count, request.ItemCode);
                return Ok(new { success = true, count = results.Count, items = results });
            }

            _logger.LogInformation("Item not found for code: {ItemCode}", request.ItemCode);
            return Ok(new { success = false, count = 0, items = new List<object>(), message = "Item not found." });
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
