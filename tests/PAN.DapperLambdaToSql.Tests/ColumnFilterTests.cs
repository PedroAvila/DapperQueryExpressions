using System.Reflection;

namespace PAN.DapperLambdaToSql.Tests;

public class ColumnFilterTests
{
    private static PropertyInfo Property<T>(string name) => typeof(T).GetProperty(name)!;

    [Fact]
    public void IsWriteable_WhenPropertyHasNoAttribute_ShouldReturnTrue()
        => Assert.True(DapperHelper.IsWriteable(Property<User>(nameof(User.Name))));

    [Fact]
    public void IsWriteable_WhenPropertyIsMarkedWriteFalse_ShouldReturnFalse()
    {
        Assert.False(DapperHelper.IsWriteable(Property<User>(nameof(User.Role))));
        Assert.False(DapperHelper.IsWriteable(Property<User>(nameof(User.Gym))));
    }

    [Fact]
    public void IsWriteable_WhenPropertyIsAForeignKey_ShouldReturnTrue()
    {
        // GymId y RoleId son columnas reales: deben poder actualizarse.
        Assert.True(DapperHelper.IsWriteable(Property<User>(nameof(User.GymId))));
        Assert.True(DapperHelper.IsWriteable(Property<User>(nameof(User.RoleId))));
    }

    [Fact]
    public void IsComputed_WhenPropertyIsMarkedComputed_ShouldReturnTrue()
        => Assert.True(DapperHelper.IsComputed(Property<Employee>(nameof(Employee.FullName))));

    [Fact]
    public void IsComputed_WhenPropertyIsOrdinary_ShouldReturnFalse()
        => Assert.False(DapperHelper.IsComputed(Property<Employee>(nameof(Employee.FirstName))));
}

public class TableNameTests
{
    [Fact]
    public void GetTableName_WhenThereIsNoAttribute_ShouldPluralizeTheTypeName()
    {
        Assert.Equal("Users", DapperHelper.GetTableName(typeof(User)));
        Assert.Equal("Roles", DapperHelper.GetTableName(typeof(Role)));
    }

    [Fact]
    public void GetTableName_WhenTableAttributeIsPresent_ShouldUseIt()
        => Assert.Equal("Gimnasios", DapperHelper.GetTableName(typeof(BrandedGym)));

    [Fact]
    public void GetTableName_WhenCalledTwice_ShouldReturnTheCachedValue()
    {
        var first = DapperHelper.GetTableName(typeof(Role));
        var second = DapperHelper.GetTableName(typeof(Role));

        Assert.Equal(first, second);
    }

    [Fact]
    public void GetTableName_WhenResolvedConcurrentlyOnAColdCache_ShouldStayConsistent()
    {
        // La caché es estática y se escribe desde cualquier hilo que resuelva un tipo
        // por primera vez. Con un Dictionary sin sincronizar esto podía corromperla.
        var types = typeof(string).Assembly.GetTypes().Where(t => t.IsPublic).Take(300).ToList();
        var results = new System.Collections.Concurrent.ConcurrentDictionary<Type, System.Collections.Concurrent.ConcurrentBag<string>>();

        var work = Enumerable.Range(0, 40).SelectMany(_ => types);

        Parallel.ForEach(work, new ParallelOptions { MaxDegreeOfParallelism = 32 }, type =>
            results.GetOrAdd(type, _ => []).Add(DapperHelper.GetTableName(type)));

        Assert.All(results, entry => Assert.Single(entry.Value.Distinct()));
        Assert.DoesNotContain(results, entry => entry.Value.Any(string.IsNullOrEmpty));
    }
}
