using System.Collections.Generic;
using System.Text;

namespace DemoRepo;

/// <summary>
/// Intentionally bad code for AI Refactor demos.
/// Smells: manual for-loops where LINQ would be cleaner, string concatenation in
/// a loop instead of <c>StringBuilder</c>/string interpolation, repeated null checks,
/// in-place mutation of a return list, "out" parameter that returns a single value,
/// no use of records / pattern matching.
///
/// Expected LLM refactor: LINQ <c>GroupBy</c>/<c>Sum</c>/<c>Where</c>, a
/// <c>StockSummary</c> record returned by value, string interpolation, switch
/// expression for the verdict, and a single readable expression-bodied method.
/// </summary>
public class InventoryReport
{
    public string BuildReport(List<InventoryItem> items, out int totalLowStock)
    {
        totalLowStock = 0;
        string output = "";

        // Group by category by hand
        Dictionary<string, List<InventoryItem>> byCategory = new Dictionary<string, List<InventoryItem>>();
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (it == null) continue;
            if (it.Category == null) continue;
            if (byCategory.ContainsKey(it.Category) == false)
            {
                byCategory[it.Category] = new List<InventoryItem>();
            }
            byCategory[it.Category].Add(it);
        }

        // Compute per-category totals via nested loops + string concat
        foreach (var category in byCategory.Keys)
        {
            int sumQty = 0;
            decimal sumValue = 0m;
            int lowStock = 0;
            for (int i = 0; i < byCategory[category].Count; i++)
            {
                var it = byCategory[category][i];
                sumQty = sumQty + it.Quantity;
                sumValue = sumValue + (it.Quantity * it.UnitPrice);
                if (it.Quantity < 5)
                {
                    lowStock = lowStock + 1;
                }
            }
            totalLowStock = totalLowStock + lowStock;

            string verdict;
            if (lowStock == 0)
            {
                verdict = "healthy";
            }
            else if (lowStock < 3)
            {
                verdict = "watch";
            }
            else
            {
                verdict = "reorder";
            }

            output = output + "Category: " + category + "\n";
            output = output + "  Qty: " + sumQty.ToString() + "\n";
            output = output + "  Value: $" + sumValue.ToString("F2") + "\n";
            output = output + "  Low-stock items: " + lowStock.ToString() + " (" + verdict + ")\n";
            output = output + "\n";
        }

        return output;
    }
}

public class InventoryItem
{
    public string? Sku { get; set; }
    public string? Category { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
