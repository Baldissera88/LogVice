import sys
from PIL import Image, ImageDraw, ImageFilter

def hexc(h, a=255):
    h = h.lstrip('#'); return (int(h[0:2],16), int(h[2:4],16), int(h[4:6],16), a)

SKY_T, SKY_B = hexc('#140A33'), hexc('#3D0B5E')
SUN_T, SUN_B = hexc('#FFE45C'), hexc('#FF2D95')
GROUND, HORIZON, GRID, RIM = hexc('#12062B'), hexc('#FF9BEA'), hexc('#FF4FD8'), hexc('#FF9BEA', 150)

def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(4))

def mark(n):
    S = 8
    N = n * S
    im = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    inset = 0 if n < 24 else round(n * 0.03)
    rad = max(2, round(n * 0.2))
    tile = Image.new('L', (N, N), 0)
    ImageDraw.Draw(tile).rounded_rectangle([inset * S, inset * S, (n - inset) * S - 1, (n - inset) * S - 1], rad * S, fill=255)
    yh = round(n * 0.60)
    cy, r = n * 0.47, n * 0.31
    top = cy - r
    sky = Image.new('RGBA', (N, N))
    sd = ImageDraw.Draw(sky)
    for y in range(N):
        sd.line([(0, y), (N, y)], fill=lerp(SKY_T, SKY_B, y / N))
    layer = sky.copy()
    d = ImageDraw.Draw(layer)
    if n >= 48:
        for sx, sy, s in [(0.18, 0.16, 1.2), (0.80, 0.13, 1.0), (0.88, 0.36, 0.8), (0.12, 0.42, 0.8), (0.30, 0.07, 0.7)]:
            rr = max(1, n * 0.008 * s) * S
            d.ellipse([sx * N - rr, sy * N - rr, sx * N + rr, sy * N + rr], fill=hexc('#FFFFFF', 200))
    if n >= 32:
        glow = Image.new('RGBA', (N, N), (0, 0, 0, 0))
        g = r * 1.18
        ImageDraw.Draw(glow).ellipse([(n / 2 - g) * S, (cy - g) * S, (n / 2 + g) * S, (cy + g) * S], fill=(255, 60, 160, 95))
        glow = glow.filter(ImageFilter.GaussianBlur(n * 0.06 * S))
        layer = Image.alpha_composite(layer, glow)
        d = ImageDraw.Draw(layer)
    sun = Image.new('RGBA', (N, N))
    sud = ImageDraw.Draw(sun)
    y0, y1 = int(top * S), yh * S
    for y in range(N):
        t = min(1, max(0, (y - y0) / max(1, y1 - y0)))
        sud.line([(0, y), (N, y)], fill=lerp(SUN_T, SUN_B, t))
    sm = Image.new('L', (N, N), 0)
    smd = ImageDraw.Draw(sm)
    smd.ellipse([(n / 2 - r) * S, (cy - r) * S, (n / 2 + r) * S, (cy + r) * S], fill=255)
    smd.rectangle([0, yh * S, N, N], fill=0)
    k = 2 if n < 24 else 3
    for i in range(k):
        if n < 40:
            ys, t = yh - (2 * i + 1), 1
        else:
            f = (i + 0.55) / k
            t = max(1, round(n * (0.012 + 0.008 * i)))
            ys = int(round(cy + (yh - cy) * f - 0.5 - t / 2))
        smd.rectangle([0, ys * S, N, (ys + t) * S - 1], fill=0)
    layer.paste(sun, (0, 0), sm)
    d = ImageDraw.Draw(layer)
    d.rectangle([0, yh * S, N, N], fill=GROUND)
    ht = max(1, round(n / 64))
    d.rectangle([0, yh * S, N, (yh + ht) * S - 1], fill=HORIZON)
    m = 1 if n <= 20 else 2 if n <= 32 else 4
    bottom = n - inset
    lw = max(1, round(n / 90))
    for j in range(m):
        f = ((j + 1) / (m + 0.6)) ** 1.5
        y = int(round(yh + ht + (bottom - yh - ht) * f))
        d.rectangle([0, y * S, N, (y + lw) * S - 1], fill=GRID)
    vx = n / 2
    offs = [-0.45, 0.45] if n <= 16 else [-0.95, -0.32, 0.32, 0.95] if n <= 32 else [-1.6, -0.9, -0.42, 0, 0.42, 0.9, 1.6]
    for o in offs:
        d.line([(vx * S, (yh + ht) * S), ((vx + o * n * 0.55) * S, n * S)], fill=GRID, width=int(lw * S * (0.8 if n > 32 else 1)))
    out = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    out.paste(layer, (0, 0), tile)
    if n >= 32:
        od = ImageDraw.Draw(out)
        w = max(1, round(n / 85))
        od.rounded_rectangle([inset * S + w * S / 2, inset * S + w * S / 2, (n - inset) * S - w * S / 2, (n - inset) * S - w * S / 2], rad * S, outline=RIM, width=int(w * S))
    return out.resize((n, n), Image.BOX)

SIZES = [16, 20, 24, 32, 40, 48, 64, 256]

def preview(path):
    W, H = 1180, 620
    sheet = Image.new('RGBA', (W, H), hexc('#F3F3F3'))
    d = ImageDraw.Draw(sheet)
    d.rounded_rectangle([600, 20, 1160, 600], 16, fill=hexc('#202020'))
    sheet.alpha_composite(mark(256), (170, 40))
    sheet.alpha_composite(mark(256), (750, 40))
    for bx, by in [(40, 330), (620, 330)]:
        x = bx
        for s in [64, 48, 40, 32, 24, 20, 16]:
            sheet.alpha_composite(mark(s), (x, by + 64 - s))
            x += s + 18
        x = bx
        for s in [16, 24, 32]:
            z = mark(s).resize((s * 6 if s == 16 else s * 4 if s == 24 else s * 3,) * 2, Image.NEAREST)
            sheet.alpha_composite(z, (x, by + 100))
            x += z.width + 24
    sheet.convert('RGB').save(path)

if __name__ == '__main__':
    if sys.argv[1] == 'preview':
        preview(sys.argv[2])
    else:
        frames = [mark(s) for s in SIZES]
        frames[-1].save(sys.argv[2], format='ICO', sizes=[(s, s) for s in SIZES], append_images=frames[:-1])
        frames[-1].save(sys.argv[3], format='PNG')
