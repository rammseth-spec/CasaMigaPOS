# Dependencias y licencias

Versiones exactas resueltas: `packages.lock.json` en cada proyecto. NuGet.Config limita origen predeterminado a nuget.org. Licencias verificadas en metadatos nuspec de paquetes descargados; inventario completo en evidence/dependency-licenses.json. No se ha asignado licencia al código propio.

| Dependencia directa | Versión | Licencia / necesidad |
|---|---|---|
| .NET SDK / WPF | SDK 10.0.400; runtime 10.0.11 | MIT en proyectos .NET; redistribución SDK/runtime con avisos propios; UI Windows |
| Microsoft.Data.Sqlite | 10.0.11 | MIT; proveedor oficial, SQL parametrizado y backup |
| MSTest.TestFramework | 3.10.4 | MIT; reglas críticas |
| MSTest.TestAdapter | 3.10.4 | MIT; descubrimiento/ejecución |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT; host de tests |

SQLitePCLRaw 2.1.12 (bundle/core/provider/lib) usa Apache-2.0 y aporta el motor nativo; SQLite cargado realmente es **3.53.3**. SQLite upstream es de dominio público. Paquetes de prueba transitivos incluyen Microsoft.Testing.*, TestPlatform, CodeCoverage, ApplicationInsights y Newtonsoft.Json: no son dependencias de la aplicación de caja. Inventario automatizado conserva sus licencias MIT. No se incorpora ninguna referencia POS copyleft.

Mantenimiento/compatibilidad: Microsoft.Data.Sqlite 10 corresponde al stack .NET 10; MSTest tiene proyecto oficial microsoft/testfx, Test SDK microsoft/vstest, SQLitePCLRaw ericsink/SQLitePCL.raw. Se verificó compilación y ejecución, no sólo compatibilidad declarada. Repetir evaluación antes de actualizar; versiones fijadas no significan actualizar automáticamente a la última.

Seguridad: consulta al feed oficial NuGet guardada en evidence/nuget-audit.json. Se encontraron avisos por ID para SQLitePCLRaw.lib.e_sqlite3 <=2.1.11 y Newtonsoft.Json <13.0.1; las versiones resueltas 2.1.12 y 13.0.3 quedan fuera de esos rangos. No equivale a ausencia garantizada de vulnerabilidades. Además, prueba explícita exige SQLite >=3.51.3 por el fallo WAL-reset documentado por SQLite. No se han realizado análisis de penetración ni validación de firma Authenticode del producto.

Fuentes: [proveedor](https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.11), [MSTest](https://www.nuget.org/packages/MSTest.TestFramework/3.10.4), [Test SDK](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/17.14.1), [motor empaquetado](https://www.nuget.org/packages/SQLitePCLRaw.lib.e_sqlite3/2.1.12), [WAL-reset](https://www.sqlite.org/wal.html), [licencia SQLite](https://www.sqlite.org/copyright.html).

Antes de distribuir: incluir avisos/licencias transitivas y del runtime, revisar paquete instalador y dependencias de publicación. No se han redistribuido librerías POS de Floreant, uniCenta ni Odoo.

## Dependencias de FASE 1A

Se añade Microsoft.Extensions.Identity.Core 10.0.11 (MIT), mantenido por dotnet/aspnetcore, para PasswordHasher; no se incorpora servidor ni UI de ASP.NET. Transitivas Microsoft.Extensions.* y Microsoft.AspNetCore.Cryptography.* verificadas en los nuspec oficiales. ADR-006 documenta algoritmo, parámetros y fuentes.

Inventario actualizado: evidence/phase1a-dependency-licenses.json (36 paquetes resueltos). Consulta oficial actualizada: evidence/phase1a-nuget-audit.json. Sólo aparecen los dos avisos por ID ya descritos; rangos afectados excluyen Newtonsoft.Json 13.0.3 y SQLitePCLRaw.lib.e_sqlite3 2.1.12. No se encontraron avisos aplicables en esa consulta; no es garantía absoluta de seguridad. Runtime packs 10.0.11 oficiales se usaron sólo para la publicación de desarrollo. Antes de distribución, incorporar sus avisos de redistribución.

FASE 1B: sin dependencias nuevas. ZIP, SHA-256 y JSON usan bibliotecas del runtime .NET ya presente. Lockfiles conservan versiones de 1A; no se repite auditoría online sin actualización de paquetes.
