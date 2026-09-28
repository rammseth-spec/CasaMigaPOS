# Corrección de acceso y ajuste de ventana — 28/09/2026

Solicitud: el usuario no podía entrar y veía contenido recortado. La captura muestra bootstrap (alta del primer administrador), con error explícito por longitud de contraseña. No prueba fallo de autenticación de una cuenta existente. Las credenciales entregadas al cierre 1K.2B correspondían únicamente al backup sintético, no a esa instalación nueva.

Cambios sólo Presentation:

- MainWindow.xaml: formulario de alta en dos columnas, dimensiones/márgenes compactos, logo Uniform de 96 DIP de alto, alineación superior y ScrollViewer conservado. No se cambia geometría del logo ni los estilos operativos.
- MainWindow.xaml.cs: conservar PasswordBoxes cuando falla validación; limpiarlos sólo tras alta exitosa, pasar nombre a login y enfocar contraseña.
- MainViewModel.cs: confirmación explícita de administrador creado e instrucción para iniciar sesión. IdentityService, permisos, longitud 12–128 y datos existentes no cambian.

Verificación visual real de WPF mediante render aislado del fragmento de acceso y tema actual, sin operar la instalación del usuario:

| Área disponible (DIP) | ExtentHeight | Scroll vertical |
|---|---:|---:|
| 960×600 | 599.5133 | 0 |
| 1280×760 | 599.5133 | 0 |

Se inspeccionó PNG de 960×600: logo completo, cuatro campos, texto de ayuda, botón, error y pie visibles. Capturas y harness locales: `work/access-layout-review/`, `work/access-layout-review.ps1`. Si mensajes más largos requieren altura adicional, el scroll permite acceder al contenido.

Smoke de MainViewModel con servicios reales sobre directorio nuevo bajo work: contraseña corta rechazada; confirmación distinta rechazada; alta correcta pasa a login con mensaje de éxito; login con esa cuenta navega correctamente. `work/access-flow-review.ps1`. No cuenta de negocio creada/reset ni contraseña ajena alterada.

Release final: 0 errores/advertencias. Pruebas focalizadas Phase1K1ShellTests: 2/2, 0 fallidas/omitidas, `TestResults/access-shell.trx`. Se ejecutó scripts/verify.ps1 para la corrección funcional; después se ajustaron únicamente márgenes/alto del logo, se recompiló y se repitió comprobación focal y render. Suite completa: 621/621 (535 existentes +86 fotos), 0 fallidas/omitidas; TRX `foundation_net10.0_20260928005006.trx` y `foundation_net10.0_20260928005431.trx`.

Aplicación Release reabierta a petición de revisión. Se confirmó el proceso 15056, con ventana «Casa Miga POS · Caja», ejecutando el binario Release actualizado. Los intentos posteriores de arranque fueron rechazados por el bloqueo de directorio de esa instancia; se conserva la ventana existente para revisión. Cambios locales sin commit nuevo. No se inicia 1K.2C ni se cambia schema16.

## Ajuste posterior de login

A petición del usuario, composición centrada verticalmente según ViewportHeight, ancho máximo 1160 DIP y panel verde con cuatro esquinas redondeadas. El contenido crece con su altura natural y conserva scroll en ventanas bajas. Render WPF de login inspeccionado a 960×600 y 1536×790: logo, campos, botón y pie completos, sin scroll necesario. Bootstrap vuelto a renderizar a 960×600 y 1280×760, sin desbordamiento. Harness local: work/login-layout-review.ps1. Compilación Release sin errores ni advertencias; scripts/verify.ps1 final aprobado: 621/621 pruebas, 0 fallidas/omitidas. TRX foundation_net10.0_20260928114548.trx y foundation_net10.0_20260928114943.trx. Aplicación actualizada lanzada para revisión (PID 32500).
