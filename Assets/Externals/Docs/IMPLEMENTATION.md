# 에디터 기반 전투 구성

## 실행

Unity 6000.3.11f1, 새 Input System과 uGUI 사용.
`낚싯대 전투 > 전투 씬 열기`로 `Scenes/FishingPrototype.unity`를 연 뒤 Play한다.
Play 전부터 플레이어·맵·카메라·호리병·HUD가 씬에 저장되어 있다.
실행 시 빈 씬을 자동 조립하는 RuntimeInitializeOnLoadMethod, new GameObject/AddComponent, OnGUI 경로는 FishingCombat 런타임 코드에서 제거했다.
적 출현·수리검 발사·독가스 방출은 연결된 프리팹을 Instantiate한다. 게임 로직은 정상적으로 Play/빌드 실행 중 동작한다.
기본 OriginalFishingPoC와 Linebreaker는 로컬에만 보관하며 이번 전환과 Git 공유 범위에서 제외한다.

## 어디를 편집하는가

| 대상 | 에셋/씬 항목 | 편집 내용 |
| --- | --- | --- |
| 플레이어 | Prefab_Player | 물리·이동·점프·체력·외형·낚싯줄·포획 효과음 |
| 낚싯대 | Prefab_Rod / Player 안의 중첩 인스턴스 | 외형, 플레이어 기준 위치·각도 |
| 일반/무거운 적 | Prefab_NormalEnemy / Prefab_HeavyEnemy | 체력·질량·콜라이더·외형·AI·공격 프리팹·이름/체력 UI |
| 호리병/돌 | Prefab_Bottle / Prefab_Rock | 외형·물리·반격 독 프리팹 |
| 수리검 | Prefab_Shuriken | 외형·물리·회전 속도 |
| 적/반격 독 | Prefab_EnemyPoison / Prefab_CounterPoison | 색·크기·연기 파츠·지속시간·피해·범위 |
| 출현 예고 | Prefab_SpawnWarning | 경고 외형·색·크기 |
| 맵 | Prefab_Level / Environment_Level | 지형·문·병 배치·체크포인트·출현 위치 |
| 카메라 | Prefab_Camera / Camera_Main | 배경색·시야·SmoothCameraFollow 구도/추적 속도 |
| 진행 시스템 | Prefab_Systems / System_Combat | 출현 프리팹·웨이브 수·간격·씬 참조·중력 |
| 화면 UI | Prefab_Hud / Canvas_CombatHud | Canvas/RectTransform 배치·폰트·크기·색·게이지 |

위 프리팹은 모두 `Assets/Externals/Prefabs`에 있다. 외형 PNG는 `Textures/Combat`, 선 머티리얼은 `Materials`, 포획 WAV는 `Sounds`에 있다. 모두 `Assets/Externals` 기준이다.
Level의 Point_FirstLeft/Right, Point_SecondLeft/Right가 실제 적 출현 위치다. StageDirector의 Transform 참조를 유지한다.
Point_StartCheckpoint/MiddleCheckpoint는 저장 위치, Point_FirstTrigger/SecondTrigger는 전투 구역 진입 기준이다.
씬에서 수정한 프리팹 인스턴스는 필요한 변경만 Apply한다. 다른 사람과 씬 편집이 겹치면 프리팹으로 변경을 전달하고 필요한 위치에서 unpack한다.
HUD의 문구는 Text 값 대신 LocalizedText.textKey와 CSV를 수정한다. 체력·목표·포획 상태는 연결된 UI의 내용만 런타임 갱신한다.
동적 UI 숫자/상태, 좌우 반전, 카메라 추적 등 게임 동작은 실행 중 갱신된다.

## 현재 게임 규칙

