# ADR 0007 — El README y `<Version>` se actualizan en el mismo cambio

- **Estado:** Aceptado
- **Fecha:** 2026-08-01
- **Afecta a:** proceso de release, [README.md](../../README.md), [PAN.DapperLambdaToSql.csproj](../../PAN.DapperLambdaToSql.csproj)
- **Relacionado:** [ADR 0006](0006-orden-y-paginado-en-memoria.md)

## Contexto

`GeneratePackageOnBuild=True` hace que cualquier build en `Release` regenere
`bin/Release/PAN.DapperLambdaSQL.<version>.nupkg` con el `<Version>` que haya
en el `.csproj` en ese momento (ver la sección "Commands" de `CLAUDE.md`). El
`.nupkg` empaqueta el `README.md` del repo (`PackageReadmeFile` +
`<None Include="README.md" Pack="true" .../>`), así que ese archivo es lo que
un consumidor ve en la página de NuGet del paquete.

Sin una convención explícita, es fácil que ambos se desincronicen: agregar
una API nueva (como el orden multi-clave y `QueryPagedAsync` de
[ADR 0006](0006-orden-y-paginado-en-memoria.md)), documentarla en el README,
y olvidar el bump de `<Version>` — o al revés, bumpear la versión sin que el
README describa lo que cambió. En ambos casos el `.nupkg` que se sube queda
mintiendo: o la versión publicada no trae la documentación de lo nuevo, o el
README promete algo que la versión instalada todavía no tiene.

## Decisión

Todo cambio que actualiza el README (features nuevas, ejemplos, secciones)
se hace en el mismo commit/PR que el bump de `<Version>` y la entrada
correspondiente en `<PackageReleaseNotes>`. El criterio es simple: si el
README cambió para describir algo que un consumidor puede usar, el paquete
tiene que poder publicarse con esa descripción ya adentro.

No hay automatización de por medio — el repo no tiene CI (ver `CLAUDE.md`) —
es una convención de equipo: el mismo cambio que toca el README deja el
`.csproj` listo para `dotnet pack` sin pasos manuales adicionales.

## Alternativas consideradas

**Un git hook que bloquee el commit si `README.md` cambió sin que
`<Version>` cambiara.** Se descartó por ahora: agrega infraestructura nueva
a un repo que deliberadamente no tiene CI ni hooks, para un problema que una
convención documentada ya resuelve. Si en el futuro se cuela una
desincronización real, es la primera alternativa a reconsiderar.

## Consecuencias

- Cada PR que toca el README debe revisar dos cosas más: el número en
  `<Version>` y el bloque nuevo en `<PackageReleaseNotes>`.
- No hay garantía automática — depende de que quien revisa el PR lo note.
- El README publicado en NuGet.org para una versión dada siempre describe
  exactamente lo que esa versión hace, sin desfasaje.
