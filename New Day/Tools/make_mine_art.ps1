# Draws all the pixel art of the mine shaft: floor / wall / pool tiles for the five themes, the stairs, ladder, cart and rails,
# and the new enemies. Everything is 16 pixels per unit. Run it again any time you change a colour:
#
#     powershell -ExecutionPolicy Bypass -File Tools\make_mine_art.ps1 -Project "<project folder>"
#
# It writes ONE PNG PER PICTURE into Assets\Project\Art\MineShaft (so there are no sprite sheets to slice), except the three
# recoloured bat / bug sheets, which keep the layout of the originals. Then run Farm > Set Up > Mine Shaft in Unity.
param([string]$Project = (Split-Path -Parent $PSScriptRoot))

Add-Type -AssemblyName System.Drawing
$out = Join-Path $Project "Assets\Project\Art\MineShaft"
New-Item -ItemType Directory -Force $out | Out-Null
$mountain = Join-Path $Project "Assets\Project\Art\Mountain"

function Hex([string]$h) { return [System.Drawing.ColorTranslator]::FromHtml($h) }
function New-Bmp([int]$w, [int]$h) { return New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb) }
function Save-Bmp($bmp, [string]$name) { $bmp.Save((Join-Path $out ($name + ".png")), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose() }
function Fill($bmp, $color) { for ($y = 0; $y -lt $bmp.Height; $y++) { for ($x = 0; $x -lt $bmp.Width; $x++) { $bmp.SetPixel($x, $y, $color) } } }
function Dot($bmp, [int]$x, [int]$y, $color) { if ($x -ge 0 -and $y -ge 0 -and $x -lt $bmp.Width -and $y -lt $bmp.Height) { $bmp.SetPixel($x, $y, $color) } }

# Turns rows of letters into a picture. Each letter is looked up in $palette ('.' or an unknown letter = see-through).
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

# Squashes (factor below 1) or stretches (above 1) a picture up and down, keeping its feet on the bottom edge.
function Scale-Y($src, [double]$factor) {
    $bmp = New-Bmp $src.Width $src.Height
    for ($fromBottom = 0; $fromBottom -lt $src.Height; $fromBottom++) {
        $srcFromBottom = [int][Math]::Floor($fromBottom / $factor)
        if ($srcFromBottom -ge $src.Height) { continue }
        for ($x = 0; $x -lt $src.Width; $x++) {
            $bmp.SetPixel($x, $src.Height - 1 - $fromBottom, $src.GetPixel($x, $src.Height - 1 - $srcFromBottom))
        }
    }
    return $bmp
}

function Mirror($src) { $bmp = $src.Clone(); $bmp.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipX); return $bmp }

# ---------------------------------------------------------------- tiles
function Speckle($bmp, $rng, [int]$count, $color) { for ($i = 0; $i -lt $count; $i++) { $bmp.SetPixel($rng.Next($bmp.Width), $rng.Next($bmp.Height), $color) } }

function Make-Floor([string]$name, [int]$seed, $t, [string]$variant) {
    $rng = New-Object System.Random($seed)
    $bmp = New-Bmp 16 16
    Fill $bmp (Hex $t.floor)
    Speckle $bmp $rng 26 (Hex $t.floorDark)
    Speckle $bmp $rng 16 (Hex $t.floorLight)
    if ($variant -eq "b") {
        # a few pebbles: a light dot with a dark one under it
        for ($i = 0; $i -lt 3; $i++) { $x = 2 + $rng.Next(12); $y = 2 + $rng.Next(11); Dot $bmp $x $y (Hex $t.floorLight); Dot $bmp ($x + 1) $y (Hex $t.floorLight); Dot $bmp $x ($y + 1) (Hex $t.floorDark); Dot $bmp ($x + 1) ($y + 1) (Hex $t.floorDark) }
    }
    if ($variant -eq "c") {
        # a crack wandering across the tile
        $x = 3 + $rng.Next(4); $y = 2 + $rng.Next(3)
        for ($i = 0; $i -lt 10; $i++) { Dot $bmp $x $y (Hex $t.floorDark); $x += 1; $y += $rng.Next(3) - 1 + $(if ($i % 3 -eq 0) { 1 } else { 0 }) }
    }
    if ($t.ember) { Speckle $bmp $rng 3 (Hex $t.ember) }
    Save-Bmp $bmp $name
}

