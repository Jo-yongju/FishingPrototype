# 낚시 IoT 미니게임 명세 분석 및 구현 계획

| 항목 | 내용 |
|---|---|
| 계획 버전 | 0.1 |
| 기준 명세 | FishingIoT_GameSpec_v0.2.md |
| 대상 Unity | Unity 6.5 / 6000.5.9f1, URP 17.5.0 |
| 현재 상태 | 분석·계획만 작성, 게임 구현 미착수 |
| 이번 구현 대상 | 명세 단계 1~2: 독립 로컬 게임 |
| 후속 대상 | IoT, Network Authority, 메인 프로젝트 통합 |

## 1. 결론

현재 명세는 독립 게임을 먼저 만든 뒤 IoT, 네트워크, 메인 프로젝트를 차례로 연결하는 방식에 적합하며 구현 가능성이 높다.

특히 다음 경계가 이미 정의되어 있어 후속 통합 시 핵심 게임 로직의 재작성을 피할 수 있다.

- 입력 경계: `IFishingInputSource`
- 장치 피드백 경계: `IFishingFeedbackOutput`
- 판정 경계: `IFishingAuthority`
- 메인 프로젝트 경계: `FishingMiniGameFacade`
- 전달 데이터: `FishingInputFrame`, `FishingFeedbackFrame`, Launch/Result DTO

현재 구현에서는 실제 IoT, 네트워크 SDK, 메인 프로젝트 코드가 필요하지 않다. 다만 미래 연결을 보장하려면 인터페이스와 Assembly Definition 경계를 첫 구현부터 지켜야 한다.

## 2. 예상 시간

### 2.1 체크포인트별 예상

| 체크포인트 | 결과물 | 예상 시간 |
|---|---|---:|
| 첫 플레이 가능 수직 슬라이스 | 캐스팅 → 입질 → 챔질 → 파이팅 → 포획/실패 1회 | 3~5시간 |
| 명세 단계 1 완료 | 독립 라운드, Mock 장력·피드백, 1종 물고기, 기본 테스트 | 5~8시간 |
| 명세 단계 2 완료 | 3종 물고기, 반복 플레이, UI·점수·결과, 테스트 보강 | 추가 5~9시간 |
| 단계 1~2 합계 | 검증 가능한 로컬 미니게임 | 약 10~17시간 |

Unity 컴파일·임포트, PlayMode 확인, 화면 조정 횟수에 따라 변동될 수 있다. 한 번의 사용자 플레이 피드백과 수정까지 포함하면 12~20시간을 안전 범위로 본다.

이전에 언급한 2~4시간은 최소 수직 슬라이스만 빠르게 만드는 거친 추정이었다. 명세가 요구하는 구조 분리, 3종 콘텐츠, 테스트와 통합 준비까지 포함한 현실적인 전체 예상은 위 표가 더 정확하다.

### 2.2 후속 연동은 별도 산정

다음 단계는 외부 명세와 팀 기술 선택이 필요하므로 현재 예상에 포함하지 않는다.

- IoT 입력·피드백 어댑터
- 실제 장치 보정과 연결 안정화
- NetworkFishingAuthority
- 지연 보정과 이탈 처리
- 팀 메인 프로젝트 이식과 충돌 해결

## 3. 구현 범위

### 3.1 이번에 구현할 것

- 고정 3D 시점의 플레이스홀더 낚시 화면
- 180초 로컬 점수 라운드
- 라운드 상태 머신과 플레이어 낚시 상태 머신
- 캐스팅, 입질 대기, 챔질, 파이팅, 포획, 도주
- 정규화 장력 구간 판정
- 물고기 체력, 줄 내구도, 점수 계산
- 결정적 난수 시드
- Keyboard/Mock 입력
- Replay 확장이 가능한 입력 기록 구조
- Mock 피드백 출력과 디버그 표시
- LocalFishingAuthority
- 물고기 3종과 데이터 기반 난이도
- HUD, 결과 화면, 재시작
- Standalone 씬과 통합용 MiniGame 씬
- EditMode 및 핵심 PlayMode 테스트
- `FishingMiniGameFacade`와 DTO 계약

### 3.2 이번에 구현하지 않을 것

- 실제 센서 및 Embedded 코드
- 실제 IoT 통신
- 실제 힘·전류·토크·모터 제어
- 네트워크 프레임워크와 서버
- 메인 로비·로그인·계정·재화
- 최종 모델링, 고급 셰이더, 최종 사운드
- 온라인 보안의 완성형 구현

