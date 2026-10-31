param(
    [string]$UnityEditor = 'C:/Program Files/Unity/Hub/Editor/2022.3.26f1/Editor/Unity.exe'
)

# 실제 작업 중인 씬과 실행 취소 기록을 건드리지 않고 별도 프로젝트에서 검증한다.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$testProject = Join-Path ([IO.Path]::GetTempPath()) ('tank-cliff-tests-' + [Guid]::NewGuid().ToString('N'))
foreach ($folder in @('Assets/Runtime','Assets/Editor','Packages','ProjectSettings')) {
    New-Item -ItemType Directory -Path (Join-Path $testProject $folder) -Force | Out-Null
}
foreach ($source in @('Assets/Script/Map/CliffPainter.cs','Assets/Script/Map/CliffTileSet.cs',
    'Assets/Script/Map/HillTileSet.cs','Assets/Script/Attributes/LabelAttribute.cs')) {
    Copy-Item -LiteralPath (Join-Path $repo $source) -Destination (Join-Path $testProject 'Assets/Runtime')
}
foreach ($source in @('Assets/Script/Editor/CliffPainterUndo.cs','Assets/Script/Editor/CliffMapEditorWindow.cs',
    'Assets/Script/Editor/CliffPainterEditor.cs','Tests/CliffPainterRegressionTests.cs')) {
    Copy-Item -LiteralPath (Join-Path $repo $source) -Destination (Join-Path $testProject 'Assets/Editor')
}
Copy-Item -LiteralPath (Join-Path $repo 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $testProject 'ProjectSettings')
'{"dependencies":{}}' | Set-Content -LiteralPath (Join-Path $testProject 'Packages/manifest.json') -Encoding UTF8
$log = Join-Path $testProject 'test.log'
$arguments = @('-batchmode','-nographics','-projectPath',('"' + $testProject + '"'),
    '-executeMethod','CliffPainterRegressionTests.RunBatch','-logFile',('"' + $log + '"'))
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(180000)) {
    $process.Kill()
    throw "임시 프로젝트 검증 제한 시간을 넘었습니다. 로그: $log"
}
$result = Select-String -LiteralPath $log -Pattern 'CLIFF_TEST_PASS' | Select-Object -Last 1
if ($process.ExitCode -ne 0 -or $null -eq $result) {
    Get-Content -LiteralPath $log -Tail 40
    throw "지형 회귀 검증 실패. 로그: $log"
}
Write-Output $result.Line
Write-Output "검증 로그: $log"
