import sys, os, math, random
SRC=open('/home/claude/mtn_out/gen_mountain.py').read().split('# ============ ROCKS')[0]
OUTDIR=sys.argv[1]
sys.argv=[sys.argv[0],OUTDIR]
exec(SRC)
random.seed(11)

SKIN=(226,232,196,255); SKINH=(242,246,216,255); SKINL=(186,198,158,255)
DRESS=(96,150,130,255); DRESSH=(140,185,160,255); DRESSL=(64,106,92,255)
WING=(150,196,188,190); WINGD=(120,160,156,170)
HAIR=(214,190,100,255); HAIRH=(238,218,140,255); HAIRL=(170,146,70,255)
INK=(60,60,80,255); SWEAT=(150,210,255,255); SWEATH=(225,245,255,255)
FLUSH=(240,140,130,200)

def sweat(c,x,y):
    c.set(x,y,SWEAT); c.set(x,y+1,SWEAT); c.set(x-1,y+2,SWEAT); c.set(x,y+2,SWEATH); c.set(x+1,y+2,SWEAT)

def fairy_up(wings):
    c=Cv(24,24)
    if wings=='up':       # weak flap, wings only half raised
        c.poly([(11,11),(6,5),(5,10),(10,13)],WING); c.poly([(13,11),(18,5),(19,10),(14,13)],WING)
    else:                 # drooping
        c.poly([(11,12),(5,13),(6,18),(10,15)],WINGD); c.poly([(13,12),(19,13),(18,18),(14,15)],WINGD)
    c.ell(12,17,2.6,4,DRESS,hi=DRESSH,lo=DRESSL)
    c.ell(12,11,3,3,SKIN,hi=SKINH,lo=SKINL)
    c.poly([(8,9),(12,6),(16,9),(12,8)],HAIR); c.set(12,7,HAIRH); c.set(8,10,HAIRL)
    c.line(10,11,11,11,INK); c.line(13,11,14,11,INK)       # half-closed eyes
    c.set(11,13,FLUSH); c.set(13,13,FLUSH)
    sweat(c,17,8)
    for x,y in ((10,21),(14,22)): c.set(x,y,(200,220,190,120))
    c.outline(); return c

def fairy_fall():
    c=Cv(24,24)
    c.poly([(11,11),(7,1),(4,6),(9,13)],WINGD); c.poly([(13,11),(17,1),(20,6),(15,13)],WINGD)   # wings thrown up
    c.ell(12,17,2.6,4,DRESS,hi=DRESSH,lo=DRESSL)
    c.line(9,14,6,10,SKIN); c.line(15,14,18,10,SKIN)       # arms flailing
    c.ell(12,11,3,3,SKIN,hi=SKINH,lo=SKINL)
    c.poly([(8,9),(12,6),(16,9),(12,8)],HAIR)
    c.line(10,10,11,12,INK); c.line(11,10,10,12,INK); c.line(13,10,14,12,INK); c.line(14,10,13,12,INK)  # x_x
    sweat(c,18,12)
    c.outline(); return c

def fairy_lie(f):
    c=Cv(24,24)
    # wings folded flat across the back
    c.poly([(11,16),(17,13),(21,16),(15,19)],WINGD)
    if f==1: c.poly([(12,15),(16,11),(19,13),(15,17)],WING)     # one wing twitches up
    c.ell(14,20,5,2.8,DRESS,hi=DRESSH,lo=DRESSL)                # body
    c.ell(19,21,2.2,1.4,SKIN,lo=SKINL)                          # feet
    c.ell(7,19,3,3,SKIN,hi=SKINH,lo=SKINL)                      # head
    c.poly([(3,17),(7,14),(10,17),(7,17)],HAIR)
    if f==0: c.line(5,19,7,19,INK)                              # eyes shut
    else: c.set(5,19,INK); c.set(7,19,INK); c.set(5,18,(255,255,255,255)); c.set(7,18,(255,255,255,255))
    c.set(6,21,FLUSH); c.set(8,21,FLUSH)
    c.line(9,21,11,21,SKINL) if f==0 else None
    if f==1:
        for x,y in ((2,14),(1,12),(3,11)): c.set(x,y,(210,230,200,150))   # cough puffs
    else: sweat(c,10,12)
    c.outline(); return c

