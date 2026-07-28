# ADR 0002 — Resolución de la clave primaria por atributo y convención

- **Estado:** Aceptado
- **Fecha:** 2026-07-28
- **Afecta a:** `DapperExtensions.UpdateAsync<T>`, `DapperHelper.GetKeyProperty`
- **Relacionado:** [ADR 0001](0001-excluir-propiedades-no-escribibles-del-update.md)

## Contexto

`UpdateAsync<T>` tenía la clave primaria amarrada al literal `"Id"` en tres puntos que
además no estaban de acuerdo entre sí:

- la exclusión del `SET` comparaba con `OrdinalIgnoreCase`;
- la lectura del valor usaba `entityType.GetProperty("Id")`, que **sí** distingue
  mayúsculas;
- el `WHERE Id = @Id` iba escrito dentro del string SQL.

De ahí salían tres defectos. Una entidad con `public int ID` quedaba correctamente fuera
del `SET` pero reventaba con `NullReferenceException` al leer el valor, porque
`GetProperty("Id")` devolvía `null`. Una entidad con la llave bajo otro nombre fallaba
igual, y antes de fallar ya había metido su propia llave en el `SET`. Y en ambos casos el
error era un `NullReferenceException` pelado, sin mensaje, apuntando a una línea de esta
librería —el mismo problema de diagnóstico que motivó el ADR 0001—.

Dapper.Contrib resuelve la llave por `[Key]`/`[ExplicitKey]` con respaldo en una
convención `id` insensible a mayúsculas. La convención de este proyecto es más amplia:
`Id`, `ID` y `Clase+Id`.

## Decisión

Un `DapperHelper.GetKeyProperty(Type, PropertyInfo[])` con este orden de resolución:

1. propiedad con `[Key]` o `[ExplicitKey]`;
2. propiedad llamada `Id`, comparación exacta;
3. propiedad llamada `Id` sin distinguir mayúsculas — cubre `ID`, `iD`;
4. propiedad llamada `<NombreDeClase>Id` sin distinguir mayúsculas;
5. si nada coincide, `InvalidOperationException` con un mensaje que nombra la entidad y
   los nombres que se buscaron.

El paso 2 va antes que el 3 y el 4 deliberadamente: garantiza que toda entidad con un `Id`
convencional se resuelva por el camino corto y produzca exactamente el mismo SQL que antes.
Los atributos ganan siempre a la convención, de modo que cualquier caso ambiguo se puede
desambiguar anotando la propiedad.

En `UpdateAsync`, el `WHERE` y el nombre del parámetro se construyen con el `Name` de la
propiedad resuelta, y la exclusión del `SET` compara **la instancia** de `PropertyInfo`
—no el nombre—, de forma que se saque exactamente una propiedad y ni una más.

`GetKeyProperty` recibe el arreglo de propiedades que `UpdateAsync` ya obtuvo, en lugar de
volver a llamar a `GetProperties()`. Así se evita una segunda pasada de reflexión sin
introducir una caché, que en esta clase habría que diseñar con cuidado (ver más abajo).

### Por qué la convención busca un nombre y no un patrón

El paso 4 compara contra la cadena concreta `type.Name + "Id"`. **Nunca** contra un patrón
del estilo "cualquier propiedad que termine en `Id`". La diferencia es crítica porque las
llaves foráneas son sintácticamente idénticas a esa convención: en el dominio de referencia,
`User` tiene `GymId` y `RoleId`, `Membership` tiene `CustomerId`, `GymId` y `CatalogueId`.
Con un patrón, cualquiera de ellas sería candidata a clave primaria; buscando el nombre
exacto `UserId` o `MembershipId`, ninguna coincide.

El caso que mejor ilustra el riesgo es `Payment.TransactionId`, un `string?` que guarda el
identificador de la pasarela de pago: bajo un patrón laxo podría terminar siendo la clave
primaria de la tabla.

Una entidad sin `Id` y sin `<Clase>Id` lanza excepción en vez de elegir una foránea.
Fallar es el comportamiento correcto ahí; adivinar sería el defecto.

## Alternativas consideradas

**Solo replicar Contrib (`[Key]`/`[ExplicitKey]` + `id` insensible a mayúsculas).** Habría
corregido los `NullReferenceException` con menos código, pero deja fuera `Clase+Id`, que es
convención establecida de este proyecto y aparece en esquemas donde la columna llave se
llama como la tabla.

**Aceptar cualquier propiedad terminada en `Id`.** Descartada por lo explicado arriba:
convierte toda llave foránea en candidata a primaria, con fallos silenciosos.

**Soportar llaves compuestas.** Queda fuera de alcance; obligaría a rediseñar la
construcción del `WHERE`, que hoy asume una sola columna.

## Consecuencias

Se verificó empíricamente contra las 9 entidades del proyecto de referencia (`Catalogue`,
`Customer`, `Frequency`, `Gym`, `Membership`, `Package`, `Payment`, `Role`, `User`),
comparando la lista de columnas del `SET` y la cláusula `WHERE` que produce la regla
anterior contra la nueva: **las 9 resuelven su llave por el paso 2 y generan SQL idéntico**.
Las foráneas siguen en el `SET` —`User` mantiene `GymId` y `RoleId`, y por tanto se puede
seguir moviendo un usuario de gimnasio—, y `TransactionId` sigue siendo una columna común
de `Payment`.

Cambios de comportamiento respecto de la versión anterior:

- Una entidad sin llave resoluble lanza `InvalidOperationException` con mensaje en lugar de
  `NullReferenceException`. También cambia el momento: ahora falla al entrar al método, no
  después de recorrer las propiedades.
- Si una entidad tuviera `Id` y `ID` a la vez —legal en C#, aunque no conforme a CLS—, antes
  ambas quedaban fuera del `SET` y ahora solo queda fuera la resuelta. Se acepta el cambio;
  es un modelado patológico.
- Una entidad con `Id` **y además** un `[Key]` sobre otra propiedad ahora se resuelve por el
  atributo, no por `Id`. Es el resultado deseado: lo explícito gana.

Brechas conocidas que este ADR no cierra:

- Llaves compuestas siguen sin soportarse.
- La caché de `GetTableName` sigue siendo un `Dictionary` estático sin sincronización, y
  escribir en él desde varios hilos puede corromperlo. `GetKeyProperty` deliberadamente no
  agrega una segunda caché con el mismo defecto; si en algún momento se quiere cachear la
  resolución de la llave por rendimiento, conviene arreglar ambas a la vez con
  `ConcurrentDictionary`.
