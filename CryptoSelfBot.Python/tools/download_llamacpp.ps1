$tools='E:\Visual Estudio\Binance App Proyect\CryptoSelfBot.Python\tools'
New-Item -ItemType Directory -Force -Path $tools | Out-Null
$names = @('llama.cpp-windows.zip','llama.cpp-windows-x64.zip','llama-windows.zip','llama.zip','prebuilt-windows.zip','llama_windows.zip','llama-cpp-windows.zip','llama.cpp-windows-x86_64.zip')
$downloaded = $null
foreach ($n in $names) {
	$url = "https://github.com/ggerganov/llama.cpp/releases/latest/download/$n"
	Write-Host "Trying $url"
	try {
		Invoke-WebRequest -Uri $url -OutFile (Join-Path $tools $n) -UseBasicParsing -ErrorAction Stop
		Write-Host "Downloaded $n"
		$downloaded = (Join-Path $tools $n)
		break
	} catch {
		Write-Host "Failed: $n -> $($_.Exception.Message)"
	}
}

if ($downloaded) {
	Write-Host "Extracting $downloaded"
	try {
		Expand-Archive -LiteralPath $downloaded -DestinationPath $tools -Force
		Write-Host "Extracted"
	} catch {
		Write-Host "Extraction failed: $($_.Exception.Message)"
	}

	$exes = Get-ChildItem -Path $tools -Recurse -Filter '*.exe' -ErrorAction SilentlyContinue | Select-Object -First 20
	if ($exes) {
		$exes | ForEach-Object { Write-Host "Found exe: $($_.FullName)" }
	} else {
		Write-Host 'No exe found in extracted content'
	}
} else {
	Write-Host 'No candidate downloaded'
}
