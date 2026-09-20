#!/usr/bin/env python3
"""Rasterise the Eksabli mark into a multi-size favicon.ico.

Kept in the repo so the icon can be regenerated when the brand mark changes, and written
against the stdlib alone -- no Pillow, no ImageMagick -- because neither is available on the
machines that build this project.

Geometry mirrors angular/src/assets/images/eksabli-mark.svg exactly (48-unit viewBox): a
rounded tile with a violet diagonal gradient and three white bars forming an "E".

Usage:  python tools/generate-favicon.py <out.ico> [<out.ico> ...]
"""
import struct
import sys

# --- source geometry, in the SVG's 48-unit space -----------------------------------------
VIEW = 48.0
TILE_R = 13.0
GRAD_FROM = (0x7C, 0x6A, 0xF0)   # --eks-primary-500
GRAD_TO = (0x4F, 0x37, 0xC4)     # --eks-primary-700
BARS = [
    (15.0, 14.0, 19.0, 5.0, 2.5, 1.00),
    (15.0, 21.5, 12.0, 5.0, 2.5, 0.72),
    (15.0, 29.0, 19.0, 5.0, 2.5, 1.00),
]
SS = 4  # supersampling factor per axis


def in_rrect(px, py, x, y, w, h, r):
    if px < x or px > x + w or py < y or py > y + h:
        return False
    cx = min(max(px, x + r), x + w - r)
    cy = min(max(py, y + r), y + h - r)
    dx, dy = px - cx, py - cy
    return dx * dx + dy * dy <= r * r


def sample(px, py):
    """Return (r, g, b, a) for one point in 48-unit space."""
    if not in_rrect(px, py, 0.0, 0.0, VIEW, VIEW, TILE_R):
        return (0, 0, 0, 0.0)
    t = ((px / VIEW) + (py / VIEW)) / 2.0
    col = [GRAD_FROM[i] + (GRAD_TO[i] - GRAD_FROM[i]) * t for i in range(3)]
    for (bx, by, bw, bh, br, alpha) in BARS:
        if in_rrect(px, py, bx, by, bw, bh, br):
            col = [c + (255 - c) * alpha for c in col]
            break
    return (col[0], col[1], col[2], 1.0)


def render(size):
    """Supersampled RGBA rows, top-down."""
    rows = []
    step = VIEW / (size * SS)
    for py in range(size):
        row = []
        for px in range(size):
            ar = ag = ab = aa = 0.0
            for sy in range(SS):
                for sx in range(SS):
                    # premultiplied accumulation keeps edges from fringing dark
                    u = (px * SS + sx + 0.5) * step
                    v = (py * SS + sy + 0.5) * step
                    r, g, b, a = sample(u, v)
                    ar += r * a; ag += g * a; ab += b * a; aa += a
            n = float(SS * SS)
            if aa == 0:
                row.append((0, 0, 0, 0))
            else:
                row.append((int(ar / aa + 0.5), int(ag / aa + 0.5),
                            int(ab / aa + 0.5), int(aa / n * 255 + 0.5)))
        rows.append(row)
    return rows


def dib(size, rows):
    """32-bit BGRA BITMAPINFOHEADER image: bottom-up pixels plus a padded AND mask."""
    hdr = struct.pack('<IiiHHIIiiII', 40, size, size * 2, 1, 32, 0, size * size * 4,
                      0, 0, 0, 0)
    px = bytearray()
    for row in reversed(rows):
        for (r, g, b, a) in row:
            px += bytes((b, g, r, a))
    mask_stride = ((size + 31) // 32) * 4   # 1bpp rows padded to 4 bytes
    mask = bytes(mask_stride * size)        # all-zero: alpha channel carries transparency
    return hdr + bytes(px) + mask


def build(sizes=(16, 32, 48)):
    images = [dib(s, render(s)) for s in sizes]
    out = struct.pack('<HHH', 0, 1, len(images))
    offset = 6 + 16 * len(images)
    for s, img in zip(sizes, images):
        out += struct.pack('<BBBBHHII', s if s < 256 else 0, s if s < 256 else 0,
                           0, 0, 1, 32, len(img), offset)
        offset += len(img)
    return out + b''.join(images)


if __name__ == '__main__':
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    data = build()
    for path in sys.argv[1:]:
        with open(path, 'wb') as fh:
            fh.write(data)
        print('wrote %s (%d bytes)' % (path, len(data)))
