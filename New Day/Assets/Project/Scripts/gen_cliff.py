"""Mountain cliff tiles (16x16, opaque rock + see-through edges). Output: cliff_tiles.png (256 px wide) + prev_cliff.png.
Names (order = column, then row from the top) are listed in NAMES and mirrored in CliffPaletteBuilder.cs."""
import random, math, sys
import numpy as np
from PIL import Image

R0=(34,34,46); R1=(72,70,84); R2=(108,104,116); R3=(144,138,146); R4=(184,176,172)
T=(0,0,0,0)
MOSS=[(36,72,38),(60,106,50),(92,144,66),(132,182,88)]
SNOW=[(176,196,226),(212,226,244),(240,246,252),(255,255,255)]
ICE=[(110,150,200),(150,190,226),(196,224,244),(236,248,255)]

NAMES = (["Fill_%d"%i for i in range(1,5)] + ["Lip_T","Lip_L","Lip_R","Lip_TL","Lip_TR","Inner_TL","Inner_TR"] + [None]*5 +
         ["FaceUpper_%d"%i for i in range(1,5)] + ["FaceUpper_L","FaceUpper_R"] + ["FaceLower_%d"%i for i in range(1,5)] + ["FaceLower_L","FaceLower_R"] +
         ["Moss","Snow","Crack","Rubble","Pebbles","Ice","Vines","SmallRock"])

class Cv:
    def __init__(s): s.a=np.zeros((16,16,4),np.uint8)
    def set(s,x,y,c):
        if 0<=x<16 and 0<=y<16: s.a[y,x]=tuple(c)+((255,) if len(c)==3 else ())
    def get(s,x,y): return tuple(s.a[y,x])
    def clear(s,x,y): s.a[y,x]=(0,0,0,0)
    def opaque(s,x,y): return 0<=x<16 and 0<=y<16 and s.a[y,x,3]>0

def rock(cv,rng,seed,top=True):
    """plateau top / wall base texture"""
    for y in range(16):
        for x in range(16):
            r=rng.random()
            c=R2
            if r<0.10: c=R1
            elif r<0.18: c=R3
            elif r<0.21: c=R4
            cv.set(x,y,c)
    # strata cracks
    for _ in range(rng.choice([1,2,2,3])):
        x=rng.randrange(2,14); y=rng.randrange(0,16); L=rng.randrange(3,8); dx=rng.choice([-1,1])
        for i in range(L):
            cv.set(x,y,R1); 
            if rng.random()<0.5: x+=dx
            y+=1
            if y>15: break
    # lit patches
    for _ in range(3):
        x=rng.randrange(1,13); y=rng.randrange(1,13)
        for dx,dy in ((0,0),(1,0),(0,1)): cv.set(x+dx,y+dy,R3)

def wall(cv,rng,dark):
    """vertical strata wall. dark=0 upper (lit), 1 lower (shadowed)"""
    x=0
    base=[R2,R1] if not dark else [R1,R0]
    lit=R3 if not dark else R2
    i=0
    while x<16:
        w=rng.randrange(2,5)
        col=base[i%2] if rng.random()<0.7 else base[(i+1)%2]
        for xx in range(x,min(16,x+w)):
            for y in range(16):
                cv.set(xx,y,col)
        if x<16: 
            for y in range(16):
                if rng.random()<0.75: cv.set(x,y,lit)
        x+=w; i+=1
    # horizontal ledges and speckle
    for _ in range(rng.choice([1,2])):
        y=rng.randrange(3,13)
        for xx in range(16):
            if rng.random()<0.55: cv.set(xx,y,R0 if dark else R1)
    for _ in range(14):
        cv.set(rng.randrange(16),rng.randrange(16),rng.choice([R0,R1,R3] if not dark else [R0,R1,R2]))

def ragged(rng,n=16,lo=0,hi=1):
    return [rng.randint(lo,hi) for _ in range(n)]

