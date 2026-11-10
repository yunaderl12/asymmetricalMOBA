# Draws the placeholder field spell illustrations (same low-poly flat style and outline as the skill icons) and writes
# one transparent PNG per spell to Assets/_Project/Art/FieldSpells/<FieldSpellData asset name>.png. Standard library only.
# Run:     python3 ArtSource/skill_icons/build_field_spell_icons.py
# Preview: python3 ArtSource/skill_icons/build_field_spell_icons.py --preview /path/to/sheet.png   (all icons on their felt colours)
# Then in Unity run Tools/Skills/Assign Field Spell Icons, which sets FieldSpellData.icon from the file names.
# Art space is 0..100 with y down. Keep shapes inside a circle of radius ~46 around (50, 50): the UI places the
# picture inside a round felt token.
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_skill_icons import SIZE, SS, Canvas, Icon, ngon, rgb, rotate, star, write_png  # noqa: E402


def cloud(ic, light, dark, y, scale=1.0):
    """A puffy low-poly cloud centred on x 50 whose underside is at about y."""
    s = scale
    ic.poly(dark, [(50 - 40 * s, y), (50 + 40 * s, y), (50 + 42 * s, y - 8 * s), (50 - 42 * s, y - 8 * s)])
    for cx, cy, r in ((-27, -10, 13), (-8, -18, 17), (14, -15, 15), (30, -8, 12)):
        ic.poly(dark, ngon(50 + cx * s, y + cy * s, r * s, n=9))
    for cx, cy, r in ((-27, -12, 9), (-9, -21, 12), (12, -18, 10)):
        ic.poly(light, ngon(50 + cx * s - 1.5 * s, y + cy * s - 1.5 * s, r * s, n=9))


def tornado():
    ic = Icon()
    # Stacked rings, widest at the top, swaying a little to the side as they go down.
    rings = 7
    for k in reversed(range(rings)):
        t = k / (rings - 1)
        y = 18 + t * 66
        hw = 40 * (1 - t) + 6
        cx = 50 + math.sin(t * 2.6) * 7
        ic.poly("7E9AA8", ngon(cx, y, hw, hw * 0.3, n=14))
        ic.poly("DCEBF0", ngon(cx - hw * 0.06, y - hw * 0.05, hw * 0.88, hw * 0.2, n=14))
    # Rocks thrown around.
    for x, y, r, rot in ((12, 48, 5.5, 10), (86, 38, 4.5, 40), (80, 70, 4, 0)):
        ic.poly("6B4A33", ngon(x, y, r, n=5, rot=rot))
        ic.poly("9C7552", ngon(x - 1, y - 1, r * 0.6, n=5, rot=rot))
    return ic


def lightning():
    ic = Icon()
    bolt = [(50, 30), (68, 30), (58, 52), (72, 52), (38, 96), (46, 62), (32, 62)]
    ic.faceted("FFF4A8", "F5C518", bolt)
    cloud(ic, "8C95AB", "4C5368", 38)
    return ic


def earthquake():
    ic = Icon()
    # Ground split in two; the right block has dropped.
    ic.poly("7A4E27", [(8, 58), (47, 51), (44, 86), (10, 84)])
    ic.poly("9C6A38", [(8, 58), (47, 51), (46, 62), (9, 68)])
    ic.poly("5FA040", [(8, 47), (49, 40), (47, 51), (8, 58)])
    ic.poly("86C95A", [(8, 47), (49, 40), (48, 45), (8, 52)])
    ic.poly("7A4E27", [(55, 67), (92, 60), (90, 90), (57, 92)])
    ic.poly("9C6A38", [(55, 67), (92, 60), (91, 70), (56, 77)])
    ic.poly("5FA040", [(53, 57), (92, 50), (92, 60), (55, 67)])
    ic.poly("86C95A", [(53, 57), (92, 50), (92, 54), (54, 61)])
    # Stones jumping up and shake lines.
    for x, y, r in ((30, 26, 5), (52, 18, 4), (70, 32, 4.5)):
        ic.poly("6B4A33", ngon(x, y, r, n=5, rot=20))
        ic.poly("A47E58", ngon(x - 1, y - 1, r * 0.55, n=5, rot=20))
    for side in (-1, 1):
        for k, (dy, ln) in enumerate(((0, 9), (9, 7))):
            x0 = 50 + side * 36
            ic.line("FFFFFF", [(x0, 20 + dy), (x0 + side * ln, 24 + dy)], 3.2)
    return ic


