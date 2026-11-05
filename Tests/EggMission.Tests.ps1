# 런타임과 동일한 진행 규칙으로 시간 갱신, 중복 처리, 부화 후 승리를 검사한다.
$ErrorActionPreference = 'Stop'
$source = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '../Assets/Script/Mission/EggMissionRules.cs')
Add-Type -TypeDefinition $source
$script:checks = 0
function Check($condition, $message) {
    if (-not $condition) { throw $message }
    $script:checks++
}
$rules = [EggMissionRules]::new(4, [float[]](300, 240, 180, 120))
Check ($rules.RemainingSeconds -eq 300 -and -not $rules.Complete) '첫 단계는 5분이며 자동 승리하지 않는다'
Check (-not $rules.Tick(290, $false)) '10초가 남으면 부화하지 않는다'
Check ($rules.ResolveEgg(2, $false)) '순서와 관계없이 원하는 알을 파괴할 수 있다'
Check ($rules.RemainingSeconds -eq 240 -and $rules.RemainingEggs -eq 3) '잔여 시간을 이월하지 않고 4분으로 시작한다'
Check (-not $rules.ResolveEgg(2, $true)) '파괴된 알은 부화할 수 없다'
Check ($rules.RemainingSeconds -eq 240 -and $rules.LivingMonsters -eq 0) '중복 처리로 시간과 몬스터 수가 변하지 않는다'
Check (-not $rules.Tick(300, $true)) '일시정지 중에는 시간이 흐르지 않는다'
Check ($rules.RemainingSeconds -eq 240) '일시정지 후 시간이 유지된다'
Check ($rules.Tick(240, $false)) '정확히 제한 시간이 지나면 부화 요청이 발생한다'
Check ($rules.RemainingEggs -eq 3 -and $rules.LivingMonsters -eq 0) '부화 요청만으로 알이 사라지지 않는다'
Check ($rules.Tick(1, $false)) '생성 실패 시 시간을 늘리거나 알을 소비하지 않는다'
Check ($rules.ResolveEgg(0, $true)) '생성 성공 후 부화를 확정한다'
Check ($rules.RemainingSeconds -eq 180 -and $rules.LivingMonsters -eq 1) '부화도 다음 단계로 진행한다'
Check (-not $rules.Tick(30, $false)) '부화 몬스터가 살아 있어도 다음 타이머가 흐른다'
Check ($rules.RemainingSeconds -eq 150) '부화 몬스터와 교전하는 동안 시간이 감소한다'
$rules.DefeatMonster(0)
$rules.DefeatMonster(0)
Check ($rules.LivingMonsters -eq 0 -and $rules.RemainingSeconds -eq 150) '몬스터 처치는 타이머를 갱신하지 않으며 중복 집계하지 않는다'
$null = $rules.ResolveEgg(1, $false)
Check ($rules.RemainingSeconds -eq 120) '마지막 알의 제한 시간은 2분이다'
$null = $rules.ResolveEgg(3, $true)
Check ($rules.RemainingEggs -eq 0 -and $rules.RemainingSeconds -eq 0 -and -not $rules.Complete) '마지막 알이 부화해도 몬스터가 살아 있으면 승리하지 않는다'
Check (-not $rules.Tick(1000, $false)) '모든 알 처리 후 추가 부화는 없다'
$rules.DefeatMonster(3)
Check ($rules.Complete) '마지막 부화 몬스터까지 처치하면 완료한다'

