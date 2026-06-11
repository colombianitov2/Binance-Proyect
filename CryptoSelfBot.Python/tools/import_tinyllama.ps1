Param()

$workspace = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path "$workspace\..\.." | Select-Object -ExpandProperty Path
$iasDir = Join-Path $repoRoot 'IAS'
$tinySrc = Join-Path $iasDir 'tinyllama'
$openClaude = Join-Path $iasDir 'OpenClaude-Portable-main'
$destModels = Join-Path $openClaude 'data\models\tinyllama-local'

Write-Host "Import tinyllama manifest from: $tinySrc"
if (-not (Test-Path $tinySrc)) {
	Write-Error "tinyllama folder not found at $tinySrc"
	exit 1
}

New-Item -ItemType Directory -Force -Path $destModels | Out-Null

Get-ChildItem -Path $tinySrc -File | ForEach-Object {
	$dest = Join-Path $destModels $_.Name
	Copy-Item -Path $_.FullName -Destination $dest -Force
	Write-Host "Copied $_.Name -> $dest"
}

Write-Host "Import complete. You may now run OpenClaude setup script to let it detect the model or start the OpenClaude dashboard." 
