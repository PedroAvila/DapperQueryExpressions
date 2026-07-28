# PAN.DapperLambdaToSql

**PAN.DapperLambdaToSql** es una librería ligera que extiende Dapper y Dapper.Contrib, permitiendo realizar operaciones genéricas como `UPDATE` y `EXIST` utilizando expresiones lambda (`Expression<Func<T, bool>>`), al estilo de Entity Framework.
> ⚠️ **Importante:** Esta librería está diseñada para usarse **exclusivamente** con **Dapper** y **Dapper.Contrib**.
> **Nota:** Esta librería no es compatible con Entity Framework ni con Entity Framework Core. Está enfocada en simplificar el uso de Dapper para operaciones comunes sin necesidad de escribir SQL manualmente.

## ✨ Características

- ✅ Actualización de entidades genéricas con `UpdateAsync`
- ✅ Verificación de existencia con `ExistAsync`
- ✅ Compatible con `Dapper` y `Dapper.Contrib`: respeta `[Table]`, `[Key]`, `[ExplicitKey]`, `[Write(false)]` y `[Computed]`
- ✅ Varias convenciones de clave primaria: `Id`, `ID` y `Clase+Id`
- ✅ Funciona con **SQL Server** y **MySQL / MariaDB**, delimitando los identificadores según el motor
- ✅ Sin boilerplate: elimina la necesidad de escribir SQL manual para cada entidad

## 💡 Instalación

```
dotnet add package PAN.DapperLambdaToSql
```


# 🚀 Uso

## 📌 Actualizar cualquier entidad con `UpdateAsync`

