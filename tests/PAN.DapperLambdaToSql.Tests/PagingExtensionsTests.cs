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
}
