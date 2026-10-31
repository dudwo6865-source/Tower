# 실제 출격 규칙을 실행한다. 이동 경로와 전투 애니메이션은 유니티에서 확인한다.
$ErrorActionPreference = 'Stop'
$source = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../Assets/Script/Mission/BossSortieRules.cs')
Add-Type -TypeDefinition $source
$rules = [BossSortieRules]::new()
$script:checks = 0
function Check($condition, $message) {
    if (-not $condition) { throw $message }
    $script:checks++
}
$rules.Tick($false, 1, 2, $false, $true, 0, 1000, 0.2)
Check ($rules.State -eq 'Nest') '낮에는 출격하지 않는다'
$rules.Tick($true, 1, 2, $false, $true, 0, 1000, 0.2)
Check ($rules.State -eq 'Nest') '첫 출격 밤 이전에는 기다린다'
$rules.Tick($true, 2, 2, $true, $true, 150, 1000, 0.2)
Check ($rules.State -eq 'Nest') '둥지에서 교전 중이면 출격하지 않는다'
$rules.Tick($true, 2, 2, $false, $true, 150, 1000, 0.2)
Check ($rules.State -eq 'Assault') '같은 밤에 교전이 끝나면 출격한다'
$rules.Tick($true, 2, 2, $true, $false, 349, 1000, 0.2)
Check ($rules.State -eq 'Assault') '둥지에서 받은 피해는 출격 피해에 포함하지 않는다'
$rules.Tick($true, 2, 2, $true, $false, 350, 1000, 0.2)
Check ($rules.State -eq 'Retreat') '교전 중에도 출격 후 최대 체력 20퍼센트 피해에서 후퇴한다'
$rules.Tick($true, 2, 2, $true, $true, 400, 1000, 0.2)
Check ($rules.State -eq 'Nest') '추격 피해를 받으며 돌아와도 둥지로 전환한다'
$rules.Tick($true, 2, 2, $false, $true, 400, 1000, 0.2)
Check ($rules.State -eq 'Nest') '후퇴한 당일 밤에는 재출격하지 않는다'
$rules.Tick($false, 3, 2, $false, $true, 400, 1000, 0.2)
$rules.Tick($true, 3, 2, $false, $true, 400, 1000, 0.2)
Check ($rules.State -eq 'Assault') '다음 밤에는 누적 체력을 초기화하지 않고 다시 출격한다'
$rules.Tick($true, 3, 2, $true, $false, 599, 1000, 0.2)
Check ($rules.State -eq 'Assault') '출격마다 피해 기준을 새로 잡는다'
$rules.Tick($false, 4, 2, $true, $false, 599, 1000, 0.2)
Check ($rules.State -eq 'Retreat') '아침에는 피해량과 교전 여부에 관계없이 후퇴한다'
$late = [BossSortieRules]::new()
$late.Tick($true, 1, 1, $true, $true, 0, 1000, 0.2)
$late.Tick($false, 2, 1, $false, $true, 0, 1000, 0.2)
Check ($late.State -eq 'Nest') '교전이 아침에 끝나면 지난 밤 출격을 실행하지 않는다'
Write-Output "보스 출격 규칙 $script:checks 개 검증 통과"
