using DemoRepo;
using Xunit;

namespace DemoRepo.Tests;

public class UserAuthTests
{
    private readonly UserAuth _auth = new();

    [Fact]
    public void CheckUser_ReturnsTrue_ForNonEmptyName()
    {
        Assert.True(_auth.CheckUser("akhil"));
    }

    [Fact]
    public void CheckUser_ReturnsFalse_ForEmptyString()
    {
        Assert.False(_auth.CheckUser(""));
    }

    [Fact]
    public void CheckUser_ReturnsFalse_ForNull()
    {
        Assert.False(_auth.CheckUser(null!));
    }
}
