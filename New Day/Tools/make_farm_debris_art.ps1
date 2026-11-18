# Draws the pixel art for the random farm clutter: pollution litter (crushed can, debris, black tar lump, purple goo lump),
# three weeds, a fallen tree and the Fiber icon. Everything is 16 pixels per unit, feet at the bottom edge.
# Run it again any time you change a picture or a colour:
#
#     powershell -ExecutionPolicy Bypass -File Tools\make_farm_debris_art.ps1 -Project "<project folder>"
#
# It writes ONE PNG PER PICTURE into Assets\Project\Art\FarmDebris (no sprite sheets, so nothing to slice).
# Then run Farm > Set Up > Farm Debris in Unity.
param([string]$Project = (Split-Path -Parent $PSScriptRoot))

Add-Type -AssemblyName System.Drawing
$out = Join-Path $Project "Assets\Project\Art\FarmDebris"
New-Item -ItemType Directory -Force $out | Out-Null

function Hex([string]$h) { return [System.Drawing.ColorTranslator]::FromHtml($h) }
function New-Bmp([int]$w, [int]$h) { return New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb) }
function Save-Bmp($bmp, [string]$name) { $bmp.Save((Join-Path $out ($name + ".png")), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose() }

# Turns rows of letters into a picture. Each letter is looked up in $palette ('.' or an unknown letter = see-through).
# Palette keys must not differ only by upper / lower case: PowerShell hashtables treat "a" and "A" as the same key.
function Draw-Ascii([string[]]$rows, [int]$w, [int]$h, [hashtable]$palette) {
    $bmp = New-Bmp $w $h
    for ($y = 0; $y -lt $h -and $y -lt $rows.Length; $y++) {
        $row = $rows[$y]
        for ($x = 0; $x -lt $w -and $x -lt $row.Length; $x++) {
            $key = [string]$row[$x]
            if ($palette.ContainsKey($key)) { $bmp.SetPixel($x, $y, (Hex $palette[$key])) }
        }
    }
    return $bmp
}

# ---------------------------------------------------------------- pollution litter
# a crushed soda can lying on its side: o outline, s silver, w shine, r red label, d dark red
$can = @{ o = "#33343c"; s = "#aeb3c0"; w = "#eef0f5"; r = "#c8323a"; d = "#8e1f28" }
Save-Bmp (Draw-Ascii @(
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "....oooooooo....",
    "...oswrrrdsso...",
    "..oswwrrrddsso..",
    "..osswrrdddsso..",
    "...osrrrrddso...",
    "....ooooooooo...",
    "................"
) 16 16 $can) "litter_can"

# debris: a broken plank and a bent bit of metal: k dark wood, b wood, g metal edge, m metal
$debris = @{ k = "#4e321d"; b = "#9a6a3c"; g = "#454a55"; m = "#8a93a3" }
Save-Bmp (Draw-Ascii @(
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "..........gg....",
    ".........gmmg...",
    "..kkkk..gmmmg...",
    ".kbbbbk.gmmg....",
    "kbbkbbbkkgg.....",
    "kbbbbbkbbbk.....",
    ".kkbbbbbbbbk....",
    "...kkkkkkkk.....",
    "................"
) 16 16 $debris) "litter_debris"

# a black tar lump with an oily shine: k outline, t tar, h shine
$tar = @{ k = "#0b0b0e"; t = "#22212a"; h = "#5d6275" }
Save-Bmp (Draw-Ascii @(
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "......kkkk......",
    "....kkttttkk....",
    "...ktthtttttk...",
    "..kthhtttttttk..",
    "..kttttttttttk..",
    ".kttttttttthttk.",
    ".kttttttttttttk.",
    "..kkkkkkkkkkkk..",
    "................"
) 16 16 $tar) "litter_tar"

# a purple goo lump, glowing a little: k outline, p goo, l light, g glow specks
$goo = @{ k = "#3a1450"; p = "#8a2fb0"; l = "#d98af5"; g = "#b86ae0" }
Save-Bmp (Draw-Ascii @(
    "................",
    "................",
    "................",
    "................",
    "........g.......",
    "...g............",
    ".......kk.......",
    "......kplk...g..",
    "....kkpppkk.....",
    "...kpllppppk....",
    "..kplpppppppk...",
    "..kpppppkpppk...",
    ".kpppppkpkpppk..",
    ".kppppppppppppk.",
    "..kkkkpkkkkkkk..",
    "......k........."
) 16 16 $goo) "litter_goo"

# ---------------------------------------------------------------- weeds: k dark green, g green, l light green, y seed / flower
$weed = @{ k = "#2b5422"; g = "#4f8f34"; l = "#8cc850"; y = "#d8c050"; f = "#e8d040" }
Save-Bmp (Draw-Ascii @(
    "................",
    "................",
    "................",
    "................",
    ".......k........",
    "...k...k...k....",
    "...kk.kgk.kk....",
    "....kgkgkgk.....",
    "..k.kgggggkk.k..",
    "..kkgggggggkk...",
    "...kgggllgggk...",
    "....ggglgggk....",
    "...kgggggggk....",
    "....kkgggkk.....",
    ".....kkkkk......",
    "................"
) 16 16 $weed) "weed_a"

Save-Bmp (Draw-Ascii @(
    "................",
    "......y...y.....",
    ".....yy..yy.....",
    "......k...k.....",
    "..y...k..k......",
    "..yy..k..k...y..",
    "...k..k.k...yy..",
    "...k..kkk...k...",
    "....k.kgk..k....",
    "....kkggk.k.....",
    ".....kglgkk.....",
    "....kgglggk.....",
    "...kggglgggk....",
    "....kggggggk....",
    ".....kkkkkk.....",
    "................"
) 16 16 $weed) "weed_b"

Save-Bmp (Draw-Ascii @(
    "................",
    "................",
    "................",
    "................",
    "................",
    "......fff.......",
    ".....fffff......",
    "......fff.......",
    ".......k........",
    "..kk...k...kk...",
    ".kggk..k..kggk..",
    ".kglgk.k.kglgk..",
    "..kgglkkklggk...",
    "...kgggggggk....",
    "....kkkkkkk.....",
    "................"
) 16 16 $weed) "weed_c"

# ---------------------------------------------------------------- a fallen tree (32 x 16): o outline, b bark, d bark dark, m moss, r ring, c core, l leaf, n twig
$log = @{ o = "#2e1c10"; b = "#7a4e2c"; d = "#5a3820"; m = "#5f8f3a"; r = "#c9a06a"; c = "#a37a48"; l = "#4f8f34"; n = "#5a3820" }
Save-Bmp (Draw-Ascii @(
    "................................",
    "................................",
    "................................",
    "................................",
    "...........ll...................",
    "..........llnl..................",
    "...........lnll.................",
    "............n...................",
    "..ooooooooooooooooooooooooooo...",
    ".obbmmbbbdbbbbbbbbdbbmmmbbbbboo.",
    ".obdbbbbbbbbdbbbbbbbbbbbbdbborro",
    ".obbbbbdbbbbbbbbbbdbbbbbbbbbocro",
    ".odbbbbbbbbbdbbbbbbbbbdbbbbborro",
    ".obbbdbbbbbbbbbbbdbbbbbbbbbbboo.",
    "..ooooooooooooooooooooooooooo...",
    "................................"
) 32 16 $log) "fallen_tree"

# ---------------------------------------------------------------- the Fiber icon: a little bundle of green strands tied with string
$fiber = @{ k = "#2b5422"; g = "#5f9f3a"; l = "#9ad25a"; t = "#c8a060" }
Save-Bmp (Draw-Ascii @(
    "................",
    "................",
    "...k.....k......",
    "...gk...kg..k...",
    "....gk.kgl.kg...",
    "....lgkglgkgl...",
    ".....lggglgl....",
    "......glglg.....",
    "......ttttt.....",
    "......glglg.....",
    ".....glgglgl....",
    "....glg.glg.g...",
    "....gl..gl..gl..",
    "...gl...gl...g..",
    "...k....k....k..",
    "................"
) 16 16 $fiber) "icon_fiber"

Write-Output ("Wrote the farm debris art to " + $out)
