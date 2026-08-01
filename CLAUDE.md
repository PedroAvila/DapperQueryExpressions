# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

`PAN.DapperLambdaToSql` — a single `netstandard2.0` class library published to NuGet. It adds extension methods to `IDbConnection` (`UpdateAsync`, `ExistAsync`, `QueryAsync`) that build SQL from entity reflection and lambda expressions, in the style of EF Core, on top of Dapper + Dapper.Contrib. `PagingExtensions` adds in-memory `OrderByProperty`/`ToPagedResult` for generic ordering/paging over an already-materialized `IEnumerable<T>` — Dapper has no deferred `IQueryable`-style layer, so that's resolved after the fact rather than translated to SQL.

The library itself is five small files; tests live in [tests/PAN.DapperLambdaToSql.Tests/](tests/PAN.DapperLambdaToSql.Tests/). There is no CI.

## Commands

```powershell
# The repo root holds both a .sln and a .csproj, so bare `dotnet build` fails
# with MSB1011 — always name the target explicitly.
dotnet build PAN.DapperLambdaToSql.csproj
dotnet build PAN.DapperLambdaToSql.csproj -c Release   # also emits the .nupkg (GeneratePackageOnBuild=True)
dotnet pack PAN.DapperLambdaToSql.csproj -c Release

dotnet test tests/PAN.DapperLambdaToSql.Tests/PAN.DapperLambdaToSql.Tests.csproj
dotnet test tests/PAN.DapperLambdaToSql.Tests/PAN.DapperLambdaToSql.Tests.csproj --filter "FullyQualifiedName~UpdateAsyncMySqlTests"
dotnet test tests/PAN.DapperLambdaToSql.Tests/PAN.DapperLambdaToSql.Tests.csproj --filter "DisplayName~ReservedWord"
```

The library `.csproj` sits at the repo root, so its default `**/*.cs` glob would swallow the test sources — it carries a `<Compile Remove="tests/**" />` that must stay.

Bumping `<Version>` in [PAN.DapperLambdaToSql.csproj](PAN.DapperLambdaToSql.csproj) is the release step — every build regenerates `bin/<config>/PAN.DapperLambdaSQL.<version>.nupkg`.

## Architecture

Five files, layered:

1. [DapperExtensions.cs](DapperExtensions.cs) — the public SQL-generating surface. `UpdateAsync<T>` reflects over public instance properties, `ExistAsync<T>` translates a predicate to check existence (`SELECT COUNT(*)`), and `QueryAsync<T>` translates a predicate to fetch matching rows (`SELECT *`); all three resolve the table name through `DapperHelper` and execute via Dapper.
2. [DapperHelper.cs](DapperHelper.cs) — reflection/metadata layer: `GetTableName` (Dapper.Contrib `[Table]` attribute, else type name + `"s"`, cached by `RuntimeTypeHandle`), `GetKeyProperty`, the `IsWriteable`/`IsComputed` attribute checks, and `ToSql`, a thin facade that runs the visitor and returns `(Sql, DynamicParameters)`.
3. [SqlExpressionVisitor.cs](SqlExpressionVisitor.cs) — an `ExpressionVisitor` that turns `Expression<Func<T, bool>>` into a WHERE fragment plus parameters.
4. [SqlDialects.cs](SqlDialects.cs) — `ISqlDialect.Delimit`, the only thing that differs per engine. `SqlServerDialect` (`[x]`), `MySqlDialect` (`` `x` ``), `NoDelimiterDialect` (pre-multi-engine behavior, used for unrecognized providers).
5. [PagingExtensions.cs](PagingExtensions.cs) — pure in-memory helpers, no SQL/`IDbConnection` involved: `OrderByProperty<T>` sorts an `IEnumerable<T>` by a property name resolved via reflection, `ToPagedResult<T>` applies that and slices into a `PagedResult<T>`. See [docs/adr/0006](docs/adr/0006-orden-y-paginado-en-memoria.md).

### Dialect selection

`DapperHelper.GetDialect(connection)` switches on `connection.GetType().Name` — `sqlconnection` / `mysqlconnection` — the same trick Dapper.Contrib uses for its `ISqlAdapter`, and the only option available since a `netstandard2.0` library can't reference the provider packages. `DapperHelper.Dialect` force-overrides it (global static; leave `null` for multi-engine apps). Unknown providers fall back to **no delimiting** rather than Contrib's default-to-SQL-Server, because a misdetection on MySQL would emit `[Users]` and break what previously worked.

Columns are delimited, parameters never: `` `Key` = @Key `` is valid, `@`Key`` is not. Postgres is deliberately unimplemented — see [docs/adr/0004](docs/adr/0004-soporte-multi-motor-por-dialecto.md).

### Visitor semantics (the subtle part)

