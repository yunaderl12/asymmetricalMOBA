# Draws the placeholder skill illustrations (low-poly flat style) and writes one transparent PNG per skill to
# Assets/_Project/Art/Skills/<SkillData asset name>.png. Standard library only.
# Run:     python3 ArtSource/skill_icons/build_skill_icons.py
# Preview: python3 ArtSource/skill_icons/build_skill_icons.py --preview /path/to/sheet.png   (all icons on their diamonds)
# Then in Unity run Tools/Skills/Assign Skill Icons, which sets SkillData.icon from the file names.
# Art space is 0..100 with y down. Keep shapes inside a circle of radius ~46 around (50, 50): the UI places that
# circle inside the diamond (see SkillIconView.cs).
import math
import os
import struct
import sys
import zlib

SIZE = 256
SS = 4
OUTLINE = "1B1724"
OUTLINE_W = 2.3


def rgb(h):
    return bytes(int(h[i:i + 2], 16) for i in (0, 2, 4))


# ---------------------------------------------------------------- shape helpers
def ngon(cx, cy, rx, ry=None, n=8, rot=0.0):
    ry = rx if ry is None else ry
    return [(cx + math.cos(math.radians(rot) + k / n * math.tau) * rx,
             cy + math.sin(math.radians(rot) + k / n * math.tau) * ry) for k in range(n)]


def rotate(pts, deg, c=(50, 50)):
    a = math.radians(deg)
    ca, sa = math.cos(a), math.sin(a)
    return [(c[0] + (x - c[0]) * ca - (y - c[1]) * sa, c[1] + (x - c[0]) * sa + (y - c[1]) * ca) for x, y in pts]


def mirror(pts, x0=50):
    return [(2 * x0 - x, y) for x, y in pts]


def star(cx, cy, r1, r2, n, rot=-90.0):
    return [(cx + math.cos(math.radians(rot) + k * math.pi / n) * (r1 if k % 2 == 0 else r2),
             cy + math.sin(math.radians(rot) + k * math.pi / n) * (r1 if k % 2 == 0 else r2)) for k in range(n * 2)]


def segment(a, b, w):
    dx, dy = b[0] - a[0], b[1] - a[1]
    ln = math.hypot(dx, dy) or 1.0
    nx, ny = -dy / ln * w / 2, dx / ln * w / 2
    return [(a[0] + nx, a[1] + ny), (b[0] + nx, b[1] + ny), (b[0] - nx, b[1] - ny), (a[0] - nx, a[1] - ny)]


class Icon:
    def __init__(self):
        self.polys = []

    def poly(self, color, pts):
        self.polys.append((rgb(color), list(pts)))
        return self

    def line(self, color, pts, w):
        for a, b in zip(pts, pts[1:]):
            self.poly(color, segment(a, b, w))
        for p in pts:
            self.poly(color, ngon(p[0], p[1], w / 2, n=8))
        return self

    def faceted(self, light, dark, pts):
        """Polygon split down its vertical centre line into a lit left half and a shaded right half."""
        self.poly(dark, pts)
        cx = sum(p[0] for p in pts) / len(pts)
        left = []
        for a, b in zip(pts, pts[1:] + pts[:1]):
            if a[0] <= cx:
                left.append(a)
            if (a[0] - cx) * (b[0] - cx) < 0:
                t = (cx - a[0]) / (b[0] - a[0])
                left.append((cx, a[1] + (b[1] - a[1]) * t))
        return self.poly(light, left)

    def cross(self, light, dark, cx, cy, size, arm=0.36):
        s, a = size / 2, size * arm / 2
        pts = [(cx - a, cy - s), (cx + a, cy - s), (cx + a, cy - a), (cx + s, cy - a), (cx + s, cy + a), (cx + a, cy + a),
               (cx + a, cy + s), (cx - a, cy + s), (cx - a, cy + a), (cx - s, cy + a), (cx - s, cy - a), (cx - a, cy - a)]
        self.poly(dark, pts)
        self.poly(light, [(cx - a, cy - s), (cx + a, cy - s), (cx + a, cy - a), (cx + s, cy - a), (cx + s, cy + a * 0.1),
                          (cx - s, cy + a * 0.1), (cx - s, cy - a), (cx - a, cy - a)])
        return self

    def spiral_blade(self, a0, light, dark, r0=13, r1=47, span=150, w0=15):
        outer, inner = [], []
        steps = 9
        for k in range(steps + 1):
            t = k / steps
            a = math.radians(a0 + span * t)
            r = r0 + (r1 - r0) * t
            w = w0 * (1 - t) + 0.6
            outer.append((50 + math.cos(a) * r, 50 + math.sin(a) * r))
            inner.append((50 + math.cos(a) * (r - w), 50 + math.sin(a) * (r - w)))
        self.poly(dark, outer + inner[::-1])
        mid = [((o[0] + i[0]) / 2, (o[1] + i[1]) / 2) for o, i in zip(outer, inner)]
        return self.poly(light, outer + mid[::-1])


