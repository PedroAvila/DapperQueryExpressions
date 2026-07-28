namespace PAN.DapperLambdaToSql.Tests;

public class SqlDialectTests
{
    [Theory]
    [InlineData("Users", "[Users]")]
    [InlineData("Key", "[Key]")]
    [InlineData("Order", "[Order]")]
    public void SqlServer_ShouldWrapInBrackets(string identifier, string expected)
        => Assert.Equal(expected, SqlDialects.SqlServer.Delimit(identifier));

    [Theory]
    [InlineData("Users", "`Users`")]
    [InlineData("Key", "`Key`")]
    [InlineData("Order", "`Order`")]
    public void MySql_ShouldWrapInBackticks(string identifier, string expected)
        => Assert.Equal(expected, SqlDialects.MySql.Delimit(identifier));

    [Fact]
    public void None_ShouldLeaveIdentifierUntouched()
        => Assert.Equal("Users", SqlDialects.None.Delimit("Users"));

    [Fact]
    public void SqlServer_WhenNameIsQualified_ShouldDelimitEachPart()
        => Assert.Equal("[dbo].[Users]", SqlDialects.SqlServer.Delimit("dbo.Users"));

    [Fact]
    public void MySql_WhenNameIsQualified_ShouldDelimitEachPart()
        => Assert.Equal("`dbo`.`Users`", SqlDialects.MySql.Delimit("dbo.Users"));

    [Fact]
    public void SqlServer_WhenNameIsAlreadyDelimited_ShouldNotDelimitTwice()
        => Assert.Equal("[dbo].[Users]", SqlDialects.SqlServer.Delimit("[dbo].[Users]"));

    [Fact]
    public void MySql_WhenNameIsAlreadyDelimited_ShouldNotDelimitTwice()
        => Assert.Equal("`Users`", SqlDialects.MySql.Delimit("`Users`"));

    [Fact]
    public void SqlServer_WhenNameContainsClosingDelimiter_ShouldEscapeIt()
        => Assert.Equal("[Weird]]Name]", SqlDialects.SqlServer.Delimit("Weird]Name"));

    [Fact]
    public void MySql_WhenNameContainsClosingDelimiter_ShouldEscapeIt()
        => Assert.Equal("`Weird``Name`", SqlDialects.MySql.Delimit("Weird`Name"));

    [Theory]
    [InlineData("")]
    public void Delimit_WhenIdentifierIsEmpty_ShouldReturnItUnchanged(string identifier)
    {
        Assert.Equal(identifier, SqlDialects.SqlServer.Delimit(identifier));
        Assert.Equal(identifier, SqlDialects.MySql.Delimit(identifier));
    }
}