`VisitMember` unconditionally emits `node.Member.Name` as a **column name**. Captured variables and closures are therefore *not* handled by the normal visit path — the binary handlers explicitly call the private `VisitMemberAccess` for the right-hand operand, which compiles the sub-expression (`Expression.Lambda(node).Compile().DynamicInvoke()`) and funnels the result through `VisitConstant` as a parameter. Left side = column, right side = value is a hard structural assumption; reversing operands (`"x" == e.Name`) produces wrong SQL.

Parameters are named positionally as `@param0`, `@param1`, … and registered with the `@` already in the name.

Supported: `==`, `!=` (→ `<>`, the ANSI form, not `!=`), `<`, `<=`, `>`, `>=`, `&&`, `||`, `string.Contains(...)` (→ `LIKE '%value%'`, no wildcard-escaping), and arbitrary nested/parenthesized AND/OR grouping. `VisitBinary` dispatches by `NodeType` to `VisitComparisonBinary` (comparisons) or `VisitLogicalBinary` (AND/OR); `VisitMethodCall` handles `string.Contains` and throws `NotSupportedException` for anything else (previously it silently mis-emitted SQL for any method call). Grouping uses *minimal parenthesization*: a sub-expression is only wrapped in parens when it's an `OrElse` node used as a direct operand of an `AndAlso` node — SQL's `AND`-before-`OR` precedence matches C#'s, so that's the only case where the default grouping would otherwise be wrong. See [docs/adr/0005](docs/adr/0005-alcance-de-operadores-y-agrupamiento.md).

### UpdateAsync conventions

The primary key is resolved by `DapperHelper.GetKeyProperty` in this order: `[Key]`/`[ExplicitKey]` → exact `Id` → case-insensitive `id` → `<ClassName>Id` → `InvalidOperationException` with a descriptive message. Step 4 looks up **one specific name** (`type.Name + "Id"`), never a "ends with Id" pattern — foreign keys (`GymId`, `RoleId`) and external identifiers (`TransactionId`) are syntactically identical to that convention and would otherwise become key candidates. The exclusion from SET compares the resolved `PropertyInfo` **instance**, not the name, so exactly one property is dropped and FK columns stay updatable. See [docs/adr/0002](docs/adr/0002-resolucion-de-la-clave-primaria.md).

`null`-valued properties are skipped, which is what makes it a partial/PATCH update; there is no way to null out a column through it. Composite keys are not supported — the WHERE assumes a single column.

Properties marked `[Write(false)]` or `[Computed]` are skipped via `DapperHelper.IsWriteable` / `IsComputed`, which mirror Dapper.Contrib. This matters for navigation properties hydrated by a JOIN: they are non-null and not named `Id`, so without that check they get emitted as SET columns and the UPDATE fails against columns that don't exist. See [docs/adr/0001](docs/adr/0001-excluir-propiedades-no-escribibles-del-update.md).

`GetTableName` caches into a static `ConcurrentDictionary` via `GetOrAdd`; the factory is pure, so the double-invocation `GetOrAdd` allows under contention is harmless. See [docs/adr/0003](docs/adr/0003-cache-de-nombres-de-tabla-thread-safe.md). `GetKeyProperty` intentionally has no cache — it reuses the `PropertyInfo[]` that `UpdateAsync` already fetched.

## Packaging constraints

Dapper and Dapper.Contrib are referenced with `<PrivateAssets>all</PrivateAssets>`, so they are **not** flowed to consumers — consuming projects must install both themselves. This is intentional and documented in the README; don't "fix" it by removing `PrivateAssets`.

Note the mismatch: assembly/namespace is `PAN.DapperLambdaToSql` but `<PackageId>` is `PAN.DapperLambdaSQL`.

Target is `netstandard2.0` — no `Task`-returning APIs beyond what's in the BCL there, and no C# runtime features requiring newer frameworks, though `LangVersion` is `latest` so modern syntax (file-scoped namespaces, etc.) is fine.

## Tests

xUnit + NSubstitute, matching the conventions of the consuming project (`Method_WhenCondition_ShouldOutcome`, constructor setup, no AAA comments). No database is involved: [Fakes/CapturingConnections.cs](tests/PAN.DapperLambdaToSql.Tests/Fakes/CapturingConnections.cs) defines `DbConnection` subclasses **named** `SqlConnection` / `MySqlConnection` / `NpgsqlConnection` — the type name is what drives dialect detection, so renaming those classes silently guts the engine coverage. Their command throws `SqlCapturedException` carrying the final `CommandText`, so assertions run against the SQL Dapper was about to send, not a reconstruction.

`UpdateAsyncSqlTests` / `ExistAsyncSqlTests` are abstract: cases are written once and each engine subclass supplies only its delimiter, so MySQL and SQL Server can never drift apart in coverage.

Parallelization is disabled assembly-wide ([AssemblyInfo.cs](tests/PAN.DapperLambdaToSql.Tests/AssemblyInfo.cs)) because `DapperHelper.Dialect` is global mutable state.

## Conventions

Code comments and the README are written in Spanish; match that when editing existing files.
