# HBPakEditor - Add to PATH
# Run this script to add HBPakEditor to your system PATH

param(
    [string]$Path
)

Write-Host "=== HBPakEditor PATH Setup ===" -ForegroundColor Cyan
Write-Host ""

# Default path
$defaultPath = "C:\Users\ShadowEvil\source\repos\HBPakEditor\bin\publish"

# Get path from user if not provided
if (-not $Path) {
    Write-Host "Enter the path to HBPakEditor directory"
    Write-Host "(Press Enter for default: $defaultPath)"
    Write-Host ""
    $userInput = Read-Host "Path"

    if ([string]::IsNullOrWhiteSpace($userInput)) {
        $Path = $defaultPath
    } else {
        $Path = $userInput
    }
}

# Clean up path - remove surrounding quotes if present
$Path = $Path.Trim()
$Path = $Path.Trim('"')
$Path = $Path.Trim("'")

# Validate path
$exePath = Join-Path $Path "HBPakEditor.exe"
if (-not (Test-Path $exePath)) {
    Write-Host ""
    Write-Host "ERROR: HBPakEditor.exe not found at: $exePath" -ForegroundColor Red
    Write-Host "Please verify the path and try again." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Found HBPakEditor at: $exePath" -ForegroundColor Green

# Get current user PATH
$currentPath = [Environment]::GetEnvironmentVariable("Path", "User")

# Check if already in PATH
if ($currentPath -split ';' | Where-Object { $_ -eq $Path }) {
    Write-Host ""
    Write-Host "HBPakEditor is already in your PATH!" -ForegroundColor Yellow
    exit 0
}

# Add to PATH
Write-Host ""
Write-Host "Adding to user PATH..." -ForegroundColor Cyan

$newPath = $currentPath + ";" + $Path
[Environment]::SetEnvironmentVariable("Path", $newPath, "User")

Write-Host ""
Write-Host "SUCCESS! HBPakEditor added to PATH." -ForegroundColor Green
Write-Host ""
Write-Host "You can now use 'HBPakEditor' from any terminal." -ForegroundColor Cyan
Write-Host "Note: You may need to restart your terminal for changes to take effect." -ForegroundColor Yellow
Write-Host ""
Write-Host "Try running: HBPakEditor help" -ForegroundColor Cyan
