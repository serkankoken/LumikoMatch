param()

$ErrorActionPreference = "Stop"
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$Root = Join-Path $ProjectRoot "Assets\CharacterMatch3"

function New-Guid32 { ([guid]::NewGuid()).ToString("N") }

function Read-Guid($path) {
    $content = Get-Content -LiteralPath $path -Raw
    if ($content -match "guid:\s*([0-9a-fA-F]+)") { return $matches[1] }
    throw "Could not read guid from $path"
}

function Ensure-Dir($path) {
    if (!(Test-Path -LiteralPath $path)) {
        New-Item -ItemType Directory -Force -Path $path | Out-Null
    }
}

function Get-OrCreate-MetaGuid($assetPath, $importer, $mainObjectFileId) {
    $meta = "$assetPath.meta"
    if (Test-Path -LiteralPath $meta) {
        return Read-Guid $meta
    }

    $guid = New-Guid32
    if ($importer -eq "NativeFormatImporter") {
        @"
fileFormatVersion: 2
guid: $guid
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: $mainObjectFileId
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@ | Set-Content -LiteralPath $meta -Encoding UTF8
    } elseif ($importer -eq "PrefabImporter") {
        @"
fileFormatVersion: 2
guid: $guid
PrefabImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@ | Set-Content -LiteralPath $meta -Encoding UTF8
    } else {
        @"
fileFormatVersion: 2
guid: $guid
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@ | Set-Content -LiteralPath $meta -Encoding UTF8
    }
    return $guid
}

function Yaml-String($value) {
    if ([string]::IsNullOrEmpty($value)) { return "''" }
    return "'" + $value.Replace("'", "''") + "'"
}

function Coord($x, $y) { [pscustomobject]@{ x = [int]$x; y = [int]$y } }
function Layer($x, $y, $layers) { [pscustomobject]@{ x = [int]$x; y = [int]$y; layers = [int]$layers } }
function Placement($x, $y, $kind, $character, $orientation) { [pscustomobject]@{ x = [int]$x; y = [int]$y; kind = [int]$kind; character = [int]$character; orientation = [int]$orientation } }
function Goal($type, $character, $amount) { [pscustomobject]@{ type = [int]$type; character = [int]$character; amount = [int]$amount } }

function New-Level($number) {
    $width = if ($number -lt 31) { 7 } elseif ($number -lt 41) { 8 } else { 9 }
    $height = if ($number -lt 21) { 7 } elseif ($number -lt 36) { 8 } else { 9 }
    $active = New-Object bool[] ($width * $height)
    for ($i = 0; $i -lt $active.Length; $i++) { $active[$i] = $true }
    $hard = @(20,25,30,35,40,45,49,50) -contains $number
    $normal = @(10,15,24,34,44,47,48) -contains $number
    $level = [ordered]@{
        number = $number
        name = "Level {0:000}" -f $number
        difficulty = if ($hard) { 2 } elseif ($normal) { 1 } else { 0 }
        theme = if ($number -le 6) { "meadow" } elseif ($number -le 12) { "beach" } else { "desert" }
        width = $width
        height = $height
        active = $active
        moves = if ($hard) { 24 } elseif ($normal) { 26 } else { 30 }
        available = if ($number -ge 3) { @(0,1,2,3,4) } else { @(0,1,2,3) }
        seed = 7000 + $number * 37
        goals = New-Object System.Collections.ArrayList
        soft = New-Object System.Collections.ArrayList
        crates = New-Object System.Collections.ArrayList
        locks = New-Object System.Collections.ArrayList
        companions = New-Object System.Collections.ArrayList
        exits = New-Object System.Collections.ArrayList
        normals = New-Object System.Collections.ArrayList
        specials = New-Object System.Collections.ArrayList
        tutorial = ""
        forced = $false
        from = Coord 0 0
        to = Coord 0 0
    }
    if ($number -le 5) { $level.moves = 24 }
    return $level
}

