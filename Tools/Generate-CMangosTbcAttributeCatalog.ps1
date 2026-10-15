param(
    [Parameter(Mandatory = $true)] [string] $SpellDefinesPath,
    [Parameter(Mandatory = $true)] [string] $OutputPath,
    [string] $SourceCommit = "unknown"
)

$ErrorActionPreference = "Stop"
$enumToField = [ordered]@{
    SpellAttributes = "Attributes"; SpellAttributesEx = "AttributesEx"
    SpellAttributesEx2 = "AttributesEx2"; SpellAttributesEx3 = "AttributesEx3"
    SpellAttributesEx4 = "AttributesEx4"; SpellAttributesEx5 = "AttributesEx5"
    SpellAttributesEx6 = "AttributesEx6"; SpellAttributesServerside = "AttributesServerside"
}

function ConvertTo-DisplayName([string] $enumName, [string] $enumType, [int] $bit) {
    if ($enumName -eq "SPELL_ATTR_DAYTIME_ONLY") { return "Daytime Only" }
    if ($enumName -eq "SPELL_ATTR_NIGHT_ONLY") { return "Night Only" }
    if (-not $enumName) { return "Unknown ($enumType bit $bit)" }
    $name = $enumName -replace '^SPELL_ATTR_(?:EX[2-6]?_|SS_)?', ''
    return ($name -split '_' | ForEach-Object {
        if ($_ -in @('AI', 'AOE', 'PVP', 'UI', 'NPC')) { $_ }
        else { $_.Substring(0, 1).ToUpperInvariant() + $_.Substring(1).ToLowerInvariant() }
    }) -join ' '
}

function Get-Support([string] $enumName, [string] $comment, [bool] $serverOnly) {
    if (-not $enumName -or $enumName -match '_UNK\d*$') { return "unknown" }
    if ($comment -match '(?i)\bNYI\b|not implemented') { return "nyi" }
    if ($comment -match '(?i)unused|not used') { return "unused" }
    if ($serverOnly) { return "server-only" }
    return "supported"
}

function Get-Confidence([string] $enumName, [string] $comment) {
    if (-not $enumName -or $enumName -match '_UNK\d*$') { return "unknown" }
    if ($comment -match '(?i)no idea|unknown|unk\b|maybe|possibly|likely|unconfirmed|investigate') { return "low" }
    if ($comment -match '(?i)NYI|unused|not used|seems|different') { return "medium" }
    return "high"
}

$sourceLines = Get-Content -LiteralPath $SpellDefinesPath
$parsed = @{}
$currentEnum = $null
for ($index = 0; $index -lt $sourceLines.Count; $index++) {
    $line = $sourceLines[$index]
    if ($line -match '^enum (SpellAttributes(?:Ex[2-6]?)?|SpellAttributesServerside)\s*$') {
        $currentEnum = $matches[1]; $parsed[$currentEnum] = @{}; continue
    }
    if ($currentEnum -and $line -match '^};') { $currentEnum = $null; continue }
    if (-not $currentEnum) { continue }
    if ($line -match '^\s*(SPELL_ATTR_[A-Z0-9_]+)\s*=\s*(0x[0-9A-Fa-f]+)\s*,?\s*(?://\s*(.*))?$') {
        $mask = [Convert]::ToUInt32($matches[2].Substring(2), 16)
        $bit = 0
        while (((1L -shl $bit) -band $mask) -eq 0 -and $bit -lt 32) { $bit++ }
        if ($bit -ge 32 -or $mask -ne [uint32](1L -shl $bit)) {
            throw "Attribute '$($matches[1])' on line $($index + 1) is not a single 32-bit flag."
        }
        $parsed[$currentEnum][$bit] = [ordered]@{
            enum_name = $matches[1]
            comment = if ($matches[3]) { $matches[3].Trim() } else { "" }
            line = $index + 1
        }
    }
}

$fields = foreach ($pair in $enumToField.GetEnumerator()) {
    $enumType = $pair.Key; $field = $pair.Value
    if (-not $parsed.ContainsKey($enumType)) { throw "Required enum '$enumType' was not found." }
    $serverOnly = $enumType -eq 'SpellAttributesServerside'
    $entries = foreach ($bit in 0..31) {
        $definition = $parsed[$enumType][$bit]
        $enumName = if ($definition) { $definition.enum_name } else { $null }
        $comment = if ($definition) { $definition.comment } else { "No named CMaNGOS enum value for this bit." }
        [ordered]@{
            field = $field; bit_index = $bit; mask = ('0x{0:X8}' -f [uint32](1L -shl $bit))
            cmangos_enum = $enumName; display_name = ConvertTo-DisplayName $enumName $enumType $bit
            description = $comment
            evidence = [ordered]@{
                repository = "https://github.com/Sparco6/MasterWoW-Project"; commit = $SourceCommit
                path = "src/game/Spells/SpellDefines.h"; line = if ($definition) { $definition.line } else { $null }
            }
            support = Get-Support $enumName $comment $serverOnly
            confidence = Get-Confidence $enumName $comment
        }
    }
    [ordered]@{ field = $field; core_enum = $enumType; client_editable = -not $serverOnly; width_bits = 32; attributes = $entries }
}

$catalog = [ordered]@{
    schema_version = 1; wow_version = "2.4.3"; client_build = 8606; emulator = "CMangosTbc"
    generated_from = [ordered]@{
        repository = "https://github.com/Sparco6/MasterWoW-Project"; commit = $SourceCommit
        path = "src/game/Spells/SpellDefines.h"
    }
    notes = @(
        "Client fields Attributes through AttributesEx6 are edited without dropping unknown bits.",
        "AttributesServerside is catalogued for developer reference and is not serialized into the TBC client Spell.dbc.",
        "Unknown, NYI and unused bits remain visible and editable; labels do not imply runtime support."
    )
    fields = $fields
}

$directory = Split-Path -Parent $OutputPath
if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
$catalog | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $OutputPath -Encoding utf8

