# Draws the default "storybook" art for the game's windows (menus, player menu, title screen, mailbox, dialogue,
# day recap, forest stump): a leather book cover with brass corners, cream paper pages, bookmark ribbons, a
# honey-coloured highlighter wash, a ribbon banner for titles, inked boxes for item slots and more.
#
#     powershell -ExecutionPolicy Bypass -File Tools\make_book_ui_art.ps1 -Project "<project folder>"
#
# It writes ONE PNG PER PICTURE into Assets\Project\Art\UI\Book. Every picture is small pixel art; the game shows it at
# 2 screen pixels per art pixel and stretches or tiles the middle, so the edges always stay crisp.
#
# YOU CAN REPAINT ANY OF THESE FILES YOURSELF (Aseprite, Photoshop, Krita...). Keep the file name the same.
# The "border" numbers below (also set by Farm > Set Up > Book UI) are how many pixels at each edge stay fixed;
# the middle part is stretched or repeated to fit the window. If you change a picture's size a lot, open it in
# Unity's Sprite Editor and drag the green border lines to match.
#
# Running this script again OVERWRITES the PNGs with the defaults, so don't run it after you've painted your own.
param([string]$Project = (Split-Path -Parent $PSScriptRoot))

Add-Type -AssemblyName System.Drawing
$out = Join-Path $Project "Assets\Project\Art\UI\Book"
New-Item -ItemType Directory -Force $out | Out-Null

# Same seed every run, so the paper grain looks the same each time.
$rng = New-Object System.Random 20261116

