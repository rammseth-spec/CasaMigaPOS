# Impresora de tickets de la laptop de Casa Miga (copia del 25/11/2026)

Configuración leída de la computadora donde hoy imprime la caja real, para que otro agente o técnico la reproduzca. Sólo se leyó: no se cambió ninguna impresora, ningún driver ni ningún `printing.json`.

## 1. Configuración del programa (`printing.json`)

- [`printing.json`](printing.json) es una copia exacta, byte por byte, del archivo de la caja real (`<carpeta de datos>\printing.json`). SHA-256 `EAB1BDAF69A3F002…`.
- Trae los mismos valores que [`../printing.casamiga.json`](../printing.casamiga.json), que instala `configurar-impresora.ps1`. Sólo cambia el orden de las claves y que los acentos van escapados (`\u00ED`).

| Campo | Valor | Qué significa |
|---|---|---|
| `queueName` | `POS-80` | Nombre exacto de la cola de Windows a la que la caja manda el ticket |
| `columns` | `48` | Papel de 80 mm (576 puntos) |
| `encodingName` / `characterTable` | `ibm850` / `2` | Página de códigos CP850 (`ESC t 2`), para acentos y ñ |
| `cutMode` | `Partial` | Corte parcial al final del ticket |
| `openCashDrawer` | `false` | No hay cajón conectado |
| `enabled` | `true` | Impresión activa |
| `business` | Casa Miga, dirección, WhatsApp 461 276 7429, redes | Encabezado y pie del ticket |

## 2. Windows

| Dato | Valor en esta laptop |
|---|---|
| Nombre de la impresora (cola) | `POS-80` |
| Predeterminada de Windows | Sí |
| Driver | `POS-80 11.3.0.1` (fabricante «POS», Unidrv v3, x64) |
| Paquete del driver | `pos80.inf`, en `C:\WINDOWS\System32\DriverStore\FileRepository\pos80.inf_amd64_a2badcef07359909` (1.6 MB) |
| Puerto | `USB001` («Puerto de impresora virtual para USB») |
| Dispositivo USB | «POS80 Series / POS80 Printer USB», `VID_0416 & PID_5011` |
| Compartida | No |
| Color / dúplex | Blanco y negro / una cara |

Otras impresoras en la laptop, que la caja no usa: HP LaserJet 200 color MFP M276 (red), Microsoft Print to PDF y Fax.

## 3. Cómo reproducirla en otra computadora

- La caja manda el ticket en **RAW (ESC/POS)**: no depende del driver del fabricante. `configurar-impresora.ps1` crea la cola `POS-80` con el driver «Generic / Text Only» de Windows, y eso basta.
- Si se prefiere el mismo driver que esta laptop (`POS-80 11.3.0.1`), hay que instalarlo desde el paquete del fabricante de la impresora. Sus archivos no se suben al repositorio por licencia.
- Lo único indispensable:
  - que la cola se llame **exactamente `POS-80`**;
  - que esté en el puerto USB de la impresora;
  - que `printing.json` esté en la carpeta de datos de la caja.

## 4. Pendiente conocido

La web imprime con el cuadro de impresión del navegador (ver `docs/CURRENT_STATE.md`). En esta laptop hay que elegir:
- `POS-80`;
- márgenes «Ninguno»;
- sin «Gráficos de fondo».
