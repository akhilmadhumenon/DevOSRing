using DemoRepo;
using Xunit;

namespace DemoRepo.Tests;

public class OrderProcessorTests
{
    private readonly OrderProcessor _processor = new();

    [Theory]
    [InlineData("gold", 1500, 5, true, 25)]
    [InlineData("gold", 1500, 5, false, 20)]
    [InlineData("gold", 1500, 2, true, 18)]
    [InlineData("gold", 1500, 2, false, 15)]
    [InlineData("gold", 500, 1, false, 10)]
    [InlineData("silver", 600, 3, false, 12)]
    [InlineData("silver", 100, 1, false, 5)]
    [InlineData("bronze", 300, 1, false, 3)]
    [InlineData("bronze", 100, 1, false, 0)]
    [InlineData("unknown", 999, 9, true, 0)]
    public void ApplyDiscount_ReturnsExpectedPercentage(
        string tier, decimal orderTotal, int itemCount, bool isReturning, int expected)
    {
        var result = _processor.ApplyDiscount(tier, orderTotal, itemCount, isReturning);
        Assert.Equal(expected, result);
    }
}