## 4. 명세 분석

### 4.1 잘 정의된 부분

- Local → IoT → Network → Main 순서가 명확하다.
- 상태 전이가 표로 정의되어 있어 상태 머신 구현 기준이 분명하다.
- 게임 수치가 ScriptableObject 설정으로 분리될 예정이다.
- 장력은 `TensionNormalized`라는 의미값으로 추상화되어 있다.
- `PullStrength`가 물리 단위가 아닌 게임 난이도 값으로 정의되어 있다.
- Mock 입력과 Mock 피드백만으로 전체 게임을 실행할 수 있다.
- 서버 권위 구조와 결정적 시드가 미래 네트워크 전환을 지원한다.
- Facade와 직렬화 가능한 DTO가 메인 프로젝트 결합 범위를 제한한다.

### 4.2 구현 시 특히 주의할 부분

#### 단일 시간 기준

입질 시간, 챔질 창, 위험 장력 누적, 라운드 시간은 서로 다른 스크립트가 독자적으로 `Time.time`을 읽으면 테스트와 네트워크 전환이 어려워진다. `IGameClock`을 주입해 Local에서는 Unity 시간, 테스트에서는 수동 시간을 사용한다.

#### Mock 장력 생성

게임 Core가 장력을 계산하면 안 되므로 Keyboard/Mock 계층에서 `TensionNormalized`를 만들어야 한다. 최초 구현은 플레이용 조작과 직접 장력 조절용 Debug 조작을 분리한다.

#### 상태와 표현 분리

애니메이션 종료 이벤트가 게임 결과를 결정하면 안 된다. Core 상태가 먼저 확정되고 Presentation은 해당 상태를 재생한다.

#### 서버 권위 전환

LocalFishingAuthority도 미래 서버와 같은 명령·상태 경계를 사용해야 한다. UI가 점수나 물고기 체력을 직접 수정하면 안 된다.

#### 종료의 단일성

시간 종료, 사용자 종료, 장치 연결 해제, 오류가 동시에 발생해도 `Completed` 또는 `Failed`가 정확히 한 번만 호출되어야 한다.

### 4.3 현재 미결사항과 임시 기본값

사용자 결정 전까지 다음 값을 개발용 기본값으로 사용하도록 계획한다.

| 항목 | 임시 기본값 |
|---|---|
| 시점 | 고정 3D 사선 시점 |
| 그래픽 | URP 기본 머티리얼과 Primitive 플레이스홀더 |
| 라운드 | 180초 총점 경쟁 |
| 물고기 | 붕어 계열 Easy, 잉어 계열 Normal, 메기 계열 Hard |
| 캐스팅 | Space를 누르는 동안 힘 충전, 놓으면 투척 |
| 챔질 | 입질 중 Space 입력 |
| 릴 감기 | R 또는 마우스 왼쪽 버튼 유지 |
| 낚싯대 방향 | A/D 및 W/S |
| Mock 장력 Debug | Q/E로 장력 감소/증가 |
| 피드백 | Debug UI와 Console 선택 표시 |

키는 모두 `KeyboardFishingInputSource` 내부에만 존재하므로 이후 입력 시스템 교체 시 Core에 영향이 없다.

## 5. 목표 아키텍처

```text
Keyboard / Mock / Replay
          │
          v
IFishingInputSource ──> FishingGameController ──> IFishingAuthority
                               │                         │
                               v                         v
                       Player State Machine      Authoritative State
                               │                         │
                               └──────────┬──────────────┘
                                          v
                                   Presentation
                                          │
                                          v
                              IFishingFeedbackOutput
                               ├─ Mock / Null
                               └─ IoT (후속)

Main Project ──> FishingMiniGameFacade ──> FishingGameController
```

### 5.1 의존 방향

- Core는 Presentation, Unity 씬, IoT, Network를 참조하지 않는다.
- Application은 Core와 계약 인터페이스만 참조한다.
- Infrastructure가 입력·피드백·Local Authority 구현체를 제공한다.
- Presentation은 상태를 읽어 화면과 사운드로 표현한다.
- Integration은 Facade와 외부 DTO만 공개한다.
- Tests는 공개 계약 또는 명시적인 테스트용 진입점을 사용한다.

## 6. 계획 파일 구조