function Make-WallTop([string]$name, [int]$seed, $t) {
    $rng = New-Object System.Random($seed)
    $bmp = New-Bmp 16 16
    Fill $bmp (Hex $t.top)
    Speckle $bmp $rng 30 (Hex $t.topDark)
    for ($i = 0; $i -lt 4; $i++) { $x = $rng.Next(15); $y = $rng.Next(16); Dot $bmp $x $y (Hex $t.topLight); Dot $bmp ($x + 1) $y (Hex $t.topLight) }
    Save-Bmp $bmp $name
}

function Make-WallFace([string]$name, [int]$seed, $t) {
    $rng = New-Object System.Random($seed)
    $bmp = New-Bmp 16 16
    Fill $bmp (Hex $t.face)
    Speckle $bmp $rng 18 (Hex $t.faceDark)
    for ($x = 0; $x -lt 16; $x++) {
        $bmp.SetPixel($x, 0, (Hex $t.top)); $bmp.SetPixel($x, 1, (Hex $t.faceLight)); $bmp.SetPixel($x, 2, (Hex $t.faceLight))   # the lit rim at the top
        $bmp.SetPixel($x, 7, (Hex $t.faceDark)); $bmp.SetPixel($x, 11, (Hex $t.faceDark))                                         # layers of rock
        $bmp.SetPixel($x, 14, (Hex $t.faceDark)); $bmp.SetPixel($x, 15, (Hex $t.topDark))                                         # shadow at the foot
    }
    foreach ($joint in @(@(4, 3, 6), @(12, 3, 6), @(8, 8, 10), @(0, 8, 10), @(5, 12, 13), @(13, 12, 13))) { for ($y = $joint[1]; $y -le $joint[2]; $y++) { $bmp.SetPixel($joint[0], $y, (Hex $t.faceDark)) } }
    Save-Bmp $bmp $name
}

function Make-Pool([string]$name, [int]$seed, $t) {
    $rng = New-Object System.Random($seed)
    $bmp = New-Bmp 16 16
    Fill $bmp (Hex $t.pool)
    Speckle $bmp $rng 14 (Hex $t.poolDark)
    for ($i = 0; $i -lt 5; $i++) { $x = $rng.Next(12); $y = 1 + $rng.Next(14); $len = 2 + $rng.Next(3); for ($k = 0; $k -lt $len; $k++) { Dot $bmp ($x + $k) $y (Hex $t.poolLight) } }
    Save-Bmp $bmp $name
}

$themes = [ordered]@{
    ice    = @{ floor = "#b9d6e8"; floorDark = "#a3c4da"; floorLight = "#d6eaf5"; top = "#3d5a78"; topDark = "#324b66"; topLight = "#56779a"; face = "#6f93b5"; faceDark = "#587a9c"; faceLight = "#a9cbe4"; pool = "#8fd0f0"; poolDark = "#74bfe6"; poolLight = "#eefaff" }
    rock   = @{ floor = "#8a7560"; floorDark = "#7a6652"; floorLight = "#9c8872"; top = "#3a2f28"; topDark = "#2e251f"; topLight = "#4d4037"; face = "#5d4c3f"; faceDark = "#483a30"; faceLight = "#7c6856"; pool = "#17120f"; poolDark = "#0e0b09"; poolLight = "#2a211b" }
    cave   = @{ floor = "#5f7d78"; floorDark = "#526e6a"; floorLight = "#6e8e88"; top = "#1f3336"; topDark = "#18282b"; topLight = "#2d474b"; face = "#35555a"; faceDark = "#294449"; faceLight = "#4f777b"; pool = "#2f7fb5"; poolDark = "#256a9c"; poolLight = "#7cc0e6" }
    desert = @{ floor = "#d9b676"; floorDark = "#c9a563"; floorLight = "#e8cb90"; top = "#7a4f2c"; topDark = "#684224"; topLight = "#93623a"; face = "#a8713f"; faceDark = "#8c5b30"; faceLight = "#cb9358"; pool = "#b08a4c"; poolDark = "#96733c"; poolLight = "#c9a868" }
    lava   = @{ floor = "#4a3434"; floorDark = "#3c2828"; floorLight = "#5c4242"; top = "#1c1214"; topDark = "#140c0e"; topLight = "#2e1e20"; face = "#3a2224"; faceDark = "#2a1618"; faceLight = "#603434"; pool = "#e8601c"; poolDark = "#c2440e"; poolLight = "#ffc23a"; ember = "#c0502a" }
}
$seed = 100
foreach ($key in $themes.Keys) {
    $t = $themes[$key]
    Make-Floor "tile_${key}_floor_a" ($seed + 1) $t "a"
    Make-Floor "tile_${key}_floor_b" ($seed + 2) $t "b"
    Make-Floor "tile_${key}_floor_c" ($seed + 3) $t "c"
    Make-WallTop "tile_${key}_wall_top" ($seed + 4) $t
    Make-WallFace "tile_${key}_wall_face" ($seed + 5) $t
    Make-Pool "tile_${key}_pool" ($seed + 6) $t
    $seed += 10
}