function Hex([string]$h, [int]$a = 255) { $c = [System.Drawing.ColorTranslator]::FromHtml($h); return [System.Drawing.Color]::FromArgb($a, $c.R, $c.G, $c.B) }
function New-Bmp([int]$w, [int]$h) { return New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb) }
function Save-Bmp($bmp, [string]$name) { $bmp.Save((Join-Path $out ($name + ".png")), [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose(); Write-Host ("  " + $name + ".png") }
function Clamp([double]$v) { if ($v -lt 0) { return 0 } if ($v -gt 255) { return 255 } return [int]$v }
# Darker (f < 1) or lighter (f > 1) version of a colour, keeping its see-through amount.
function Shade($c, [double]$f) { return [System.Drawing.Color]::FromArgb($c.A, (Clamp ($c.R * $f)), (Clamp ($c.G * $f)), (Clamp ($c.B * $f))) }
# Blend from colour a to colour b (t = 0 is a, t = 1 is b).
function Mix($a, $b, [double]$t) {
    return [System.Drawing.Color]::FromArgb((Clamp ($a.A + ($b.A - $a.A) * $t)), (Clamp ($a.R + ($b.R - $a.R) * $t)), (Clamp ($a.G + ($b.G - $a.G) * $t)), (Clamp ($a.B + ($b.B - $a.B) * $t)))
}
function WithAlpha($c, [int]$a) { return [System.Drawing.Color]::FromArgb($a, $c.R, $c.G, $c.B) }

# ---------------------------------------------------------------- colours (change these to recolour everything)
$paper      = Hex "#f3e3c1"     # page
$paperLight = Hex "#f9eed5"
$paperFiber = Hex "#e7d3a9"
$paperRim   = Hex "#e3cb9b"     # slightly darker band just inside the page edge
$pageLine   = Hex "#8c6a3f"     # outline of the page
$stackLight = Hex "#efdcb5"     # the stacked page edges
$stackDark  = Hex "#c7a873"
$gutterInk  = Hex "#6b4728"

$leather    = Hex "#7b3c22"     # cover
$leatherDk  = Hex "#5b2a16"
$leatherLt  = Hex "#91492a"
$leatherEdge= Hex "#331509"
$stitch     = Hex "#e2b65c"
$brass      = Hex "#c9973d"
$brassLt    = Hex "#f2d27d"
$brassDk    = Hex "#7a5521"

$ink        = Hex "#3d2716"
$inkSoft    = Hex "#7d6044"
$red        = Hex "#a63a28"
$redDk      = Hex "#6f2116"
$redLt      = Hex "#c95a3e"
$tan        = Hex "#bfa06a"
$tanDk      = Hex "#8a6c3c"
$tanLt      = Hex "#d8bf8b"
$honey      = Hex "#f1c75a"
$clear      = [System.Drawing.Color]::FromArgb(0, 0, 0, 0)

# ---------------------------------------------------------------- paper pages
# $stack = which sides show the edges of the other pages under this one (any of "L", "R", "B").
# $gutter = the side that goes into the book's fold ("L", "R" or ""): no outline there, and it gets darker.
function Draw-Page([int]$w, [int]$h, [string]$stack, [string]$gutter) {
    $bmp = New-Bmp $w $h
    $sL = $stack.Contains("L"); $sR = $stack.Contains("R"); $sB = $stack.Contains("B")
    $sw = 6                                             # width of the stacked-page strip
    $x0 = if ($gutter -eq "L") { 0 } elseif ($sL) { $sw } else { 1 }
    $x1 = if ($gutter -eq "R") { $w - 1 } elseif ($sR) { $w - 1 - $sw } else { $w - 2 }
    $y0 = 1
    $y1 = if ($sB) { $h - 1 - $sw } else { $h - 2 }
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $c = $null
            $inStackR = $sR -and $x -gt $x1 -and $y -ge 2
            $inStackL = $sL -and $x -lt $x0 -and $y -ge 2
            $inStackB = $sB -and $y -gt $y1 -and $x -ge ($(if ($sL) { 2 } else { 0 })) -and $x -le ($(if ($sR) { $w - 3 } else { $w - 1 }))
            if ($inStackR -or $inStackL -or $inStackB) {
                # stacked page edges: thin alternating light / dark lines, outlined at the very edge
                if ($inStackR) { $k = $w - 1 - $x } elseif ($inStackL) { $k = $x } else { $k = $h - 1 - $y }
                if ($inStackB -and ($inStackR -or $inStackL)) { $k = [Math]::Min($k, $h - 1 - $y) }
                if ($k -eq 0) { $c = $pageLine } elseif ($k % 2 -eq 1) { $c = $stackDark } else { $c = $stackLight }
            }
            elseif ($x -lt $x0 -or $x -gt $x1 -or $y -lt $y0 -or $y -gt $y1) {
                $c = $pageLine
                if (($x -lt $x0 -and $y -lt 2) -or ($x -gt $x1 -and $y -lt 2)) { $c = $clear }     # rounded outer corners
            }
            else {
                # the paper itself: soft grain with a few darker fibres and lighter flecks
                $n = $rng.NextDouble()
                $c = $paper
                if ($n -lt 0.07) { $c = Mix $paper $paperFiber 0.6 } elseif ($n -gt 0.96) { $c = Mix $paper $paperLight 0.7 }
                # a warm band just inside the outer edges
                $d = 99
                if ($gutter -ne "L") { $d = [Math]::Min($d, $x - $x0) }
                if ($gutter -ne "R") { $d = [Math]::Min($d, $x1 - $x) }
                $d = [Math]::Min($d, [Math]::Min($y - $y0, $y1 - $y))
                if ($d -le 1) { $c = Mix $c $paperRim 0.8 } elseif ($d -le 3) { $c = Mix $c $paperRim 0.35 }
                # the fold: the paper curves down into the spine, so it gets darker
                if ($gutter -ne "") {
                    $g = if ($gutter -eq "R") { $w - 1 - $x } else { $x }
                    if ($g -lt 14) { $t = [Math]::Pow(1 - $g / 14.0, 1.7); $c = Mix $c $gutterInk (0.42 * $t) }
                }
            }
            $bmp.SetPixel($x, $y, $c)
        }
    }
    return $bmp
}

