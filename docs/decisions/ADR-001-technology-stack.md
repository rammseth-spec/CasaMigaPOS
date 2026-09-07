# ADR-001 — Stack Windows offline

Estado: adoptado para FASE 0 bajo el mandato de evaluar y recomendar. Fecha: 2026-09-05. Reevaluar si Windows deja de ser el único destino.

## Decisión

.NET 10 LTS, C#, WPF; separación por proyectos y composición manual. Microsoft.Data.Sqlite como acceso ADO.NET parametrizado; MSTest. Sin contenedor DI, ORM, bus de eventos ni biblioteca de controles añadidos en esta fundación.

La política oficial sitúa .NET 10 en soporte LTS hasta noviembre de 2028. Requiere parches y futura actualización de LTS, no permanencia indefinida. [Microsoft](https://dotnet.microsoft.com/en-us/platform/support/policy).

## Comparación para Casa Miga

| Criterio | .NET + WPF | Tauri 2 |
|---|---|---|
| Windows 11 | UI Windows, C# y API Win32 | WebView2 más núcleo Rust |
| Offline | Runtime incluido mediante publicación self-contained | Assets locales; empaquetar WebView2 para instalar sin red |
| SQLite | Proveedor Microsoft, transacciones y backup API | Plugin SQL/sqlx; la lógica crítica debe permanecer en Rust |
| USB / ESC/POS | Adaptador Winspool RAW en C#; requiere driver y pruebas | Adaptador Rust/Win32 o plugin auditado; mismo requisito de hardware |
| Ethernet / cajón | TCP y comandos validados detrás de puertos | TCP desde Rust; igual necesidad de validar protocolo |
| Touch | XAML con tamaño y estados explícitos | CSS/HTML facilita diseño adaptable |
| Pruebas | MSTest, integración SQLite; UI aparte | Rust tests, frontend y pruebas de IPC/UI |
| Rendimiento | Sin motor web; medición real pendiente | Núcleo Rust eficiente; costo WebView pendiente de medir |
| Tamaño | Self-contained añade runtime | Binario pequeño no equivale a instalador offline pequeño con WebView2 |
| MSI / EXE | Publicación genera EXE, instalador es paso adicional | Herramientas integradas WiX/NSIS |
| Actualización | Instalador firmado y migración controlada por diseñar | Plugin updater con firma; también necesita política de migraciones |
| Seguridad | Menor superficie de IPC; permisos en casos de uso | Capabilities/CSP, aislamiento IPC y Rust requieren mantenimiento |
| Mantenimiento | Un lenguaje principal y ecosistema Windows consolidado | Rust, herramientas web, IPC y ciclo WebView2 |
| PostgreSQL futuro | Nuevo adaptador en servidor; no cambio automático | También viable desde Rust/servidor |

La elección es una valoración para una terminal Windows; no una afirmación de superioridad universal ni un benchmark. WPF permite estilos propios y UI táctil sin diseñar un ERP genérico. La ventana actual sólo muestra texto y no recrea el logotipo.

## Alternativas

Microsoft recomienda WinUI 3 para aplicaciones nuevas: se evaluó y se pospone por añadir Windows App SDK y complejidad de despliegue sin una función imprescindible aquí. WPF está soportado y cubre el caso; esta preferencia es una decisión del proyecto, no la recomendación general de Microsoft. WinForms es viable pero su modelo visual ofrece menos flexibilidad para esta interfaz. Avalonia aportaría multiplataforma que hoy no se necesita. Electron añadiría Chromium/Node sin ventaja determinante. No se compararon exhaustivamente estos últimos ni se agregaron dependencias.

## Distribución y aceptación

Objetivo: Windows 11 x64, ejecutable self-contained y posteriormente MSI/EXE firmado. Elegir herramienta de instalador tras revisar licencia/costo; no confundir `publish` con un instalador probado. Actualizar fuera de sesión de caja, previo backup verificado, sin degradar esquema. Medir arranque, memoria, tamaño y tacto en la PC real antes de v0.1.

Fuentes: [WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/), [plataforma Windows](https://learn.microsoft.com/en-us/windows/apps/get-started/), [publicación .NET](https://learn.microsoft.com/en-us/dotnet/core/deploying/), [instaladores Tauri](https://v2.tauri.app/distribute/windows-installer/), [SQL Tauri](https://v2.tauri.app/plugin/sql/), [capabilities](https://v2.tauri.app/security/capabilities/), [WritePrinter](https://learn.microsoft.com/en-us/windows/win32/printdocs/writeprinter).