function Index($level, $x, $y) { $y * $level.width + $x }
function Is-Inside($level, $x, $y) { $x -ge 0 -and $x -lt $level.width -and $y -ge 0 -and $y -lt $level.height }
function Is-Active($level, $x, $y) { (Is-Inside $level $x $y) -and $level.active[(Index $level $x $y)] }
function Deactivate($level, $x, $y) { if (Is-Inside $level $x $y) { $level.active[(Index $level $x $y)] = $false } }
function Add-Goal($level, $type, $character, $amount) { [void]$level.goals.Add((Goal $type $character $amount)) }
function Add-Soft($level, $x, $y, $layers) { if (Is-Active $level $x $y) { [void]$level.soft.Add((Layer $x $y $layers)) } }
function Add-Crate($level, $x, $y, $layers) { if (Is-Active $level $x $y) { [void]$level.crates.Add((Layer $x $y $layers)) } }
function Add-Lock($level, $x, $y, $layers) { if (Is-Active $level $x $y) { [void]$level.locks.Add((Layer $x $y $layers)) } }
function Add-Normal($level, $x, $y, $character) { if (Is-Active $level $x $y) { [void]$level.normals.Add((Placement $x $y 0 $character 0)) } }
function Add-Special($level, $x, $y, $kind, $character, $orientation) { if (Is-Active $level $x $y) { [void]$level.specials.Add((Placement $x $y $kind $character $orientation)) } }
function Add-Exit($level, $x, $y) {
    if (Is-Active $level $x $y) {
        foreach ($e in $level.exits) { if ($e.x -eq $x -and $e.y -eq $y) { return } }
        [void]$level.exits.Add((Coord $x $y))
    }
}
function Add-Companion($level, $sx, $sy, $ex, $ey) { if (Is-Active $level $sx $sy) { [void]$level.companions.Add((Coord $sx $sy)) }; Add-Exit $level $ex $ey }
function Add-SoftBox($level, $cx, $cy, $radius, $layers) { for ($y=$cy-$radius; $y -le $cy+$radius; $y++) { for ($x=$cx-$radius; $x -le $cx+$radius; $x++) { Add-Soft $level $x $y $layers } } }
function Add-CrateBox($level, $cx, $cy, $radius, $layers) { for ($y=$cy-$radius; $y -le $cy+$radius; $y++) { for ($x=$cx-$radius; $x -le $cx+$radius; $x++) { Add-Crate $level $x $y $layers } } }
function Sum-Layers($items) { $sum = 0; foreach ($i in $items) { $sum += [int]$i.layers }; return $sum }
function Add-Tutorial($level, $text, $fx, $fy, $tx, $ty) { $level.tutorial = $text; $level.forced = $true; $level.from = Coord $fx $fy; $level.to = Coord $tx $ty }

function Apply-Mask($level) {
    $n = $level.number
    if (@(4,8,31) -contains $n) {
        Deactivate $level 0 0; Deactivate $level ($level.width-1) 0; Deactivate $level 0 ($level.height-1); Deactivate $level ($level.width-1) ($level.height-1)
    }
    if (@(20,32,40,50) -contains $n) {
        for ($y=0; $y -lt $level.height; $y++) {
            if ($y -lt [math]::Floor($level.height/2)-1 -or $y -gt [math]::Floor($level.height/2)+1) { Deactivate $level ([math]::Floor($level.width/2)) $y }
        }
    }
    if (@(33,34) -contains $n) {
        for ($y=0; $y -lt $level.height; $y++) { for ($x=0; $x -lt $level.width; $x++) { if (($x+$y) -lt 2 -or ($x+$y) -gt ($level.width+$level.height-4)) { Deactivate $level $x $y } } }
    }
    if (@(35,47,49) -contains $n) {
        Deactivate $level 0 1; Deactivate $level 1 0; Deactivate $level ($level.width-1) ($level.height-2); Deactivate $level ($level.width-2) ($level.height-1)
    }
}

