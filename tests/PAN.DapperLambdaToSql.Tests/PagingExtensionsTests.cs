namespace PAN.DapperLambdaToSql.Tests;

public class PagingExtensionsTests
{
    private static readonly List<User> Users =
    [
        new User { Id = 1, Name = "Carla", RoleId = 3 },
        new User { Id = 2, Name = "Ana", RoleId = 1 },
        new User { Id = 3, Name = "Beto", RoleId = 2 },
    ];

    [Fact]
    public void OrderByProperty_WhenPropertyExists_ShouldOrderAscending()
    {
        var ordered = Users.OrderByProperty(nameof(User.Name)).ToList();

        Assert.Equal(["Ana", "Beto", "Carla"], ordered.Select(u => u.Name));
    }

    [Fact]
    public void OrderByProperty_WhenDescendingIsTrue_ShouldOrderDescending()
    {
        var ordered = Users.OrderByProperty(nameof(User.RoleId), descending: true).ToList();

        Assert.Equal([3, 2, 1], ordered.Select(u => u.RoleId));
    }

    [Fact]
    public void OrderByProperty_WhenPropertyNameIsLowercase_ShouldStillResolveCaseInsensitively()
    {
        var ordered = Users.OrderByProperty("name").ToList();

        Assert.Equal(["Ana", "Beto", "Carla"], ordered.Select(u => u.Name));
    }

    [Fact]
    public void OrderByProperty_WhenPropertyDoesNotExist_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Users.OrderByProperty("Nonexistent").ToList());
    }

    [Fact]
    public void ToPagedResult_ShouldReturnTheRequestedPageAndTotalCount()
    {
        var result = Users.ToPagedResult(page: 2, pageSize: 2);

        Assert.Single(result.Items);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public void ToPagedResult_WhenOrderByIsProvided_ShouldOrderBeforePaging()
    {
        var result = Users.ToPagedResult(page: 1, pageSize: 2, orderBy: nameof(User.Name));

        Assert.Equal(["Ana", "Beto"], result.Items.Select(u => u.Name));
    }

    [Fact]
    public void ToPagedResult_WhenOrderByIsNull_ShouldPreserveSourceOrder()
    {
        var result = Users.ToPagedResult(page: 1, pageSize: 3);

        Assert.Equal(["Carla", "Ana", "Beto"], result.Items.Select(u => u.Name));
    }

    private static readonly List<User> UsersWithTies =
    [
        new User { Id = 1, Name = "Carla", RoleId = 2 },
        new User { Id = 2, Name = "Ana", RoleId = 1 },
        new User { Id = 3, Name = "Beto", RoleId = 2 },
        new User { Id = 4, Name = "Dana", RoleId = 1 },
    ];

    [Fact]
    public void OrderByProperties_WhenSecondKeyBreaksTies_ShouldOrderByBothKeys()
    {
        var ordered = UsersWithTies.OrderByProperties((nameof(User.RoleId), false), (nameof(User.Name), false)).ToList();

        Assert.Equal(["Ana", "Dana", "Beto", "Carla"], ordered.Select(u => u.Name));
    }

    [Fact]
    public void OrderByProperties_WhenKeysMixAscendingAndDescending_ShouldApplyEachDirection()
    {
        var ordered = UsersWithTies.OrderByProperties((nameof(User.RoleId), true), (nameof(User.Name), false)).ToList();

        Assert.Equal(["Beto", "Carla", "Ana", "Dana"], ordered.Select(u => u.Name));
    }

    [Fact]
    public void OrderByProperties_WhenSortKeysIsEmpty_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Users.OrderByProperties().ToList());
    }

    [Fact]
    public void OrderByProperties_WhenAPropertyDoesNotExist_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Users.OrderByProperties((nameof(User.RoleId), false), ("Nonexistent", false)).ToList());
    }

    [Fact]
    public void ToPagedResult_WithSortKeys_ShouldBreakTiesBeforePaging()
    {
        var result = UsersWithTies.ToPagedResult(page: 1, pageSize: 2, (nameof(User.RoleId), false), (nameof(User.Name), false));

        Assert.Equal(["Ana", "Dana"], result.Items.Select(u => u.Name));
        Assert.Equal(4, result.TotalCount);
    }

    [Fact]
    public void ToPagedResult_WithEmptySortKeys_ShouldPreserveSourceOrder()
    {
        var result = UsersWithTies.ToPagedResult(page: 1, pageSize: 4, Array.Empty<(string, bool)>());

        Assert.Equal(["Carla", "Ana", "Beto", "Dana"], result.Items.Select(u => u.Name));
    }
}
