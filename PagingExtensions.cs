using System.Reflection;

namespace PAN.DapperLambdaToSql;

/// <summary>
/// Orden y paginado genéricos en memoria. Dapper no expone una capa IQueryable
/// diferida como EF, así que esto opera sobre una secuencia ya materializada
/// (por ejemplo, el resultado de <see cref="DapperExtensions.QueryAsync{T}"/>).
/// Ver docs/adr/0006.
/// </summary>
public static class PagingExtensions
{
    public static IEnumerable<T> OrderByProperty<T>(this IEnumerable<T> source, string propertyName, bool descending = false)
    {
        var property = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new ArgumentException($"'{typeof(T).Name}' no tiene una propiedad pública llamada '{propertyName}'.", nameof(propertyName));

        return descending
            ? source.OrderByDescending(item => property.GetValue(item))
            : source.OrderBy(item => property.GetValue(item));
    }

    public static PagedResult<T> ToPagedResult<T>(
        this IEnumerable<T> source,
        int page, int pageSize,
        string? orderBy = null, bool descending = false)
    {
        if (orderBy is { Length: > 0 })
            source = source.OrderByProperty(orderBy, descending);

        var materialized = source as IList<T> ?? source.ToList();
        var total = materialized.Count;
        var items = materialized.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<T>(items, page, pageSize, total);
    }
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