```text
Assets/FishingMiniGame/
  Runtime/
    Core/
      State/
      Rules/
      Fish/
      Time/
    Application/
    Presentation/
    Infrastructure/
      Input/
      Feedback/
      Authority/
    Integration/
  Configs/
  Prefabs/
  Scenes/
  Tests/
    EditMode/
    PlayMode/
```

### 6.1 Assembly Definition 계획

| Assembly | 책임 | 허용 의존성 |
|---|---|---|
| FishingMiniGame.Core | 상태, 규칙, 점수, 물고기 런타임 모델 | 최소 Unity 타입 또는 순수 C# |
| FishingMiniGame.Runtime | Application, Presentation, Local Infrastructure | Core, Unity |
| FishingMiniGame.Integration | Facade, Launch/Result DTO | Core, Runtime 공개 API |
| FishingMiniGame.Tests | EditMode/PlayMode 테스트 | 테스트 대상 Assembly |

후속 단계에서만 `FishingMiniGame.IoT`, `FishingMiniGame.Network`를 추가한다.

## 7. 세부 구현 계획

### 작업 0 — 프로젝트 사전 점검

예상: 20~40분

- Unity 6.5에서 현재 프로젝트 컴파일 확인
- 입력 패키지 없이 Legacy Keyboard 어댑터 사용 가능 여부 확인
- URP 기본 설정과 씬 저장 위치 확인
- 기존 템플릿 에셋과 신규 폴더 경계 확인
- 테스트 Assembly 사용 가능 여부 확인

완료 기준:

- 신규 게임 코드가 들어갈 경로와 Assembly 의존성이 확정됨
- 프로젝트가 오류 없이 열린 상태

### 작업 1 — 계약과 공통 타입

예상: 40~70분

계획 타입:

- `FishingInputFrame`
- `FishingFeedbackFrame`, `FishingFeedbackState`
- `IFishingInputSource`
- `IFishingFeedbackOutput`
- `IFishingAuthority`
- `IGameClock`
- `FishingLaunchContext`
- `FishingRoundResult`
- 오류·종료 사유 Enum과 DTO

완료 기준:

- Core에서 하드웨어 및 네트워크 타입 참조가 없음
- DTO에 GameObject·MonoBehaviour·ScriptableObject 참조가 없음
- 모든 정규화 값의 범위와 잘못된 입력 처리 방식이 주석과 테스트에 명시됨

### 작업 2 — Core 상태와 규칙

예상: 1.5~2.5시간

계획 구성:

- 라운드 상태 머신
  - Uninitialized
  - Ready
  - Countdown
  - Playing
  - Finishing
  - Completed
  - Aborted
- 플레이어 낚시 상태 머신
  - Idle
  - Casting
  - Waiting
  - BiteWindow
  - Hooked
  - Fighting
  - Caught
  - Escaped
  - Cooldown
- `TensionRuleEvaluator`
- `HookTimingEvaluator`
- `FishingScoreCalculator`
- `LineDurabilityModel`
- `FishFightRuntime`
- 시드 기반 난수 서비스

규칙:

- 상태 변경은 상태 머신만 수행
- `TensionNormalized`는 입력 시 0~1로 제한
- 안전 구간에서 유효 Reel 입력이 있을 때만 물고기 체력 감소
- Slack 누적과 고장력 누적을 별도 추적
- 결과 생성 후 재호출해도 동일한 결과 유지

완료 기준:

- Unity 씬 없이 테스트에서 전체 상태 전이 가능
- 고정 시드와 동일 입력에서 결과 재현
- 경계 시간 테스트 통과

### 작업 3 — 물고기 데이터와 로컬 판정기

예상: 1~1.5시간

- `FishDefinition` ScriptableObject
- `FishCatalog`
- `FishingGameConfig`
- 런타임용 불변 Fish 데이터 변환
- `LocalFishingAuthority`
- 어종 선택과 크기·점수 생성
- Authority 상태 스냅샷

초기 3종 역할:

| 역할 | 행동 특징 | 목적 |
|---|---|---|
| Easy | 긴 챔질 창, 약한 저항, 긴 휴식 | 조작 학습 |
| Normal | 균형 잡힌 돌진과 휴식 | 기본 재미 검증 |
| Hard | 짧은 챔질 창, 강한 돌진, 빠른 방향 전환 | 숙련도 검증 |

완료 기준:

