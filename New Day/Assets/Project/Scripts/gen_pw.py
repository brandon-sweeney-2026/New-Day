import sys, os, math
from PIL import Image

OUT = sys.argv[1] if len(sys.argv) > 1 else 'out'
os.makedirs(OUT, exist_ok=True)
SRC = '/home/claude/player_out/player_sheet.png'
CW, CH = 24, 32            # cell size of the new frames
BW, BH = 16, 24            # the original player cell
OX, OY = 4, 8              # where the original sits inside the new cell (feet stay on the bottom, centred)
K = (0, 0, 0, 255); G = (25, 138, 18, 255); D = (10, 125, 15, 255); SK = (237, 195, 154, 255)

src = Image.open(SRC).convert('RGBA')
def base(col, row): return src.crop((col * BW, row * BH, col * BW + BW, row * BH + BH))

# ---- the armless bodies (arms of the idle pose cut away) ----
def armless(view):
    if view == 'F': b = base(0, 0)
    elif view == 'B': b = base(0, 1)
    else: b = base(0, 2)                          # side view faces LEFT (the right-facing one is a mirror)
    b = b.copy(); px = b.load()
    if view in ('F', 'B'):
        for y in range(12, 17):
            for x in (2, 3, 12, 13):
                if (x, y) in ((3, 16), (12, 16)) or not (x == 2 and y == 16 or x == 13 and y == 16): px[x, y] = (0, 0, 0, 0)
        for y in range(12, 17): px[2, y] = (0, 0, 0, 0); px[13, y] = (0, 0, 0, 0)
    return b

BODY = {v: armless(v) for v in 'FBS'}

# shoulders in cell coordinates (x right, y DOWN)
SHOULDER = {'F': ((15.5, 20.5), (8.5, 20.5)), 'B': ((15.5, 20.5), (8.5, 20.5)), 'S': ((12.0, 20.5), (12.0, 20.5))}   # (weapon-arm side, other side)

def dirv(deg): return (math.cos(math.radians(deg)), -math.sin(math.radians(deg)))   # y down

def blank(): return Image.new('RGBA', (CW, CH), (0, 0, 0, 0))

def put_body(img, view, dx, dy, ux, uy):
    """legs (rows 19+) stay near the ground; the upper body (rows 0-18) can lean (ux, uy) and crouch."""
    b = BODY[view]
    legs = b.crop((0, 19, BW, BH)); upper = b.crop((0, 0, BW, 19))
    img.alpha_composite(legs, (OX + dx, OY + 19 + dy))
    img.alpha_composite(upper, (OX + dx + ux, OY + dy + uy))

def thick_line(mask, pts):
    for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
        n = max(1, int(max(abs(x1 - x0), abs(y1 - y0)) * 2))
        for i in range(n + 1):
            t = i / n; x = x0 + (x1 - x0) * t; y = y0 + (y1 - y0) * t
            ix, iy = int(math.floor(x)), int(math.floor(y))
            for ox in (0, 1):
                for oy in (0, 1): mask.add((ix + ox, iy + oy))

def draw_arm(img, pts, hand):
    """pts: the shoulder, an optional elbow, then the hand (cell coords). Sleeve = green with a darker underside, hand = skin, black outline."""
    mask = set(); thick_line(mask, pts)
    hx, hy = int(round(hand[0])), int(round(hand[1]))
    hmask = {(hx - 1, hy - 1), (hx, hy - 1), (hx - 1, hy), (hx, hy)}
    allm = mask | hmask
    px = img.load()
    def inb(x, y): return 0 <= x < CW and 0 <= y < CH
    for (x, y) in allm:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if (nx, ny) not in allm and inb(nx, ny): px[nx, ny] = K
    for (x, y) in mask:
        if inb(x, y): px[x, y] = D if (x, y + 1) not in allm else G
    for (x, y) in hmask:
        if inb(x, y): px[x, y] = SK

