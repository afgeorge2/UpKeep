$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
$apiProject = Join-Path $projectRoot 'UpKeep\UpKeep.csproj'
$clientDirectory = Join-Path $projectRoot 'UpKeepClient'
$viteCli = Join-Path $clientDirectory 'node_modules\vite\bin\vite.js'
$apiProcess = $null
$clientProcess = $null

function Test-LocalPort {
    param([int]$Port)

    $connection = [System.Net.Sockets.TcpClient]::new()

    try {
        $connection.Connect('127.0.0.1', $Port)
        return $true
    }
    catch [System.Net.Sockets.SocketException] {
        return $false
    }
    finally {
        $connection.Dispose()
    }
}

function Stop-ProcessTree {
    param([System.Diagnostics.Process]$Process)

    if ($null -ne $Process -and -not $Process.HasExited) {
        & taskkill.exe /PID $Process.Id /T /F 2>$null | Out-Null
    }
}

if (Test-LocalPort -Port 5085) {
    $upkeepReady = $false
    try {
        $apiDescription = Invoke-RestMethod 'http://localhost:5085/swagger/v1/swagger.json' -TimeoutSec 5
        $clientPage = Invoke-WebRequest 'http://localhost:5173' -UseBasicParsing -TimeoutSec 5
        $proxyResponse = Invoke-WebRequest 'http://localhost:5173/api/properties' -UseBasicParsing -TimeoutSec 5
        $upkeepReady = $apiDescription.info.title -eq 'UpKeep' -and
            $clientPage.Content -match '<title>upkeepclient</title>' -and
            $proxyResponse.StatusCode -eq 200 -and
            $proxyResponse.Headers['Content-Type'] -match 'application/json'
    }
    catch {
        # An occupied port alone does not mean the app is ready.
    }
    if ($upkeepReady) {
        Write-Host 'UpKeep is already running: http://localhost:5173'
        Write-Host 'Refresh the page to use it. The existing servers remain running.'
        return
    }
    throw 'Port 5085 is occupied, but the complete UpKeep app is not responding. Stop the existing API in its original terminal, then run .\dev.ps1 again.'
}

if (-not (Test-Path -LiteralPath $viteCli)) {
    throw 'Vite is not installed. Run npm install in UpKeepClient and try again.'
}

Write-Host 'Starting UpKeep API and React client...'
Write-Host 'Press Ctrl+C to stop both.'

# This launcher starts Vite directly, so npm predev does not run automatically.
& node (Join-Path $clientDirectory 'scripts\prepare-ocr.mjs')
if ($LASTEXITCODE -ne 0) {
    throw 'Could not prepare the equipment-label reader. Run npm install in UpKeepClient and try again.'
}

try {
    $apiProcess = Start-Process `
        -FilePath 'dotnet' `
        -ArgumentList @('run', '--project', $apiProject, '--launch-profile', 'http') `
        -WorkingDirectory $projectRoot `
        -NoNewWindow `
        -PassThru

    Write-Host 'Waiting for the API to become ready...'
    $apiStartDeadline = [DateTime]::UtcNow.AddSeconds(30)

    while (-not (Test-LocalPort -Port 5085)) {
        if ($apiProcess.HasExited) {
            throw "The API stopped unexpectedly with exit code $($apiProcess.ExitCode)."
        }

        if ([DateTime]::UtcNow -ge $apiStartDeadline) {
            throw 'The API did not start within 30 seconds.'
        }

        Start-Sleep -Milliseconds 250
    }

    Write-Host 'API ready. Starting React client...'

    $clientProcess = Start-Process `
        -FilePath 'node' `
        -ArgumentList @($viteCli) `
        -WorkingDirectory $clientDirectory `
        -NoNewWindow `
        -PassThru

    while (-not $apiProcess.HasExited -and -not $clientProcess.HasExited) {
        Start-Sleep -Milliseconds 500
    }

    if ($apiProcess.HasExited) {
        throw "The API stopped unexpectedly with exit code $($apiProcess.ExitCode)."
    }

    throw "The React client stopped unexpectedly with exit code $($clientProcess.ExitCode)."
}
finally {
    Write-Host 'Stopping UpKeep development servers...'
    Stop-ProcessTree -Process $clientProcess
    Stop-ProcessTree -Process $apiProcess
}