- 어종 차이가 코드 분기가 아니라 데이터로 표현됨
- Local Authority가 점수와 결과의 유일한 확정 주체임

### 작업 4 — Keyboard/Mock/Replay 기반

예상: 1~2시간

- `KeyboardFishingInputSource`
- `MockFishingInputSource`
- `ReplayFishingInputSource`의 최소 계약
- `MockFishingFeedbackOutput`
- `NullFishingFeedbackOutput`
- 입력 및 피드백 Debug Overlay

Mock 장력 전략:

- 플레이 입력은 Reel과 낚싯대 방향 의미값을 생성
- Debug 입력은 `TensionNormalized`를 직접 조절해 모든 경계조건을 재현
- 자동 테스트는 Scripted Mock 또는 Replay Frame으로 장력 시퀀스를 주입
- Core는 어떤 방식으로 값이 생성됐는지 알지 못함

완료 기준:

- 장치 없이 모든 상태에 진입 가능
- Bite/Run/Rest/Caught/Escaped 피드백 확인
- 연결 해제 시 StopFeedback 호출

### 작업 5 — Application과 Facade

예상: 1~1.5시간

- `FishingGameController`
- `FishingPlayerSession`
- 라운드 타이머와 Countdown
- Input → Authority → State → Presentation 순서 고정
- `FishingMiniGameFacade`
- Initialize, BeginRound, Pause, Abort, Shutdown
- Completed와 Failed 단일 발생 보장

완료 기준:

- Standalone Bootstrap과 메인 프로젝트가 같은 Facade 경로 사용
- 재시작 시 이전 이벤트와 상태가 남지 않음
- 종료와 오류가 중복 통지되지 않음

### 작업 6 — 3D Presentation과 HUD

예상: 2~3시간

- 물, 부두, 낚싯대, 줄, 물고기용 Primitive 플레이스홀더
- 고정 3D 카메라
- 캐스팅 궤적과 찌 표시
- 입질 신호와 간단한 화면 효과
- 장력 게이지와 안전 구간
- 물고기 체력·남은 시간·점수
- 포획/도주 알림
- 결과 화면과 재시작 버튼
- Mock Feedback 상태 표시

Presentation 규칙:

- 화면은 상태를 읽기만 하고 판정하지 않음
- 애니메이션이 완료되지 않아도 Core 결과는 보존됨
- 에셋 키가 없어도 플레이스홀더로 동작

완료 기준:

- 처음 보는 사용자가 현재 상태와 해야 할 조작을 화면에서 알 수 있음
- Core 수치와 HUD가 일치함

### 작업 7 — 씬과 Bootstrap

예상: 40~80분

- `FishingStandalone.unity`
- `FishingMiniGame.unity`
- 개발용 Standalone Bootstrap
- 통합용 Facade Root
- 설정 에셋 연결
- 씬 언로드 시 정리 검증

완료 기준:

- Standalone 씬을 직접 실행해 전체 라운드 플레이 가능
- MiniGame 씬은 자체 로비나 로그인 없이 초기화 대기
- DontDestroyOnLoad와 전역 Singleton 없이 동작

### 작업 8 — 테스트와 안정화

예상: 2~4시간

EditMode:

- 모든 상태 전이
- 잘못된 상태의 입력 무시
- 챔질 성공 창 경계
- 안전·Slack·고장력 누적
- 물고기 체력과 줄 내구도
- 점수식
- 고정 시드 재현
- 종료 멱등성
- 입력값 Clamp와 비정상 Sequence 처리

PlayMode:

- Standalone 시작부터 결과까지
- 성공 포획
- 헛챔질
- Slack 도주
- 고장력 줄 끊김
- 라운드 시간 종료
- 재시작과 씬 재로딩
- 피드백 Stop 호출

완료 기준:

- Unity Console 컴파일 오류 없음
- 핵심 EditMode 테스트 통과
- 주요 PlayMode 시나리오 통과
- 수동 플레이 1회 이상 완료

## 8. 구현 순서와 체크포인트

```text
사전 점검
  ↓
계약/DTO/Assembly
  ↓
Core 상태 머신과 규칙
  ↓
Local Authority + 1종 물고기
  ↓
Keyboard/Mock + 최소 HUD
  ↓
[체크포인트 A: 첫 플레이 가능한 수직 슬라이스]
  ↓
Facade + Standalone/MiniGame 씬 분리
  ↓
3종 물고기 + UI/결과
  ↓
EditMode/PlayMode 테스트와 안정화
  ↓
[체크포인트 B: 명세 단계 1~2 완료]
```

