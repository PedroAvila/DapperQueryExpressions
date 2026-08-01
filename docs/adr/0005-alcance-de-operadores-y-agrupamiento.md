# ADR 0005 — Alcance de operadores y agrupamiento en `SqlExpressionVisitor`

- **Estado:** Aceptado
- **Fecha:** 2026-08-01
- **Afecta a:** `SqlExpressionVisitor`, `DapperExtensions.QueryAsync<T>`
- **Relacionado:** [ADR 0004](0004-soporte-multi-motor-por-dialecto.md)

## Contexto

`SqlExpressionVisitor` solo traducía `==` y `&&`. Cualquier otro operador lanzaba
`NotSupportedException`, salvo `.Contains(...)`: al no existir un override de
`VisitMethodCall`, la clase base recorría el objeto y los argumentos de la llamada
como si fueran una columna y una constante sueltas, y emitía SQL incorrecto **sin
avisar**. Además, el lado derecho de un `&&` solo aceptaba otro `Equal` -código
duplicado casi idéntico entre `VisitBinary` y un `VisitEqualityBinary` privado-, así
que `a && b && c` funcionaba por casualidad de la recursión por izquierda, pero
`a && (b || c)` no tenía forma de expresarse.

El disparador fue agregar `QueryAsync<T>` (ver README) como equivalente más directo
al `Where()` de EF: para que sea útil como filtro real hace falta más que igualdad
encadenada con AND.

## Decisión

`VisitBinary` pasa a despachar por `NodeType` en vez de un `if/else` cerrado:

| `ExpressionType` | SQL |
|---|---|
| `Equal` | `=` |
| `NotEqual` | `<>` (no `!=`: es el operador ANSI, portable entre SQL Server y MySQL) |
| `LessThan` / `LessThanOrEqual` | `<` / `<=` |
| `GreaterThan` / `GreaterThanOrEqual` | `>` / `>=` |
| `AndAlso` | `AND` |
| `OrElse` | `OR` |

**Paréntesis mínimos.** Un operando solo se envuelve en `(...)` cuando es un nodo
`OrElse` colgando directamente de un `AndAlso` -en cualquiera de los dos lados, no
solo el derecho como antes-. `AND` liga más fuerte que `OR` tanto en C# como en SQL,
así que ese es el único caso donde el agrupamiento por defecto cambiaría el
significado (`a && (b || c)` sin paréntesis se leería `(a AND b) OR c`). `OrElse`
colgando de `AndAlso`, y mismo operador anidado en cualquier combinación, no
necesitan paréntesis: la precedencia y la asociatividad ya coinciden entre C# y SQL.

**`Contains` → `LIKE`.** `VisitMethodCall` reconoce específicamente
`string.Contains(string)` -un solo argumento, `DeclaringType == typeof(string)`- y
lo traduce a `columna LIKE @paramN` con el valor envuelto en `%...%` antes de
parametrizarlo. Cualquier otra llamada -incluido `Enumerable.Contains` sobre una
lista, o el overload de dos argumentos con `StringComparison`- lanza
`NotSupportedException` explícita en vez de mal-emitir SQL en silencio.

## Alternativas consideradas

**Paréntesis en todo nodo lógico, sin distinguir el caso.** Se descartó porque
cambiaría el SQL de los dos casos ya soportados y probados (`==`, `&&` simple),
que hoy se generan sin paréntesis.

**Traducción completa estilo LINQ-to-SQL** (`StartsWith`, `EndsWith`,
`Enumerable.Contains` → `IN (...)`, etc.). Deliberadamente fuera de alcance: el
objetivo es cubrir lo que un predicado típico contra esta librería necesita, no
reimplementar el traductor de EF Core.

## Consecuencias

- El valor de `Contains` no escapa `%` ni `_`; si el valor del usuario los trae,
  se interpretan como comodines de `LIKE`.
- `Contains` mantiene la misma asunción estructural que el resto del visitor:
  el lado izquierdo (`.Object`) es la columna, el argumento es el valor. Un
  `Contains` entre dos columnas de la entidad no está contemplado.
- Comparaciones sobre propiedades `Nullable<T>` (`int?`, etc.) no se probaron
  explícitamente; el árbol de expresión puede traer nodos `Convert` que el
  visitor no trata de forma especial. No es una regresión -antes tampoco eran
  usables en comparaciones, porque solo existía `==`- pero queda como límite
  conocido.
