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
조작: A/D 이동, 스페이스바 점프, 좌클릭 공격·유지 포획, E 내려찍기, Q 투척, F 저장·회복.

## 에셋 폴더

게임 에셋은 `Assets/Externals` 아래에서 종류별로 관리하며, URP 렌더링 설정은 `Assets/Settings`에 둡니다.

- `Scripts` / `Scripts/Editor`: 런타임 코드 / 에디터 도구·검사
- `Scenes`, `Prefabs`: 편집 씬과 프리팹
- `Textures/Combat`, `Materials`, `Sounds`: 전투 이미지·머티리얼·효과음
- `Data/Stats`: 스프레드시트에서 가져온 스탯 SO
- `Resources`: 이름으로 로드하는 CSV와 UI 폰트
- `Models`, `Shaders`: 모델·셰이더

`.meta` 파일을 함께 이동해 GUID를 유지합니다. `Resources` 내부 파일 이름은 런타임 로딩 키이므로 변경하지 않습니다.


## 사천당가 적 기획 적용

`feature/enemy`는 `feature/player`의 작업 상태에서 분기했습니다. 기존 player → develop PR은 별도로 유지합니다.

- `Prefab_PoisonEnemy`: 감지 → 공격 범위까지 추적 → 제자리 준비 → 전방 독가스 분사 → 회복 대기. 분사 시작부터 gasRange 전체에 판정을 적용하고, 범위 안의 연기만 앞으로 흐릅니다. 준비 중과 발사 직전에 플레이어 쪽으로 외형·공격 지점을 맞추고, 독가스 분사가 시작된 뒤에는 방향을 고정합니다.
- `Prefab_ShurikenEnemy`: 최소·최대 거리 경계를 포함해 공격하며, 가까우면 후퇴하고 멀면 접근합니다. 준비가 끝나는 시점의 플레이어 위치로 수리검을 던집니다.
- 두 적의 `Point_AttackOrigin`에서 공격하며, `attackVisual`의 들어 올리기·회전과 노란색 점멸로 준비를 표시합니다. 현재 적 외형은 기존 스프라이트를 재사용합니다.
- 독가스병은 최초 detectionRange 진입 후 행동을 지속하며, 수리검병은 거리 유지 조건으로 즉시 행동합니다. 포획·투척·죽음은 준비/분사를 중단합니다.
- 수리검은 중력 없이 직진하고 적 수리검은 다른 적/일반 오브젝트를 통과합니다. 화면 밖으로 나가면 제거합니다.
- 손이 비어 있고 수리검 위 Q 배지가 보일 때 Q를 누르면 가까운 수리검 하나를 잡아 반격합니다. 잡는 순간 청록색, 되던지는 순간 자홍색으로 표시합니다. 모션 종료 후 발사한 적을 향해 flySpeed × 1.5, attackDamage × 2로 되던집니다. 발사자가 사라졌으면 가장 가까운 살아 있는 적, 적이 없으면 들어온 반대 방향을 사용합니다.
- 이미 잡은 대상이 있으면 Q는 기존 투척을 우선합니다. 좌클릭으로 잡은 수리검의 기존 수동 투척(속도 2배, 피해 1.5배)은 유지합니다.
- 독가스 접촉 시 PoisonStatus.status는 poisoning, 만료 시 normal입니다. 접촉 갱신은 피해 틱을 초기화하지 않습니다. refreshDurationOnContact가 꺼져 있으면 같은 가스 안에 머무는 동안 다시 적용하지 않고, 벗어났다가 재진입할 때 갱신합니다.
- 기존 호리병의 독 흡수·보라색 반격 연기 기능은 유지합니다. 사천당가 보스는 행동 기획이 없어 구현하지 않았습니다.

### 임시 밸런스 값과 편집 위치

기획에 숫자가 없던 항목은 아래 기본값으로 두었습니다. 새 행동 수치는 프리팹 인스펙터에 있으며 원본 스프레드시트는 수정하지 않았습니다.

| 위치 | 값 |
| --- | --- |
| 적 TangEnemyAI | detectionRange 12, attackWindupTime 0.6초, attackRecoveryTime 1.5초 |
| 독가스병 TangEnemyAI | attackRange 4, sprayDuration 2초, gasRange 4 |
| 수리검병 TangEnemyAI | minKeepDistance 3, maxKeepDistance 5 |
| EnemyPoison의 PoisonCloud | poisonDuration 2초, damageTickInterval 0.5초, damagePerTick 3, moveSpeedReduction 0.4(40%), refreshDurationOnContact 켬 |
| Shuriken | flySpeed 7, damage 12, attackDamage 12(수리검 반격 피해 기준) |
| Player의 FishingRodController | shurikenCounterRange 2.5, shurikenCounterMotionTime 0.3초 |

적 체력(생성 시)·이동속도·추가 포획 시간은 각각 연결된 SO를 사용합니다. 독가스병은 ENEMY_CH01_DANGGA_001, 수리검병은 ENEMY_CH01_DANGGA_002입니다. 새 행동 수치를 시트로 옮길 때 위 필드 이름과 비율 단위를 기준으로 연결하면 됩니다.

적 체력은 추가 배율 없이 SO 값을 그대로 사용합니다. 현재 독가스병 75, 수리검병 80입니다. 독가스의 사각형 배경 표시는 끄고 연기 파츠만 표시합니다.

중독 피해는 프레임이 지연돼도 만료 전까지 도래한 틱을 합산합니다. 접촉 갱신 전의 밀린 틱은 기존 피해값으로 처리하며 이미 적용한 틱은 반복하지 않습니다.
