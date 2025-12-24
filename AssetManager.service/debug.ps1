param(
    [string]$PipeName = "asset-monitor-pipe",
    [int]$IntervalSeconds = 2
)

while ($true) {
    Clear-Host
    Write-Host "AssetManager Debug ($(Get-Date))"
    Write-Host "-------------------------------"

    try {
        $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", $PipeName, [System.IO.Pipes.PipeDirection]::In)
        $pipe.Connect(3000)

        $reader = New-Object System.IO.StreamReader($pipe)
        $json = $reader.ReadLine()

        $reader.Close()
        $pipe.Close()

        $obj = $json | ConvertFrom-Json
        $obj | Format-List
    }
    catch {
        Write-Host "Erro ao ler pipe: $_" -ForegroundColor Red
    }

    Start-Sleep -Seconds $IntervalSeconds
}