def fairy_sit():
    c=Cv(24,24)
    c.poly([(10,14),(5,17),(6,22),(10,20)],WINGD); c.poly([(14,14),(19,17),(18,22),(14,20)],WINGD)   # wings drooping
    c.ell(12,19,3.4,3.6,DRESS,hi=DRESSH,lo=DRESSL)
    c.ell(9,22,2,1.2,SKIN,lo=SKINL); c.ell(15,22,2,1.2,SKIN,lo=SKINL)   # feet
    c.ell(12,11,3.2,3.2,SKIN,hi=SKINH,lo=SKINL)
    c.poly([(8,9),(12,6),(16,9),(12,8)],HAIR); c.set(12,7,HAIRH)
    c.line(10,12,11,12,INK); c.line(13,12,14,12,INK)
    c.set(11,14,FLUSH); c.set(13,14,FLUSH)
    sweat(c,17,9)
    c.outline(); return c

strip([fairy_up('up'),fairy_up('down'),fairy_fall(),fairy_lie(0),fairy_lie(1),fairy_sit()],'fairy_sick')

# ---- portraits (idle, talk, weak smile, blink), 64x64 ----
def portrait(mouth,eyes='open'):
    c=Cv(64,64)
    for y in range(64):
        for x in range(64):
            d=math.hypot(x-32,y-34)
            if d<28: c.set(x,y,(190,215,205,int(max(0,(28-d))*1.8)))
    c.poly([(13,12),(3,36),(24,34)],(150,190,184,150)); c.poly([(51,12),(61,36),(40,34)],(150,190,184,150))   # drooping wings
    c.ell(32,48,12,12,DRESS,hi=DRESSH,lo=DRESSL)
    c.ell(32,32,14,14,SKIN,hi=SKINH,lo=SKINL)
    c.ell(32,17,15,8,HAIR,hi=HAIRH,lo=HAIRL)
    c.rect(17,18,20,38,HAIR); c.rect(44,18,47,38,HAIR)
    c.poly([(20,12),(24,6),(26,13)],HAIR); c.poly([(36,10),(41,5),(42,13)],HAIR)   # messy hair
    for ex in (25,39):
        c.ell(ex,38,4,1.6,(140,150,165,150))                       # dark circles
        if eyes=='closed': c.line(ex-3,34,ex+3,35,INK)
        elif eyes=='happy': c.line(ex-3,35,ex,33,INK); c.line(ex,33,ex+3,35,INK)
        else:
            c.ell(ex,34,3,3.2,(255,255,255,255)); c.ell(ex,35,2,2.2,(70,110,100,255)); c.set(ex+1,34,(255,255,255,255))
            c.rect(ex-3,31,ex+3,32,SKIN); c.line(ex-3,33,ex+3,33,INK)   # heavy lid
    c.ell(32,40,2.2,2.2,(240,130,125,220))                            # red nose
    c.ell(21,41,3.5,2.5,FLUSH); c.ell(43,41,3.5,2.5,FLUSH)           # fever cheeks
    if mouth=='open': c.ell(32,46,2.5,2,(150,70,80,255))
    elif mouth=='smile': c.line(29,45,31,46,(150,70,80,255)); c.line(31,46,34,46,(150,70,80,255)); c.line(34,46,36,45,(150,70,80,255))
    else: c.line(29,46,31,45,(150,70,80,255)); c.line(31,45,33,46,(150,70,80,255)); c.line(33,46,35,45,(150,70,80,255))   # wobbly
    # sweat drop
    c.ell(51,26,2,3,SWEAT,hi=SWEATH); c.poly([(49,25),(51,20),(53,25)],SWEAT)
    return c
