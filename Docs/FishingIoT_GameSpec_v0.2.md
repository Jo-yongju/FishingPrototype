# 낚시 IoT 미니게임 구현 명세서

| 항목 | 내용 |
|---|---|
| 문서 버전 | 0.2 (IoT 비종속 경계 보강) |
| 대상 프로젝트 | FishingPrototype |
| Unity 기준 | Unity 6.5 / 6000.5.9f1, URP 17.5.0 |
| 현재 단계 | 명세 작성만 완료, 게임 구현 미착수 |
| 최종 용도 | 멀티플레이 메인 프로젝트에 포함되는 IoT 낚시 미니게임 |

## 1. 목표

이 미니게임은 낚싯대 형태의 IoT 장치를 이용해 캐스팅, 입질 대응, 챔질, 릴 감기와 장력 조절을 수행하는 짧은 멀티플레이 낚시 게임이다.

개발 순서는 다음과 같이 고정한다.

1. 독립 Unity 프로젝트에서 가상 입력을 사용하는 싱글플레이 게임으로 핵심 재미를 검증한다.
2. 가상 입력을 실제 IoT 입력 어댑터로 교체한다.
3. 로컬 판정기를 네트워크 판정기로 교체해 멀티플레이를 연결한다.
4. 최종적으로 팀 메인 프로젝트의 로비, 참가자 정보, 화면 전환, 결과 시스템과 결합한다.

처음부터 IoT, 네트워크 또는 메인 프로젝트에 직접 의존하지 않는다. 이후 연동 시 핵심 게임 로직을 다시 작성하지 않고 외부 연결부만 교체하는 것을 목표로 한다.

## 2. 현재 범위와 제외 범위

### 2.1 현재 구현 대상으로 정의할 범위

- 낚시 한 판의 시작과 종료
- 캐스팅, 대기, 입질, 챔질, 파이팅, 포획 또는 실패 흐름
- 물고기 종류와 난이도에 따른 행동 차이
- 정규화된 장력 상태와 릴·낚싯대 의미 입력을 이용한 파이팅 판정
- 점수와 포획 결과 산출
- 키보드 또는 개발용 가상 입력
- 독립 실행용 개발 화면과 디버그 정보
- IoT, 네트워크, 메인 프로젝트를 위한 인터페이스와 데이터 계약

### 2.2 현재 만들지 않을 것

- 실제 IoT 통신 드라이버
- MQTT, Serial, BLE, WebSocket 등 하드웨어 전송 방식 확정
- 실제 멀티플레이 SDK와 서버 코드
- 팀 메인 로비와 로그인 시스템
- 팀 공용 플레이어·계정·재화 시스템
- 최종 아트, 사운드, 연출 품질
- 온라인 부정행위 방지의 완성형 구현

## 3. 핵심 설계 원칙

```text
IoT 장치 ──> IoT 입력 어댑터 ───┐
가상 입력 ─> 로컬 입력 어댑터 ──┤
                                v
메인 프로젝트 ─> MiniGame Facade ─> 낚시 핵심 로직 ─> 화면/사운드
                                ^            │
로컬 판정기 ─────────────────────┤            ├─> Mock 피드백 어댑터
네트워크 판정기 ─────────────────┘            └─> IoT 피드백 어댑터
```

- 핵심 규칙은 IoT 패킷, 네트워크 SDK, 로비 코드와 분리한다.
- 입력은 장치 원본 값이 아니라 정규화된 `FishingInputFrame`으로 전달한다.
- 결과 판정은 `IFishingAuthority` 뒤에 숨긴다. 초기에는 로컬 판정기, 이후에는 서버 권위 판정기를 사용한다.
- 메인 프로젝트는 `FishingMiniGameFacade` 하나를 통해 시작·종료·결과 수신을 수행한다.
- 물고기 수치, 제한 시간, 점수는 코드에 고정하지 않고 설정 에셋으로 관리한다.
- 전역 싱글턴, 메인 프로젝트 오브젝트 이름 검색, 씬 인덱스 고정에 의존하지 않는다.
- 핵심 계산은 가능한 한 순수 C#으로 작성해 EditMode 테스트가 가능해야 한다.

