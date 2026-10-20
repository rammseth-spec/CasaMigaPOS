# Actualizar la caja real a la versión 31 de la base, y cómo volver

Probado el 20/10/2026 sobre copias recientes de la caja real (23 ventas, $1,633, caja del 19/10 abierta, 44 productos). **Todavía no se aplica a la caja real**: espera la aprobación del usuario.

## Qué cambia

El programa de `main` (preparaciones dentro de preparaciones y arreglos para la entrega) agrega la migración 031: una tabla nueva, vacía, y sus reglas. No modifica ninguna venta, caja, producto, usuario, ticket ni registro de auditoría. Una vez actualizada, **una versión anterior del programa ya no abre esa base**: se niega con «La base requiere una versión más reciente de la aplicación» y no la daña.

## Lo que se comprobó

| Prueba | Resultado |
|---|---|
| Respaldo oficial de la caja real (`CasaMiga.Maintenance backup`, con la caja cerrada) restaurado en una carpeta nueva | Correcto; restaura también `printing.json` |
| Arranque del programa nuevo sobre una copia de la caja en versión 30 (el mismo camino que al abrir la app) | Respalda antes y después, y sube la base a 31 |
| Todas las tablas de la caja real (versión 30) contra la copia ya en 31, renglón por renglón | Iguales; la única diferencia es el registro de la migración 31 y la tabla nueva vacía |
| Versión anterior (`4e8ec88`) sobre la copia ya en 31 | Se niega a abrirla y no la daña |
| Versión anterior sobre la copia de la carpeta tomada antes de actualizar | Abre normal: versión 30, 23 ventas, $1,633, caja abierta |
| Herramienta anterior restaurando un respaldo hecho por la versión nueva | **No puede**: los respaldos nuevos llevan la configuración de la impresora y la versión anterior no la reconoce |

Por eso **la forma de volver es la copia de la carpeta completa**, no el `.cmbackup`.

## Pasos para actualizar (cuando se apruebe)

1. Cerrar la caja (el programa, no el turno) y comprobar que no queda abierta.
2. Sacar una **copia de la carpeta completa** de datos: `C:\Users\ramms\CasaMiga-Revision\local-main-c2fb393\caja` → `C:\Users\ramms\CasaMiga-Ventas\respaldos\caja-antes-v31-<fecha>`. Copiar también la carpeta del programa: `CasaMiga-Ventas\programa` → `CasaMiga-Ventas\programa-4e8ec88-anterior`.
3. Hacer además un respaldo oficial: `CasaMiga.Maintenance backup <carpeta de datos>`.
4. Poner el programa nuevo en `CasaMiga-Ventas\programa`, sin `development.settings.json`.
5. Abrir la caja con su lanzador. Al arrancar respalda, sube la base a 31 y vuelve a respaldar.
6. Revisar que estén las ventas del día, la caja abierta y el catálogo.

## Cómo volver a la versión anterior

1. Cerrar la caja.
2. Renombrar la carpeta de datos actualizada (`caja` → `caja-v31-no-usada`) y poner en su lugar la copia del paso 2 (`caja-antes-v31-<fecha>` → `caja`).
3. Volver a poner el programa anterior (`programa-4e8ec88-anterior` → `programa`).
4. Abrir la caja.

**Importante:** lo que se haya vendido o registrado *después* de actualizar queda en la carpeta `caja-v31-no-usada` y no aparece en la versión anterior. Por eso conviene actualizar al **inicio** del día, antes de vender, y decidir en ese momento si se queda o se regresa.