# ---------------------------------------------------------------- leather cover with stitching and brass corners
function Draw-Cover([int]$w, [int]$h) {
    $bmp = New-Bmp $w $h
    $inset = 7
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $n = $rng.NextDouble()
            $c = $leather
            if ($n -lt 0.16) { $c = Mix $leather $leatherDk 0.7 } elseif ($n -gt 0.90) { $c = $leatherLt }
            # raised edge: light along the top/left, dark along the bottom/right, black-brown outline
            if ($x -eq 1 -or $y -eq 1) { $c = Mix $c $leatherLt 0.8 }
            if ($x -eq $w - 2 -or $y -eq $h - 2) { $c = $leatherDk }
            if ($x -eq 0 -or $y -eq 0 -or $x -eq $w - 1 -or $y -eq $h - 1) { $c = $leatherEdge }
            # gold stitching all the way round
            $onV = ($x -eq $inset -or $x -eq $w - 1 - $inset) -and $y -ge $inset -and $y -le $h - 1 - $inset
            $onH = ($y -eq $inset -or $y -eq $h - 1 - $inset) -and $x -ge $inset -and $x -le $w - 1 - $inset
            if (($onV -and ($y % 4) -lt 2) -or ($onH -and ($x % 4) -lt 2)) { $c = $stitch }
            if (($onV -and ($y % 4) -eq 2) -or ($onH -and ($x % 4) -eq 2)) { $c = $leatherDk }      # the hole after each stitch
            # brass corner caps
            $cx = [Math]::Min($x, $w - 1 - $x); $cy = [Math]::Min($y, $h - 1 - $y)
            $s = $cx + $cy
            if ($s -le 16 -and $cx -ge 1 -and $cy -ge 1) {
                if ($s -ge 15) { $c = $brassDk } elseif ($s -eq 14) { $c = $brassLt } else { $c = $brass; if ($rng.NextDouble() -lt 0.12) { $c = Shade $brass 0.9 } }
                if ($cx -eq 1 -or $cy -eq 1) { $c = $brassDk }
                if ($cx -eq 5 -and $cy -eq 5) { $c = $brassDk }                 # rivet
                if (($cx -eq 4 -and $cy -eq 4)) { $c = $brassLt }
            }
            $bmp.SetPixel($x, $y, $c)
        }
    }
    return $bmp
}

# ---------------------------------------------------------------- bookmark ribbon tab (round top; the bottom tucks under the cover)
function Draw-Tab([int]$w, [int]$h, $base, $light, $dark) {
    $bmp = New-Bmp $w $h
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            # round off the two top corners (radius 5)
            $r = 5
            $cx = if ($x -lt $r) { $r - $x } elseif ($x -gt $w - 1 - $r) { $x - ($w - 1 - $r) } else { 0 }
            $cy = if ($y -lt $r) { $r - $y } else { 0 }
            $dist = [Math]::Sqrt($cx * $cx + $cy * $cy)
            if ($dist -gt $r + 0.4) { continue }
            $c = $base
            if (($x % 2) -eq 0 -and $rng.NextDouble() -lt 0.35) { $c = Shade $base 0.93 }           # woven threads
            if ($x -le 2) { $c = Mix $c $light 0.5 }                                                  # light from the left
            if ($x -ge $w - 3) { $c = Mix $c $dark 0.5 }
            # stitched line just inside the edge
            $edge = $dist -gt $r - 0.6 -or $x -eq 0 -or $x -eq $w - 1 -or $y -eq 0
            $inner = (($x -eq 3 -or $x -eq $w - 4) -and $y -ge 4) -or ($y -eq 3 -and $x -ge 4 -and $x -le $w - 5)
            if ($inner -and (($x + $y) % 3) -ne 0) { $c = $light }
            if ($edge) { $c = $dark }
            $bmp.SetPixel($x, $y, $c)
        }
    }
    return $bmp
}

Write-Host "Writing book art to $out"

Save-Bmp (Draw-Cover 64 64) "book_cover"
Save-Bmp (Draw-Page 64 64 "RB" "") "page_single"
Save-Bmp (Draw-Page 64 64 "LB" "R") "page_left"
Save-Bmp (Draw-Page 64 64 "RB" "L") "page_right"