체크포인트 A에서 조작 재미를 먼저 검증한다. 장력 조작이 재미없거나 이해하기 어렵다면 Presentation과 Mock 입력만 조정하고 Core의 하드웨어 경계는 유지한다.

## 9. 검증 매트릭스

| 요구사항 | 구현 위치 | 검증 방법 |
|---|---|---|
| IoT 없이 전체 플레이 | Keyboard/Mock Infrastructure | PlayMode 및 수동 플레이 |
| Core 하드웨어 비종속 | Core Assembly | 참조 검사와 코드 리뷰 |
| 정규화 장력 판정 | TensionRuleEvaluator | EditMode 경계 테스트 |
| Mock 피드백 | Feedback Infrastructure | Debug Overlay 및 테스트 더블 |
| 데이터 기반 물고기 | FishDefinition/Catalog | 3종 프로필 비교 |
| 서버 권위 전환 가능 | IFishingAuthority | Local 구현 교체 가능성 검사 |
| 메인 통합 가능 | Facade/DTO | Standalone Bootstrap도 Facade 사용 |
| Additive 씬 대응 | FishingMiniGame 씬 | 로드·언로드 PlayMode 테스트 |
| 결정적 결과 | Clock/Random/Authority | 고정 시드 Replay 테스트 |

## 10. 주요 위험과 대응

| 위험 | 영향 | 대응 |
|---|---|---|
| Mock 장력이 실제 IoT 감각과 다름 | 추후 밸런스 재조정 | 장력 규칙을 Config화하고 Adapter만 교체 |
| Legacy Input 사용 | 메인 프로젝트 입력 시스템과 차이 | Keyboard Adapter에만 격리 |
| Presentation이 판정을 침범 | 네트워크 전환 어려움 | Core 우선 상태 확정, View 읽기 전용 |
| 상태별 시간이 여러 곳에 분산 | 재현성과 테스트 저하 | IGameClock 단일화 |
| Unity 오브젝트가 DTO에 포함 | 메인 통합·직렬화 실패 | 기본 타입과 안정 ID만 사용 |
| 전역 Singleton 사용 | Additive 통합 충돌 | Facade Root와 명시적 의존성 주입 |
| 클라이언트 장력값 조작 | 멀티플레이 공정성 저하 | Authority 범위·변화량·Sequence 검증 |
| 과도한 초기 아트 작업 | 핵심 재미 검증 지연 | Primitive 플레이스홀더 우선 |

## 11. 단계 1~2 완료 정의

다음 항목을 모두 만족해야 로컬 게임 완료로 판단한다.

- 캐스팅부터 포획 또는 실패까지 전체 흐름이 동작한다.
- 180초 동안 여러 번 낚시할 수 있다.
- 물고기 3종의 난이도와 행동 차이가 체감된다.
- 장력이 낮음·적정·높음일 때 결과가 명세와 일치한다.
- 포획 점수와 최종 결과가 표시된다.
- Keyboard/Mock 입력만으로 플레이할 수 있다.
- Mock 피드백의 상태와 강도를 확인할 수 있다.
- IoT, Network, Main SDK가 프로젝트에 없어도 동작한다.
- Core에 하드웨어 원본값과 통신 코드가 없다.
- Standalone과 통합용 씬이 분리되어 있다.
- Facade가 초기화·시작·중지·결과 반환을 담당한다.
- 컴파일 오류가 없고 핵심 테스트가 통과한다.
- 게임 종료 후 이벤트·입력·피드백이 정리된다.

## 12. 구현 착수 전 사용자 확인 항목

다음 임시 기본값을 그대로 사용할지 확인하면 구현 중 재작업을 줄일 수 있다.

1. 고정 3D 사선 시점
2. 180초 총점 경쟁
3. Easy/Normal/Hard 물고기 3종
4. Space 캐스팅·챔질
5. R 릴 감기, A/D·W/S 낚싯대 방향
6. Q/E Mock 장력 Debug 조절
7. URP Primitive 플레이스홀더
8. 한국어 개발용 HUD

이 항목은 IoT·네트워크·메인 프로젝트 구조를 바꾸지 않으며, 구현 후에도 Input 또는 Presentation 계층에서 조정할 수 있다.