### 3.1 게임과 IoT/Embedded 책임 경계

```text
[GAME]
물고기 행동·난이도 / 상태 머신 / 점수 / 물고기 체력 / 줄 내구도
피드백 상태와 0~1 강도 결정
               ↓↑ 의미 기반 추상 인터페이스
[IoT / Embedded]
실제 센서·액추에이터 / 센서 보정 / 실제 물리량 측정
모터 및 안전 제어 / 하드웨어 통신
```

- 게임은 `FishingInputFrame`과 `IFishingFeedbackOutput`만 안다.
- 게임은 실제 센서 종류, 물리 단위, 모터, 제어 알고리즘, 통신 프로토콜을 알면 안 된다.
- IoT/Embedded는 게임의 점수식, 물고기 체력, 상태 머신 내부를 알 필요가 없다.
- Embedded 내부 구조와 안전 제어의 세부 사항은 별도 명세에서 정의한다.

## 4. 게임 콘셉트

### 4.1 한 줄 설명

제한 시간 동안 캐스팅과 챔질 타이밍을 맞추고, 물고기의 저항에 대응해 줄 장력을 유지하면서 더 가치 높은 물고기를 잡는 경쟁형 낚시 미니게임.

### 4.2 기본 플레이 형태

- 독립 프로토타입: 1인 연습 모드
- 최종 형태: 각 참가자가 자신의 클라이언트와 IoT 낚싯대를 사용하는 동시 진행 멀티플레이
- 권장 참가자 수 가정: 1~4명, 설정으로 확장 가능
- 권장 한 판 길이 가정: 180초
- 승리 조건 가정: 제한 시간 종료 시 총점이 가장 높은 참가자 승리

참가자 수와 경기 시간은 팀 메인 기획이 확정되면 변경한다. 핵심 로직은 특정 인원수에 의존하지 않는다.

## 5. 핵심 플레이 루프

1. 플레이어가 낚시 준비 상태가 된다.
2. 캐스팅 동작으로 방향과 세기를 결정한다.
3. 캐스팅 품질과 낚시터 조건을 이용해 대상 물고기 후보를 결정한다.
4. 입질까지 기다리며 시각·음향·진동 신호를 받는다.
5. 제한된 시간 안에 챔질한다.
6. 물고기가 걸리면 릴을 감고 낚싯대 방향을 조절해 장력을 관리한다.
7. 물고기 체력을 모두 소진하면 포획하고, 줄이 끊기거나 제한 시간을 넘기면 실패한다.
8. 점수와 포획 기록을 갱신하고 다음 캐스팅으로 돌아간다.
9. 라운드 종료 시 전체 결과를 메인 프로젝트 또는 개발용 화면에 전달한다.

## 6. 상태 설계

### 6.1 라운드 상태

| 상태 | 설명 | 종료 조건 |
|---|---|---|
| Uninitialized | 외부 설정을 받기 전 | 초기화 완료 |
| Ready | 참가자와 설정 로딩 완료 | 시작 명령 |
| Countdown | 시작 카운트다운 | 카운트다운 종료 |
| Playing | 낚시 가능 | 제한 시간 종료 또는 강제 종료 |
| Finishing | 마지막 결과 정산 | 정산 완료 |
| Completed | 결과 전달 완료 | 호스트가 종료 처리 |
| Aborted | 오류·이탈로 중단 | 호스트가 종료 처리 |

### 6.2 플레이어 낚시 상태

| 상태 | 허용 입력 | 주요 처리 |
|---|---|---|
| Idle | Cast | 다음 캐스팅 준비 |
| Casting | RodDirection, CastPower | 투척 방향·세기 확정 |
| Waiting | Cancel | 입질 시간 계산 및 신호 출력 |
| BiteWindow | Hook | 챔질 성공 시간 판정 |
| Hooked | Reel, RodDirection | 물고기 파이팅 초기화 |
| Fighting | Reel, RodDirection | 장력·물고기 체력·줄 내구도 계산 |
| Caught | 없음 | 포획 점수와 기록 생성 |
| Escaped | 없음 | 실패 사유 기록 |
| Cooldown | 없음 | 짧은 후처리 뒤 Idle 복귀 |