def make_lip(kind,seed):
    rng=random.Random(seed); cv=Cv(); rock(cv,rng,seed)
    top = 'T' in kind; left = 'L' in kind and kind!='Lip_R'; right = kind in ('Lip_R','Lip_TR')
    top = kind in ('Lip_T','Lip_TL','Lip_TR'); left = kind in ('Lip_L','Lip_TL'); 
    rt=ragged(rng,16,1,2); rl=ragged(rng,16,1,2); rr=ragged(rng,16,1,2)
    for y in range(16):
        for x in range(16):
            cut=False; edge=False; hl=False
            if top:
                if y<rt[x]: cut=True
                elif y==rt[x]: edge=True
                elif y==rt[x]+1: hl=True
            if left:
                if x<rl[y]: cut=True
                elif x==rl[y]: edge=True
                elif x==rl[y]+1: hl=True
            if right:
                if 15-x<rr[y]: cut=True
                elif 15-x==rr[y]: edge=True
                elif 15-x==rr[y]+1: hl=True
            if cut: cv.clear(x,y)
            elif edge: cv.set(x,y,R0)
            elif hl: cv.set(x,y,R4 if rng.random()<0.7 else R3)
    if top and (left or right):                   # round the corner
        for (x,y) in ((0,0),(1,0),(0,1)) if left else ((15,0),(14,0),(15,1)): cv.clear(x,y)
    return cv

def make_inner(kind,seed):
    rng=random.Random(seed); cv=Cv(); rock(cv,rng,seed)
    cx = 0 if kind=='Inner_TL' else 16
    for y in range(16):
        for x in range(16):
            d=math.hypot(x+.5-cx,y+.5)
            if d<4.0: cv.clear(x,y)
            elif d<5.0: cv.set(x,y,R0)
            elif d<6.0: cv.set(x,y,R4 if rng.random()<0.7 else R3)
    return cv

def make_face(kind,idx,seed):
    rng=random.Random(seed); cv=Cv()
    lower = kind.startswith('FaceLower')
    wall(cv,rng,1 if lower else 0)
    if not lower:
        # the plateau's front edge: bright lip across the top
        for x in range(16):
            cv.set(x,0,R4 if rng.random()<0.8 else R3); cv.set(x,1,R3 if rng.random()<0.7 else R2)
    else:
        # foot: shadow, rubble and a ragged bottom that melts into the ground
        for x in range(16):
            cv.set(x,12,R0 if rng.random()<0.6 else R1)
        rb=ragged(rng,16,0,2)
        for x in range(16):
            for y in range(14,16):
                if y-14<rb[x]: cv.clear(x,y) if (y==15) else cv.set(x,y,R0)
        for _ in range(3):
            x=rng.randrange(1,14); cv.set(x,13,R2); cv.set(x+1,13,R3); cv.set(x,14,R1)
    if kind.endswith('_L') or kind.endswith('_R'):
        left=kind.endswith('_L')
        rr=ragged(rng,16,1,2)
        for y in range(16):
            for k in range(3):
                x=k if left else 15-k
                if k<rr[y]: cv.clear(x,y)
                elif k==rr[y]: cv.set(x,y,R0)
                elif k==rr[y]+1 and y>1: cv.set(x,y,R3 if left else R1)
        if not lower:
            for k in range(3):
                x=k if left else 15-k
                if k<2: cv.clear(x,0)
    return cv

def blob(cv,rng,cx,cy,rx,ry,ramp,jitter=0.35):
    for y in range(16):
        for x in range(16):
            d=((x+.5-cx)/rx)**2+((y+.5-cy)/ry)**2
            if d<=1+rng.uniform(-jitter,jitter)*0.6:
                lvl=3 if (x-cx)+(y-cy)<-1 and d<0.5 else (2 if (x-cx)+(y-cy)<1 else (1 if d<0.8 else 0))
                if rng.random()<0.12: lvl=min(3,lvl+1)
                cv.set(x,y,ramp[lvl])