$allDestroyed = [EggMissionRules]::new(3, [float[]](300, 240, 180, 120))
foreach ($i in 0..2) { $null = $allDestroyed.ResolveEgg($i, $false) }
Check ($allDestroyed.Complete -and $allDestroyed.ResolvedEggs -eq 3) '같은 프레임에 모든 알을 파괴해도 각각 한 번씩 처리한다'
Check (-not $allDestroyed.ResolveEgg(-1, $false)) '잘못된 알 번호는 무시한다'
Check (-not $allDestroyed.ResolveEgg(3, $false)) '범위 밖 알 번호는 무시한다'
Check ($null -ne [EggMissionRules]::ValidateDurations([float[]](300, 240), 3)) '단계 수 부족을 거부한다'
Check ($null -ne [EggMissionRules]::ValidateDurations([float[]](300, 300), 2)) '줄어들지 않는 시간을 거부한다'
Check ($null -ne [EggMissionRules]::ValidateDurations([float[]](300, 400), 2)) '증가하는 시간을 거부한다'
Check ($null -ne [EggMissionRules]::ValidateDurations([float[]](0), 1)) '0초 제한 시간을 거부한다'
Check ($null -ne [EggMissionRules]::ValidateDurations([float[]]([float]::NaN), 1)) '유효하지 않은 시간을 거부한다'
Check ($null -ne [EggMissionRules]::ValidateDurations([float[]]([float]::PositiveInfinity), 1)) '무한 시간을 거부한다'
Check ($null -ne [EggMissionRules]::ValidateDurations([float[]](300), 0)) '알이 없는 설정으로 자동 승리하지 않는다'
$durations = [float[]](300, 240)
$copy = [EggMissionRules]::new(2, $durations)
$durations[1] = 1
$null = $copy.ResolveEgg(0, $false)
Check ($copy.RemainingSeconds -eq 240) '시작 후 외부 배열 수정이 진행 중 시간에 영향을 주지 않는다'
$animated = [EggMissionRules]::new(2, [float[]](10, 5))
$null = $animated.Tick(10, $false)
Check ($animated.BeginHatch(0)) '시간 만료 시 소환을 기다리는 부화 상태로 진입한다'
Check ($animated.HatchingEggs -eq 1 -and $animated.LivingMonsters -eq 0 -and $animated.RemainingSeconds -eq 5) '부화 연출 시작 때 다음 타이머가 시작되며 아직 몬스터는 없다'
Check (-not $animated.BeginHatch(0)) '부화 시작을 중복 처리하지 않는다'
$null = $animated.Tick(2, $false)
Check ($animated.ResolveEgg(0, $true)) '소환 이벤트 때 몬스터로 전환한다'
Check ($animated.RemainingSeconds -eq 3 -and $animated.HatchingEggs -eq 0 -and $animated.LivingMonsters -eq 1) '소환 이벤트는 다음 타이머를 다시 늘리지 않는다'
Check (-not $animated.ResolveEgg(0, $true)) '중복 소환 이벤트는 무시한다'
$null = $animated.BeginHatch(1)
$animated.DefeatMonster(0)
Check (-not $animated.Complete) '마지막 알이 부화 애니메이션 중이면 먼저 승리하지 않는다'
$null = $animated.ResolveEgg(1, $false)
Check ($animated.Complete -and $animated.HatchingEggs -eq 0) '소환 전 마지막 알을 파괴하면 부화가 취소되고 완료한다'
Check (-not $animated.ResolveEgg(1, $true)) '파괴된 알의 뒤늦은 소환 이벤트는 무시한다'
$overlap = [EggMissionRules]::new(2, [float[]](10, 5))
$null = $overlap.BeginHatch(0)
$null = $overlap.BeginHatch(1)
Check ($overlap.HatchingEggs -eq 2 -and -not $overlap.Complete) '시간이 짧아 부화 연출이 겹쳐도 각각 추적한다'
$null = $overlap.ResolveEgg(1, $true)
$null = $overlap.ResolveEgg(0, $true)
Check ($overlap.LivingMonsters -eq 2 -and $overlap.HatchingEggs -eq 0) '역순으로 도착한 소환 이벤트도 정확히 처리한다'
$overlap.DefeatMonster(1)
$overlap.DefeatMonster(0)
Check ($overlap.Complete) '부화 몬스터가 모두 죽어야 최종 완료한다'
Write-Output "알 미션 규칙 검사 $script:checks 개 통과"