def frame(view, fa, ang, reach, lean=0.0, crouch=0, dx=0, arm_ang=None, two=False, hide_weapon=False, hand_override=None, extra=None, behind=None):
    """One body pose. fa = facing angle, ang = weapon angle (screen, y up), reach = shoulder-to-hand length in pixels.
       lean pushes the upper body toward the facing direction (negative = back). Returns (image, hand(x,y), ang, behind)."""
    f = {'F': (0, 1), 'B': (0, -1), 'S': (-1, 0)}[view]
    ux = dx + int(round(f[0] * lean)); uy = int(round(f[1] * lean)) + crouch
    img = blank()
    put_body(img, view, dx, 0, ux - dx, uy)
    s_w, s_o = SHOULDER[view]
    sw = (s_w[0] + ux, s_w[1] + uy); so = (s_o[0] + ux, s_o[1] + uy)
    aa = ang if arm_ang is None else arm_ang
    d = dirv(aa)
    hand = (sw[0] + d[0] * reach, sw[1] + d[1] * reach) if hand_override is None else hand_override
    if view == 'S' and extra is None and not two:
        pass
    if extra:                                         # extra arm drawn first (it is the "other" arm)
        extra(img, sw, so, hand, ux, uy)
    if two:
        h2 = (hand[0] - 2.0 if view != 'S' else hand[0] + (1.5 if f[0] < 0 else -1.5), hand[1] + (0.0 if view != 'S' else 1.5))
        draw_arm(img, [so, h2], h2)
    draw_arm(img, [sw, hand], hand)
    if behind is None: behind = dirv(ang)[1] < -0.35          # (image y is down: pointing up on screen = negative)
    return (img, hand, ang, behind and not hide_weapon, hide_weapon)

# ---------------------------------------------------------------- the animations
FAC = {'D': -90.0, 'U': 90.0, 'L': 180.0, 'R': 0.0}
VIEW = {'D': 'F', 'U': 'B', 'L': 'S', 'R': 'S'}
ORDER = ['D', 'U', 'L', 'R']

ANIMS = []                     # (name, {facing: [ (frame..., dur) ]}, loops?)

def mirror_frame(fr):
    img, hand, ang, behind, hidden = fr[:5]
    return (img.transpose(Image.FLIP_LEFT_RIGHT), (CW - hand[0], hand[1]), 180.0 - ang, behind, hidden)

def build(fn):
    """fn(facing_letter, fa, view) -> list of (frame tuple, duration). R = mirror of L."""
    out = {}
    for L in 'DUL':
        out[L] = fn(L, FAC[L], VIEW[L])
    out['R'] = [(mirror_frame(fr), d) for fr, d in out['L']]
    return out

# ---- sword: quick and light. A and B swing in opposite directions
def sword(side):
    def fn(L, fa, v):
        s = side
        seq = [
            (dict(ang=fa + s * 95, reach=3.0, lean=0), 0.05),                  # ready
            (dict(ang=fa + s * 80, reach=4.0, lean=-1), 0.045),                # wind-up
            (dict(ang=fa + s * 40, reach=5.5, lean=1), 0.03),                  # strike
            (dict(ang=fa - s * 5, reach=6.5, lean=1), 0.03),
            (dict(ang=fa - s * 50, reach=6.0, lean=1), 0.03),
            (dict(ang=fa - s * 78, reach=5.0, lean=0), 0.06),                  # follow-through
            (dict(ang=fa - s * 95, reach=3.5, lean=0), 0.06),                  # recover
        ]
        return [(frame(v, fa, **kw), d) for kw, d in seq]
    return fn
ANIMS.append(('sword_a', build(sword(1))))
ANIMS.append(('sword_b', build(sword(-1))))

# ---- dagger: fast straight poke
def dagger(L, fa, v):
    seq = [
        (dict(ang=fa, reach=2.5, lean=0), 0.04),
        (dict(ang=fa, reach=1.0, lean=-1), 0.035),                          # coil back
        (dict(ang=fa, reach=5.0, lean=1, dx=0), 0.03),                      # thrust
        (dict(ang=fa, reach=8.0, lean=2), 0.05),                            # full extension
        (dict(ang=fa, reach=8.0, lean=1), 0.04),
        (dict(ang=fa, reach=4.0, lean=0), 0.06),
    ]
    return [(frame(v, fa, **kw), d) for kw, d in seq]
ANIMS.append(('dagger', build(dagger)))

# ---- hammer: two hands, lifted over the head, slammed down
def hammer(L, fa, v):
    if L == 'D': raised, sign = 70.0, -1.0
    elif L == 'U': raised, sign = 160.0, -1.0
    elif L == 'R': raised, sign = 112.0, -1.0
    else: raised, sign = 68.0, 1.0
    if L in ('L',): pass
    carry = fa - sign * 35.0
    def lerp(a, b, t): return a + (b - a) * t
    seq = [
        (dict(ang=carry, reach=4.0, lean=0), 0.08),
        (dict(ang=lerp(carry, raised, 0.35), reach=5.0, lean=-1), 0.15),
        (dict(ang=lerp(carry, raised, 0.7), reach=6.0, lean=-1, crouch=1), 0.15),
        (dict(ang=raised, reach=7.0, lean=-1, crouch=1), 0.12),             # top: strain
        (dict(ang=raised, reach=7.0, lean=-1, crouch=1, dx=0), 0.08),       # (the game shakes the head slightly here)
        (dict(ang=lerp(raised, fa, 0.55), reach=7.0, lean=1, crouch=0), 0.03),   # slam
        (dict(ang=fa, reach=7.0, lean=2, crouch=2), 0.12),                  # impact
        (dict(ang=fa, reach=7.0, lean=2, crouch=2), 0.2),                   # stuck in the ground
        (dict(ang=fa - sign * 20.0, reach=6.0, lean=1, crouch=1), 0.2),     # drag it back
    ]
    return [(frame(v, fa, two=True, **kw), d) for kw, d in seq]
