$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'BossSkillTiming.cs'
if (-not (Test-Path -LiteralPath $source)) { $source = Join-Path $PSScriptRoot '../Assets/Script/Combat/BossSkillTiming.cs' }
Add-Type -TypeDefinition (Get-Content -Raw -LiteralPath $source)
$t = [BossSkillTiming]::new()
$script:checks = 0
function Check($ok, $message) { if (-not $ok) { throw $message }; $script:checks++ }
$t.Reset(0, 5)
Check (-not $t.TryBegin(4, 10, 1, 4)) '첫 대기 중 발동 금지'
Check ($t.TryBegin(5, 10, 1, 4)) '준비 시작'
Check (-not $t.TryBegin(6, 10, 1, 4)) '진행 중 재진입 금지'
Check (-not $t.TryHit(5.5, 0.8)) '예고 시간 이전 타격 금지'
Check ($t.TryHit(6.125, 0.8)) '유효 타격'
Check (-not $t.TryHit(6.125, 0.8)) '중복 이벤트는 한 번만 피해'
Check (-not $t.HasExpired(6.5)) '후딜 동안 공격 잠금'
Check ($t.HasExpired(7)) '후딜 종료'
$t.Cancel()
Check (-not $t.TryHit(7, 0.8)) '종료 후 늦은 이벤트 무시'
Check (-not $t.TryBegin(14, 10, 1, 4)) '쿨타임 유지'
Check ($t.TryBegin(15, 10, 1, 4)) '쿨타임 종료 후 재사용'
$t.Cancel()
Check (-not $t.TryHit(16, 0.8)) '후퇴 및 사망 취소 후 타격 금지'
Check (-not $t.TryBegin(16, 10, 1, 4)) '취소해도 쿨타임 유지'
Check ($t.TryBegin(25, 10, 1, 4)) '다음 시전'
Check ($t.HasExpired(29)) '누락 이벤트 시간 초과'
Check (-not $t.TryHit(29, 0.8)) '시간 초과 이후 피해 금지'
$t.Cancel()
Check (-not $t.IsActive) '시간 초과 취소 시 행동 잠금 해제'
Write-Output "스킬 타이밍 $script:checks 개 검증 통과"