Este método permite actualizar cualquier entidad genérica sin escribir SQL manualmente. Solo necesitas asegurarte de que la entidad tenga una clave primaria reconocible —ver [Clave primaria: nombres soportados](#-clave-primaria-nombres-soportados)— y que las propiedades que deseas actualizar no sean `null`.

```csharp
// En tu servicio o repositorio genérico
public async Task<bool> UpdateAsync(T entity)
{
    return await _context.UpdateAsync(entity);
}
```

### 👉 Internamente genera y ejecuta dinámicamente una consulta SQL como esta:
```
UPDATE NombreTabla SET Columna1 = @Columna1, Columna2 = @Columna2 WHERE Id = @Id

```

## 🗄️ Motores soportados

El motor se detecta solo, a partir del tipo de conexión que le pasas. No hay nada que configurar:

| Motor | Se activa con | Identificadores |
|---|---|---|
| SQL Server | `SqlConnection` (`System.Data.SqlClient` o `Microsoft.Data.SqlClient`) | `[Nombre]` |
| MySQL / MariaDB | `MySqlConnection` (`MySql.Data` o `MySqlConnector`) | `` `Nombre` `` |
| Otros | cualquier otra conexión | sin delimitar |

```csharp
// La misma llamada, el SQL correcto para cada motor
using var cn = new SqlConnection(cs);    // UPDATE [Roles] SET [Key] = @Key WHERE [Id] = @Id
using var cn = new MySqlConnection(cs);  // UPDATE `Roles` SET `Key` = @Key WHERE `Id` = @Id

await cn.UpdateAsync(role);
```

Delimitar los identificadores permite usar columnas cuyo nombre es **palabra reservada** —`Key`, `Order`, `Group`, `Status`—, que sin comillas provocan un error de sintaxis.

Si usas un proveedor que envuelve la conexión real (perfilado, tracing) y por eso no se reconoce, puedes forzar el dialecto:

```csharp
DapperHelper.Dialect = SqlDialects.SqlServer;   // o SqlDialects.MySql
```

> ⚠️ Es una configuración **global**. Si tu aplicación habla con dos motores a la vez, déjala en `null` y confía en la detección automática.

**PostgreSQL no está soportado.** Postgres convierte a minúsculas todo identificador que no vaya entre comillas, así que la decisión de delimitar condiciona cómo debe crearse el esquema; se prefirió no adivinarla.

## 🔑 Clave primaria: nombres soportados

`UpdateAsync` resuelve la clave primaria recorriendo esta lista **en orden** y deteniéndose en la primera coincidencia:

| # | Qué se busca | Ejemplo |
|---|---|---|
| 1 | Propiedad con `[Key]` o `[ExplicitKey]` de Dapper.Contrib | `[ExplicitKey] public Guid Codigo { get; set; }` |
| 2 | Una propiedad llamada `Id`, coincidencia exacta | `public int Id { get; set; }` |
| 3 | Una propiedad llamada `Id` sin distinguir mayúsculas | `public int ID { get; set; }` |
| 4 | Una propiedad llamada `<NombreDeLaClase>Id` | `public int UserId { get; set; }` en la clase `User` |

Si ninguna coincide, se lanza una `InvalidOperationException` indicando la entidad y los nombres que se buscaron.

Los atributos **siempre** ganan a la convención, así que cualquier caso ambiguo se resuelve anotando la propiedad correcta con `[ExplicitKey]`.

### ⚠️ Las llaves foráneas nunca se confunden con la primaria

El paso 4 busca **un nombre concreto** —el de la clase más `Id`—, nunca "cualquier propiedad que termine en `Id`". Es una distinción importante, porque las llaves foráneas siguen exactamente ese mismo patrón de nombres:

```csharp
public class User
{
    public int Id { get; set; }        // ← clave primaria (resuelta en el paso 2)
    public int? GymId { get; set; }    // ← foránea: se actualiza como cualquier columna
    public int RoleId { get; set; }    // ← foránea: se actualiza como cualquier columna
}
```

En `User` el nombre buscado por el paso 4 sería `UserId`, así que ni `GymId` ni `RoleId` son candidatas. Y como la resolución se detiene en el paso 2, en este ejemplo el paso 4 ni siquiera llega a evaluarse.

Solo la clave primaria resuelta se excluye del `SET`. Todas las demás columnas, foráneas incluidas, se siguen actualizando con normalidad.

### La clave da nombre al `WHERE`

La columna del `WHERE` y el parámetro toman el nombre real de la propiedad resuelta:

```csharp
public class Producto
{
    [ExplicitKey] public string Sku { get; set; }
    public string Nombre { get; set; }
}
```

```sql
UPDATE Productos SET Nombre = @Nombre WHERE Sku = @Sku
```

## 📌 Actualizar cualquier entidad con UpdateAsync (actualización parcial)

Este método de extensión permite actualizar dinámicamente cualquier entidad genérica sin escribir SQL manualmente.
Solo actualizará las propiedades `no nulas` que no sean la clave primaria, por lo que es ideal para escenarios de actualización parcial (PATCH).

- ✅ Ventaja: No se sobreescriben columnas con `null`, `0` o `DateTime.MinValue` si no las envías.

### Ejemplo de entidad:

```csharp
public class Gym
{
    public int Id { get; set; }
    public int? Code { get; set; }       // Campo que no se actualiza si no se envía
    public string Name { get; set; }
    public string Address { get; set; }
    public string Phone { get; set; }
    public DateTime? CreatedAt { get; set; } // Tampoco se actualiza si no se envía
}
```

### Ejemplo de uso en un servicio o handler:

```csharp
var gym = new Gym
{
    Id = 1,
    Name = "New Name",
    Phone = "999-888-777"
    // No enviamos Code ni CreatedAt => no se actualizan
};

bool actualizado = await gymRepository.UpdateGymAsync(gym);

if (actualizado)
{
    Console.WriteLine("Actualización parcial exitosa 🚀");
}
else
{
    Console.WriteLine("No se actualizó ningún registro.");
}
```

### Ejemplo de uso en un repositorio

```csharp
public async Task<bool> UpdateGymAsync(Gym gym)
{
    return await _dbConnection.UpdateAsync(gym);
}
```

💡 **Notas:**

- La clave primaria se usa exclusivamente para el `WHERE` en el `UPDATE`; nunca se incluye en el `SET`.
- Las propiedades con `null` se ignoran y no se incluyen en la sentencia SQL.
- Las propiedades marcadas con `[Write(false)]` o `[Computed]` de Dapper.Contrib se excluyen del `UPDATE`. Es lo que necesitas para las propiedades de navegación que cargas con un `JOIN` y que no existen como columna en la tabla, y para las columnas cuyo valor genera la base de datos (columnas calculadas, `DEFAULT`, `rowversion`, triggers).
- Funciona con cualquier entidad cuya clave primaria siga alguna de las [convenciones soportadas](#-clave-primaria-nombres-soportados).


### 📌 Verificar existencia con ExistAsync
Este método permite consultar si existe una entidad que cumpla con una condición específica, usando expresiones lambda al estilo de Entity Framework.

```
// En tu servicio o repositorio genérico
public async Task<bool> ExistAsync(Expression<Func<T, bool>> predicate)
{
    return await _context.ExistAsync(predicate);
}
```

Ejemplo:
```
bool existe = await _context.ExistAsync<User>(x => x.Email == "test@example.com");
```






