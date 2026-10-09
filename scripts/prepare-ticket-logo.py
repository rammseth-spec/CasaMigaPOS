"""Technical thermal derivative of the pinned Variant 04 master render; no drawing."""
from pathlib import Path
from hashlib import sha256
from PIL import Image

root = Path(__file__).resolve().parents[1]
master = root / "assets/brand/Variant04/CasaMiga_LOGO_OFICIAL_VECTOR.svg"
source = root / "src/CasaMiga.Presentation/Assets/CasaMigaLogo.png"
assert sha256(master.read_bytes()).hexdigest().upper() == "7E67467CCF778EF40C40E8D15716AD550E1DA3A11C7D592D57578531A8228423"
assert sha256(source.read_bytes()).hexdigest().upper() == "A1858372624CDDC87091A3DCFCBC5FA5E21F8C2621C5D5C6E2ACEE6D363B06CC"
logo = Image.open(source).convert("RGBA")
# The approved 3080 x 1786 transparent render is derived directly from that SVG.
# 1K.4E physical review found the 490-dot mark still dominant.
# Scale uniformly to 446 dots (9.0% smaller); rounding introduces <0.1% aspect error.
ink_width = 446
height = round(ink_width * logo.height / logo.width)
alpha = logo.getchannel("A").resize((ink_width, height), Image.Resampling.LANCZOS)
ink = alpha.point(lambda value: 0 if value >= 128 else 255).convert("1")
canvas = Image.new("1", (576, height + 16), 1)
canvas.paste(ink, ((canvas.width - ink_width) // 2, 8))
destination = root / "assets/brand/Variant04/CasaMiga_Ticket_Termico_576px_1bit.png"
canvas.save(destination)
print(destination.name, canvas.size, "1-bit", sha256(destination.read_bytes()).hexdigest().upper())