- A/D 이동, W 점프. 좌클릭 기본공격, 0.3 + catchHold초 홀드 포획. 완료 후 마우스를 놓아도 포획 유지. E 내려찍기, Q 투척.
- 기본공격은 클릭 지점까지의 직선 영역을 검사하며 같은 공격에 같은 적은 한 번만 attack 피해를 받는다.
- 공격 영역은 0.5초 유지하며 광클로 유지 시간을 초기화하지 않는다.
- 포획 시간은 기본 0.3초에 대상 catchHold를 더한다. 피격·독 피해로 포획을 해제하지 않고 사망 시 놓는다.
- 잡은 대상과 플레이어는 충돌하지 않는다. 해제 시 원래 충돌 설정 복구, 반사 수리검은 계속 플레이어를 통과한다.
- 내려찍기·투척 보정은 적만 대상으로 한다. 일반 투척 속도는 throwSpeed, 수리검은 flySpeed의 2배다.
- 일반 오브젝트는 피해로 파괴되지 않는다. 잡지 않은 일반 물체가 y=-12 아래로 떨어지면 최초 위치로 복귀한다.
- 사용한 독가스 병은 소멸. 보라색 연기는 3초 유지, 반경 2 안의 적에게 0.5초마다 8 피해, 플레이어 무피해.
- 적 독가스는 반경 4에서 2초 접근 후 전방 생성, 3초 유지. 독 상태는 접촉 중 갱신, 이탈 후 2초 지속, 0.5초마다 3 피해·이동속도 40% 감소.
- 수리검은 포획·반사 가능하고 적끼리 막히지 않는다. 충돌 또는 수명 만료 시 제거된다.
- F 저장·회복, F9/R 저장 지점부터 재시작. 저장은 아직 체크포인트 번호만 보존하며 전체 월드 스냅샷은 없다.
- 낚시·기술 구매·인벤토리는 별도 범위다. 공격/포획 SO 연결은 아래 기획 적용 절을 따른다.

## 스크립트 책임

| 클래스 | 주요 메서드/역할 |
| --- | --- |
| PrototypeBootstrap | Awake: 씬에 저장된 프레임 제한·중력 적용. Update/Reload: 재시작 입력. 자동 씬 생성 없음 |
| StageDirector | Start: 저장 지점 반영. SpawnEncounter/SpawnEnemy: 연결된 적·예고 프리팹 생성. ChooseSpawnPoint: 씬 위치 선택. ResetEncounter: 재도전 |
| PlayerController / PlayerHealth | 이동·점프, ReceiveDamage/Recover: 피격·사망·복귀 |
| PlayerAppearance | 저장된 외형을 기준으로 걷기·반전·피격 표시 |
| FishingRodController | BeginPress/BeginAttack/TickAttack: 0.5초 기본공격. SelectCatchTarget/TickCapture: 타겟팅·홀드. BeginSlam/TickSlam: 부채꼴 내려찍기. SelectThrowDirection/ThrowHeld: Q 투척. LateUpdate/DrawLine: 표시 갱신 |
| HookableBody | Awake/Configure: 체력·대상 설정. SetHooked: 포획/충돌. OnCollisionEnter2D: 충돌 피해. Respawn: 사망 처리 |
| TangEnemyAI / Shuriken | 적 공격 상태 및 투사체 생성·반사·명중 |
| PoisonCloud / PoisonBottle / PoisonStatus | 독 영역 효과, 병 충전·반격 프리팹 생성·소멸, 플레이어 잔류 독 |
| CombatHud / BodyHud / LocalizedText | 미리 배치된 Canvas UI의 내용·게이지·CSV 문구 갱신 |
| SmoothCameraFollow / CameraShake | 카메라 추적과 피격 흔들림 |
| CombatText | Get/Format/Parse: CSV 조회·형식 검증·번역 대체 |
| Editor/CombatAuthoring, CombatShapes | 이관용 외형/컴포넌트 생성 도구. 게임 빌드에 포함되지 않음 |
| Editor/CombatSceneBuilder | Build: 없는 프리팹 생성 및 씬 연결. UpgradePlayer: 줄/찌/효과음 이관. Check: 에셋 참조 검사 |

MonoBehaviour를 각각 클래스명과 같은 파일로 분리해 Unity 프리팹 직렬화를 지원한다.
필드는 camelCase, 기존 변경 필드는 FormerlySerializedAs 유지, 인스펙터 항목은 기능별 Header를 사용한다.

