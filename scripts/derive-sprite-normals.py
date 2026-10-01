#!/usr/bin/env python3
"""Derive normal-map sheets for staged sprite sheets (offline, gitignored output).

Usage: scripts/derive-sprite-normals.py <sprites.json> <art stage dir>

For every sheet the sprite manifest names with a "normals" entry, writes that
normal sheet next to the staged colour sheet, cell-for-cell aligned so the
Engine samples both with the same atlas UVs. Height is a dome over each
sprite's opaque silhouette (distance to the nearest transparent pixel, eased
to a rounded profile) plus a little luminance relief; the normal is the
height gradient in image space (+x right, +y down), which is the tangent
frame the Engine's sprite shader builds from the atlas UVs. The colour sheets
are donor art and so are these derivatives: they stay in the gitignored stage.
Requires Pillow and numpy.
"""
import json
import os
import sys

import numpy as np
from PIL import Image

DOME_RADIUS = 6      # pixels from the silhouette edge to the dome's top
DOME_WEIGHT = 1.0
DETAIL_WEIGHT = 0.35  # luminance relief on top of the dome
SLOPE = 2.5           # gradient gain; higher is a stronger relief


def distance_inside(mask: np.ndarray, limit: int) -> np.ndarray:
    """Chebyshev distance (capped at limit) from each opaque pixel to transparency."""
    distance = np.zeros(mask.shape, dtype=np.float32)
    current = mask.copy()
    for step in range(1, limit + 1):
        distance[current] = step
        padded = np.pad(current, 1, constant_values=False)
        eroded = current.copy()
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                eroded &= padded[1 + dy:1 + dy + mask.shape[0], 1 + dx:1 + dx + mask.shape[1]]
        current = eroded
        if not current.any():
            break
    return distance


def derive(colour_path: str, normal_path: str) -> None:
    rgba = np.asarray(Image.open(colour_path).convert("RGBA"), dtype=np.float32) / 255.0
    mask = rgba[..., 3] > 0.5
    t = distance_inside(mask, DOME_RADIUS) / DOME_RADIUS
    dome = np.sqrt(np.clip(1.0 - (1.0 - t) ** 2, 0.0, 1.0))
    luminance = rgba[..., 0] * 0.299 + rgba[..., 1] * 0.587 + rgba[..., 2] * 0.114
    height = np.where(mask, DOME_WEIGHT * dome + DETAIL_WEIGHT * luminance, 0.0)

    gy, gx = np.gradient(height)
    normal = np.dstack((-gx * SLOPE, -gy * SLOPE, np.ones_like(height)))
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    encoded = np.where(mask[..., None], normal, np.array([0.0, 0.0, 1.0]))
    pixels = np.dstack((((encoded * 0.5) + 0.5) * 255.0, np.full(mask.shape, 255.0)))
    Image.fromarray(np.clip(pixels, 0, 255).astype(np.uint8), "RGBA").save(normal_path)


def main() -> int:
    if len(sys.argv) != 3:
        print(__doc__, file=sys.stderr)
        return 2
    manifest = json.load(open(sys.argv[1]))
    stage = sys.argv[2]
    written = 0
    for name, sheet in manifest.get("atlases", {}).items():
        normals = sheet.get("normals")
        colour = os.path.join(stage, name)
        if not normals or not os.path.isfile(colour):
            continue
        derive(colour, os.path.join(stage, normals))
        written += 1
    print(f"derived {written} normal sheet(s) into {stage}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
