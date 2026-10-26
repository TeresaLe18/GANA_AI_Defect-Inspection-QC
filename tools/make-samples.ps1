# Generates synthetic sample images for the R6 test scenarios (good / minor / major / blurry / corrupt).
# Synthetic drawings are only for pipeline testing; use real product photos for a meaningful Gemini evaluation.
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot "..\samples"
New-Item -ItemType Directory -Force $out | Out-Null

function New-Bottle([string]$variant) {
    $bmp = New-Object System.Drawing.Bitmap 800, 800
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::FromArgb(235, 238, 242))
    $body = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.Rectangle]::new(250, 150, 300, 560)), ([System.Drawing.Color]::FromArgb(40, 90, 160)), ([System.Drawing.Color]::FromArgb(120, 170, 230)), 0
    $g.FillRectangle($body, 250, 200, 300, 500)
    $g.FillEllipse($body, 250, 650, 300, 100)
    $g.FillRectangle([System.Drawing.Brushes]::DimGray, 330, 110, 140, 90)
    $g.DrawString("THERMO 500", (New-Object System.Drawing.Font "Arial", 22, ([System.Drawing.FontStyle]::Bold)), [System.Drawing.Brushes]::White, 300, 420)
    if ($variant -eq 'minor') {
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(220, 230, 230, 230)), 2
        $g.DrawLine($pen, 300, 300, 380, 340); $g.DrawLine($pen, 310, 296, 392, 338); $g.DrawLine($pen, 296, 312, 372, 350)
    }
    if ($variant -eq 'major') {
        $dark = [System.Drawing.Brushes]::Black
        $pts = [System.Drawing.Point[]]@([System.Drawing.Point]::new(520, 250), [System.Drawing.Point]::new(430, 330), [System.Drawing.Point]::new(470, 380), [System.Drawing.Point]::new(380, 520), [System.Drawing.Point]::new(420, 560))
        $g.DrawLines((New-Object System.Drawing.Pen ($dark), 6), $pts)
        $g.FillEllipse([System.Drawing.Brushes]::DarkSlateGray, 250, 540, 120, 90)   # dent
        $g.FillRectangle([System.Drawing.Brushes]::Chocolate, 460, 120, 70, 70)      # broken cap fragment
    }
    $g.Dispose(); return $bmp
}

foreach ($v in 'good', 'minor', 'major') {
    $b = New-Bottle $v; $b.Save((Join-Path $out "sample_$v.png"), [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
}

# Blurry: downscale to 8x8 then stretch back.
$src = New-Bottle 'good'
$tiny = New-Object System.Drawing.Bitmap 8, 8
$g = [System.Drawing.Graphics]::FromImage($tiny); $g.DrawImage($src, 0, 0, 8, 8); $g.Dispose()
$blur = New-Object System.Drawing.Bitmap 800, 800
$g = [System.Drawing.Graphics]::FromImage($blur); $g.InterpolationMode = 'HighQualityBicubic'; $g.DrawImage($tiny, 0, 0, 800, 800); $g.Dispose()
$blur.Save((Join-Path $out "sample_blurry.png"), [System.Drawing.Imaging.ImageFormat]::Png)

# Faulty: a text file renamed to .jpg (not a real image).
Set-Content -Path (Join-Path $out "sample_faulty.jpg") -Value "this is not an image"
Write-Host "Samples written to $out"
