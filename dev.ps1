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
    throw 'Port 5085 is already in use. Stop the running UpKeep API and try again.'
}

if (-not (Test-Path -LiteralPath $viteCli)) {
    throw 'Vite is not installed. Run npm install in UpKeepClient and try again.'
}

Write-Host 'Starting UpKeep API and React client...'
Write-Host 'Press Ctrl+C to stop both.'

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
