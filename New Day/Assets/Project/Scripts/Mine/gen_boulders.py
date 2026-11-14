"""Mountain boulders: 9 sizes x 6 variants (colour AND shape). Output: out/Boulder_<W>x<H>_<Name>.png (16 px per tile, base at the bottom edge)."""
import math, random, os, sys
import numpy as np
from PIL import Image

SIZES = [(1,1),(2,1),(2,2),(2,3),(3,3),(4,3),(4,4),(5,4),(5,5)]
# name, outline + 4 shades (dark -> light), optional cover ('moss' / 'snow') with its own 4 shades
VARIANTS = [
 dict(name="Granite",   shape="round",   ramp=[(40,44,56),(82,88,102),(118,124,138),(156,163,175),(204,209,217)]),
 dict(name="Sandstone", shape="angular", ramp=[(78,52,36),(138,102,70),(176,138,96),(206,172,124),(234,208,160)]),
 dict(name="Slate",     shape="tall",    ramp=[(24,30,48),(50,60,84),(76,90,116),(106,122,150),(150,168,194)]),
 dict(name="Mossy",     shape="cluster", ramp=[(44,46,52),(88,92,98),(122,126,130),(158,160,160),(196,198,194)],
      cover="moss", cover_ramp=[(30,64,34),(52,100,46),(84,138,60),(128,178,82)]),
 dict(name="Rust",      shape="slab",    ramp=[(62,32,28),(110,56,42),(150,82,56),(188,112,74),(224,154,108)]),
 dict(name="Frost",     shape="split",   ramp=[(44,60,92),(92,120,158),(136,166,198),(184,210,230),(232,244,252)],
      cover="snow", cover_ramp=[(150,176,210),(196,216,236),(228,238,248),(252,254,255)]),
]
LIGHT = np.array([-0.55, -0.62, 0.56]); LIGHT /= np.linalg.norm(LIGHT)

def chamfer(inside):
    """distance of every True pixel to the nearest False pixel (two-pass chamfer, 3-4 weights)."""
    h, w = inside.shape
    INF = 10**6
    d = np.where(inside, INF, 0).astype(np.int64)
    for y in range(h):
        for x in range(w):
            if d[y,x] == 0: continue
            best = d[y,x]
            if x > 0: best = min(best, d[y,x-1] + 3)
            if y > 0:
                best = min(best, d[y-1,x] + 3)
                if x > 0: best = min(best, d[y-1,x-1] + 4)
                if x < w-1: best = min(best, d[y-1,x+1] + 4)
            d[y,x] = best
    for y in range(h-1, -1, -1):
        for x in range(w-1, -1, -1):
            if d[y,x] == 0: continue
            best = d[y,x]
            if x < w-1: best = min(best, d[y,x+1] + 3)
            if y < h-1:
                best = min(best, d[y+1,x] + 3)
                if x < w-1: best = min(best, d[y+1,x+1] + 4)
                if x > 0: best = min(best, d[y+1,x-1] + 4)
            d[y,x] = best
    return np.where(inside, d / 3.0, 0.0)

def value_noise(h, w, cell, rng, amp=1.0):
    gh, gw = h // cell + 3, w // cell + 3
    g = np.array([[rng.random() for _ in range(gw)] for _ in range(gh)])
    out = np.zeros((h, w))
    for y in range(h):
        for x in range(w):
            fy, fx = y / cell, x / cell
            y0, x0 = int(fy), int(fx)
            ty, tx = fy - y0, fx - x0
            ty = ty*ty*(3-2*ty); tx = tx*tx*(3-2*tx)
            a = g[y0,x0]*(1-tx) + g[y0,x0+1]*tx
            b = g[y0+1,x0]*(1-tx) + g[y0+1,x0+1]*tx
            out[y,x] = a*(1-ty) + b*ty
    return (out - 0.5) * 2 * amp

def ellipse(mask, cx, cy, rx, ry):
    h, w = mask.shape
    ys, xs = np.mgrid[0:h, 0:w]
    mask |= (((xs + 0.5 - cx) / max(rx, 0.5))**2 + ((ys + 0.5 - cy) / max(ry, 0.5))**2) <= 1.0

def polygon(mask, pts):
    h, w = mask.shape
    ys, xs = np.mgrid[0:h, 0:w]
    px, py = xs + 0.5, ys + 0.5
    inside = np.zeros((h, w), bool)
    n = len(pts)
    for i in range(n):
        x1, y1 = pts[i]; x2, y2 = pts[(i+1) % n]
        cond = ((y1 > py) != (y2 > py)) & (px < (x2 - x1) * (py - y1) / (y2 - y1 + 1e-9) + x1)
        inside ^= cond
    mask |= inside