## CSV

Resources/Data_CombatText.csv는 UTF-8 key,ko,en 구조. ko는 필수, en은 번역 대기.
쉼표·따옴표·셀 내부 줄바꿈 지원. CombatText.Language로 언어 지정, 빈 번역은 한국어로 대체한다.
가변 문장은 {0} 등의 자리표시자와 CombatText.Format 사용. 표시 이름은 gameObject.name과 분리돼 있다.
밸런스 수치용 SO 연결은 다른 담당자의 작업이며 이 CSV와 별개다.

## 검사 및 이관 도구

- `낚싯대 전투 > 검사 실행`: 전투·CSV 회귀 검사.
- `낚싯대 전투 > 전체 편집 씬 검사`: 프리팹 스크립트·스프라이트·연결 검사.
- `낚싯대 전투 > 전체 프리팹 및 편집 씬 준비`: 최초 이관/누락 에셋 생성용. 이미 만든 프리팹·아트는 덮어쓰지 않으며 씬의 시스템 참조는 다시 연결한다. 일반적인 외형/배치 수정에는 이 메뉴가 필요 없다.

검증 기록: 전투 회귀, 프리팹 직렬화, 에디터 자세 유지, 씬 초기화, 적 출현, 수리검 발사, 병 소모/반격 지속 피해, 플레이어 독 피해를 검사했다.
Canvas와 맵을 함께 렌더링해 표시를 확인했다. 다양한 해상도와 장시간 전투의 조작감은 추가 플레이 검증이 필요하다.

## 미사용 코드 정리

이전 근접 AI와 GroundSurface 마커를 제거했다. 적 종류는 출현 시 일반/무거운 프리팹 선택으로 처리한다. 낚싯대 외형 참조는 FishingRodController에만 둔다. 설정되지 않던 운동량 유지·낚싯대 잠금 분기와 피해 위치 매개변수를 제거했다. PlayerController의 사망 복귀용 입력 잠금은 유지한다. 출현 검사는 ChooseSpawnPoint를 호출해 편집된 마커의 좌표·높이와 동거리 선택을 검증한다.

포획/포획 대기 중 찌 스프라이트를 숨기고, LateUpdate에서 줄 끝을 대상의 렌더링 Transform 위치에 맞춘다. 해제하면 찌 표시를 복원한다. 수리검은 생성 시 배치된 일반 포획 오브젝트의 콜라이더를 무시하며 반사 후에도 유지한다. 지형 충돌과 반사 후 적 피해는 유지한다.

## 데이터 테이블 시트 연결

