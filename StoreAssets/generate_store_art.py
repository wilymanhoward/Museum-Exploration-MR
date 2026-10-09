import math, os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = r"D:/Xiamen/Museum Mixed Reality Project/Museum Exploration MR"
OUT = ROOT + "/StoreAssets"
FONT = ROOT + "/Assets/Fonts/Cardo-Regular.ttf"

NAVY_IN, NAVY_OUT = (30, 52, 78), (8, 17, 30)
GOLD, GOLD_LIGHT, GOLD_DARK = (214, 176, 92), (240, 214, 150), (168, 128, 52)
CYAN = (0, 212, 255)

def bez(p0, p1, p2, p3, n=80):
    return [((1-t)**3*p0[0] + 3*(1-t)**2*t*p1[0] + 3*(1-t)*t**2*p2[0] + t**3*p3[0],
             (1-t)**3*p0[1] + 3*(1-t)**2*t*p1[1] + 3*(1-t)*t**2*p2[1] + t**3*p3[1])
            for t in (i / n for i in range(n + 1))]

def background(w, h):
    img = Image.new("RGB", (w, h))
    px = img.load()
    cx, cy, r = w * 0.5, h * 0.42, math.hypot(w, h) * 0.55
    for y in range(h):
        for x in range(w):
            t = min(1.0, math.hypot(x - cx, y - cy) / r) ** 1.3
            px[x, y] = tuple(int(NAVY_IN[i] + (NAVY_OUT[i] - NAVY_IN[i]) * t) for i in range(3))
    return img

def emblem(size, brackets=True):
    """Perahu besar under sail inside scan-corner brackets, on transparent, drawn in a 1000-unit space."""
    S = size / 1000
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    P = lambda pts: [(x * S, y * S) for x, y in pts]

    # soft glow behind the boat
    glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse(P([(250, 250), (820, 760)]), fill=(214, 176, 92, 70))
    layer = Image.alpha_composite(layer, glow.filter(ImageFilter.GaussianBlur(90 * S)))
    d = ImageDraw.Draw(layer)

    # sails (fore-and-aft, trailing aft of each mast) + jib
    main_sail = [(462, 248)] + bez((462, 248), (420, 262), (380, 280), (342, 300), 20)[1:] + \
                bez((342, 300), (318, 390), (300, 480), (292, 556), 30)[1:] + [(462, 562)]
    fore_sail = [(632, 305)] + bez((632, 305), (600, 318), (570, 332), (544, 346), 20)[1:] + \
                bez((544, 346), (530, 420), (515, 490), (508, 552), 30)[1:] + [(632, 552)]
    jib = [(650, 300), (650, 540), (812, 512)]
    d.polygon(P(main_sail), fill=GOLD_LIGHT)
    d.polygon(P(fore_sail), fill=GOLD_LIGHT)
    d.polygon(P(jib), fill=GOLD_LIGHT)
    # sail seams
    for y in (360, 430, 500):
        d.line(P([(470 - (y - 250) * 0.5, y), (462, y)]), fill=GOLD, width=max(1, int(7 * S)))
    for y in (400, 470):
        d.line(P([(632 - (y - 305) * 0.45, y), (632, y)]), fill=GOLD, width=max(1, int(7 * S)))

    # masts
    d.line(P([(470, 220), (470, 600)]), fill=GOLD, width=int(16 * S))
    d.line(P([(640, 280), (640, 592)]), fill=GOLD, width=int(14 * S))

    # hull: deck line, upturned bow (linggi), rounded keel back to the stern
    hull = bez((205, 556), (400, 612), (620, 612), (800, 548)) + [(852, 462)] + \
           bez((852, 462), (832, 560), (705, 690), (520, 702))[1:] + \
           bez((520, 702), (360, 708), (248, 662), (205, 556))[1:]
    d.polygon(P(hull), fill=GOLD)
    # red-and-dark waterline band like the real Sabar/Kemajuan hulls, kept in gold tones
    band = bez((232, 618), (420, 662), (640, 660), (800, 590)) + bez((800, 590), (760, 640), (650, 690), (520, 702))[1:] + \
           bez((520, 702), (380, 706), (280, 676), (232, 618))[1:]
    # clip the band to the hull so it reads as the lower hull, not a second boat
    hull_mask = Image.new("L", (size, size), 0); ImageDraw.Draw(hull_mask).polygon(P(hull), fill=255)
    band_mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(band_mask).rectangle([0, 640 * S, size, size], fill=255)
    from PIL import ImageChops
    band_layer = Image.new("RGBA", (size, size), GOLD_DARK + (255,))
    band_layer.putalpha(ImageChops.multiply(hull_mask, band_mask))
    layer = Image.alpha_composite(layer, band_layer)
    d = ImageDraw.Draw(layer)
    # bow ornament tip
    d.ellipse(P([(840, 448), (866, 474)]), fill=GOLD_LIGHT)

    # waves
    for i, (y, x0, x1, a) in enumerate([(752, 250, 790, 230), (796, 320, 720, 150)]):
        pts = [(x, y + 12 * math.sin((x - x0) / 34)) for x in range(x0, x1 + 1, 6)]
        d.line(P(pts), fill=CYAN + (a,), width=int(13 * S), joint="curve")

    if brackets:
        L, w, m0, m1 = 120, int(24 * S), 130, 870
        for (cx, cy, sx, sy) in [(m0, m0, 1, 1), (m1, m0, -1, 1), (m0, m1, 1, -1), (m1, m1, -1, -1)]:
            d.line(P([(cx, cy + sy * L), (cx, cy), (cx + sx * L, cy)]), fill=CYAN + (235,), width=w, joint="curve")
            for (ex, ey) in [(cx, cy + sy * L), (cx + sx * L, cy)]:
                r = w / 2
                d.ellipse([ex * S - r, ey * S - r, ex * S + r, ey * S + r], fill=CYAN + (235,))
            r = w / 2
            d.ellipse([cx * S - r, cy * S - r, cx * S + r, cy * S + r], fill=CYAN + (235,))
    return layer

