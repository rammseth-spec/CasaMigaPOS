# Evidencia aislada — imágenes 1K.2A

Ejecutada el 27/09/2026 local (2026-09-28T01:11:40Z), Windows build 26200, .NET 10.0.11, WPF 10.0.0.0. Script reproducible: `phase1k2a-image-probe.ps1`, con PowerShell STA. Salidas gráficas locales bajo `work/phase1k2a-probe`, ignorado por Git.

Entrada: carta técnica determinista de 1536 × 1536 con gradiente, líneas y círculos. No es una fotografía, no representa pan ni se incorpora al catálogo. No se usó IA, stock, Internet ni Opción 07 para producirla.

Método: JPEG calidad 85 y PNG; una carga de calentamiento y 30 lecturas por combinación mediante FileStream, BitmapImage OnLoad, Freeze y CopyPixels en BGRA32. Mediana: muestra ordenada 16/30; p95: 29/30. Caché del sistema operativo caliente. No se mide latencia de disco frío, render del POS, consumo GPU ni working set completo. La copia de píxeles añade trabajo que el flujo final no necesita repetir por card.

| Formato | Lado px | Archivo bytes | Píxeles BGRA32 bytes | Mediana ms | p95 ms | Apertura exclusiva con bitmap vivo |
|---|---:|---:|---:|---:|---:|---|
| JPEG Q85 | 512 | 44,809 | 1,048,576 | 1.647 | 3.204 | Sí |
| PNG | 512 | 11,006 | 1,048,576 | 0.995 | 1.547 | Sí |
| JPEG Q85 | 768 | 94,549 | 2,359,296 | 2.879 | 3.468 | Sí |
| PNG | 768 | 12,109 | 2,359,296 | 2.076 | 3.092 | Sí |

| Lado JPEG | Decodificaciones secuenciales | Tiempo ms | MiB teóricos si se retuvieran todos en BGRA32 |
|---|---:|---:|---:|
| 512 | 30 | 64.65 | 30 |
| 512 | 50 | 96.93 | 50 |
| 512 | 100 | 195.59 | 100 |
| 768 | 30 | 93.84 | 67.5 |
| 768 | 50 | 168.52 | 112.5 |
| 768 | 100 | 325.97 | 225 |

Los lotes reutilizan el mismo archivo y descartan cada instancia: **no son un catálogo real de 100 imágenes distintas ni una medición de RAM retenida**. No hubo fallos de decodificación. La apertura exclusiva posterior demuestra liberación del handle en esta prueba; no demuestra ausencia global de fugas en una futura UI.

Inspección visual de las dos cartas JPEG: 768 conserva más detalle fino al visualizar más píxeles; ambas muestran artefactos de muestreo en líneas de alta frecuencia. No se extrapola ese resultado a textura de pan. Para un área de foto de hasta 232 DIP, 512 cubre hasta 200% de escala sin ampliar; 768 consume 2.25 veces el payload y aquí produjo 2.11 veces el archivo JPEG. La nitidez a 300% y en fotografías reales sigue pendiente.

PNG ganó tamaño y tiempo en esta carta de colores planos. Eso **no justifica escoger PNG para fotografía**, ni prueba que JPEG siempre será menor. La propuesta JPEG Q85/512 se apoya en codecs nativos, área objetivo y presupuesto de memoria; queda una validación visual con 3–5 fotos reales antes de congelar la política de importación. WebP no se midió: no se instaló ni incorporó codec adicional. No se realizaron benchmarks del POS ni pruebas Windows 10/LTSC.