ANIMS.append(('hammer', build(hammer)))

# ---- flail: wind-up (heave it back), spin (8 body poses, picked by where the ball is), wind-down and catching breath
def flail_wind(L, fa, v):
    back = fa + 180.0
    seq = [
        (dict(ang=fa + 150, reach=3.0, lean=-1, crouch=0), 0.1),
        (dict(ang=back, reach=5.0, lean=-1, crouch=1), 0.12),
        (dict(ang=back, reach=5.5, lean=-2, crouch=1), 0.1),
    ]
    return [(frame(v, fa, **kw), d) for kw, d in seq]
ANIMS.append(('flail_wind', build(flail_wind)))

def flail_end(L, fa, v):
    seq = [
        (dict(ang=fa - 100, reach=3.5, lean=0, crouch=1), 0.15),                 # ball drops, arm sags
        (dict(ang=-90.0, reach=4.0, lean=0, crouch=2, hide_weapon=True, two=False), 0.3),   # hunched, catching breath
    ]
    return [(frame(v, fa, **kw), d) for kw, d in seq]
ANIMS.append(('flail_end', build(flail_end)))

def flail_spin_frames():
    frs = []
    for k in range(8):
        th = k * 45.0
        c, s_ = math.cos(math.radians(th)), math.sin(math.radians(th))
        if abs(c) >= abs(s_): view = 'S'; mirror = c > 0
        else: view = 'B' if s_ > 0 else 'F'; mirror = False
        # lean away from the ball (it is heavy and pulls on you)
        # image y is down, so "away" in x is -c, in y is +s
        lean_x = -c * 1.2; lean_y = s_ * 1.2
        f = {'F': (0, 1), 'B': (0, -1), 'S': (-1, 0)}[view]
        if view == 'S':
            ang_local = th if not mirror else 180.0 - th           # draw the left-facing version and mirror it
            lean = -lean_x if mirror else lean_x                  # lean along the facing axis
            lean_amt = -(lean_x * (-1)) if False else (lean_x * -1 if not mirror else lean_x)
            fr = frame('S', 180.0, ang_local, 4.5, lean=0, crouch=1, dx=int(round((-c * 1.0) * (1 if not mirror else -1))) * 0)
        else:
            fr = frame(view, 0, th, 4.5, lean=0, crouch=1, dx=int(round(-c * 1.0)))
        if mirror: fr = mirror_frame(fr)
        frs.append((fr, 0.05))
    return frs

# ---- bow: the arms physically draw it back
def bow_frames(L, fa, v):
    pulls = [0.0, 2.0, 3.0, 4.0, 6.0]
    out = []
    for p in pulls + ['release', 'follow']:
        if p == 'release': pull = -1.5
        elif p == 'follow': pull = 0.0
        else: pull = p
        out.append((bow_frame(v, fa, pull, p == 'follow'), 0.05 if p == 'release' else 0.08))
    return out

def bow_frame(view, fa, pull, relaxed):
    f = {'F': (0, 1), 'B': (0, -1), 'S': (-1, 0)}[view]
    img = blank()
    lean = -1 if pull >= 3 else 0
    ux = int(round(f[0] * lean)); uy = int(round(f[1] * lean))
    put_body(img, view, 0, 0, ux, uy)
    s_w, s_o = SHOULDER[view]
    sw = (s_w[0] + ux, s_w[1] + uy); so = (s_o[0] + ux, s_o[1] + uy)
    back = 3.0 + pull
    if view == 'S':
        H = (sw[0] - 7.0, sw[1] - 1.0)
        P = (H[0] + back, H[1])
        E = (min(sw[0] + 1.5 + pull * 0.35, P[0] + 0.0), sw[1] + 2.5)
        draw_arm(img, [sw, E, P], P)                       # the drawing arm: elbow behind, hand at the string
        draw_arm(img, [sw, H], H)                          # the bow arm
    elif view == 'F':
        H = (sw[0] + 1.0, sw[1] + 4.0)
        P = (H[0] - 3.0, H[1] - back)
        E = (so[0] - 1.5, so[1] + 1.5)
        draw_arm(img, [so, E, P], P)
        draw_arm(img, [sw, H], H)
    else:
        H = (sw[0] + 0.5, sw[1] - 1.0)
        P = (H[0] - 3.0, H[1] + back * 0.6)
        E = (so[0] - 1.0, so[1] + 1.0)
        draw_arm(img, [so, E, P], P)
        draw_arm(img, [sw, H], H)
    return (img, H, fa, False, False)
