# FASE 1A — aceptación y límites

Baseline previo: FASE 0 copiada a la nueva carpeta vacía, 11 tests pasan y build Release 0 errores/0 warnings. Se conserva el Git de destino; no hay commits previos. Implementar sólo identidad/permisos y apertura, sin ventas/hardware/cierre completo.

1. Migración 002, sin cambiar bytes de 001: usuarios, roles/permisos, cash_session, auditoría. UNIQUE parcial por terminal OPEN; CHECK de montos/estado/cierre y trigger que impide reabrir CLOSED. Transacción inmediata SQLite evita carrera de lectura/escritura, índice protege incluso contra otro escritor.
2. Puertos de reloj, passwords y unidad de trabajo pequeños; aggregate inmutable y autorización en Application. Apertura y auditoría comparten commit. Sin repositorio genérico.
3. Bootstrap local sin credenciales fijas, login, logoff y terminal configurable con ID persistente. Contraseñas mediante Microsoft Identity; ver ADR-006.
4. WPF con recursos centralizados, placeholder textual temporal, login/bootstrap, estado cerrado, captura/cancelación de fondo, estado abierto recuperable. No sesión autenticada recordada al reiniciar.
5. Pruebas de R1-R9, permisos, concurrencia real con dos conexiones, rollback por fallo de auditoría, upgrade 001→002 y bootstrap/login. Clean/restore/build/test y smoke UI con reinicio. Toda limitación física de touch se reporta.

Riesgos: tiempo de hashing ocupa una transacción de login acotada; bloqueo persistido reduce intentos pero puede usarse para negar servicio local. Backups contienen hashes y datos, deben protegerse. El estado de caja abierto pertenece a terminal, no se cambia de propietario al entrar otro usuario autorizado. La segunda terminal se admite en modelo/pruebas, no se crea red ni multiterminal operativo.
