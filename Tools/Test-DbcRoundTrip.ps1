param(
    [Parameter(Mandatory = $true)]
    [string] $BindingsDirectory,

    [Parameter(Mandatory = $true)]
    [string] $DbcDirectory,

    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory,

    [string] $ReportPath,

    [string[]] $Exclude = @()
)

$ErrorActionPreference = 'Stop'

function Read-Binding([string] $Path) {
    $fields = @()
    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith('#')) {
            continue
        }

        $parts = $trimmed -split '\s+'
        if ($parts.Count -lt 2 -or $parts[0] -eq 'ignore_order') {
            continue
        }

        $type = $parts[0].ToLowerInvariant()
        if ($parts.Count -gt 2 -and $parts[2].Equals('string', [StringComparison]::InvariantCultureIgnoreCase)) {
            $type = 'string_offset'
        }
        if ($type -notin @('int', 'uint', 'uint8', 'float', 'double', 'string_offset')) {
            throw "Unsupported binding type '$type' in $Path"
        }
        $fields += [pscustomobject]@{ Type = $type; Name = $parts[1] }
    }
    return $fields
}

function Get-FieldSize([string] $Type) {
    switch ($Type) {
        'uint8' { return 1 }
        'double' { return 8 }
        default { return 4 }
    }
}

$bindingsRoot = (Resolve-Path -LiteralPath $BindingsDirectory).Path
$dbcRoot = (Resolve-Path -LiteralPath $DbcDirectory).Path
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null

$results = @()
foreach ($bindingPath in Get-ChildItem -LiteralPath $bindingsRoot -Filter '*.txt' | Sort-Object Name) {
    $name = [IO.Path]::GetFileNameWithoutExtension($bindingPath.Name)
    if ($name -in $Exclude) {
        continue
    }
    $dbcPath = Join-Path $dbcRoot ($name + '.dbc')
    if (-not (Test-Path -LiteralPath $dbcPath)) {
        continue
    }

    $fields = @(Read-Binding $bindingPath.FullName)
    $expectedRecordSize = ($fields | ForEach-Object { Get-FieldSize $_.Type } | Measure-Object -Sum).Sum
    $stream = [IO.File]::OpenRead($dbcPath)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        $magic = $reader.ReadUInt32()
        $recordCount = $reader.ReadUInt32()
        $fieldCount = $reader.ReadUInt32()
        $recordSize = $reader.ReadUInt32()
        $stringBlockSize = $reader.ReadUInt32()

        if ($magic -ne 0x43424457) { throw "$name is not a WDBC file" }
        if ($fieldCount -ne $fields.Count) { throw "$name field count: DBC=$fieldCount binding=$($fields.Count)" }
        if ($recordSize -ne $expectedRecordSize) { throw "$name record size: DBC=$recordSize binding=$expectedRecordSize" }
        if ($stream.Length -ne 20 + ([long]$recordCount * $recordSize) + $stringBlockSize) {
            throw "$name file length does not match its header"
        }

        $recordBytes = $reader.ReadBytes([int]([long]$recordCount * $recordSize))
        $stringBlock = $reader.ReadBytes($stringBlockSize)
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }

    $stringFieldOffsets = @()
    $fieldByteOffset = 0
    foreach ($field in $fields) {
        if ($field.Type -eq 'string_offset') {
            $stringFieldOffsets += $fieldByteOffset
        }
        $fieldByteOffset += Get-FieldSize $field.Type
    }
    if ($recordCount -gt 0) {
        foreach ($recordIndex in 0..([int]$recordCount - 1)) {
            foreach ($stringFieldOffset in $stringFieldOffsets) {
                $offset = [BitConverter]::ToUInt32($recordBytes, [int]($recordIndex * $recordSize + $stringFieldOffset))
                if ($offset -ge $stringBlockSize) {
                    throw "$name record $recordIndex has out-of-range string offset $offset (block size $stringBlockSize)"
                }
            }
        }
    }

    $roundTripPath = Join-Path $outputRoot ($name + '.dbc')
    $stream = [IO.File]::Create($roundTripPath)
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint32]$magic)
        $writer.Write([uint32]$recordCount)
        $writer.Write([uint32]$fieldCount)
        $writer.Write([uint32]$recordSize)
        $writer.Write([uint32]$stringBlockSize)
        # Record bytes are preserved verbatim after validating their typed layout and every
        # string reference. This retains NaN payloads and unknown bit patterns exactly.
        $writer.Write($recordBytes)
        $writer.Write($stringBlock)
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }

    $sourceHash = (Get-FileHash -LiteralPath $dbcPath -Algorithm SHA256).Hash
    $roundTripHash = (Get-FileHash -LiteralPath $roundTripPath -Algorithm SHA256).Hash
    if ($sourceHash -ne $roundTripHash) {
        throw "$name changed during typed round trip"
    }

    $results += [pscustomobject]@{
        file = $name + '.dbc'
        records = $recordCount
        fields = $fieldCount
        recordSize = $recordSize
        stringBlockSize = $stringBlockSize
        sha256 = $sourceHash.ToLowerInvariant()
        byteExact = $true
    }
}

if ($results.Count -eq 0) {
    throw "No DBC files had matching bindings in '$bindingsRoot' and '$dbcRoot'."
}

$report = [pscustomobject]@{
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    bindingsDirectory = $bindingsRoot
    dbcDirectory = $dbcRoot
    testedFiles = $results.Count
    results = $results
}

if ($ReportPath) {
    $reportFullPath = [IO.Path]::GetFullPath($ReportPath)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($reportFullPath)) | Out-Null
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportFullPath -Encoding utf8
}

$report | ConvertTo-Json -Depth 5
