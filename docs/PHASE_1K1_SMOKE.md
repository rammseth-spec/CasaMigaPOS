# Smoke visual humano — FASE 1K.1

Estado: **APROBADO por el usuario el 27/09/2026**.

Revisar en la compilación Release preparada:

1. Login: logo oficial HD nítido, completo y proporcionado; tarjeta y botón Entrar legibles.
2. Administrator: sidebar completa, header con usuario/rol/terminal, navegación Caja, Catálogo, Inventario, Recetas, Producción, Historial y POS.
3. POS: conserva interacción y aparece dentro del shell, sin cabecera/navegación duplicada.
4. Manager: acceso según permisos existentes; sin capacidades administrativas nuevas.
5. Cashier: no ve Inventario, Recetas, Producción ni Historial; conserva Caja, Catálogo y POS.
6. Logout: “Salir de mi usuario” vuelve al login sin cerrar la caja.
7. Redimensionar: revisar 1280 × 820 y el mínimo 960 × 640; no debe deformarse el logo ni perderse contenido sin scroll.
8. Pantalla completa: iniciar sesión y comprobar entrada automática; Esc y logout deben restaurar la ventana. El botón y F11 también alternan el modo.
9. Botones: confirmar texto claro sobre café, contraste legible en secundarios, hover y deshabilitados.
10. Logo: comparar techo, pan/cortes, Casa Miga, sonrisa delgada, PANADERÍA y guiones contra el manual; confirmar ausencia visual de Opción 07.
11. Icono: confirmar el logo Casa Miga en la barra de título, barra de tareas y selector de aplicaciones.

Hoja de Aguacate BEREL 405N se aplica como `#4A5D4E`, equivalencia digital aproximada autorizada por el usuario el 27 de septiembre de 2026.

No realizar operaciones financieras, movimientos de inventario ni producción real durante este smoke visual.

Revisión adicional del menú (27/09/2026): logo ajustado al espacio interior sin ancho fijo que lo recorte; iconos vectoriales por módulo; grupos tipográficos y pestañas internas de Catálogo/Recetas diferenciados. Plantilla propia de navegación con hover suave y sin cambio de aspecto al deshabilitarse durante cargas. Contenedor de contenido limitado al ancho disponible y encabezado en dos filas para evitar recortes.

Se inspeccionaron Caja, Catálogo, Inventario, Recetas, Producción e Historial en la sesión de prueba, sin registrar operaciones. Después de corregir se renderizó el shell WPF a 960 × 820 y 1280 × 820 y se revisaron ambas imágenes. `verify-theme.ps1` comprueba texto blanco, pestaña seleccionada verde/blanca y estabilidad del color del menú al deshabilitarse. El usuario aprobó el resultado visual final. No se realizaron operaciones financieras, de inventario o producción, y no se presenta este smoke visual como certificación transaccional de todos los roles.