def silhouette(shape, W, H, rng):
    """W,H in pixels. Base is the bottom row."""
    m = np.zeros((H, W), bool)
    base = H - 1.0
    if shape == "round":
        ry = (H - 2) * rng.uniform(0.55, 0.62); rx = (W - 2) / 2.0
        ellipse(m, W/2, 1 + ry, rx, ry)
        if W > 40: ellipse(m, W*rng.uniform(.3,.45), 1 + ry*1.15, rx*0.6, ry*0.9)
    elif shape == "angular":
        j = lambda a: a * rng.uniform(0.85, 1.15)
        pts = [(1, base), (W-1.5, base), (W-1, H*0.55), (W*rng.uniform(.68,.82), 1.2), (W*rng.uniform(.28,.42), 1.6+H*0.06), (1, H*rng.uniform(.38,.55))]
        if W > 24: pts.insert(4, (W*0.52, 1 + H*rng.uniform(0.0, 0.1)))
        polygon(m, pts)
    elif shape == "tall":
        ellipse(m, W/2, H - H*0.26, (W-2)/2, H*0.3)
        ellipse(m, W*rng.uniform(.42,.58), H*0.42, W*0.30, H*0.42)
        if W > 24: ellipse(m, W*rng.uniform(.2,.35), H*0.62, W*0.2, H*0.28)
    elif shape == "cluster":
        lobes = 2 + (1 if W > 24 else 0) + (1 if W > 48 else 0)
        for i in range(lobes):
            rx = max(4.0, (W - 2) / lobes * rng.uniform(0.62, 0.82)); ry = max(4.0, (H - 2) * (rng.uniform(0.5, 0.58) if i == lobes // 2 else rng.uniform(0.3, 0.45)))
            if i % 2 == 1 and i != lobes // 2: ry *= rng.uniform(0.7, 0.9)                       # alternate tall and low humps
            cx = (W - 2) * (i + 0.5) / lobes + 1 + rng.uniform(-W*0.04, W*0.04)
            ellipse(m, cx, base - ry*0.72, rx, ry)
    elif shape == "slab":
        top = H * rng.uniform(0.18, 0.3); rad = max(2.0, min(W, H) * 0.2)
        ys, xs = np.mgrid[0:H, 0:W]
        px, py = xs + 0.5, ys + 0.5
        cx0, cx1, cy0 = 1 + rad, W - 1 - rad, top + rad
        qx = np.clip(px, cx0, cx1); qy = np.clip(py, cy0, base + 5)
        m |= ((px - qx)**2 + (py - qy)**2) <= rad**2
        slope = rng.uniform(-0.12, 0.12) * H
        m &= (py > top + slope * (px / W - 0.5) * 2)
    elif shape == "split":
        ellipse(m, W*0.3, H - H*rng.uniform(.5,.62), W*0.3, H*rng.uniform(.5,.6))
        ellipse(m, W*0.7, H - H*rng.uniform(.4,.55), W*0.3, H*rng.uniform(.42,.54))
        ellipse(m, W*0.5, H - H*0.28, W*0.45, H*0.28)
    m[int(base):, :] = m[int(base)-1:int(base), :] if H > 2 else m[int(base):, :]
    return m

def make_rock(shape, w, h, var, seed):
    rng = random.Random(seed)
    W, Hh = w * 16, h * 16
    mask = silhouette(shape, W, Hh, rng)
    # roughen the edge
    din = chamfer(mask); dout = chamfer(~mask)
    sdf = np.where(mask, din, -dout)
    sdf = sdf + value_noise(Hh, W, 5, rng, amp=1.4) + value_noise(Hh, W, 2, rng, amp=0.5)
    mask = sdf > 0.5
    mask[0, :] = False; mask[:, 0] = False; mask[:, -1] = False
    mask[Hh-1, :] |= np.roll(mask[Hh-2, :], 0)           # flat base
    # keep the biggest blob
    lab = -np.ones(mask.shape, int); best = (0, None)
    cur = 0
    for y in range(Hh):
        for x in range(W):
            if mask[y,x] and lab[y,x] < 0:
                stack = [(y,x)]; lab[y,x] = cur; cnt = 0; cells = []
                while stack:
                    cy, cx = stack.pop(); cnt += 1; cells.append((cy,cx))
                    for dy, dx in ((1,0),(-1,0),(0,1),(0,-1)):
                        ny, nx = cy+dy, cx+dx
                        if 0 <= ny < Hh and 0 <= nx < W and mask[ny,nx] and lab[ny,nx] < 0:
                            lab[ny,nx] = cur; stack.append((ny,nx))
                if cnt > best[0]: best = (cnt, cur)
                cur += 1
    mask = lab == best[1]
    d = chamfer(mask)

    # dome height from edge distance, faceted by random voronoi cells
    dome = np.sqrt(d)
    k = np.ones(3) / 3
    for _ in range(2):
        dome = np.apply_along_axis(lambda r: np.convolve(r, k, mode='same'), 1, dome)
        dome = np.apply_along_axis(lambda r: np.convolve(r, k, mode='same'), 0, dome)
    gy, gx = np.gradient(dome)
    area = int(mask.sum())
    nseeds = max(3, area // rng.choice([70, 90, 120]))
    ys_in, xs_in = np.nonzero(mask)
    seeds = [(float(xs_in[i]), float(ys_in[i])) for i in [rng.randrange(len(xs_in)) for _ in range(nseeds)]]
    tilt = [(rng.uniform(-0.55, 0.55), rng.uniform(-0.55, 0.55)) for _ in seeds]
    ys, xs = np.mgrid[0:Hh, 0:W]
    dists = np.stack([(xs - sx)**2 + (ys - sy)**2 for sx, sy in seeds])
    cell = dists.argmin(axis=0)
    tx = np.array([t[0] for t in tilt])[cell]; ty = np.array([t[1] for t in tilt])[cell]
    gxf = gx * 1.6 + tx * 0.5; gyf = gy * 1.6 + ty * 0.5
    nrm = np.stack([-gxf, -gyf, np.ones_like(gxf)])
    nrm /= np.linalg.norm(nrm, axis=0)
    shade = (nrm[0]*LIGHT[0] + nrm[1]*LIGHT[1] + nrm[2]*LIGHT[2])
    vals = shade[mask]
    q = np.quantile(vals, [0.22, 0.5, 0.8])
    level = np.digitize(shade, q) + 1            # 1..4
    # a little grain
    grain = np.array([[rng.random() for _ in range(W)] for _ in range(Hh)])
    level = np.where((grain < 0.05) & (level < 4), level + 1, level)
    level = np.where((grain > 0.96) & (level > 1), level - 1, level)
    # ambient occlusion at the foot
    level[Hh-3:, :] = np.minimum(level[Hh-3:, :], 2)

    ramp = var["ramp"]
    img = np.zeros((Hh, W, 4), np.uint8)
    for y in range(Hh):
        for x in range(W):
            if mask[y,x]:
                img[y,x,:3] = ramp[int(level[y,x])]; img[y,x,3] = 255

    # cover: moss on top surfaces / snow cap
    if "cover" in var:
        cr = var["cover_ramp"]
        n1 = value_noise(Hh, W, 6, rng, amp=0.6) + value_noise(Hh, W, 3, rng, amp=0.25)
        top = 1.0 - (ys / float(Hh))
        if var["cover"] == "snow":
            score = top * 1.5 + n1 - 0.78 + (gyf < 0.15) * 0.1
        else:
            score = top * 1.2 + n1 - 0.55 + np.clip(-gy, -0.3, 0.6) * 0.5
        cov = mask & (score > 0.1) & (d >= 1.0)
        for y in range(Hh):
            for x in range(W):
                if cov[y,x]:
                    lv = max(0, min(3, int(level[y,x]) - 1))
                    img[y,x,:3] = cr[lv]
        mask_cover = cov
    # cracks
    ncr = rng.choice([1, 2, 2, 3]) if w * h >= 2 else rng.choice([0, 1])
    if var["shape"] == "split" if False else shape == "split": ncr += 1
    for _ in range(ncr):
        x = rng.uniform(W * 0.25, W * 0.75); y = 2 + rng.uniform(0, 4)
        length = rng.uniform(0.35, 0.7) * Hh
        dx = rng.uniform(-0.4, 0.4)
        for _s in range(int(length)):
            xi, yi = int(x), int(y)
            if 1 <= xi < W-1 and 1 <= yi < Hh-2 and mask[yi, xi] and d[yi, xi] > 1.5:
                img[yi, xi, :3] = ramp[0] if rng.random() < 0.85 else ramp[1]
                if mask[yi, xi+1] and d[yi, xi+1] > 1.5 and tuple(img[yi, xi+1, :3]) != tuple(ramp[0]):
                    lv = min(4, int(level[yi, xi+1]) + 1); img[yi, xi+1, :3] = ramp[lv]
            y += 1; x += dx + rng.uniform(-0.6, 0.6); dx += rng.uniform(-0.15, 0.15)
    # outline: darkest colour on the border; lit side catches a highlight
    for y in range(Hh):
        for x in range(W):
            if not mask[y,x]: continue
            edge = (y == Hh-1) or any(not (0 <= y+dy < Hh and 0 <= x+dx < W and mask[y+dy, x+dx]) for dy, dx in ((1,0),(-1,0),(0,1),(0,-1)))
            if edge:
                col = ramp[0]
                if "cover" in var and var["cover"] == "snow" and score[y, x] > 0.1 and y < Hh * 0.6:
                    col = var["cover_ramp"][0]
                img[y,x,:3] = col
    for y in range(1, Hh-1):
        for x in range(1, W-1):
            if mask[y,x] and d[y,x] > 1.0 and d[y,x] < 2.2 and (not mask[y-1,x] or not mask[y,x-1] or d[y-1,x] < 1.1 or d[y,x-1] < 1.1) and level[y,x] >= 3:
                if not ("cover" in var and tuple(img[y,x,:3]) in [tuple(c) for c in var["cover_ramp"]]):
                    img[y,x,:3] = ramp[4]
    return Image.fromarray(img, "RGBA")

def main(outdir="out"):
    os.makedirs(outdir, exist_ok=True)
    for (w, h) in SIZES:
        for vi, var in enumerate(VARIANTS):
            v = dict(var); v["shape"] = var["shape"]
            im = make_rock(var["shape"], w, h, v, seed=w*1000 + h*100 + vi*7 + 3)
            im.save(f"{outdir}/Boulder_{w}x{h}_{var['name']}.png")
    print("done")

if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "out")
