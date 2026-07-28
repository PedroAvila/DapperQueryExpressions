# ADR 0001 — Excluir del UPDATE las propiedades `[Write(false)]` y `[Computed]`

- **Estado:** Aceptado
- **Fecha:** 2026-07-28
- **Afecta a:** `DapperExtensions.UpdateAsync<T>`, `DapperHelper`

## Contexto

`UpdateAsync<T>` construía el `SET` recorriendo todas las propiedades públicas de
instancia de la entidad, con solo dos condiciones para incluirlas: que el valor no
fuera `null` y que la propiedad no se llamara `Id`.

Eso rompe en cuanto la entidad tiene propiedades que no corresponden a columnas de
la tabla. El caso que lo destapó fue una entidad `User` con propiedades de navegación
hidratadas por un JOIN:

```csharp
[Write(false)] public Role? Role { get; set; }
[Write(false)] public Gym?  Gym  { get; set; }
```

Vienen cargadas (no son `null`) y no se llaman `Id`, así que pasaban ambos filtros y
se emitían como `Role = @Role, Gym = @Gym`. La base de datos rechaza el statement
porque esas columnas no existen.

El síntoma era especialmente confuso porque el `INSERT` del mismo repositorio sí
funcionaba: ese camino llama a `InsertAsync` de Dapper.Contrib, que filtra las
propiedades con su método interno `IsWriteable`. La divergencia no estaba en la
entidad ni en el esquema, sino en que esta librería no replicaba ese filtro.

## Decisión

Replicar en `DapperHelper` los dos filtros que Dapper.Contrib aplica al construir un
`UPDATE`, y aplicarlos en el bucle de `UpdateAsync` antes de leer el valor:

- `IsWriteable(PropertyInfo)` — excluye solo cuando existe un `[Write(false)]`
  explícito. Es una transcripción del `IsWriteable` interno de
  `Dapper.Contrib.Extensions.SqlMapperExtensions`: en ausencia del atributo, o con
  `[Write(true)]`, la propiedad es escribible.
- `IsComputed(PropertyInfo)` — excluye las marcadas con `[Computed]`, atributo
  marcador que indica que el valor lo genera el motor (columnas calculadas,
  `DEFAULT`, `rowversion`, triggers).

Se mantuvieron como dos métodos separados, con los nombres de los atributos que
representan, en lugar de fundirlos en un único predicado. Son dos conceptos distintos
de Contrib y conviene que el código lo refleje aunque hoy el efecto sobre el `UPDATE`
sea el mismo.

Viven en `DapperHelper` y no en el propio bucle porque es la capa de metadatos y
reflexión de la librería, y porque ya importa `Dapper.Contrib.Extensions`
(`DapperExtensions.cs` no lo hace).

## Alternativas consideradas

**No implementar `[Computed]` y dejar que cada quien use `[Write(false)]`.** El efecto
sobre el `UPDATE` es idéntico, así que técnicamente resuelve el mismo problema. Se
descartó por compatibilidad: este paquete se publica en NuGet y su propuesta de valor
es comportarse como Dapper.Contrib pero con actualización parcial y lambdas. Obligar
a re-anotar entidades que ya están anotadas para Contrib contradice esa promesa, y el
modo de fallo sería exactamente el mismo error de SQL difícil de rastrear.

**Ignorar cualquier propiedad de tipo complejo en lugar de leer atributos.** Habría
resuelto el caso de las propiedades de navegación sin pedir anotaciones, pero es una
heurística: rompe con tipos complejos que sí son columnas legítimas y no cubre el caso
`[Computed]`, que suele ser de tipos primitivos.

## Consecuencias

Las entidades ya anotadas para Dapper.Contrib funcionan con `UpdateAsync` sin cambios.
Una propiedad `[Computed]` deja de escribirse, y se sigue leyendo con normalidad en los
`SELECT` — el atributo solo afecta la escritura.

Es un cambio de comportamiento: código existente que dependiera de que `UpdateAsync`
escribiera una propiedad `[Write(false)]` o `[Computed]` deja de hacerlo. Se considera
corrección de un defecto, no una regresión.

Quedan sin cubrir dos brechas de paridad con Contrib, que este ADR **no** resuelve:

- La clave primaria sigue amarrada al nombre literal `Id`. `[Key]` y `[ExplicitKey]`
  no se consultan, así que una entidad cuya llave se llame distinto falla en
  `GetProperty("Id")` con `NullReferenceException`.
- El descarte de propiedades `null` sigue impidiendo escribir `NULL` en una columna de
  forma deliberada. Es el diseño intencional de la actualización parcial (PATCH), no
  un defecto, pero conviene tenerlo presente al leer este ADR.
