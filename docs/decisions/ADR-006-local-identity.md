# ADR-006 — Identidad local mínima (FASE 1A)

Estado: adoptado dentro del alcance aprobado. Consulta oficial: 2026-09-05.

Usar PasswordHasher de Microsoft.Extensions.Identity.Core 10.0.11 (MIT, mantenido por dotnet/aspnetcore), sin servidor web ni Identity UI. IdentityV3 serializa algoritmo, sal y factor de trabajo; PBKDF2-HMAC-SHA512, sal aleatoria de 128 bits y subclave de 256 bits en la implementación consultada. Configurar 210000 iteraciones, verificar mediante API oficial y renovar hash cuando devuelve SuccessRehashNeeded. No implementar primitivas propias. Medir latencia en la caja física antes de producción.

Microsoft recomienda PasswordHasher para nuevas aplicaciones de login en lugar de usar directamente la primitiva KeyDerivation.Pbkdf2. Argon2id es una alternativa válida, pero añade otra implementación/dependencia sin necesidad demostrada aquí. No se usa SHA rápido ni cifrado reversible para contraseñas.

Bootstrap inicial sólo si no existe ningún usuario, dentro de transacción: usuario escoge nombre/contraseña (12 a 128 caracteres), recibe rol Administrador y queda auditoría. Sin contraseña predeterminada, seed automático, cuentas demo en producción ni secreto en Git. Login posterior explícito. La posesión del directorio Windows antes del bootstrap es el límite de confianza; restringir ACL del puesto antes de uso real.

Los tres roles son datos; la autorización consulta permisos explícitos en Application dentro de la misma transacción que la operación. Administrador: los cuatro permisos. Encargado: open/close/view y manual_open_future. Cajero: open/view; close queda reservado para un incremento futuro. Tener un permiso no implementa la función correspondiente.

Sesión autenticada sólo en memoria, sin token persistido. Reiniciar requiere login; recupera sesión de caja existente si el usuario tiene cash.view, preservando identidad de quien abrió. No confiar en un ID de usuario remitido desde la UI. Revalidar usuario activo y permiso en cada operación. Logout limpia contexto.

Cinco fallos de cuenta conocida bloquean un minuto (persistido, reloj inyectado); respuestas genéricas para usuario inexistente/inactivo/bloqueado o contraseña errónea. Verificación dummy para nombres desconocidos. No es defensa frente a administrador Windows que modifica archivos; no hay recuperación de credenciales ni MFA en esta etapa.

Fuentes: [recomendación Microsoft](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/password-hashing?view=aspnetcore-10.0), [código oficial PasswordHasher](https://github.com/dotnet/aspnetcore/blob/v10.0.11/src/Identity/Extensions.Core/src/PasswordHasher.cs), [paquete y licencia](https://www.nuget.org/packages/Microsoft.Extensions.Identity.Core/10.0.11).

FASE 1B añade cash.close.other sólo a Administrator y Manager. Cashier conserva open/view, sin close automático. Cerrar siempre requiere cash.close y adicional other para sesión ajena. Ver ADR-007 y la migración 003; esta ampliación no modifica el hashing ni introduce administración de usuarios.
