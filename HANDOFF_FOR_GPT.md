# FishingPrototype — GPT 전달 문서

## 프로젝트 개요

Unity 6.5(6000.5.9f1)로 제작한 독립 실행형 바다낚시 미니게임 프로토타입입니다.
현재는 키보드/Mock 입력만 사용하며 실제 IoT, Serial, STM32, Bluetooth 및 네트워크 기능은 구현하지 않았습니다.

향후 팀 메인 프로젝트, IoT 입력, 멀티플레이 Authority를 연결할 수 있도록 Fishing Core와 Unity 표현 계층을 분리했습니다.

## 실행 방법

1. Unity Hub에서 이 폴더를 프로젝트로 추가합니다.
2. Unity 6000.5.9f1 또는 호환되는 Unity 6 버전으로 엽니다.
3. `Assets/FishingMiniGame/Scenes/FishingStandalone.unity`를 엽니다.
4. Play를 누릅니다.
5. 시작 화면에서 `START FISHING`을 누릅니다.

## 조작법

- `SPACE` 누르기 유지 후 떼기: 캐스팅
- 입질 표시 중 `SPACE`: 챔질
- `R` 또는 마우스 왼쪽 버튼: 릴 감기
- `Q / E`: 장력 낮추기 / 높이기
- `A / D`: 물고기 도주 방향 반대로 대응

## 구현된 기능

- 상태 머신: Idle → Casting → Waiting → BiteWindow → Hooked → Fighting → Caught/Escaped → Cooldown
- 180초 라운드, 점수, 포획 기록
- 고등어, 참돔, 방어 3종
- 거짓 입질과 성급한 챔질 페널티
- Fight / Run / Rest 파이팅 페이즈
- 장력, 낚싯줄 내구도, 물고기 체력
- Seed 기반 재현 가능한 불규칙 행동
- 시작 화면, HUD, 결과 UI
- 독립 실행 씬과 Additive 통합용 씬
- 무료 CC0 캐릭터·환경·애니메이션 물고기
- 교체 가능한 `FishingVisualSet`, `AnglerVisualAdapter`, `FishVisualAdapter`
- 캐릭터 손의 `RodGripAnchor`
- 물고기 입의 `HookAnchor`

## 주요 구조

- Core 및 DTO: `Assets/FishingMiniGame/Runtime/Core`
- 게임 제어: `Assets/FishingMiniGame/Runtime/Application`
- 로컬 입력/Authority: `Assets/FishingMiniGame/Runtime/Infrastructure`
- 화면 및 UI: `Assets/FishingMiniGame/Runtime/Presentation`
- 메인 프로젝트 통합 Facade: `Assets/FishingMiniGame/Runtime/Integration`
- Editor 구성 도구: `Assets/FishingMiniGame/Editor`
- 테스트: `Assets/FishingMiniGame/Tests`
- 명세와 계획: `Docs`

## 현재 검증 결과

- Unity 컴파일 오류: 0
- 씬 누락 스크립트 및 깨진 프리팹: 0
- EditMode 테스트: 7/7 통과
- PlayMode 테스트: 3/3 통과
- Mock 입력으로 고등어 → 방어 → 참돔 연속 포획 확인

## 의도적으로 미구현한 범위

- 실제 IoT 장치 및 센서
- Serial/Bluetooth 통신
- 실제 멀티플레이 네트워크
- 팀 메인 로비 및 세션 연결
- 서버 권한 동기화
- 전용 낚시 모션과 정밀 양손 IK
- 출시급 VFX, 사운드, 어종 모델

## 알려진 시각적 한계

- 무료 캐릭터는 Generic 리그이며 전용 캐스팅/릴링 애니메이션이 없습니다.
- 현재 캐릭터 움직임은 Idle 애니메이션과 절차적 기울기 중심입니다.
- 물고기는 게임용 스타일라이즈드 근사 모델이며 생물학적으로 정확한 모델은 아닙니다.
- 무료 에셋은 모두 프리팹/VisualSet을 통해 추후 유료 또는 커스텀 에셋으로 교체할 수 있습니다.

## 라이선스

외부 아트 에셋의 출처와 라이선스는 `Assets/FishingMiniGame/Art/ThirdPartyLicenses.md`에 기록되어 있습니다.

## 대표 화면

`Assets/FishingMiniGame/Screenshots/hook-anchor-fixed.png`