def make_decor(name,seed):
    rng=random.Random(seed); cv=Cv()
    if name=="Moss":
        for _ in range(3): blob(cv,rng,rng.randrange(3,13),rng.randrange(3,13),rng.uniform(2.5,4.5),rng.uniform(2,3.5),MOSS)
    elif name=="Snow":
        for _ in range(3): blob(cv,rng,rng.randrange(3,13),rng.randrange(3,13),rng.uniform(3,5),rng.uniform(2,3.5),SNOW)
    elif name=="Ice":
        for _ in range(2): blob(cv,rng,rng.randrange(4,12),rng.randrange(4,12),rng.uniform(3,5),rng.uniform(2.5,4),ICE)
    elif name=="Crack":
        x=rng.randrange(4,11); y=0; dx=rng.choice([-1,1])
        while y<16:
            cv.set(x,y,R0)
            if rng.random()<0.35: cv.set(x+dx,y,R0)
            y+=1; x+=dx if rng.random()<0.45 else 0
            if rng.random()<0.15: dx=-dx
            x=max(1,min(14,x))
    elif name=="Rubble":
        for _ in range(7):
            x=rng.randrange(1,13); y=rng.randrange(6,14); w=rng.choice([2,3]); h=rng.choice([1,2])
            for yy in range(h):
                for xx in range(w): cv.set(x+xx,y+yy,R2 if yy==0 else R1)
            cv.set(x,y,R3)
            for xx in range(w): cv.set(x+xx,y+h,R0)
    elif name=="Pebbles":
        for _ in range(9):
            x=rng.randrange(1,15); y=rng.randrange(3,15)
            cv.set(x,y,R3); cv.set(x+1,y,R2); cv.set(x,y+1,R1)
    elif name=="Vines":
        for _ in range(4):
            x=rng.randrange(1,15); L=rng.randrange(5,13)
            for y in range(L):
                cv.set(x,y,MOSS[1] if y%2 else MOSS[2])
                if y%4==3: cv.set(x+1,y,MOSS[3]); cv.set(x-1,y-1,MOSS[0])
    elif name=="SmallRock":
        for y in range(16):
            for x in range(16):
                d=((x+.5-8)/4.6)**2+((y+.5-10.5)/3.6)**2
                if d<=1:
                    lvl=3 if (x-8)+(y-10)<-1 else (2 if (x-8)+(y-10)<1 else 1)
                    cv.set(x,y,[R1,R2,R3,R4][lvl-0 if lvl<4 else 3] if lvl>=1 else R1)
        for y in range(16):
            for x in range(16):
                if cv.opaque(x,y) and not all(cv.opaque(x+dx,y+dy) for dx,dy in ((1,0),(-1,0),(0,1),(0,-1))): cv.set(x,y,R0)
    return cv

def build():
    sheet=Image.new('RGBA',(256,48),(0,0,0,0))
    for i,n in enumerate(NAMES):
        if n is None: continue
        seed=101+i*13
        if n.startswith("Fill"): cv=Cv(); rock(cv,random.Random(seed),seed)
        elif n.startswith("Lip"): cv=make_lip(n,seed)
        elif n.startswith("Inner"): cv=make_inner(n,seed)
        elif n.startswith("Face"): cv=make_face(n,i,seed)
        else: cv=make_decor(n,seed)
        sheet.paste(Image.fromarray(cv.a,'RGBA'),((i%16)*16,(i//16)*16))
    return sheet

def preview(sheet,fn):
    # a little cliff on grass: 7 wide x 6 tall, using the same rules as the brush
    S=3; bg=(95,168,77,255)
    t={n:sheet.crop(((i%16)*16,(i//16)*16,(i%16)*16+16,(i//16)*16+16)) for i,n in enumerate(NAMES) if n}
    W,H=9,7
    im=Image.new('RGBA',(W*16,H*16),bg)
    r=random.Random(5)
    for cy in range(1,H-1):
        for cx in range(1,W-1):
            # region: cells 1..W-2 x 1..H-2 ; bottom 2 rows are the face
            left=cx==1; right=cx==W-2; topr=cy==1
            low=cy==H-2; up=cy==H-3
            if low or up:
                base="FaceLower" if low else "FaceUpper"
                n=base+("_L" if left else "_R" if right else "_%d"%r.randint(1,4))
            elif topr: n="Lip_TL" if left else "Lip_TR" if right else "Lip_T"
            elif left: n="Lip_L"
            elif right: n="Lip_R"
            else: n="Fill_%d"%r.randint(1,4)
            im.alpha_composite(t[n],(cx*16,cy*16))
    im.alpha_composite(t["Moss"],(3*16,2*16)); im.alpha_composite(t["Rubble"],(4*16,H*16-16-8)) if False else None
    im=im.resize((im.width*S,im.height*S),Image.NEAREST); im.save(fn)
    # all tiles on grass
    sh=Image.new('RGBA',(256,48),bg); sh.alpha_composite(sheet)
    sh.resize((256*3,48*3),Image.NEAREST).save(fn.replace('.png','_sheet.png'))

if __name__=="__main__":
    out=sys.argv[1] if len(sys.argv)>1 else "."
    s=build(); s.save(out+"/cliff_tiles.png"); preview(s,out+"/prev_cliff.png"); print("ok")
