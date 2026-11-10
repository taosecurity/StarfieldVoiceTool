"""Draw the Starfield Voice Tool icon from scratch and write the .ico and a PNG preview.

The icon is original artwork made only from the shapes below (no game art): a dark blue rounded
square with a few stars, a speech bubble holding a sound wave, and a small four-point sparkle.

Usage (needs Pillow):
    python art/make_icon.py
Writes StarfieldVT/starfieldvt.ico (16-256 px) and art/starfieldvt-256.png.
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

S = 1024  # draw large, then scale down for smooth edges
ROOT = Path(__file__).resolve().parent.parent

SPACE_TOP = (16, 28, 64)
SPACE_BOTTOM = (34, 56, 112)
BUBBLE = (242, 245, 250)
WAVE = (245, 166, 35)
STAR = (255, 255, 255)


def rounded_mask(size, radius):
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, size - 1, size - 1), radius=radius, fill=255)
    return mask


def background(simple):
    bg = Image.new("RGBA", (S, S))
    draw = ImageDraw.Draw(bg)
    for y in range(S):
        t = y / (S - 1)
        color = tuple(round(a + (b - a) * t) for a, b in zip(SPACE_TOP, SPACE_BOTTOM))
        draw.line((0, y, S, y), fill=color + (255,))
    # a few small stars (left out of the tiny sizes, where they'd only be noise)
    stars = [] if simple else [(140, 150, 9), (300, 95, 6), (860, 860, 8), (120, 820, 6), (900, 470, 5), (560, 110, 5)]
    for x, y, r in stars:
        draw.ellipse((x - r, y - r, x + r, y + r), fill=STAR + (220,))
    out = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    out.paste(bg, (0, 0), rounded_mask(S, int(S * 0.22)))
    return out


def sparkle(draw, cx, cy, r, color):
    """A four-point star: two thin diamonds crossed."""
    w = r * 0.28
    draw.polygon([(cx, cy - r), (cx + w, cy), (cx, cy + r), (cx - w, cy)], fill=color)
    draw.polygon([(cx - r, cy), (cx, cy - w), (cx + r, cy), (cx, cy + w)], fill=color)


def icon(simple=False):
    """simple=True: the 16 and 24 px version, with three thick bars and no stars."""
    img = background(simple)
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)

    # speech bubble with a tail at the lower left
    box = (170, 250, 854, 700)
    draw.rounded_rectangle(box, radius=150, fill=BUBBLE + (255,))
    draw.polygon([(300, 640), (430, 690), (240, 850)], fill=BUBBLE + (255,))

    # sound wave: rounded bars, tallest in the middle
    if simple:
        heights, bar_w, gap = [190, 330, 230], 120, 75
    else:
        heights, bar_w, gap = [120, 230, 330, 230, 300, 170, 100], 56, 30
    total = len(heights) * bar_w + (len(heights) - 1) * gap
    x = (box[0] + box[2]) / 2 - total / 2
    cy = (box[1] + box[3]) / 2
    for h in heights:
        draw.rounded_rectangle((x, cy - h / 2, x + bar_w, cy + h / 2), radius=bar_w / 2, fill=WAVE + (255,))
        x += bar_w + gap

    # soft shadow under the bubble, then the bubble layer
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    shadow.paste((0, 0, 0, 110), (0, 0), layer.split()[3])
    shadow = shadow.filter(ImageFilter.GaussianBlur(18)).transform(
        (S, S), Image.AFFINE, (1, 0, -10, 0, 1, -18))
    img = Image.alpha_composite(img, shadow)
    img = Image.alpha_composite(img, layer)

    # sparkle at the upper right, overlapping the bubble's corner
    top = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    sparkle(ImageDraw.Draw(top), 815, 235, 120, STAR + (255,))
    sparkle(ImageDraw.Draw(top), 815, 235, 62, WAVE + (255,))
    return Image.alpha_composite(img, top)


def main():
    big, small = icon(), icon(simple=True)
    sizes = [16, 24, 32, 48, 64, 128, 256]
    frames = [(small if n <= 24 else big).resize((n, n), Image.LANCZOS) for n in sizes]
    ico = ROOT / "StarfieldVT" / "starfieldvt.ico"
    frames[-1].save(ico, format="ICO", sizes=[(n, n) for n in sizes], append_images=frames[:-1])
    png = ROOT / "art" / "starfieldvt-256.png"
    frames[-1].save(png)
    print(f"wrote {ico} ({', '.join(map(str, sizes))} px) and {png}")


if __name__ == "__main__":
    main()