상태 전이는 한 곳의 상태 머신에서만 발생해야 한다. 화면이나 입력 어댑터가 상태를 직접 바꾸면 안 된다.

## 7. 게임 규칙 초깃값

아래 값은 첫 플레이 가능한 버전을 위한 가정이며 모두 설정 에셋에서 변경할 수 있어야 한다.

| 항목 | 초깃값 | 비고 |
|---|---:|---|
| 라운드 시간 | 180초 | 메인 프로젝트에서 덮어쓰기 가능 |
| 입질 대기 | 1.5~7초 | 물고기별 가중치 적용 |
| 기본 챔질 성공 창 | 0.8초 | 난이도에 따라 축소 |
| 안전 장력 구간 | 0.30~0.75 | 0~1 정규화 값 |
| 위험 장력 지속 허용 | 1.2초 | 초과 시 줄 끊김 |
| 기본 파이팅 제한 | 25초 | 물고기별 변경 가능 |
| 포획 후 대기 | 1.5초 | 결과 연출 시간 |

### 7.1 캐스팅

- 입력 어댑터는 `CastStarted`, `CastReleased`, 방향, 정규화된 힘을 제공한다.
- 도달 거리와 정확도는 힘, 타이밍, 방향 안정성으로 계산한다.
- 초기 키보드 모드에서는 버튼 누른 시간으로 힘을 만들고 방향키로 방향을 정한다.
- IoT 모드에서는 장치별 원본 데이터를 어댑터가 분석해 같은 게임 의미값으로 변환한다. 원본 센서 종류와 변환 방식은 게임 명세에서 확정하지 않는다.

### 7.2 챔질

- 입질 시작 시각과 `Hook` 입력 시각 차이로 성공 여부를 판정한다.
- 너무 빠른 입력은 헛챔질, 너무 늦은 입력은 미끼 이탈로 기록한다.
- 실제 네트워크 연결 후에는 서버 시간이 판정 기준이며 클라이언트 지연 보정 정책을 추가한다.

### 7.3 파이팅과 장력

- 게임 Core는 실제 물리 장력을 계산하지 않고 `IFishingInputSource`가 제공하는 `TensionNormalized`를 파이팅 판정에 사용한다.
- `TensionNormalized`는 0~1의 게임 의미값이다. 0은 줄 장력이 거의 없는 상태, 0.5 부근은 적정 상태, 1은 매우 높은 상태를 뜻한다.
- 이 값은 실측 물리 단위가 아니다. Keyboard/Mock 어댑터는 테스트용 값을 생성하고, 실제 IoT 어댑터는 장치별 원본 데이터를 같은 의미값으로 변환한다.
- 안전 구간 안에서 릴을 감으면 물고기 체력이 감소한다.
- 장력이 너무 낮으면 `Slack` 상태와 물고기 이탈 위험이 증가한다.
- 장력이 너무 높으면 줄 내구도가 감소하고 일정 시간 지속 시 줄이 끊어진다.
- 실제 물리 단위 계산, 센서 측정, 보정, 구동 제어는 게임 Core의 책임이 아니다.
- 물고기는 돌진, 좌우 이동, 휴식 패턴을 데이터 기반으로 선택한다.

### 7.4 점수

권장 기본식:

```text
최종 점수 = 기본 점수 × 크기 배율 × 난이도 배율 × 포획 품질 배율
```

- 기본 점수: 어종별 값
- 크기 배율: 개체 크기에서 산출
- 난이도 배율: 물고기 저항 난이도
- 포획 품질 배율: 챔질 정확도, 장력 안정성, 포획 시간
- 모든 최종 점수 계산은 판정기에서 수행한다.

## 8. 물고기 데이터

`FishDefinition`은 최소한 다음 데이터를 가진다.

