# (Windows PowerShell 5.1이 한글을 읽도록 UTF-8 BOM으로 저장)
# 측정 하네스 반복 실행 (계획서 0-5 · Phase 3) — 개발 빌드를 정책별로 여러 번 돌린다.
#  사용: powershell -ExecutionPolicy Bypass -File run_harness.ps1 -Exe <빌드 경로\OperationKivotos.exe> [-Policies Sector,Distance] [-Runs 3] [-Densities 1,2,4,8]
#  밀도: -Densities = 스포너당 마릿수 배율(게임 -density). 생략하면 1(기존과 같음). 파일 이름에 _x2 · _x4 …가 붙는다(×1은 없음)
#  끄기 실험: -Offs none,bt,anim,hpbar,avoid = 실행마다 하나씩(게임 -off). 한 실행에 여러 항목은 bt+anim. 파일 이름에 _off-bt …(none은 없음)
#   예) 갱신 LOD 0단계: -Policies Distance -Densities 4 -Offs none,bt,anim,hpbar,avoid -Runs 3  → 15회
#  갱신 LOD: -Lods 0,1 = 끔 · 켬 (게임 -lod). 생략하면 1(게임 기본값). 파일 이름에 _lod(켬만)
#   예) 밀도 스윕: -Policies Distance -Densities 1,2,4,8 -Lods 0,1 -Runs 1  → 8회
#  무인: -harness면 StartScene · SelectScene(새로하기 = 매번 깨끗한 세이브)을 자동으로 넘긴다 — 세이브는 에디터와 같은 persistentDataPath
#  순서: 1회차에 (밀도 × 끄기 × LOD × 정책) 전부 → 2회차에 전부 → ... (같은 조합을 연달아 돌리지 않아 발열 · 백그라운드 변화가 한 조합에 몰리지 않게)
#  결과: 빌드 폴더의 MetricsLogs\field_* · activation_* (실행마다 시각이 다른 파일)
#  분석: python analyze_field.py <같은 정책 3개의 _frames.csv>  → 3회 편차
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string[]]$Policies = @('Sector', 'AdjacentSector', 'Spotlight3x3', 'Spotlight5x5', 'Distance', 'SpotlightDistance', 'CullingGroup'),
    [int]$Runs = 3,
    [string[]]$Densities = @('1'),
    [string[]]$Offs = @('none'),
    [string[]]$Lods = @('1'),
    [int]$Shadows = 0,
    [int]$Width = 1920,
    [int]$Height = 1080,
    [int]$TimeoutMinutes = 15
)

if (-not (Test-Path $Exe)) { Write-Error "실행 파일 없음: $Exe"; exit 1 }

# -File로 부르면 "-Policies Sector,Distance"가 문자열 하나로 들어온다 → 쉼표로 나누고 이름을 검사한다.
#  (10-10: 나누지 않은 "Sector,Distance"를 게임이 플래그로 합쳐 Distance로 3회 돌았다)
$valid = @('Sector', 'AdjacentSector', 'Spotlight3x3', 'Spotlight5x5', 'Distance', 'SpotlightDistance', 'CullingGroup')
$Policies = @($Policies | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($p in $Policies) { if ($valid -notcontains $p) { Write-Error "알 수 없는 정책: $p (가능: $($valid -join ', '))"; exit 1 } }

# 밀도도 같은 이유로 쉼표를 나눈다. 게임 쪽 범위(HarnessArgs.MaxDensity)와 같게 1~16
$Densities = @($Densities | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($d in $Densities) {
    $v = 0
    if (-not [int]::TryParse($d, [ref]$v) -or $v -lt 1 -or $v -gt 16) { Write-Error "밀도는 1~16 정수: $d"; exit 1 }
}

# 끄기 항목 — 쉼표는 실행 구분, +는 한 실행 안의 여러 항목. 이름은 게임 쪽(HarnessArgs.CostOffTokens)과 같게
$validOff = @('none', 'bt', 'anim', 'hpbar', 'avoid')
$Offs = @($Offs | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($o in $Offs) {
    foreach ($t in ($o -split '\+')) { if ($validOff -notcontains $t) { Write-Error "알 수 없는 끄기 항목: $t (가능: $($validOff -join ', '))"; exit 1 } }
}

$Lods = @($Lods | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($l in $Lods) { if (@('0', '1') -notcontains $l) { Write-Error "LOD는 0 또는 1: $l"; exit 1 } }

# 한 회차 안의 조합 (밀도 × 끄기 × LOD × 정책)
$combos = @()
foreach ($density in $Densities) { foreach ($off in $Offs) { foreach ($lod in $Lods) { foreach ($policy in $Policies) {
    $combos += , @($density, $off, $lod, $policy)
} } } }

$total = $Runs * $combos.Count
$n = 0
for ($run = 1; $run -le $Runs; $run++) {
    foreach ($c in $combos) {
        $density, $off, $lod, $policy = $c
        $n++
        $argList = @('-harness', '-policy', $policy, '-shadows', $Shadows, '-density', $density, '-off', $off, '-lod', $lod,
                  '-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0')
        Write-Host ("[{0}/{1}] {2:HH:mm:ss} 회차 {3} · ×{4} · 끔 {5} · LOD {6} · {7}" -f $n, $total, (Get-Date), $run, $density, $off, $lod, $policy)

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
