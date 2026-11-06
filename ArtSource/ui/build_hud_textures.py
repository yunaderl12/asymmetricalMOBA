# HUDの布（フェルト）と紙の模様を作る。どちらも 256px の繰り返し画像で、色はUI側で掛け合わせる。
# 使い方: python3 -I ArtSource/ui/build_hud_textures.py
import math, os, random, struct, zlib

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', '_Project', 'Resources', 'UI')
N = 256


def write_png(path, rows):
    raw = b''.join(b'\x00' + bytes(r) for r in rows)

    def chunk(tag, data):
        body = tag + data
        return struct.pack('>I', len(data)) + body + struct.pack('>I', zlib.crc32(body) & 0xffffffff)

    header = struct.pack('>IIBBBBB', N, N, 8, 6, 0, 0, 0)
    with open(path, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', header) + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


def fibers(seed, count, length, strength):
    """曲がった短い繊維をばらまく。値は明るさの増減で、端は反対側へつながる。"""
    random.seed(seed)
    grid = [[0.0] * N for _ in range(N)]
    for _ in range(count):
        x, y = random.uniform(0, N), random.uniform(0, N)
        angle = random.uniform(0, math.tau)
        amount = random.choice((-1, 1)) * random.uniform(0.3, 1.0) * strength
        for _ in range(length):
            angle += random.uniform(-0.35, 0.35)
            x += math.cos(angle)
            y += math.sin(angle)
            grid[int(y) % N][int(x) % N] += amount
    return grid


def save(name, grid, base):
    """base を中心に明るさを揺らした不透明な灰色。UIの色にそのまま掛ける。"""
    rows = []
    for row in grid:
        line = []
        for v in row:
            c = int(round(max(0.0, min(1.0, base + v)) * 255))
            line += [c, c, c, 255]
        rows.append(line)
    write_png(os.path.join(OUT, name), rows)


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    save('HudFelt.png', fibers(3, 5200, 14, 0.07), 0.93)
    save('HudPaper.png', fibers(5, 2600, 9, 0.035), 0.97)
    print('wrote', os.path.normpath(OUT))
