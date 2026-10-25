# Builds and runs the headless test apps (Rhino 8 must be installed; Rhino itself may stay open,
# since they build GluLamb into their own output folders, not into bin/).
#
#   ./run-tests.ps1                 all of them
#   ./run-tests.ps1 reconstruction  only those whose name contains the word
#
# The test apps are in the solution under "tests" but left out of its build: each builds its own
# copy of GluLamb, and several of those at once in one solution build collide.

param([string]$Only = "")

$ErrorActionPreference = "Continue"
$root = $PSScriptRoot
$tests = @(
    @{ Name = "GluLamb.Reconstruction.Test"; Args = @("--out", (Join-Path $root "temp/reconstruction-out")) },
    @{ Name = "GluLamb.ClusterTest";         Args = @() },
    @{ Name = "GluLamb.Gallery";             Args = @("--out", (Join-Path $root "temp/gallery-out")) }
)

$failed = @()
foreach ($t in $tests) {
    if ($Only -and $t.Name -notlike "*$Only*") { continue }
    $project = Join-Path $root "$($t.Name)/$($t.Name).csproj"

    Write-Host "== $($t.Name)"
    dotnet build $project -v q -clp:ErrorsOnly
    if ($LASTEXITCODE -ne 0) { $failed += "$($t.Name) (build)"; continue }

    dotnet run --project $project --no-build -- @($t.Args)
    if ($LASTEXITCODE -ne 0) { $failed += $t.Name }
}

if ($failed.Count -gt 0) { Write-Host "Failed: $($failed -join ', ')"; exit 1 }
Write-Host "All test apps passed."
