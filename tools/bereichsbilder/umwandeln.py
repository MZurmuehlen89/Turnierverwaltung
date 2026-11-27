"""Wandelt die KI-Rohbilder (PNG) in die ausgelieferten JPEGs um (#269).

Aufruf:  python tools/bereichsbilder/umwandeln.py

Liest tools/bereichsbilder/original/*.png (nicht im Repo, siehe .gitignore) und schreibt
src/Fussballturnierverwaltung.WinUi/Assets/Bereiche/<name>.jpg (sRGB, Qualität 85, progressiv).
"""
from pathlib import Path

from PIL import Image

ORDNER = Path(__file__).resolve().parent
ZIEL = ORDNER.parents[1] / "src" / "Fussballturnierverwaltung.WinUi" / "Assets" / "Bereiche"

for quelle in sorted((ORDNER / "original").glob("*.png")):
    ziel = ZIEL / (quelle.stem + ".jpg")
    with Image.open(quelle) as bild:
        bild.convert("RGB").save(ziel, "JPEG", quality=85, optimize=True, progressive=True)
    print(f"{ziel.name}  {ziel.stat().st_size // 1024} KB")
