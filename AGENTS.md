# 팀 개발 규칙

- 작업 프로젝트는 D:/Dev/XPproject이며 원격은 TamHeeKim/xpdevelopment이다.
- 기본 OriginalFishingPoC와 기존 Linebreaker는 별도 요청 없이 수정하지 않는다.
- main은 릴리즈, develop은 통합, feature/기능명은 기능 개발에 사용한다.
- 기능 브랜치명은 feature/player-movement 같은 소문자 kebab-case로 작성한다.
- 기능은 develop 대상 PR로 제출한다. 브랜치 병합은 PR로만 수행하고 최소 1명의 리뷰 승인을 받는다.
- main과 develop에 직접 푸시하지 않는다. 권한이 없으면 원격 보호 규칙을 설정했다고 주장하지 않는다.
- 커밋 제목: [TEST], [MERGE], [BUILD], [INIT], [DOC], [REFAC], [FIX], [MOD], [ADD], [FEAT] 뒤 공백과 설명.
- 1주 또는 2주 스프린트로 진행하고 Docs/Sprint-01.md의 작업·검증 상태를 갱신한다.
- 생성하는 오브젝트는 형식_이름. 예: Player_Fisher, Prop_PoisonBottle, Text_Objective. 표시 이름은 별도 CSV 키로 관리한다.
- 변수와 필드는 camelCase. 클래스·메서드·프로퍼티는 기존 C# PascalCase를 유지한다.
- 인스펙터 필드는 기능별 Header와 필요한 Tooltip을 제공한다. 직렬화 필드명 변경은 FormerlySerializedAs로 기존 값을 보존한다.
- 씬 공동 편집으로 충돌이 예상되면 담당자와 범위를 나누고 변경 오브젝트를 프리팹으로 전달 후 필요한 위치에서 unpack한다. 불필요한 전체 씬 재저장은 피한다.
- 사용자 표시 텍스트는 Assets/FishingCombat/Resources/Data_CombatText.csv에서 불러온다. 개발자용 예외·로그·인스펙터 설명은 번역 대상이 아니다.
- 변경 전 호출부와 메서드 역할을 확인하고 Assets/FishingCombat/IMPLEMENTATION.md를 함께 갱신한다.
- git config core.hooksPath .githooks로 로컬 훅을 활성화한다. 훅은 원격 보호 규칙을 대체하지 않는다.