function Populate-Level($number) {
    $l = New-Level $number
    Apply-Mask $l
    $cx = [math]::Floor($l.width / 2)
    $cy = [math]::Floor($l.height / 2)

    switch ($number) {
        1 { Add-Goal $l 0 0 8; Add-Normal $l 2 3 0; Add-Normal $l 4 3 0; Add-Normal $l 3 4 0; Add-Tutorial $l "Swap two neighbors to make three matching character heads." 3 4 3 3 }
        2 { Add-Goal $l 0 0 10; Add-Goal $l 0 1 10; $l.tutorial = "Cascades count too. A single move can collect more than one goal." }
        3 { Add-Goal $l 0 2 14; Add-Normal $l 1 3 0; Add-Normal $l 2 3 0; Add-Normal $l 4 3 0; Add-Normal $l 3 4 0; Add-Tutorial $l "Match four in a row to create a Line Piece." 3 4 3 3 }
        4 { Add-Goal $l 0 2 12; Add-Goal $l 0 3 12; $l.moves = 26 }
        5 { Add-Goal $l 0 4 12; Add-Normal $l 0 3 4; Add-Normal $l 1 3 4; Add-Normal $l 3 3 4; Add-Normal $l 4 3 4; Add-Normal $l 2 4 4; Add-Tutorial $l "Match five in a straight line to create a Rainbow Piece." 2 4 2 3 }
        default {
            if ($number -le 10) {
                $radius = if ($number -le 7) { 1 } else { 2 }
                $layers = if ($number -ge 10) { 2 } else { 1 }
                Add-SoftBox $l $cx $cy $radius $layers
                if ($number -ge 8) { Add-Soft $l 1 1 1; Add-Soft $l ($l.width-2) 1 1; Add-Soft $l 1 ($l.height-2) 1; Add-Soft $l ($l.width-2) ($l.height-2) 1 }
                Add-Goal $l 1 0 (Sum-Layers $l.soft)
                if ($number -eq 7) { Add-Goal $l 0 0 12 }
                if ($number -eq 6) { $l.tutorial = "Soft Cover sits under pieces. Match or hit that cell to clear it." }
            } elseif ($number -le 15) {
                if ($number -eq 11) { Add-CrateBox $l $cx $cy 1 1; $l.tutorial = "Crates block cells. Break them by matching next to them or hitting them with specials." }
                elseif ($number -eq 12) { Add-Normal $l 2 3 1; Add-Normal $l 4 3 1; Add-Normal $l 3 2 1; Add-Normal $l 3 4 1; Add-Normal $l 3 5 1; Add-Goal $l 0 1 12; Add-Tutorial $l "T and L matches create Burst Pieces." 3 5 3 3 }
                elseif ($number -eq 13) { for ($y=1; $y -lt $l.height-1; $y++) { Add-Crate $l $cx $y 1 } }
                elseif ($number -eq 14) { Add-CrateBox $l $cx $cy 1 1; Add-Goal $l 0 3 14 }
                else { Add-CrateBox $l $cx $cy 1 2; Add-Special $l 1 3 1 0 0; Add-Special $l 5 3 2 1 0 }
                if ($l.crates.Count -gt 0) { Add-Goal $l 2 0 (Sum-Layers $l.crates) }
            } elseif ($number -le 20) {
                Add-SoftBox $l $cx $cy 2 2
                if ($number -ge 18) { Add-Crate $l ($cx-1) $cy 1; Add-Crate $l ($cx+1) $cy 1; Add-Crate $l $cx ($cy-1) 1; Add-Crate $l $cx ($cy+1) 1 }
                Add-Goal $l 1 0 (Sum-Layers $l.soft)
                if ($l.crates.Count -gt 0) { Add-Goal $l 2 0 (Sum-Layers $l.crates) }
                if ($number -eq 19) { Add-Goal $l 0 0 16 }
            } elseif ($number -le 25) {
                Add-Companion $l $cx ($l.height-1) $cx 0
                if ($number -ge 22) { Add-Crate $l ([math]::Max(1,$cx-1)) 3 1 }
                if ($number -ge 23) { Add-Companion $l 1 ($l.height-1) 1 0; Add-Exit $l ($l.width-2) 0 }
                if ($number -eq 24) { Add-Goal $l 0 2 16 }
                if ($number -ge 25) { Add-SoftBox $l $cx 2 1 1; Add-Crate $l ($cx+1) 4 1; Add-Goal $l 1 0 (Sum-Layers $l.soft); Add-Goal $l 2 0 (Sum-Layers $l.crates) }
                Add-Goal $l 3 0 $l.companions.Count
                if ($number -eq 21) { $l.tutorial = "Guide Companion Tokens to an EXIT cell at the bottom." }
            } elseif ($number -le 30) {
                $lockLayers = if ($number -ge 30) { 2 } else { 1 }
                Add-Lock $l $cx $cy $lockLayers; Add-Normal $l $cx $cy 0
                if ($number -ge 27) { Add-Lock $l ($cx-1) $cy 1; Add-Lock $l ($cx+1) $cy 1 }
                if ($number -eq 29) { Add-Companion $l 1 ($l.height-1) 1 0 }
                if ($number -ge 30) { Add-CrateBox $l ($cx+1) ($cy+1) 1 1; Add-SoftBox $l ($cx-1) ($cy-1) 1 1; Add-Goal $l 2 0 (Sum-Layers $l.crates); Add-Goal $l 1 0 (Sum-Layers $l.soft) }
                Add-Goal $l 0 0 16
                if ($l.companions.Count -gt 0) { Add-Goal $l 3 0 1 }
                if ($number -eq 26) { $l.tutorial = "Character Locks hold a piece in place until matches or specials break the lock." }
            } elseif ($number -le 35) {
                Add-Goal $l 0 1 18
                if ($number -ge 34) { Add-Soft $l 1 1 2; Add-Soft $l ($l.width-2) ($l.height-2) 2; Add-Goal $l 1 0 (Sum-Layers $l.soft) }
                if ($number -ge 35) { Add-CrateBox $l $cx $cy 1 1; Add-Goal $l 2 0 (Sum-Layers $l.crates) }
            } elseif ($number -le 40) {
                if ($number -eq 36) { Add-Special $l $cx $cy 1 0 0; Add-Special $l ($cx+1) $cy 1 1 1; Add-Tutorial $l "Swap two Line Pieces to clear a row and a column." $cx $cy ($cx+1) $cy }
                elseif ($number -eq 37) { Add-Special $l $cx $cy 1 0 0; Add-Special $l ($cx+1) $cy 2 1 0; Add-Tutorial $l "Line plus Burst clears a wide cross." $cx $cy ($cx+1) $cy }
                elseif ($number -eq 38) { Add-Special $l $cx $cy 2 0 0; Add-Special $l ($cx+1) $cy 2 1 0 }
                elseif ($number -eq 39) { Add-Special $l $cx $cy 3 0 0; Add-Normal $l ($cx+1) $cy 2; Add-Tutorial $l "Rainbow plus a character removes every piece of that character." $cx $cy ($cx+1) $cy }
                else { Add-Special $l $cx $cy 3 0 0; Add-Special $l ($cx+1) $cy 1 2 1; Add-CrateBox $l $cx ($cy-2) 1 1; Add-Goal $l 2 0 (Sum-Layers $l.crates) }
                Add-Goal $l 0 2 18
            } elseif ($number -le 45) {
                Add-SoftBox $l $cx 3 1 1; Add-Crate $l 2 4 1; Add-Crate $l ($l.width-3) 4 1
                if ($number -eq 41) { Add-Goal $l 0 0 18; Add-Goal $l 0 3 18; Add-Goal $l 2 0 (Sum-Layers $l.crates) }
                elseif ($number -eq 42) { Add-Companion $l 1 ($l.height-1) 1 0; Add-Goal $l 3 0 1; Add-Goal $l 1 0 (Sum-Layers $l.soft) }
                elseif ($number -eq 43) { Add-Lock $l 3 5 1; Add-Normal $l 3 5 4; Add-Goal $l 0 4 18; Add-Goal $l 2 0 (Sum-Layers $l.crates) }
                elseif ($number -eq 44) { Add-Goal $l 4 0 9000; Add-Goal $l 1 0 (Sum-Layers $l.soft) }
                else { Add-Companion $l 1 ($l.height-1) 1 0; Add-Lock $l 3 5 1; Add-Normal $l 3 5 4; Add-Goal $l 0 0 18; Add-Goal $l 3 0 1; Add-Goal $l 2 0 (Sum-Layers $l.crates) }
            } else {
                if ($number -eq 46) { $l.difficulty = 0; $l.moves = 30; Add-Special $l 2 4 1 0 0; Add-Special $l 5 4 2 1 0; Add-Goal $l 0 0 18 }
                else {
                    $finalLayers = if ($number -ge 49) { 2 } else { 1 }
                    Add-SoftBox $l $cx $cy 2 $finalLayers; Add-CrateBox $l $cx $cy 1 $finalLayers
                    Add-Lock $l 2 ($l.height-3) 1; Add-Normal $l 2 ($l.height-3) 3
                    Add-Companion $l 1 ($l.height-1) 1 0
                    if ($number -ge 48) { Add-Companion $l ($l.width-2) ($l.height-1) ($l.width-2) 0 }
                    if ($number -eq 50) { $l.name = "Level 050: Lantern Finale"; Add-Special $l ($cx-1) ($cy+2) 3 0 0; Add-Special $l $cx ($cy+2) 2 4 0; $l.moves = 28; $l.tutorial = "Finale: use the familiar tools together. No new rules, just sharper choices." }
                    Add-Goal $l 1 0 (Sum-Layers $l.soft); Add-Goal $l 2 0 (Sum-Layers $l.crates); Add-Goal $l 3 0 $l.companions.Count
                }
            }
        }
    }

    $activeCount = 0; foreach ($a in $l.active) { if ($a) { $activeCount++ } }
    $l.oneStar = [int][math]::Round($activeCount * 55 + $number * 220)
    $l.twoStar = [int][math]::Round($l.oneStar * 1.8)
    $l.threeStar = [int][math]::Round($l.oneStar * 2.75)
    return $l
}

