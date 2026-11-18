# Draws every picture the City Block scene uses into Assets\Project\Art\City (one PNG per picture, 16 pixels per tile).
#   powershell -File Tools\make_city_art.ps1 -Project "<project folder>"
# Change a colour or a shape here and run it again; then run Farm > Set Up > City Scene in Unity.
# People are drawn from a small "paper doll" description (hair, skin, clothes), so new city folk are one line each.
# NOTE: PowerShell hashtable keys ignore upper / lower case, so never use two keys that differ only by case.
param([string]$Project = (Split-Path $PSScriptRoot -Parent))
Add-Type -AssemblyName System.Drawing
# PowerShell already has shortcuts called "r" and "clear"; they would win over the drawing functions below, so take them away for this run.
foreach ($a in "r", "clear") { if (Test-Path ("alias:" + $a)) { Remove-Item ("alias:" + $a) -Force } }
$out =Join-Path $Project "Assets\Project\Art\City"
New-Item -ItemType Directory -Force $out | Out-Null
$INK = "2a1e2a"     # the dark outline colour used by the other art in the project

# ---------------------------------------------------------------- small drawing helpers (x, y from the TOP-LEFT of the picture)
function Col([string]$hex, [int]$a = 255) {
  [System.Drawing.Color]::FromArgb($a, [Convert]::ToInt32($hex.Substring(0,2),16), [Convert]::ToInt32($hex.Substring(2,2),16), [Convert]::ToInt32($hex.Substring(4,2),16))
}
function Shade([string]$hex, [double]$k) {     # k below 1 = darker, above 1 = lighter
  $r = [math]::Min(255, [math]::Round([Convert]::ToInt32($hex.Substring(0,2),16) * $k))
  $g = [math]::Min(255, [math]::Round([Convert]::ToInt32($hex.Substring(2,2),16) * $k))
  $b = [math]::Min(255, [math]::Round([Convert]::ToInt32($hex.Substring(4,2),16) * $k))
  "{0:x2}{1:x2}{2:x2}" -f [int]$r, [int]$g, [int]$b
}
function NewBmp([int]$w, [int]$h) { New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb) }
function R($b, [int]$x, [int]$y, [int]$w, [int]$h, [string]$hex, [int]$a = 255) {   # a filled rectangle
  if ($w -le 0 -or $h -le 0) { return }
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.CompositingMode = if ($a -lt 255) { 'SourceOver' } else { 'SourceCopy' }
  $br = New-Object System.Drawing.SolidBrush (Col $hex $a)
  $g.FillRectangle($br, $x, $y, $w, $h); $br.Dispose(); $g.Dispose()
}
function P($b, [int]$x, [int]$y, [string]$hex) { if ($x -ge 0 -and $y -ge 0 -and $x -lt $b.Width -and $y -lt $b.Height) { $b.SetPixel($x, $y, (Col $hex)) } }
function Clear($b, [int]$x, [int]$y, [int]$w, [int]$h) {
  for ($j = $y; $j -lt $y + $h; $j++) { for ($i = $x; $i -lt $x + $w; $i++) { if ($i -ge 0 -and $j -ge 0 -and $i -lt $b.Width -and $j -lt $b.Height) { $b.SetPixel($i, $j, [System.Drawing.Color]::FromArgb(0,0,0,0)) } } }
}
function Box($b, [int]$x, [int]$y, [int]$w, [int]$h, [string]$fill, [string]$edge) { R $b $x $y $w $h $edge; R $b ($x+1) ($y+1) ($w-2) ($h-2) $fill }
function Disc($b, [int]$cx, [int]$cy, [int]$rad, [string]$hex) {
  for ($j = -$rad; $j -le $rad; $j++) { for ($i = -$rad; $i -le $rad; $i++) { if ($i*$i + $j*$j -le $rad*$rad + $rad*0.6) { P $b ($cx+$i) ($cy+$j) $hex } } }
}
# Gives every drawn shape a 1 pixel dark edge (inside the rectangle x0,y0,w,h only, so sheet cells do not bleed into each other).
function Outline($b, [int]$x0 = 0, [int]$y0 = 0, [int]$w = -1, [int]$h = -1) {
  if ($w -lt 0) { $w = $b.Width; $h = $b.Height }
  $ink = Col $INK; $todo = New-Object System.Collections.ArrayList
  for ($y = $y0; $y -lt $y0 + $h; $y++) { for ($x = $x0; $x -lt $x0 + $w; $x++) {
    if ($b.GetPixel($x, $y).A -ne 0) { continue }
    $near = $false
    foreach ($d in @(@(1,0), @(-1,0), @(0,1), @(0,-1))) {
      $nx = $x + $d[0]; $ny = $y + $d[1]
      if ($nx -ge $x0 -and $ny -ge $y0 -and $nx -lt $x0 + $w -and $ny -lt $y0 + $h -and $b.GetPixel($nx, $ny).A -ne 0) { $near = $true; break }
    }
    if ($near) { [void]$todo.Add(@($x, $y)) }
  } }
  foreach ($p in $todo) { $b.SetPixel($p[0], $p[1], $ink) }
}
function Save($b, [string]$name) { $b.Save((Join-Path $out ($name + ".png")), [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose() }

# A tiny 3 x 5 letter set for signs. Each letter is 5 rows of 3 marks (# = ink).
$FONT = @{
  A="###","#.#","###","#.#","#.#"; B="##.","#.#","##.","#.#","##."; C="###","#..","#..","#..","###"; D="##.","#.#","#.#","#.#","##.";
  E="###","#..","##.","#..","###"; F="###","#..","##.","#..","#.."; G="###","#..","#.#","#.#","###"; H="#.#","#.#","###","#.#","#.#";
  I="###",".#.",".#.",".#.","###"; J="..#","..#","..#","#.#","###"; K="#.#","#.#","##.","#.#","#.#"; L="#..","#..","#..","#..","###";
  M="#.#","###","###","#.#","#.#"; N="##.","#.#","#.#","#.#","#.#"; O="###","#.#","#.#","#.#","###"; P="###","#.#","###","#..","#..";
  Q="###","#.#","#.#","###","..#"; R="###","#.#","##.","#.#","#.#"; S="###","#..","###","..#","###"; T="###",".#.",".#.",".#.",".#.";
  U="#.#","#.#","#.#","#.#","###"; V="#.#","#.#","#.#","#.#",".#."; W="#.#","#.#","###","###","#.#"; X="#.#","#.#",".#.","#.#","#.#";
  Y="#.#","#.#",".#.",".#.",".#."; Z="###","..#",".#.","#..","###"
}
function Text($b, [int]$x, [int]$y, [string]$s, [string]$hex, [int]$k = 1) {
  foreach ($ch in $s.ToUpper().ToCharArray()) {
    $key = [string]$ch
    if ($FONT.ContainsKey($key)) {
      $rows = $FONT[$key]
      for ($r = 0; $r -lt 5; $r++) { for ($c = 0; $c -lt 3; $c++) { if ($rows[$r][$c] -eq '#') { R $b ($x + $c*$k) ($y + $r*$k) $k $k $hex } } }
    }
    $x += 4 * $k
  }
}
function TextW([string]$s, [int]$k = 1) { return ($s.Length * 4 - 1) * $k }
function TextC($b, [int]$cx, [int]$y, [string]$s, [string]$hex, [int]$k = 1) { Text $b ($cx - [int]((TextW $s $k) / 2)) $y $s $hex $k }

# A window: frame, glass with a streak of light, and a sill.
function Win($b, [int]$x, [int]$y, [int]$w, [int]$h, [string]$frame, [string]$glass) {
  R $b $x $y $w $h $frame; R $b ($x+1) ($y+1) ($w-2) ($h-2) $glass
  R $b ($x+2) ($y+2) 2 ([math]::Max(1, $h-6)) (Shade $glass 1.18)
  R $b ($x + [int]($w/2)) ($y+1) 1 ($h-2) $frame
  R $b ($x-1) ($y+$h) ($w+2) 2 (Shade $frame 0.8)
}
function Lines($b, [int]$x, [int]$y, [int]$w, [int]$h, [string]$hex, [int]$step) { for ($j = $y; $j -lt $y + $h; $j += $step) { R $b $x $j $w 1 $hex } }

# ================================================================ BUILDINGS
# An ordinary city building: shop front at the bottom, rows of windows, a sign and a roof edge. Used for all the background buildings.
function Block([string]$name, [int]$wT, [int]$hT, [string]$wall, [string]$trim, [string]$sign, [string]$signBg, [string]$awning, [string]$glass = "a8d8ea", [bool]$balconies = $false) {
  $W = $wT * 16; $H = $hT * 16; $b = NewBmp $W $H
  R $b 0 0 $W $H $wall
  Lines $b 0 14 $W ($H - 62) (Shade $wall 0.93) 4
  R $b 0 0 $W 10 (Shade $trim 0.8); R $b 0 10 $W 3 $trim; Lines $b 4 2 ($W - 8) 6 (Shade $trim 0.65) 3      # roof edge
  R $b 0 0 2 $H (Shade $wall 0.75); R $b ($W-2) 0 2 $H (Shade $wall 0.75)
  # upper floors
  $cols = [int][math]::Floor(($W - 16) / 28); $gap = ($W - $cols * 20) / ($cols + 1)
  for ($fy = 20; $fy -lt $H - 96; $fy += 34) {
    for ($c = 0; $c -lt $cols; $c++) {
      $wx = [int]($gap + $c * (20 + $gap))
      Win $b $wx $fy 20 22 "f4f0e6" $glass
      if ($balconies) { R $b ($wx-2) ($fy+22) 24 2 (Shade $trim 0.7); Lines $b ($wx-2) ($fy+16) 24 6 (Shade $trim 0.7) 2; for ($k = 0; $k -lt 24; $k += 4) { R $b ($wx-2+$k) ($fy+16) 1 7 (Shade $trim 0.7) } }
    }
  }
  # sign band and awning
  $sy = $H - 70
  Box $b 6 $sy ($W - 12) 16 $signBg (Shade $signBg 0.6)
  $k = if ((TextW $sign 2) -le $W - 20) { 2 } else { 1 }
  TextC $b ([int]($W/2)) ($sy + [int]((16 - 5*$k)/2)) $sign "fff8e8" $k
  if ($awning) { R $b 0 ($H-50) $W 8 $awning; for ($i = 0; $i -lt $W; $i += 16) { R $b $i ($H-50) 8 8 (Shade $awning 1.25) }; R $b 0 ($H-42) $W 2 (Shade $awning 0.7) }
  # shop front
  R $b 0 ($H-40) $W 40 (Shade $wall 1.12); R $b 0 ($H-6) $W 6 (Shade $wall 0.7)
  $dw = 28; $dx = [int]($W/2 - $dw/2)
  Win $b 8 ($H-36) ($dx - 16) 26 "f4f0e6" $glass; Win $b ($dx + $dw + 8) ($H-36) ($W - $dx - $dw - 16) 26 "f4f0e6" $glass
  Box $b $dx ($H-38) $dw 38 $glass "f4f0e6"; R $b ([int]($W/2)) ($H-37) 1 37 "f4f0e6"; R $b ($dx+3) ($H-34) 2 20 (Shade $glass 1.18)
  R $b ([int]($W/2)-4) ($H-20) 2 5 "c9a03a"; R $b ([int]($W/2)+3) ($H-20) 2 5 "c9a03a"
  Save $b $name
}
Block "bg_cafe"        8 10 "3f8f88" "2d5f5c" "CAFE"       "2d4a52" "e8734a"
Block "bg_books"       8 10 "8a6a9a" "5a4468" "BOOKS"      "4a3558" "e0b84a"
Block "bg_market"      8 10 "c98a4a" "8a5a2e" "MARKET"     "6a3e22" "5a9a5a"
Block "bg_clinic"      8 10 "eef0ee" "9ab0b8" "CLINIC"     "c0443a" $null
Block "bg_museum"     10 11 "d8c8a0" "a89868" "MUSEUM"     "5a4a3a" $null "88b8c8"
Block "bg_apartments_a" 10 11 "b8785a" "7a4a38" "FLATS"    "5a3a30" $null "a8d8ea" $true
Block "bg_apartments_b" 10 11 "7a8aa0" "4a5870" "LOFTS"    "3a4458" $null "a8d8ea" $true
Block "bg_office_a"   10 12 "5a7088" "34465a" "OFFICES"    "24303e" $null "b8e0f0"
Block "bg_office_b"    8 11 "8a7460" "5a483a" "BANK"       "3a2e26" "3a6a4a"

# ---- the university: red brick, white portico with four columns, a clock in the gable
$b = NewBmp 224 192
R $b 0 28 224 164 "a85a48"; Lines $b 0 30 224 150 "96503f" 4
R $b 0 20 224 10 "5a5a6a"; R $b 0 28 224 3 "e8e2d4"                                  # slate roof edge
R $b 0 176 224 16 "d8d2c4"; R $b 0 188 224 4 "a8a294"                                # stone base
foreach ($wx in 10, 36, 164, 190) { Win $b $wx 44 20 28 "f4f0e6" "a8d8ea"; Win $b $wx 96 20 28 "f4f0e6" "a8d8ea"; Win $b $wx 142 20 28 "f4f0e6" "a8d8ea" }
R $b 62 60 100 132 "e8e2d4"                                                          # portico
for ($i = 0; $i -lt 26; $i++) { R $b (62 + $i*2) (58 - $i) (100 - $i*4) 1 "e8e2d4" }   # gable
R $b 60 58 104 3 "c8c2b4"; Disc $b 112 46 7 "fff8e8"; R $b 112 41 1 6 $INK; R $b 112 46 4 1 $INK
foreach ($cx in 66, 88, 126, 148) { R $b $cx 62 10 118 "f8f4ea"; R $b ($cx+7) 62 3 118 "c8c2b4"; R $b ($cx-2) 62 14 4 "d8d2c4"; R $b ($cx-2) 176 14 4 "d8d2c4" }
Box $b 70 70 84 14 "3a4a6a" "24303e"; TextC $b 112 74 "UNIVERSITY" "fff8e8" 1
Box $b 98 132 28 60 "6a4a32" "3a2a1e"; R $b 111 133 2 58 "3a2a1e"; R $b 101 138 8 16 "a8d8ea"; R $b 115 138 8 16 "a8d8ea"; R $b 107 164 2 4 "c9a03a"; R $b 115 164 2 4 "c9a03a"
R $b 86 180 52 4 "c8c2b4"; R $b 80 184 64 4 "b8b2a4"; R $b 74 188 76 4 "a8a294"   # steps
Save $b "city_university"

# ---- the corporate tower: dark glass, gold trim, the name in lights
$b = NewBmp 192 288
R $b 8 24 176 264 "24344f"
for ($y = 62; $y -lt 232; $y += 12) { for ($x = 16; $x -lt 176; $x += 14) { $k = 1.0 + ((($x * 7 + $y * 3) % 5) - 2) * 0.07; R $b $x $y 11 9 (Shade "6aa6d0" $k); R $b $x $y 3 9 (Shade "9ccdea" $k) } }
R $b 8 24 6 264 "c9a03a"; R $b 178 24 6 264 "c9a03a"; R $b 10 24 2 264 "f0d070"; R $b 180 24 2 264 "f0d070"
R $b 20 8 152 20 "1a2438"; R $b 20 6 152 3 "c9a03a"; R $b 92 0 3 8 "9aa0a8"; R $b 91 0 5 2 "e04a3a"     # crown and mast
Box $b 28 30 136 26 "101828" "c9a03a"; TextC $b 96 36 "STERLING" "f0d070" 3
R $b 8 232 176 8 "c9a03a"; R $b 8 238 176 2 "8a6a1e"
R $b 8 240 176 48 "d0d4da"; Lines $b 8 244 176 40 "bcc0c8" 8; R $b 8 282 176 6 "8a8e96"                  # marble lobby front
Win $b 18 246 46 34 "c9a03a" "9ccdea"; Win $b 128 246 46 34 "c9a03a" "9ccdea"
Box $b 76 242 40 46 "9ccdea" "c9a03a"; R $b 95 243 2 45 "c9a03a"; R $b 80 246 3 30 "cfeaf8"; R $b 100 246 3 30 "cfeaf8"
R $b 70 236 52 6 "8a1e2a"; R $b 70 241 52 1 "5a1018"                                                     # red canopy over the doors
Save $b "city_tower"

# ---- city hall: pale stone, six columns, a little dome and a flag
$b = NewBmp 224 176
R $b 0 44 224 132 "e6dcc0"; Lines $b 0 48 224 110 "d6ccae" 6
R $b 0 38 224 8 "b8ae90"; R $b 0 44 224 2 "f4eeda"
Disc $b 112 30 18 "7aa89a"; R $b 90 30 44 10 "7aa89a"; R $b 86 36 52 4 "b8ae90"; R $b 111 2 2 12 "8a8e96"; R $b 113 3 10 6 "c0443a"; R $b 113 5 10 2 "fff8e8"
Clear $b 0 48 0 0
foreach ($wx in 8, 34, 170, 196) { Win $b $wx 64 20 34 "f8f4ea" "a8d8ea"; Win $b $wx 118 20 30 "f8f4ea" "a8d8ea" }
for ($i = 0; $i -lt 20; $i++) { R $b (58 + $i*2) (62 - $i) (108 - $i*4) 1 "f4eeda" }
R $b 56 62 112 4 "d6ccae"; Box $b 66 68 92 14 "5a4a3a" "3a2e22"; TextC $b 112 72 "CITY HALL" "f4e8c0" 1
foreach ($cx in 60, 78, 96, 120, 138, 156) { R $b $cx 84 8 80 "f8f4ea"; R $b ($cx+6) 84 2 80 "d6ccae"; R $b ($cx-1) 84 10 3 "d6ccae"; R $b ($cx-1) 161 10 3 "d6ccae" }
Box $b 100 122 24 42 "5a3e2a" "3a2a1e"; R $b 111 123 2 40 "3a2a1e"; R $b 108 144 2 4 "c9a03a"; R $b 114 144 2 4 "c9a03a"
R $b 52 164 120 4 "d6ccae"; R $b 46 168 132 4 "c6bc9e"; R $b 40 172 144 4 "b6ac8e"
Save $b "city_hall"

# ---- the waste treatment plant: corrugated metal, tanks, pipes and a striped stack
$b = NewBmp 224 160
R $b 0 40 150 120 "7d8f86"; for ($x = 0; $x -lt 150; $x += 4) { R $b $x 40 1 114 "6c7d75" }
R $b 0 34 150 8 "566660"; R $b 0 154 224 6 "4a524e"
R $b 20 0 14 40 "d8d8d0"; for ($y = 2; $y -lt 34; $y += 12) { R $b 20 $y 14 6 "c0443a" }; R $b 18 0 18 3 "8a8e96"       # the stack
Box $b 10 50 130 18 "e0b84a" "8a6a1e"; TextC $b 75 54 "WASTE TREATMENT" $INK 2
Box $b 10 92 48 62 "a0aaa4" "566660"; Lines $b 12 96 44 56 "8c9690" 6; for ($i = 0; $i -lt 48; $i += 12) { R $b (10+$i) 86 6 6 "e0b84a"; R $b (16+$i) 86 6 6 $INK }   # roll door
Box $b 98 112 28 42 "4a6a7a" "2a3a44"; R $b 102 116 20 12 "a8d8ea"; R $b 120 134 2 5 "e0b84a"                          # office door (the way in)
Win $b 68 100 22 20 "d8d8d0" "a8d8ea"
foreach ($tx in 158, 192) { R $b $tx 62 28 92 "9ab0b8"; R $b ($tx+3) 62 5 92 "c4d6dc"; R $b ($tx+22) 62 6 92 "7a9098"; R $b ($tx-1) 58 30 6 "7a9098"; R $b ($tx-1) 100 30 3 "7a9098"; R $b ($tx+10) 50 8 8 "7a9098" }
R $b 150 76 10 5 "5a8a5a"; R $b 186 86 8 5 "5a8a5a"; R $b 140 128 20 5 "5a8a5a"; R $b 140 128 5 26 "5a8a5a"
Save $b "city_plant"

# ================================================================ TILES (16 x 16)
function Tile([string]$name, [scriptblock]$draw) { $t = NewBmp 16 16; & $draw $t; Save $t $name }
Tile "floor_uni"    { param($t) R $t 0 0 16 16 "c9a878"; R $t 0 0 16 1 "b89868"; R $t 0 8 16 1 "b89868"; R $t 5 0 1 8 "b89868"; R $t 12 8 1 8 "b89868" }
Tile "floor_tower"  { param($t) R $t 0 0 16 16 "e4e6ea"; R $t 0 0 16 1 "c4c8d0"; R $t 0 0 1 16 "c4c8d0"; P $t 5 6 "d0d4dc"; P $t 6 7 "d0d4dc"; P $t 11 11 "d0d4dc"; P $t 10 3 "d0d4dc" }
Tile "floor_tower_gold" { param($t) R $t 0 0 16 16 "8a1e2a"; R $t 0 0 16 2 "c9a03a"; R $t 0 14 16 2 "c9a03a"; R $t 0 5 16 1 "7a1622"; R $t 0 10 16 1 "7a1622" }
Tile "floor_tower_back" { param($t) R $t 0 0 16 16 "6a6e72"; R $t 0 0 16 1 "5a5e62"; R $t 0 0 1 16 "5a5e62"; R $t 4 9 5 3 "5e6a5a"; P $t 11 4 "50545a"; P $t 12 5 "50545a" }
Tile "floor_hall"   { param($t) R $t 0 0 16 16 "a8743e"; R $t 0 0 8 8 "b8844a"; R $t 8 8 8 8 "b8844a"; R $t 0 0 16 1 "8a5c2e"; R $t 0 8 16 1 "8a5c2e"; R $t 0 0 1 16 "8a5c2e"; R $t 8 0 1 16 "8a5c2e" }
Tile "floor_plant"  { param($t) R $t 0 0 16 16 "7c8a8a"; for ($i = 0; $i -lt 16; $i += 4) { R $t $i 0 1 16 "6a7878"; R $t 0 $i 16 1 "6a7878" }; P $t 2 2 "98a6a6"; P $t 10 10 "98a6a6" }
Tile "wall_uni_upper"   { param($t) R $t 0 0 16 16 "e8dcc0"; R $t 0 15 16 1 "d0c4a8" }
Tile "wall_uni_base"    { param($t) R $t 0 0 16 16 "e8dcc0"; R $t 0 8 16 8 "7a5236"; R $t 0 8 16 1 "9a6e4a"; R $t 0 15 16 1 "4a3020" }
Tile "wall_tower_upper" { param($t) R $t 0 0 16 16 "2e3e5c"; R $t 0 0 1 16 "3a4c6e"; R $t 8 0 1 16 "283650" }
Tile "wall_tower_base"  { param($t) R $t 0 0 16 16 "2e3e5c"; R $t 0 0 1 16 "3a4c6e"; R $t 0 10 16 6 "c9a03a"; R $t 0 10 16 1 "f0d070"; R $t 0 15 16 1 "8a6a1e" }
Tile "wall_hall_upper"  { param($t) R $t 0 0 16 16 "d8e0d0"; R $t 0 15 16 1 "c0c8b8" }
Tile "wall_hall_base"   { param($t) R $t 0 0 16 16 "d8e0d0"; R $t 0 6 16 10 "5a6e5a"; R $t 0 6 16 1 "7a8e7a"; R $t 0 15 16 1 "3a4a3a" }
Tile "wall_plant_upper" { param($t) R $t 0 0 16 16 "98a49e"; for ($i = 0; $i -lt 16; $i += 4) { R $t $i 0 1 16 "86928c" } }
Tile "wall_plant_base"  { param($t) R $t 0 0 16 16 "98a49e"; R $t 0 9 16 7 "e0b84a"; for ($i = -8; $i -lt 16; $i += 8) { for ($k = 0; $k -lt 7; $k++) { R $t ($i+$k) (9+$k) 4 1 $INK } }; R $t 0 15 16 1 "4a524e" }
Tile "partition_city"   { param($t) R $t 0 0 16 16 "4a4e58"; R $t 0 0 16 3 "6a6e7a"; R $t 0 13 16 3 "33363e" }
Tile "tile_water"   { param($t) R $t 0 0 16 16 "3f86b8"; R $t 2 4 5 1 "6aa8d0"; R $t 9 10 5 1 "6aa8d0"; R $t 11 2 3 1 "357aa8" }
Tile "tile_water_b" { param($t) R $t 0 0 16 16 "3f86b8"; R $t 8 5 5 1 "6aa8d0"; R $t 1 12 5 1 "6aa8d0"; R $t 3 8 3 1 "357aa8" }
Tile "tile_bank"    { param($t) R $t 0 0 16 16 "3f86b8"; R $t 3 3 5 1 "6aa8d0"; R $t 0 9 16 7 "a8a294"; R $t 0 9 16 1 "c8c2b4"; R $t 5 10 1 6 "8a8478"; R $t 13 10 1 6 "8a8478"; R $t 0 8 16 1 "2e6a94" }
Tile "tile_sludge"  { param($t) R $t 0 0 16 16 "4a7a8a"; R $t 1 3 7 3 "6a5a7a"; R $t 8 9 7 4 "5a6e3e"; R $t 3 11 3 2 "7a6a8a"; P $t 11 4 "8ab04a"; P $t 5 8 "8ab04a" }
Tile "tile_sludge_b" { param($t) R $t 0 0 16 16 "447488"; R $t 6 2 8 3 "5a6e3e"; R $t 1 8 8 4 "6a5a7a"; P $t 12 11 "8ab04a"; P $t 3 5 "8ab04a" }

# ================================================================ PROPS (the pivot is at the bottom middle unless the builder says otherwise)
function Prop([string]$name, [int]$w, [int]$h, [scriptblock]$draw, [bool]$edge = $true) { $p = NewBmp $w $h; & $draw $p; if ($edge) { Outline $p }; Save $p $name }

# ---- university
Prop "uni_lab_bench" 48 26 { param($p) R $p 1 10 46 6 "d8dce0"; R $p 1 16 46 9 "6a7a8a"; R $p 3 18 12 6 "56646f"; R $p 18 18 12 6 "56646f"; R $p 33 18 12 6 "56646f"
  R $p 5 3 4 7 "a8e0c0"; R $p 6 1 2 2 "d8dce0"; R $p 13 5 5 5 "e88a9a"; R $p 14 3 3 2 "d8dce0"; R $p 24 2 3 8 "8a8e96"; R $p 22 8 8 2 "5a5e66"; R $p 34 4 8 6 "88c8e8"; R $p 36 2 4 2 "d8dce0" }
Prop "uni_bookshelf" 32 42 { param($p) R $p 1 1 30 40 "7a5236"; foreach ($y in 4, 14, 24, 33) { R $p 3 $y 26 8 "4a3020"; $cols = "c0443a","3f86b8","e0b84a","5a9a5a","8a6a9a","e8e2d4"; for ($i = 0; $i -lt 8; $i++) { R $p (4 + $i*3) ($y + 1 + ($i % 2)) 2 (7 - ($i % 2)) $cols[($i + $y) % 6] } } }
Prop "uni_chalkboard" 64 28 { param($p) R $p 0 0 64 28 "7a5236"; R $p 2 2 60 22 "2e4a3e"; Text $p 5 5 "H2O" "d8e8d8" 1; Text $p 22 5 "NPK" "d8e8d8" 1; R $p 40 6 16 1 "d8e8d8"; R $p 40 10 12 1 "d8e8d8"; R $p 6 14 30 1 "d8e8d8"; R $p 6 18 20 1 "d8e8d8"; R $p 44 13 10 9 "d8e8d8"; R $p 45 14 8 7 "2e4a3e"; R $p 4 25 10 2 "f4f0e6" } $false
Prop "uni_lectern" 16 24 { param($p) R $p 2 2 12 5 "9a6e4a"; R $p 3 7 10 16 "7a5236"; R $p 5 10 6 8 "6a4630"; R $p 4 3 8 2 "f4f0e6" }
Prop "uni_student_desk" 24 18 { param($p) R $p 1 5 22 4 "c9a878"; R $p 2 9 2 8 "8a8e96"; R $p 20 9 2 8 "8a8e96"; R $p 5 2 7 3 "f4f0e6"; R $p 15 3 4 2 "3f86b8" }
Prop "uni_research_desk" 48 34 { param($p) R $p 1 16 46 5 "e8e2d4"; R $p 1 21 46 12 "5a6a8a"; R $p 4 23 18 8 "4a587a"; R $p 26 23 18 8 "4a587a"
  R $p 6 2 16 12 "2a3440"; R $p 7 3 14 9 "5ad0a0"; R $p 9 5 6 1 "d8ffe8"; R $p 9 8 9 1 "d8ffe8"; R $p 12 14 4 2 "2a3440"
  R $p 28 8 4 8 "88c8e8"; R $p 29 6 2 2 "d8dce0"; R $p 35 10 5 6 "5a9a5a"; R $p 36 5 1 5 "5a9a5a"; R $p 38 6 1 4 "5a9a5a"; R $p 34 3 3 3 "8ad06a"; R $p 38 4 3 3 "8ad06a" }
Prop "uni_plant_trial" 32 24 { param($p) R $p 1 14 30 9 "7a5236"; R $p 2 12 28 3 "4a3020"; foreach ($i in 0..5) { $x = 4 + $i * 4 + ($i % 2); R $p $x (11 - $i % 3 - 2) 1 (3 + $i % 3) "5a9a5a"; R $p ($x - 1) (8 - $i % 3) 3 2 "8ad06a" }; R $p 4 17 6 3 "f4f0e6"; R $p 14 17 6 3 "f4f0e6"; R $p 24 17 5 3 "f4f0e6" }
Prop "city_potted_palm" 16 30 { param($p) R $p 4 21 8 8 "b8785a"; R $p 3 20 10 2 "d8987a"; R $p 7 8 2 13 "6a4a32"; R $p 2 4 12 3 "5a9a5a"; R $p 1 7 5 3 "4a8a4a"; R $p 10 7 5 3 "4a8a4a"; R $p 5 1 6 4 "6ab06a"; R $p 3 10 3 3 "5a9a5a"; R $p 10 10 3 3 "5a9a5a" }

# ---- the tower
Prop "tower_reception" 64 28 { param($p) R $p 1 8 62 6 "f0d070"; R $p 1 14 62 13 "1a2438"; R $p 4 16 56 9 "24344f"; TextC $p 32 18 "STERLING" "f0d070" 1; R $p 6 2 10 6 "2a3440"; R $p 7 3 8 4 "88c8e8"; R $p 46 3 4 5 "5a9a5a"; R $p 45 1 6 3 "8ad06a" }
Prop "tower_seed_kiosk" 48 42 { param($p) R $p 1 1 46 10 "8a1e2a"; TextC $p 24 4 "SEEDS" "f0d070" 1; R $p 1 11 46 30 "e4e6ea"; foreach ($y in 13, 22, 31) { R $p 3 $y 42 8 "c4c8d0"; $cols = "c0443a","e0b84a","5a9a5a","e88a4a","8a6a9a","f4f0e6","3f86b8"; for ($i = 0; $i -lt 7; $i++) { R $p (4 + $i*6) ($y + 1) 5 6 $cols[($i + $y) % 7]; R $p (5 + $i*6) ($y + 2) 3 2 "fff8e8" } } }
Prop "tower_upgrade_kiosk" 48 42 { param($p) R $p 1 1 46 10 "8a1e2a"; TextC $p 24 4 "HOME" "f0d070" 1; R $p 1 11 46 30 "e4e6ea"
  R $p 4 14 12 12 "88c8e8"; R $p 9 14 2 12 "f4f0e6"; R $p 4 19 12 2 "f4f0e6"; R $p 19 14 10 24 "7a5236"; R $p 26 26 2 3 "c9a03a"; R $p 32 14 12 6 "c0443a"; R $p 32 21 12 6 "3f86b8"; R $p 32 28 12 6 "e0b84a"; R $p 4 29 12 9 "9a6e4a"; R $p 4 33 12 1 "6a4630" }
Prop "tower_smelter_0" 48 48 { param($p) R $p 4 1 8 14 "5a5e66"; R $p 3 1 10 3 "8a8e96"; R $p 1 14 46 33 "5a5e66"; R $p 1 14 46 3 "8a8e96"; R $p 3 20 3 24 "44484e"; R $p 42 20 3 24 "44484e"
  R $p 14 24 20 16 $INK; R $p 16 26 16 12 "e86a2a"; R $p 19 29 10 8 "f8c040"; R $p 22 32 4 4 "fff0a0"; R $p 36 22 6 4 "c0443a"; R $p 36 30 6 4 "5ad06a"; R $p 8 40 32 4 "44484e" }
Prop "tower_smelter_1" 48 48 { param($p) R $p 4 1 8 14 "5a5e66"; R $p 3 1 10 3 "8a8e96"; R $p 1 14 46 33 "5a5e66"; R $p 1 14 46 3 "8a8e96"; R $p 3 20 3 24 "44484e"; R $p 42 20 3 24 "44484e"
  R $p 14 24 20 16 $INK; R $p 16 26 16 12 "f08a3a"; R $p 18 28 12 9 "ffd860"; R $p 21 30 6 6 "fff8d0"; R $p 36 22 6 4 "e86a5a"; R $p 36 30 6 4 "5ad06a"; R $p 8 40 32 4 "44484e" }
Prop "tower_ceo_desk" 64 30 { param($p) R $p 1 10 62 5 "5a2a1a"; R $p 1 15 62 14 "3a1a10"; R $p 4 17 56 10 "4a2216"; R $p 28 19 8 6 "c9a03a"; R $p 6 4 12 6 "c9a03a"; R $p 8 2 8 2 "f0d070"; R $p 44 5 10 5 "f4f0e6"; R $p 46 3 6 2 "f4f0e6"; R $p 24 6 6 4 "2a3440" }
Prop "tower_statue" 32 50 { param($p) R $p 6 40 20 9 "d0d4da"; R $p 4 38 24 3 "e4e6ea"; R $p 12 14 8 24 "c9a03a"; R $p 10 16 2 12 "c9a03a"; R $p 20 10 2 14 "c9a03a"; R $p 12 5 8 9 "f0d070"; R $p 12 3 8 3 "c9a03a"; R $p 20 6 6 5 "f0d070"; R $p 14 16 2 18 "f0d070"; R $p 12 30 3 8 "a8862e"; R $p 17 30 3 8 "a8862e" }
Prop "tower_elevator" 32 44 { param($p) R $p 0 0 32 44 "c9a03a"; R $p 2 6 28 38 "9aa0a8"; R $p 15 6 2 38 "5a5e66"; R $p 4 8 3 34 "c4c8d0"; R $p 19 8 3 34 "c4c8d0"; R $p 12 1 8 4 "1a2438"; R $p 15 2 2 2 "5ad06a" } $false
Prop "tower_logo" 64 24 { param($p) R $p 0 0 64 24 "c9a03a"; R $p 2 2 60 20 "101828"; TextC $p 32 4 "STERLING" "f0d070" 1; TextC $p 32 13 "WE OWN TOMORROW" "9aa0a8" 1 } $false
Prop "tower_staff_sign" 32 16 { param($p) R $p 0 0 32 16 "e0b84a"; R $p 1 1 30 14 $INK; TextC $p 16 3 "STAFF" "e0b84a" 1; TextC $p 16 9 "ONLY" "e0b84a" 1 } $false
Prop "tower_barrel" 16 22 { param($p) R $p 2 2 12 19 "5a8a4a"; R $p 2 6 12 2 "3a5a2e"; R $p 2 14 12 2 "3a5a2e"; R $p 4 3 2 17 "7ab06a"; R $p 5 9 6 4 "e0b84a"; R $p 7 10 2 2 $INK; R $p 3 1 10 2 "7ab06a" }
Prop "tower_barrel_leak" 16 22 { param($p) R $p 2 2 12 19 "6a5a7a"; R $p 2 6 12 2 "4a3e58"; R $p 2 14 12 2 "4a3e58"; R $p 4 3 2 17 "8a7a9a"; R $p 5 9 6 4 "e0b84a"; R $p 7 10 2 2 $INK; R $p 3 1 10 2 "8ab04a"; R $p 11 3 2 9 "8ab04a"; R $p 12 12 2 4 "8ab04a" }
foreach ($f in 0, 1, 2) {
  Prop ("tower_chute_" + $f) 48 40 { param($p) R $p 1 1 46 38 "5a5e66"; R $p 1 1 46 4 "8a8e96"; R $p 8 8 32 24 $INK; R $p 10 10 28 20 "1a2a22"; for ($i = 0; $i -lt 48; $i += 8) { R $p $i 34 4 5 "e0b84a"; R $p ($i+4) 34 4 5 $INK }
    $yy = 10 + $f * 6; R $p 18 $yy 12 8 "5a8a4a"; R $p 18 ($yy + 2) 12 1 "3a5a2e"; R $p 20 ($yy + 3) 4 3 "e0b84a"; R $p 12 (26 + ($f % 2)) 24 3 "8ab04a"; P $p (14 + $f * 7) 24 "c8e86a"; P $p (30 - $f * 5) 23 "c8e86a" }
}
foreach ($f in 0, 1) { Prop ("tower_conveyor_" + $f) 64 14 { param($p) R $p 1 1 62 6 "44484e"; for ($i = $f * 4; $i -lt 62; $i += 8) { R $p (1 + $i) 2 3 4 "6a6e76" }; R $p 1 7 62 3 "8a8e96"; R $p 4 10 3 3 "5a5e66"; R $p 30 10 3 3 "5a5e66"; R $p 57 10 3 3 "5a5e66" } }

# ---- city hall
Prop "hall_counter" 64 28 { param($p) R $p 1 8 62 5 "c9a878"; R $p 1 13 62 14 "5a6e5a"; R $p 4 15 26 10 "4a5c4a"; R $p 34 15 26 10 "4a5c4a"; R $p 6 2 10 6 "f4f0e6"; R $p 8 3 6 1 "9aa0a8"; R $p 8 5 6 1 "9aa0a8"; R $p 44 3 5 5 "c0443a"; R $p 45 1 3 2 "7a5236"; R $p 26 4 8 4 "e0b84a" }
Prop "hall_flag" 20 46 { param($p) R $p 3 2 2 40 "c9a03a"; R $p 2 0 4 3 "f0d070"; R $p 0 42 8 3 "8a6a1e"; R $p 5 3 13 10 "3a6a9a"; R $p 5 6 13 4 "f4f0e6"; R $p 9 7 3 2 "c9a03a" }
Prop "hall_notice_board" 48 26 { param($p) R $p 0 0 48 26 "7a5236"; R $p 2 2 44 22 "c9a878"; R $p 5 4 10 8 "f4f0e6"; R $p 18 5 12 6 "f8e8a0"; R $p 33 4 9 9 "f4f0e6"; R $p 6 14 14 7 "c8e0f0"; R $p 24 14 8 7 "f4c0c0"; R $p 35 15 8 6 "f4f0e6"; P $p 10 4 "c0443a"; P $p 24 5 "c0443a"; P $p 37 4 "3f86b8" } $false
Prop "hall_cabinet" 16 32 { param($p) R $p 1 1 14 30 "8a9aa0"; foreach ($y in 3, 12, 21) { R $p 2 $y 12 8 "a4b4ba"; R $p 6 ($y + 3) 4 2 "5a6a70" } }
Prop "hall_bench" 32 16 { param($p) R $p 1 3 30 4 "9a6e4a"; R $p 1 8 30 3 "7a5236"; R $p 3 11 2 4 "4a3020"; R $p 27 11 2 4 "4a3020"; R $p 1 1 30 2 "7a5236" }
Prop "hall_model" 48 30 { param($p) R $p 1 14 46 15 "7a5236"; R $p 1 12 46 3 "5a9a5a"; R $p 5 4 6 9 "e6dcc0"; R $p 13 1 8 12 "24344f"; R $p 23 6 7 7 "a85a48"; R $p 32 8 6 5 "7d8f86"; R $p 40 5 4 8 "b8785a"; R $p 3 13 42 1 "8a8e96" }

# ---- the treatment plant
Prop "plant_hopper" 48 44 { param($p) R $p 1 1 46 6 "e0b84a"; for ($i = 0; $i -lt 46; $i += 8) { R $p (1+$i) 1 4 6 $INK }; for ($i = 0; $i -lt 14; $i++) { R $p (3 + $i) (7 + $i) (42 - $i * 2) 1 "8a9690" }; R $p 17 21 14 22 "6a7670"; R $p 20 26 8 8 "2a3a34"; R $p 4 36 40 7 "566660"; R $p 36 26 6 4 "5ad06a"; R $p 36 31 6 4 "c0443a" }
Prop "plant_tank" 32 50 { param($p) R $p 2 6 28 43 "9ab0b8"; R $p 4 6 5 43 "c4d6dc"; R $p 24 6 6 43 "7a9098"; R $p 1 3 30 5 "7a9098"; R $p 1 26 30 3 "7a9098"; R $p 12 0 8 4 "7a9098"; R $p 11 32 10 10 "f4f0e6"; R $p 13 34 6 6 "5ad0a0"; R $p 15 36 2 2 "f4f0e6" }
Prop "plant_panel" 32 34 { param($p) R $p 1 1 30 32 "566660"; R $p 3 3 26 12 "1a2a22"; R $p 5 9 3 4 "5ad06a"; R $p 10 7 3 6 "5ad06a"; R $p 15 10 3 3 "e0b84a"; R $p 20 5 3 8 "5ad06a"; R $p 4 18 5 5 "c0443a"; R $p 12 18 5 5 "5ad06a"; R $p 20 18 5 5 "e0b84a"; R $p 4 26 22 4 "8a9690" }
Prop "plant_pipes" 64 24 { param($p) R $p 0 4 64 6 "5a8a5a"; R $p 0 5 64 1 "7ab07a"; R $p 0 14 64 6 "8a9aa0"; R $p 0 15 64 1 "aabac0"; foreach ($x in 10, 34, 54) { R $p $x 2 4 20 "566660" }; R $p 22 6 5 5 "c0443a" } $false
Prop "plant_bin_trash" 16 22 { param($p) R $p 2 5 12 16 "566660"; R $p 1 3 14 3 "6a7a74"; R $p 4 8 8 6 "f4f0e6"; R $p 6 9 4 4 "566660" }
Prop "plant_bin_recycle" 16 22 { param($p) R $p 2 5 12 16 "3f86b8"; R $p 1 3 14 3 "5aa0d0"; R $p 4 8 8 6 "f4f0e6"; R $p 6 9 4 4 "3f86b8" }
Prop "plant_bin_compost" 16 22 { param($p) R $p 2 5 12 16 "5a9a5a"; R $p 1 3 14 3 "7ab07a"; R $p 4 8 8 6 "f4f0e6"; R $p 6 9 4 4 "5a9a5a" }

# ---- out on the street
Prop "hotdog_cart" 48 46 { param($p) R $p 22 14 2 14 "8a8e96"
  for ($i = 0; $i -lt 10; $i++) { R $p (4 + $i * 2) (12 - $i) (40 - $i * 4) 1 $(if ($i % 2 -eq 0) { "e04a3a" } else { "f8e8a0" }) }; R $p 4 13 40 2 "e04a3a"; for ($i = 4; $i -lt 44; $i += 8) { R $p $i 13 4 3 "f8e8a0" }
  R $p 6 28 36 12 "e04a3a"; R $p 6 28 36 2 "d8dce0"; R $p 6 34 36 2 "f8e8a0"; TextC $p 24 36 "HOT DOGS" "fff8e8" 1
  R $p 10 23 10 5 "d8dce0"; R $p 26 22 6 6 "e0b84a"; R $p 34 22 5 6 "c0443a"; Disc $p 12 42 3 "44484e"; Disc $p 36 42 3 "44484e"; P $p 12 42 "c4c8d0"; P $p 36 42 "c4c8d0"; R $p 42 30 5 2 "8a8e96" }
foreach ($f in 0, 1, 2) {
  Prop ("river_outflow_" + $f) 32 32 { param($p) R $p 6 2 20 14 "6a6e76"; R $p 6 2 20 3 "8a8e96"; R $p 9 6 14 10 $INK; R $p 4 0 24 3 "8a8e96"
    R $p 11 (10 + $f) 10 (20 - $f) "7a6a8a"; R $p 13 (12 + $f * 2) 3 8 "8ab04a"; R $p 18 (16 - $f) 2 9 "5a6e3e"; R $p (7 + $f) 28 (18 - $f) 3 "8ab04a"; P $p (9 + $f * 5) 26 "c8e86a"; P $p (22 - $f * 4) 25 "c8e86a" }
}
Prop "river_dump_sign" 16 26 { param($p) R $p 7 12 2 13 "7a5236"; R $p 1 1 14 12 "f4f0e6"; R $p 1 1 14 2 "c0443a"; R $p 4 5 8 2 "3f86b8"; R $p 6 4 4 5 "566660"; R $p 3 9 10 1 "c0443a" }
Prop "river_boom" 64 10 { param($p) for ($i = 0; $i -lt 8; $i++) { R $p (1 + $i * 8) 2 6 5 $(if ($i % 2 -eq 0) { "e8734a" } else { "f4f0e6" }) }; R $p 0 4 64 1 "8a8e96" }
Prop "river_reeds" 16 18 { param($p) foreach ($i in 0..4) { $x = 2 + $i * 3; R $p $x (5 + ($i % 2) * 2) 1 (12 - ($i % 2) * 2) "5a9a5a"; R $p $x (3 + ($i % 2) * 2) 1 3 "7a5236" } }
Prop "river_ripple_0" 16 6 { param($p) R $p 2 2 6 1 "9ccdea"; R $p 9 4 5 1 "9ccdea" } $false
Prop "river_ripple_1" 16 6 { param($p) R $p 4 3 6 1 "9ccdea"; R $p 10 1 4 1 "9ccdea" } $false
Prop "city_bus_stop" 36 40 { param($p) R $p 2 6 2 33 "8a8e96"; R $p 32 6 2 33 "8a8e96"; R $p 1 2 34 5 "3a6a9a"; TextC $p 18 3 "BUS" "f4f0e6" 1; R $p 4 8 28 18 "a8d8ea"; R $p 6 10 2 12 "cfeaf8"; R $p 6 30 24 3 "9a6e4a"; R $p 8 33 2 6 "5a5e66"; R $p 26 33 2 6 "5a5e66" }
Prop "city_bike_rack" 36 18 { param($p) foreach ($i in 0..3) { $x = 2 + $i * 9; R $p $x 4 2 13 "9aa0a8"; R $p ($x + 5) 4 2 13 "9aa0a8"; R $p $x 3 7 2 "9aa0a8" }; R $p 1 15 34 2 "6a6e76" }
Prop "city_fountain" 48 40 { param($p) R $p 2 24 44 14 "c8c2b4"; R $p 2 24 44 3 "e8e2d4"; R $p 5 27 38 6 "6aa8d0"; R $p 21 8 6 18 "d8d2c4"; R $p 16 12 16 3 "e8e2d4"; R $p 23 2 2 7 "9ccdea"; R $p 19 5 2 8 "9ccdea"; R $p 27 5 2 8 "9ccdea"; R $p 16 14 2 12 "9ccdea"; R $p 30 14 2 12 "9ccdea" }
Prop "city_tree_planter" 24 44 { param($p) R $p 4 34 16 9 "a8a294"; R $p 3 33 18 2 "c8c2b4"; R $p 11 20 3 14 "6a4a32"; Disc $p 12 12 10 "4a8a4a"; Disc $p 8 9 5 "6ab06a"; Disc $p 15 14 4 "5a9a5a" }
Prop "city_solar_lamp" 16 44 { param($p) R $p 7 10 2 33 "5a5e66"; R $p 4 41 8 2 "44484e"; R $p 2 2 12 4 "24344f"; R $p 3 3 10 1 "6aa6d0"; R $p 4 7 8 4 "fff0a0"; R $p 3 6 10 2 "8a8e96" }
Prop "city_flower_bed" 32 14 { param($p) R $p 1 6 30 7 "a8a294"; R $p 2 5 28 3 "4a3020"; $cols = "e04a3a","f8e060","e88ab0","f4f0e6","8a6ad0"; foreach ($i in 0..8) { R $p (3 + $i * 3) (2 + $i % 2) 2 2 $cols[$i % 5]; R $p (3 + $i * 3) (4 + $i % 2) 1 2 "5a9a5a" } }
Prop "city_trash_pile" 24 16 { param($p) R $p 2 8 9 7 "44484e"; R $p 4 6 5 3 "5a5e66"; R $p 10 10 8 5 "6a5a7a"; R $p 16 7 6 8 "5a6e3e"; R $p 12 6 4 4 "c8c2b4"; P $p 6 5 "8ab04a"; P $p 19 5 "8ab04a" }

# ================================================================ VEHICLES (all face RIGHT; the game flips them to drive left)
function Car([string]$name, [string]$body, [bool]$taxi = $false) {
  Prop $name 46 24 { param($p) R $p 2 11 42 8 $body; R $p 10 4 24 8 $body; R $p 2 16 42 3 (Shade $body 0.75); R $p 12 6 9 6 "a8d8ea"; R $p 23 6 9 6 "a8d8ea"; R $p 13 7 2 4 "cfeaf8"; R $p 24 7 2 4 "cfeaf8"
    R $p 42 12 2 3 "f8e8a0"; R $p 2 12 2 3 "c0443a"; R $p 20 14 3 1 (Shade $body 0.6); Disc $p 11 19 4 $INK; Disc $p 34 19 4 $INK; Disc $p 11 19 1 "c4c8d0"; Disc $p 34 19 1 "c4c8d0"
    if ($taxi) { R $p 18 1 8 3 "f4f0e6"; R $p 2 14 42 1 $INK } }
}
Car "car_red" "c0443a"; Car "car_blue" "3f6ab8"; Car "car_white" "e4e6ea"; Car "car_green" "5a8a5a"; Car "car_taxi" "e8c040" $true; Car "car_black" "3a3e4a"
Prop "car_van" 56 30 { param($p) R $p 2 4 40 21 "f4f0e6"; R $p 42 10 12 15 "f4f0e6"; R $p 43 12 8 6 "a8d8ea"; R $p 2 22 52 3 "c4c8d0"; R $p 6 9 30 8 "8a1e2a"; TextC $p 21 11 "STERLING" "f0d070" 1; R $p 52 19 2 3 "f8e8a0"; Disc $p 12 25 4 $INK; Disc $p 44 25 4 $INK; Disc $p 12 25 1 "c4c8d0"; Disc $p 44 25 1 "c4c8d0" }
Prop "car_bus" 88 34 { param($p) R $p 2 3 84 26 "3f8f88"; R $p 2 3 84 4 "f4f0e6"; R $p 2 24 84 5 "2d5f5c"; for ($i = 0; $i -lt 6; $i++) { R $p (6 + $i * 12) 9 10 9 "a8d8ea"; R $p (7 + $i * 12) 10 2 7 "cfeaf8" }; R $p 78 9 7 14 "a8d8ea"; R $p 84 21 2 3 "f8e8a0"; TextC $p 40 20 "CITY LINE" "f4f0e6" 1; Disc $p 16 29 4 $INK; Disc $p 70 29 4 $INK; Disc $p 16 29 1 "c4c8d0"; Disc $p 70 29 1 "c4c8d0" }
function Vespa([string]$name, [string]$body, [string]$shirt, [string]$helmet) {
  Prop $name 26 28 { param($p) Disc $p 5 23 3 $INK; Disc $p 20 23 3 $INK; P $p 5 23 "c4c8d0"; P $p 20 23 "c4c8d0"
    R $p 3 16 12 5 $body; R $p 15 18 5 3 $body; R $p 18 9 3 12 $body; R $p 17 8 6 2 "8a8e96"; R $p 21 12 3 3 "f8e8a0"; R $p 4 14 8 2 "3a2a1e"
    R $p 7 7 6 8 $shirt; R $p 12 9 6 2 $shirt; R $p 17 9 1 2 "f2cfa8"; R $p 8 15 7 3 "3a3f52"; R $p 13 17 3 4 "3a3f52"; R $p 13 21 4 1 $INK
    R $p 7 1 6 6 $helmet; R $p 10 4 4 3 "f2cfa8"; R $p 9 3 5 1 (Shade $helmet 0.7) }
}
Vespa "vespa_mint" "8ad0b8" "e88a4a" "f4f0e6"; Vespa "vespa_red" "d0483a" "3f6ab8" "3a3e4a"; Vespa "vespa_cream" "f0e0b0" "5a9a5a" "c0443a"; Vespa "vespa_blue" "5a8ad0" "f4f0e6" "e8c040"
function Bike([string]$name, [string]$frameCol, [string]$shirt, [string]$hair, [string]$skin) {
  foreach ($f in 0, 1) {
    Prop ($name + "_" + $f) 26 30 { param($p) foreach ($cx in 5, 20) { Disc $p $cx 24 4 $INK; Disc $p $cx 24 3 "d8dce0"; Disc $p $cx 24 2 $INK; Disc $p $cx 24 1 "d8dce0"; P $p $cx 24 $INK }
      R $p 5 23 8 1 $frameCol; R $p 12 17 1 7 $frameCol; R $p 12 18 8 1 $frameCol; R $p 19 14 1 10 $frameCol; R $p 18 13 4 1 "3a3e4a"; R $p 10 16 5 1 "3a2a1e"
      R $p 9 8 6 8 $shirt; R $p 14 10 5 2 $shirt; R $p 19 11 1 2 $skin
      if ($f -eq 0) { R $p 10 16 5 3 "3a3f52"; R $p 13 18 2 6 "3a3f52"; R $p 13 24 3 1 $INK } else { R $p 10 16 6 3 "3a3f52"; R $p 15 17 2 4 "3a3f52"; R $p 15 21 3 1 $INK }
      R $p 9 2 6 6 $skin; R $p 8 1 7 3 $hair; R $p 8 3 2 3 $hair; P $p 13 5 $INK }
  }
}
Bike "bike_a" "c0443a" "5ab0a8" "3a2a28" "f2cfa8"; Bike "bike_b" "3f6ab8" "e8c040" "1e1a22" "b07a4c"; Bike "bike_c" "5a9a5a" "f4f0e6" "b0503a" "f5d6b4"; Bike "bike_d" "e8a030" "8a6ab0" "d8b060" "c89868"

# ================================================================ ICONS (16 x 16, for items)
function Icon([string]$name, [scriptblock]$draw) { $t = NewBmp 16 16; & $draw $t; Outline $t; Save $t $name }
Icon "icon_hotdog"   { param($t) R $t 2 7 12 5 "e0a860"; R $t 1 6 14 3 "c0443a"; R $t 3 5 10 1 "f8e060"; P $t 5 6 "f8e060"; P $t 8 7 "f8e060"; P $t 11 6 "f8e060"; R $t 3 11 10 1 "c08840" }
Icon "icon_chili_dog" { param($t) R $t 2 7 12 5 "e0a860"; R $t 1 6 14 3 "8a3a22"; R $t 3 4 10 2 "c0443a"; P $t 5 4 "f8e060"; P $t 9 5 "f8e060"; P $t 12 4 "5a9a5a"; R $t 3 11 10 1 "c08840" }
Icon "icon_lemonade" { param($t) R $t 4 4 8 10 "f8e88a"; R $t 4 4 8 2 "f4f0e6"; R $t 5 7 2 2 "fff8d0"; R $t 9 1 1 5 "c0443a"; R $t 9 9 2 2 "e8c040" }
Icon "icon_pretzel"  { param($t) R $t 3 4 10 9 "b8783a"; Clear $t 5 6 2 3; Clear $t 9 6 2 3; Clear $t 7 10 2 2; P $t 4 5 "f4f0e6"; P $t 11 8 "f4f0e6"; P $t 7 4 "f4f0e6" }
function IconSprinkler([string]$name, [string]$metal, [int]$arms) { Icon $name { param($t) R $t 6 10 4 4 "5a5e66"; R $t 4 13 8 2 "44484e"; R $t 7 5 2 5 $metal; R $t 5 4 6 2 $metal; if ($arms -ge 2) { R $t 3 3 2 2 $metal; R $t 11 3 2 2 $metal }; if ($arms -ge 3) { R $t 7 2 2 2 $metal }; P $t 2 2 "6aa8d0"; P $t 13 2 "6aa8d0"; P $t 8 1 "6aa8d0"; P $t 1 5 "6aa8d0"; P $t 14 5 "6aa8d0" } }
IconSprinkler "icon_sprinkler_1" "b87a4a" 1; IconSprinkler "icon_sprinkler_2" "c4c8d0" 2; IconSprinkler "icon_sprinkler_3" "e8c040" 3
function IconGel([string]$name, [string]$gel, [int]$dots) { Icon $name { param($t) R $t 4 5 8 9 "f4f0e6"; R $t 5 6 6 7 $gel; R $t 5 2 6 3 "5a5e66"; R $t 6 7 1 4 (Shade $gel 1.3); for ($i = 0; $i -lt $dots; $i++) { P $t (5 + ($i % 2) * 4 + 1) (9 + [int][math]::Floor($i / 2) * 2) "f4f0e6" } } }
IconGel "icon_gel_1" "8ac8e8" 1; IconGel "icon_gel_2" "5aa8e0" 2; IconGel "icon_gel_3" "3f86d0" 3; IconGel "icon_gel_4" "2a5ab8" 4
function IconBag([string]$name, [string]$bag, [string]$band) { Icon $name { param($t) R $t 3 4 10 10 $bag; R $t 4 2 8 2 (Shade $bag 0.8); R $t 3 7 10 3 $band; R $t 7 5 2 1 "5a9a5a"; R $t 6 11 4 2 (Shade $bag 0.85) } }
IconBag "icon_fert_silver" "d8dce0" "9aa0a8"; IconBag "icon_fert_gold" "f0e0a0" "e8c040"; IconBag "icon_fert_platinum" "e4f0f4" "8ad8e8"
IconBag "icon_purifier" "c8e8c8" "5ab06a"
Icon "icon_tonic"    { param($t) R $t 6 2 4 3 "7a5236"; R $t 5 5 6 9 "f4f0e6"; R $t 6 8 4 5 "8ad06a"; R $t 6 6 1 6 "ffffff"; P $t 8 10 "f8e060" }
function IconBar([string]$name, [string]$m) { Icon $name { param($t) R $t 2 8 12 5 $m; R $t 4 6 8 2 (Shade $m 1.2); R $t 2 12 12 1 (Shade $m 0.7); R $t 5 7 3 1 "ffffff" } }
IconBar "icon_bar_copper" "c8783a"; IconBar "icon_bar_iron" "a0a8b0"; IconBar "icon_bar_gold" "e8c040"; IconBar "icon_bar_platinum" "c8e8ec"
Icon "icon_trash_bag" { param($t) R $t 4 5 8 9 "44484e"; R $t 3 7 10 6 "44484e"; R $t 6 3 4 2 "5a5e66"; R $t 5 8 1 4 "6a6e76" }
Icon "icon_scrap"     { param($t) R $t 2 9 7 4 "8a8e96"; R $t 8 6 6 4 "b87a4a"; R $t 5 5 3 4 "6a6e76"; P $t 10 7 "e8a86a"; P $t 3 10 "c4c8d0"; R $t 9 11 4 2 "5a5e66" }
Icon "icon_sludge"    { param($t) R $t 4 3 8 11 "6a5a7a"; R $t 4 6 8 1 "4a3e58"; R $t 4 10 8 1 "4a3e58"; R $t 5 2 6 1 "8ab04a"; R $t 9 3 2 5 "8ab04a"; R $t 6 7 3 3 "e0b84a" }
Icon "icon_old_boot"  { param($t) R $t 4 2 5 8 "6a4a32"; R $t 4 9 9 4 "6a4a32"; R $t 4 13 9 1 "3a2a1e"; R $t 5 3 3 1 "8a6a4a"; P $t 10 10 "8a6a4a" }

# ================================================================ PEOPLE
# One 16 x 28 frame. view: 0 = facing down, 1 = facing up, 2 = facing right.  f: 0..3 walk frame (0 and 2 are standing).
# o = the person: skin, hair, style (short / long / bob / slick / bald / cap), top, bottom, shoes, coat, tie, collar, glasses, hat, apron, skirt, beard
function Frame($b, [int]$ox, [int]$oy, [int]$view, [int]$f, $o) {
  $skin = $o.skin; $hair = $o.hair; $top = $o.top; $bot = $o.bottom
  $shoes = if ($o.shoes) { $o.shoes } else { "3a2a2e" }
  $sleeve = if ($o.coat) { $o.coat } else { $top }
  $style = if ($o.style) { $o.style } else { "short" }
  $L = if ($f -eq 1) { 1 } else { 0 }; $Rr = if ($f -eq 3) { 1 } else { 0 }       # which leg is lifted
  if ($view -ne 2) {
    # legs and shoes
    R $b ($ox+4) ($oy+19) 3 (7 - $L) $bot; R $b ($ox+4) ($oy+26 - $L) 3 1 $shoes
    R $b ($ox+9) ($oy+19) 3 (7 - $Rr) $bot; R $b ($ox+9) ($oy+26 - $Rr) 3 1 $shoes
    if ($o.skirt) { R $b ($ox+3) ($oy+18) 10 4 $bot; R $b ($ox+4) ($oy+22) 3 4 $skin; R $b ($ox+9) ($oy+22) 3 4 $skin }
    # body
    R $b ($ox+3) ($oy+11) 10 8 $top
    if ($o.coat) { R $b ($ox+3) ($oy+11) 10 12 $o.coat; if ($view -eq 0) { R $b ($ox+7) ($oy+11) 2 12 $top; R $b ($ox+6) ($oy+11) 1 4 (Shade $o.coat 0.85); R $b ($ox+9) ($oy+11) 1 4 (Shade $o.coat 0.85) } }
    if ($view -eq 0) {
      if ($o.collar) { R $b ($ox+6) ($oy+11) 4 1 $o.collar; R $b ($ox+7) ($oy+12) 2 1 $o.collar }
      if ($o.tie) { R $b ($ox+7) ($oy+12) 2 4 $o.tie; R $b ($ox+7) ($oy+11) 2 1 (Shade $o.tie 0.75) }
      if ($o.apron) { R $b ($ox+5) ($oy+13) 6 8 $o.apron; R $b ($ox+6) ($oy+11) 1 2 $o.apron; R $b ($ox+9) ($oy+11) 1 2 $o.apron }
    }
    # arms swing a little as they walk
    R $b ($ox+2) ($oy+12) 1 (6 - $L) $sleeve; R $b ($ox+2) ($oy+18 - $L) 1 1 $skin
    R $b ($ox+13) ($oy+12) 1 (6 - $Rr) $sleeve; R $b ($ox+13) ($oy+18 - $Rr) 1 1 $skin
    # head
    if ($view -eq 0) {
      R $b ($ox+4) ($oy+4) 8 7 $skin
      R $b ($ox+6) ($oy+7) 1 1 $INK; R $b ($ox+9) ($oy+7) 1 1 $INK; R $b ($ox+7) ($oy+9) 2 1 (Shade $skin 0.72)
      if ($o.glasses) { R $b ($ox+5) ($oy+7) 6 1 "3a3e4a"; R $b ($ox+6) ($oy+7) 1 1 "9ad0e0"; R $b ($ox+9) ($oy+7) 1 1 "9ad0e0" }
      if ($o.beard) { R $b ($ox+4) ($oy+9) 8 2 $o.beard; R $b ($ox+7) ($oy+9) 2 1 (Shade $skin 0.72) }
      switch ($style) {
        "slick" { R $b ($ox+4) ($oy+1) 8 3 $hair; R $b ($ox+3) ($oy+2) 10 2 $hair; R $b ($ox+5) ($oy+1) 5 1 (Shade $hair 1.25); R $b ($ox+6) ($oy+3) 4 1 (Shade $hair 0.85) }
        "bald"  { R $b ($ox+4) ($oy+2) 8 2 $skin; R $b ($ox+3) ($oy+5) 1 2 $hair; R $b ($ox+12) ($oy+5) 1 2 $hair }
        "cap"   { R $b ($ox+3) ($oy+1) 10 3 $o.hat; R $b ($ox+3) ($oy+4) 10 1 (Shade $o.hat 0.7); R $b ($ox+3) ($oy+5) 1 2 $hair; R $b ($ox+12) ($oy+5) 1 2 $hair }
        default { R $b ($ox+3) ($oy+1) 10 4 $hair; R $b ($ox+3) ($oy+5) 1 2 $hair; R $b ($ox+12) ($oy+5) 1 2 $hair
                  if ($style -eq "long") { R $b ($ox+3) ($oy+5) 1 8 $hair; R $b ($ox+12) ($oy+5) 1 8 $hair }
                  if ($style -eq "bob") { R $b ($ox+3) ($oy+5) 1 5 $hair; R $b ($ox+12) ($oy+5) 1 5 $hair; R $b ($ox+4) ($oy+5) 3 1 $hair } }
      }
    } else {
      R $b ($ox+5) ($oy+9) 6 2 $skin
      switch ($style) {
        "slick" { R $b ($ox+4) ($oy+1) 8 8 $hair; R $b ($ox+3) ($oy+2) 10 5 $hair; R $b ($ox+6) ($oy+2) 1 6 (Shade $hair 1.2); R $b ($ox+9) ($oy+2) 1 6 (Shade $hair 1.2); R $b ($ox+3) ($oy+7) 1 1 $skin; R $b ($ox+12) ($oy+7) 1 1 $skin }
        "bald"  { R $b ($ox+4) ($oy+2) 8 8 $skin; R $b ($ox+3) ($oy+6) 10 3 $hair }
        "cap"   { R $b ($ox+3) ($oy+1) 10 4 $o.hat; R $b ($ox+3) ($oy+5) 10 4 $hair }
        default { R $b ($ox+3) ($oy+1) 10 9 $hair; if ($style -eq "long") { R $b ($ox+3) ($oy+10) 10 4 $hair }; if ($style -eq "bob") { R $b ($ox+3) ($oy+10) 10 1 $hair } }
      }
    }
  } else {
    # side view, facing right
    if ($f -eq 1) { R $b ($ox+5) ($oy+19) 2 7 (Shade $bot 0.85); R $b ($ox+4) ($oy+26) 3 1 $shoes; R $b ($ox+8) ($oy+19) 3 6 $bot; R $b ($ox+8) ($oy+25) 4 1 $shoes }
    elseif ($f -eq 3) { R $b ($ox+8) ($oy+19) 3 7 (Shade $bot 0.85); R $b ($ox+8) ($oy+26) 4 1 $shoes; R $b ($ox+5) ($oy+19) 2 6 $bot; R $b ($ox+4) ($oy+25) 3 1 $shoes }
    else { R $b ($ox+6) ($oy+19) 4 7 $bot; R $b ($ox+6) ($oy+26) 5 1 $shoes }
    if ($o.skirt) { R $b ($ox+5) ($oy+18) 6 4 $bot }
    R $b ($ox+5) ($oy+11) 6 8 $top
    if ($o.coat) { R $b ($ox+5) ($oy+11) 6 12 $o.coat; R $b ($ox+10) ($oy+11) 1 12 $top }
    if ($o.tie) { R $b ($ox+10) ($oy+12) 1 4 $o.tie }
    if ($o.apron) { R $b ($ox+9) ($oy+13) 2 8 $o.apron }
    $ax = if ($f -eq 1) { 8 } elseif ($f -eq 3) { 6 } else { 7 }
    R $b ($ox+$ax) ($oy+12) 2 6 (Shade $sleeve 0.85); R $b ($ox+$ax) ($oy+18) 2 1 $skin
    R $b ($ox+5) ($oy+4) 7 7 $skin; R $b ($ox+12) ($oy+7) 1 1 $skin
    R $b ($ox+10) ($oy+7) 1 1 $INK; R $b ($ox+11) ($oy+9) 1 1 (Shade $skin 0.72)
    if ($o.glasses) { R $b ($ox+9) ($oy+6) 3 1 "3a3e4a"; R $b ($ox+7) ($oy+7) 2 1 "3a3e4a" }
    if ($o.beard) { R $b ($ox+8) ($oy+9) 4 2 $o.beard }
    switch ($style) {
      "slick" { R $b ($ox+5) ($oy+1) 7 3 $hair; R $b ($ox+4) ($oy+2) 2 6 $hair; R $b ($ox+6) ($oy+1) 5 1 (Shade $hair 1.25) }
      "bald"  { R $b ($ox+5) ($oy+2) 7 2 $skin; R $b ($ox+4) ($oy+5) 2 3 $hair }
      "cap"   { R $b ($ox+4) ($oy+1) 8 3 $o.hat; R $b ($ox+10) ($oy+4) 4 1 (Shade $o.hat 0.7); R $b ($ox+4) ($oy+4) 2 4 $hair }
      default { R $b ($ox+4) ($oy+1) 8 4 $hair; R $b ($ox+4) ($oy+5) 3 4 $hair
                if ($style -eq "long") { R $b ($ox+3) ($oy+5) 3 9 $hair }; if ($style -eq "bob") { R $b ($ox+4) ($oy+5) 3 6 $hair } }
    }
  }
}

# The close-up face for the talking box (64 x 64). expr: 0 idle, 1 talking, 2 happy, 3 blink, 4 smug, 5 angry, 6 laughing, 7 scheming
function Face($b, [int]$ox, [int]$expr, $o) {
  $skin = $o.skin; $hair = $o.hair; $top = if ($o.coat) { $o.coat } else { $o.top }
  $style = if ($o.style) { $o.style } else { "short" }
  R $b ($ox+16) 49 32 3 $top; R $b ($ox+12) 52 40 12 $top; R $b ($ox+12) 58 40 6 (Shade $top 0.88)
  if ($o.coat) { R $b ($ox+27) 49 10 15 $o.top }
  if ($o.collar) { R $b ($ox+26) 49 12 4 $o.collar; R $b ($ox+28) 53 8 3 $o.collar }
  if ($o.tie) { R $b ($ox+30) 50 4 14 $o.tie; R $b ($ox+29) 50 6 3 (Shade $o.tie 0.75) }
  if ($o.apron) { R $b ($ox+24) 54 16 10 $o.apron }
  R $b ($ox+28) 44 8 6 (Shade $skin 0.9)
  R $b ($ox+20) 22 24 22 $skin; R $b ($ox+22) 44 20 2 $skin; R $b ($ox+18) 30 2 6 (Shade $skin 0.9); R $b ($ox+44) 30 2 6 (Shade $skin 0.9)
  R $b ($ox+22) 36 3 3 (Shade $skin 0.86); R $b ($ox+39) 36 3 3 (Shade $skin 0.86)
  if ($o.beard) { R $b ($ox+20) 37 24 7 $o.beard; R $b ($ox+22) 44 20 2 $o.beard }
  # hair
  switch ($style) {
    "slick" { R $b ($ox+20) 12 24 10 $hair; R $b ($ox+19) 15 26 8 $hair; R $b ($ox+23) 13 18 1 (Shade $hair 1.3); R $b ($ox+22) 16 20 1 (Shade $hair 1.3); R $b ($ox+24) 19 16 1 (Shade $hair 1.15); R $b ($ox+19) 23 2 6 $hair; R $b ($ox+43) 23 2 6 $hair }
    "bald"  { R $b ($ox+20) 16 24 6 $skin; R $b ($ox+22) 14 20 2 $skin; R $b ($ox+18) 24 3 8 $hair; R $b ($ox+43) 24 3 8 $hair }
    "cap"   { R $b ($ox+18) 10 28 12 $o.hat; R $b ($ox+18) 20 28 3 (Shade $o.hat 0.7); R $b ($ox+18) 23 3 6 $hair; R $b ($ox+43) 23 3 6 $hair }
    default { R $b ($ox+18) 12 28 12 $hair; R $b ($ox+20) 10 24 2 $hair; R $b ($ox+18) 24 4 6 $hair; R $b ($ox+42) 24 4 6 $hair
              if ($style -eq "long") { R $b ($ox+15) 22 6 30 $hair; R $b ($ox+43) 22 6 30 $hair }
              if ($style -eq "bob") { R $b ($ox+16) 22 5 20 $hair; R $b ($ox+43) 22 5 20 $hair; R $b ($ox+20) 22 10 3 $hair } }
  }
  # eyes
  $closed = ($expr -eq 3); $arc = ($expr -eq 2 -or $expr -eq 6)
  foreach ($ex in 25, 35) {
    if ($arc) { R $b ($ox+$ex) 32 1 1 $INK; R $b ($ox+$ex+1) 31 2 1 $INK; R $b ($ox+$ex+3) 32 1 1 $INK }
    elseif ($closed) { R $b ($ox+$ex) 32 4 1 $INK }
    elseif ($expr -eq 4 -or $expr -eq 7) { R $b ($ox+$ex) 31 4 3 "ffffff"; R $b ($ox+$ex+2) 32 2 2 $INK; R $b ($ox+$ex) 30 4 1 (Shade $skin 0.8) }   # half-closed
    else { R $b ($ox+$ex) 30 4 4 "ffffff"; R $b ($ox+$ex+1) 31 2 2 $INK }
  }
  # eyebrows say a lot
  $brow = Shade $hair 0.8
  if ($expr -eq 5) { R $b ($ox+24) 27 3 1 $brow; R $b ($ox+27) 28 2 1 $brow; R $b ($ox+37) 27 3 1 $brow; R $b ($ox+35) 28 2 1 $brow }
  elseif ($expr -eq 4 -or $expr -eq 7) { R $b ($ox+24) 27 5 1 $brow; R $b ($ox+35) 25 5 1 $brow }
  else { R $b ($ox+24) 27 5 1 $brow; R $b ($ox+35) 27 5 1 $brow }
  if ($o.glasses) { foreach ($ex in 24, 34) { R $b ($ox+$ex) 29 6 1 "3a3e4a"; R $b ($ox+$ex) 34 6 1 "3a3e4a"; R $b ($ox+$ex) 29 1 6 "3a3e4a"; R $b ($ox+$ex+5) 29 1 6 "3a3e4a" }; R $b ($ox+30) 31 4 1 "3a3e4a" }
  # mouth
  $lip = Shade $skin 0.62
  switch ($expr) {
    1 { R $b ($ox+30) 38 4 4 "8a3038"; R $b ($ox+30) 38 4 1 "ffffff" }
    2 { R $b ($ox+29) 39 1 1 $lip; R $b ($ox+30) 40 4 1 $lip; R $b ($ox+34) 39 1 1 $lip }
    4 { R $b ($ox+30) 40 5 1 $lip; R $b ($ox+35) 39 1 1 $lip; R $b ($ox+36) 38 1 1 $lip }
    5 { R $b ($ox+30) 40 4 1 $lip; R $b ($ox+29) 41 1 1 $lip; R $b ($ox+34) 41 1 1 $lip }
    6 { R $b ($ox+29) 38 6 5 "8a3038"; R $b ($ox+29) 38 6 1 "ffffff"; R $b ($ox+31) 42 2 1 "c86a6a" }
    7 { R $b ($ox+29) 39 1 1 $lip; R $b ($ox+30) 40 5 1 $lip; R $b ($ox+35) 39 1 1 $lip; R $b ($ox+31) 39 3 1 "ffffff" }
    default { R $b ($ox+30) 40 4 1 $lip }
  }
}

# Makes npc_<id>.png (walk sheet: 4 frames x 3 rows of 16 x 28: down, up, side) and portraits_<id>.png (idle, talk, happy, blink).
function Person([string]$id, $o, [bool]$portrait = $true) {
  $b = NewBmp 64 84
  for ($view = 0; $view -lt 3; $view++) { for ($f = 0; $f -lt 4; $f++) { Frame $b ($f * 16) ($view * 28) $view $f $o; Outline $b ($f * 16) ($view * 28) 16 28 } }
  Save $b ("npc_" + $id)
  if ($portrait) { $p = NewBmp 256 64; for ($e = 0; $e -lt 4; $e++) { Face $p ($e * 64) $e $o }; Save $p ("portraits_" + $id) }
}

# ---- the named people of the city
$SCI   = @{ skin="f5d6b4"; hair="e8c868"; style="bob"; top="5ab0a8"; bottom="3a3f52"; coat="f4f4f0"; glasses=$true; shoes="4a4a52" }          # Dr. Hale, the scientist
$CEO   = @{ skin="f2cfa8"; hair="f0d878"; style="slick"; top="1e2638"; bottom="1e2638"; collar="ffffff"; tie="c0282a"; shoes="1a1418" }      # Victor Sterling
$CLERK = @{ skin="c89868"; hair="3a2a28"; style="short"; top="5a6e5a"; bottom="4a4438"; collar="f4f0e6"; tie="8a6a1e"; glasses=$true }
$PLANT = @{ skin="b07a4c"; hair="2a2420"; style="cap"; hat="e8a030"; top="e8a030"; bottom="4a5a66"; beard="2a2420" }
$VEND  = @{ skin="e8b890"; hair="6a4a2a"; style="cap"; hat="f4f0e6"; top="e04a3a"; bottom="3a3f52"; apron="f4f0e6"; beard="6a4a2a" }
Person "scientist" $SCI; Person "clerk" $CLERK; Person "plant_boss" $PLANT; Person "vendor" $VEND
Person "worker_a" @{ skin="f2cfa8"; hair="3a2a28"; style="short"; top="3a4252"; bottom="3a4252"; collar="ffffff"; tie="3f6ab8" }
Person "worker_b" @{ skin="8a5c3c"; hair="1c1a20"; style="long"; top="4a4458"; bottom="4a4458"; collar="ffffff"; skirt=$true }
Person "worker_c" @{ skin="d8a878"; hair="8a8a8a"; style="bald"; top="50505a"; bottom="50505a"; collar="ffffff"; tie="5a9a5a"; glasses=$true }
Person "worker_d" @{ skin="f5d6b4"; hair="b0503a"; style="bob"; top="2e3e5c"; bottom="2e3e5c"; collar="ffffff"; tie="c9a03a" }
Person "dumper_a" @{ skin="c89868"; hair="2a2420"; style="cap"; hat="e0b84a"; top="6a7a74"; bottom="4a524e"; apron="5a5e66" }
Person "dumper_b" @{ skin="f2cfa8"; hair="6a4a2a"; style="cap"; hat="e0b84a"; top="6a7a74"; bottom="4a524e"; apron="5a5e66"; beard="6a4a2a" }
Person "guard"    @{ skin="9a6a44"; hair="1c1a20"; style="cap"; hat="1a2438"; top="1a2438"; bottom="1a2438"; collar="c9a03a" }
Person "student_a" @{ skin="f2cfa8"; hair="1e1a22"; style="short"; top="c0443a"; bottom="4a6a9a" }
Person "student_b" @{ skin="b07a4c"; hair="1e1a22"; style="long"; top="e8c040"; bottom="5a4a6a" }

# ---- the passers-by: twelve looks, mixed at random by the game (walk sheets and faces)
$peds = @(
  @{ skin="f2cfa8"; hair="3a2a28"; style="short"; top="5ab0a8"; bottom="3a3f52" }, @{ skin="8a5c3c"; hair="1c1a20"; style="long"; top="e88a4a"; bottom="4a6a9a" },
  @{ skin="c89868"; hair="2a2420"; style="bob"; top="8a6ab0"; bottom="3a3a44"; skirt=$true }, @{ skin="f5d6b4"; hair="d8b060"; style="long"; top="e86a8a"; bottom="5a4a6a" },
  @{ skin="d8a878"; hair="9a9a9a"; style="bald"; top="8a9a6a"; bottom="5a5046"; glasses=$true }, @{ skin="b07a4c"; hair="1e1a22"; style="cap"; hat="c0443a"; top="f4f0e6"; bottom="30303a" },
  @{ skin="f2cfa8"; hair="b0503a"; style="short"; top="3f6ab8"; bottom="6a5a4a"; beard="b0503a" }, @{ skin="7c5234"; hair="1c1a20"; style="short"; top="e8c040"; bottom="3a3f52" },
  @{ skin="f5d6b4"; hair="6a4a2a"; style="bob"; top="5a9a5a"; bottom="4a4a52"; coat="c9a878" }, @{ skin="c89868"; hair="b8b4b0"; style="bob"; top="c86a8a"; bottom="6a5a7a"; skirt=$true; glasses=$true },
  @{ skin="9a6a44"; hair="2a2020"; style="slick"; top="50505a"; bottom="50505a"; collar="ffffff"; tie="8a6ab0" }, @{ skin="f2cfa8"; hair="1e1a22"; style="cap"; hat="3f8f88"; top="3f8f88"; bottom="4a5a66" }
)
for ($i = 0; $i -lt $peds.Count; $i++) { Person ("ped_" + ($i + 1)) $peds[$i] }

# ---- Victor Sterling gets more: a walk sheet, EIGHT faces (two rows), and a sheet of little performances
$b = NewBmp 64 84
for ($view = 0; $view -lt 3; $view++) { for ($f = 0; $f -lt 4; $f++) {
  $x = $f * 16; $y = $view * 28; Frame $b $x $y $view $f $CEO
  if ($view -eq 0) { R $b ($x+5) ($y+12) 1 5 "2e3a54"; R $b ($x+10) ($y+12) 1 5 "2e3a54"; R $b ($x+11) ($y+13) 1 1 "f0d070" }      # lapels and a gold pocket square
  if ($view -eq 1) { R $b ($x+7) ($y+12) 2 7 "161c2a" }                                                                          # the seam down the back of the jacket
  Outline $b $x $y 16 28
} }
Save $b "npc_ceo"
$p = NewBmp 256 128
for ($e = 0; $e -lt 8; $e++) {
  $row = NewBmp 64 64; Face $row 0 $e $CEO
  R $row 20 52 6 10 "2e3a54"; R $row 38 52 6 10 "2e3a54"; R $row 41 55 4 3 "f0d070"                                               # lapels, pocket square
  $g = [System.Drawing.Graphics]::FromImage($p); $g.DrawImage($row, ($e % 4) * 64, [int][math]::Floor($e / 4) * 64, 64, 64); $g.Dispose(); $row.Dispose()
}
Save $p "portraits_ceo"
# ceo_acts.png: 4 frames x 4 rows of 16 x 28.  row 0 = flipping a gold coin, 1 = laughing, 2 = checking his watch, 3 = straightening his tie
$b = NewBmp 64 112
for ($row = 0; $row -lt 4; $row++) { for ($f = 0; $f -lt 4; $f++) {
  $x = $f * 16; $y = $row * 28; Frame $b $x $y 0 0 $CEO
  R $b ($x+5) ($y+12) 1 5 "2e3a54"; R $b ($x+10) ($y+12) 1 5 "2e3a54"
  switch ($row) {
    0 { Clear $b ($x+13) ($y+12) 1 7; R $b ($x+13) ($y+12) 1 3 "1e2638"; R $b ($x+13) ($y+14) 2 1 "1e2638"; R $b ($x+14) ($y+13) 1 1 "f2cfa8"      # arm bent, hand out
        $cy = @(11, 6, 2, 6)[$f]; R $b ($x+14) ($y+$cy) $(if ($f % 2 -eq 0) { 2 } else { 1 }) 2 "f0d070" }
    1 { $up = $f % 2; if ($up -eq 1) { Clear $b ($x+2) ($y+11) 1 2; Clear $b ($x+13) ($y+11) 1 2; R $b ($x+2) ($y+11) 1 6 "1e2638"; R $b ($x+13) ($y+11) 1 6 "1e2638"; R $b ($x+2) ($y+17) 1 1 "f2cfa8"; R $b ($x+13) ($y+17) 1 1 "f2cfa8"; Clear $b ($x+2) ($y+18) 1 1; Clear $b ($x+13) ($y+18) 1 1 }
        R $b ($x+6) ($y+7) 1 1 "f2cfa8"; R $b ($x+9) ($y+7) 1 1 "f2cfa8"; R $b ($x+5) ($y+7) 2 1 $INK; R $b ($x+9) ($y+7) 2 1 $INK; R $b ($x+7) ($y+9) 2 (1 + $up) "8a3038" }
    2 { Clear $b ($x+2) ($y+12) 1 7; R $b ($x+2) ($y+12) 1 3 "1e2638"; R $b ($x+2) ($y+14) 3 1 "1e2638"; R $b ($x+4) ($y+13) 1 1 "f0d070"; R $b ($x+5) ($y+13) 1 1 "f2cfa8"
        if ($f -ge 2) { R $b ($x+6) ($y+7) 1 1 "f2cfa8"; R $b ($x+9) ($y+7) 1 1 "f2cfa8"; R $b ($x+5) ($y+8) 1 1 $INK; R $b ($x+8) ($y+8) 1 1 $INK } }
    3 { Clear $b ($x+13) ($y+12) 1 7; R $b ($x+13) ($y+12) 1 2 "1e2638"; R $b ($x+10) ($y+13) 3 1 "1e2638"; R $b ($x+9) ($y+12 + ($f % 2)) 1 1 "f2cfa8"; R $b ($x+7) ($y+12) 2 (4 + ($f % 2)) "c0282a" }
  }
  Outline $b $x $y 16 28
} }
Save $b "ceo_acts"

"made " + (Get-ChildItem $out -Filter *.png).Count + " city pictures in " + $out