- 원본: https://docs.google.com/spreadsheets/d/1TyjqkiLVNg_shUpMejZh3StgU6b0RJoVzFeIXk8NIrs/edit
- Unity 메뉴: 낚싯대 전투 > 데이터 테이블 > 시트에서 SO 가져오기. 플레이어(gid=8201)·적(gid=8202)의 모든 행을 CombatStatDefinition SO로 생성/갱신한다.
- SO 위치: Assets/Externals/Data/Stats/Player 및 Enemy. 행 ID별 기존 에셋을 갱신해 GUID와 적의 참조를 유지한다. 시트에서 삭제한 행의 기존 SO는 자동 삭제하지 않는다. ID 변경은 새 에셋으로 취급하므로 적에 새 SO를 할당한다.
- Integration 씬 위치: Assets/Externals/Scenes/Integration.unity. Enemy_TableDummy의 statDefinition에 적 SO를 할당했다. 다른 적에도 Inspector의 스탯 SO 필드에 원하는 적 SO를 드래그한다. Integration 테스트 적 배치 메뉴는 기존 enemyId를 SO 참조로 이관한다.
- Play 시작 시 할당된 SO로 초기화하고, Editor 전용 CombatSheetConnection이 즉시 및 완료 후 5초마다 원본 두 탭을 조회한다. 변경된 값만 SO에 저장하고 Changed 이벤트로 적에게 반영한다. 빌드는 저장된 SO를 사용하며 시트 네트워크 동기화를 포함하지 않는다.
- 연결 중·성공·실패와 변경된 SO의 전체 스탯을 Console에 출력한다. 값이 같으면 저장/스탯 로그/적 초기화를 반복하지 않는다. 통신 실패나 잘못된 헤더·빈 테이블·중복 ID·잘못된 숫자는 기존 SO를 유지하고 재시도한다. 두 테이블의 값 검증을 먼저 완료한 뒤 갱신한다. Play 종료/컴파일 시 진행 중 요청을 취소한다.
- 스크립트는 Assets/Externals/Scripts 아래에 있다. CombatSheetData는 시트 주소·CSV 파싱, CombatStatDefinition/CombatStatValues는 SO와 수치 검증, IntegrationEnemy는 SO 참조·체력·F키, Editor/CombatSheetConnection은 가져오기·주기 갱신·SO 저장을 담당한다.
- 현재 체력은 적 인스턴스에만 저장한다. F키는 10 피해를 주며 0 하한을 적용한다. hp 변경 시 기존 피해량을 유지한다(60/60에서 10 피해 후 시트 hp=80으로 변경하면 70/80). 이미 체력이 0이면 hp 증가로 부활하지 않는다. attack/defense/moveSpeed는 SO의 최신 값으로 조회하며 이 테스트 적에는 이동·공격 AI를 추가하지 않았다.
- Play 중 가져온 원본 스탯은 SO 에셋에 저장되어 종료 후에도 유지된다. 원본 시트에는 쓰지 않는다. CSV 파일은 이전 연결의 참고 스냅샷이며 현재의 데이터 소비 경로는 SO이다. 기존 전투 씬·프리팹·표시 텍스트 CSV는 변경하지 않는다.
- 검사: 낚싯대 전투 > 데이터 테이블 > Integration F키 검사 (Play). 실제 프레임 진행과 F 누름/유지/해제, 체력 0 하한, 변경 없음 처리, SO 참조/GUID 유지, 수치 변경 반영과 피해량 보존, 잘못된 데이터 거부를 검사한다. 테스트 데이터는 로컬 가져오기 경로에만 넣고 원래 SO 값을 복구한다. 원본 시트를 수정하지 않는다.

## 플레이어 전투 기획 적용 (2026-11-07)

- 기본공격은 낚싯대 끝과 클릭 지점을 잇는 전체 폭 attackWidth의 사각 Trigger를 0.5초 활성화한다. 유지 중 진입도 검사하며 적마다 공격당 한 번 attack 피해를 준다. 연타로 진행 중 공격 시간을 초기화하지 않는다.
- 적 타겟팅은 별도 CircleCollider2D를 사용한다. catchRange 안의 커서 대상 우선, 없으면 플레이어와 가장 가까운 적. 일반 오브젝트/수리검은 커서가 본체 위에 있을 때 포획할 수 있다.
- 클릭 시작부터 0.3 + catchHold초를 재며 줄이 도달한 뒤에만 포획을 완료한다. 완료 전 해제·대상 사망·거리 이탈은 취소. 완료 후 해제는 포획을 유지한다. 포획 거리 목표는 catchRange/2이며 줄을 서서히 당겨 맞춘다.
- E는 catchRange*2/3 안 가장 가까운 다른 적의 위치로 내려찍고, 없으면 전방 지면으로 내려찍는다. 잡은 적은 120%, 실제 물리 충돌한 다른 적은 110% 피해. 이전 물리 충돌 피해를 중첩하지 않는다. 생존 시 포획 거리 복원, 사망 시 해제. 오브젝트는 파괴되지 않는다.
- Q는 플레이어 기준 전체 60도, 거리 10 내 적을 탐색한다. 중심축 각도 차이, 동각이면 거리순이며 잡힌 대상 제외. 일반 투척은 현재 운동 속도를 유지하되 최소 속도 12, 수리검은 flySpeed*2와 attack*1.5. 일반 투척의 기존 충돌 피해는 유지한다.
- 기존 이동/점프는 임시 유지하고 회피·스킬은 추가하지 않는다. 저장·회복 키는 E와 충돌하지 않도록 F다. 우클릭 당기기와 마우스 제스처 내려찍기는 새 조작에서 제거했다.