| 필드 | 설명 |
|---|---|
| FishId | 네트워크와 결과 저장에 사용하는 안정적인 문자열 ID |
| DisplayName | 표시 이름 또는 현지화 키 |
| BaseScore | 기본 점수 |
| WeightRange | 최소·최대 무게 |
| BiteDelayRange | 입질 대기 범위 |
| HookWindow | 챔질 허용 시간 |
| MaxStamina | 기본 체력 |
| PullStrength | 기본 저항력 |
| BehaviorProfile | 돌진·휴식·방향 전환 패턴 |
| RarityWeight | 출현 가중치 |
| VisualKey | 나중에 메인 프로젝트 에셋과 연결할 키 |
| AudioKey | 나중에 사운드 시스템과 연결할 키 |

씬 오브젝트나 프리팹의 Unity 인스턴스 ID를 저장 데이터나 네트워크 식별자로 사용하지 않는다.

`PullStrength`는 실제 힘이나 토크가 아닌 게임상의 상대적 저항 강도다. 0~1 또는 설정된 게임 범위에서 물고기 행동 난이도, `Fight`/`Run` 상태, 추상 피드백 강도를 만드는 데 사용한다. 실제 장치의 물리 출력으로 변환하는 일은 IoT/Embedded 책임이다.

## 9. 입력 및 IoT 연동 계약

### 9.1 정규화된 입력

```csharp
public struct FishingInputFrame
{
    public string ParticipantId;
    public long Sequence;
    public double TimestampSeconds;
    public bool CastPressed;
    public bool CastReleased;
    public bool HookPressed;
    public float ReelDelta;
    public float TensionNormalized;
    public float RodPitch;
    public float RodYaw;
    public float MotionStrength;
    public bool IsDeviceConnected;
}
```

세부 타입과 직렬화 특성은 구현 시 조정할 수 있으나 의미는 유지한다.

- `TensionNormalized`는 0~1의 게임 의미값이며 생성 방법은 입력 어댑터가 결정한다.
- `RodPitch`와 `RodYaw`는 게임에서 사용하는 낚싯대 방향 의미값이다. 특정 센서나 실제 각도 단위를 전제로 하지 않으며 구현 시 권장 범위를 명시한다.
- `MotionStrength`는 제스처 판정에 사용할 수 있는 추상적인 동작 강도다. 특정 센서 원본값이 아니다.
- 게임 Core에 전류, PWM, 토크, 원본 센서값, ADC 값, 장치 오류 레지스터 등 하드웨어 데이터를 전달하지 않는다.

### 9.2 입력 인터페이스

```csharp
public interface IFishingInputSource
{
    bool IsConnected { get; }
    FishingInputFrame ReadFrame();
    void ResetState();
}
```

구현체는 다음처럼 분리한다.

- `KeyboardFishingInputSource`: 독립 프로토타입용. 개발 키 또는 결정적인 테스트 모델로 `ReelDelta`와 `TensionNormalized`를 생성한다.
- `MockFishingInputSource`: 입력 시나리오를 코드에서 주입하는 단위·통합 테스트용 구현체
- `ReplayFishingInputSource`: `TensionNormalized`를 포함한 기록 입력 재생과 자동 테스트용
- `IoTFishingInputSource`: 실제 장치 연결 시 추가

파이팅 중 사용하는 입력 어댑터는 실제 측정 여부와 관계없이 유효한 `TensionNormalized`를 제공해야 한다. 값을 만들 수 없는 장치는 라운드 시작 전에 지원 불가 상태를 알리거나, 별도 합의된 어댑터 내부 대체 모델을 사용한다. 게임 Core에 하드웨어별 분기문을 추가하지 않는다.

### 9.3 IoT 어댑터의 책임

- 장치 연결·재연결
- 장치별 원본 데이터 수신
- 필요한 캘리브레이션과 필터링
- 원본 데이터를 게임에서 정의한 의미 기반 입력으로 변환
- 캐스팅·챔질 등 게임 동작 의미 판별
- 끊김, 지연, 비정상 시퀀스 감지
- `Sequence`, `TimestampSeconds`, 연결 상태 관리
- 최신 유효 입력을 `FishingInputFrame`으로 제공

