# GitHub checkpoint sync

Usar este helper sólo después de que el repositorio local canónico haya sido importado y reconciliado con GitHub.

Comando típico de cierre en Codex:

./tools/github-sync/complete-codex-checkpoint.ps1 -CheckpointName "FASE 1X.Y"

El script exige árbol limpio, comprueba archivos sensibles rastreados, ejecuta la verificación canónica, confirma que el historial contiene origin/main, crea una rama única y hace push sin force. Si GitHub CLI está instalado y autenticado también abre un PR.

Nunca fusiona automáticamente. La integración a main requiere revisar CI/CodeQL y cualquier smoke manual/físico aplicable.