### 시트/SO 연결

FishingRodController.statDefinition은 기존 플레이어 SO의 attack을 읽는다. 플레이어 CSV에 attackWidth, catchRange를 추가하면 가져오기 후 그대로 반영된다. 열이 없거나 값이 0이면 프리팹 기본값(폭 0.6, 포획 거리 7.5)을 사용한다. 적 CSV는 catchHold 추가 열을 지원하고 누락 시 0초다. 기본공격당 피해를 여러 틱으로 반복하는 규칙은 정해지지 않아 1회로 적용했다.

기존 필수 헤더 순서는 유지한다. 추가 열은 이름으로 읽으며 다른 담당자의 추가 열도 보존해 파싱한다. 원본 Google 시트는 수정하지 않았다. 일반/무거운 적은 현재 존재하는 동일 사천당가 SO를 연결했고, 종류별 SO가 생기면 각 프리팹의 statDefinition을 교체한다. 기존 체력/이동속도 전체를 SO로 이전하는 작업은 이번 공격 기획 범위에 포함하지 않는다.

조정값: lineSpeed=24, slamDuration=0.4, 일반 throwSpeed=12. 미정인 발사/연출/일반 투척 속도는 Inspector에서 편집한다. 수리검 flySpeed/attack도 Prefab_Shuriken에서 편집한다.

PlayerCombatSetup.UpgradePrefabs는 편집용 공격 콜라이더, 적 타겟 원, SO 참조를 프리팹에 저장한다. 게임 중 오브젝트를 조립하지 않는다. PlayerCombatChecks는 현재 규칙의 경계/피해/홀드/시트 호환을 검사하고 기존 CombatChecks.Run에서 함께 실행한다.

## PoC 물리 감각 복원 (2026-11-07)

새 판정/입력 규칙은 유지하면서 곡선 전개(기존 0.42초 휩), 24점 곡선 낚싯줄과 처짐을 복원했다. 직선 기본공격 영역은 0.5초/attackWidth 규칙을 유지하며 가이드선은 표시하지 않는다.

포획 시 Rigidbody2D를 Kinematic으로 바꾸거나 위치/속도를 덮어쓰지 않는다. 잡힌 대상은 마우스를 따라 spring 힘과 줄의 장력으로 움직인다. ropeLength는 실제 거리에서 시작해 reelSpeed로 catchRange/2를 향해 바뀐다. swingForce=27, holdDamping=4이며 기존 거리 초과 장력 95와 플레이어 반작용 0.08을 사용한다.

E 내려찍기는 부채꼴 목표를 향해 힘을 가한다. HeldContact로 적/지형과 실제 충돌했을 때만 120%/110% 피해를 1회 적용한다. 지형을 통과하는 위치 이동은 없고, 충돌 없이 빗나가면 피해 없이 포획 유지 상태로 돌아간다. 생존 후 거리 복원 역시 물리로 서서히 진행한다. 일반 Q 투척은 조준 보정을 적용하면서 흔들던 속도를 유지하며 최소 throwSpeed를 보장한다. 수리검의 2배 속도/1.5배 피해는 유지한다.

검증: 포획 시 위치/속도/동적 몸체 유지, 충돌 전 무피해, 충돌 중복 피해 방지 회귀 검사 통과. 실제 입력 Play에서 물리 거리 수렴·마우스 이동에 따른 가속·E 충돌 피해·Q 및 수리검을 확인하고 곡선 전개/포획 화면을 렌더링했다. 장시간 직접 플레이의 감각 비교는 별도 확인이 필요하다.


## 렌더링 설정 위치 (2026-11-13)

URP 설정은 `Assets/Settings`에 둔다. 에셋과 .meta를 함께 옮겨 기존 참조를 유지했다.
