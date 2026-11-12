# XPproject

Unity 6000.3.11f1의 횡스크롤 낚싯대 액션 프로토타입입니다.

## 실행

Unity Hub에서 이 폴더를 열고 `낚싯대 전투 > 전투 씬 열기`를 선택한 뒤 Play합니다.
실행 씬: `Assets/Externals/Scenes/FishingPrototype.unity`.
기본 PoC(`Assets/OriginalFishingPoC`)와 Linebreaker(`Assets/Linebreaker`)는 로컬에만 보관하며 Git 공유 대상에서 제외합니다.

## 개발

- [팀 규칙](AGENTS.md)
- [브랜치·PR·커밋 흐름](Docs/TeamWorkflow.md)
- [1차 스프린트](Docs/Sprint-01.md)
- [전투 구현과 메서드 안내](Assets/Externals/Docs/IMPLEMENTATION.md)
- [게임 텍스트 CSV](Assets/Externals/Resources/Data_CombatText.csv)

처음 복제한 뒤 `git config core.hooksPath .githooks`를 실행합니다.
검사 메뉴: `낚싯대 전투 > 검사 실행`.
플레이어 공격/포획은 스탯 SO를 읽으며, 표시 문구는 Data_CombatText.csv에서 관리합니다.
조작: 좌클릭 공격·유지 포획, E 내려찍기, Q 투척, F 저장·회복.

## 에셋 폴더

공유 에셋은 `Assets/Externals` 아래에서 종류별로 관리합니다.

- `Scripts` / `Scripts/Editor`: 런타임 코드 / 에디터 도구·검사
- `Scenes`, `Prefabs`: 편집 씬과 프리팹
- `Textures/Combat`, `Materials`, `Sounds`: 전투 이미지·머티리얼·효과음
- `Data/Stats`: 스프레드시트에서 가져온 스탯 SO
- `Resources`: 이름으로 로드하는 CSV와 UI 폰트
- `Settings`: URP 설정
- `Models`, `Shaders`: 모델·셰이더
- `Docs`: 전투 구현 문서
- `TutorialInfo`: Unity 기본 안내 에셋

`.meta` 파일을 함께 이동해 GUID를 유지합니다. `Resources` 내부 파일 이름은 런타임 로딩 키이므로 변경하지 않습니다.
