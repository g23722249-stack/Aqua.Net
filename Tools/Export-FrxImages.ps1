<#
.SYNOPSIS
  Extracts every image stored in the .frx files of a VB6 project's forms, named after the
  form / control / property that uses it, plus a manifest.csv for porting.

.DESCRIPTION
  VB6 keeps binary property values (Picture, Icon, the Aqua controls' *Image / MenuIcon_i /
  MarkImage ...) in <form>.frx and refers to them from the .frm as  Prop = "x.frx":OFFSET.
  A picture blob at OFFSET is:
      DWORD blobLength
      [16-byte StdPicture CLSID 0BE35204-8F91-11CE-9DE3-00AA004BB851]   (usually present)
      "lt\0\0"
      DWORD dataLength
      <dataLength bytes: the original file -- BMP, ICO, CUR, GIF, JPEG, WMF, EMF or PNG>
  Blobs that are not pictures (List/ItemData arrays, long Text ...) are listed in the manifest
  as "skipped" and not written.

  Output: <OutDir>\<Form>\<Control>[_<Index>]_<Property>.<ext>   (the form itself uses its own name)
          <OutDir>\manifest.csv  (Form, ControlType, Control, Index, Property, Frx, Offset, Status,
                                  Format, Width, Height, Bytes, Sha1, File)
  Identical images share the same Sha1, so duplicates are easy to fold into one resource.

  -TransparentColor RRGGBB additionally writes, for every BMP/GIF, a PNG next to it
  (<same name>.png) in which that colour is fully transparent -- VB6 skins commonly used magenta
  (FF00FF) as the "see-through" colour, which .NET draws literally unless it is made transparent.
  Only images that actually contain the colour get a PNG; the manifest's PngFile column lists them.

.EXAMPLE
  .\Export-FrxImages.ps1 -Vbp "D:\專案\電子相簿\iPhoto\iPhoto.vbp"
  .\Export-FrxImages.ps1 -Vbp ...\iPhoto.vbp -OutDir C:\temp\iphoto_images -TransparentColor FF00FF
