using SportsPredictor.Domain.Common;
using Xunit;

namespace SportsPredictor.Domain.Tests;

file sealed class TestEntity : Entity
{
}

public class EntityTests
{
    [Fact]
    public void NewEntity_HasNonEmptyId()
    {
        var entity = new TestEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    [Fact]
    public void TwoEntities_HaveDifferentIds()
    {
        var first = new TestEntity();
        var second = new TestEntity();

        Assert.NotEqual(first.Id, second.Id);
    }
}