def meteor():
    ic = Icon()
    # Flame trail from the upper right, then the rock.
    ic.poly("E8551A", [(26, 50), (82, 6), (94, 18), (52, 76)])
    ic.poly("F7931E", [(32, 52), (80, 14), (86, 22), (50, 68)])
    ic.poly("FFD23C", [(38, 56), (76, 22), (79, 26), (48, 62)])
    rock = ngon(38, 64, 22, n=8, rot=12)
    ic.faceted("8C6550", "5A3D2F", rock)
    ic.line("FF8A2A", [(28, 58), (37, 64), (34, 74)], 2.8)
    ic.line("FF8A2A", [(42, 54), (47, 62)], 2.4)
    return ic


def manaburst():
    ic = Icon()
    ic.poly("C68BFF", star(50, 50, 46, 31, 8, rot=-90))
    ic.poly("E3C2FF", star(50, 50, 38, 28, 8, rot=-67.5))
    orb = ngon(50, 50, 25, n=12)
    ic.faceted("A85CF0", "6E28C2", orb)
    ic.poly("D7AEFF", ngon(43, 42, 9, n=8))
    ic.poly("FFFFFF", star(44, 41, 8, 2, 4))
    return ic


def downpour():
    ic = Icon()
    for x, y in ((24, 52), (38, 60), (52, 50), (66, 58), (80, 50), (30, 76), (46, 84), (62, 76), (76, 84)):
        ic.line("8FD0FF", [(x, y), (x - 6, y + 12)], 3.4)
    for x, y in ((40, 90), (70, 66)):
        drop = [(x, y - 9), (x + 5, y), (x + 3.5, y + 4), (x, y + 5.5), (x - 3.5, y + 4), (x - 5, y)]
        ic.faceted("8FD0FF", "3D8FDB", drop)
    cloud(ic, "7A88A6", "3E4862", 44, 1.08)
    return ic


ICONS = {
    "Tornado": tornado, "Lightning": lightning, "Earthquake": earthquake,
    "Meteor": meteor, "ManaBurst": manaburst, "Downpour": downpour,
}
# Felt colours of FieldSpellData.color (preview sheet only).
PREVIEW_BG = {"Tornado": "5C8C99", "Lightning": "8C8026", "Earthquake": "805933", "Meteor": "994726", "ManaBurst": "803399",
              "Downpour": "335999"}


def preview(path):
    cell, f = 240, 2
    names = list(ICONS)
    c = Canvas(3 * cell * f, 2 * cell * f)
    for k, name in enumerate(names):
        x0, y0 = (k % 3) * cell * f, (k // 3) * cell * f
        d = 210 * f
        c.fill(rgb(PREVIEW_BG[name]), ngon(x0 + cell * f / 2, y0 + cell * f / 2, d / 2, n=48))
        # Same placement as LobbyStyle.IconToken: 80% of the token.
        box = d * 0.8
        c.draw_icon(ICONS[name](), box / 100, x0 + (cell * f - box) / 2, y0 + (cell * f - box) / 2)
    write_png(path, c.downsample(f))


def main():
    if "--preview" in sys.argv:
        preview(sys.argv[sys.argv.index("--preview") + 1])
        return
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    out_dir = os.path.join(root, "Assets", "_Project", "Art", "FieldSpells")
    os.makedirs(out_dir, exist_ok=True)
    for name, build in ICONS.items():
        c = Canvas(SIZE * SS, SIZE * SS)
        c.draw_icon(build(), SIZE * SS / 100)
        write_png(os.path.join(out_dir, name + ".png"), c.downsample(SS))
        print("wrote", name)


if __name__ == "__main__":
    main()