def pucuk_rebung(d, w, y, h, color):
    """Row of songket 'pucuk rebung' (bamboo shoot) triangles along a band."""
    step = h * 1.1
    x = -step / 2
    while x < w + step:
        d.polygon([(x, y + h), (x + step / 2, y), (x + step, y + h)], outline=color, width=max(2, int(h * 0.06)))
        x += step

def render_icon(size):
    big = size * 2
    img = background(big, big).convert("RGBA")
    img = Image.alpha_composite(img, emblem(big))
    return img.resize((size, size), Image.LANCZOS)

def render_cover(w, h, title_size, layout):
    W, H = w * 2, h * 2
    img = background(W, H).convert("RGBA")
    ov = ImageDraw.Draw(img)
    band_h = int(H * 0.035)
    pucuk_rebung(ov, W, H - band_h - int(H * 0.03), band_h, GOLD + (90,))
    if layout == "landscape":
        es = int(H * 0.78); ex, ey = int(W * 0.08), int((H - es) / 2 - H * 0.02)
        tx, ty, anchor = ex + int(es * 0.97), int(H * 0.44), "lm"
    else:
        es = int(min(W, H) * 0.62); ex, ey = int((W - es) / 2), int(H * (0.15 if layout == "portrait" else 0.06))
        tx, ty, anchor = W // 2, ey + es + int(H * 0.06), "mm"
    img.alpha_composite(emblem(es), (ex, ey))
    d = ImageDraw.Draw(img)
    avail = (W - tx - int(W * 0.05)) if layout == "landscape" else int(W * 0.88)
    title, sub = "Muzium Terengganu", "R E A L I T I   C A M P U R A N   I N T E R A K T I F"
    ts = title_size * 2
    while True:
        f1 = ImageFont.truetype(FONT, int(ts)); f2 = ImageFont.truetype(FONT, int(ts * 0.36))
        if max(d.textlength(title, font=f1), d.textlength(sub, font=f2)) <= avail: break
        ts *= 0.96
    title_size = ts / 2
    d.text((tx, ty), "Muzium Terengganu", font=f1, fill=GOLD_LIGHT, anchor=anchor)
    sub_y = ty + int(title_size * 2 * 0.95)
    d.text((tx, sub_y), "R E A L I T I   C A M P U R A N   I N T E R A K T I F", font=f2, fill=CYAN, anchor=anchor)
    return img.resize((w, h), Image.LANCZOS).convert("RGB")

os.makedirs(OUT, exist_ok=True)
render_icon(1024).save(OUT + "/AppIcon_1024.png")
render_icon(512).save(OUT + "/AppIcon_512.png")
render_cover(1440, 1440, 118, "square").save(OUT + "/Cover_Square_1440x1440.png")
render_cover(2560, 1440, 150, "landscape").save(OUT + "/Cover_Landscape_2560x1440.png")
render_cover(1008, 1440, 92, "portrait").save(OUT + "/Cover_Portrait_1008x1440.png")
print("done")
