# Entrega de builds para revisión manual

Regla permanente autorizada el 03/10/2026 en 1K.3A-R1. El acceso anclado del usuario puede conservar otro ejecutable: no usarlo como evidencia ni pedir abrir desde él.

1. Comprobar baseline, Git, identidad local, instancias y hash de la base protegida.
2. Compilar Release en una carpeta específica sin sobrescribir entregas anteriores. Para revisión, definir la propiedad de MSBuild `CasaMigaReviewBuild` con el identificador de entrega. Presentation incorpora ese valor como metadata; sólo una instalación de desarrollo lo muestra en el título. Sin esta propiedad, un build comercial futuro no incorpora la etiqueta de revisión.
3. Ejecutar focales, suite completa, diff check y revisión. Registrar hashes de binarios y fuentes exactas. El SHA informativo del ensamblado puede corresponder al padre porque el commit se crea después de validar; no confundirlo con evidencia de contenido obsoleto. El manifiesto vincula fuentes, hashes, pruebas y commit final.
4. Preparar `development.settings.json` local/ignorado junto al EXE. La implementación actual de FoundationOptions ignora este archivo si CASAMIGA_DATA_DIR existe: el launcher de revisión debe limpiar esa variable en su entorno local, usar el archivo verificado y fijar el runtime del proyecto si es necesario. No cambiar opciones globales del equipo.
5. Usar una copia aislada de la instalación si la base original debe permanecer inmutable. Copiar base cerrada y coherente, terminal, configuración y medios; no trasladar locks activos. Registrar procedencia y DataDirectory exactos. Conservar la configuración física de impresión, sin enviar trabajos.
6. Crear launcher nuevo por entrega, comprobar su contenido y ruta efectiva. Registrar EXE/DLL, DataDirectory, identificador y hashes en la evidencia. No sobrescribir silenciosamente un launcher anterior.
7. Actualizar documentos y crear sólo el checkpoint autorizado. Verificar Git limpio y capas protegidas.
8. Al terminar pruebas y preparación, ejecutar automáticamente el launcher exacto y dejar la aplicación abierta en acceso. Comprobar proceso, ruta y título visible. No iniciar sesión automáticamente ni realizar operaciones dentro de la aplicación.
9. Si hay otra instancia, no cerrarla a la fuerza. Revisar si comparte DataDirectory; ante concurrencia insegura detener la apertura y explicarlo. DirectoryLease sigue siendo la defensa existente.
10. Comprobar nuevamente hash de la base original y reportar cualquier diferencia. Entregar credenciales correspondientes a la copia preparada cuando estén disponibles y autorizadas, junto con un checklist concreto. El estado es «Corrección técnica completada; smoke manual pendiente» hasta aprobación explícita del usuario.

Esta regla no autoriza cambios de negocio, migraciones, impresiones, operaciones reales o nuevas fases. No permite modificar Domain/Application/Infrastructure para identificar un build visual.