핵심 게임 로직은 실제 센서, 액추에이터, 통신 프로토콜 또는 제조사 SDK를 알면 안 된다. 특정 원본 데이터를 어떤 물리 단위로 변환하는지는 별도 IoT/Embedded 명세에서 다룬다.

### 9.4 연결 끊김 정책

- 0.5초 이하의 짧은 누락: 마지막 방향값을 짧게 유지하되 릴 입력은 0으로 처리
- 0.5초 초과: 일시정지 가능 여부를 호스트 정책에 문의
- 멀티플레이에서는 전체 게임을 멈추지 않고 해당 참가자만 안전 상태로 전환하는 것을 기본값으로 함
- 복구 불가 시 결과에 `DeviceDisconnected` 종료 사유 기록

정확한 시간은 하드웨어 테스트 후 조정한다.

### 9.5 게임 피드백 출력 계약

게임이 장치에 전달하는 출력도 하드웨어 제어값이 아니라 의미 기반 요청이어야 한다.

```csharp
public enum FishingFeedbackState
{
    None,
    Bite,
    Fight,
    Run,
    Rest,
    Caught,
    Escaped
}

public struct FishingFeedbackFrame
{
    public FishingFeedbackState State;
    public float Intensity;
}

public interface IFishingFeedbackOutput
{
    void ApplyFeedback(FishingFeedbackFrame frame);
    void StopFeedback();
}
```

- `Intensity`는 0~1의 추상 강도 요청이며 실제 힘, 전류, 토크 또는 듀티비가 아니다.
- `Intensity`는 출력 전과 어댑터 수신 시 각각 0~1 범위로 제한한다.
- 게임 Core는 물고기 상태와 `PullStrength` 등을 이용해 `State`와 `Intensity`만 결정한다.
- 실제 장치 출력과 안전 제한은 IoT/Embedded 어댑터가 책임진다.
- 라운드 종료, 장치 연결 해제, 오류 발생 시 `StopFeedback()`을 반드시 호출한다.

### 9.6 Mock 및 IoT 피드백 구현

- `MockFishingFeedbackOutput`: 상태와 강도를 Debug UI 또는 개발 로그에 표시한다.
- `IoTFishingFeedbackOutput`: 추후 실제 장치 연결 시 추가하며 추상 피드백을 장치 명령으로 변환한다.
- 선택적으로 `NullFishingFeedbackOutput`을 제공해 피드백 장치 없이도 조용히 전체 게임을 실행할 수 있다.

현재 독립 버전은 다음 구성만으로 전체 흐름을 검증해야 한다.

```text
KeyboardFishingInputSource -> Fishing Core -> MockFishingFeedbackOutput
```

IoT 연결 후에는 Core 수정 없이 다음처럼 어댑터만 교체한다.

```text
IoTFishingInputSource -> Fishing Core -> IoTFishingFeedbackOutput
```

## 10. 네트워크 연동 설계

### 10.1 권위 모델

- 최종 멀티플레이는 서버 권위 방식을 기본으로 한다.
- 물고기 선택, 입질 시점, 챔질 성공, 체력, 줄 끊김, 점수는 서버가 확정한다.
- 클라이언트는 입력 의도와 표시용 예측만 담당한다.
- 독립 버전의 `LocalFishingAuthority`가 서버 역할을 같은 프로세스에서 수행한다.
- 네트워크 연결 시 `NetworkFishingAuthority`로 교체한다.
- 서버 판정기는 클라이언트가 보낸 `TensionNormalized`를 신뢰하지 않고 0~1 범위, 시퀀스, 타임스탬프, 허용 변화량을 검증한다.
- 네트워크 계층에도 하드웨어 원본값을 전송하지 않는다.

```csharp
public interface IFishingAuthority
{
    void Initialize(FishingRoundContext context);
    void SubmitInput(FishingInputFrame input);
    FishingAuthoritativeState GetLatestState();
    FishingRoundResult BuildResult();
}
```

### 10.2 네트워크 메시지 범주