# ---------------------------------------------------------------- the fold between two pages (stretched to the page height)
$bmp = New-Bmp 24 4
for ($y = 0; $y -lt 4; $y++) {
    for ($x = 0; $x -lt 24; $x++) {
        $d = [Math]::Abs($x - 11.5)
        $a = [int](115 * [Math]::Pow([Math]::Max(0, 1 - $d / 12.0), 2.2))
        $c = WithAlpha $gutterInk $a
        if ($d -lt 1) { $c = WithAlpha (Shade $gutterInk 0.7) 230 }
        $bmp.SetPixel($x, $y, $c)
    }
}
Save-Bmp $bmp "gutter"

Save-Bmp (Draw-Tab 32 40 $tan $tanLt $tanDk) "tab"
Save-Bmp (Draw-Tab 32 40 $red $redLt $redDk) "tab_selected"

# ---------------------------------------------------------------- honey watercolour highlight for the chosen row
$bmp = New-Bmp 48 16
for ($x = 0; $x -lt 48; $x++) {
    # ragged ends: each column starts and stops at a slightly different height
    $endDist = [Math]::Min($x, 47 - $x)
    $cut = if ($endDist -lt 6) { 6 - $endDist + $rng.Next(0, 2) } else { $rng.Next(0, 2) }
    for ($y = 0; $y -lt 16; $y++) {
        if ($y -lt [Math]::Floor($cut / 2) + 1 -or $y -gt 14 - [Math]::Floor($cut / 2)) { continue }
        # watercolour collects at the edges of the wash, so the rim is a bit stronger
        $edge = ($y -le [Math]::Floor($cut / 2) + 1) -or ($y -ge 13 - [Math]::Floor($cut / 2)) -or $endDist -lt 2
        $a = if ($edge) { 175 } else { 120 + (($y * 7) % 13) }
        $bmp.SetPixel($x, $y, (WithAlpha $honey $a))
    }
}
Save-Bmp $bmp "highlight"

# ---------------------------------------------------------------- dotted ruled line under each row (repeated sideways)
$bmp = New-Bmp 6 1
$bmp.SetPixel(0, 0, (WithAlpha $inkSoft 110)); $bmp.SetPixel(1, 0, (WithAlpha $inkSoft 110))
Save-Bmp $bmp "rule"

# ---------------------------------------------------------------- red ribbon banner for titles (forked tails at both ends)
$bmp = New-Bmp 64 20
for ($y = 0; $y -lt 20; $y++) {
    for ($x = 0; $x -lt 64; $x++) {
        $e = [Math]::Min($x, 63 - $x)          # distance from the nearest end
        $c = $null
        if ($e -lt 12) {
            # the tails sit a little lower and further back than the band, with a V cut out of the end
            if ($y -lt 4 -or $y -gt 18) { continue }
            if ($e -lt 6 -and [Math]::Abs($y - 11) -lt (6 - $e)) { continue }
            $c = $redDk
            if ($y -eq 4 -or $y -eq 18 -or $e -eq 0) { $c = Shade $redDk 0.7 }
            if ($e -ge 10) { $c = Shade $redDk 0.6 }                   # the fold where the tail goes behind the band
        }
        else {
            if ($y -lt 1 -or $y -gt 15) { continue }
            $c = $red
            if ($y -eq 2) { $c = $redLt }
            if ($y -eq 14) { $c = Shade $red 0.8 }
            if ($y -eq 1 -or $y -eq 15) { $c = $redDk }
            if (($y -eq 3 -or $y -eq 13) -and ($x % 3) -ne 0) { $c = Mix $red $stitch 0.6 }    # thin gold stitching
        }
        $bmp.SetPixel($x, $y, $c)
    }
}
Save-Bmp $bmp "banner"

# ---------------------------------------------------------------- ink flourish divider (curls at both ends, a line between)
# the left curl ('i' = ink); the right one is the same picture flipped
$curl = @(
    "....ii..",
    "..ii..i.",
    ".i....i.",
    ".i...ii.",
    "..i.....",
    "...iiii."
)
$bmp = New-Bmp 72 8
$inkLine = WithAlpha $inkSoft 220
for ($y = 0; $y -lt $curl.Length; $y++) {
    for ($x = 0; $x -lt 8; $x++) {
        if ($curl[$y][$x] -eq 'i') { $bmp.SetPixel($x + 1, $y + 1, $inkLine); $bmp.SetPixel(70 - $x, $y + 1, $inkLine) }
    }
}
for ($x = 9; $x -le 62; $x++) { $bmp.SetPixel($x, 4, $inkLine) }
Save-Bmp $bmp "divider"