# ---------------------------------------------------------------- the 20 skills
def sword(ic, tf):
    """Upright sword (tip at the top) passed through the transform tf."""
    ic.poly("A9B5C4", tf([(50, 5), (59, 17), (59, 64), (41, 64), (41, 17)]))
    ic.poly("EEF3F9", tf([(50, 5), (50, 64), (41, 64), (41, 17)]))
    ic.poly("B9852A", tf([(30, 64), (70, 64), (70, 72), (30, 72)]))
    ic.poly("EDBE4C", tf([(30, 64), (70, 64), (70, 68), (30, 68)]))
    ic.poly("6B4226", tf([(45, 72), (55, 72), (55, 88), (45, 88)]))
    ic.poly("EDBE4C", tf(ngon(50, 91, 6.5, n=6)))


def slash():
    ic = Icon()
    outer = [(50 + math.cos(math.radians(a)) * 45, 50 + math.sin(math.radians(a)) * 45) for a in range(165, 286, 15)]
    inner = [(50 + math.cos(math.radians(a)) * (45 - 10 * math.sin((a - 165) / 120 * math.pi)),
              50 + math.sin(math.radians(a)) * (45 - 10 * math.sin((a - 165) / 120 * math.pi))) for a in range(165, 286, 15)]
    ic.poly("FFF3B0", outer + inner[::-1])
    sword(ic, lambda p: rotate(p, 45))
    return ic


def charge():
    ic = Icon()
    for y, x0 in ((30, 16), (50, 5), (70, 16)):
        ic.line("FFD45E", [(x0, y), (x0 + 19, y)], 7)
    sword(ic, lambda p: [(x * 0.92 + 4, y) for x, y in rotate(p, 90)])
    return ic


def whirlwind():
    ic = Icon()
    for a0 in (0, 120, 240):
        ic.spiral_blade(a0, "EEF3F9", "93A1B3")
    ic.poly("B9852A", ngon(50, 50, 10, n=6))
    ic.poly("EDBE4C", ngon(50, 49, 7, n=6))
    return ic


def guard():
    ic = Icon()
    shield = [(50, 5), (84, 14), (84, 46), (72, 72), (50, 95), (28, 72), (16, 46), (16, 14)]
    ic.faceted("F1C95A", "B9852A", shield)
    inner = [(50 + (x - 50) * 0.78, 50 + (y - 50) * 0.8) for x, y in shield]
    ic.faceted("5E9EEC", "2F6BB8", inner)
    ic.faceted("F1C95A", "D29C34", [(50, 28), (63, 46), (50, 66), (37, 46)])
    return ic


def sprint():
    ic = Icon()
    for (x, y, l, w) in ((34, 26, 30, 11), (34, 39, 25, 10), (34, 51, 19, 9)):
        ic.poly("D8E6F5", [(x, y - w / 2), (x - l, y - w), (x - l + 6, y + 1), (x, y + w / 2)])
        ic.poly("FFFFFF", [(x, y - w / 2), (x - l, y - w), (x - l + 3, y - w / 2), (x, y)])
    ic.poly("A33A2A", [(34, 16), (62, 16), (62, 50), (88, 64), (90, 76), (34, 76)])
    ic.poly("D65B3F", [(34, 16), (48, 16), (48, 58), (76, 66), (88, 64), (90, 70), (34, 70)])
    ic.poly("F1C95A", [(32, 14), (64, 14), (64, 22), (32, 22)])
    ic.poly("3A2518", [(32, 74), (92, 74), (92, 84), (32, 84)])
    return ic