- 명령: Ready, Cast, HookAttempt, ReelInput, Cancel
- 이벤트: BiteStarted, HookSucceeded, FishEscaped, FishCaught, RoundEnded
- 스냅샷: 현재 상태, 장력, 물고기 체력, 남은 시간, 점수
- 메타데이터: ParticipantId, RoundId, Sequence, ServerTimestamp

초기 네트워크 구현에서는 물고기 물리 오브젝트의 Transform을 매 프레임 동기화하지 않는다. 게임 규칙 상태를 동기화하고 각 클라이언트가 시각 표현을 재생하는 방식을 우선한다.

### 10.3 재현성과 공정성

- 라운드 시작 시 서버가 난수 시드를 제공한다.
- 난수 소비 위치를 핵심 판정기에 한정한다.
- 점수 결과에는 RoundId, ParticipantId, Seed, Catch 목록을 포함한다.
- 네트워크 프레임워크는 팀에서 선택한 뒤 어댑터 레이어에만 추가한다.

## 11. 메인 프로젝트 결합 계약

### 11.1 공개 진입점

메인 프로젝트는 `FishingMiniGameFacade`만 직접 호출한다.

```csharp
public sealed class FishingMiniGameFacade : MonoBehaviour
{
    public void Initialize(FishingLaunchContext context);
    public void BeginRound();
    public void SetPaused(bool paused);
    public void Abort(FishingAbortReason reason);
    public void Shutdown();

    public event Action<FishingRoundResult> Completed;
    public event Action<FishingRuntimeError> Failed;
}
```

### 11.2 시작 정보

`FishingLaunchContext` 최소 필드:

- RoundId
- LocalParticipantId
- 참가자 목록
- 라운드 제한 시간
- 난수 시드
- 실행 모드(LocalPractice, NetworkMatch)
- 입력 방식(Mock, IoT)
- 선택적 난이도와 낚시터 ID

### 11.3 결과 정보

`FishingRoundResult` 최소 필드:

- RoundId
- 정상 종료 여부와 종료 사유
- 참가자별 총점과 순위
- 참가자별 포획 목록
- 어종 ID, 무게, 점수, 포획 시간
- 장치 이탈·네트워크 이탈 여부
- 디버그가 아닌 최소 플레이 통계

DTO에는 `GameObject`, `MonoBehaviour`, `ScriptableObject` 참조를 넣지 않는다. 메인 프로젝트와 네트워크에서 직렬화할 수 있는 기본 타입만 사용한다.

### 11.4 씬 연결

- 개발용 `FishingStandalone` 씬은 독립 실행과 테스트에만 사용한다.
- 통합용 `FishingMiniGame` 씬은 메인 프로젝트가 Additive로 로드할 수 있어야 한다.
- 통합 씬은 자체 로비, 로그인, 메인 메뉴를 포함하지 않는다.
- 씬 이름이나 Build Index를 코드에 고정하지 않고 호스트가 씬 키를 제공한다.
- 종료 시 생성한 오브젝트, 이벤트 구독, 입력 연결을 모두 정리해야 한다.

## 12. 권장 코드 구조

```text
Assets/FishingMiniGame/
  Runtime/
    Core/                 순수 규칙, 상태 머신, 점수, 물고기 모델
    Application/          라운드와 플레이 흐름 조정
    Presentation/         Unity 화면, 애니메이션, 사운드 연결
    Infrastructure/
      Input/              Keyboard, Replay, 추후 IoT 어댑터
      Feedback/           Mock, Null, 추후 IoT 피드백 어댑터
      Authority/          Local, 추후 Network 판정기
    Integration/          Facade, LaunchContext, Result DTO
  Configs/                게임 설정과 물고기 카탈로그
  Prefabs/
  Scenes/
    FishingStandalone.unity
    FishingMiniGame.unity
  Tests/
    EditMode/
    PlayMode/
```

권장 Assembly Definition 분리:

