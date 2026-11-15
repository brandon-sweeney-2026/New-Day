# Makes the suburb residents' walk sheets and portrait sheets by recolouring the pack's own NPC sheets
# (same 64x84 walk layout: 4 frames x 3 rows of 16x28; same 256x64 portrait layout), plus two extra house pictures.
param([string]$Project)
Add-Type -AssemblyName System.Drawing
$fam = Join-Path $Project "Assets\Project\Art\Family"
$hw  = Join-Path $Project "Assets\Project\Art\Hardware"
$hs  = Join-Path $Project "Assets\Project\Art\Houses"
$out = Join-Path $Project "Assets\Project\Art\Residents"

function Rgb([string]$hex) { return @([Convert]::ToInt32($hex.Substring(0,2),16), [Convert]::ToInt32($hex.Substring(2,2),16), [Convert]::ToInt32($hex.Substring(4,2),16)) }

# Each base sheet: which colours belong to which part. The FIRST colour of a part is its main colour; the others are its shades.
$bases = @{
  dad = @{ walk="$fam\npc_dad.png"; face="$fam\portraits_dad.png"; parts=@{
      hair=@("3a3640","1e1e28","282832"); shirt=@("3d7a8a","366d7c","4992a5","468c9e","30616e"); apron=@("3d7a58","4f9e72");
      pants=@("3a3f52","313545"); skin=@("f2cfa8","d9ba97","fed9b0","debe9a") } }
  mom = @{ walk="$fam\npc_mom.png"; face="$fam\portraits_mom.png"; parts=@{
      hair=@("3a2a28"); shirt=@("9a8ac0","8a7cac","b8a5e6","b19edc","7b6e99"); apron=@("3d7a58","4f9e72");
      pants=@("c9b898","aa9c81"); skin=@("f2cfa8","d9ba97","fed9b0","debe9a") } }
  son = @{ walk="$fam\npc_son.png"; face="$fam\portraits_son.png"; parts=@{
      hair=@("1e1a22"); shirt=@("35456a","2f3e5f","3f527f","3c4f79","2a3754");
      pants=@("4a6a9a","3e5a82"); skin=@("f2cfa8","d9ba97","fed9b0","debe9a") } }
  daughter = @{ walk="$fam\npc_daughter.png"; face="$fam\portraits_daughter.png"; parts=@{
      hair=@("1e1a22"); shirt=@("e0b84a","c9a542","ffdc58","ffd355","b3933b"); clip=@("e86a8a");
      pants=@("5a4a6a","4c3e5a"); skin=@("f5d6b4","dcc0a2","ffe0bd","e1c4a5") } }
  frank = @{ walk="$hw\npc_frank.png"; face="$hw\portraits_frank.png"; parts=@{
      hair=@("6a4a2a","5a4630","4a3a2e","3a2a22","8a6038","6e583c"); shirt=@("f8f8f4","dfdfdb","d8d8d2");
      pants=@("3f5a8a","354c75"); skin=@("e8b890","cca17e","fac69b","ffe9d0","f5c398","fff0dc","e0a888") } }
}

