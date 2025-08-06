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