# ---------------------------------------------------------------- hand-inked item slot (a box drawn on the page)
function Draw-InkBox([int]$w, [int]$h, $line, $fill, [int]$thick, [bool]$wobble) {
    $bmp = New-Bmp $w $h
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $d = [Math]::Min([Math]::Min($x, $w - 1 - $x), [Math]::Min($y, $h - 1 - $y))
            $corner = ([Math]::Min($x, $w - 1 - $x) -eq 0) -and ([Math]::Min($y, $h - 1 - $y) -eq 0)
            if ($corner) { continue }
            if ($d -lt $thick) {
                $c = $line
                if ($wobble -and $d -eq $thick - 1 -and $rng.NextDouble() -lt 0.25) { $c = WithAlpha $line ([int]($line.A * 0.55)) }   # ink a bit thinner here and there
                $bmp.SetPixel($x, $y, $c)
            }
            elseif ($fill -ne $null) {
                $c = $fill
                if ($d -eq $thick) { $c = Mix $fill (WithAlpha $ink 90) 0.35 }      # a soft shadow inside the box
                $bmp.SetPixel($x, $y, $c)
            }
        }
    }
    return $bmp
}
Save-Bmp (Draw-InkBox 24 24 (WithAlpha (Hex "#6b4b2c") 235) (WithAlpha (Hex "#e3cc9e") 200) 1 $true) "slot"
Save-Bmp (Draw-InkBox 24 24 $red $null 2 $true) "slot_selected"

# double-ruled label box for totals and notes
$bmp = Draw-InkBox 32 24 (WithAlpha (Hex "#6b4b2c") 235) (WithAlpha (Hex "#ead6ab") 230) 1 $false
for ($y = 3; $y -le 20; $y++) { $bmp.SetPixel(3, $y, (WithAlpha $inkSoft 170)); $bmp.SetPixel(28, $y, (WithAlpha $inkSoft 170)) }
for ($x = 3; $x -le 28; $x++) { $bmp.SetPixel($x, 3, (WithAlpha $inkSoft 170)); $bmp.SetPixel($x, 20, (WithAlpha $inkSoft 170)) }
Save-Bmp $bmp "label_box"

# ---------------------------------------------------------------- progress bar outline (the fill is a plain colour on top)
Save-Bmp (Draw-InkBox 24 8 (WithAlpha (Hex "#6b4b2c") 230) (WithAlpha (Hex "#d9c08e") 220) 1 $false) "bar_back"

# ---------------------------------------------------------------- portrait: a photo pasted into the book with dark photo corners
$bmp = New-Bmp 40 40
$photoEdge = Hex "#fbf4e2"
for ($y = 0; $y -lt 40; $y++) {
    for ($x = 0; $x -lt 40; $x++) {
        $d = [Math]::Min([Math]::Min($x, 39 - $x), [Math]::Min($y, 39 - $y))
        $cx = [Math]::Min($x, 39 - $x); $cy = [Math]::Min($y, 39 - $y)
        if ($d -eq 0) { $c = WithAlpha $inkSoft 200 }
        elseif ($d -le 3) { $c = $photoEdge }
        elseif ($d -eq 4) { $c = Mix $photoEdge $inkSoft 0.35 }
        else { $c = Hex "#e8d4ab"; if ($rng.NextDouble() -lt 0.08) { $c = Hex "#e1cb9e" } }
        # the photo corners: little dark paper triangles holding the picture down
        if ($cx + $cy -le 8 -and $cx -le 7 -and $cy -le 7) { $c = $ink; if ($cx + $cy -eq 8) { $c = Shade $ink 1.6 } }
        $bmp.SetPixel($x, $y, $c)
    }
}
Save-Bmp $bmp "portrait_frame"

Write-Host "Done. In Unity run Farm > Set Up > Book UI (it also runs by itself the first time these files are imported)."