ANIMS.append(('bow', build(bow_frames)))

# ---------------------------------------------------------------- the sheet
rows = []        # (animname, facing or None, [(frame, dur)])
for name, per in ANIMS:
    if name == 'flail_end' or True:
        pass
for name, per in ANIMS:
    for L in ORDER: rows.append((name, L, per[L]))
rows.append(('flail_spin', None, flail_spin_frames()))

maxcols = max(len(r[2]) for r in rows)
sheet = Image.new('RGBA', (maxcols * CW, len(rows) * CH), (0, 0, 0, 0))
cs = []
cs.append('// <auto-generated> by the weapon pose generator. The data for the player weapon animations: which picture, how long, where the hand and weapon are.\n')
cs.append('using System.Collections.Generic;\nusing UnityEngine;\n\n')
cs.append('/// <summary>One frame of a weapon animation. hx/hy = where the hand (the weapon\'s grip) is in the 24x32 picture (y counts DOWN from the top).\n/// ang = which way the weapon points (degrees, 0 = right, 90 = up). behind = draw the weapon behind the body.</summary>\n')
cs.append('public struct PWFrame\n{\n    public int col; public float dur, hx, hy, ang; public bool behind, hidden;\n    public PWFrame(int col, float dur, float hx, float hy, float ang, bool behind, bool hidden)\n    { this.col = col; this.dur = dur; this.hx = hx; this.hy = hy; this.ang = ang; this.behind = behind; this.hidden = hidden; }\n}\n\n')
cs.append('public static class PlayerWeaponFrames\n{\n    public const int CellWidth = 24, CellHeight = 32;\n    public const int Columns = %d;\n\n    /// <summary>Which sheet row an animation lives in, and its frames. Names: sword_a, sword_b, dagger, hammer, flail_wind, flail_end, flail_spin, bow. Facing 0=Down 1=Up 2=Left 3=Right.</summary>\n' % maxcols)
cs.append('    public struct Row { public int row; public PWFrame[] frames; }\n    private static Dictionary<string, Row[]> table;\n\n')
cs.append('    public static Row Get(string anim, int facing)\n    {\n        if (table == null) Build();\n        Row[] r = table[anim];\n        return r.Length == 1 ? r[0] : r[Mathf.Clamp(facing, 0, 3)];\n    }\n\n')
cs.append('    private static void Build()\n    {\n        table = new Dictionary<string, Row[]>();\n')
byanim = {}
for ri, (name, L, frs) in enumerate(rows):
    for ci, (fr, dur) in enumerate(frs):
        sheet.alpha_composite(fr[0], (ci * CW, ri * CH))
    entries = ', '.join('new PWFrame(%d, %.3ff, %.2ff, %.2ff, %.1ff, %s, %s)' % (ci, dur, fr[1][0], fr[1][1], fr[2], 'true' if fr[3] else 'false', 'true' if fr[4] else 'false') for ci, (fr, dur) in enumerate(frs))
    byanim.setdefault(name, []).append('new Row { row = %d, frames = new[] { %s } }' % (ri, entries))
for name, lst in byanim.items():
    cs.append('        table["%s"] = new[]\n        {\n            %s\n        };\n' % (name, ',\n            '.join(lst)))
cs.append('    }\n}\n')
sheet.save(os.path.join(OUT, 'player_weapon_sheet.png'))
open(os.path.join(OUT, 'PlayerWeaponFrames.cs'), 'w').write(''.join(cs))
print('rows', len(rows), 'cols', maxcols, sheet.size)

# preview: a few rows at 4x on green
def preview(names, fn):
    pick = [r for r in rows if r[0] in names]
    w = max(len(r[2]) for r in pick) * CW * 3; h = len(pick) * CH * 3
    pv = Image.new('RGBA', (w + 8, h + 8), (90, 140, 90, 255))
    for ri, (name, L, frs) in enumerate(pick):
        for ci, (fr, dur) in enumerate(frs):
            im = fr[0].resize((CW * 3, CH * 3), Image.NEAREST); pv.alpha_composite(im, (4 + ci * CW * 3, 4 + ri * CH * 3))
    pv.save(os.path.join(OUT, fn))
preview(['sword_a'], 'prev_sword.png'); preview(['dagger'], 'prev_dagger.png'); preview(['hammer'], 'prev_hammer.png')
preview(['bow'], 'prev_bow.png'); preview(['flail_wind', 'flail_end', 'flail_spin'], 'prev_flail.png')
