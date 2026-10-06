# Flujo permanente Codex ↔ GitHub

Este procedimiento convierte GitHub en parte del checkpoint técnico de Casa Miga POS sin reemplazar las validaciones locales ni físicas.

## Precondición

La importación canónica del repositorio local debe estar completada. origin/main debe contener el árbol real de src, tests, migrations, scripts, documentación e historial reconciliado. Mientras el Issue #3 siga pendiente, no se debe fingir esta sincronización reconstruyendo archivos desde reportes.

## Inicio de bloque

Codex debe comprobar como mínimo: git status; git branch -vv; git remote -v; git fetch origin --prune; git log --oneline --decorate --graph -20.

Si el árbol no está limpio por trabajo previo no relacionado, si el remoto apunta a otro repositorio o si el historial local no es compatible con origin/main, detener la sincronización y reportar el conflicto. No usar force-push.

## Desarrollo

Trabajar sólo dentro del alcance autorizado. Mantener las reglas existentes de atomicidad, migraciones inmutables, trazabilidad financiera/inventario, datos aislados y efectos físicos post-commit.

## Cierre local

Antes de sincronizar: ejecutar las pruebas requeridas y scripts/verify.ps1 cuando corresponda; ejecutar git diff --check; revisar el diff completo; comprobar que no estén rastreados DB, backups, credenciales, configuraciones locales o logs; actualizar CURRENT_STATE, NEXT_STEPS, reporte y evidencia; crear commits claros; dejar git status limpio.

## Sincronización automática segura

Ejecutar: ./tools/github-sync/complete-codex-checkpoint.ps1 -CheckpointName "FASE 1X.Y"

El script valida que el repositorio remoto ya tenga el árbol canónico; vuelve a ejecutar la verificación canónica; bloquea archivos sensibles rastreados; verifica compatibilidad con origin/main; crea una rama de checkpoint única; hace push sin --force; y si gh está instalado y autenticado crea el PR automáticamente. Nunca fusiona el PR.

## GitHub

Al abrirse el PR, GitHub CI y CodeQL deben ejecutarse cuando aplique. Dependabot puede proponer actualizaciones, pero ninguna dependencia se acepta sólo por ser más nueva.

Un checkpoint sólo puede integrarse a main después de revisar el PR y las verificaciones aplicables. Para cambios que afecten hardware, instalador o UX, el smoke humano/físico sigue siendo obligatorio y debe quedar documentado aparte.

## Reporte final de Codex

Cada reporte debe incluir: fase/subfase; modelo usado; HEAD inicial y final; commits; rama remota y PR si existen; estado Git; pruebas ejecutadas y resultados; estado de CI/CodeQL conocido; schema/migraciones; datos protegidos; smoke manual aprobado, pendiente o no aplicable; riesgos residuales; siguiente paso autorizado.

Nunca declarar que GitHub, CI o el script validaron algo que no se ejecutó realmente.
