param(
    [switch]$FrameworkDependent,
    [string]$Runtime = "win-x64"
)

$project = "src/GameAccelerator.UI/GameAccelerator.UI.csproj"
$output = "./publish/$Runtime"

Write-Host "Building Game Accelerator..." -ForegroundColor Cyan
Write-Host "Runtime: $Runtime" -ForegroundColor Gray

$args = @(
    "publish", $project,
    "-c", "Release",
    "-r", $Runtime,
    "-o", $output,
    "-p:PublishSingleFile=true",
    "-p:PublishReadyToRun=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:DebugType=none",
    "-p:DebugSymbols=false"
)

if (-not $FrameworkDependent) {
    Write-Host "Mode: Self-contained (no .NET runtime needed)" -ForegroundColor Gray
    $args += "--self-contained", "true"
    $args += "-p:PublishTrimmed=true"
} else {
    Write-Host "Mode: Framework-dependent (requires .NET 8 runtime)" -ForegroundColor Gray
}

Write-Host "Publishing..." -ForegroundColor Yellow
dotnet @args

if ($LASTEXITCODE -eq 0) {
    Remove-Item "$output/*.pdb" -ErrorAction SilentlyContinue
    $exe = Get-ChildItem "$output/GameAccelerator.UI.exe" -ErrorAction SilentlyContinue
    $size = if ($exe) { [math]::Round($exe.Length / 1MB, 1) } else { "unknown" }
    Write-Host "`nDone! Published to: $output" -ForegroundColor Green
    Write-Host "Executable: GameAccelerator.UI.exe ($size MB)" -ForegroundColor Green
} else {
    Write-Host "`nPublish failed!" -ForegroundColor Red
}
