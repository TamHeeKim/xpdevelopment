# XPproject

Unity 6000.3.11f1의 횡스크롤 낚싯대 액션 프로토타입입니다.

## 실행

Unity Hub에서 이 폴더를 열고 `낚싯대 전투 > 전투 씬 열기`를 선택한 뒤 Play합니다.
실행 씬: `Assets/Externals/Scenes/FishingPrototype.unity`.

## 개발

- [팀 규칙](AGENTS.md)
- [브랜치·PR·커밋 흐름](Docs/TeamWorkflow.md)
- [1차 스프린트](Docs/Sprint-01.md)
- [게임 텍스트 CSV](Assets/Externals/Resources/Data_CombatText.csv)

처음 복제한 뒤 `git config core.hooksPath .githooks`를 실행합니다.
검사 메뉴: `낚싯대 전투 > 검사 실행`.
플레이어 공격/포획은 스탯 SO를 읽으며, 표시 문구는 Data_CombatText.csv에서 관리합니다.
조작: A/D 이동, W 점프, 좌클릭 채찍, 우클릭 연결 → 다시 우클릭 당기기, 포획 후 좌클릭 지상 내려찍기 / 우클릭 투척, R 현재 맵 재시작.

## 에셋 폴더

게임 에셋은 `Assets/Externals` 아래에서 종류별로 관리하며, URP 렌더링 설정은 `Assets/Settings`에 둡니다.

- `Scripts` / `Scripts/Editor`: 런타임 코드 / 에디터 도구·검사
- `Scenes`, `Prefabs`: 편집 씬과 프리팹
- `Textures/Combat`, `Materials`, `Sounds`: 전투 이미지·머티리얼·효과음
- `Data/Stats`: 스프레드시트에서 가져온 스탯 SO
- `Resources`: 이름으로 로드하는 CSV와 UI 폰트
- `Models`, `Shaders`: 모델·셰이더

`.meta` 파일을 함께 이동해 GUID를 유지합니다. `Resources` 내부 파일 이름은 런타임 로딩 키이므로 변경하지 않습니다.


## 2026-11-18 개정 전투 기획