def fireball():
    ic = Icon()
    ic.poly("F2711C", [(14, 60), (20, 40), (34, 28), (44, 30), (52, 8), (60, 26), (80, 6), (77, 32), (93, 28), (79, 52),
                       (74, 70), (60, 84), (40, 90), (24, 82)])
    ic.poly("C4380D", [(14, 60), (24, 82), (40, 90), (60, 84), (74, 70), (61, 75), (44, 82), (28, 74)])
    ic.poly("FFC93C", [(28, 62), (34, 46), (46, 40), (54, 28), (58, 42), (69, 36), (66, 56), (60, 70), (46, 78), (34, 74)])
    ic.poly("FFF3B0", [(38, 62), (44, 51), (54, 50), (57, 61), (48, 70)])
    return ic


def blizzard():
    ic = Icon()
    for k in range(6):
        ic.line("E6F7FF", rotate([(50, 50), (50, 6)], k * 60), 6.5)
        ic.line("BDEBFF", rotate([(50, 22), (39, 11)], k * 60), 4.5)
        ic.line("BDEBFF", rotate([(50, 22), (61, 11)], k * 60), 4.5)
    ic.poly("7CCBFA", ngon(50, 50, 12, n=6, rot=30))
    ic.poly("E6F7FF", ngon(50, 50, 6, n=6, rot=30))
    return ic


def teleport():
    ic = Icon()
    for x in (6, 20):
        ic.line("7DF3FA", [(x, 34), (x + 12, 50), (x, 66)], 6.5)
    ic.poly("35B7D6", ngon(64, 50, 29, 44, n=12))
    ic.poly("7DF3FA", ngon(62, 50, 26, 41, n=12))
    ic.poly("2A1B5E", ngon(63, 50, 17, 31, n=10))
    ic.poly("4B2F9E", ngon(63, 52, 11, 22, n=8))
    ic.poly("FFFFFF", star(63, 50, 15, 3.5, 4))
    return ic


def manashield():
    ic = Icon()
    hexa = ngon(50, 50, 46, n=6, rot=-90)
    for k in range(6):
        ic.poly("8AE6FF" if k % 2 == 0 else "3FA8E8", [(50, 50), hexa[k], hexa[(k + 1) % 6]])
    ic.poly("1E4FA3", ngon(50, 50, 27, n=6, rot=-90))
    ic.poly("FF9BF0", star(50, 51, 20, 8, 5))
    ic.poly("FFFFFF", star(50, 51, 10, 4, 5))
    return ic


def heal():
    return Icon().cross("7CF29B", "28B457", 50, 50, 78, 0.38)


def groupheal():
    ic = Icon()
    ic.cross("7CF29B", "28B457", 27, 65, 36, 0.4)
    ic.cross("7CF29B", "28B457", 73, 65, 36, 0.4)
    ic.cross("B5FFC8", "3FCF6E", 50, 34, 50, 0.4)
    return ic


def blessing():
    ic = Icon()
    wing = [(46, 44), (32, 30), (6, 28), (12, 42), (4, 46), (14, 56), (9, 62), (24, 68), (34, 66), (46, 58)]
    for pts in (wing, mirror(wing)):
        ic.poly("BCD0E8", pts)
    for pts in ([(46, 44), (32, 30), (6, 28), (12, 42), (30, 46), (46, 52)],):
        ic.poly("FFFFFF", pts)
        ic.poly("FFFFFF", mirror(pts))
    ic.faceted("F1C95A", "CF9A2F", [(50, 38), (60, 52), (50, 76), (40, 52)])
    ring = ngon(50, 20, 21, 8, n=12)
    ic.line("FFE27A", ring + ring[:1], 5)
    return ic


def smite():
    ic = Icon()
    ic.poly("F2A900", [(60, 4), (24, 54), (46, 54), (34, 96), (80, 38), (56, 38), (74, 4)])
    ic.poly("FFE066", [(60, 4), (24, 54), (46, 54), (34, 96), (54, 46), (40, 46), (64, 10)])
    return ic


def bash():
    ic = Icon()
    ic.poly("FFD45E", star(28, 30, 26, 12, 8, rot=-70))
    ic.poly("FFF3B0", star(28, 30, 13, 6, 8, rot=-70))
    tf = lambda p: rotate(p, 40)  # noqa: E731
    ic.poly("6B4226", tf([(45, 36), (55, 36), (55, 95), (45, 95)]))
    ic.poly("9A6A3F", tf([(45, 36), (50, 36), (50, 95), (45, 95)]))
    ic.poly("8996A6", tf([(22, 12), (78, 12), (78, 40), (22, 40)]))
    ic.poly("D5DDE6", tf([(22, 12), (78, 12), (78, 24), (22, 24)]))
    ic.poly("EDBE4C", tf([(43, 12), (57, 12), (57, 40), (43, 40)]))
    return ic