- `FishingMiniGame.Core`: Unity 씬과 외부 SDK 의존 최소화
- `FishingMiniGame.Runtime`: Core와 Unity 표현 계층
- `FishingMiniGame.Integration`: 메인 프로젝트 공개 계약
- `FishingMiniGame.Tests`: 테스트 전용
- 이후 입력·피드백 어댑터를 담는 `FishingMiniGame.IoT`와 판정 어댑터를 담는 `FishingMiniGame.Network`를 별도 추가

## 13. 설정 에셋

- `FishingGameConfig`: 라운드 시간, 상태별 시간, 정규화 장력 판정 구간과 게임 규칙 계수
- `FishCatalog`: 사용 가능한 FishDefinition 목록
- `FishDefinition`: 개별 물고기 규칙과 표시 키
- `FishingSpotDefinition`: 낚시터별 출현 가중치와 환경 계수
- `FishingPresentationConfig`: 카메라, UI, 연출용 수치
- `IoTCalibrationProfile`: IoT 단계에서 추가

핵심 수치와 표시 수치를 분리한다. 화면 흔들림이나 애니메이션 속도 변경이 판정 결과에 영향을 주면 안 된다.

## 14. 표시와 피드백 요구사항

최소 플레이 버전에 필요한 표시:

- 남은 라운드 시간
- 현재 점수
- 캐스팅 힘과 방향
- 입질 신호
- 챔질 성공·실패 피드백
- 장력 게이지와 안전 구간
- 물고기 체력 또는 진행도
- 포획 물고기와 획득 점수
- 장치 연결 상태 자리표시자

장치 피드백은 입력과 별도의 `IFishingFeedbackOutput`을 통해 전달한다. 독립 버전에서는 `MockFishingFeedbackOutput`이 현재 상태와 0~1 강도를 Debug UI 또는 로그에 표시한다. 장치 피드백이 없어도 화면과 사운드만으로 플레이 가능해야 한다.

## 15. 오류와 종료 처리

표준 종료 사유:

- Completed
- UserQuit
- HostAborted
- DeviceDisconnected
- NetworkDisconnected
- InvalidConfiguration
- RuntimeError

모든 종료 경로는 결과 또는 오류 이벤트를 정확히 한 번만 발생시킨다. 중복 결과 전송을 막기 위해 라운드 종료는 멱등적으로 처리한다.

## 16. 테스트 전략

### 16.1 EditMode

- 모든 상태 전이
- 챔질 경계 시간
- 장력 안전·위험 구간
- 입력 장력값의 범위 제한과 비정상 변화량 처리
- 물고기 체력 감소와 도주
- 피드백 상태 전이와 0~1 강도 제한
- 점수 계산
- 고정 시드 결과 재현
- 잘못된 설정값 검증

### 16.2 PlayMode

- Standalone 씬에서 한 판 시작부터 종료까지
- 캐스팅 성공과 실패
- 포획과 줄 끊김
- 강제 종료와 재시작
- 씬 재로딩 후 이벤트 중복 여부
- Replay 입력으로 동일 결과 재생
- Mock 피드백에서 Bite, Run, Rest, Caught, Escaped 출력 확인

### 16.3 추후 연동 테스트

- IoT 패킷 누락, 순서 역전, 노이즈, 재연결
- IoT 피드백 종료와 안전 상태 전환
- 네트워크 지연과 패킷 손실
- 메인 프로젝트에서 Additive 로드·언로드 반복
- 여러 참가자의 결과 정렬과 동점 처리

## 17. 단계별 개발 계획과 완료 조건

### 단계 0 — 명세

- 이 문서 리뷰
- 미결 기획 항목 확정
- 팀 Unity·네트워크·입력 패키지 버전 확인

### 단계 1 — 독립 로컬 수직 슬라이스

- 키보드 입력으로 캐스팅부터 포획까지 한 사이클 가능
- 물고기 최소 1종
- 장력과 점수 판정 동작
- Mock 장력 입력과 Mock 피드백 출력으로 파이팅 전체 흐름 검증
- Standalone 씬에서 한 판 종료 가능
- IoT와 네트워크 패키지 의존성 없음

### 단계 2 — 로컬 게임 완성도

