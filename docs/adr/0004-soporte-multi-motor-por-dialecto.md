# ADR 0004 — Soporte multi-motor mediante dialectos de identificadores

- **Estado:** Aceptado
- **Fecha:** 2026-07-28
- **Afecta a:** `SqlDialects.cs` (nuevo), `DapperHelper`, `SqlExpressionVisitor`, `DapperExtensions`
- **Relacionado:** [ADR 0002](0002-resolucion-de-la-clave-primaria.md)

## Contexto

La librería se venía usando solo contra MySQL y emitía los identificadores **sin delimitar**:

```sql
UPDATE Roles SET Code = @Code, Name = @Name, Key = @Key WHERE Id = @Id
```

Eso arrastra un defecto que no depende del motor: `Key` es palabra reservada tanto en MySQL
como en SQL Server, así que ese `UPDATE` es un error de sintaxis. Cualquier entidad con una
columna llamada `Key`, `Order`, `Group`, `Status`, `Level` y demás está en la misma
situación. El proyecto de referencia tiene exactamente ese caso en su entidad `Role`.

Al querer usar la librería también contra SQL Server, el problema deja de ser una arista y
pasa a ser bloqueante: hay que delimitar, y cada motor lo hace distinto.

## Decisión

Introducir `ISqlDialect`, con un único método `Delimit(string)`, y seleccionarlo a partir
del tipo de la `IDbConnection` recibida —el mismo criterio que usa Dapper.Contrib para
elegir su `ISqlAdapter` interno—.

| Dialecto | Delimitador | Se activa con |
|---|---|---|
| `SqlServerDialect` | `[Nombre]` | conexión cuyo tipo se llama `SqlConnection` |
| `MySqlDialect` | `` `Nombre` `` | conexión cuyo tipo se llama `MySqlConnection` |
| `NoDelimiterDialect` | ninguno | cualquier otro proveedor |

La detección por nombre de tipo cubre las dos variantes vigentes de cada proveedor sin
tener que referenciarlas: `System.Data.SqlClient` y `Microsoft.Data.SqlClient` exponen ambas
un `SqlConnection`, y `MySql.Data` y `MySqlConnector` exponen ambas un `MySqlConnection`.
Una librería `netstandard2.0` no puede depender de esos paquetes, así que comparar nombres
es la única vía sin acoplarse a ninguno.

`DapperHelper.Dialect` permite forzar un dialecto y gana sobre la detección automática. Está
pensado para proveedores que envuelven la conexión real —perfilado, tracing— y por tanto no
se reconocen por su nombre de tipo.

Se delimita la columna pero **nunca** el parámetro: `` `Key` = @Key `` es válido,
`` `Key` = @`Key` `` no lo sería.

### PostgreSQL queda fuera a propósito

Se decidió no implementarlo. La razón no es el esfuerzo —serían diez líneas— sino que
Postgres pliega a minúsculas todo identificador sin comillas, de modo que la elección de
delimitar o no deja de ser cosmética y pasa a condicionar cómo debe crearse el esquema.
Tomar esa decisión sin un esquema real sería adivinar. La interfaz queda lista para añadir
un `PostgresDialect` con comillas dobles cuando haga falta.

### Respaldo sin delimitar para lo desconocido

Un proveedor no reconocido conserva el comportamiento previo en vez de recibir los
delimitadores de un motor cualquiera. Dapper.Contrib, en la misma situación, cae por defecto
en su adaptador de SQL Server; aquí se prefirió no hacerlo, porque un fallo de detección
sobre MySQL produciría `[Users]` —error de sintaxis— cuando el comportamiento anterior
funcionaba. Que lo desconocido siga comportándose como antes es más seguro que suponer.

## Alternativas consideradas

**Delimitar solo las palabras reservadas.** Obligaría a mantener la lista de reservadas de
cada motor —cientos de términos, distintas por versión— y a equivocarse en silencio cuando
faltara una. Delimitar siempre es más simple y no tiene contraindicación en estos dos
motores.

**Pedir el motor como parámetro explícito en cada llamada.** Rompería la firma de las
extensiones y obligaría al llamador a repetir en cada invocación algo que ya está implícito
en la conexión que entrega.

**Detectar el motor abriendo la conexión y consultando la versión.** Innecesariamente caro y
con efectos secundarios, para un dato que el tipo de la conexión ya expone.

## Consecuencias

El SQL generado cambia para todos los usuarios existentes: los identificadores pasan a ir
delimitados. En MySQL esto **no** es una regresión —los backticks son válidos siempre,
incluso con `ANSI_QUOTES` activo, y MySQL no altera mayúsculas ni con comillas ni sin
ellas— y además corrige las columnas con nombre reservado, que hasta ahora fallaban.

Se verificó capturando el `CommandText` real de `UpdateAsync` y `ExistAsync` contra
conexiones simuladas de cada tipo:

```sql
-- MySQL
UPDATE `Roles` SET `Code` = @Code, `GymId` = @GymId, `Name` = @Name, `Key` = @Key, `IsSystemRole` = @IsSystemRole WHERE `Id` = @Id
-- SQL Server
UPDATE [Roles] SET [Code] = @Code, [GymId] = @GymId, [Name] = @Name, [Key] = @Key, [IsSystemRole] = @IsSystemRole WHERE [Id] = @Id
-- ExistAsync
SELECT COUNT(*) FROM `Users` WHERE `Name` = @param0
```

Las llaves foráneas siguen en el `SET`, la resolución de la clave primaria no cambió y las
9 entidades del proyecto de referencia generan las mismas columnas que antes.

Detalles del delimitado que conviene conocer:

- Los nombres calificados se delimitan por partes: `dbo.Users` produce `[dbo].[Users]`.
  Como efecto, un `[Table("...")]` cuyo valor contenga un punto que **no** sea separador de
  esquema quedaría partido en dos.
- Un nombre que ya venga delimitado se respeta tal cual, para no producir `[[Users]]`. El
  reconocimiento es por el delimitador del dialecto activo: pasar `[dbo].[Users]` a MySQL
  produce `` `[dbo]`.`[Users]` ``, que es basura, pero mezclar la sintaxis de un motor con
  otro ya es un error del llamador.
- El delimitador de cierre dentro de un nombre se escapa duplicándolo: `Weird]Name` produce
  `[Weird]]Name]`.

`DapperHelper.Dialect` es estado estático global. Una aplicación que hable con dos motores a
la vez debe dejarlo en `null` y confiar en la detección por conexión; fijarlo forzaría el
mismo dialecto para ambos.

El prefijo `@` de parámetros se mantiene para los dos motores, y `COUNT(*)` se sigue leyendo
como `int` aunque MySQL lo devuelva como entero de 64 bits: Dapper hace la conversión.