# ---------------------------------------------------------------- stairs, ladder, cart, rails
# Stairs down: a stone rim with steps that get darker as they go down (toward the top of the picture).
$bmp = New-Bmp 16 16
$steps = @("#000000", "#000000", "#0a0808", "#1c1816", "#1c1816", "#0a0808", "#3a332e", "#3a332e", "#1c1816", "#5d544c", "#5d544c", "#3a332e", "#857a6e", "#857a6e")
for ($y = 0; $y -lt 16; $y++) { for ($x = 0; $x -lt 16; $x++) {
    $rim = ($x -eq 0 -or $x -eq 15 -or $y -eq 0 -or $y -eq 15)
    $bmp.SetPixel($x, $y, $(if ($rim) { Hex "#2a2420" } else { Hex $steps[$y - 1] }))
} }
Save-Bmp $bmp "mine_stairs_down"

# Ladder up: two wooden rails and rungs, 16 x 24, standing on the floor.
$bmp = New-Bmp 16 24
for ($y = 0; $y -lt 24; $y++) {
    foreach ($x in @(3, 4, 11, 12)) { $bmp.SetPixel($x, $y, (Hex $(if ($x -eq 3 -or $x -eq 11) { "#8a5a2e" } else { "#6b4320" }))) }
    if ($y % 5 -eq 2) { for ($x = 5; $x -le 10; $x++) { $bmp.SetPixel($x, $y, (Hex "#a8743c")); if ($y + 1 -lt 24) { $bmp.SetPixel($x, $y + 1, (Hex "#5a3818")) } } }
}
Save-Bmp $bmp "mine_ladder_up"

# Rails: two steel rails over wooden sleepers. Tiles sideways.
$bmp = New-Bmp 16 16
foreach ($x in @(1, 2, 3, 9, 10, 11)) { for ($y = 2; $y -le 13; $y++) { $bmp.SetPixel($x, $y, (Hex $(if ($x -eq 3 -or $x -eq 11) { "#4a3018" } else { "#6b4826" }))) } }
for ($x = 0; $x -lt 16; $x++) { $bmp.SetPixel($x, 4, (Hex "#b9bec4")); $bmp.SetPixel($x, 5, (Hex "#6c737a")); $bmp.SetPixel($x, 10, (Hex "#b9bec4")); $bmp.SetPixel($x, 11, (Hex "#6c737a")) }
Save-Bmp $bmp "mine_rails"

$cart = @(
    "........................",
    "..oooooooooooooooooooo..",
    "..owwwwwwwwwwwwwwwwwwo..",
    "..oxxxxxxxxxxxxxxxxxxo..",
    "...owwwwwwwwwwwwwwwwo...",
    "...owmwwwwwwwwwwwwmwo...",
    "...owwwwwwwwwwwwwwwwo...",
    "...oxxxxxxxxxxxxxxxxo...",
    "....owwwwwwwwwwwwwwo....",
    "....owmwwwwwwwwwwmwo....",
    "....oooooooooooooooo....",
    "...okkkkkkkkkkkkkkkko...",
    "....ogggo......ogggo....",
    "...ogsssgo....ogsssgo...",
    "...ogsssgo....ogsssgo...",
    "....ogggo......ogggo....")
Save-Bmp (Draw-Ascii $cart 24 16 @{ o = "#2a1c12"; w = "#a8743c"; x = "#7a4f26"; m = "#c9ced4"; k = "#3a3a40"; g = "#5a5f66"; s = "#aab0b8" }) "mine_cart"

# ---------------------------------------------------------------- enemies
# Slime: one drawing, four frames made by squashing and stretching it (sit, squashed, in the air, landing).
$slime = @(
    "................", "................", "................", "................", "................",
    ".....oooooo.....",
    "...oohhhhhhoo...",
    "..ohhllhhhhhho..",
    "..ohllhhhhhhho..",
    ".ohhhhhhhhhhhho.",
    ".ohheehhhheehho.",
    ".ohheehhhheehho.",
    ".ohhhhhhhhhhhho.",
    ".oddhhhhhhhhddo.",
    "..oddddddddddo..",
    "...oooooooooo...")
