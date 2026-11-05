$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition (Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot '../Assets/Script/UI/StageIntroTiming.cs'))
$script:checks = 0
function Check($ok, $message) { if (-not $ok) { throw $message }; $script:checks++ }
$intro = [StageIntroTiming]::new(1)
$intro.Tick(10, $false)
Check ($intro.State.ToString() -eq 'Loading') '로딩이 오래 걸려도 공개하지 않는다'
$intro.Tick(10, $true)
Check ($intro.State.ToString() -eq 'Revealing' -and $intro.Progress -eq 0) '로딩 시간은 공개 시간에 포함하지 않는다'
$intro.Tick(0.5, $true)
Check ($intro.State.ToString() -eq 'Revealing' -and $intro.Progress -eq 0.5) '화면 공개 중간 단계'
$intro.Tick(0, $true)
Check ($intro.Progress -eq 0.5) '실제 시간이 흐르지 않으면 연출도 유지한다'
$intro.Tick(-1, $true)
Check ($intro.Progress -eq 0.5) '음수 시간으로 연출이 역행하지 않는다'
$intro.Tick(0.5, $true)
Check ($intro.State.ToString() -eq 'Finished' -and $intro.Progress -eq 1) '공개 즉시 완료하여 문구 대기 없이 로어와 조작을 허용한다'
$intro.Tick(10, $true)
Check ($intro.State.ToString() -eq 'Finished') '완료 후 연출을 재실행하지 않는다'
$zero = [StageIntroTiming]::new(0)
$zero.Tick(100, $true)
Check ($zero.State.ToString() -eq 'Revealing') '긴 로딩 프레임에도 공개 단계를 거친다'
$zero.Tick(0, $true)
Check ($zero.State.ToString() -eq 'Finished') '공개 시간이 0이면 즉시 종료한다'
$negative = [StageIntroTiming]::new(-1)
$negative.Tick(0, $true)
$negative.Tick(0, $true)
Check ($negative.State.ToString() -eq 'Finished') '잘못된 음수 공개 시간도 정상 종료한다'
Write-Output "시작 연출 타이밍 $script:checks 개 검증 통과"
