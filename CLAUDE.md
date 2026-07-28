# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

`PAN.DapperLambdaToSql` — a single `netstandard2.0` class library published to NuGet. It adds two extension methods to `IDbConnection` (`UpdateAsync`, `ExistAsync`) that build SQL from entity reflection and lambda expressions, in the style of EF Core, on top of Dapper + Dapper.Contrib.

The library itself is four small files; tests live in [tests/PAN.DapperLambdaToSql.Tests/](tests/PAN.DapperLambdaToSql.Tests/). There is no CI.

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

Four files, layered:

1. [DapperExtensions.cs](DapperExtensions.cs) — the public surface. `UpdateAsync<T>` reflects over public instance properties and `ExistAsync<T>` translates a predicate; both resolve the table name through `DapperHelper` and execute via Dapper.
2. [DapperHelper.cs](DapperHelper.cs) — reflection/metadata layer: `GetTableName` (Dapper.Contrib `[Table]` attribute, else type name + `"s"`, cached by `RuntimeTypeHandle`), `GetKeyProperty`, the `IsWriteable`/`IsComputed` attribute checks, and `ToSql`, a thin facade that runs the visitor and returns `(Sql, DynamicParameters)`.
3. [SqlExpressionVisitor.cs](SqlExpressionVisitor.cs) — an `ExpressionVisitor` that turns `Expression<Func<T, bool>>` into a WHERE fragment plus parameters.
4. [SqlDialects.cs](SqlDialects.cs) — `ISqlDialect.Delimit`, the only thing that differs per engine. `SqlServerDialect` (`[x]`), `MySqlDialect` (`` `x` ``), `NoDelimiterDialect` (pre-multi-engine behavior, used for unrecognized providers).

### Dialect selection

`DapperHelper.GetDialect(connection)` switches on `connection.GetType().Name` — `sqlconnection` / `mysqlconnection` — the same trick Dapper.Contrib uses for its `ISqlAdapter`, and the only option available since a `netstandard2.0` library can't reference the provider packages. `DapperHelper.Dialect` force-overrides it (global static; leave `null` for multi-engine apps). Unknown providers fall back to **no delimiting** rather than Contrib's default-to-SQL-Server, because a misdetection on MySQL would emit `[Users]` and break what previously worked.

Columns are delimited, parameters never: `` `Key` = @Key `` is valid, `@`Key`` is not. Postgres is deliberately unimplemented — see [docs/adr/0004](docs/adr/0004-soporte-multi-motor-por-dialecto.md).

### Visitor semantics (the subtle part)

`VisitMember` unconditionally emits `node.Member.Name` as a **column name**. Captured variables and closures are therefore *not* handled by the normal visit path — the binary handlers explicitly call the private `VisitMemberAccess` for the right-hand operand, which compiles the sub-expression (`Expression.Lambda(node).Compile().DynamicInvoke()`) and funnels the result through `VisitConstant` as a parameter. Left side = column, right side = value is a hard structural assumption; reversing operands (`"x" == e.Name`) produces wrong SQL.

Parameters are named positionally as `@param0`, `@param1`, … and registered with the `@` already in the name.

Supported today: `==` and `&&` only. Everything else throws `NotSupportedException`. `||`, `!=`, comparisons, `Contains`, and nested/parenthesized grouping are unimplemented — adding them means extending `VisitBinary` and the `VisitAndAlsoBinary` chain, which currently only accepts an `Equal` node on its right side.

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