$slimes = @{
    frost = @{ o = "#2c5a7a"; h = "#9fd8f0"; l = "#e4f7ff"; d = "#5fa6cc"; e = "#1b2b3a" }
    mud   = @{ o = "#33230f"; h = "#8c6a45"; l = "#b08d62"; d = "#5e4428"; e = "#f4e7b0" }
    magma = @{ o = "#4a1206"; h = "#e8601c"; l = "#ffc23a"; d = "#a8320e"; e = "#fff0a0" }
}
foreach ($v in $slimes.Keys) {
    $base = Draw-Ascii $slime 16 16 $slimes[$v]
    Save-Bmp (Scale-Y $base 0.72) "slime_${v}_1"
    Save-Bmp (Scale-Y $base 1.25) "slime_${v}_2"
    Save-Bmp (Scale-Y $base 0.6) "slime_${v}_3"
    Save-Bmp $base "slime_${v}_0"
}

# Spitter: a squat creature with a big mouth. Frame 2 (about to spit) has a glowing mouth and is puffed up.
$spitter = @(
    "................", "................",
    ".....oooooo.....",
    "....obbbbbbo....",
    "...obbllbbbbo...",
    "...obeebbeebo...",
    "...obeebbeebo...",
    "...obbbbbbbbo...",
    "...obbmmmmbbo...",
    "....obmmmmbo....",
    ".....obbbbo.....",
    "....oddbbddo....",
    "...oddddddddo...",
    "..oddddddddddo..",
    "..oddddddddddo..",
    "...oooooooooo...")
$spitters = @{
    rock   = @{ o = "#2c2722"; b = "#8d8478"; l = "#b5ada0"; d = "#5a5248"; e = "#ffd23e"; m = "#3a2a2a"; aim = "#ff7a2a"; shot = @{ o = "#2c2722"; h = "#b5ada0"; l = "#e0d8cc" } }
    bubble = @{ o = "#123338"; b = "#4f9a9a"; l = "#86cfc9"; d = "#2f6a70"; e = "#ffffff"; m = "#1a3a55"; aim = "#9fe8ff"; shot = @{ o = "#123338"; h = "#86cfc9"; l = "#e4ffff" } }
    cactus = @{ o = "#1c3a18"; b = "#5fa04a"; l = "#93cf6e"; d = "#3a6e30"; e = "#201a10"; m = "#7a2a3a"; aim = "#ff6fa0"; shot = @{ o = "#1c3a18"; h = "#93cf6e"; l = "#e0ffb0" } }
    fire   = @{ o = "#300a06"; b = "#b8402a"; l = "#f08a3a"; d = "#7a1e14"; e = "#ffe060"; m = "#401008"; aim = "#ffd040"; shot = @{ o = "#7a1e14"; h = "#f08a3a"; l = "#ffe060" } }
}
$shot = @("..oooo..", ".ohhhho.", "ohhllhho", "ohlllhho", "ohhlhhho", "ohhhhhho", ".ohhhho.", "..oooo..")
foreach ($v in $spitters.Keys) {
    $p = $spitters[$v]
    $base = Draw-Ascii $spitter 16 16 $p
    Save-Bmp (Scale-Y $base 0.93) "spitter_${v}_1"
    Save-Bmp $base "spitter_${v}_0"
    $aimPalette = $p.Clone(); $aimPalette["m"] = $p["aim"]
    $aim = Draw-Ascii $spitter 16 16 $aimPalette
    Save-Bmp (Scale-Y $aim 1.12) "spitter_${v}_2"
    $aim.Dispose()
    Save-Bmp (Draw-Ascii $shot 8 8 $p["shot"]) "shot_${v}"
}

# Burrower: a mound of dirt (underground) and the creature poking out of it (up).
$mound = @(
    "................", "................", "................", "................", "................",
    "................", "................", "................", "................", "................",
    "......oooo......",
    "....oodddloo....",
    "...odddlddddo...",
    "..oddddddddddo..",
    ".oddddddddddddo.",
    ".oooooooooooooo.")
$burrowUp = @(
    "................", "................",
    ".....oooooo.....",
    "....obbbbbbo....",
    "...obbllbbbbo...",
    "...obebbbbebo...",
    "...obbbbbbbbo...",
    "...obbttttbbo...",
    "...obbbbbbbbo...",
    "...obbbbbbbbo...",
    "....obbbbbbo....",
    "....obbbbbbo....",
    "...oodbbbbdoo...",
    "..oddddddddddo..",
    ".oddddddddddddo.",
    ".oooooooooooooo.")
