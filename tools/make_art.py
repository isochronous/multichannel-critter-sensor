"""Build the Multichannel Critter Sensor kanim.

    python tools/make_art.py

The building itself is publish/sprite-small.png, used untouched (128 px, the hand-sized
version). Everything that moves is generated here and laid over it, positioned from
measurements of publish/sprite-large.png (same framing, 500 px):

  tip_l / tip_r  the antenna balls recoloured green, shown while that side's signal is on
  glow           a soft green halo over an active antenna tip; its opacity pulses
  print          three paw prints that fade in and out from the left edge to the middle of
                 the visor, each clipped to the visible screen so they pass behind the ribbon
  egg            an egg on the right of the visor, rocking between three poses

Animations (all loop): off, on_combined (both antennae, prints), on_critter (left antenna,
prints), on_egg (right antenna, egg), on_both; plus place and ui. The prints always walk
from the left edge to about the middle of the visor, leaving the right side to the egg.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "common", "tools", "MakeKanim"))
from kanim_writer import Sprite, write_kanim  # noqa: E402

NAME = "multichannel_critter_sensor"
OUT = os.path.join(ROOT, "src", "MultichannelCritterSensor", "anim", "assets", NAME)

SIZE = 220.0                  # display size of the sprite in anim units (cell = 200)
CENTER = (0.0, -100.0)        # where the sprite's centre sits
OVERLAY_DENSITY = 0.6         # texture px per large-sprite px for the generated overlays
INK = (178, 232, 238)         # the pale cyan the visor "draws" with
GREEN = (70, 235, 60)
LOOP = 84                     # frames per loop at 30 fps, as in the vanilla sensor


def load_large():
    large = Image.open(os.path.join(ROOT, "publish", "sprite-large.png")).convert("RGBA")
    assert large.size == (500, 500), "measurements below assume the 500 px sprite"
    return large


def units(x, y):
    """Large-sprite pixel -> anim units."""
    k = SIZE / 500.0
    return CENTER[0] + (x - 250) * k, CENTER[1] + (y - 250) * k


def overlay(image):
    """A generated image drawn at large-sprite scale -> Sprite at overlay density."""
    size = (max(1, round(image.width * OVERLAY_DENSITY)), max(1, round(image.height * OVERLAY_DENSITY)))
    small = image.convert("RGBa").resize(size, Image.LANCZOS).convert("RGBA")
    return Sprite(small, image.width * SIZE / 500.0, image.height * SIZE / 500.0)


def visor_mask(large):
    """The visible dark screen: the biggest blob of visor-navy pixels."""
    a = np.asarray(large).astype(int)
    r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    navy = (al > 200) & (r < 30) & (g < 45) & (b < 60) & (b - r > 8)
    navy[:255] = False
    navy[405:] = False
    mask = Image.fromarray((navy * 255).astype(np.uint8))
    # Close the faint circuit lines drawn on the screen, then keep the blob under a seed point.
    mask = mask.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5))
    seeded = mask.copy()
    ImageDraw.floodfill(seeded, (380, 330), 128)
    mask = seeded.point(lambda v: 255 if v == 128 else 0)
    return mask.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(1.2))


# Antenna balls on the large sprite, outline included: (centre x, centre y, radius).
BALLS = {"l": (51.5, 50.0, 51.0), "r": (447.5, 50.0, 51.0)}


def green_tip(large, cx, cy, radius):
    """The ball area with blue swapped for green, feathered at the rim."""
    pad = int(radius * 1.1)
    box = (int(cx) - pad, int(cy) - pad, int(cx) + pad, int(cy) + pad)
    crop = large.crop(box)
    r, g, b, al = crop.split()
    a = np.asarray(crop).astype(int)
    bluish = ((a[..., 2] - a[..., 0]) > 40) & (a[..., 3] > 0)
    disc = Image.new("L", crop.size, 0)
    ImageDraw.Draw(disc).ellipse((pad - radius, pad - radius, pad + radius, pad + radius), fill=255)
    keep = Image.fromarray((bluish * 255).astype(np.uint8))
    keep = Image.composite(keep, Image.new("L", crop.size, 0), disc).filter(ImageFilter.GaussianBlur(0.8))
    swapped = Image.merge("RGBA", (r, b, g.point(lambda v: int(v * 0.35)), keep))
    return swapped, (box[0] + box[2]) / 2.0, (box[1] + box[3]) / 2.0


def glow_image(radius):
    size = int(radius * 3.6)
    ys, xs = np.mgrid[0:size, 0:size]
    d = np.hypot(xs - size / 2.0, ys - size / 2.0) / (size / 2.0)
    alpha = np.clip(1 - d, 0, 1) ** 1.8 * 210
    im = np.zeros((size, size, 4), np.uint8)
    im[..., :3] = GREEN
    im[..., 3] = alpha.astype(np.uint8)
    return Image.fromarray(im, "RGBA")


def paw_image(size=68):
    s = 4
    im = Image.new("L", (size * s, size * s), 0)
    d = ImageDraw.Draw(im)

    def ellipse(cx, cy, rx, ry):
        d.ellipse(((cx - rx) * size * s, (cy - ry) * size * s, (cx + rx) * size * s, (cy + ry) * size * s), fill=255)
    ellipse(0.50, 0.66, 0.25, 0.20)      # pad
    ellipse(0.36, 0.58, 0.13, 0.13)
    ellipse(0.64, 0.58, 0.13, 0.13)
    for cx, cy in ((0.20, 0.36), (0.40, 0.20), (0.62, 0.20), (0.82, 0.36)):   # toes
        ellipse(cx, cy, 0.095, 0.125)
    alpha = im.resize((size, size), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), INK + (0,))
    out.putalpha(alpha.point(lambda v: int(v * 0.85)))
    return out


def egg_image(width=62, height=84):
    s = 4
    pad = 6
    im = Image.new("L", ((width + 2 * pad) * s, (height + 2 * pad) * s), 0)
    points = []
    for i in range(120):
        t = 2 * math.pi * i / 120
        # Narrower towards the top: squeeze x by how high the point is.
        y = -math.cos(t)
        x = math.sin(t) * (1 - 0.22 * (-y + 1) / 2 * 1.6 + 0.0)
        points.append(((pad + width / 2 + x * width / 2) * s, (pad + height / 2 + y * height / 2) * s))
    d = ImageDraw.Draw(im)
    d.polygon(points, fill=150)
    d.line(points + points[:1], fill=255, width=4 * s)
    for cx, cy, r in ((0.40, 0.42, 0.07), (0.62, 0.60, 0.09), (0.36, 0.72, 0.05)):   # speckles
        d.ellipse(((pad + (cx - r) * width) * s, (pad + cy * height - r * width) * s,
                   (pad + (cx + r) * width) * s, (pad + cy * height + r * width) * s), fill=255)
    alpha = im.resize((width + 2 * pad, height + 2 * pad), Image.LANCZOS)
    out = Image.new("RGBA", alpha.size, INK + (0,))
    out.putalpha(alpha.point(lambda v: int(v * 0.85)))
    return out


def clipped(image, cx, cy, angle, mask):
    """Rotates the image, places its centre at (cx, cy) on the large sprite and clips it to the mask."""
    rotated = image.rotate(-angle, resample=Image.BICUBIC, expand=True)
    box = (int(round(cx - rotated.width / 2.0)), int(round(cy - rotated.height / 2.0)))
    region = mask.crop((box[0], box[1], box[0] + rotated.width, box[1] + rotated.height))
    r, g, b, al = rotated.split()
    rotated.putalpha(Image.fromarray((np.asarray(al).astype(int) * np.asarray(region) // 255).astype(np.uint8)))
    return rotated, box[0] + rotated.width / 2.0, box[1] + rotated.height / 2.0


def fade(frame, start, rise=9, hold=24, fall=12):
    """Opacity of a print that starts appearing at `start` (vanilla timing, roughly)."""
    t = frame - start
    if t < 0 or t > rise + hold + fall:
        return 0.0
    if t < rise:
        return t / float(rise)
    if t <= rise + hold:
        return 1.0
    return 1.0 - (t - rise - hold) / float(fall)


def main():
    large = load_large()
    small = Image.open(os.path.join(ROOT, "publish", "sprite-small.png")).convert("RGBA")
    base = Sprite(small, SIZE, SIZE)
    mask = visor_mask(large)
    box = mask.point(lambda v: 255 if v > 128 else 0).getbbox()
    print("visible screen (large px):", box)

    symbols = {"body": [base], "place": [base], "ui": [base]}
    positions = {}

    for side, (cx, cy, radius) in BALLS.items():
        tip, tx, ty = green_tip(large, cx, cy, radius)
        symbols["tip_" + side] = [overlay(tip)]
        positions["tip_" + side] = units(tx, ty)
        positions["glow_" + side] = units(cx, cy)
        if "glow" not in symbols:
            symbols["glow"] = [overlay(glow_image(radius))]

    # Prints walk left to right, alternating feet, tilted the way they are heading.
    paw = paw_image()
    top, bottom = box[1] + 42, box[3] - 44
    egg_x = box[2] - 52
    middle = (box[0] + box[2]) / 2.0
    spots = [(box[0] + 40 + i * (middle - box[0] - 40) / 2.0, bottom if i % 2 == 0 else top) for i in range(3)]
    frames = []
    for i, (x, y) in enumerate(spots):
        image, px, py = clipped(paw, x, y, 72 if i % 2 == 0 else 58, mask)
        frames.append(overlay(image))
        positions[("print", i)] = units(px, py)
    symbols["print"] = frames

    egg = egg_image()
    symbols["egg"] = [overlay(egg)]
    egg_base = (egg_x, box[3] - 12)          # the point the egg rocks on
    egg_half = (egg.height - 12) / 2.0

    def egg_element(frame):
        pose = (-16, 0, 16, 0)[(frame // 7) % 4]
        angle = math.radians(pose)
        cx = egg_base[0] + math.sin(angle) * egg_half
        cy = egg_base[1] - math.cos(angle) * egg_half
        x, y = units(cx, cy)
        return ("egg", 0, x, y, {"rotation": pose})

    def glow_elements(frame, side, phase):
        pulse = 0.72 + 0.28 * math.cos(2 * math.pi * (frame / 42.0 + phase))
        gx, gy = positions["glow_" + side]
        tx, ty = positions["tip_" + side]
        return [("glow", 0, gx, gy, {"alpha": pulse}), ("tip_" + side, 0, tx, ty)]

    def print_elements(frame, key, count, step):
        elements = []
        for i in range(count):
            alpha = fade(frame, 4 + i * step)
            if alpha > 0:
                x, y = positions[(key, i)]
                elements.append((key, i, x, y, {"alpha": alpha}))
        return elements

    body = ("body", 0, CENTER[0], CENTER[1])
    states = {
        "off": lambda f: [],
        "on_combined": lambda f: glow_elements(f, "l", 0) + glow_elements(f, "r", 0.35) + print_elements(f, "print", 3, 14),
        "on_critter": lambda f: glow_elements(f, "l", 0) + print_elements(f, "print", 3, 14),
        "on_egg": lambda f: glow_elements(f, "r", 0.35) + [egg_element(f)],
        "on_both": lambda f: glow_elements(f, "l", 0) + glow_elements(f, "r", 0.35) + print_elements(f, "print", 3, 14) + [egg_element(f)],
    }
    anims = {}
    for name, build in states.items():
        count = 1 if name == "off" else LOOP
        anims[name] = [build(f) + [body] for f in range(count)]
    anims["place"] = [[("place", 0, CENTER[0], CENTER[1])]]
    anims["ui"] = [[("ui", 0, CENTER[0], CENTER[1])]]
    write_kanim(OUT, NAME, symbols, anims, texture_size=512)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