def taunt():
    ic = Icon()
    ic.poly("B8211F", star(50, 50, 47, 31, 10))
    ic.poly("F24A3D", star(50, 49, 40, 26, 10))
    ic.poly("FFFFFF", [(42, 20), (58, 20), (55, 60), (45, 60)])
    ic.poly("FFFFFF", ngon(50, 72, 8, n=8))
    return ic


def stomp():
    ic = Icon()
    ic.poly("6B4A2C", [(9, 68), (91, 68), (84, 78), (66, 76), (58, 91), (50, 78), (40, 92), (34, 77), (16, 78)])
    ic.poly("9A7446", [(9, 68), (91, 68), (88, 72), (12, 72)])
    for x0, x1 in ((7, 20), (93, 80)):
        ic.line("FFD45E", [(x0, 48), (x1, 60)], 5)
    ic.line("FFD45E", [(13, 32), (23, 47)], 5)
    ic.line("FFD45E", [(87, 32), (77, 47)], 5)
    ic.poly("6E7A89", [(36, 4), (64, 4), (64, 34), (76, 42), (78, 66), (26, 66), (26, 34), (36, 30)])
    ic.poly("B4BFCC", [(36, 4), (50, 4), (50, 36), (60, 44), (64, 60), (26, 60), (26, 34), (36, 30)])
    ic.poly("EDBE4C", [(34, 16), (66, 16), (66, 23), (34, 23)])
    ic.poly("3A3F48", [(24, 62), (80, 62), (80, 69), (24, 69)])
    return ic


def ironwall():
    ic = Icon()
    wall = [(22, 8), (33, 8), (33, 18), (44, 18), (44, 8), (56, 8), (56, 18), (67, 18), (67, 8), (78, 8), (78, 78), (22, 78)]
    ic.poly("8D99A6", wall)
    ic.poly("C6CFD9", [(22, 8), (33, 8), (33, 18), (44, 18), (44, 8), (50, 8), (50, 78), (22, 78)])
    joints = []
    for y in (31, 46, 61):
        joints.append(((25, y), (75, y)))
    for y0, y1, xs in ((18, 31, (50,)), (31, 46, (36, 64)), (46, 61, (50,)), (61, 78, (36, 64))):
        joints.extend(((x, y0), (x, y1 - 2)) for x in xs)
    for a, b in joints:
        ic.poly("59636F", segment(a, b, 1.2))
    return ic


def recover():
    ic = Icon()
    heart = []
    for k in range(24):
        t = k / 24 * math.tau
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        heart.append((50 + x * 2.5, 47 - y * 2.5))
    ic.faceted("FF6B7D", "D32F4B", heart)
    ic.cross("FFFFFF", "D9F7E2", 50, 47, 34, 0.36)
    return ic


def detect():
    ic = Icon()
    eye = [(4, 50), (16, 36), (32, 26), (50, 22), (68, 26), (84, 36), (96, 50), (84, 64), (68, 74), (50, 78), (32, 74), (16, 64)]
    ic.poly("C9D6E8", eye)
    ic.poly("FFFFFF", eye[:7] + [(68, 58), (32, 58)])
    ic.poly("1F6FC2", ngon(50, 50, 21, n=10))
    ic.poly("47A9FF", ngon(50, 47, 16, n=10))
    ic.poly("10162A", ngon(50, 50, 9, n=8))
    ic.poly("FFFFFF", ngon(44, 43, 4.5, n=6))
    return ic


def warppoint():
    ic = Icon()
    head = ngon(50, 37, 31, n=12, rot=15)
    tip = [(24, 53), (50, 96), (76, 53)]
    ic.poly("C93A1E", tip)
    ic.poly("F2613A", [(24, 53), (50, 96), (50, 53)])
    ic.faceted("FF8A5C", "D9431F", head)
    ic.poly("FFFFFF", ngon(50, 37, 15, n=10))
    ic.poly("2FC2D8", ngon(50, 37, 8, n=8))
    return ic