function Emit-IntList($name, $items) {
    if ($items.Count -eq 0) { return "  ${name}: []`n" }
    $s = "  ${name}:`n"
    foreach ($i in $items) { $s += "  - $i`n" }
    return $s
}

function Emit-BoolList($name, $items) {
    $s = "  ${name}:`n"
    foreach ($i in $items) { $s += "  - " + ($(if ($i) { "1" } else { "0" })) + "`n" }
    return $s
}

function Emit-Goals($items) {
    if ($items.Count -eq 0) { return "  goals: []`n" }
    $s = "  goals:`n"
    foreach ($g in $items) { $s += "  - goalType: $($g.type)`n    characterType: $($g.character)`n    amount: $($g.amount)`n" }
    return $s
}

function Emit-Layers($name, $items) {
    if ($items.Count -eq 0) { return "  ${name}: []`n" }
    $s = "  ${name}:`n"
    foreach ($p in $items) { $s += "  - coordinate: {x: $($p.x), y: $($p.y)}`n    layers: $($p.layers)`n" }
    return $s
}

function Emit-Coords($name, $items) {
    if ($items.Count -eq 0) { return "  ${name}: []`n" }
    $s = "  ${name}:`n"
    foreach ($p in $items) { $s += "  - {x: $($p.x), y: $($p.y)}`n" }
    return $s
}

