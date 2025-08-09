# PAN.DapperLambdaToSql

**PAN.DapperLambdaToSql** es una librería ligera que extiende Dapper y Dapper.Contrib, permitiendo realizar operaciones genéricas como `UPDATE` y `EXIST` utilizando expresiones lambda (`Expression<Func<T, bool>>`), al estilo de Entity Framework.

## ✨ Características

- ✅ Actualización de entidades genéricas con `UpdateAsync`
- ✅ Verificación de existencia con `ExistAsync`
- ✅ Compatible con `Dapper` y `Dapper.Contrib`
- ✅ Sin boilerplate: elimina la necesidad de escribir SQL manual para cada entidad

## 💡 Instalación

```
dotnet add package PAN.DapperLambdaToSql
```


# 🚀 Uso

## 📌 Actualizar cualquier entidad con `UpdateAsync`

Este método permite actualizar cualquier entidad genérica sin escribir SQL manualmente. Solo necesitas asegurarte de que la entidad tenga una propiedad `Id` (clave primaria), y que las propiedades que deseas actualizar no sean `null`.

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

## 📌 Actualizar cualquier entidad con UpdateAsync (actualización parcial)

Este método de extensión permite actualizar dinámicamente cualquier entidad genérica sin escribir SQL manualmente.
Solo actualizará las propiedades `no nulas` y diferentes de `Id`, por lo que es ideal para escenarios de actualización parcial (PATCH).

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

- La propiedad `Id` se usa exclusivamente para el `WHERE` en el `UPDATE`.
- Las propiedades con `null` se ignoran y no se incluyen en la sentencia SQL.
- Funciona con cualquier tipo de entidad que tenga un `Id`.


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






