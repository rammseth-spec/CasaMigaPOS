"""Offline evidence from prepared assets and actual ESC/POS payloads. Never prints."""
from pathlib import Path
from hashlib import sha256
import json
import sqlite3
import textwrap
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
prepared = root / "work/phase1k2c-r2-prepared-assets"
evidence = root / "docs/evidence"
mapping = json.loads((prepared / "mapping.json").read_text(encoding="utf-8-sig"))
database = root / "work/phase1k2c-r2-smoke-20260928/casamiga.db"
with sqlite3.connect(database.as_uri()+"?mode=ro", uri=True) as connection:
    products = {sku: (product_id, name) for product_id, sku, name in connection.execute("SELECT id,sku,name FROM product")}
assert len(mapping) == 26 and len({item["Id"] for item in mapping}) == 26
font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 15)
sheet = Image.new("RGB", (1200, 6 * 290), "#FDF8EF")
draw = ImageDraw.Draw(sheet)
for i, item in enumerate(mapping):
    path = prepared / item["Asset"]
    photo = Image.open(path)
    assert photo.size == (512, 512)
    assert item["Provenance"] == "PROVISIONAL"
    product_id, name = products[f'CM-{item["Id"]:03}']
    assert name == item["Product"]
    item["ProductId"] = product_id
    item["AssetSha256"] = sha256(path.read_bytes()).hexdigest().upper()
    source = root / "work/phase1k2c-package" / item["Source"]
    item["SourceSha256"] = sha256(source.read_bytes()).hexdigest().upper()
    x, y = (i % 5) * 240, (i // 5) * 290
    sheet.paste(photo.resize((224, 224), Image.Resampling.LANCZOS), (x + 8, y + 4))
    label = f'{item["Id"]:02} {item["Product"]}'
    draw.multiline_text((x + 8, y + 233), "\n".join(textwrap.wrap(label, 27)), fill="#4B2E19", font=font, spacing=3)
assert mapping[0]["AssetSha256"] != mapping[1]["AssetSha256"]
sheet.save(evidence / "phase1k2c-r2-contact-sheet.png")
(evidence / "phase1k2c-r2-mapping.json").write_text(json.dumps(mapping, ensure_ascii=False, indent=2), encoding="utf-8")

mono = ImageFont.truetype("C:/Windows/Fonts/consola.ttf", 20)
for name in ("con-ticket", "reprint"):
    path = evidence / "phase1k2c-r2-checkout" / (name + ".bin")
    data = path.read_bytes()
    parts, line = [], bytearray()
    i, scale = 0, 0
    while i < len(data):
        if data[i:i+3] == b"\x1dv0":
            stride = int.from_bytes(data[i+4:i+6], "little")
            height = int.from_bytes(data[i+6:i+8], "little")
            pixels = data[i+8:i+8+stride*height]
            assert stride == 72 and height == 341
            raster = Image.frombytes("1", (stride*8, height), bytes(v ^ 255 for v in pixels))
            assert raster.tobytes() == Image.open(root / "assets/brand/Variant04/CasaMiga_Ticket_Termico_576px_1bit.png").tobytes()
            parts.append(raster.convert("RGB")); i += 8 + stride * height; continue
        if data[i] == 27:
            assert data[i+1] in (0x74, 0x61)
            i += 3; continue
        if data[i] == 29:
            if data[i+1] == 0x56:
                assert i == len(data)-3 and data[i+2] == 1
                break
            assert data[i+1] == 0x21
            scale = data[i+2]; i += 3; continue
        if data[i] == 10:
            strip = Image.new("RGB", (576, 26), "white")
            ImageDraw.Draw(strip).text((0, 0), line.decode("cp850"), fill="black", font=mono)
            if scale == 1: strip = strip.resize((576, 52), Image.Resampling.NEAREST)
            parts.append(strip); line.clear()
        elif data[i] != 13:
            line.append(data[i])
        i += 1
    assert sum(p.height == 341 for p in parts) == 1
    preview = Image.new("RGB", (576, sum(p.height for p in parts)), "white")
    y = 0
    for part in parts: preview.paste(part, (0, y)); y += part.height
    preview.save(path.with_suffix(".png"))
print("26 assets: dimensions/hash/mapping valid; distinct conchas; two payload rasters match current official logo exactly.")
