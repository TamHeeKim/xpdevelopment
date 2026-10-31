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

위 프리팹은 모두 `Assets/FishingCombat/Prefabs`에 있다. 외형 PNG·선 머티리얼·포획 WAV는 Art 폴더에 있다.
Level의 Point_FirstLeft/Right, Point_SecondLeft/Right가 실제 적 출현 위치다. StageDirector의 Transform 참조를 유지한다.
Point_StartCheckpoint/MiddleCheckpoint는 저장 위치, Point_FirstTrigger/SecondTrigger는 전투 구역 진입 기준이다.
씬에서 수정한 프리팹 인스턴스는 필요한 변경만 Apply한다. 다른 사람과 씬 편집이 겹치면 프리팹으로 변경을 전달하고 필요한 위치에서 unpack한다.
HUD의 문구는 Text 값 대신 LocalizedText.textKey와 CSV를 수정한다. 체력·목표·포획 상태는 연결된 UI의 내용만 런타임 갱신한다.
동적 UI 숫자/상태, 좌우 반전, 카메라 추적 등 게임 동작은 실행 중 갱신된다.

## 현재 게임 규칙

- A/D 이동, W 점프. 좌클릭 공격, 홀드 포획, 놓기 투척, 우클릭 홀드 당기기.
- 공격은 줄 전체를 검사하며 같은 휘두르기에 같은 대상은 한 번만 맞는다. 줄 끝일수록 피해·넉백이 커진다.
- 광클로 휘두르기를 다시 시작하지 않는다. 예약된 다음 공격은 회수 대기를 생략한다.
- 일반 포획 홀드 0.18초, 무거운 적 추가 충전 0.8초. 피격·독 피해로 포획/충전이 취소되지 않고 사망 시 놓는다.
- 잡은 대상과 플레이어는 충돌하지 않는다. 해제 시 원래 충돌 설정 복구, 반사 수리검은 계속 플레이어를 통과한다.
- 내려찍기·투척 보정은 적만 대상으로 하며 투척 속도는 유지한다.
- 일반 오브젝트는 피해로 파괴되지 않는다. 잡지 않은 일반 물체가 y=-12 아래로 떨어지면 최초 위치로 복귀한다.
- 사용한 독가스 병은 소멸. 보라색 연기는 3초 유지, 반경 2 안의 적에게 0.5초마다 8 피해, 플레이어 무피해.
- 적 독가스는 반경 4에서 2초 접근 후 전방 생성, 3초 유지. 독 상태는 접촉 중 갱신, 이탈 후 2초 지속, 0.5초마다 3 피해·이동속도 40% 감소.
- 수리검은 포획·반사 가능하고 적끼리 막히지 않는다. 충돌 또는 수명 만료 시 제거된다.
- E 저장·회복, F9/R 저장 지점부터 재시작. 저장은 아직 체크포인트 번호만 보존하며 전체 월드 스냅샷은 없다.
- 낚시·기술 구매·인벤토리·밸런스 SO 연동은 별도 범위다.

## 스크립트 책임

| 클래스 | 주요 메서드/역할 |
| --- | --- |
| PrototypeBootstrap | Awake: 씬에 저장된 프레임 제한·중력 적용. Update/Reload: 재시작 입력. 자동 씬 생성 없음 |
| StageDirector | Start: 저장 지점 반영. SpawnEncounter/SpawnEnemy: 연결된 적·예고 프리팹 생성. ChooseSpawnPoint: 씬 위치 선택. ResetEncounter: 재도전 |
| PlayerController / PlayerHealth | 이동·점프, ReceiveDamage/Recover: 피격·사망·복귀 |
| PlayerAppearance | 저장된 외형을 기준으로 걷기·반전·피격 표시 |
| FishingRodController / TipContact | StartWhip/UpdateWhip: 공격. Hook/ReleaseHook: 포획·투척. FindAssistTarget: 보정. DrawLine: 프리팹에 저장된 선의 좌표 갱신 |
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
