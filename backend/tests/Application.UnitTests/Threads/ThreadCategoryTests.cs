using Domain.Entities.Threads;
using Xunit;

namespace Application.UnitTests.Threads;

public sealed class ThreadCategoryTests
{
    [Fact]
    public void New_category_is_active_by_default()
    {
        var c = new ThreadCategory(Guid.NewGuid(), "housemate", "Housemate", "Find a room or flatmate", "home", 10);
        Assert.Equal("housemate", c.Slug);
        Assert.Equal("Housemate", c.Name);
        Assert.True(c.IsActive);
        Assert.Equal(10, c.SortOrder);
    }

    [Fact]
    public void Update_changes_display_fields_but_not_slug()
    {
        var c = new ThreadCategory(Guid.NewGuid(), "housemate", "Housemate", "old", "home", 10);
        c.Update("Housemates", "Find a room or flatmate", "house", 5, isActive: false);
        Assert.Equal("housemate", c.Slug);
        Assert.Equal("Housemates", c.Name);
        Assert.Equal("house", c.IconKey);
        Assert.Equal(5, c.SortOrder);
        Assert.False(c.IsActive);
    }
}
