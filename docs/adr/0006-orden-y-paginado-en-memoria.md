# ADR 0006 — Orden y paginado genéricos, resueltos en memoria

- **Estado:** Aceptado
- **Fecha:** 2026-08-01
- **Afecta a:** `PagingExtensions` (nuevo)
- **Relacionado:** [ADR 0005](0005-alcance-de-operadores-y-agrupamiento.md)

## Contexto

Con `QueryAsync<T>` (ver README) ya se pueden traer filas filtradas, pero surgió
la necesidad de ordenar y paginar esos resultados "genéricamente" -por nombre de
columna recibido como string, sin un `switch` por entidad-, al estilo de un
`OrderBy(string)` dinámico.

EF Core puede resolver eso contra la base porque `IQueryable` es una capa diferida:
la expresión de orden se compone con el resto de la consulta y se traduce a SQL
recién al enumerar. Dapper no tiene un equivalente -`connection.QueryAsync<T>` ya
devuelve una lista materializada en memoria-, así que no hay forma de bajar un
"ordená por este nombre de columna" hasta el SQL sin reimplementar esa capa
diferida desde cero.

## Decisión

`OrderByProperty<T>(IEnumerable<T>, string propertyName, bool descending)` resuelve
la propiedad por reflexión (`Type.GetProperty`, case-insensitive) y ordena en
memoria con `Enumerable.OrderBy`/`OrderByDescending`, usando el valor devuelto por
`PropertyInfo.GetValue` como clave -válido porque `Comparer<object>.Default` cae en
`IComparable` cuando el valor concreto lo implementa (`string`, `int`, `DateTime`,
etc.)-. Alcance deliberadamente acotado a una propiedad simple: no resuelve rutas
anidadas tipo `"Cliente.Nombre"`.

Sin caché de `PropertyInfo` por `(Type, nombre)`. A diferencia de `GetTableName`
-cacheado porque se invoca en rutas calientes, ver [ADR 0003](0003-cache-de-nombres-de-tabla-thread-safe.md)-,
acá la resolución directa alcanza; no se justifica la abstracción extra todavía.

`ToPagedResult<T>(IEnumerable<T>, int page, int pageSize, string? orderBy, bool descending)`
aplica `OrderByProperty` si se pidió orden, materializa una sola vez y devuelve un
`PagedResult<T>` (`Items`, `Page`, `PageSize`, `TotalCount`, `TotalPages`).

## Alternativas consideradas

**`System.Linq.Dynamic.Core`.** Resuelve el mismo problema y de forma más completa
-rutas anidadas, múltiples claves de orden, `Where` dinámico por string-, pero se
descartó por dos razones: agrega una dependencia de runtime real, y a diferencia de
Dapper/Dapper.Contrib -que cualquier consumidor de esta librería ya tiene instalado
por definición, ver la sección de empaquetado del README- no es algo que un
consumidor típico de Dapper ya tenga; y su capacidad principal (anidamiento,
múltiples claves) excede lo pedido, que es ordenar por una sola propiedad simple.

## Consecuencias

- El orden y el paginado ocurren después de traer **todas** las filas que matchean
  el filtro a memoria. Para tablas grandes sin filtro selectivo, esto no reemplaza
  un `ORDER BY`/`OFFSET-FETCH` hecho en el motor.
- `OrderByProperty` lanza `ArgumentException` si el nombre de propiedad no existe
  en `T` -falla rápido en vez de devolver la secuencia sin ordenar-.
- `ToPagedResult` no valida `page`/`pageSize`; un `pageSize <= 0` produce una
  página vacía (o `TotalPages` en `0`) en vez de una excepción.

## Ampliación: orden multi-clave (2026-08-01)

El "alcance acotado a una propiedad simple" de la sección anterior excluía
desempate: no había forma de pedir "ordená por `RoleId` y, dentro de cada rol,
por `Name`". En la práctica esa necesidad apareció, así que se agregó
`OrderByProperties<T>(IEnumerable<T>, params (string Property, bool Descending)[] sortKeys)`,
que resuelve la primera clave con `OrderBy`/`OrderByDescending` y encadena el
resto con `ThenBy`/`ThenByDescending`. `OrderByProperty` (una sola clave) ahora
delega en este método para no duplicar la lógica de ordenamiento.

Se agregó también un overload de `ToPagedResult` con la misma firma
`params (string, bool)[] sortKeys`, en paralelo al overload existente de
`orderBy`/`descending` -que **no se tocó**-: mantener ambos evita romper a
quien ya llama a la firma vieja, y la resolución de overloads de C# no es
ambigua entre "parámetros con default" y "params" cuando no se pasa ninguna
clave.

A diferencia de `OrderByProperties` -que exige al menos una clave y lanza
`ArgumentException` con un array vacío, porque llamarlo sin claves es un mal
uso de un método que existe solo para ordenar-, el overload de `ToPagedResult`
trata un `sortKeys` vacío como "no pediste orden" y preserva el orden de
origen, igual que el overload viejo con `orderBy: null`.

Esto no cambia la alternativa descartada más arriba: seguir sin
`System.Linq.Dynamic.Core` sigue siendo la decisión correcta -el desempate
por nombre de propiedad no necesita rutas anidadas ni un motor de expresiones
completo, solo encadenar `ThenBy`-.

También se agregó `DapperExtensions.QueryPagedAsync<T>`, que envuelve
`QueryAsync<T>(predicate)` + `ToPagedResult(...)` en una sola llamada -pura
conveniencia para no repetir esos dos pasos en cada repositorio; no introduce
comportamiento nuevo más allá de lo ya descrito acá-.