# Recolour: every shade of a part becomes the new main colour, made as much lighter or darker as the old shade was.
function Recolor([string]$src, [string]$dst, $parts, $colors, [bool]$child) {
  $map = @{}
  foreach ($part in $colors.Keys) {
    if (-not $parts.ContainsKey($part)) { continue }
    $ref = Rgb $parts[$part][0]; $to = Rgb $colors[$part]
    foreach ($shade in $parts[$part]) {
      $c = Rgb $shade; $n = @(0,0,0)
      $k = (0.3*$c[0] + 0.59*$c[1] + 0.11*$c[2]) / [math]::Max(1, 0.3*$ref[0] + 0.59*$ref[1] + 0.11*$ref[2])   # how much lighter or darker this shade is than the main colour
      for ($i=0; $i -lt 3; $i++) { $n[$i] = [int][math]::Min(255, [math]::Round($to[$i] * $k)) }
      $map[$shade] = [System.Drawing.Color]::FromArgb(255, $n[0], $n[1], $n[2])
    }
  }
  $a = New-Object System.Drawing.Bitmap $src
  $b = New-Object System.Drawing.Bitmap $a.Width, $a.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  for ($y=0; $y -lt $a.Height; $y++) { for ($x=0; $x -lt $a.Width; $x++) {
    $p = $a.GetPixel($x,$y)
    if ($p.A -eq 255) { $k = "{0:x2}{1:x2}{2:x2}" -f $p.R,$p.G,$p.B; if ($map.ContainsKey($k)) { $p = $map[$k] } }
    $b.SetPixel($x,$y,$p)
  } }
  $a.Dispose()
  if ($child) {
    # a child is the same drawing with 3 rows taken out of the body and 2 out of the legs, standing on the same ground line
    $keep = @(0..13) + @(17..21) + @(24..27)
    $c = New-Object System.Drawing.Bitmap $b.Width, $b.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($row=0; $row -lt 3; $row++) { for ($i=0; $i -lt $keep.Count; $i++) {
      $sy = $row*28 + $keep[$i]; $dy = $row*28 + (28 - $keep.Count) + $i
      for ($x=0; $x -lt $b.Width; $x++) { $c.SetPixel($x, $dy, $b.GetPixel($x, $sy)) }
    } }
    $b.Dispose(); $b = $c
  }
  $b.Save($dst, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
}

# id, base sheet, child?, then the new main colour of each part (a part left out keeps the base's colour)
$people = @(
  @{ id="emeka";  base="son";      c=@{ hair="1c1a20"; skin="7c5234"; shirt="4a78b0"; pants="3a3f52" } },
  @{ id="grace";  base="daughter"; c=@{ hair="2a1c18"; skin="8a5c3c"; shirt="c0504a"; pants="3a3640"; clip="e8c050" } },
  @{ id="nia";    base="daughter"; child=$true; c=@{ hair="2a1c18"; skin="8a5c3c"; shirt="f08a5a"; pants="4a6a9a"; clip="f4f0e6" } },
  @{ id="walt";   base="dad";      c=@{ hair="6a6258"; skin="d8a878"; shirt="8a9a6a"; apron="6a5a44"; pants="5a5046" } },
  @{ id="rosa";   base="mom";      c=@{ hair="b8b4b0"; skin="d8a878"; shirt="c86a8a"; apron="f0e6d0"; pants="6a5a7a" } },
  @{ id="priya";  base="mom";      c=@{ hair="1e1a22"; skin="b07a4c"; shirt="5ab0a8"; apron="5ab0a8"; pants="4a9088" } },
  @{ id="dev";    base="son";      c=@{ hair="1e1a22"; skin="b07a4c"; shirt="d04a3a"; pants="30303a" } },
  @{ id="bea";    base="daughter"; c=@{ hair="d8b060"; skin="f5d6b4"; shirt="6a8a5a"; pants="4a5a7a"; clip="5a8ad0" } },
  @{ id="tariq";  base="son";      c=@{ hair="2a2020"; skin="c89868"; shirt="5a4a7a"; pants="3a3a44" } },
  @{ id="yuki";   base="mom";      c=@{ hair="1e1a22"; skin="f2cfa8"; shirt="3a4a6a"; apron="f0f0ea"; pants="4a4a52" } },
  @{ id="kenji";  base="dad";      c=@{ hair="9a9a9a"; skin="f2cfa8"; shirt="7a8a9a"; apron="7a8a9a"; pants="5a5a50" } },
  @{ id="marcus"; base="son";      c=@{ hair="3a2a22"; skin="9a6a44"; shirt="5a6478"; pants="3a4050" } },
  @{ id="lena";   base="daughter"; c=@{ hair="b0503a"; skin="f5d6b4"; shirt="70b0c0"; pants="6a5a4a"; clip="e8c050" } },
  @{ id="sam";    base="dad";      c=@{ hair="5a4030"; skin="f2cfa8"; shirt="b08a4a"; apron="b08a4a"; pants="4a5060" } },
  @{ id="mateo";  base="son";      c=@{ hair="2a2420"; skin="c89868"; shirt="5a9a5a"; pants="6a5a40" } },
  @{ id="lucia";  base="mom";      c=@{ hair="2a2420"; skin="c89868"; shirt="e8a030"; apron="e8a030"; pants="3a3a44" } },
  @{ id="ada";    base="mom";      c=@{ hair="d0c8b8"; skin="f2cfa8"; shirt="8a6ab0"; apron="5a9a6a"; pants="6a6a72" } },
  @{ id="gus";    base="frank";    c=@{ hair="a8a8a0"; skin="d8a878"; shirt="b04a3a"; pants="6a6450" } }
)
foreach ($p in $people) {
  $b = $bases[$p.base]; $child = [bool]$p.child
  Recolor $b.walk (Join-Path $out ("npc_" + $p.id + ".png")) $b.parts $p.c $child
  Recolor $b.face (Join-Path $out ("portraits_" + $p.id + ".png")) $b.parts $p.c $false
}

# two more house pictures: the blue colonial in dusty rose, the yellow bungalow in lavender
$house = @{ siding=@("7ea0c4","6b88a6","88acd3","587089"); shutters=@("3f5a8a","32486e") }
Recolor "$hs\sub_house_1.png" "$hs\sub_house_9.png" $house @{ siding="c98f8f"; shutters="5a3a48" } $false
$house = @{ siding=@("e8c85a","c5aa4c","fad861","a28c3e") }
Recolor "$hs\sub_house_3.png" "$hs\sub_house_10.png" $house @{ siding="9a8fc8" } $false
"made " + (Get-ChildItem $out -Filter *.png).Count + " resident pictures"