#>
param(
    [Parameter(Mandatory = $true)][string]$Vbp,
    [string]$OutDir,
    [ValidatePattern('^#?[0-9A-Fa-f]{6}$')][string]$TransparentColor
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$vbpPath = (Resolve-Path -LiteralPath $Vbp).Path
$vbpDir = Split-Path $vbpPath -Parent
if (-not $OutDir) { $OutDir = Join-Path $vbpDir 'frx_export' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$ansi = [Text.Encoding]::Default           # .frm files are in the system ANSI code page (Big5 here)
$picGuid = [byte[]](0x04,0x52,0xE3,0x0B,0x91,0x8F,0xCE,0x11,0x9D,0xE3,0x00,0xAA,0x00,0x4B,0xB8,0x51)
$sha1 = [Security.Cryptography.SHA1]::Create()

function Test-Bytes([byte[]]$buf, [int]$at, [byte[]]$pattern) {
    if ($at + $pattern.Length -gt $buf.Length) { return $false }
    for ($i = 0; $i -lt $pattern.Length; $i++) { if ($buf[$at + $i] -ne $pattern[$i]) { return $false } }
    return $true
}

# Returns the embedded image bytes, or $null when the blob isn't a picture.
function Get-PictureBytes([byte[]]$frx, [int]$off) {
    if ($off + 12 -gt $frx.Length) { return $null }
    $p = $off + 4                                                   # skip blobLength
    if (Test-Bytes $frx $p $picGuid) { $p += 16 }                   # optional CLSID
    if (-not (Test-Bytes $frx $p ([byte[]](0x6C,0x74,0,0)))) { return $null }   # "lt\0\0"
    $len = [BitConverter]::ToInt32($frx, $p + 4)
    $p += 8
    if ($len -le 0 -or $p + $len -gt $frx.Length) { return ,([byte[]]@()) }  # empty picture
    $out = New-Object byte[] $len
    [Array]::Copy($frx, $p, $out, 0, $len)
    return ,$out
}

function Get-Format([byte[]]$d) {
    if ($d.Length -lt 4) { return 'bin' }
    if ($d[0] -eq 0x42 -and $d[1] -eq 0x4D) { return 'bmp' }
    if ($d[0] -eq 0 -and $d[1] -eq 0 -and $d[2] -eq 1 -and $d[3] -eq 0) { return 'ico' }
    if ($d[0] -eq 0 -and $d[1] -eq 0 -and $d[2] -eq 2 -and $d[3] -eq 0) { return 'cur' }
    if ($d[0] -eq 0x47 -and $d[1] -eq 0x49 -and $d[2] -eq 0x46) { return 'gif' }
    if ($d[0] -eq 0xFF -and $d[1] -eq 0xD8) { return 'jpg' }
    if ($d[0] -eq 0x89 -and $d[1] -eq 0x50 -and $d[2] -eq 0x4E -and $d[3] -eq 0x47) { return 'png' }
    if ($d[0] -eq 0xD7 -and $d[1] -eq 0xCD -and $d[2] -eq 0xC6 -and $d[3] -eq 0x9A) { return 'wmf' }
    if ($d.Length -ge 44 -and $d[40] -eq 0x20 -and $d[41] -eq 0x45 -and $d[42] -eq 0x4D -and $d[43] -eq 0x46) { return 'emf' }
    return 'bin'
}

function Get-Dimensions([byte[]]$d, [string]$fmt) {
    try {
        if ($fmt -eq 'ico' -or $fmt -eq 'cur') {
            $w = [int]$d[6]; $h = [int]$d[7]; if ($w -eq 0) { $w = 256 }; if ($h -eq 0) { $h = 256 }
            return @($w, $h)
        }
        $ms = New-Object IO.MemoryStream(, $d)
        $img = [Drawing.Image]::FromStream($ms)
        $r = @($img.Width, $img.Height); $img.Dispose(); $ms.Dispose(); return $r
    } catch { return @('', '') }
}

function Get-SafeName([string]$s) { return ($s -replace '[\\/:*?"<>|]', '_') }

$keyColor = $null
if ($TransparentColor) {
    if (-not ('FrxExport.Pixels' -as [type])) {
        # per-pixel loop in C#: a PowerShell loop over a full-size background takes far too long
        Add-Type -TypeDefinition @'
namespace FrxExport {
    public static class Pixels {
        // buf is 32bpp BGRA; every pixel equal to (r,g,b) becomes fully transparent. Returns the count.
        public static int ClearKey(byte[] buf, byte r, byte g, byte b) {
            int hits = 0;
            for (int i = 0; i + 3 < buf.Length; i += 4) {
                if (buf[i] == b && buf[i + 1] == g && buf[i + 2] == r) {
                    buf[i] = 0; buf[i + 1] = 0; buf[i + 2] = 0; buf[i + 3] = 0; hits++;
                }
            }
            return hits;
        }
    }
}
'@
    }
    $hex = $TransparentColor.TrimStart('#')
    $keyColor = [Drawing.Color]::FromArgb(255, [Convert]::ToInt32($hex.Substring(0, 2), 16),
                                          [Convert]::ToInt32($hex.Substring(2, 2), 16), [Convert]::ToInt32($hex.Substring(4, 2), 16))
}

# Writes <file>.png with $keyColor made transparent; returns the png path, or '' when the image has
# no pixel of that colour (nothing to make transparent) or can't be decoded.
function Write-TransparentPng([byte[]]$d, [string]$file) {
    $ms = New-Object IO.MemoryStream(, $d)
    try {
        $src = [Drawing.Image]::FromStream($ms)
        $bmp = New-Object Drawing.Bitmap($src.Width, $src.Height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [Drawing.Graphics]::FromImage($bmp); $g.DrawImage($src, 0, 0, $src.Width, $src.Height); $g.Dispose(); $src.Dispose()
        $rect = New-Object Drawing.Rectangle(0, 0, $bmp.Width, $bmp.Height)
        $bits = $bmp.LockBits($rect, [Drawing.Imaging.ImageLockMode]::ReadWrite, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $n = [Math]::Abs($bits.Stride) * $bmp.Height
        $buf = New-Object byte[] $n
        [Runtime.InteropServices.Marshal]::Copy($bits.Scan0, $buf, 0, $n)
        $hits = [FrxExport.Pixels]::ClearKey($buf, $keyColor.R, $keyColor.G, $keyColor.B)
        [Runtime.InteropServices.Marshal]::Copy($buf, 0, $bits.Scan0, $n)
        $bmp.UnlockBits($bits)
        if ($hits -eq 0) { $bmp.Dispose(); return '' }
        $png = [IO.Path]::ChangeExtension($file, '.png')
        $bmp.Save($png, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
        return $png
    } catch { return '' } finally { $ms.Dispose() }
}

$rows = New-Object System.Collections.Generic.List[object]
$forms = Get-Content -LiteralPath $vbpPath -Encoding Default | Where-Object { $_ -match '^Form=' } |
    ForEach-Object { Join-Path $vbpDir ($_.Substring(5).Trim()) }

foreach ($frmPath in $forms) {
    if (-not (Test-Path -LiteralPath $frmPath)) { Write-Warning "missing form: $frmPath"; continue }
    $frmDir = Split-Path $frmPath -Parent
    $lines = [IO.File]::ReadAllLines($frmPath, $ansi)
    $formName = [IO.Path]::GetFileNameWithoutExtension($frmPath)
    foreach ($l in $lines) { if ($l -match '^Attribute VB_Name = "(\w+)"') { $formName = $matches[1]; break } }
    $frxCache = @{}

    # Walk the designer section; each Begin..End block collects its props and is emitted on End,
    # so an Index line anywhere in the block is known before files are named.
    $stack = New-Object System.Collections.Stack
    foreach ($l in $lines) {
        if ($l -match '^Attribute VB_Name') { break }
        if ($l -match '^\s*Begin\s+(\S+)\s+(\S+)') {
            $stack.Push(@{ Type = $matches[1]; Name = $matches[2]; Index = ''; Props = New-Object System.Collections.Generic.List[object] })
            continue
        }
        if ($l -match '^\s*BeginProperty\b') { $stack.Push($null); continue }   # Font etc.
        if ($l -match '^\s*EndProperty\b') { [void]$stack.Pop(); continue }
        if ($l -match '^\s*End\s*$') {
            if ($stack.Count -eq 0) { continue }
            $blk = $stack.Pop()
            if ($null -eq $blk) { continue }
            foreach ($pr in $blk.Props) {
                $frxFile = Join-Path $frmDir $pr.Frx
                if (-not $frxCache.ContainsKey($frxFile)) {
                    $frxCache[$frxFile] = if (Test-Path -LiteralPath $frxFile) { [IO.File]::ReadAllBytes($frxFile) } else { $null }
                }
                $frx = $frxCache[$frxFile]
                $row = [ordered]@{ Form = $formName; ControlType = $blk.Type; Control = $blk.Name; Index = $blk.Index
                                   Property = $pr.Prop; Frx = $pr.Frx; Offset = ('{0:X}' -f $pr.Offset)
                                   Status = ''; Format = ''; Width = ''; Height = ''; Bytes = ''; Sha1 = ''; File = ''; PngFile = '' }
                if ($null -eq $frx) { $row.Status = 'frx missing'; $rows.Add([pscustomobject]$row); continue }
                $data = Get-PictureBytes $frx $pr.Offset
                if ($null -eq $data) { $row.Status = 'skipped (not a picture)'; $rows.Add([pscustomobject]$row); continue }
                if ($data.Length -eq 0) { $row.Status = 'empty picture'; $rows.Add([pscustomobject]$row); continue }
                $fmt = Get-Format $data
                $dim = Get-Dimensions $data $fmt
                $ctl = if ($blk.Type -eq 'VB.Form') { $formName } else { $blk.Name + $(if ($blk.Index -ne '') { '_' + $blk.Index } else { '' }) }
                $dir = Join-Path $OutDir (Get-SafeName $formName)
                New-Item -ItemType Directory -Force -Path $dir | Out-Null
                $file = Join-Path $dir ((Get-SafeName "$ctl`_$($pr.Prop)") + ".$fmt")
                [IO.File]::WriteAllBytes($file, $data)
                $row.Status = 'ok'; $row.Format = $fmt; $row.Width = $dim[0]; $row.Height = $dim[1]; $row.Bytes = $data.Length
                $row.Sha1 = ([BitConverter]::ToString($sha1.ComputeHash($data)) -replace '-', '').ToLower()
                $row.File = $file.Substring($OutDir.Length).TrimStart('\')
                if ($null -ne $keyColor -and ($fmt -eq 'bmp' -or $fmt -eq 'gif')) {
                    $png = Write-TransparentPng $data $file
                    if ($png) { $row.PngFile = $png.Substring($OutDir.Length).TrimStart('\') }
                }
                $rows.Add([pscustomobject]$row)
            }
            continue
        }
        if ($stack.Count -gt 0 -and $null -ne $stack.Peek()) {
            $blk = $stack.Peek()
            if ($l -match '^\s*Index\s*=\s*(\d+)') { $blk.Index = $matches[1] }
            elseif ($l -match '^\s*(\w+)\s*=\s*"([^"]+\.frx)":([0-9A-Fa-f]+)') {
                $blk.Props.Add(@{ Prop = $matches[1]; Frx = $matches[2]; Offset = [Convert]::ToInt32($matches[3], 16) })
            }
        }
    }
}

$manifest = Join-Path $OutDir 'manifest.csv'
$rows | Export-Csv -LiteralPath $manifest -NoTypeInformation -Encoding UTF8
$ok = @($rows | Where-Object Status -eq 'ok')
"Forms scanned : $($forms.Count)"
"frx references: $($rows.Count)"
"Images written: $($ok.Count)  (unique by content: $(@($ok | Select-Object -ExpandProperty Sha1 -Unique).Count))"
if ($null -ne $keyColor) { "Transparent PNG: $(@($ok | Where-Object PngFile).Count)  (colour #$($TransparentColor.TrimStart('#').ToUpper()))" }
$rows | Where-Object Status -ne 'ok' | Group-Object Status | ForEach-Object { "Not written   : $($_.Count) $($_.Name)" }
"Output        : $OutDir"
"Manifest      : $manifest"