function Emit-Placements($name, $items) {
    if ($items.Count -eq 0) { return "  ${name}: []`n" }
    $s = "  ${name}:`n"
    foreach ($p in $items) { $s += "  - coordinate: {x: $($p.x), y: $($p.y)}`n    kind: $($p.kind)`n    character: $($p.character)`n    lineOrientation: $($p.orientation)`n" }
    return $s
}

function Write-LevelAsset($level, $scriptGuid, $path) {
    $name = "Level_{0:000}" -f $level.number
    $yaml = @"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $scriptGuid, type: 3}
  m_Name: $name
  m_EditorClassIdentifier: 
  levelNumber: $($level.number)
  displayName: $(Yaml-String $level.name)
  difficultyLabel: $($level.difficulty)
  backgroundThemeId: $(Yaml-String $level.theme)
  boardWidth: $($level.width)
  boardHeight: $($level.height)
"@
    $yaml += "`n"
    $yaml += Emit-BoolList "activeCells" $level.active
    $yaml += "  moveLimit: $($level.moves)`n"
    $yaml += Emit-IntList "availableCharacterTypes" $level.available
    $yaml += "  randomSeed: $($level.seed)`n  reshufflingAllowed: 1`n  maximumAutomaticReshuffleAttempts: 10`n"
    $yaml += Emit-Goals $level.goals
    $yaml += "  oneStarScore: $($level.oneStar)`n  twoStarScore: $($level.twoStar)`n  threeStarScore: $($level.threeStar)`n"
    $yaml += Emit-Layers "softCoverPlacements" $level.soft
    $yaml += Emit-Layers "cratePlacements" $level.crates
    $yaml += Emit-Layers "lockPlacements" $level.locks
    $yaml += Emit-Coords "companionTokenStartingPositions" $level.companions
    $yaml += Emit-Coords "companionExitCells" $level.exits
    $yaml += Emit-Placements "prePlacedNormalPieces" $level.normals
    $yaml += Emit-Placements "prePlacedSpecialPieces" $level.specials
    $yaml += "  tutorialInstructions: $(Yaml-String $level.tutorial)`n"
    $yaml += "  tutorialForcedSwap:`n    enabled: " + ($(if ($level.forced) { "1" } else { "0" })) + "`n    from: {x: $($level.from.x), y: $($level.from.y)}`n    to: {x: $($level.to.x), y: $($level.to.y)}`n"
    $yaml | Set-Content -LiteralPath $path -Encoding UTF8
}