$burrowers = @{
    mole = @{ o = "#2a1c12"; b = "#6b4f3a"; l = "#94735a"; e = "#111111"; t = "#f0b0a0"; d = "#7a5c40" }
    worm = @{ o = "#5a3a1a"; b = "#c99a5a"; l = "#ecc88a"; e = "#330000"; t = "#fff4d8"; d = "#cba765" }
}
foreach ($v in $burrowers.Keys) {
    $m0 = Draw-Ascii $mound 16 16 $burrowers[$v]
    Save-Bmp (Mirror $m0) "burrower_${v}_mound_1"
    Save-Bmp $m0 "burrower_${v}_mound_0"
    $u0 = Draw-Ascii $burrowUp 16 16 $burrowers[$v]
    Save-Bmp (Scale-Y $u0 0.92) "burrower_${v}_up_1"
    Save-Bmp $u0 "burrower_${v}_up_0"
}

# Golem: 24 x 24. Frames: 0 and 1 walking, 2 wind-up (eyes and core flare), 3 slam (squashed down).
$golem = @(
    "........................",
    "........oooooooo........",
    ".......obbbbbbbbo.......",
    "......obbllbbbbbbo......",
    "......obeebbbbeebo......",
    "......obeebbbbeebo......",
    "......obbbbbbbbbbo......",
    "...oooobbbbbbbbbboooo...",
    "..obbbobbbbbbbbbbobbbo..",
    ".obbbbobbbbccbbbbobbbbo.",
    ".oblbbobbbccccbbbobblbo.",
    ".obbbbobbbccccbbbobbbbo.",
    ".obbbbobbbbccbbbbobbbbo.",
    ".obbbbobbbbbbbbbbobbbbo.",
    ".oddbbobbbbbbbbbbobbddo.",
    ".oddddobbbbbbbbbbobdddo.",
    "..odddoddbbbbbbddodddo..",
    "...ooo.oddddddddo.ooo...",
    ".......obbbooobbbo......",
    "......obbbbo.obbbbo.....",
    "......obbbbo.obbbbo.....",
    "......oddddo.oddddo.....",
    "......oddddo.oddddo.....",
    ".......oooo...oooo......")
$golems = @{
    stone = @{ o = "#262c30"; b = "#7f8a8c"; l = "#b3bdbf"; d = "#55606a"; e = "#6fe0ff"; c = "#6fe0ff"; flare = "#ffffff" }
    magma = @{ o = "#120808"; b = "#4a3434"; l = "#7a5050"; d = "#2e1c1c"; e = "#ffb030"; c = "#ff6a1c"; flare = "#fff0a0" }
}
foreach ($v in $golems.Keys) {
    $p = $golems[$v]
    $base = Draw-Ascii $golem 24 24 $p
    Save-Bmp (Scale-Y $base 0.95) "golem_${v}_1"
    Save-Bmp (Scale-Y $base 0.8) "golem_${v}_3"
    Save-Bmp $base "golem_${v}_0"
    $flare = $p.Clone(); $flare["e"] = $p["flare"]; $flare["c"] = $p["flare"]
    Save-Bmp (Draw-Ascii $golem 24 24 $flare) "golem_${v}_2"
}

# Recoloured copies of the mountain bat and bug sheets (same layout as the originals, so they are cut up the same way).
# Every pixel takes the new colour at its old brightness; the yellow eyes are left alone.
function Recolour-Sheet([string]$source, [string]$name, [string]$tint) {
    $path = Join-Path $mountain $source
    if (-not (Test-Path $path)) { Write-Host "  (skipped $name : $source not found)"; return }
    $src = New-Object System.Drawing.Bitmap($path)
    $bmp = New-Bmp $src.Width $src.Height
    $c = Hex $tint
    for ($y = 0; $y -lt $src.Height; $y++) { for ($x = 0; $x -lt $src.Width; $x++) {
        $p = $src.GetPixel($x, $y)
        if ($p.A -eq 0) { continue }
        if ($p.R -gt 180 -and $p.G -gt 150 -and $p.B -lt 110) { $bmp.SetPixel($x, $y, $p); continue }
        $light = [Math]::Min(1.0, (0.299 * $p.R + 0.587 * $p.G + 0.114 * $p.B) / 255.0 * 1.6 + 0.2)
        $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($p.A, [int]($c.R * $light), [int]($c.G * $light), [int]($c.B * $light)))
    } }
    $src.Dispose()
    Save-Bmp $bmp $name
}
Recolour-Sheet "bat_sheet.png" "cave_bat_sheet" "#6fc4d4"
Recolour-Sheet "bat_sheet.png" "fire_bat_sheet" "#ff7a34"
Recolour-Sheet "bug_sheet.png" "scarab_sheet" "#e6b84a"

Write-Host ("Mine art written to " + $out + " (" + (Get-ChildItem $out -Filter *.png).Count + " pictures).")
