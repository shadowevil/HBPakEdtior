# HBPakEditor - Remove from PATH
# Run this script to remove HBPakEditor from your system PATH

param(
    [string]$Path
)

Write-Host "=== HBPakEditor PATH Removal ===" -ForegroundColor Cyan
Write-Host ""

# Default path
$defaultPath = "C:\Users\ShadowEvil\source\repos\HBPakEditor\bin\publish"

# Get path from user if not provided
if (-not $Path) {
    Write-Host "Enter the HBPakEditor path to remove from PATH"
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

# Get current user PATH
$currentPath = [Environment]::GetEnvironmentVariable("Path", "User")
$pathArray = $currentPath -split ';' | Where-Object { $_ -ne "" }

# Check if in PATH
if (-not ($pathArray | Where-Object { $_ -eq $Path })) {
    Write-Host ""
    Write-Host "HBPakEditor path not found in your PATH." -ForegroundColor Yellow
    Write-Host "Nothing to remove." -ForegroundColor Yellow
    exit 0
}

# Remove from PATH
Write-Host ""
Write-Host "Removing from user PATH..." -ForegroundColor Cyan

$newPathArray = $pathArray | Where-Object { $_ -ne $Path }
$newPath = $newPathArray -join ';'
[Environment]::SetEnvironmentVariable("Path", $newPath, "User")

Write-Host ""
Write-Host "SUCCESS! HBPakEditor removed from PATH." -ForegroundColor Green
Write-Host ""
Write-Host "Note: You may need to restart your terminal for changes to take effect." -ForegroundColor Yellow