ICONS = {
    "Slash": slash, "Charge": charge, "Whirlwind": whirlwind, "Guard": guard,
    "Fireball": fireball, "Blizzard": blizzard, "Teleport": teleport, "ManaShield": manashield,
    "Heal": heal, "GroupHeal": groupheal, "Blessing": blessing, "Smite": smite,
    "Bash": bash, "Taunt": taunt, "Stomp": stomp, "IronWall": ironwall,
    "Sprint": sprint, "Recover": recover, "Detect": detect, "WarpPoint": warppoint,
}
# Diamond colours for the preview sheet only.
PREVIEW_BG = ["B02323"] * 4 + ["B233F2"] * 4 + ["3399FF"] * 4 + ["33CC55"] * 4 + ["6B8CFF", "4DD96B", "6B8CFF", "4DD9E6"]


# ---------------------------------------------------------------- rasteriser
class Canvas:
    """RGBA rows; polygons are filled opaque with even-odd scanlines (painter's order)."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.rows = [bytearray(w * 4) for _ in range(h)]

    def fill(self, color, pts):
        pix = color + b"\xff"
        ys = [p[1] for p in pts]
        for y in range(max(0, int(min(ys))), min(self.h, int(max(ys)) + 2)):
            sy = y + 0.5
            xs = []
            for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]):
                if (y0 <= sy) != (y1 <= sy):
                    xs.append(x0 + (sy - y0) / (y1 - y0) * (x1 - x0))
            xs.sort()
            row = self.rows[y]
            for a, b in zip(xs[0::2], xs[1::2]):
                i0, i1 = max(0, int(a + 0.5)), min(self.w, int(b + 0.5))
                if i1 > i0:
                    row[i0 * 4:i1 * 4] = pix * (i1 - i0)

    def draw_icon(self, ic, scale, ox=0.0, oy=0.0):
        out = rgb(OUTLINE)
        d = OUTLINE_W
        offsets = [(math.cos(k / 12 * math.tau) * d, math.sin(k / 12 * math.tau) * d) for k in range(12)]
        for _, pts in ic.polys:
            for dx, dy in offsets:
                self.fill(out, [((x + dx) * scale + ox, (y + dy) * scale + oy) for x, y in pts])
        for color, pts in ic.polys:
            self.fill(color, [(x * scale + ox, y * scale + oy) for x, y in pts])

    def downsample(self, f):
        rows = []
        for y in range(self.h // f):
            src = self.rows[y * f:(y + 1) * f]
            out = bytearray(self.w // f * 4)
            for x in range(self.w // f):
                r = g = b = a = 0
                for s in src:
                    for i in range(x * f * 4, (x + 1) * f * 4, 4):
                        sa = s[i + 3]
                        if sa:
                            r += s[i] * sa
                            g += s[i + 1] * sa
                            b += s[i + 2] * sa
                            a += sa
                if a:
                    out[x * 4:x * 4 + 4] = bytes((r // a, g // a, b // a, a // (f * f)))
            rows.append(out)
        return rows


def write_png(path, rows):
    w, h = len(rows[0]) // 4, len(rows)
    raw = b"".join(b"\x00" + bytes(r) for r in rows)

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xffffffff)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def preview(path):
    cell, f = 240, 2
    names = list(ICONS)
    c = Canvas(5 * cell * f, 4 * cell * f)
    for k, name in enumerate(names):
        x0, y0 = (k % 5) * cell * f, (k // 5) * cell * f
        d = 210 * f
        px, py = x0 + (cell * f - d) / 2, y0 + (cell * f - d) / 2
        kite = [(0.0, 0.3492), (0.5009, 1.0), (1.0, 0.348), (0.5009, 0.0)]
        c.fill(rgb(PREVIEW_BG[k]), [(px + x * d, py + y * d) for x, y in kite])
        # Same placement as SkillIconView: 76% of the diamond, centred 42.7% from its top.
        box = d * 0.76
        c.draw_icon(ICONS[name](), box / 100, px + (d - box) / 2, py + d * 0.427 - box / 2)
    write_png(path, c.downsample(f))


def main():
    if "--preview" in sys.argv:
        preview(sys.argv[sys.argv.index("--preview") + 1])
        return
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    out_dir = os.path.join(root, "Assets", "_Project", "Art", "Skills")
    os.makedirs(out_dir, exist_ok=True)
    for name, build in ICONS.items():
        c = Canvas(SIZE * SS, SIZE * SS)
        c.draw_icon(build(), SIZE * SS / 100)
        write_png(os.path.join(out_dir, name + ".png"), c.downsample(SS))
        print("wrote", name)


if __name__ == "__main__":
    main()
