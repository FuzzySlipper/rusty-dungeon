#!/usr/bin/env python3
"""Derive particle flipbook strips from staged sprite sheets (offline, gitignored output).

Usage: scripts/derive-particle-strips.py <sprites.json> <art stage dir>

The Engine's billboard particles play a flipbook laid out as equal frames
across one image, while the donor keeps particle animations as runs of cells
in its particle sheet. For every entry of the sprite manifest's "particles"
section, copies cells start..end of the named staged sheet, left to right,
into the entry's strip file next to the staged sheets. The sheets are donor
art and so are these strips: they stay in the gitignored stage.
Requires Pillow.
"""
import json
import os
import sys

from PIL import Image


def main() -> None:
    manifest_path, stage = sys.argv[1], sys.argv[2]
    manifest = json.load(open(manifest_path))
    atlases = manifest.get("atlases", {})
    written = 0
    for particle_id, particle in manifest.get("particles", {}).items():
        sheet_name = particle["atlas"]
        sheet = atlases.get(sheet_name)
        source = os.path.join(stage, sheet_name)
        if sheet is None or not os.path.exists(source):
            print(f"  skip particle {particle_id}: sheet {sheet_name} is not staged")
            continue
        with Image.open(source) as image:
            image = image.convert("RGBA")
            columns, rows = sheet["columns"], sheet["rows"]
            cell_w, cell_h = image.width // columns, image.height // rows
            cells = list(range(particle["start"], particle["end"] + 1))
            strip = Image.new("RGBA", (cell_w * len(cells), cell_h))
            for frame, cell in enumerate(cells):
                left, top = (cell % columns) * cell_w, (cell // columns) * cell_h
                strip.paste(image.crop((left, top, left + cell_w, top + cell_h)), (frame * cell_w, 0))
        target = os.path.join(stage, particle["strip"])
        os.makedirs(os.path.dirname(target), exist_ok=True)
        strip.save(target)
        written += 1
    print(f"  content/delve/imports/art   {written} particle strip(s)")


if __name__ == "__main__":
    main()