- [플레이어 기획서](https://app.notion.com/p/41b52b58f2b0801bb486c50ed70cc4bf)
- [사천당가 적 기획서](https://app.notion.com/p/41b52b58f2b08078b81fde508a85ed7e)

### 플레이어

기본 채찍은 최소·최대 거리로 전개 거리를 제한하고, 거리 비율로 전체 공격 시간을 보간합니다. 준비 시간은 고정이며 전개·회수 시간이 변합니다. 완전 전개 시 낚싯줄 전체의 캡슐 영역을 한 번 검사합니다. 타격 후 들어온 적은 맞지 않으며 연속 클릭으로 공격이 초기화되지 않습니다. 공격 종료 전에는 다른 낚싯대 기술을 시작할 수 없습니다.

포획은 우클릭 연결 → 새 우클릭으로 당기기 → 기본 포획 위치 도착 순서입니다. 홀드 시간과 진행도 게이지는 없습니다. 최초 catchRange 내에서 커서가 본체/타겟팅 콜라이더 안에 있는 대상만 선택합니다. 본체까지 최단 거리를 비교하고, 겹친 본체는 SpriteRenderer의 정렬 레이어·순서와 카메라 깊이를 비교합니다. 현재 직교 2D 카메라 기준이며 향후 원근 2.5D 전환 시 화면 공간 투영 판정을 조정해야 합니다.

포획 완료 전 좌클릭, 연결 후 입력 시간 초과, 화면 이탈, 물리 피격, 대상 제거 시 취소합니다. 연결 후 catchRange 이탈은 취소하지 않습니다. 이동·점프·독 피해는 취소하지 않으며, 완료된 포획은 물리 피격에도 유지합니다. 취소 클릭은 기본 공격을 동시에 발생시키지 않습니다.

포획 대상은 holdDistance/holdHeight의 상대 위치를 유지합니다. 바라보는 방향 변경 시 반전하지 않습니다. 내려찍기는 지상 좌클릭이며 주변 적만 slamRange로 보정합니다. 잡힌 적 120%, 맞은 적 110% 피해 후 살아 있으면 기본 포획 위치로 돌아옵니다. 수리검은 내려찍기 대상이 아닙니다.

우클릭 투척은 낮은 포물선으로 목표 위치를 보정합니다. 최초 적/지형 충돌만 피해를 주며 살아 있는 적은 착지 후 행동을 재개합니다. 오브젝트는 canCatch/breakOnSlam/breakOnThrow로 설정하고 체력으로 파괴하지 않습니다. 기존 독가스 호리병의 흡수·보라색 반격 연기와 사용 후 소멸은 유지합니다.

수리검은 커서 타겟팅 후 우클릭 한 번으로 즉시 포획합니다. 이후 새 좌클릭 또는 우클릭으로 속도 2배, 피해 1.5배로 던집니다. Q 자동 반격과 Q 배지는 제거했습니다.

### 적과 중독

독가스병은 detectionRange 진입 후 추적 → 준비 → 고정 방향 분사 → 대기를 반복합니다. 수리검병은 최초 maxKeepDistance 진입 전 대기하고, 이후 최소·최대 거리 사이를 유지합니다. 준비 이후 범위를 벗어나도 공격을 계속하며 수리검은 발사 순간의 위치를 조준합니다.

기본 채찍 피격은 hitStunTime 동안 행동과 시간 진행을 보존하여 정지합니다. 추가 피격은 경직을 연장하지 않고 넉백도 없습니다. 낚싯줄 연결은 행동을 초기화하고 독가스 분사를 중단합니다. 이미 발사된 수리검과 적용된 중독은 유지합니다. 적 간격 조정은 일반 이동 중에만 적용합니다.

중독 상태는 poison/normal입니다. 중독 피해는 일반 피격 무적·hitLock을 발생시키거나 영향을 받지 않습니다. 감속은 moveSpeed × (1 - moveSpeedReduction)이며 중첩하지 않습니다. refreshDurationOnContact가 false면 중독 중 재접촉해도 갱신하지 않습니다. 만료 후 계속 접촉하면 새 중독을 적용합니다. 프레임 지연 시 만료 전 틱을 합산합니다.

### 데이터와 임시값

플레이어 및 적의 CombatStatDefinition을 기존 시트 가져오기에서 갱신합니다. 새 열이 없는 기존 시트도 명시적인 임시 기본값으로 읽습니다. hp/attack/defense/moveSpeed 및 원래 값은 보존하며 방어력 피해 공식은 기획 미정이라 적용하지 않습니다. SO가 연결된 새 필드는 SO 값이 인스펙터 기본값보다 우선합니다.

| 위치 | 추가 필드와 임시 기본값 |
| --- | --- |
| 플레이어 SO | minAttackRange 2, maxAttackRange 10, minAttackTime 0.35초, maxAttackTime 0.8초 |
| 플레이어 SO | catchInputTimeout 2초, holdDistance 2.5, holdHeight 1, slamRange 4, throwAngle 60, throwRange 10 |
| 플레이어 SO | jumpPower 10.5, invulnTime 0.5초, hitLock 0.15초 |
| 적 SO | hitStunTime 0.18초, detectionRange 12, attackRange 4, attackWindupTime 0.6초, attackRecoveryTime 1.5초, sprayDuration 2초, gasRange 4, minKeepDistance 3, maxKeepDistance 5 |
| 낚싯대 프리팹 | lineSpeed 24, pullSpeed 12, throwSpeed 12, throwArcHeight 0.7, throwRecoveryTime 0.2초, thrownSelfDamage 12, thrownHitDamage 11 |
| 적 프리팹 | separationDistance 1.2, separationSpeed 1.5 |
| 독가스 프리팹 | damagePerTick 4, damageTickInterval 0.5초, poisonDuration 4초, moveSpeedReduction 0.4, refreshDurationOnContact 켬 |
| 수리검 프리팹 | attackDamage 20, flySpeed 10 |

holdDistance/slamRange는 catchRange와 독립적입니다. catchHold, shurikenCounterRange, shurikenCounterMotionTime은 더 이상 사용하지 않습니다. 독가스·수리검·오브젝트의 별도 테이블 가져오기는 데이터 담당자가 기존 프리팹 필드와 연결할 수 있도록 유지했습니다. 원본 온라인 시트는 수정하지 않았습니다.

미정인 기술 장착 UI, 추가 기술, 회피, 보스, 방어력 공식은 구현 범위에서 제외합니다. 기존 Build.zip은 이번 소스 변경과 별개인 이전 빌드입니다.


## WF 아트와 ProPixelizer

- 아트 패키지: `Assets/Externals/Models/WF` (원본 GUID 유지).
- 데모 씬: `Assets/Externals/Models/WF/WF.unity`. 기존 전투 씬의 플레이어/적 모델 교체는 별도 작업입니다.
- ProPixelizer 2.0.3: `WF/Addon/ProPixelizer`. 외부 패키지 내부 구조는 유지하고 Utils.PackageLocation만 배치 경로에 맞췄습니다.
- `Assets/Settings/PC_Renderer.asset`의 SSAO 다음에 ProPixelizer Render Feature 등록. PC_RPAsset의 MSAA 비활성, Depth Priming 비활성, Render Graph 활성 설정을 사용합니다. Mobile 렌더러에는 적용하지 않았습니다.
- 픽셀 효과는 제공된 ProPixelizer 머티리얼/카메라 설정을 사용합니다. 기능 등록만으로 기존 게임의 모델이나 애니메이션이 교체되지는 않습니다.


### 전투 씬 아트 적용 (2026-11-23)

`FishingPrototype.unity`에서 WF 배경과 FisherMan 모델을 사용합니다. 배경은 `Prefab_WFBackground.prefab`, 플레이어 외형은 `Prefab_Player.prefab/Model_FisherMan`에서 편집합니다. Level 프리팹에서 길과 소품, 맵 입구·출구를 편집합니다. 이전 전투 게이트와 고정 웨이브 마커는 제거했습니다.

플레이어는 기존 Rigidbody2D/Collider2D를 유지하며 3D 모델은 외형만 담당합니다. 모델에 애니메이션 클립이 없어 기본 자세로 이동하며 좌우 회전, 피격 깜빡임, 사망/부활 표시를 연결했습니다. Z 스케일은 1로 수정하고 모델 아래에서 비율을 보정합니다. WF 배경은 전투 구간에 맞춰 반복 배치하고 하나의 넓은 수면에서 실시간 물 반사를 사용합니다. 원본 WF 데모는 그대로 유지합니다.

외형 회귀 검사: `낚싯대 전투 > 플레이어 아트 연결 검사`.

카메라는 Perspective, 시야각 60도, Z=-10으로 설정합니다. 커서는 카메라 레이와 전투 평면 Z=0의 교점으로 변환합니다. 전투 배경은 원본 Water 머티리얼과 PlanarReflection, Volume_WF 후처리, 카메라 전경 식생을 사용합니다. 수면 자체는 반사 대상에서 제외합니다. 카메라 높이는 지형 이동량에 맞춘 -0.775이며 원본 씬의 지면 대비 구도를 유지합니다.

독가스병·수리검병도 WF Enemy 모델을 사용합니다. 각 적 프리팹의 Model_Enemy에서 외형을 편집하며, 기존 크기 차이·독주머니·공격 지점·2D 충돌은 유지합니다. 모델은 기본 자세이며 방향 회전, 피격 깜빡임, 사망/부활 표시를 연결했습니다. 이름/체력바를 모델 머리 위로 배치했습니다.


## 연속 진행 프로토타입

- `FishingPrototype`의 오른쪽 출구를 지나면 `Road_02`, 다시 오른쪽 끝에 도달하면 `FishingPrototype`으로 이동합니다. 동일한 WF 배경을 재사용하는 두 씬을 반복하며 HUD의 맵 번호는 계속 증가합니다. 적을 모두 처치할 필요는 없습니다.
- 입구 X=-8, 출구 X=44. `Prefab_Level`의 `Point_MapEntry`/`Point_MapExit`에서 편집합니다. 다음 씬은 각 씬의 StageDirector.nextScene과 Build Settings에 연결합니다.
- 적은 플레이어 앞 14유닛에서 예고 1.2초 후 나타나며, 출현 사이에 2초 대기합니다. 독가스병/수리검병을 번갈아 출현시키고 동시 생존은 최대 4명입니다. 플레이어가 예고 지점에 접근하거나 지나면 새 위치에서 다시 예고합니다. 출구 근처에는 강제로 출현시키지 않습니다. 뒤로 20유닛 이상 멀어진 적은 포획/투척 중이 아니면 정리합니다.
- 다음 맵에는 현재 체력을 이어가고 포획/투사체/독가스는 이전 씬과 함께 정리합니다. 사망하면 현재 맵 입구에서 체력을 회복하고 출현을 다시 시작합니다. R은 현재 맵 재시작이며 기존 F 저장·회복/F9 불러오기는 제거했습니다. 이전 TangCheckpoint 저장값은 사용하지 않습니다.
- 돌·호리병은 플레이어 충돌만 제외합니다. 포획·던지기·지형 및 적 충돌은 유지하며, 잡았다 놓아도 플레이어 이동을 막지 않습니다. 프리팹의 `blocksPlayerMovement`에서 조정합니다.

독가스병·수리검병도 플레이어와 몸체 충돌을 제외해 서로 밀지 않고 통과합니다. 공격 피해·피격 넉백·포획·적끼리의 충돌/간격 유지는 기존대로 동작합니다.
