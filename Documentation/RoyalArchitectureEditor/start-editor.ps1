param([switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
$editorRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$editorAddress = 'http://127.0.0.1:8731/'
$healthAddress = $editorAddress + 'api/health'

function Read-EditorHealth {
    try { return Invoke-RestMethod -Uri $healthAddress -TimeoutSec 2 -Method Get }
    catch { return $null }
}

function Test-EditorIdentity($health) {
    if (-not $health -or $health.service -ne 'royal-architecture-editor') { return $false }
    return [string]::Equals([IO.Path]::GetFullPath([string]$health.workspace), $editorRoot, [StringComparison]::OrdinalIgnoreCase)
}

try {
    $health = Read-EditorHealth
    if (-not (Test-EditorIdentity $health)) {
        $client = [Net.Sockets.TcpClient]::new()
        try {
            $connect = $client.ConnectAsync('127.0.0.1', 8731)
            try { $connected = $connect.Wait(600) -and $client.Connected } catch { $connected = $false }
        }
        finally { $client.Dispose() }
        if ($connected) { throw '端口 8731 已由其他服务使用。为避免影响它，建筑编辑器没有启动。' }

        $pythonCommand = Get-Command python.exe -ErrorAction SilentlyContinue
        if (-not $pythonCommand) { $pythonCommand = Get-Command py.exe -ErrorAction SilentlyContinue }
        if (-not $pythonCommand) { throw '未找到 Python。请安装 Python 3.10 或更高版本后重试。' }
        $logsPath = Join-Path $editorRoot 'logs'
        New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
        $serverPath = Join-Path $editorRoot 'server.py'
        $arguments = '-u "' + $serverPath + '"'
        $process = Start-Process -FilePath $pythonCommand.Source -ArgumentList $arguments -WorkingDirectory $editorRoot `
            -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logsPath 'server.log') `
            -RedirectStandardError (Join-Path $logsPath 'server-error.log')
        $deadline = [DateTime]::UtcNow.AddSeconds(20)
        do {
            Start-Sleep -Milliseconds 250
            $health = Read-EditorHealth
            if (Test-EditorIdentity $health) { break }
            if ($process.HasExited) { break }
        } while ([DateTime]::UtcNow -lt $deadline)
        if (-not (Test-EditorIdentity $health)) {
            throw '建筑编辑器未能启动。请查看同目录 logs/server-error.log；已有服务未被关闭或替换。'
        }
    }
    if (-not $NoBrowser) { Start-Process $editorAddress }
    Write-Output $editorAddress
}
catch {
    $message = $_.Exception.Message
    Write-Error $message -ErrorAction Continue
    if (-not $NoBrowser) {
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.MessageBox]::Show($message, '建筑编辑器启动失败') | Out-Null
    }
    exit 1
}
