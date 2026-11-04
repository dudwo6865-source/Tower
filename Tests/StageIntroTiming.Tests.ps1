$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition (Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot '../Assets/Script/UI/StageIntroTiming.cs'))
$script:checks = 0
function Check($ok, $message) { if (-not $ok) { throw $message }; $script:checks++ }
$intro = [StageIntroTiming]::new(1, 1, 0.3)
$intro.Tick(10, $false)
Check ($intro.State.ToString() -eq 'Loading') '로딩이 오래 걸려도 공개하지 않는다'
$intro.Tick(10, $true)
Check ($intro.State.ToString() -eq 'Revealing' -and $intro.Progress -eq 0) '로딩 시간은 공개 시간에 포함하지 않는다'
$intro.Tick(0.5, $true)
Check ($intro.State.ToString() -eq 'Revealing' -and $intro.Progress -eq 0.5) '화면 공개 중간 단계'
$intro.Tick(0, $true)
Check ($intro.Progress -eq 0.5) '실제 시간이 흐르지 않으면 연출도 유지한다'
$intro.Tick(0.5, $true)
Check ($intro.State.ToString() -eq 'Announcement') '공개 완료 뒤 로어 단계에 진입한다'
$intro.Tick(0.9, $true)
Check ($intro.State.ToString() -eq 'Announcement') '로어 후 안내 유지 시간 보장'
$intro.Tick(0.2, $true)
Check ($intro.State.ToString() -eq 'Dismissing') '안내 유지 뒤 문구를 지운다'
$intro.Tick(0.15, $true)
Check ($intro.State.ToString() -eq 'Dismissing' -and [Math]::Abs($intro.Progress - 0.5) -lt 0.001) '문구 페이드 중간 단계'
$intro.Tick(0.2, $true)
Check ($intro.State.ToString() -eq 'Finished') '문구가 사라진 뒤 조작을 허용한다'
$intro.Tick(10, $true)
Check ($intro.State.ToString() -eq 'Finished') '완료 후 연출을 재실행하지 않는다'
$zero = [StageIntroTiming]::new(0, 0, 0)
$zero.Tick(100, $true)
Check ($zero.State.ToString() -eq 'Revealing') '긴 프레임에도 공개 단계를 건너뛰지 않는다'
$zero.Tick(100, $true)
Check ($zero.State.ToString() -eq 'Announcement') '길이가 0이어도 로어 신호를 한 번 전달한다'
$zero.Tick(100, $true)
Check ($zero.State.ToString() -eq 'Dismissing') '길이가 0인 문구 단계도 순서를 유지한다'
$zero.Tick(100, $true)
Check ($zero.State.ToString() -eq 'Finished') '모든 길이가 0이어도 정상 종료한다'
Write-Output "시작 연출 타이밍 $script:checks 개 검증 통과"
