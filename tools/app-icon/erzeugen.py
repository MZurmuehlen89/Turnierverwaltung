"""Erzeugt aus AppIcon.svg alle Icon-Dateien der WinUi-App (#398).

Aufruf:  python tools/app-icon/erzeugen.py [--vorschau datei.png]

Braucht Pillow und einen installierten Edge oder Chrome (rastert das SVG headless – weder Pillow noch
das svgtool unter tools/seed-bildmaterial geben Verläufe und Kantenglättung in dieser Qualität wieder).
"""
import argparse
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image

ORDNER = Path(__file__).resolve().parent
QUELLE = ORDNER / "AppIcon.svg"
ASSETS = ORDNER.parents[1] / "src" / "Fussballturnierverwaltung.WinUi" / "Assets"

BROWSER = [
    Path(r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"),
    Path(r"C:\Program Files\Microsoft\Edge\Application\msedge.exe"),
    Path(r"C:\Program Files\Google\Chrome\Application\chrome.exe"),
]
RASTER_KANTE = 1024

ICO_GROESSEN = [16, 24, 32, 48, 64, 128, 256]
# Taskleiste, Startmenü und Alt+Tab nehmen bei der paketierten App diese Dateien, nicht die .ico.
# Das Motiv bringt seine Kachel selbst mit, deshalb sind die Fassungen für helle und dunkle Leisten gleich.
TARGETSIZE_GROESSEN = [16, 24, 32, 48, 256]
TARGETSIZE_FASSUNGEN = ["altform-unplated", "altform-lightunplated"]

# Dateiname → (Breite, Höhe, Anteil des Motivs an der kürzeren Kante)
LOGOS = {
    "Square44x44Logo.scale-200.png": (88, 88, 1.0),
    "StoreLogo.png": (50, 50, 1.0),
    "LockScreenLogo.scale-200.png": (48, 48, 1.0),
    "Square150x150Logo.scale-200.png": (300, 300, 0.5),
    "Wide310x150Logo.scale-200.png": (620, 300, 0.5),
    "SplashScreen.scale-200.png": (1240, 600, 0.5),
}


def svg_rastern() -> Image.Image:
    browser = next((b for b in BROWSER if b.exists()), None)
    if browser is None:
        sys.exit("Weder Edge noch Chrome gefunden – Pfade in BROWSER ergänzen.")

    with tempfile.TemporaryDirectory() as temp:
        # Hülle statt direktem Aufruf der SVG-Datei: sonst zeichnet der Browser sie in ihrer Eigengröße
        # (256 px) in die Ecke des Fensters, statt sie auf die Rasterkante zu strecken.
        huelle = Path(temp) / "huelle.html"
        huelle.write_text(
            f'<body style="margin:0"><img src="{QUELLE.as_uri()}" '
            f'style="display:block;width:{RASTER_KANTE}px;height:{RASTER_KANTE}px"></body>',
            encoding="utf-8")
        bild = Path(temp) / "raster.png"
        subprocess.run(
            [str(browser), "--headless", "--disable-gpu", "--hide-scrollbars",
             "--default-background-color=00000000", f"--window-size={RASTER_KANTE},{RASTER_KANTE}",
             f"--user-data-dir={Path(temp) / 'profil'}", f"--screenshot={bild}", huelle.as_uri()],
            check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        return Image.open(bild).convert("RGBA").copy()


def skaliert(motiv: Image.Image, kante: int) -> Image.Image:
    return motiv.resize((kante, kante), Image.LANCZOS)


def auf_flaeche(motiv: Image.Image, breite: int, hoehe: int, anteil: float) -> Image.Image:
    kante = round(min(breite, hoehe) * anteil)
    flaeche = Image.new("RGBA", (breite, hoehe), (0, 0, 0, 0))
    flaeche.alpha_composite(skaliert(motiv, kante), ((breite - kante) // 2, (hoehe - kante) // 2))
    return flaeche


def assets_schreiben(motiv: Image.Image) -> None:
    skaliert(motiv, max(ICO_GROESSEN)).save(ASSETS / "AppIcon.ico", sizes=[(g, g) for g in ICO_GROESSEN])
    for groesse in TARGETSIZE_GROESSEN:
        for fassung in TARGETSIZE_FASSUNGEN:
            skaliert(motiv, groesse).save(ASSETS / f"Square44x44Logo.targetsize-{groesse}_{fassung}.png")
    for name, (breite, hoehe, anteil) in LOGOS.items():
        auf_flaeche(motiv, breite, hoehe, anteil).save(ASSETS / name)


def vorschau_schreiben(motiv: Image.Image, ziel: Path) -> None:
    """Alle .ico-Größen auf hellem und dunklem Grund, darunter die kleinen Größen vergrößert."""
    abstand, zeile = 20, 256 + 40
    breite = sum(ICO_GROESSEN) + abstand * (len(ICO_GROESSEN) + 1)
    bogen = Image.new("RGBA", (breite, 3 * zeile), (243, 243, 243, 255))
    bogen.paste((32, 32, 32, 255), (0, zeile, breite, 2 * zeile))
    for reihe in range(2):
        x = abstand
        for groesse in reversed(ICO_GROESSEN):
            bogen.alpha_composite(skaliert(motiv, groesse), (x, reihe * zeile + abstand))
            x += groesse + abstand
    x = abstand
    for groesse, faktor in ((16, 12), (24, 8), (32, 6)):
        lupe = skaliert(motiv, groesse).resize((groesse * faktor, groesse * faktor), Image.NEAREST)
        bogen.alpha_composite(lupe, (x, 2 * zeile + abstand))
        x += groesse * faktor + abstand
    bogen.save(ziel)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--vorschau", type=Path, help="statt der Assets nur einen Vorschaubogen schreiben")
    argumente = parser.parse_args()

    motiv = svg_rastern()
    if argumente.vorschau:
        vorschau_schreiben(motiv, argumente.vorschau)
    else:
        assets_schreiben(motiv)
        print(f"Assets geschrieben nach {ASSETS}")