Ensure-Dir (Join-Path $Root "Data")
Ensure-Dir (Join-Path $Root "Levels")
Ensure-Dir (Join-Path $Root "Scenes")
Ensure-Dir (Join-Path $Root "Materials")
Ensure-Dir (Join-Path $Root "Prefabs")

$levelScriptGuid = Read-Guid (Join-Path $Root "Scripts\Data\LevelDefinition.cs.meta")
$catalogScriptGuid = Read-Guid (Join-Path $Root "Scripts\Data\CharacterCatalog.cs.meta")
$libraryScriptGuid = Read-Guid (Join-Path $Root "Scripts\Data\LevelLibrary.cs.meta")
$scoringScriptGuid = Read-Guid (Join-Path $Root "Scripts\Data\ScoringConfig.cs.meta")
$bootstrapScriptGuid = Read-Guid (Join-Path $Root "Scripts\Core\GameBootstrap.cs.meta")
$sessionScriptGuid = Read-Guid (Join-Path $Root "Scripts\Core\GameSession.cs.meta")
$mapScriptGuid = Read-Guid (Join-Path $Root "Scripts\UI\LevelMapUI.cs.meta")
$cellViewGuid = Read-Guid (Join-Path $Root "Scripts\Board\BoardCellView.cs.meta")

$spriteGuids = @{
    Cat = Read-Guid (Join-Path $ProjectRoot "Assets\Char\Cat.png.meta")
    Bunny = Read-Guid (Join-Path $ProjectRoot "Assets\Char\Bunny.png.meta")
    Dino = Read-Guid (Join-Path $ProjectRoot "Assets\Char\Dino.png.meta")
    Penguin = Read-Guid (Join-Path $ProjectRoot "Assets\Char\Penguin.png.meta")
    Bear = Read-Guid (Join-Path $ProjectRoot "Assets\Char\Bear.png.meta")
}

$catalogPath = Join-Path $Root "Data\CharacterCatalog.asset"
$catalogGuid = Get-OrCreate-MetaGuid $catalogPath "NativeFormatImporter" 11400000
@"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $catalogScriptGuid, type: 3}
  m_Name: CharacterCatalog
  m_EditorClassIdentifier: 
  entries:
  - characterType: 0
    sprite: {fileID: 21300000, guid: $($spriteGuids.Cat), type: 3}
    fallbackColor: {r: 1, g: 0.54, b: 0.48, a: 1}
  - characterType: 1
    sprite: {fileID: 21300000, guid: $($spriteGuids.Bunny), type: 3}
    fallbackColor: {r: 0.66, g: 0.78, b: 1, a: 1}
  - characterType: 2
    sprite: {fileID: 21300000, guid: $($spriteGuids.Dino), type: 3}
    fallbackColor: {r: 0.54, g: 0.88, b: 0.5, a: 1}
  - characterType: 3
    sprite: {fileID: 21300000, guid: $($spriteGuids.Penguin), type: 3}
    fallbackColor: {r: 0.52, g: 0.9, b: 1, a: 1}
  - characterType: 4
    sprite: {fileID: 21300000, guid: $($spriteGuids.Bear), type: 3}
    fallbackColor: {r: 1, g: 0.78, b: 0.42, a: 1}
