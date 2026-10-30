# 전투 구현 안내

## 실행과 범위

Unity 6000.3.11f1, 새 Input System 사용. `낚싯대 전투 > 전투 씬 열기`로 FishingPrototype 씬을 실행한다.
전투 오브젝트는 PrototypeBootstrap에서 생성한다. 기본 OriginalFishingPoC와 Linebreaker는 수정 대상이 아니다.
현재 버전은 1~6번 범위의 프로토타입이며, 저장은 체크포인트 번호만 보존한다. 전체 월드 스냅샷은 아직 없다.

## 현재 규칙

- A/D 이동, W 점프. 좌클릭 공격, 홀드 포획, 놓기 투척, 우클릭 홀드 당기기.
- 공격은 줄 전체를 검사하고 한 번의 휘두르기에 같은 대상은 한 번만 맞는다. 줄 끝일수록 피해·넉백이 커진다.
- 휘두르기는 광클로 재시작하지 않는다. 예약된 다음 공격은 회수 대기를 생략한다.
- 일반 포획 홀드 0.18초, 무거운 적 추가 충전 0.8초. 피격·독 피해로 포획이나 충전이 취소되지 않는다. 사망 시 놓는다.
- 잡은 대상과 플레이어는 충돌하지 않는다. 놓으면 이전 충돌 설정 복구. 반사 수리검은 플레이어를 계속 통과한다.
- 내려찍기·투척 보정 대상은 적만 가능. 던지기는 실제 속도를 유지해 방향만 보정한다.
- 일반 오브젝트는 피해로 파괴되지 않는다. 잡지 않은 일반 오브젝트가 y=-12 아래로 떨어지면 최초 위치로 복귀한다.
- 사용한 독가스 병은 소멸하고 보라색 반격 연기가 3초 남는다. 반경 2에서 0.5초마다 적에게 8 피해, 플레이어는 무피해.
- 적 독가스는 반경 4에서 접근 시작, 2초 추적 후 정지·전방 생성, 3초 유지. 접촉 중 독 상태 갱신, 벗어난 뒤 2초 지속, 0.5초마다 3 피해·이동속도 40% 감소.
- 수리검은 포획·반사 가능하고 적끼리 막히지 않는다. 충돌 또는 수명 만료 시 제거된다.
- E 저장·회복, F9/R 저장 지점부터 재시작. 사망 재도전에서는 소모한 병을 복구하지 않는다.
- 낚시·기술 구매·인벤토리·밸런스 SO 연동은 현재 구현 범위 밖이다.

## 파일과 주요 메서드

| 파일/클래스 | 주요 메서드와 책임 |
| --- | --- |
| PrototypeBootstrap | CreatePrototype/Build: 대상 씬에서만 생성. CreatePlayer/CreateFishingRod/CreateHookable/CreateVisual: 런타임 구성. Reload: 씬 다시 읽기 |
| PlayerController | Update: 입력. FixedUpdate: 이동·점프·독 둔화. IsGrounded: 발밑 판정 |
| FishingRodController | Update/FixedUpdate: 입력·상태 전환. StartWhip/UpdateWhip/UpdateReturn: 공격·회수. SweepWhipCurve/HitTarget: 타격. Hook/ReleaseHook/Interrupt: 포획·해제. FindAssistTarget: 적 보정 대상 선택. DrawLine: 줄 표시 |
| HookableBody | Configure: 대상 설정. ReceiveDamage: 적 체력. SetHooked: 포획과 충돌 설정. OnCollisionEnter2D: 충돌 피해. Respawn: 사망 처리. SetDisplayNameKey: 표시 키 변경 |
| TangCombat / TangEnemyAI | FixedUpdate: 독가스 접근·수리검 공격 상태. Move: 수평 이동 |
| TangCombat / PoisonCloud | Spawn: 적/반격 독 생성. FixedUpdate: 효과·주기 피해·수명. CreateSmoke/AnimateSmoke: 보라색 연기 |
| TangCombat / PoisonBottle | Fill: 독 충전. OnCollisionEnter2D: 반격 연기 생성 후 병 소멸 |
| TangCombat / Shuriken | Reflect/ConfigureEnemy: 반사와 충돌 필터. Update: 수명. OnCollisionEnter2D: 명중 |
| StageGameplay / PlayerHealth | ReceiveDamage: 체력 감소. Recover: 사망·체크포인트 복귀. Heal/SetCheckpoint: 저장 지점 처리 |
| StageGameplay / StageDirector | SpawnEncounter: 예고·순차 등장. ResetEncounter: 재도전. SpawnEnemy: 사천당가 적 생성 |
| StageGameplay / EnemyCombatAI | 기존 근접 적 구현. 현재 StageDirector는 TangEnemyAI를 사용함 |
| CombatAimAssist | CanRedirect/RedirectThrow: 투척 각도 제한·속도 보존 |
| CombatHud | OnGUI: CSV 기반 HUD. Update: 도움말·표시 대상 갱신 |
| CombatShapes / PlayerAppearance | 외형 생성 및 걷기·피격 표시. 내부 자식 이름과 Find 참조를 같이 변경할 것 |
| CameraSupport | SmoothCameraFollow.LateUpdate: 추적. CameraShake.Pulse/LateUpdate: 흔들림 |
| CombatText | Get/Format: 번역 키 조회·문장 조합. Parse: CSV 파싱·검증 |
| Editor/CombatChecks | Run: 전투와 CSV 회귀 검사 |

## 이름과 직렬화

필드는 camelCase이며 변경된 공개 필드에는 FormerlySerializedAs가 있다. 기존 값을 보존하려면 이를 임의로 지우지 않는다.
클래스·메서드·프로퍼티는 C# PascalCase를 유지한다.
생성 오브젝트는 형식_이름이며 UI는 gameObject.name 대신 HookableBody.DisplayName을 쓴다.
공개 설정 필드는 런타임 부트스트랩 호출부가 사용하므로 일괄 private 전환하지 않았다. 인스펙터는 기능별 Header로 구분한다.

## 텍스트 CSV

경로: Resources/Data_CombatText.csv. UTF-8, 첫 행은 key,ko,en이다. key는 고유하며 ko는 필수다.
CSV 셀의 쉼표·따옴표·실제 줄바꿈을 지원한다. 스프레드시트에서 내보낸 CSV를 그대로 사용할 수 있다.
`CombatText.Language = "en"`으로 언어를 지정할 수 있다. 빈 번역/지원하지 않는 언어는 ko로 대체한다. 현재 en 열은 번역 대기 상태다.
문장에 값이 들어가면 `{0}`, `{1}` 같은 자리표시자와 CombatText.Format을 사용한다.
잘못된 열 수·빈 키·중복 키·미완성 따옴표는 오류 처리한다. 실행 중 CSV 편집은 Play를 다시 시작해 반영한다.
이는 표시 텍스트 전용이며 밸런스 수치 SO 연결을 대신하지 않는다.

## 검증

`낚싯대 전투 > 검사 실행` 또는 배치의 `-executeMethod FishingPrototype.Editor.CombatChecks.Run`.
검사는 투척 상태, 보정, 광클 방지, 충돌 필터, 독가스, 반격 연기, CSV 및 표시 이름 분리를 포함한다.
실제 타격감·UI 겹침·포획 조작은 플레이 검증이 추가로 필요하다.
