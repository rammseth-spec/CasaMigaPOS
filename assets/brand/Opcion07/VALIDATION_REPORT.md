# VALIDACIÓN DEL PAQUETE — OPCIÓN 07

Se realizaron tres rondas de validación:

## Ronda 1 — Fuente y composición
- La referencia original del usuario se preservó byte por byte.
- Dimensiones originales: 314 × 227 px.
- SHA-256 original: `178F8B03EDD649B451FCE9ABFA06CE1C5D893FB96E22AC5A9B93B8C3BEC06D47`.
- El recorte técnico se extrajo directamente de la imagen original.
- Crop exacto usado: `(37, 52, 276, 164)`.
- No se redibujó ni recompuso ningún elemento.

## Ronda 2 — Derivados visuales
- Se verificó que el recorte conserve:
  - techo lineal simple;
  - pan pequeño;
  - dos cortes blancos visibles;
  - CASA ✦ MIGA;
  - PANADERÍA;
  - ausencia de vapor;
  - ausencia de líneas laterales.
- La versión transparente usa la misma geometría del recorte.
- La versión monocromática usa la misma máscara/alpha.

## Ronda 3 — Activo térmico
- Dimensiones: 560 × 262 px.
- Modo: 1-bit.
- Redimensionado proporcional desde el recorte exacto.
- Umbral monocromático: 175.
- No hay píxeles negros tocando los bordes externos:
  - superior: 0
  - inferior: 0
  - izquierdo: 0
  - derecho: 0
- Los cortes blancos del pan permanecen visibles.
- No se detecta la franja vertical derecha ni el artefacto de esquina de paquetes anteriores.

## Hashes principales
- Referencia original: `178F8B03EDD649B451FCE9ABFA06CE1C5D893FB96E22AC5A9B93B8C3BEC06D47`
- Recorte fondo crema: `B461FE4F4BD8D3B85B363E00F5E5AFDD2FB95A9EEDAD825FA0D18AFC79881D92`
- UI transparente: `BE1D3C9E4F1AD87EF70F9337E255C0803B9921F48C7EB0B3C3708434DF0A86A6`
- Monocromático transparente: `1529463951B3952B9CAC90DC4D03D63A76FC4E2A394DD5027B841CE6E4A53F27`
- Ticket 560px 1-bit: `06D4ADE3AE160967FCD21E958458C17113BEF3C4A8E03F15D63115530AE821C6`