- 물고기 최소 3종과 데이터 기반 차이
- 제한 시간, 반복 캐스팅, 결과 화면
- 기본 UI·사운드·연출
- 주요 Core EditMode 테스트

### 단계 3 — IoT 연결

- 실제 장치 사양 확정
- IoT 어댑터와 캘리브레이션 구현
- Keyboard와 IoT 입력을 설정으로 교체 가능
- Mock과 IoT 피드백 출력을 설정으로 교체 가능
- 연결 끊김과 재연결 테스트 통과

### 단계 4 — 네트워크 연결

- 팀 네트워크 프레임워크 확정
- 서버 권위 NetworkFishingAuthority 구현
- 참가자별 입력과 결과 동기화
- 지연 보정과 이탈 정책 적용

### 단계 5 — 메인 프로젝트 통합

- 팀 저장소의 Unity·패키지 버전에 맞춰 이식
- Facade로 로비 참가자와 라운드 정보를 전달받음
- Additive 로딩과 결과 반환
- 메인 UI·사운드·계정 시스템에 연결
- 독립 개발용 Bootstrap 의존 제거 또는 개발 전용으로 격리

## 18. 현재 프로젝트 관련 주의사항

- 현재 프로젝트는 Unity 6.5.9f1과 URP 17.5.0 기준이다.
- Unity MCP v10.1.0은 개발 보조 도구이며 런타임 빌드에는 포함하지 않는다.
- 현재 Unity 6.5와 공식 Input System 패키지의 컴파일 호환 문제를 피하기 위해 Input System을 프로젝트 의존성에 넣지 않았다.
- 단계 1의 실제 입력 구현 전에 팀 메인 프로젝트의 입력 패키지와 버전을 확인한다.
- 입력 코드가 특정 Unity 입력 패키지에 직접 퍼지지 않도록 반드시 `IFishingInputSource` 뒤에 둔다.

## 19. 구현 전 결정이 필요한 항목

| 질문 | 현재 권장 가정 | 결정 주체/시점 |
|---|---|---|
| 실제 IoT 센서 구성 | 미확정. 게임은 센서 종류에 의존하지 않음 | 별도 IoT/Embedded 명세 |
| 릴 조작 입력 방식 | `ReelDelta`를 제공할 수 있는 방식으로 추후 결정 | IoT 팀과 협의 |
| 장력 입력 방식 | 정규화된 `TensionNormalized` 제공. 물리 측정 방식은 미확정 | 별도 IoT/Embedded 명세 |
| 낚싯대 동작 입력 | 필요한 의미값만 정의하고 실제 생성 방식은 추후 결정 | IoT 팀과 협의 |
| 장치 피드백 방식 | `State` + `Intensity` 추상 계약 사용 | 별도 IoT/Embedded 명세 |
| 장치 통신 방식 | 미확정 | IoT 팀과 협의 |
| 최종 참가자 수 | 1~4명 | 메인 게임 기획 |
| 한 판 시간 | 180초 | 플레이 테스트 후 |
| 경쟁 방식 | 총점 순위 | 메인 게임 기획 |
| 네트워크 프레임워크 | 확정하지 않음 | 팀 메인 기술 선택 후 |
| 동점 처리 | 최고 단일 포획 점수, 이후 포획 시간 | 팀 기획 확인 |
| 물고기 종류 | 프로토타입 3종 | 콘텐츠 기획 |
| 목표 플랫폼 | Windows | 팀 빌드 대상 확인 |
| 그래픽 스타일 | 플레이스홀더 후 교체 | 아트 방향 확정 후 |

## 20. 단계 1 착수 승인 조건

다음 항목을 사용자와 합의한 뒤 게임 구현을 시작한다.

- 기본 플레이 시점: 1인칭 또는 3인칭/측면
- 캐스팅 조작 방식
- 파이팅 조작 방식과 난이도
- 한 판 시간과 점수 승리 조건
- 최초 물고기 3종의 성격
- 사용할 임시 아트 수준
- 단계 1에서 사용할 키보드 조작키

이 합의 전에는 프로젝트 구조와 게임 코드를 추가하지 않는다.
