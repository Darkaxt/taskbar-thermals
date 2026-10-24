param(
    [Parameter(Mandatory)][ValidateSet('Build','Publish')][string]$Phase,
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
if($Tag -notmatch '^v(\d+\.\d+\.\d+)$'){throw 'Expected a vMAJOR.MINOR.PATCH tag'}
$version=$Matches[1]
$output=[IO.Path]::GetFullPath($OutputDirectory)
$root=Split-Path $PSScriptRoot
$savedTemp=$env:TEMP
$savedTmp=$env:TMP
Push-Location $root
try{
    $dist=Join-Path $output 'dist'
    if($Phase -eq 'Build'){
        $scratch=Join-Path $output 'scratch'
        New-Item -ItemType Directory -Force $scratch|Out-Null
        $env:TEMP=$scratch
        $env:TMP=$scratch
        $artifacts=Join-Path $output 'artifacts'
        dotnet run --project tests/TaskbarThermals.Contracts.csproj -c Release --artifacts-path $artifacts -p:UseSharedCompilation=false
        if($LASTEXITCODE){throw 'Contracts failed'}
        $common=@('-c','Release','--artifacts-path',$artifacts,'-p:UseSharedCompilation=false','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true',"-p:Version=$version",'--nologo')
        dotnet publish TaskbarThermals.csproj @common -o (Join-Path $output 'fd')
        if($LASTEXITCODE){throw 'Framework-dependent publish failed'}
        dotnet publish TaskbarThermals.csproj @common --self-contained true -p:EnableCompressionInSingleFile=true -o (Join-Path $output 'sc')
        if($LASTEXITCODE){throw 'Standalone publish failed'}
        New-Item -ItemType Directory -Force $dist|Out-Null
        Copy-Item (Join-Path $output 'fd/TaskbarThermals.exe') (Join-Path $dist 'TaskbarThermals.exe')
        Copy-Item (Join-Path $output 'sc/TaskbarThermals.exe') (Join-Path $dist 'TaskbarThermals-standalone.exe')
        return
    }
    $checksums=[Collections.Generic.List[string]]::new()
    $rows=foreach($name in @('TaskbarThermals.exe','TaskbarThermals-standalone.exe')){
        $file=Get-Item (Join-Path $dist $name)
        if($file.VersionInfo.ProductVersion -notlike "$version*"){throw "Version mismatch: $name"}
        $hash=(Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $checksums.Add("$hash  $name")
        $needs=if($name -eq 'TaskbarThermals.exe'){'[.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)'}else{'nothing'}
        "| ``$name`` | $([math]::Round($file.Length/1MB,1)) MB | $needs |"
    }
    $checksums|Set-Content (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii
    $changelog=Get-Content CHANGELOG.md -Raw
    $match=[regex]::Match($changelog,"(?ms)^## $([regex]::Escape($version))\b[^\n]*\n(.*?)(?=^## |\z)")
    if(-not $match.Success){throw 'Missing release notes'}
    $notes=@($match.Groups[1].Value.Trim(),'','### Downloads','','| File | Size | Needs |','|---|---|---|')+$rows+@(
        '',"CPU temperature uses administrator rights and the existing [PawnIO](https://pawnio.eu) driver. The executables are unsigned.",
        '',"BS2 Pro telemetry uses THRM's local read-only observer connection. This software release does not claim verification of Vader controller inputs.",
        '',"Built by GitHub Actions from ``$Tag``. SHA256SUMS.txt records both executable hashes."
    )
    $notesPath=Join-Path $output 'release-notes.md'
    $notes -join "`n"|Set-Content $notesPath -Encoding utf8NoBOM
    gh release create $Tag (Join-Path $dist 'TaskbarThermals.exe') (Join-Path $dist 'TaskbarThermals-standalone.exe') (Join-Path $dist 'SHA256SUMS.txt') --title "Taskbar Thermals $version" --notes-file $notesPath --verify-tag
    if($LASTEXITCODE){throw 'Release publication failed'}
}finally{$env:TEMP=$savedTemp;$env:TMP=$savedTmp;Pop-Location}