im=Image.new('RGBA',(256,64),(0,0,0,0))
for i,p in enumerate((portrait('wobble'),portrait('open'),portrait('smile','happy'),portrait('wobble','closed'))):
    im.paste(p.im,(i*64,0))
im.save(os.path.join(OUT,'fairy_sick_portraits.png'))

# ---- the fairy door: 32x32, bottom-centre pivot ----
BARK=(104,76,56,255); BARKH=(138,104,76,255); BARKL=(70,50,38,255)
MOSS=(86,150,86,255); MOSSH=(120,190,110,255)
def door(open_):
    c=Cv(32,32)
    # pillars + arch
    c.rect(2,9,7,31,BARK); c.rect(2,9,3,31,BARKH); c.rect(7,9,7,31,BARKL)
    c.rect(24,9,29,31,BARK); c.rect(24,9,25,31,BARKH); c.rect(29,9,29,31,BARKL)
    c.ell(16,10,14,8,BARK,hi=BARKH,lo=BARKL,seed=3,noise=0.06)
    # carve the opening
    for y in range(8,32):
        for x in range(8,24):
            if y>=11+ (abs(x-15.5)**2)/14: c.px[x,y]=(0,0,0,0)
    # fill the doorway
    for y in range(8,32):
        for x in range(8,24):
            if c.get(x,y)[3]==0 and y>=11+(abs(x-15.5)**2)/14:
                if open_:
                    d=math.hypot((x-15.5)/8.0,(y-24)/14.0)
                    k=max(0.0,1-d)
                    c.set(x,y,(int(150+90*k),int(225+25*k),int(200+40*k),255))
                else:
                    c.set(x,y,(34,28,54,255) if (x+y)%7 else (44,36,66,255))
    # moss on top, little flowers
    for x in range(5,28,2): c.set(x,3+int(abs(x-16)/3.2),MOSS)
    for x,y in ((9,5),(16,2),(23,5)):
        c.set(x,y,MOSSH)
    c.set(12,3,(255,170,210,255)); c.set(20,3,(255,230,120,255)); c.set(16,1,(255,170,210,255))
    # mushrooms at the feet
    for mx in (4,27):
        c.rect(mx,29,mx,30,(240,230,210,255)); c.ell(mx,28,2,1.3,(220,70,80,255)); c.set(mx-1,28,(255,255,255,255))
    if open_:
        r=random.Random(5)
        for _ in range(9):
            x=r.randint(9,22); y=r.randint(14,29); c.set(x,y,(255,255,230,255))
        for x,y in ((8,20),(23,22),(8,26)): c.set(x,y,(210,255,235,255))
    else:
        # thorny vines across the door
        c.line(8,14,23,26,(54,40,74,255)); c.line(8,26,23,16,(54,40,74,255)); c.line(8,20,23,21,(70,52,92,255))
        for x,y in ((11,17),(15,21),(19,23),(12,24),(20,18)): c.set(x,y,(150,60,200,255))
        c.set(16,12,(150,60,200,200))     # dim rune
    c.outline(); return c
save(door(False),'fairy_door_locked'); save(door(True),'fairy_door_open')

# preview
from PIL import Image as I
names=['fairy_sick','fairy_sick_portraits','fairy_door_locked','fairy_door_open']
ims=[I.open(os.path.join(OUT,n+'.png')) for n in names]
W=max(i.width for i in ims)*6; H=sum(i.height for i in ims)*6+40
pv=I.new('RGBA',(W,H),(60,90,60,255)); y=0
for i in ims:
    b=i.resize((i.width*6,i.height*6),I.NEAREST); pv.paste(b,(0,y),b); y+=b.height+10
pv.save('/home/claude/sick_out/preview.png')
