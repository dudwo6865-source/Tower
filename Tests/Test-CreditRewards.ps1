param(
    [string]$MonoRoot = 'C:\Program Files\Unity\Hub\Editor\2022.3.26f1\Editor\Data\MonoBleedingEdge'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$testOutput = Join-Path ([IO.Path]::GetTempPath()) ('tank-credit-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testOutput | Out-Null
$executable = Join-Path $testOutput 'CreditRewardsTests.exe'
$mono = Join-Path $MonoRoot 'bin/mono.exe'
$compiler = Join-Path $MonoRoot 'lib/mono/4.5/csc.exe'
& $mono $compiler /nologo /r:System.Runtime.Serialization.dll "/out:$executable" `
    (Join-Path $PSScriptRoot 'CreditRewardsHarness.cs') `
    (Join-Path $PSScriptRoot 'HealthHarnessStubs.cs') `
    (Join-Path $projectRoot 'Assets/Script/Combat/EntityHealth.cs') `
    (Join-Path $projectRoot 'Assets/Script/Map/StageClearRewards.cs') `
    (Join-Path $projectRoot 'Assets/Script/UI/ScoreRevealProgress.cs') `
    (Join-Path $projectRoot 'Assets/Script/Economy/ManaStoneManager.cs') `
    (Join-Path $projectRoot 'Assets/Script/CreditWallet.cs')
if ($LASTEXITCODE -ne 0) { throw '검증 프로그램 컴파일에 실패했습니다.' }
& $mono $executable (Join-Path $testOutput 'save')
if ($LASTEXITCODE -ne 0) { throw '크레딧 검증에 실패했습니다.' }
