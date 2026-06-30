param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [bool]$SelfContained = $true,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "src\GameToolOrchestrator.Wpf\GameToolOrchestrator.Wpf.csproj"
$publishRoot = Join-Path $repoRoot "artifacts\publish"
$outputDirectory = Join-Path $publishRoot "GameToolOrchestrator.Wpf-$Runtime"
$exePath = Join-Path $outputDirectory "GameToolOrchestrator.Wpf.exe"

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Command
    )

    Write-Host ""
    Write-Host "==> $Name"
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Publishing GameToolOrchestrator.Wpf"
Write-Host "Repository: $repoRoot"
Write-Host "Runtime: $Runtime"
Write-Host "Configuration: $Configuration"
Write-Host "SelfContained: $SelfContained"

if (Test-Path $outputDirectory) {
    Remove-Item -LiteralPath $outputDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

Push-Location $repoRoot
try {
    Invoke-Step "dotnet restore" { dotnet restore }
    Invoke-Step "dotnet build" { dotnet build GameToolOrchestrator.sln -c $Configuration --no-restore -m:1 }

    if (-not $SkipTests) {
        Invoke-Step "dotnet test" { dotnet test GameToolOrchestrator.sln -c $Configuration --no-build --no-restore -m:1 }
    }

    Invoke-Step "dotnet publish WPF" {
        dotnet publish $projectPath `
            -c $Configuration `
            -r $Runtime `
            --self-contained $SelfContained `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -o $outputDirectory
    }

    Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination $outputDirectory -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot "sample-config.json") -Destination $outputDirectory -Force

    $docsOutput = Join-Path $outputDirectory "docs"
    New-Item -ItemType Directory -Path $docsOutput -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot "docs\manual-verification.md") -Destination $docsOutput -Force
    if (Test-Path (Join-Path $repoRoot "docs\release-checklist.md")) {
        Copy-Item -LiteralPath (Join-Path $repoRoot "docs\release-checklist.md") -Destination $docsOutput -Force
    }

    New-Item -ItemType Directory -Path (Join-Path $outputDirectory "logs") -Force | Out-Null

    Write-Host ""
    Write-Host "Publish completed."
    Write-Host "Executable: $exePath"
}
finally {
    Pop-Location
}
