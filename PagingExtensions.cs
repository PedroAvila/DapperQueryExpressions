using System.Reflection;

namespace PAN.DapperLambdaToSql;

/// <summary>
/// Orden y paginado genéricos en memoria. Dapper no expone una capa IQueryable
/// diferida como EF, así que esto opera sobre una secuencia ya materializada
/// (por ejemplo, el resultado de <see cref="DapperExtensions.QueryAsync{T}"/>).
/// Admite tanto una sola clave de orden (<c>OrderByProperty</c>) como varias con
/// desempate (<c>OrderByProperties</c>, vía <c>params (string, bool)[]</c>).
/// Ver docs/adr/0006.
/// </summary>
public static class PagingExtensions
{
    private static PropertyInfo ResolveProperty<T>(string propertyName)
        => typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new ArgumentException($"'{typeof(T).Name}' no tiene una propiedad pública llamada '{propertyName}'.", nameof(propertyName));

    public static IEnumerable<T> OrderByProperty<T>(this IEnumerable<T> source, string propertyName, bool descending = false)
        => source.OrderByProperties((propertyName, descending));

    /// <summary>
    /// Ordena por varias claves, aplicando cada una como desempate de la anterior
    /// -al estilo de un OrderBy(...).ThenBy(...).ThenBy(...) armado dinámicamente-.
    /// </summary>
    public static IEnumerable<T> OrderByProperties<T>(this IEnumerable<T> source, params (string Property, bool Descending)[] sortKeys)
    {
        if (sortKeys is not { Length: > 0 })
            throw new ArgumentException("Se requiere al menos una clave de orden.", nameof(sortKeys));

        var (firstProperty, firstDescending) = sortKeys[0];
        var first = ResolveProperty<T>(firstProperty);
        IOrderedEnumerable<T> ordered = firstDescending
            ? source.OrderByDescending(item => first.GetValue(item))
            : source.OrderBy(item => first.GetValue(item));

        for (int i = 1; i < sortKeys.Length; i++)
        {
            var (property, desc) = sortKeys[i];
            var resolved = ResolveProperty<T>(property);
            ordered = desc
                ? ordered.ThenByDescending(item => resolved.GetValue(item))
                : ordered.ThenBy(item => resolved.GetValue(item));
        }

        return ordered;
    }

    public static PagedResult<T> ToPagedResult<T>(
        this IEnumerable<T> source,
        int page, int pageSize,
        string? orderBy = null, bool descending = false)
    {
        if (orderBy is { Length: > 0 })
            source = source.OrderByProperty(orderBy, descending);

        return source.Paginate(page, pageSize);
    }

    /// <summary>
    /// Igual que el overload de una sola clave, pero acepta varias -desempatando
    /// en el orden dado-. A diferencia de <see cref="OrderByProperties{T}"/>, acá
    /// un array vacío es válido: significa "no pediste orden", no un mal uso.
    /// </summary>
    public static PagedResult<T> ToPagedResult<T>(
        this IEnumerable<T> source,
        int page, int pageSize,
        params (string Property, bool Descending)[] sortKeys)
    {
        if (sortKeys is { Length: > 0 })
            source = source.OrderByProperties(sortKeys);

        return source.Paginate(page, pageSize);
    }

    private static PagedResult<T> Paginate<T>(this IEnumerable<T> source, int page, int pageSize)
    {
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