"@ | Set-Content -LiteralPath $catalogPath -Encoding UTF8

$scoringPath = Join-Path $Root "Data\ScoringConfig.asset"
$scoringGuid = Get-OrCreate-MetaGuid $scoringPath "NativeFormatImporter" 11400000
@"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $scoringScriptGuid, type: 3}
  m_Name: ScoringConfig
  m_EditorClassIdentifier: 
  normalPieceRemoved: 60
  fourMatchCreationBonus: 120
  fiveMatchCreationBonus: 300
  tOrLMatchBonus: 240
  softCoverLayerRemoved: 80
  crateLayerRemoved: 100
  characterLockRemoved: 100
  companionTokenDelivered: 1000
  remainingMoveBonus: 500
  cascadeMultiplierStep: 0.35
"@ | Set-Content -LiteralPath $scoringPath -Encoding UTF8

$levelRefs = New-Object System.Collections.ArrayList
for ($n = 1; $n -le 50; $n++) {
    $path = Join-Path $Root ("Levels\Level_{0:000}.asset" -f $n)
    $guid = Get-OrCreate-MetaGuid $path "NativeFormatImporter" 11400000
    $level = Populate-Level $n
    Write-LevelAsset $level $levelScriptGuid $path
    [void]$levelRefs.Add([pscustomobject]@{ guid = $guid; number = $n })
}

$libraryPath = Join-Path $Root "Data\LevelLibrary.asset"
$libraryGuid = Get-OrCreate-MetaGuid $libraryPath "NativeFormatImporter" 11400000
$libraryYaml = @"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $libraryScriptGuid, type: 3}
  m_Name: LevelLibrary
  m_EditorClassIdentifier: 
  levels:
"@
$libraryYaml += "`n"
foreach ($ref in $levelRefs) { $libraryYaml += "  - {fileID: 11400000, guid: $($ref.guid), type: 2}`n" }
$libraryYaml | Set-Content -LiteralPath $libraryPath -Encoding UTF8

function Write-Scene($path, $sceneName, $scriptGuid, $extraFields) {
    $guid = Get-OrCreate-MetaGuid $path "DefaultImporter" 0
    @"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &100000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 100001}
  - component: {fileID: 100002}
  m_Layer: 0
  m_Name: $sceneName
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &100001
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 100000}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!114 &100002
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 100000}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $scriptGuid, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
$extraFields
--- !u!1 &200000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 200001}
  - component: {fileID: 200002}
  - component: {fileID: 200003}
  m_Layer: 0
  m_Name: Main Camera
  m_TagString: MainCamera
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &200001
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 200000}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: -10}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!20 &200002
Camera:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 200000}
  m_Enabled: 1
  serializedVersion: 2
  m_ClearFlags: 2
  m_BackGroundColor: {r: 0.1, g: 0.19, b: 0.24, a: 1}
  m_projectionMatrixMode: 1
  m_GateFitMode: 2
  m_FOVAxisMode: 0
  m_Iso: 200
  m_ShutterSpeed: 0.005
  m_Aperture: 16
  m_FocusDistance: 10
  m_FocalLength: 50
  m_BladeCount: 5
  m_Curvature: {x: 2, y: 11}
  m_BarrelClipping: 0.25
  m_Anamorphism: 0
  m_SensorSize: {x: 36, y: 24}
  m_LensShift: {x: 0, y: 0}
  m_NormalizedViewPortRect:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
  near clip plane: 0.3
  far clip plane: 1000
  field of view: 60
  orthographic: 1
  orthographic size: 9.6
  m_Depth: -1
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingPath: -1
  m_TargetTexture: {fileID: 0}
  m_TargetDisplay: 0
  m_TargetEye: 3
  m_HDR: 1
  m_AllowMSAA: 1
  m_AllowDynamicResolution: 0
  m_ForceIntoRT: 0
  m_OcclusionCulling: 1
  m_StereoConvergence: 10
  m_StereoSeparation: 0.022
