using System.Collections.Generic;
using DemoRepo;
using Xunit;

namespace DemoRepo.Tests;

public class InventoryReportTests
{
    [Fact]
    public void BuildReport_CountsLowStockItems()
    {
        var report = new InventoryReport();
        var items = new List<InventoryItem>
        {
            new() { Sku = "A", Category = "Tools", Quantity = 2, UnitPrice = 10m },
            new() { Sku = "B", Category = "Tools", Quantity = 8, UnitPrice = 5m },
            new() { Sku = "C", Category = "Parts", Quantity = 1, UnitPrice = 3m },
        };

        var text = report.BuildReport(items, out var totalLowStock);

        Assert.Equal(2, totalLowStock);
        Assert.Contains("Category: Tools", text);
        Assert.Contains("Category: Parts", text);
    }

    [Fact]
    public void BuildReport_SkipsNullItemsAndNullCategories()
    {
        var report = new InventoryReport();
        var items = new List<InventoryItem>
        {
            null!,
            new() { Sku = "X", Category = null, Quantity = 1, UnitPrice = 1m },
            new() { Sku = "Y", Category = "Office", Quantity = 9, UnitPrice = 2m },
        };

        var text = report.BuildReport(items, out var totalLowStock);

        Assert.Equal(0, totalLowStock);
        Assert.Contains("Category: Office", text);
    }
}
