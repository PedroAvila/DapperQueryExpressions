# ADR 0003 — Caché de nombres de tabla segura para concurrencia

- **Estado:** Aceptado
- **Fecha:** 2026-07-28
- **Afecta a:** `DapperHelper.GetTableName`
- **Relacionado:** [ADR 0002](0002-resolucion-de-la-clave-primaria.md)

## Contexto

`GetTableName` memoriza el nombre resuelto en un `Dictionary<RuntimeTypeHandle, string>`
estático, y escribía en él sin ningún tipo de sincronización.

`Dictionary<,>` no es seguro para escrituras concurrentes. Dos hilos insertando a la vez
pueden dejar los buckets internos en un estado inconsistente: lecturas que devuelven el
valor equivocado, `IndexOutOfRangeException` desde dentro del propio diccionario o, si la
inserción dispara un redimensionamiento, un ciclo infinito recorriendo una cadena de
buckets corrupta.

No es teórico para esta librería. Es una caché estática de proceso, y el escenario natural
de uso es una API web donde varias peticiones resuelven entidades simultáneamente. La
ventana es angosta —solo la primera resolución de cada tipo escribe— pero el arranque en
frío bajo carga es justamente cuando muchas entidades distintas se resuelven a la vez.

## Decisión

Sustituir el `Dictionary` por un `ConcurrentDictionary` de solo lectura en el campo, y
resolver mediante `GetOrAdd`. La lógica de resolución del nombre se extrajo tal cual a un
`ResolveTableName` privado, sin cambios de comportamiento.

Bajo contención, `GetOrAdd` puede ejecutar la fábrica más de una vez, aunque solo un valor
queda almacenado y todos los llamadores reciben ese mismo valor. Es inocuo aquí:
`ResolveTableName` es una función pura del tipo —lee atributos y el nombre— y siempre
devuelve el mismo resultado, así que una ejecución de más solo cuesta algo de reflexión
repetida.

## Alternativas consideradas

**`lock` sobre el `Dictionary` existente.** Correcto y de una línea, pero serializa también
las lecturas, que son el 99.99% de las llamadas una vez caliente la caché.
`ConcurrentDictionary` da lecturas sin bloqueo.

**`Lazy<string>` como valor del diccionario.** Garantizaría que la fábrica corre una sola
vez por tipo. Se descartó porque el beneficio no compensa la asignación extra por entrada
cuando la fábrica ya es pura y barata.

## Consecuencias

Los nombres resueltos son idénticos; solo cambia la seguridad de la caché. Se verificó con
un arnés que ejecuta 12 360 llamadas sobre 206 tipos distintos con la caché fría y 32 hilos
en paralelo: cero excepciones, cero tipos que devolvieran valores distintos entre hilos y
cero valores nulos. Los nombres de las 9 entidades del proyecto de referencia se mantienen
—`User → Users`, `Frequency → Frequencys`, etc.—, coincidentes con la pluralización de
Dapper.Contrib, de la que este método es una transcripción.

Queda cerrada la excepción que el [ADR 0002](0002-resolucion-de-la-clave-primaria.md) dejó
anotada: ya no hay motivo para evitar una caché en `GetKeyProperty` por no replicar este
defecto. Aun así no se agregó ninguna — la resolución de la llave reutiliza el arreglo de
propiedades que `UpdateAsync` ya obtuvo, así que no repite trabajo de reflexión.