--- !u!81 &200003
AudioListener:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 200000}
  m_Enabled: 1
--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
  - {fileID: 100001}
  - {fileID: 200001}
"@ | Set-Content -LiteralPath $path -Encoding UTF8
    return $guid
}

$bootScene = Join-Path $Root "Scenes\Boot.unity"
$mapScene = Join-Path $Root "Scenes\LevelMap.unity"
$gameplayScene = Join-Path $Root "Scenes\Gameplay.unity"
$bootGuid = Write-Scene $bootScene "GameBootstrap" $bootstrapScriptGuid "  levelMapSceneName: LevelMap"
$mapFields = "  levelLibrary: {fileID: 11400000, guid: $libraryGuid, type: 2}`n  characterCatalog: {fileID: 11400000, guid: $catalogGuid, type: 2}"
$mapGuid = Write-Scene $mapScene "LevelMap" $mapScriptGuid $mapFields
$sessionFields = "  characterCatalog: {fileID: 11400000, guid: $catalogGuid, type: 2}`n  levelLibrary: {fileID: 11400000, guid: $libraryGuid, type: 2}`n  scoringConfig: {fileID: 11400000, guid: $scoringGuid, type: 2}"
$gameplayGuid = Write-Scene $gameplayScene "GameSession" $sessionScriptGuid $sessionFields

function Write-Material($name, $r, $g, $b, $a) {
    $path = Join-Path $Root "Materials\$name.mat"
    [void](Get-OrCreate-MetaGuid $path "NativeFormatImporter" 2100000)
@"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 6
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: $name
  m_Shader: {fileID: 7, guid: 0000000000000000f000000000000000, type: 0}
  m_ShaderKeywords: 
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats: []
    m_Colors:
    - _Color: {r: $r, g: $g, b: $b, a: $a}
"@ | Set-Content -LiteralPath $path -Encoding UTF8
}
Write-Material "BoardCell" 1 1 1 0.35
Write-Material "SoftCover" 0.55 0.9 0.95 0.65
Write-Material "Crate" 0.45 0.24 0.12 1
Write-Material "RainbowOverlay" 1 0.85 0.25 1

function Write-Prefab($path, $name, $scriptGuid) {
    [void](Get-OrCreate-MetaGuid $path "PrefabImporter" 100100000)
    $componentList = if ($scriptGuid) { "  - component: {fileID: 100001}`n  - component: {fileID: 100002}" } else { "  - component: {fileID: 100001}" }
    $mono = ""
    if ($scriptGuid) {
        $mono = @"
--- !u!114 &100002
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 100000}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $scriptGuid, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
"@
    }
@"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &100000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
$componentList
  m_Layer: 5
  m_Name: $name
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &100001
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 100000}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0.5, y: 0.5}
  m_AnchorMax: {x: 0.5, y: 0.5}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 100, y: 100}
  m_Pivot: {x: 0.5, y: 0.5}
$mono
"@ | Set-Content -LiteralPath $path -Encoding UTF8
}
Write-Prefab (Join-Path $Root "Prefabs\BoardCell.prefab") "BoardCell" $cellViewGuid
Write-Prefab (Join-Path $Root "Prefabs\PieceView.prefab") "PieceView" $null
Write-Prefab (Join-Path $Root "Prefabs\FloatingText.prefab") "FloatingText" $null

@"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1045 &1
EditorBuildSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Scenes:
  - enabled: 1
    path: Assets/CharacterMatch3/Scenes/Boot.unity
    guid: $bootGuid
  - enabled: 1
    path: Assets/CharacterMatch3/Scenes/LevelMap.unity
    guid: $mapGuid
  - enabled: 1
    path: Assets/CharacterMatch3/Scenes/Gameplay.unity
    guid: $gameplayGuid
  m_configObjects: {}
  m_UseUCBPForAssetBundles: 0
"@ | Set-Content -LiteralPath (Join-Path $ProjectRoot "ProjectSettings\EditorBuildSettings.asset") -Encoding UTF8

Write-Host "Generated Character Match-3 fallback assets, levels, scenes, prefabs, and build settings."
