# (Windows PowerShell 5.1이 한글을 읽도록 UTF-8 BOM으로 저장)
# 측정 하네스 반복 실행 (계획서 0-5 · Phase 3) — 개발 빌드를 정책별로 여러 번 돌린다.
#  사용: powershell -ExecutionPolicy Bypass -File run_harness.ps1 -Exe <빌드 경로\OperationKivotos.exe> [-Policies Sector,Distance] [-Runs 3]
#  무인: -harness면 StartScene · SelectScene(새로하기 = 매번 깨끗한 세이브)을 자동으로 넘긴다 — 세이브는 에디터와 같은 persistentDataPath
#  순서: 1회차에 정책 7개 → 2회차에 7개 → ... (같은 정책을 연달아 돌리지 않아 발열 · 백그라운드 변화가 한 정책에 몰리지 않게)
#  결과: 빌드 폴더의 MetricsLogs\field_* · activation_* (실행마다 시각이 다른 파일)
#  분석: python analyze_field.py <같은 정책 3개의 _frames.csv>  → 3회 편차
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string[]]$Policies = @('Sector', 'AdjacentSector', 'Spotlight3x3', 'Spotlight5x5', 'Distance', 'SpotlightDistance', 'CullingGroup'),
    [int]$Runs = 3,
    [int]$Shadows = 0,
    [int]$Width = 1920,
    [int]$Height = 1080,
    [int]$TimeoutMinutes = 15
)

if (-not (Test-Path $Exe)) { Write-Error "실행 파일 없음: $Exe"; exit 1 }

$total = $Runs * $Policies.Count
$n = 0
for ($run = 1; $run -le $Runs; $run++) {
    foreach ($policy in $Policies) {
        $n++
        $argList = @('-harness', '-policy', $policy, '-shadows', $Shadows,
                  '-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0')
        Write-Host ("[{0}/{1}] {2:HH:mm:ss} 회차 {3} · {4}" -f $n, $total, (Get-Date), $run, $policy)

        $p = Start-Process -FilePath $Exe -ArgumentList $argList -PassThru
        if (-not $p.WaitForExit($TimeoutMinutes * 60 * 1000)) {
            Write-Warning "  $TimeoutMinutes분 초과 — 강제 종료 (이 실행의 로그는 저장 안 됐을 수 있음)"
            $p.Kill()
        }
        else {
            Write-Host ("  종료 코드 {0}" -f $p.ExitCode)
        }
    }
}
Write-Host "끝 — MetricsLogs 폴더를 확인"
