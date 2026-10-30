#!/usr/bin/env python3
"""Regenerate committed PNG icon resources from the editable SVG originals.

Developer tool only: python3 + CairoSVG + Pillow. Normal builds and users do not
need an SVG renderer; the resulting PNGs are checked into the project.
"""
from pathlib import Path
import argparse
import hashlib
import json
import sys

SIZES = (16, 24, 32, 48)
ROOT = Path(__file__).resolve().parents[1]
ICONS = ROOT / "src/Icons"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Check resource hashes without renderer dependencies")
    arguments = parser.parse_args()
    manifest = ICONS / "rendered.json"
    if arguments.check:
        recorded = json.loads(manifest.read_text())
        for name, digest in recorded["files"].items():
            path = ICONS / name
            if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != digest:
                raise SystemExit("Stale or missing icon: " + str(path))
        expected = {str(path.relative_to(ICONS)) for path in (ICONS / "Source").glob("*.svg")}
        expected.update(str(path.relative_to(ICONS)) for path in (ICONS / "Rendered").glob("*/*.png"))
        if expected != set(recorded["files"]):
            raise SystemExit("Icon manifest and source/rendered files differ; regenerate icons.")
        print("PASS: all SVG originals and rendered PNGs match the manifest")
        return

    try:
        import cairosvg
        from PIL import Image
    except ImportError as error:
        raise SystemExit("Icon regeneration needs CairoSVG and Pillow; normal builds do not. " + str(error))
    files = {}
    for source in sorted((ICONS / "Source").glob("*.svg")):
        files[str(source.relative_to(ICONS))] = hashlib.sha256(source.read_bytes()).hexdigest()
        for size in SIZES:
            target = ICONS / "Rendered" / str(size) / (source.stem + ".png")
            target.parent.mkdir(parents=True, exist_ok=True)
            cairosvg.svg2png(url=str(source), write_to=str(target), output_width=size, output_height=size)
            with Image.open(target) as image:
                if image.size != (size, size) or image.getbbox() is None:
                    raise SystemExit("Empty or incorrect icon: " + str(target))
            files[str(target.relative_to(ICONS))] = hashlib.sha256(target.read_bytes()).hexdigest()
    manifest.write_text(json.dumps({"sizes": SIZES, "files": files}, indent=2) + "\n")
    print("Rendered", len(files), "SVG/PNG assets.")


if __name__ == "__main__":
    main()
