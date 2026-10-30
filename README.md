# XPproject

Unity 6000.3.11f1의 횡스크롤 낚싯대 액션 프로토타입입니다.

## 실행

Unity Hub에서 이 폴더를 열고 `낚싯대 전투 > 전투 씬 열기`를 선택한 뒤 Play합니다.
실행 씬: `Assets/FishingCombat/Scenes/FishingPrototype.unity`.
기본 PoC(`Assets/OriginalFishingPoC`)와 Linebreaker(`Assets/Linebreaker`)는 로컬에만 보관하며 Git 공유 대상에서 제외합니다.

## 개발

- [팀 규칙](AGENTS.md)
- [브랜치·PR·커밋 흐름](Docs/TeamWorkflow.md)
- [1차 스프린트](Docs/Sprint-01.md)
- [전투 구현과 메서드 안내](Assets/FishingCombat/IMPLEMENTATION.md)
- [게임 텍스트 CSV](Assets/FishingCombat/Resources/Data_CombatText.csv)

처음 복제한 뒤 `git config core.hooksPath .githooks`를 실행합니다.
검사 메뉴: `낚싯대 전투 > 검사 실행`.
밸런스 스프레드시트/SO 연결은 별도 담당 범위이며, 여기의 CSV는 표시 텍스트 전용입니다.
