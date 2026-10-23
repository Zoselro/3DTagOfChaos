# 조사 보고서: TagOfChaos 아키텍처·코드 품질 종합 감사 (2026-10-11 6차)

> **개정 안내 (6차)** — 5차 판(2026-10-02)을 **지우지 않고 갱신**했다. 이 문서 앞부분(1~15장)이 6차 판이고, **5차 판 원문은 맨 뒤 「부록 Z」에 한 글자도 바꾸지 않고 그대로 보존**했다.
> 코드 주석이 5차 판의 절 번호(`research.md R4.10-2`, `R5-14`, `§8.21` 등)를 참조하므로 그 번호 체계는 부록 Z에서 그대로 찾을 수 있다. 6차 판의 새 지적은 **F-01~F-25** 번호를 쓰고, 5차 판과 같은 문제는 옛 번호를 함께 적었다.
> 위치: 프로젝트 루트에는 `research.md`가 없고 `Plan.md/research.md`가 기존 문서다(문서는 `Plan.md/`에 둔다는 프로젝트 규칙). 그래서 이 파일을 갱신했다.
> 7차(2026-10-12): 아래 「7차 상태 갱신」 표가 F-항목의 현재 상태다.
> 상태 표기: ✅ 해결됨 / 🔁 부분 해결 / ⏸ 유지 / 📈 악화(회귀) / 🆕 신규.

## 7차 상태 갱신 (2026-10-12) — `AuditFixPlan.md` 수정 결과

> 6차 본문(아래 1~15장)은 그대로 두고 F-항목의 현재 상태만 적는다. 근거·변경 파일·검증은 `Plan.md/AuditFixPlan.md` 각 장의 "결과". 클라이언트 조작 경로(S-01~S-15)도 같은 문서 2장·7~10장.

| F | 상태 | 어떻게 |
|---|---|---|
| F-01 | ✅ | 들리는 쪽이 보낸 사람·거리·이미 들림·들고 있음을 보고 거절, 드는 쪽 되돌림(`OnGrabRejected`) |
| F-02 | ✅ | 출시 씬 6개의 `OfflineModeBootstrap` 오브젝트 삭제 |
| F-03 | ✅ | 방장이 마지막으로 보낸 괴물 목록에서 이어 계산(`MonsterRoster`) — 동시 지원 유실 없음 |
| F-04 | ✅ | 남은 괴물 = 방에 남은 사람과의 교집합 |
| F-05 | ✅ | 로켓 발사 한 번만(`launchRequested`) |
| F-06 | ✅ | 칠하기·괴물 지원·문 요청을 보낸 사람(`Sender`)과 거리로 검증 |
| F-07 | ✅ | "탈출했나" = 방장이 쓴 `EscapeState` 하나(`EscapeManager.HasLeftMap/HasEscaped`) |
| F-08 | ⏸ | 결정 Q6 — 별도 작업 |
| F-09 | ✅ | 예비 경로는 `SceneLoadGrace` 뒤에만 색칠 시간을 쓴다 |
| F-10 | ✅ | 모든 클라이언트가 0.2초 간격으로 위치 기록 |
| F-11 | ✅ | `PlayerList` 반복 할당 제거(`RoomState.Players`) |
| F-12 | ✅ | `GameSceneCore` 미사용 UI 4종·PhotonView 삭제(변장 시간은 위쪽 하나만) |
| F-13 | ✅ | getter 22개·문구 5개·소리 ID 3개 삭제(공개 메서드 4개는 결정 Q5로 유지) |
| F-14 | ✅ | 파티클·승객 인형·`FindDeep` 도우미 하나로 |
| F-15 | ✅ | 재질 공유 캐시·`MaterialPropertyBlock`·로켓 부품 캐시 |
| F-16 | ✅ | 콜백 없는 PUN 상속 6개 → `MonoBehaviour`(`HideOrSeekPlayer`는 이제 콜백을 써서 유지) |
| F-17 | ✅ | 강퇴용 `EnableCloseConnection`은 대기실을 떠날 때 끔 |
| F-18 | ✅ | `EscapeState.Revision` — 방장 화면은 옛 상태 무시 |
| F-19 | ✅ | `Give` 실패 없음(void), 훔치기 순서 정리 |
| F-20 | ✅ | `WitchStrikeAt`로 방장이 바뀌어도 마녀 경과 이어짐(도구 쿨다운·훔치기 기록은 의도대로 초기화) |
| F-21 | ✅ | 레시피·오븐 문을 씬에 직접 연결(이름 규칙은 예비) |
| F-22 | ✅ | `SerializationRate`를 앱 시작 때 설정 |
| F-23 | 🔁 | 자동 PlayMode 시험은 범위 밖 — 사람이 하는 멀티 절차(`AuditFixPlan.md` 16장)와 시험 메뉴 |
| F-24 | ⏸ | 문서화만 |
| F-25 | ✅ | 잡은 수는 방장이 누적해 씀 |

- 시험: EditMode 283개 모두 통과(`PlayerTestScene.unity`는 빌드 목록에서 뺐다). 실제 다인원 동작은 `AuditFixPlan.md` 16장 절차로 확인 예정.

## 한눈에 보기

| 항목 | 값 |
|---|---|
| 확인된 문제 수 | **25건**(F-01~F-25) |
| 심각도별 | Critical **0** · High **1** · Medium **8** · Low **10** · Info **6** |
| 확신 수준별 | 확인됨 **22** · 가능성 높음 **2**(F-05, F-18) · 추가 확인 필요 **1**(F-09). 확인됨 중 F-03·F-04·F-06·F-11은 원인이 코드로 확인됐고, 실제 발생 빈도·재현(다중 클라이언트·조작 클라이언트·Profiler)은 14장 절차로 확인해야 한다 |
| 조사한 파일 | 런타임 C# **165개(17,831줄) 전부 정적 스캔**, 그중 **48개는 직접 정독**(목록 1.3) · 에디터 C# 44개(12,221줄)는 참조·시험 관점 스캔 · 씬·프리팹·SO **196개 YAML** 스캔 · `GameSceneCore.prefab`·캐릭터 프리팹 2개는 에디터에서 열어 구성 확인 · PUN 소스 3곳(`Room.SetCustomProperties`, `LoadBalancingClient.OpSetPropertiesOfRoom`, `PhotonNetwork.PlayerList`) |
| 시험 현황(2026-10-11 18:55 UTC 실행) | EditMode **265개: 통과 258 · 실패 6 · 보류 1** — 실패 6개는 모두 F-02, 보류 1개는 F-13(클립 없는 소리 ID) |
| 검증하지 못한 항목 | 실제 2인 이상 온라인 동작(이 환경은 오프라인 1인 Play Mode만 가능), 조작된 클라이언트 재현(F-06), 실제 지연 환경의 경합(F-03·F-04·F-05), 씬 로드 첫 프레임의 메시지 처리 순서(F-09), 에디터 스크립트 줄 단위 감사, Photon 대시보드 설정 — 15장 |

**가장 먼저 확인할 것**
1. **F-01(High)** 이미 다른 쿠키에게 들린 쿠키를 또 들 수 있다 → 두 쿠키가 같은 쿠키를 "들고 있는" 상태가 되고, 앞사람이 내려놓으면 뒷사람 손에서 빠진다. 들고 있는 쿠키도 들 수 있어 연쇄 캐리가 생긴다. 경합이 아니라 **세 명만 가까이 있으면 그대로 재현되는 경로**다.
2. **F-02(Medium, 📈 회귀)** 개발용 `OfflineModeBootstrap`이 출시 씬 6개(대기실 + 맵 5)에 다시 저장돼 있다. 비활성이라 빌드에서 실행되지는 않지만 `BuildSceneTests` 6개가 계속 실패해 **진짜 실패를 가린다**.
3. **F-03·F-04(Medium)** 괴물이 2명인 방(7~8명)에서만 생기는 경합 두 가지 — 가마솥 동시 지원 유실, 괴물 두 명 동시 퇴장 시 목록에 나간 괴물이 남음.

---

## 목차

1. 조사 개요 및 범위
2. 프로젝트 구조와 주요 클래스의 책임
3. 클래스 간 의존성 및 호출 관계
4. 전체 조사 결과 요약
5. 심각도별 문제 목록
6. 필수 조사 항목별 상세 분석(A~G)
7. Photon PUN2 네트워크 구조 분석
8. Unity Lifecycle 및 이벤트 흐름 분석
9. 중복 코드와 미사용 코드 분석
10. 성능 및 메모리 관리 분석
11. 기존 설계의 장점과 유지해야 할 구조
12. 우선순위별 개선 권장 사항
13. 수정 시 주의해야 할 의존 관계와 회귀 위험
14. 추가로 Unity Editor에서 검증해야 할 항목
15. 조사하지 못했거나 결론을 내릴 수 없는 항목
- 부록 Z. 5차 판(2026-10-02) 원문 보존

---

## 1. 조사 개요 및 범위

### 1.1 목적과 원칙
- 코드 품질·아키텍처·유지보수성·Unity 특성·Photon PUN2 구조를 **실제 코드와 참조 관계를 따라가며** 판단했다. 이름만 보고 미사용이라 단정하지 않았다(9장 방법 참고).
- **조사만 했다.** C#·프리팹·씬·설정·패키지는 고치지 않았다(13장 끝의 변경 파일 점검 결과 참고).
- 단순 스타일 취향은 지적하지 않았다. 결함(동작 오류·상태 불일치)과 유지보수 위험을 구분해 심각도를 매겼다.
- 기존 설계 의도(방장 권위·Room Prop 단일 상태·순수 함수 규칙 분리)를 먼저 파악하고, 그 의도 안에서 어긋난 곳만 지적했다. 새 패턴 도입을 권하지 않았다.

### 1.2 방법
1. **전체 정적 스캔(165개 파일)** — 주석을 뺀 코드에서 다음을 모았다: 타입·멤버 선언과 모든 참조, `Update` 계열 본문(+ 같은 파일 안에서 부르는 메서드 1단계)의 비싼 호출, 이벤트 `+=`/`-=` 짝, 정적 가변 상태, `Find`·`Camera.main`·`AddComponent`, `[PunRPC]`·`.RPC(`·`RaiseEvent`·`OnEvent`, `SetCustomProperties` 쓰는 곳, `PhotonNetwork.Instantiate/Destroy`.
2. **정적 검색으로 안 보이는 사용처** — 씬·프리팹·SO 196개의 YAML에서 스크립트 배치 수와 **UnityEvent 저장 메서드 이름**(`m_MethodName`)을 모았고, `nameof`·문자열 RPC 이름·`Invoke(nameof…)`·`[RuntimeInitializeOnLoadMethod]`·인터페이스 구현(`IJob.Execute` 등)·Unity 메시지 메서드를 미사용 후보에서 뺐다. 에디터 코드·시험 코드의 참조도 셌다.
3. **핵심 흐름 정독(48개 파일)** — 판 시작 → 색칠 → 괴물 합류 → 탈출 요청·판정 → 마녀 → 결과 → 대기실 복귀, 방장 교체·퇴장, 쿠키 들기·잡기, 이벤트 수신 검증.
4. **PUN 동작은 프로젝트에 들어 있는 PUN 소스로 확인** — 온라인 방에서 `SetCustomProperties`는 서버 응답 전까지 로컬 캐시를 바꾸지 않는다(`LoadBalancingClient.OpSetPropertiesOfRoom`: `BroadcastPropertiesChangeToAll`이면 캐시 갱신 없음), `PhotonNetwork.PlayerList`는 부를 때마다 `OrderBy(...).ToArray()`로 새 배열을 만든다.
5. **에디터에서 실물 확인** — `GameSceneCore.prefab` 전체 계층·참조, 캐릭터 프리팹 PhotonView 설정(관찰 대상·소유권·동기화 방식), 출시 씬에 저장된 개발용 오브젝트, 칠하기 화면 Play Mode 캡처(F-12).
6. 5차 판의 모든 결함 항목을 현재 코드와 다시 대조했다(4.3).

### 1.3 직접 정독한 파일(48개, `Assets/02. Scripts/` 기준)
Escape: `EscapeManager`, `EscapeAuthority`, `EscapeRules`, `EscapeNet`(일부), `EscapeState`(디코드), `EscapeCharacterState`, `MonsterEscapeState`, `GroundItemView`, `SpyRocket`, `WitchTimeline`, `EscapeVisuals`(일부), `EscapeExitFx`(일부), `EscapeHud`(Update·탑승 표시) ·
Monster: `GameRuleController`, `GamePhaseStarter`, `MonsterJoinController`, `MasterClientPolicy`, `RoomLifecycleWatcher`, `MonsterAssignmentAuthority`, `Cauldron`, `MonsterGrabKillTrigger`, `ResultScreenController` ·
Unit: `HideOrSeekPlayer`(동기화·잡기·Update·회피), `PlayerGrabController`, `PlayerCarryFollower`(일부), `PlayerNetworkSync` ·
Core: `RoomState`, `NetworkTransformSync`, `RoomSceneTransition`, `PlayerInput`(일부), `GamePhaseState`, `PhaseCountdownDisplay`(일부), `NetKeys`(범위표) ·
Lobby: `GameStartAuthority`, `LobbyKick`, `GameLobbyController`, `LobbyController`(접속 부분), `PlayerListItem` ·
GameManager: `GameManager`, `EscMenu`, `OfflineModeBootstrap`(Dev) ·
ColorTag: `PaintPhaseController`, `PlayerPaintCanvas`(이벤트·정리), `ColorPaletteSO`, `PaintColliderUpdater`(일부) ·
Environment: `InteractableDoor`(네트워크 부분) · Audio: `AudioRuntime`(생성·정리), `MusicRules`, `MusicCatalogSO`, `MusicDirector`(징글) · Interaction: `CharacterInteractor`(생성) · Camera: `WitchRevealCamera`.

### 1.4 범위 밖
- `Assets/Photon/`(서드파티, 217개)는 동작 확인에 필요한 3곳만 읽었다.
- 에디터 스크립트 44개는 런타임 영향(빌드 도구·설치 도구·시험)만 봤고 줄 단위 감사는 하지 않았다(5차 판과 같음).
- 맵 씬의 오브젝트 단위 아트·조명 설정은 보지 않았다(스크립트 배치만).

---

## 2. 프로젝트 구조와 주요 클래스의 책임

### 2.1 엔진·구성
- Unity **6000.0.58f2**, Photon **PUN 2.55**, 내장 렌더 파이프라인, TextMesh Pro, 레거시 Input Manager(`Core/PlayerInput`) + Input System 패키지 병행.
- 어셈블리: `TagOfChaos.Scripts`(런타임), `TagOfChaos.Editor`, `TagOfChaos.EditorTests`. PlayMode 시험 어셈블리는 없다(F-23).
- 빌드 씬: `PlayerTestScene`(꺼짐) · `LobbyScene` · `GameLobbyScene` · 맵 5개(`Game_CandyForest`, `Game_GingerbreadVillage`, `Game_ChocolateFactory`, `Game_CursedCandyCarnival`, `Game_HauntedBakery`).
- 네트워크 설정: `SendRate 30`, `SerializationRate`는 로비에서 `GameSettings.CharacterSyncRate`로 설정(기본 10), `RunInBackground true`, 지역 고정은 코드가 사본 설정으로 처리(`LobbyController.ConnectToRegion`, 에셋을 바꾸지 않음 — 양호).

### 2.2 폴더별 규모(런타임)

| 폴더 | 파일 | 줄 | 책임 |
|---|---:|---:|---|
| Escape | 40 | 5,600 | 탈출 모드: 상태(`EscapeState`, byte[] Room Prop 하나), 방장 권위(`EscapeAuthority`), 화면 맞춤(`EscapeManager`), 장치·상자·로켓·도구·HUD·맵 연출 5종·마녀 |
| Audio | 26 | 2,871 | 소리 런타임(풀·음악·추격·환경음·출력 장치) |
| Monster | 20 | 2,128 | 괴물 조작, 승패 판정, 괴물 선정·합류, 방장 정책, 관전, 결과 화면 |
| ColorTag | 11 | 1,391 | 색칠(캔버스·붓·팔레트), 색칠 단계 |
| Lobby | 12 | 1,346 | 로비·대기실·방 설정·지역 이동·강퇴·시작 권한 |
| Core | 21 | 1,288 | 공용 계약·Room Prop 조회(`RoomState`)·키(`NetKeys`)·단계 해석(`GamePhaseState`)·입력·설정 SO |
| Unit | 11 | 1,085 | 쿠키 캐릭터·들기·동기화 |
| Camera | 5 | 619 | 3인칭·괴물 1인칭·마녀 카메라·흔들림 |
| GameManager | 8 | 583 | 채팅·스폰·나가기·ESC 메뉴 |
| Environment | 4 | 478 | 문·깜빡이는 조명·가마솥 연출 |
| Interaction | 5 | 356 | 상호작용(가까운 대상·길게 누르기·안내 UI) |
| Dev | 2 | 86 | 오프라인 부트스트랩·괴물 시험 스폰 |
| **합계** | **165** | **17,831** | (5차 판 127개·12,889줄 → +38개·+4,942줄) |

### 2.3 큰 클래스(Info, F-24)
`ColorTag/PlayerPaintCanvas` 641줄 · `Escape/EscapeAuthority` 640줄 · `Unit/HideOrSeekPlayer` 508줄 · `Monster/MonsterController` 438줄 · `Escape/EscapeHud` 384줄(UI를 코드로 생성) · `Audio/AudioRuntime` 380줄 · `Lobby/LobbyController` 370줄.

### 2.4 주요 클래스의 책임(입력 → 상태 변경 → 출력)

| 클래스 | 실행 위치 | 입력 | 바꾸는 상태 | 외부 의존 |
|---|---|---|---|---|
| `GameStartAuthority`(static) | 방장 | 호스트의 시작 요청(`StartGameRequest`) | `PaintPhaseEndTime`·`GameMapScene`·`SpyActorNumbers`, 방 닫기, 씬 이동 | `RoomState`, `GameSettings`, `RoomSceneTransition` |
| `MonsterAssignmentAuthority` | 방장(대기실) | `ClaimMonster` 이벤트 | `MonsterActorNumbers`·`MonsterRevealTime` | `RoomState` |
| `MasterClientPolicy` | 방장 | 입장·퇴장·괴물 목록 변경 | Photon 방장(`SetMasterClient`) | `RoomState.DesiredMasterActor` |
| `MonsterJoinController` | 방장 + 괴물 본인 | `PaintPhaseEndTime` 경과 | `MonsterJoined`·`GameEndTime`, 괴물 `PhotonNetwork.Instantiate` | `RoomState`, `SceneSpawnPoints` |
| `PaintPhaseController` | 방장 | 색칠 종료 | `ForcedPaint*` | 팔레트 SO |
| `EscapeManager` | 모두(맵 씬) | Room Prop `EscapeState`, 로컬 요청 | 화면(상자·바닥 아이템·장치·로켓), 요청 전송 | `EscapeAuthority`, HUD, 정적 조회 제공 |
| `EscapeAuthority`(순수 C#) | 방장 | `EscapeRequest`(발신자 = `EventData.Sender`) | `EscapeState` 전체, `WitchMode`·`WitchAppearAt`·`SpyEscapedAt`·`TimeAttackEndTime`, 알림·도구 명중 이벤트 | 카탈로그·레시피 SO |
| `GameRuleController` | 방장 | 플레이어 Props·`EscapeState`·시각 | `WitchStrike`, `GameResult`·`EscapeEndReason`·`RevealedSpies` | `EscapeRules`(순수), `WitchTimeline`(순수) |
| `RoomLifecycleWatcher` | 방장(맵) | 괴물 퇴장 | 괴물 목록·`MonsterDepartedAt`, 대기실 복귀 | `WitchTimeline` |
| `ResultScreenController` | 모두 | `GameResult` | 결과 UI, 12초 뒤 방장이 대기실로 | `EscapeManager`, `MonsterJarTrophy` |
| `HideOrSeekPlayer` | 모두(소유자 = 물리) | 입력·동기화·RPC | 위치·상태, 본인 Player Props(`HitCount`·`DeathCause`) | 협력 클래스 6종 + 런타임 `AddComponent` 6종 |
| `PlayerGrabController` | 소유자 | 잡기 키 | 들기 관계(RPC) | `HideOrSeekPlayer` |
| `MonsterGrabKillTrigger` | 괴물 소유자 | 조준·상호작용 | `RequestGrabKill` RPC, 본인 `CatchCount` | 물리 질의 |

---

## 3. 클래스 간 의존성 및 호출 관계

### 3.1 도메인 의존 행렬(최상위 타입 이름 참조, Core 제외)
`Core`는 다른 도메인 타입을 참조하지 않는다. 처음 행렬에서 `Core → Escape`로 보였던 5건은 `NetKeys.EscapeState`처럼 **같은 이름의 문자열 상수**였다(타입 의존 아님).

| 도메인 → Escape | 참조하는 Escape 타입 | 파일 |
|---|---|---|
| Monster | `EscapeManager`(4), `EscapeState`(2), `WitchMode`(2), `WitchTimeline`(2), `EscapeRules`, `EscapeEndReason`, `DeathCause`, `MonsterEscapeState`, `StunReceiver`, `MonsterJarTrophy` | `GameRuleController`, `MonsterController`, `ResultScreenController`, `RoomLifecycleWatcher`, `SpectatorController` |
| Audio | `EscapeManager`(4), `EscapeState`(2), `ItemLocation` | `EscapeAudioPresenter`, `MusicDirector`, `AmbientEmitter`, `CookieAudio` 등 |
| Unit | `EscapeManager`(2), `PlayerInventory`(2), `EscapeCharacterState`, `HeldItemPresenter`, `StunReceiver`, `DeathCause` | `HideOrSeekPlayer`, `PlayerGrabController` |
| Camera | `EscapeManager`, `WitchPresenter`, `WitchMode`, `WitchTimeline` | `WitchRevealCamera` |

- **양방향(순환) 의존**: `Escape ↔ Monster`, `Escape ↔ Unit`, `Escape ↔ Audio`, `Escape ↔ Camera`. 대부분 Escape가 캐릭터·소리를 쓰고, 캐릭터·소리가 `EscapeManager`의 **정적 조회**(`IsWaiting`·`HasLeftMap`·`IsActive`·`Instance.State`)를 직접 부르는 형태다(F-07, 5차 R4.2-2 ⏸).
- 단일 어셈블리라 컴파일 순환은 없지만, Escape를 빼거나 바꾸면 Unit·Monster·Audio·Camera를 함께 고쳐야 한다.

### 3.2 한 판의 호출 흐름(탈출 모드)
1. 호스트 버튼 → `GameLobbyController.OnStartGameButtonClicked` → `GameStartAuthority.RequestStart` → (방장) `TryStart`: `PaintPhaseEndTime`·맵·스파이를 **한 요청으로** 쓰고 → `RoomSceneTransition.LoadLevelForRoom`(캐시 삭제 → `LoadLevel`).
2. 맵 씬: `PlayerSpawner`(쿠키 `PhotonNetwork.Instantiate`) · `GamePhaseStarter`(대비책: `PaintPhaseEndTime`이 없을 때만 씀, F-09) · `EscapeManager.Update`(방장, `CanInitialize` → `EscapeAuthority.Initialize` → `Commit`).
3. 색칠 종료 → `PaintPhaseController`(강제 색) · `MonsterJoinController`(`MonsterJoined`·`GameEndTime`, 괴물 본인 스폰).
4. 요청: 쿠키 → `EscapeManager.Request` → `RaiseEvent(EscapeRequest, MasterClient)` → 방장 `OnEvent` → `EscapeRequest.TryParse(e.Sender)` → `EscapeAuthority.Handle` → 바뀌면 `Commit`(Room Prop + 방장 화면 즉시).
5. 판정: 방장 `GameRuleController.Update` 매 프레임 → `EscapeRules.Evaluate`(순수) → `WitchStrike` / `GameResult`.
6. 마녀: `WitchTimeline`(순수 시각표) + `WitchPresenter`(모든 화면) + `WitchRevealCamera`(카메라) + 본인만 죽음 처리(`EscapeCharacterState`·`MonsterEscapeState`, `IsMine` 확인).
7. 결과: `ResultScreenController`(모두) → 12초 뒤 방장 `LoadLevelForRoom(GameLobby)` → 대기실 `RoundStateResetter`가 판 키 정리·방 재개방.

### 3.3 방장 전용 폴링(Update)
`GameRuleController`, `EscapeManager`(일부), `MonsterJoinController`, `PaintPhaseController`, `GamePhaseStarter`, `RoomLifecycleWatcher`, `MonsterAssignmentAuthority`, `MasterClientPolicy`(첫 프레임만), `RoundStateResetter` — 9개. 모두 "Room Prop 키 존재 + 로컬 요청 플래그"로 중복 쓰기를 막는 같은 패턴을 쓴다(5차 R4.11-19). **이 패턴에서 로컬 플래그를 빠뜨린 곳이 F-05**, 플래그 대신 옛 캐시로 계산한 곳이 F-04다.

---

## 4. 전체 조사 결과 요약

### 4.1 결론
- **탈출 모드의 네트워크 설계는 견고하다.** 상태 변경은 방장 한 곳(`EscapeAuthority`)에서만 일어나고(외부에서 `EscapeState`를 고치는 코드 **0곳** 확인), 요청자는 `EventData.Sender`로 정하며, 알림·도구 명중은 방장 발신만 받는다(`EscapeManager.SentByMaster`). 요청·상태 디코딩은 형식 검사와 `try`로 보호된다. 규칙(`EscapeRules`·`WitchTimeline`·`RecipePlanner`)은 순수 함수라 시험된다.
- **5차 판 결함 10건 중 7건이 해결됐다**(4.3). 남은 큰 결함은 **쿠키 들기(F-01)** 하나다.
- **새 위험은 "괴물 2명" 규칙과 "서버 응답 전 옛 캐시"의 조합에서 나온다**(F-03·F-04·F-05). 모두 서버 왕복 시간(RTT) 안에 두 일이 겹칠 때만 생긴다.
- **회귀 1건**: 개발용 부트스트랩이 출시 씬에 다시 저장돼 시험 6개가 계속 실패한다(F-02).
- 캡슐화는 양호하다: MonoBehaviour의 공개 가변 필드 **0개**, 전역 setter는 입력 잠금 2개뿐, ScriptableObject에 런타임 상태 저장 **없음**(조회용 캐시만).

### 4.2 필수 조사 항목별 판정

| 항목 | 판정 | 근거 요약 |
|---|---|---|
| A 사용 현황·중복 | ⚠ 경미 | 미사용 멤버 33개(대부분 진단용 getter)·미사용 SoundId 3개·미사용 UI 오브젝트 4개·중복 4종(F-12~F-14). 버려진 클래스는 없음 |
| B 아키텍처·책임 | ⚠ 유지보수 위험 | `EscapeManager` 정적 조회 결합(F-07), 런타임 `AddComponent` 구성(F-08), 큰 클래스(F-24). 캡슐화는 양호 |
| C Unity 객체·리소스 | ⚠ 경미 | 개발용 오브젝트가 출시 씬에(F-02), 머티리얼 누수(F-15). SO·풀은 양호 |
| D Lifecycle·실행 순서 | ✅ 대체로 양호 | PUN 콜백 재정의 11곳 모두 `base` 호출, 정적 이벤트 5종 해제 짝 맞음, 전역 입력 잠금 복구 경로 있음. 첫 프레임 경합(F-09)만 확인 필요 |
| E Photon PUN2 | ⚠ 결함 1·경합 3 | 들기 수락 절차 없음(F-01), 발신자 미검증 3종(F-06), RTT 경합(F-03~F-05), 방장 교체 시 위치 기록 없음(F-10) |
| F 성능·안정성 | ⚠ 경미 | 매 프레임 `PlayerList` 할당(F-11), 매 프레임 `Find`·문자열(F-15). 예외 위험 경로는 찾지 못함 |
| G 게임 규칙·상태 전이 | ✅ 양호 | 판정은 `GameRuleController` 한 곳 + 순수 `EscapeRules`. 결과 1회 쓰기 가드·방장 교체 재판정 있음. 경합은 F-05 |

### 4.3 5차 판(2026-10-02) 결함 목록의 현재 상태

| 5차 번호 | 내용 | 현재 | 근거(현재 코드) |
|---|---|---|---|
| R4.7-9 | 마녀 카메라 흔들림이 덮임 | ✅ | 흔들림은 오프셋(`CameraShake.Offset`)으로, 카메라 쪽이 자세를 정한 뒤 더한다: `Camera/Camera_Ctrl.cs:104·136`, `MonsterFirstPersonCamera.cs:82`, `WitchRevealCamera.cs:161` |
| R4.10-9 | 방장 교체 뒤 스파이 잡힘 알림 중복 | ✅ | 방장이 아니어도 잡힌 사람을 기록: `Escape/EscapeManager.cs:131·150-157` |
| R4.10-8 | `ToolHit`·`EscapeNotice` 발신자 미검증 | ✅ | `EscapeManager.SentByMaster`(`:271-278`), 방장이 명중 대상 거리 재검사(`EscapeAuthority.ValidTargets`, `:364-385`) |
| R5-14 | 기절·들림 중 회피 동결 | ✅ | 잠긴 동안 `DodgeOut()`: `Unit/HideOrSeekPlayer.cs:316` |
| R5-19 | 기절 순간 미끄러짐 | ✅ | `HideOrSeekPlayer.cs:332-333` |
| R4.7-10 | 기절한 원격 쿠키 보간 정지 | ✅ | `HideOrSeekPlayer.UpdateRemote`(`:303-310`) |
| R5-20 | 색칠 중 ESC 메뉴 클릭이 몸에 칠해짐 | ✅ | `ColorTag/PlayerPaintCanvas.cs:227·421`, `BrushCursorController.cs:60` |
| R4.7-3 | `GetComponent<T>()?.` 가짜 null | ✅ | `TryGetComponent`로 바뀜(남은 `?.`는 `GameLobbyController.cs:155`의 `FindFirstObjectByType` 결과 — 진짜 null이라 안전) |
| R4.8 | `EscapeHud`가 인벤토리 `Changed` 해제 누락 | ✅ | `Escape/EscapeHud.cs:211` |
| R5-21 | 런타임 셰이더 키워드 변형 누락 | ✅ | 재질 틀 에셋을 Resources에서 복제(`EscapeVisuals.Template`, `:97-108`) |
| R4.10-1 | 쿠키 들기 수락 절차·발신자 확인 없음 | ⏸ → **F-01(High)** | 결정적 재현 경로 추가 확인 |
| R4.7-7 | `GamePhaseStarter` 첫 프레임 경합 | ⏸ → **F-09** | 추가 확인 필요 |
| R4.10-10 | 연속 커밋 시 옛 응답이 화면을 잠깐 되돌림 | ⏸ → **F-18** | `EscapeAuthority.Commit`(`:619-624`) 그대로 |
| R4.9 | `EscapeVisuals.Tint` 머티리얼 누수 | ⏸ → **F-15** | `:73-80` 그대로 |
| R4.4-9 | 맵 씬의 `__OfflineBoot` | 5차 ✅ → **📈 F-02** | 6개 씬에 다시 저장됨 |
| R4.2-2 · R4.5 | `EscapeManager` 정적 조회 결합 | ⏸ → **F-07** | |
| R4.3 | 런타임 `AddComponent` 구성 | ⏸(증가) → **F-08** | 쿠키 7종·괴물 4종 |
| R4.11 | 중복 로직 | ⏸ → **F-14** | |

---

## 5. 심각도별 문제 목록

> 형식: **ID · 심각도 · 확신 수준** — 위치 / 현재 구현 / 원인 / 영향 / 발생 조건·재현성 / 수정 방향 / 수정 시 영향 범위.
> 줄 번호는 2026-10-11 작업 트리 기준, 경로는 `Assets/02. Scripts/` 기준.

### Critical (0건)
없음. 게임 진행을 멈추거나 네트워크를 깨뜨리는 문제는 찾지 못했다.

### High (1건)

#### F-01 · High · 확인됨(코드) — 이미 들린 쿠키·들고 있는 쿠키를 또 들 수 있다 (5차 R4.10-1 ⏸)
- **위치**: `Unit/PlayerGrabController.cs` `TryGrab()` 51-70, `Release()` 72-87 · `Unit/HideOrSeekPlayer.cs` `OnGrabbedByOwner` 103-108, `OnReleased` 110-115 · `Unit/PlayerCarryFollower.cs` `TryAttach` 33-44.
- **현재 구현**: 드는 쪽이 범위 안 쿠키를 찾으면 그 쿠키 주인에게 `OnGrabbedByOwner(내 ViewID)` RPC를 보내고 **응답을 기다리지 않고** 자기 쪽 `carriedPlayer`를 바로 정한다. 들리는 쪽은 `pv.IsMine && !IsBroken`만 보고 `TryAttach`로 `carrierViewId`를 **덮어쓴다**. 내려놓기 RPC `OnReleased`는 누가 보냈는지 보지 않고 무조건 떨어진다.
- **원인**: (1) `TryGrab`이 대상이 이미 들려 있는지(`CurrentCarrierViewId >= 0`)·대상이 다른 쿠키를 들고 있는지 확인하지 않는다. (2) 받는 쪽에 수락·거절 절차가 없다. (3) 두 RPC에 `PhotonMessageInfo`가 없어 발신자를 검증할 수 없다.
- **영향**: ① A가 C를 든 상태에서 B가 C를 들면 C는 B를 따라가지만 A는 계속 "들고 있는" 자세·상태로 남는다. ② 그 뒤 A가 내려놓으면 `OnReleased`가 C에 가서 **B 손에서 C가 빠진다**(B는 들고 있다고 믿음). ③ C가 D를 들고 있을 때 E가 C를 들면 C는 들린 채 D를 계속 들고 있다(연쇄 캐리). C는 이동이 잠겨 D를 내려놓을 수도 없다(`PlayerGrabController.Update` 41행에서 잠기면 입력을 받지 않음 — 정리는 34-38행의 "대상 파괴·탈출" 조건뿐). 탈출 판정·관전 대상과는 무관하지만 들기 상태가 꼬여 조작이 막힌다.
- **발생 조건·재현성**: 쿠키 3명이 서로 1.5 m 안에 있으면 경합 없이 바로 재현된다(네트워크 지연과 무관). 조작된 클라이언트는 아무 쿠키에나 `OnReleased`·`OnGrabbedByOwner`를 보낼 수 있다.
- **수정 방향**: 가장 작은 수정은 (a) `TryGrab`에서 `target.CurrentCarrierViewId >= 0`이거나 대상이 `IsCarrying`이면 건너뛰고, (b) `OnGrabbedByOwner`에서 이미 들려 있으면 무시, (c) 두 RPC에 `PhotonMessageInfo`를 받아 `OnReleased`는 지금 들고 있는 쪽(`carrierViewId`의 Owner)이 보낸 것만 받는 것이다. 드는 쪽 상태를 응답 뒤에 정하는 수락 절차는 그다음 단계다.
- **수정 시 영향**: `CarriedBodyPresenter`(손 위 자세), `PlayerNetworkSync.RemoteCarrierViewId`, 그랩버 화면의 `SetLocalCarrierOverride`(0.6초 임시 값), `CookieAudio`(들림 소리), 관전(`IsSpectatable`)과 맞물린다. RPC 서명을 바꾸면 이전 빌드와 섞여 접속할 수 없다(같은 빌드끼리만 시험).

### Medium (8건)

#### F-02 · Medium · 확인됨 — 개발용 `OfflineModeBootstrap`이 출시 씬 6개에 저장됨 (📈 5차 R4.4-9 회귀)
- **위치**: `Assets/Scenes/GameLobbyScene.unity`, `Assets/Scenes/Maps/Game_{CandyForest,GingerbreadVillage,ChocolateFactory,CursedCandyCarnival,HauntedBakery}.unity` — 이름 `GameObject`, `m_IsActive: 0`, 컴포넌트 `Dev/OfflineModeBootstrap`(`autoCreateRoom: 1`). 시험: `Assets/Editor/Tests/BuildSceneTests`의 `ReleaseScene_HasNoDevScripts` 6건 실패.
- **현재 구현**: 오브젝트가 비활성이라 `Awake`가 돌지 않아 빌드 동작에는 영향이 없다.
- **원인**: 오프라인 시험을 위해 씬에 부트스트랩을 붙였다가 지우기 전에 씬이 저장된 것으로 보인다(파일 수정 시각: `GameLobbyScene` 10-12 02:32, `Game_HauntedBakery` 10-12 03:53 로컬). 이 환경의 자동 시험 도구도 `__OfflineBoot`를 붙였다 떼는 방식이라 같은 위험이 있다.
- **영향**: ① **시험 스위트가 늘 빨간색이라 새 실패가 묻힌다**(지금도 265개 중 실패 6개가 "원래 실패"로 취급되고 있다). ② 누가 실수로 오브젝트를 켜면 빌드가 오프라인 모드로 시작한다(`Awake`에서 `PhotonNetwork.OfflineMode = true`).
- **재현**: 전체 EditMode 시험 실행.
- **수정 방향**: 6개 씬에서 그 오브젝트를 지우고 저장 → 시험 6개 통과 확인. 재발 방지는 이미 있는 `BuildSceneTests`가 맡는다(빌드 전 시험을 돌리는 습관이 필요).
- **수정 시 영향**: 씬 파일만 바뀐다. `PlayerTestScene`(빌드 꺼짐)의 부트스트랩 2개는 의도된 것이라 남긴다.

#### F-03 · Medium · 확인됨(코드), 빈도는 추가 확인 필요 — 괴물 2명 방에서 가마솥 동시 지원 하나가 사라진다
- **위치**: `Monster/MonsterAssignmentAuthority.cs` `OnEvent` 23-40(26행 `confirmRequested`면 무시), `ConfirmMonsters` 115-128 · `Monster/Cauldron.cs` `OnTriggerEnter` 13-34.
- **현재 구현**: 방장은 지원을 받으면 `confirmRequested = true`로 쓰고, 서버 응답(`OnRoomPropertiesUpdate`)이 올 때까지 다음 지원을 **버린다**. 가마솥은 들어가는 순간 한 번만 보낸다.
- **원인**: "응답 전 중복 쓰기 방지"용 플래그가 "다른 사람의 정당한 지원"까지 막는다. 지원을 모아 두거나 다시 보내는 경로가 없다.
- **영향**: 괴물이 2명 필요한 7~8명 방에서 두 사람이 RTT 안에 가마솥에 들어가면 두 번째 사람은 가마솥에 서 있어도 괴물이 되지 않는다. 대기실 문구는 "1명 더 들어가야 함"을 보여 주고, 그 사람은 나갔다가 다시 들어가야 한다.
- **재현성**: 낮음~중간(사람들이 가마솥으로 몰릴 때). 온라인 8인 필요.
- **수정 방향**: 대기 중인 지원을 `HashSet<int>`로 모았다가 응답이 오면 이어서 확정하거나, 가마솥이 "안에 있고 아직 괴물이 아니며 자리가 남았으면" 일정 간격으로 다시 보내기(`OnTriggerStay` + 쿨다운).
- **수정 시 영향**: `MonsterRevealController`(공지), `GameLobbyController.RefreshStatus`(문구), `MasterClientPolicy`(괴물 확정 시 방장 이동)와 순서가 맞물린다.

#### F-04 · Medium · 확인됨(코드), 빈도는 추가 확인 필요 — 괴물 두 명이 RTT 안에 나가면 나간 괴물이 목록에 남는다
- **위치**: `Monster/RoomLifecycleWatcher.cs` `OnPlayerLeftRoom` 13-36(17행 캐시 읽기, 23행 계산).
- **현재 구현**: 퇴장 콜백마다 **캐시의** `MonsterActorNumbers`에서 나간 사람을 빼 새 배열을 쓴다.
- **원인**: 온라인에서 `SetCustomProperties`는 서버 응답 전까지 캐시를 바꾸지 않는다(PUN `OpSetPropertiesOfRoom` 확인). 두 퇴장 콜백이 응답 전에 연달아 오면 두 번째 계산은 옛 목록 `[A,B]`에서 B만 빼 `[A]`를 쓴다 — A는 이미 나갔다.
- **영향**: 남은 괴물이 0명인데 목록에는 A가 남아 "괴물 모두 이탈 → 경고 → 대기실 복귀"(GameRule.md §7.1)가 일어나지 않는다. 판은 시간 종료 등 다른 경로로 끝나므로 멈추지는 않지만, 괴물 없는 판이 남은 시간 내내 이어진다.
- **재현성**: 괴물 둘이 함께 나가거나 같은 네트워크에서 함께 끊길 때.
- **수정 방향**: 퇴장 때마다 캐시 대신 **현재 방에 실제로 있는 사람**(`CurrentRoom.Players`)과 괴물 목록의 교집합으로 계산한다. `MonsterAssignmentAuthority.OnPlayerLeftRoom`(64-74행, 대기실)도 같은 방식이라 함께 고친다.
- **수정 시 영향**: `MasterClientPolicy`(괴물 목록 변경에 반응), `GameRuleController`(괴물 목록으로 판정), 처단 중 예외(`witchPurging`, 27행).

#### F-05 · Medium · 가능성 높음(코드) — 로켓 발사 기록에 "보냈음" 로컬 가드가 없다
- **위치**: `Escape/EscapeAuthority.cs` `TryLaunchRocket` 424-448(426행 가드가 캐시의 `SpyEscapedAt`만 봄). 호출: `RocketBoard` 285, `OnPlayerLeft` 401, `Reevaluate` 420(→ `OnPlayerBroken` 408, 방장 교체 `EscapeManager.cs:183`).
- **현재 구현**: 처단(`TryStartPurge`)은 `purgeRequested` 로컬 플래그로 막지만, 로켓 발사는 캐시의 `SpyEscapedAt` 존재로만 막는다.
- **원인**: F-04와 같다(응답 전 캐시가 옛 값).
- **영향**: 마지막 스파이가 탄 직후 RTT 안에 다른 사람이 잡히거나 나가면 발사 기록이 한 번 더 써진다. `SpyEscapedAt`·`WitchAppearAt`·`TimeAttackEndTime`이 수십 ms 뒤로 밀리고, 두 호출 사이에 마지막 쿠키가 잡혔다면 첫 쓰기의 `WitchMode=1`·`TimeAttackEndTime`이 남은 채 두 번째 쓰기는 타임어택 없음으로 판단한다(키를 지우지 않음) → 쿠키가 없는데 타임어택 단계로 들어간다(`GamePhaseState`는 `TimeAttackEndTime` 존재로 판단). 다만 `EscapeRules`는 `activeCookies == 0`이면 바로 `EndAllResolved`를 내므로 판은 곧 끝난다.
- **재현성**: 드묾(두 사건이 RTT 안에 겹쳐야 함).
- **수정 방향**: `TryStartPurge`처럼 `launchRequested` 로컬 플래그를 두고 방장 교체 시에는 캐시로 다시 판단(`GameRuleController`의 `resultRequested` 패턴과 같게).
- **수정 시 영향**: `GameRuleController`(D10 판정), `SpyRocket.AnimateLaunch`(발사 시각 기준), `WitchRevealCamera`(로켓 샷 시각), 시험 `Request1011Tests.WitchTimeline_*`.

#### F-06 · Medium · 확인됨(코드), 재현은 조작 클라이언트 필요 — 이벤트 발신자 미검증 3종
- **위치와 현재 구현**:
  1. `ColorTag/PlayerPaintCanvas.cs` `OnEvent` 477-499: 이벤트 내용의 `viewId`가 내 캔버스면 그대로 칠하거나 지운다. **`photonEvent.Sender`가 그 캐릭터 주인인지 보지 않는다.**
  2. `Monster/MonsterAssignmentAuthority.cs` `OnEvent` 34행: 괴물 지원자를 **이벤트 내용의 번호**(`CustomData`)로 정한다(`Cauldron.cs:31`이 자기 번호를 넣는다). `photonEvent.Sender`를 쓰지 않는다.
  3. `Environment/InteractableDoor.cs` `OnEvent` 149-156: 방장이 문 요청의 거리·단계를 검사하지 않고 그대로 쓴다.
- **원인**: 탈출 요청(`EscapeRequest.TryParse(e.Sender)`)·강퇴(`LobbyKick.IsForMe`)·잡기(`RequestGrabKill`의 `info.Sender`)에 적용한 발신자 검증이 이 세 경로에는 없다.
- **영향**: 조작된 클라이언트가 ① 남의 위장 색을 지우거나 바꾸고, ② 다른 사람을 괴물로 지정하고(대기실 진행 조작), ③ 멀리서 문을 열고 닫을 수 있다. 정상 클라이언트끼리는 생기지 않는다.
- **수정 방향**: ① `photonEvent.Sender == pv.Owner.ActorNumber` 확인, ② `CustomData` 대신 `photonEvent.Sender` 사용(내용은 무시), ③ 요청자 캐릭터 위치와 문 거리 확인(이미 있는 `CharacterRegistry`로).
- **수정 시 영향**: 오프라인 모드에서는 발신자가 1번으로 채워지므로(`EscapeManager.SentByMaster` 주석) 같은 예외 처리가 필요하다. `Cauldron`의 이벤트 형식은 그대로 둬도 된다.

#### F-07 · Medium(유지보수) · 확인됨 — 다른 도메인이 `EscapeManager` 정적 조회를 직접 부른다 (5차 R4.2-2·R4.5 ⏸)
- **위치**: `Escape/EscapeManager.cs` 13(`Instance`), 53-56(`HasLeftMap`·`IsWaiting`), 72(`IsActive`), 75(`ActionsAllowed`). 호출: `Unit/HideOrSeekPlayer.cs:60·73`, `Unit/PlayerGrabController.cs:37`, `Monster/GameRuleController.cs:26·48·73`, `Monster/MonsterController`, `Monster/SpectatorController`, `Monster/ResultScreenController.cs:123`, `Monster/RoomLifecycleWatcher`, `Audio/EscapeAudioPresenter`·`MusicDirector`·`AmbientEmitter`·`CookieAudio`, `Camera/WitchRevealCamera`.
- **현재 구현·원인**: 맵 씬 하나에 하나뿐인 `EscapeManager`의 정적 프로퍼티로 탈출 상태를 묻는다. 탈출 모드가 없는 씬(PlayerTestScene)에서는 `Instance == null`로 false가 돌아오게 짜여 있다.
- **영향**: 탈출 상태의 의미("맵을 떠났다")를 바꾸면 4개 도메인을 함께 고쳐야 한다. "탈출했나" 판정이 여러 벌로 갈라진 원인이기도 하다(F-14).
- **수정 방향**: 새 패턴을 들이지 말고, 이미 있는 판정을 `EscapeState`의 메서드 하나(예: `HasLeftMap(actor)`)로 모은 뒤 정적 조회는 그것만 부르게 한다. 정적 조회 자체는 지금 규모에서 유지해도 된다.
- **수정 시 영향**: F-14와 같이 진행하면 회귀 범위가 줄어든다.

#### F-08 · Medium(유지보수) · 확인됨 — 캐릭터 구성이 프리팹이 아니라 런타임 `AddComponent`에 있다 (5차 R4.3 ⏸, 증가)
- **위치**: 쿠키 `Unit/HideOrSeekPlayer.cs:189·193·197·248-250`(`CookieAudio`·`CarriedBodyPresenter`·`StunReceiver`·`HeldItemPresenter`·`EscapeCharacterState`·`PlayerInventory`(+ `ToolUser`, `Escape/PlayerInventory.cs:35`)) · 괴물 `Monster/MonsterController.cs:132·134·135·151`(`StunReceiver`·`MonsterAudio`·`MonsterEscapeState`·`MonsterViewSwitcher`) · 카메라 `Escape/WitchPresenter.cs:69`(`WitchRevealCamera`를 `Camera.main`에), `Camera/MonsterViewSwitcher.cs:33` · HUD `Escape/EscapeHud.cs`(UI 전체를 코드로). 런타임 `AddComponent` **56곳 / 25개 파일**, `new GameObject(` 36곳.
- **영향**: 프리팹·씬을 열어도 실제 구성이 보이지 않아 인스펙터로 값을 조정할 수 없고, 구성 순서(`Awake`에서 붙이는 순서)에 숨은 의존이 생긴다.
- **수정 방향**: 지금 동작을 바꾸지 않는 범위에서, 캐릭터 프리팹에 컴포넌트를 미리 붙이고 `Init`만 코드로 남긴다(5차 판 권장 9와 같음). HUD는 그대로 둬도 된다(한 화면뿐).
- **수정 시 영향**: `HideOrSeekPlayer.Awake`의 순서 주석(동기화 협력 객체를 먼저 만드는 이유)을 지켜야 한다. 탈출 모드가 없는 씬에서는 탈출 컴포넌트를 비활성으로 둬야 한다.

#### F-09 · Medium · 추가 확인 필요 — 방장의 맵 씬 첫 프레임에 `GamePhaseStarter`가 색칠 종료 시각을 다시 쓸 수 있다 (5차 R4.7-7 ⏸)
- **위치**: `Monster/GamePhaseStarter.cs` `Update` 13-30 · `Lobby/GameStartAuthority.cs` 70-78(쓰기 직후 같은 프레임에 `LoadLevel`).
- **현재 구현**: 방장은 대기실에서 `PaintPhaseEndTime`을 쓰고 바로 씬을 옮긴다. 맵 씬의 `GamePhaseStarter`는 캐시에 키가 없으면 "대기실을 거치지 않은 경로"로 보고 새로 쓴다.
- **원인(추론)**: 쓰기 응답은 씬 로드 중(PUN이 메시지 큐를 멈춤)에 도착해 쌓였다가 로드 뒤 `PhotonHandler`의 디스패치에서 처리된다. 첫 `Update` 전에 디스패치가 한 번도 돌지 않으면 키가 없는 것으로 보인다.
- **영향(일어나면)**: 색칠 종료 시각이 "씬 로드 시각 + 색칠 시간"으로 늦춰져, 대기실에 남은 괴물(옛 값으로 카운트다운)과 맵의 쿠키가 다른 시각을 본다.
- **확인 방법**: 14장 E-1. 일어나지 않는다면 Info로 내린다.
- **수정 방향(일어나면)**: `GamePhaseStarter`가 몇 초 기다린 뒤에도 키가 없을 때만 쓰게 하거나, 대기실을 거치지 않는 시험 경로에서만 동작하게 한다.

### Low (10건)

| ID | 확신 | 위치 | 현재 구현 → 원인 → 영향 | 조건 | 수정 방향 / 영향 범위 |
|---|---|---|---|---|---|
| **F-10** | 확인됨 | `Escape/EscapeManager.cs:134`(`TrackPositions`는 방장만), `Escape/EscapeAuthority.cs:393-402·580·586` | 위치 기록이 방장일 때만 쌓인다 → 새 방장은 직전 방장(나간 사람)의 위치를 모른다 → 나간 방장의 아이템이 그 자리가 아니라 **장치 앞**에 떨어진다. 새 방장이 된 첫 프레임에는 거리 검사가 "모르면 통과"로 생략된다 | 방장이 아이템을 든 채 나갈 때 | 모든 클라이언트가 `TrackPositions`를 돌리거나(가벼움), 나간 사람의 캐릭터 마지막 위치를 동기화 값에서 읽는다 / `IsNear`·`IsNearDevice` |
| **F-11** | 확인됨(할당), GC 양은 확인 필요 | `Core/RoomState.cs:99-105·115-122`(`HostActor`·`DesiredMasterActor`가 반복문 안에서 `PlayerList`를 인원수만큼 다시 부름), `Lobby/GameLobbyController.cs:60`(매 프레임), `Escape/EscapeManager.cs:152`(모든 클라이언트 매 프레임), `Monster/GameRuleController.cs:50·108`(방장 매 프레임), `Escape/EscapeHud.cs:256`(탑승 중 매 프레임 + `string.Format`) | `PhotonNetwork.PlayerList`는 부를 때마다 `OrderBy().ToArray()`로 새 배열 → 대기실 8인이면 `HostActor` 한 번에 배열 9개 + 정렬 9번, 매 프레임 | 상시 | `HostActor`는 `PlayerList`를 한 번만 받아 쓰기, 매 프레임 루프는 `CurrentRoom.Players.Values` 순회(정렬 불필요) / 순서가 필요한 결과 화면은 그대로 |
| **F-12** | 확인됨(에디터·Play Mode) | `Assets/04. Prefabs/Scene/GameSceneCore.prefab` | ① `ResultScreen/MonsterWinBanner`·`CookieWinBanner`: `ResultScreenController.HideWinBanners`(188-192)가 **끄기만** 함(사용자 요청으로 제목 숨김) ② `EscMenu/Panel/Box/Buttons/SettingsButton`(꺼짐): `EscMenu.settingsButton`·`roomSettings`가 이 프리팹에서 비어 있어 게임 맵에서 절대 안 보임(방 설정은 대기실 씬의 별도 ESC 메뉴) ③ `GameRuleManagers`의 **PhotonView**: 같은 오브젝트 스크립트 7개 모두 RPC·`photonView`를 쓰지 않음 ④ 색칠 시간에 남은 시간이 위 `PaintCountdown`("변장 시간 00:43")과 색 패널 `ColorSlotPanel/TimeLabel`("43")에 **두 번** 표시됨 | 상시 | 프리팹에서 ①~③ 제거 + `ResultScreenController`의 배너 칸·`HideWinBanners`, `EscMenuBuilder`의 게임 맵용 설정 버튼 생성 정리. ④는 사용자 결정 / `GameSceneCore`를 쓰는 씬 6개(PhotonView 제거는 씬 ViewID 재배정 확인 필요) |
| **F-13** | 확인됨 | 9.2 표 | 참조 0회 멤버 33개(대부분 진단용 getter), 재생하지 않는 `SoundId` 3개(`CookieCarried`, `SpectateSwitch`, `MonsterWaitTick` — 클립도 없어 `AudioTests.AllIds_HaveClips`가 늘 "보류"), 읽지 않는 문구 5개(`EscapeTextsSO`) | 상시 | 9.2 참고. 정리 전 컴파일러·시험으로 재확인 |
| **F-14** | 확인됨 | 9.3 표 | "탈출했나" 판정 6벌, 파티클 생성 6곳, `FindDeep` 2벌, 승객 인형 생성 3벌 (5차 R4.11 ⏸) | 상시 | `EscapeState.HasLeftMap(actor)` 하나로, 파티클·승객은 정적 도우미 하나로 / F-07과 함께 |
| **F-15** | 확인됨 | `Escape/EscapeVisuals.cs:73-80`(`Tint`), 호출 `ToolFx.cs:22`(도구 쓸 때마다)·`EscapeExitFx.cs:26`·`StunReceiver.cs:58`·`EscapeDevice.cs:209`; `EscapeExitFx.Tick` 33-46(매 프레임 `Find` + `GetComponentsInChildren` + `r.material`); `SpyRocket.Update` 95-113(매 프레임 `$"Slot_{i:00}_Filled"` 문자열 + `transform.Find`) | `Tint`가 렌더러마다 새 머티리얼을 만들고 지우지 않음(씬 전환 때 Unity가 정리하므로 판 단위 누적). `Tick`의 `r.material`은 `Tint`가 넣은 재질을 한 번 더 복제 | 판 진행 중 | 머티리얼을 오브젝트 파괴 시 함께 지우거나 색별 캐시, `Tick`·`SpyRocket`은 찾은 Transform·렌더러를 캐시 |
| **F-16** | 확인됨 | `ColorTag/BrushCursorController`, `ColorTag/ColorSelectionPanel`, `GameManager/GameManager`, `Monster/GamePhaseStarter`, `Monster/MonsterController`, `Monster/MonsterJoinController`, `Monster/SpectatorController`, `Unit/HideOrSeekPlayer` | Photon 콜백을 하나도 재정의하지 않으면서 `MonoBehaviourPunCallbacks`를 상속 → 활성화마다 콜백 대상으로 등록되어 모든 방 콜백 순회에 들어간다(5차 R4.8의 8개 그대로) | 상시 | `MonoBehaviour`(PhotonView가 필요하면 `MonoBehaviourPun`)로. 단 `OnEnable/OnDisable` 재정의(`override` + `base`)도 같이 바꿔야 한다 |
| **F-17** | 확인됨 | `Lobby/GameLobbyController.cs:42` | 강퇴용 `PhotonNetwork.EnableCloseConnection = true`(전역)를 대기실에서 켜고 끄지 않는다 → 대기실을 한 번 거친 클라이언트는 게임 중에도 방장의 `CloseConnection`을 받아들인다 | 상시 | 대기실을 떠날 때(`OnDestroy`) false로 / `LobbyKick.CloseIfStillHere` |
| **F-18** | 가능성 높음 | `Escape/EscapeAuthority.cs` `Commit` 619-624, `EscapeManager.OnRoomPropertiesUpdate` 170-177 (5차 R4.10-10 ⏸) | 방장 화면은 커밋 즉시 새 상태를 그리는데, 연속 커밋이면 첫 커밋의 서버 응답이 나중에 와서 화면을 잠깐 옛 상태로 되돌린다(권위 상태 `authority.State`는 영향 없음 — 화면만) | 한 프레임에 두 번 커밋(드묾) | 응답의 버전 번호가 방장 화면 버전보다 낮으면 무시 / `EscapeState` 형식(버전 필드 추가 시 `FormatVersion` 올림) |
| **F-19** | 확인됨 | `Escape/EscapeAuthority.cs:255-257` | `Steal`이 칸을 비운 뒤 `if (!Give(...)) return false;` — `Give`는 두 분기 모두 `true`를 돌려 이 `return false`는 **도달 불가**. 만약 앞으로 `Give`가 false를 돌려주게 바뀌면 칸만 비고 아이템은 장치에 남는 불일치가 생긴다 | 현재 없음 | `Give`를 `void`로 바꾸거나 칸 비우기를 `Give` 뒤로 / `PickUp`(177행)도 `return Give(...)` |

### Info (6건)

| ID | 위치 | 내용 |
|---|---|---|
| **F-20** | `Monster/GameRuleController.cs:18·64·118-122`, `Escape/EscapeAuthority.cs:22·24` | 방장이 바뀌면 마녀 내리침 경과 시간(`witchStruckAt`, 방장 로컬 `Time.time`)이 새 방장 기준으로 다시 시작돼 결과가 최대 2.5초 늦을 수 있다. 도구 쿨다운(`toolReadyAt`)·훔치기 시작 기록(`stealStarts`)도 새 방장에게 없어 쿨다운이 풀리고 훔치기는 다시 눌러야 한다. 의도된 단순화로 보이며 결함은 아니다 |
| **F-21** | `Escape/EscapeManager.cs:89`, `Escape/Sequences/OvenSequence.cs:78`(`GameObject.Find(DoorName)`), `GameObject.Find` 5곳, `Camera.main` 20곳(5차 18) | 씬 이름 규칙(`Game_` 접두사)으로 레시피를 찾고, 맵 오브젝트 이름으로 연출 대상을 찾는다. 맵 이름·오브젝트 이름을 바꾸면 조용히 동작이 빠진다(`EscapeManager`는 오류 로그를 남김) |
| **F-22** | `Lobby/LobbyController.cs:73` | `SerializationRate`를 로비 씬에서만 설정 — 에디터에서 맵 씬을 바로 실행하면 기본값 10으로 돈다(빌드는 항상 로비부터라 영향 없음) |
| **F-23** | `Assets/Editor/Tests/*` | 시험은 EditMode 265개뿐이고 PlayMode·다중 클라이언트 시험이 없다. 네트워크 경합(F-01·F-03~F-05)은 자동 시험으로 잡히지 않는다 |
| **F-24** | 2.3 | 600줄 넘는 클래스 2개(`PlayerPaintCanvas`, `EscapeAuthority`). `EscapeAuthority`는 요청 처리·배치·출발·로켓·처단·알림을 모두 맡지만 순수 C#이고 상태 한 곳을 지키는 역할이라 나누는 이득이 크지 않다 |
| **F-25** | `Monster/MonsterGrabKillTrigger.cs:153-154` | 잡은 수를 캐시 값 + 1로 쓴다(읽고-더해-쓰기). 잡기 쿨다운(처형 애니메이션 끝까지)이 RTT보다 길어 실제로 겹치지 않는다 — 결함 아님, 쿨다운을 줄일 때 주의 |

---

## 6. 필수 조사 항목별 상세 분석

### A. 코드 사용 현황 및 중복
- **미사용 클래스**: 없음. 다른 파일에서 참조되지 않는 타입 25개는 모두 같은 파일 안에서 쓰이는 중첩 타입·인터페이스 상속·확장 메서드였다(`EscapeVectorExtensions.WithY`는 `EscapeAuthority.cs:92`에서 사용, `ICameraFollowTarget`은 `IGameCharacter`가 상속).
- **미사용 메서드·필드**: 33개(9.2). 속성 호출(`AudioRuntime.Boot`, `CharacterInteractor.Bootstrap` — `[RuntimeInitializeOnLoadMethod]`)과 인터페이스 구현(`PaintColliderUpdater.CookJob.Execute` — `IJob`)은 후보에서 뺐다.
- **도달 불가 코드**: F-19. **죽은 데이터**: 재생하지 않는 소리 ID 3개, 읽지 않는 문구 5개(F-13), 끄기만 하는 배너 2개(F-12).
- **중복**: F-14(9.3).
- **불필요한 Update**: 매 프레임 할당·탐색은 F-11·F-15. 방장 폴링 9개는 대부분 첫 줄에서 바로 빠지는 가벼운 검사다.

### B. 아키텍처 및 책임 분리
- **지켜진 책임 분리(양호)**: 방장 권위 쓰기는 `*Authority`·`GameRuleController`·`RoomLifecycleWatcher` 등 "방장 전용" 클래스에만 있다. UI는 요청만 보낸다(`GameLobbyController` → `GameStartAuthority`/`LobbyKick`/`RoomSettingsAuthority`). 5차 판에서 지적한 "UI가 방장 쓰기를 직접 함"(`RoomSettingsMenu`)은 `RoomSettingsAuthority`로 옮겨졌다.
- **남은 위험**: F-07(정적 조회 결합), F-08(런타임 구성), 채팅 클래스(`GameManager.Start`)가 `Time.timeScale`·`IsMessageQueueRunning` 같은 전역 설정을 바꾸는 것(5차 R4.1 ⏸, `GameManager/GameManager.cs:32-33`).
- **캡슐화**: MonoBehaviour 공개 가변 필드 0개, `EscapeState`는 `EscapeAuthority` 밖에서 수정 0곳, 전역 setter는 `PlayerInput.CutsceneActive`·`IsMenuOpen` 2개(켠 쪽이 `OnDisable`/`OnDestroy`에서 되돌림).
- **시험 가능성**: 규칙·시각표·배치·호스트 판정·강퇴 검증이 순수 함수라 EditMode로 시험된다. 네트워크 쪽은 PlayMode 시험이 없다(F-23).

### C. Unity 객체 및 리소스 관리
- **Prefab/Script 역할**: F-08. 씬 의존: F-21.
- **Singleton·전역 상태**: `Instance`/`Current`/`Local` 정적 참조 10개(`EscapeManager.Instance`, `EscMenu.Instance`, `WitchPresenter.Current`, `PlayerInventory.Local`, `PlayerPaintCanvas.Local`, `ToolUser.Aiming`, `EscapeHud.instance`, `AudioRuntime.instance`, `CharacterInteractor.instance`, `RegionSelector.Current`). 정적 가변 필드 43곳/24개 파일(5차 22곳). 영구 객체 2개(`AudioRuntime`, `CharacterInteractor`)는 중복 생성 방지·종료 처리가 있다.
- **ScriptableObject**: 런타임 상태 저장 없음. 쓰기 후보로 잡힌 `LobbyController.cs:135`는 `AppSettings` **사본**에 쓰는 것이라 에셋을 바꾸지 않는다(양호). 조회용 사전(`MusicCatalogSO.lookup` 등)은 지연 생성 캐시.
- **풀**: `GroundItemView`(바닥 아이템)만 풀을 쓰고, 재사용 시 위치·아이템·등록 상태를 모두 다시 설정한다(`Show`/`Hide`, 25-45). `Instantiate`/`Destroy`와 충돌 없음.
- **정리**: 칠하기 캔버스·스탬프 재질·콜라이더 쿠킹 버퍼는 `OnDestroy`에서 지운다(`PlayerPaintCanvas.cs:616-635`). 누수는 F-15만.
- **개발용 오브젝트**: F-02.

### D. Unity Lifecycle 및 실행 순서 — 8장

### E. Photon PUN2 — 7장

### F. 성능 및 안정성 — 10장. 예외 위험: 네트워크 입력 디코딩은 형식 검사 + `try`(`EscapeNet.TryParse`, `EscapeState.Decode`), 색 번호는 범위 밖이면 투명(`ColorPaletteSO.GetColor`), 인덱스 요청은 처리 함수마다 범위 검사. 새로 찾은 NRE·인덱스 오류 경로는 없다.

### G. 게임 규칙 및 상태 전이
- **단계 해석 한 곳**: `Core/GamePhaseState`(Room Prop 조합 → `Lobby/Paint/AwaitingMonster/Hunt/TimeAttack/Result`).
- **판정 한 곳**: 방장 `GameRuleController` + 순수 `EscapeRules.Evaluate`(출발 연출 중 보류, 처단은 방 시간 무시, 타임어택은 방 시간 정지, 끝나면 `resultRequested`로 한 번만 씀, 방장 교체 시 캐시로 재판정).
- **타이머**: `PaintPhaseEndTime`(시작 권한) → `GameEndTime`(괴물 합류) → `TimeAttackEndTime`(로켓) → `WitchAppearAt`·`WitchStrike`. 모두 `PhotonNetwork.Time` 기준 Room Prop이라 늦게 본 화면도 같은 시각을 쓴다.
- **사망·리스폰·씬 전환**: 사망 확정은 본인만(`HitCount`·`DeathCause`), 리스폰 없음(관전), 씬 전환은 방장 `RoomSceneTransition` 한 곳(캐시 삭제 후 `LoadLevel`).
- **멈춤 가능성**: 찾지 못했다. 결과 화면 자동 복귀는 모든 클라이언트가 카운트다운하고 방장만 실행하므로 방장이 바뀌어도 이어진다(`ResultScreenController.cs:167-184`). 괴물 전원 이탈은 F-04 조건에서만 복귀가 빠진다.
- **중복 판정**: "탈출했나"가 여러 곳에서 각자 계산된다(F-14) — 지금은 같은 식이라 결과가 같지만 한 곳만 바뀌면 결과 화면·징글·판정이 어긋난다.

---

## 7. Photon PUN2 네트워크 구조 분석

### 7.1 통신 수단 목록
| 수단 | 개수 | 내용 |
|---|---|---|
| `[PunRPC]` | 4 | `GameManager.LogMsg`(채팅·접속 기록, `RpcTarget.All`), `HideOrSeekPlayer.OnGrabbedByOwner`·`OnReleased`(대상 주인에게), `RequestGrabKill`(`All`, 발신자 = 괴물 주인 검증) |
| `RaiseEvent` | 12곳 / 코드 13종(`NetEventCodes`) | 방장에게 요청: `StartGameRequest`, `RoomSettingsRequest`, `KickRequest`, `ClaimMonster`, `EscapeRequest`, `DoorStateRequest` · 방장 → 모두/대상: `EscapeNotice`, `ToolHit`, `Kicked` · 소유자 → 다른 사람: `PaintStroke`, `ClearColor` |
| Room Props | 방장 쓰기 11개 파일 | 판 상태 전부(`NetKeys` 범위표: Room/Player × Round/Session) |
| Player Props | 본인 쓰기 8곳 | `HitCount`·`DeathCause`·`Escaped`·`CatchCount`·`SelectedSlot`·`SkinIndex`·색칠 칸 수 등 — 모두 `LocalPlayer`에만, 원격 복사본에서 실행되는 경로는 `IsMine` 확인 후(`MonsterEscapeState.cs:28`, `EscapeCharacterState.cs:39·82`) |
| `PhotonNetwork.Instantiate` | 2(+Dev 1) | 쿠키(`PlayerSpawner.cs:69`), 괴물(`MonsterJoinController.cs:80`) — 둘 다 Resources 프리팹, 소유자 본인 생성 |
| `PhotonNetwork.Destroy` | 0 | 퇴장 시 PUN 자동 정리, 씬 전환 시 `OpRemoveCompleteCache` + 로컬 파괴(`RoomSceneTransition`) — 의도된 설계(주석에 이유) |

### 7.2 PhotonView·동기화
| 프리팹 | 동기화 | 소유권 | 관찰 대상 | 판정 |
|---|---|---|---|---|
| `HideOrSeekPlayer` | UnreliableOnChange | Fixed | `HideOrSeekPlayer` | 쓰기(위치·회전·상태 + 들기 여부·들고 있는 ViewID) ↔ 읽기 순서 대칭 확인(`PlayerNetworkSync`) |
| `MonsterPlayer` | UnreliableOnChange | Fixed | `MonsterController` | 위치·회전·상태 대칭 확인 |
| `GameSceneCore/GameManager` | (관찰 없음) | Fixed | — | 채팅 RPC용, 사용 중 |
| `GameSceneCore/GameRuleManagers` | (관찰 없음) | Fixed | — | **사용하지 않음**(F-12) |
- 소유권 이전(`TransferOwnership`)을 쓰지 않는다. 들린 쿠키도 자기 PhotonView를 유지하고 모든 화면이 손 위에 놓는다 — 소유권 경합이 없는 단순한 설계.
- 원격 보간은 고정 비율 Lerp + 10 m 넘으면 즉시 이동(`NetworkTransformSync.Interpolate`).

### 7.3 권한(방장/클라이언트) 정리
- **방장만**: 판 시작·괴물 확정·방장 정책·색칠 종료 처리·괴물 합류 시각·탈출 상태 전체·판정·괴물 이탈 처리·씬 이동·문 상태·강퇴 실행.
- **소유자만**: 자기 캐릭터 물리·사망 확정·탈출 표시·잡은 수·선택 칸·색칠(획 전송).
- **모두(같은 계산)**: 단계 해석, 연출(공통 시계), 결과 화면, 소리.
- **방장 정책**: "괴물이 아닌 사람 중 가장 먼저 들어온 사람"(`MasterClientPolicy` + `RoomState.DesiredMasterActor`), 시작 버튼 주인(호스트)은 괴물이어도 그대로 — 둘을 분리한 설계는 일관적이다.

### 7.4 발신자 검증 현황
| 경로 | 검증 | 근거 |
|---|---|---|
| `EscapeRequest` | ✅ 발신자 = `e.Sender` | `EscapeManager.cs:251` |
| `EscapeNotice`·`ToolHit` | ✅ 방장 발신만 | `EscapeManager.SentByMaster` |
| `StartGameRequest` | ✅ 요청자 = 호스트 | `GameStartAuthority.cs:49` |
| `RoomSettingsRequest` | ✅ 요청자 = 호스트 | `RoomSettingsAuthority.cs:38` |
| `KickRequest`·`Kicked` | ✅ 요청자 = 호스트 / 방장 발신만 | `LobbyKick.Handle`·`IsForMe` |
| `RequestGrabKill` RPC | ✅ 발신자 = 괴물 주인 | `HideOrSeekPlayer.cs:140-146` |
| `OnGrabbedByOwner`·`OnReleased` RPC | ❌ | F-01 |
| `PaintStroke`·`ClearColor` | ❌ | F-06 ① |
| `ClaimMonster` | ❌(내용의 번호 사용) | F-06 ② |
| `DoorStateRequest` | △(발신자 무관, 거리·단계 미검사) | F-06 ③ |

### 7.5 "응답 전 캐시" 경합 정리
PUN 온라인 방은 `SetCustomProperties` 응답이 올 때까지 로컬 캐시를 바꾸지 않는다(소스 확인). 그래서 방장 코드 대부분이 로컬 플래그로 응답 전 중복을 막는다: `GameRuleController.resultRequested/witchRequested`, `MonsterJoinController.joinRequested`, `PaintPhaseController.resolveRequested`, `MonsterAssignmentAuthority.confirmRequested/resetRequested/deadlineClearRequested`, `EscapeManager.initRequested`, `EscapeAuthority.purgeRequested`, `GameStartAuthority.lastStartTime`, `InteractableDoor.masterPendingWrites`, `MasterClientPolicy.requestedActor`.
- 빠진 곳: **F-05**(`TryLaunchRocket`).
- 캐시로 새 값을 계산하는 곳: **F-04**(`RoomLifecycleWatcher`, `MonsterAssignmentAuthority.OnPlayerLeftRoom`).
- 플래그가 정당한 요청까지 막는 곳: **F-03**.

### 7.6 방장 교체·퇴장·재접속
- 교체: 새 방장이 `EscapeAuthority.Adopt` → `Reevaluate`(출발·처단·로켓 재확인, 알림 재전송 없음), 판정 플래그 초기화 후 캐시로 재판정, 괴물 이탈 복귀 시각은 Room Prop에서 이어받음, 문 대기 쓰기 초기화.
- 퇴장: 나간 사람 아이템을 마지막 위치에 떨어뜨림(F-10 예외), 출발·로켓 재평가, 괴물 목록 정리(F-04).
- 재접속: 판 진행 중에는 방이 닫혀(`IsOpen = false`) 다시 들어올 수 없다. 대기실에서는 재입장 허용(강퇴 후에도, §19 Q6). 판 중간 재접속 복구 경로는 없다(설계상 범위 밖).

---

## 8. Unity Lifecycle 및 이벤트 흐름 분석

### 8.1 초기화 순서 의존
- `HideOrSeekPlayer.Awake`는 동기화가 `Start`보다 먼저 올 수 있음을 알고 협력 객체를 `Awake`에서 만든다(주석 근거 있음, 양호).
- `EscapeManager`: `Awake`(카탈로그·레시피·상자 바인딩·권위 객체) → `Start`(HUD·소리·Room 상태 읽기) → `Update`(방장 초기화). 캐릭터 쪽은 `HideOrSeekPlayer.AttachEscapeMode`가 `EscapeManager.Instance`를 찾는다 — 둘 다 같은 씬에서 `Awake`가 끝난 뒤 `Start` 단계라 순서 문제는 확인되지 않았다(캐릭터는 네트워크 생성이라 씬 오브젝트보다 늦게 생긴다).
- `[RuntimeInitializeOnLoadMethod]`: `AudioRuntime.Boot`(BeforeSceneLoad), `CharacterInteractor.Bootstrap`(AfterSceneLoad) — 씬과 무관한 영구 객체.
- 첫 프레임 경합 후보: F-09.

### 8.2 PUN 콜백 등록
- `MonoBehaviourPunCallbacks`를 상속하고 `OnEnable`/`OnDisable`을 재정의한 11곳은 모두 `public override` + `base` 호출 → 콜백 등록이 유지된다(`BrushCursorController:22`, `PlayerPaintCanvas:609`, `InteractableDoor:84·90`, `HeldItemPresenter:40·46`, `GameManager:86`, `MonsterController:116·122`, `HideOrSeekPlayer:267·273`).
- `IOnEventCallback` 구현 5개(`GameLobbyController`, `MonsterAssignmentAuthority`, `EscapeManager`, `InteractableDoor`, `PlayerPaintCanvas`)는 `MonoBehaviourPunCallbacks`의 자동 등록을 쓴다.
- 콜백을 안 쓰는 상속 8개: F-16.

### 8.3 이벤트 구독·해제
| 이벤트 | 종류 | 구독 | 해제 | 판정 |
|---|---|---|---|---|
| `AudioVolumeSettings.Changed` | static | `AudioRuntime.Build` | `OnDestroy` | ✅ |
| `OutputDevices.Changed` | static | `SoundSettingsPanel:61` | `OnDestroy:64` | ✅ |
| `SpectatorController.SpectateTargetChanged`·`SpectatingVehicleChanged` | static | `SpectatorLabel:25-26` | `:31-32` | ✅ |
| `WitchPresenter.Impacted` | static | `MonsterEscapeState:18`, `EscapeCharacterState:85` | 실행 시 + `OnDestroy` | ✅ |
| `SceneManager.sceneLoaded` | static | `AudioListenerAnchor:36` | `:42` | ✅ |
| `EscapeManager.StateChanged` | 인스턴스 | `EscapeCharacterState:23`, `EscapeHud`, 장치 등 | 캐릭터는 `OnDestroy:30`, HUD는 해제 없음 | ✅(HUD와 매니저가 같은 씬에서 함께 파괴) |
| `PlayerInventory.Changed` | 인스턴스 | `EscapeHud:213` | `:211` | ✅ |
| `SoundSettingsPanel.Closed`·`RoomSettingsMenu.Closed` | 인스턴스 | `EscMenu.Awake` | 없음 | ✅(같은 씬) |
- 중복 구독: `MonsterEscapeState`·`EscapeCharacterState`는 `handled`/`witchHandled` 플래그로 한 번만.

### 8.4 코루틴·Invoke·전역 상태 복구
- `StartCoroutine` 10곳: 퇴장·씬 전환으로 객체가 파괴되면 함께 멈춘다. 장시간 코루틴은 결과 자동 복귀(`StopAllCoroutines` 후 실행)와 강퇴 3초 대기(방장이 바뀌면 `CloseConnection`이 실패만 함)뿐.
- `Invoke(nameof(DieByWitch))` 2곳: `CancelInvoke`와 짝.
- 전역 입력 잠금 3종: 채팅(`GameManager.OnDisable/OnDestroy`), ESC 메뉴(`EscMenu.OnDestroy`), 컷신(`WitchRevealCamera.OnDisable`)이 되돌린다 → 씬 전환 뒤 입력이 잠긴 채 남지 않는다.
- 정적 플래그: `OfflineModeBootstrap.SpawnAsMonster`·`OfflineMode`는 `OnDestroy`에서 복구, `PaintPhaseController.IsPaintScene`은 `Awake`/`OnDestroy` 짝, `InteractableDoor.masterPendingWrites`는 방장 교체·판 초기화 때 비움, `PhotonNetwork.EnableCloseConnection`은 복구 안 함(F-17).

---

## 9. 중복 코드와 미사용 코드 분석

### 9.1 방법
선언 이름이 런타임·에디터·시험 코드(주석 제외) 전체와 씬·프리팹의 UnityEvent 메서드 이름에서 **선언 1회 외에 한 번도 나오지 않는** 것만 후보로 남겼다. 속성으로 호출되는 메서드, 인터페이스 구현, Unity 메시지, `override`, PUN 콜백, 직렬화 필드(인스펙터 데이터)는 따로 판단했다. **정리 전에는 컴파일러로 다시 확인해야 한다**(같은 이름이 다른 의미로 쓰였을 수 있음).

### 9.2 미사용 후보(F-13)
| 구분 | 위치 | 비고 |
|---|---|---|
| 진단·시험용으로 보이는 getter | `Audio/AudioRuntime.cs:58·60·61`(`Director`, `BusyWorldVoices`, `BusyFlatVoices`), `:217·220·221`(`SpatialOf`, `WallOf`, `CutoffOf`), `Audio/AudioVoicePool.cs:27·32`(`Capacity`, `AudibilityOf`), `Audio/ChaseDirector.cs:37`(`LayerSource`), `Audio/IndoorZone.cs:25·42`(`ActiveCount`, `LocalBoxes`), `Audio/MusicPlayer.cs:35`(`IsStingerPlaying`), `Audio/SoundSettingsPanel.cs:40·42`(`RowCount`, `OutputOptionCount`), `Audio/UiSound.cs:16·17`(`Click`, `Hover`), `Camera/MonsterFirstPersonCamera.cs:86`(`KillLookBlend`), `Environment/FlickerLight.cs:28`(`BaseIntensity`), `Escape/EscapeDevice.cs:115`(`IsComplete`), `Escape/MaterialChest.cs:61`(`IsAppearing`), `Escape/PlayerInventory.cs:26`(`HeldItemIndex`), `Monster/MonsterGrabKillTrigger.cs:32`(`IsOnCooldown`), `Monster/SpectatorController.cs:47`(`IsSpectating`) | 예전 시험·디버그에서 쓰던 흔적. 지워도 동작 변화 없음 |
| 미사용 메서드 | `Audio/GameAudio.cs:79`(`StopMusic`), `Audio/SoundSettingsPanel.cs:104`(`PickOutput`), `Lobby/RoomSettingField.cs:71`(`StepBy`), `Escape/Data/ItemSO.cs:66`(`EditorSetVisuals`) | 공개 API로 남겨 둔 것일 수 있음 — 사용자 결정 |
| 읽지 않는 데이터 | `Escape/Data/EscapeTextsSO.cs:18·19·28·30·41`(`inventoryFull`, `handsFull`, `stolenToRocketWithFinal`, `stolenToRocketNoFinal`, `boardingLocked`), `ColorTag/ColorPaletteSO.cs:18`(`colorName` — 인스펙터 이름표일 수 있음) | SO 에셋에 한글 문구가 들어 있다면 지울 때 에셋도 확인 |
| 재생하지 않는 소리 ID | `Audio/SoundId.cs:31·40·55`(`CookieCarried`, `SpectateSwitch`, `MonsterWaitTick`) | 클립도 없어 `AudioTests.AllIds_HaveClips`가 늘 "보류" |
| 쓰지 않는 UI·컴포넌트 | F-12 ①~③ | |
| 오탐으로 제외 | `AudioRuntime.Boot`, `CharacterInteractor.Bootstrap`(`[RuntimeInitializeOnLoadMethod]`), `PaintColliderUpdater.CookJob.Execute`(`IJob`) | |

### 9.3 중복(F-14)
| 중복 | 위치 | 위험 |
|---|---|---|
| "탈출했나/맵을 떠났나" 6벌 | `Escape/EscapeManager.cs:53-56`(`HasLeftMap`·`IsWaiting`), `Escape/EscapeAuthority.cs:462`(`IsActiveEscaper`), `Monster/GameRuleController.cs:53`, `Monster/ResultScreenController.cs:132`, `Audio/EscapeAudioPresenter.cs:77-78`, `Audio/MusicDirector`(`HasLeftMap`) | 판정·결과 화면·징글이 서로 다른 기준이 될 수 있음(지금은 같음) |
| 연기·불꽃 파티클 생성 6곳 | `Escape/MaterialChest.cs:107`, `Escape/SpyRocket.cs:163`, `Escape/Sequences/CoasterSequence.cs:200`, `OvenSequence.cs:161·195`, `TrainSequence.cs:174` | 재질·수명 설정이 조금씩 다름 |
| 깊은 자식 찾기 2벌 | `Escape/WitchPresenter.cs:211`, `Escape/EscapeSequence.cs:115` | |
| 승객 인형 생성 3벌 | `Escape/BoardingFx.cs:29-30`, `CoasterSequence.cs:137-141`, `TrainSequence.cs:144-148` | |

---

## 10. 성능 및 메모리 관리 분석

### 10.1 매 프레임 비용(주요)
| 위치 | 내용 | 빈도 | 판정 |
|---|---|---|---|
| `Lobby/GameLobbyController.Update`(55-) → `RoomState.HostActor` | `PlayerList` (인원+1)번 = 배열·정렬 (인원+1)벌 | 대기실, 모든 클라이언트 | F-11 |
| `Escape/EscapeManager.TrackBrokenPlayers`(150-157) | `PlayerList` 1벌 | 맵, 모든 클라이언트 | F-11 |
| `Monster/GameRuleController.UpdateEscape`(46-88) | `PlayerList` 1벌 + 목록 재사용 | 맵, 방장 | F-11 |
| `Escape/EscapeHud.RefreshBoarding`(244-262) | `PlayerList` + `string.Format` | 탑승 중 | F-11 |
| `Escape/EscapeExitFx.Tick`(33-46) | `Find` + `GetComponentsInChildren`(배열) + `r.material` | 장치 완성 뒤 | F-15 |
| `Escape/SpyRocket.Update`(95-103) | 보간 문자열 2개 + `transform.Find` × 2칸 | 맵 상시 | F-15 |
| `Core/GamePhaseState.Current` | Room Prop 4번 조회(문자열 키) | 여러 클래스가 매 프레임 | Info — 할당 없음 |
- 할당 없는 쪽(양호): 물리 질의는 `NonAlloc`(`MonsterGrabKillTrigger`, `PlayerGrabController`), 칠하기 스탬프 목록 재사용, 결과 판정 목록 재사용, 콜라이더 쿠킹은 워커 스레드 Job(`PaintColliderUpdater`).
- 할당이 있지만 1회성: `PaintPhaseController.ResolvePaintPhase`(LINQ — 판당 한 번), 결과 화면(`OrderBy` — 한 번).

### 10.2 네트워크 전송량
- 캐릭터 동기화: 초당 `SerializationRate`(기본 10)번, 쿠키 5필드·괴물 3필드, `UnreliableOnChange`.
- 칠하기: 스탬프를 묶어 보내고 받는 사람 수에 따라 간격을 늘림(`GameSettingsSO.PaintStrokeSendIntervalFor`, 146-152).
- 탈출 상태: 바뀔 때만 byte[] 전체(아이템·상자·칸 수에 비례, 수백 바이트 수준) — 방장 커밋 빈도는 요청 수와 같다.
- 판단: 과도한 전송은 찾지 못했다.

### 10.3 메모리·GC
- 누수: F-15(머티리얼, 판 단위). 칠하기 캔버스·재질·쿠킹 버퍼는 정리됨. 영구 객체 2개는 의도된 것.
- GC: 프레임당 할당은 F-11·F-15의 몇 개 배열·문자열 수준. 실제 양은 Profiler 확인 필요(14장 E-6).

---

## 11. 기존 설계의 장점과 유지해야 할 구조

1. **방장 권위 + 상태 한 곳**: 탈출 상태 전체를 `EscapeState`(byte[] Room Prop 하나)로 두고 `EscapeAuthority`만 바꾼다. 늦게 들어온 사람도 Room Prop 하나로 같은 화면을 만든다. 외부 수정 0곳 — **유지**.
2. **요청자 = `EventData.Sender`**: 요청 내용의 번호를 믿지 않는다. 같은 원칙을 F-06 경로에도 넓히면 된다.
3. **공통 시계 연출**: 맵 연출·로켓·마녀·카메라가 Room Prop의 시각과 `PhotonNetwork.Time`의 차이로만 계산해 추가 메시지 없이 모든 화면이 같은 장면을 본다 — **유지**.
4. **순수 함수 규칙**: `EscapeRules`, `WitchTimeline`, `RecipePlanner`, `RoomState.HostActor(int[])`, `LobbyKick.CanKick`, `MusicRules`, `GamePhaseState.Evaluate` — EditMode 시험이 덮는다. 새 규칙도 이 방식으로.
5. **응답 전 중복 쓰기 방지 패턴**(7.5): 이미 9곳에서 쓰고 이유가 주석에 있다. 빠진 곳만 맞추면 된다.
6. **씬 전환 한 곳**: `RoomSceneTransition`(이벤트 캐시 삭제 이유까지 주석) — 유지.
7. **방장 정책과 호스트 분리**: 진행 권한(괴물 아님)과 시작 버튼 주인을 나눈 설계 — 유지.
8. **입력 한 곳**: `PlayerInput`이 채팅·메뉴·컷신 잠금을 한데 모아 캐릭터 종류와 무관하게 멈춘다.
9. **데이터는 SO**: 아이템·도구·레시피·문구·소리·음악·설정이 SO이고 한글 문구는 에셋에(코드에 한글 없음 규칙 준수).
10. **소유권 이전을 쓰지 않는 들기**: 경합이 적은 단순한 선택이다. F-01은 이 설계를 바꾸지 않고 검사만 더하면 된다.

---

## 12. 우선순위별 개선 권장 사항

| 순위 | 항목 | 근거 | 난이도 | 비고 |
|---|---|---|---|---|
| 1 | 들기 검사: 이미 들린·들고 있는 쿠키 제외 + `OnGrabbedByOwner` 무시 조건 + `OnReleased` 발신자 확인 | F-01 | 하 | 회귀 시험: 3인 들기 시나리오 |
| 2 | 출시 씬 6개의 개발용 부트스트랩 제거 → 시험 전부 통과 상태 회복 | F-02 | 하 | 이후 실패는 모두 "새 실패" |
| 3 | 이벤트 발신자 검증 3종(칠하기·괴물 지원·문) | F-06 | 하 | 오프라인 예외 처리 |
| 4 | 괴물 목록 계산을 "실제 방 인원 ∩ 목록"으로(게임·대기실 두 곳) | F-04 | 하 | |
| 5 | 로켓 발사 로컬 가드 | F-05 | 하 | |
| 6 | 가마솥 지원 대기열 또는 재전송 | F-03 | 중 | 8인 온라인 시험 필요 |
| 7 | `GamePhaseStarter` 첫 프레임 확인(14장 E-1) 후 필요하면 수정 | F-09 | 하 | |
| 8 | `PlayerList` 반복 할당 정리, `Tick`·`SpyRocket` 캐시 | F-11·F-15 | 하 | |
| 9 | 미사용 정리(GameSceneCore 4개, 멤버 33개, ID 3개) | F-12·F-13 | 하 | 사용자 결정 항목 포함(변장 시간 두 번 표시) |
| 10 | "탈출했나" 판정 하나로 모으기 + 파티클·승객 도우미 | F-14·F-07 | 중 | |
| 11 | 캐릭터 프리팹에 컴포넌트 미리 붙이기 | F-08 | 중 | |
| 12 | `EnableCloseConnection` 복구, 콜백 없는 PUN 상속 정리 | F-17·F-16 | 하 | |

---

## 13. 수정 시 주의해야 할 의존 관계와 회귀 위험

- **RPC·이벤트 형식 변경**(F-01·F-06): 서명·내용이 바뀌면 이전 빌드와 같은 방에서 섞일 수 없다. `GameVersion`(`LobbyController`)을 올려 다른 빌드끼리 같은 방에 들어오지 않게 한다.
- **`EscapeState` 형식 변경**(F-18 버전 필드 등): `FormatVersion`을 올리지 않으면 디코드가 어긋난다(지금은 버전이 다르면 null로 안전하게 무시).
- **괴물 목록 쓰기**(F-03·F-04): `MasterClientPolicy`가 목록 변경에 반응해 방장을 옮긴다. 목록을 자주 쓰면 방장이 흔들릴 수 있으니 한 번에 확정된 값만 쓴다.
- **`MonoBehaviourPunCallbacks` → `MonoBehaviour` 변경**(F-16): `OnEnable`/`OnDisable`의 `override` 키워드가 컴파일 오류가 되므로 함께 고친다. `photonView` 속성을 쓰는 클래스는 `MonoBehaviourPun`으로.
- **프리팹 구성 변경**(F-08·F-12): `GameSceneCore`를 쓰는 씬 6개, PhotonView 제거 시 씬 ViewID 재배정, `EscMenuBuilder`가 다시 만들 때 설정 버튼을 되살리지 않게.
- **중복 판정 통합**(F-14): 결과 화면·징글·판정·소리·관전이 같은 판정을 쓰게 되므로, 통합 직후 탈출·로켓·처단 세 경로를 Play Mode로 다시 확인.
- **시험**: `Request1011Tests`·`EscapeTests`·`RuleTests`·`AudioTests`가 규칙을 덮는다. F-02를 먼저 고쳐 전체가 초록인 상태에서 시작하면 회귀가 바로 드러난다.
- **변경 파일 점검(이번 작업)**: 감사 시작 전 `Assets`·`ProjectSettings`·`Packages`·`Plan.md`의 파일 4,564개 크기·수정 시각을 저장했고, 보고서를 쓴 뒤 다시 비교했다 — 결과는 이 문서를 올린 뒤 대화 보고에 적는다(바뀐 파일은 `Plan.md/research.md` 하나여야 한다).

---

## 14. 추가로 Unity Editor에서 검증해야 할 항목

| # | 확인 | 절차 | 판단 |
|---|---|---|---|
| E-1 | F-09 첫 프레임 경합 | `GamePhaseStarter.Update`에 임시 로그(키가 없어 쓸 때) → 빌드 2개 + 에디터로 온라인 판 10회 시작 | 로그가 한 번도 안 나오면 F-09를 Info로 |
| E-2 | F-01 들기 꼬임 | 3인 온라인: A가 C를 든 상태에서 B가 C 잡기 → A 내려놓기 | C가 B 손에서 빠지면 재현 |
| E-3 | F-03 가마솥 | 7~8인 온라인, 두 사람이 동시에 가마솥 진입 | 둘 다 괴물이 되는지 |
| E-4 | F-04 괴물 동시 퇴장 | 2괴물 판에서 두 괴물 클라이언트를 동시에 종료 | 5초 뒤 대기실 복귀가 일어나는지 |
| E-5 | F-05 로켓 중복 | 마지막 스파이 탑승과 같은 순간 쿠키 잡힘(네트워크 지연 시뮬레이션: `PhotonNetwork.NetworkingClient.LoadBalancingPeer.IsSimulationEnabled`) | `SpyEscapedAt`이 두 번 바뀌는지 |
| E-6 | F-11 GC | Profiler(Deep Profile 끔)로 대기실 8인·맵 진행 중 프레임당 GC Alloc | 0.5 KB/프레임 이하면 Low 유지 |
| E-7 | F-02 수정 후 | 전체 EditMode 시험 | 실패 0 |
| E-8 | F-12 ③ PhotonView 제거 | 제거 후 맵 씬 열어 PhotonView ViewID 경고 확인, 채팅 RPC 동작 | |
| E-9 | F-06 | 시험용으로 다른 클라이언트에서 `PaintStroke`(남의 ViewID)·`ClaimMonster`(남의 번호) 직접 전송 | 수정 전 재현, 수정 후 무시 |

---

## 15. 조사하지 못했거나 결론을 내릴 수 없는 항목

| 항목 | 이유 | 필요한 것 |
|---|---|---|
| 실제 다중 클라이언트 동작(F-01·F-03~F-05·F-09) | 이 환경은 오프라인 1인 Play Mode만 가능 | 빌드 2개 이상 + 에디터(14장) |
| 조작된 클라이언트 공격 재현(F-06) | 수정된 클라이언트가 필요 | 시험용 빌드 |
| 실제 GC 양·프레임 비용(F-11·F-15) | Profiler 기록 없음 | 14장 E-6 |
| 에디터 스크립트 44개의 줄 단위 품질 | 범위상 런타임 영향만 확인 | 별도 감사 |
| Photon 대시보드·서버 설정(지역·CCU·웹훅) | 프로젝트 밖 | 대시보드 접근 |
| 맵 씬의 오브젝트 단위 설정(콜라이더·조명·LOD) | 이번 범위 밖(스크립트 배치만) | 맵별 점검 |
| 모바일·다른 플랫폼 빌드 | Windows 빌드만 확인됨(오늘 `Builds/R1011Check` 성공, 오류 0) | 대상 플랫폼 빌드 |
| 오래 켜 둔 세션(여러 판 반복)의 누적 | 장시간 실행 기록 없음 | 10판 연속 Play + 메모리 스냅숏 |

---

# 부록 Z. 5차 판(2026-10-02) 원문 보존

> 아래는 5차 판 전체를 수정 없이 옮긴 것이다. 코드 주석의 `research.md R…`·`§…` 참조는 여기서 찾는다. 현재 상태는 6차 판 4.3 표를 우선한다.


# 조사 보고서: TagOfChaos 런타임 스크립트 전수 분석 + 아키텍처 11개 관점 감사 (2026-10-02 5차)

> **개정 안내 (5차)**: 4차 판(2026-09-28)을 **갱신**한다. 4차 판 원문은 `Plan.md/research.prev.md`로 옮겨 두었다(3차 판 백업은 덮어썼다).
> 코드 주석이 이 문서의 절 번호를 참조하므로(`research.md R4.10-2` — `Unit/HideOrSeekPlayer.cs:115`, `research.md R5-1` — `GameManager/GameManager.cs:26`)
> **R 번호 체계(R1~R7, 부록 A~D)와 기존 항목 번호를 그대로 유지**했고, 새 절 **R8(효과음·배경음 필요 지점 조사)**을 더했다.
> 상태 표기: **✅ 해결됨** / **🔁 부분 해결** / **⏸ 유지(미해결)** / **📈 악화** / **🆕 신규**. 새 항목은 같은 절 안에서 다음 번호를 받는다.
>
> **4차 판 이후 들어온 큰 변경**
> - **탈출 모드 전체**(`Escape/` 38개 파일, 4,812줄): 상자·인벤토리·탈출 장치·스파이 로켓·도구(뿅망치·스턴건·물풍선)·마녀 타임어택·맵별 탈출 연출 5종·3D 유리병 결과 화면.
>   방장 권위(`EscapeAuthority`)가 요청을 검증하고 상태 전체를 Room Prop 하나(`EscapeState`, byte[])로 쓴다.
> - 방 설정(인원·제한시간·타임어택 시간), ESC 메뉴(`EscMenu`), 대기실 방 설정 창(`RoomSettingsMenu`), 로비 지역 고정(kr).
> - 스파이 역할(`SpyActorNumbers`), 인원표(`GameSettingsSO.roleTable`, 4~8명), 괴물 1인칭(`MonsterFirstPersonCamera`·`MonsterViewSwitcher`·`GrabAimReticle`).
> - 맵 5개 모두 탈출 모드 씬이 됐다(`EscapeManager`가 모든 맵 씬에 있음).
>
> **조사 방법**
> - Unity MCP(우회 스크립트)로 에디터에 접속해 `Assets/02. Scripts/` 아래 **11개 폴더, 127개 `.cs`, 12,889줄을 모두 읽었다**(4차 79개·6,614줄 → +48개·+6,275줄).
>   에디터 스크립트 20개(7,247줄)는 구조와 런타임에 영향을 주는 부분만 봤다(줄 단위 감사는 4차 판 R7 이후 다시 하지 않았다).
> - 빌드 씬 8개의 YAML에서 **스크립트 GUID → 컴포넌트 수**, 프리팹 인스턴스, `AudioSource`·`AudioListener` 수를 집계했다(R2.4).
> - `GameSceneCore.prefab`·캐릭터 프리팹 2개를 `PrefabUtility.LoadPrefabContents`/`LoadAssetAtPath`로 열어 실제 구성을 확인했다.
> - `GameSettings.asset`, `GraphicsSettings.asset`(항상 포함 셰이더), `ProjectSettings.asset`(`activeInputHandler`), TMP 기본 글꼴(한글 대체 글꼴), PUN 소스(`PhotonNetwork.OfflineMode` setter)를 확인했다.
> - 4차 판의 모든 지적 항목을 현재 코드와 다시 대조했다. **이번 판도 정적 분석이 중심이다** — Play Mode로 재현하지 않은 항목은 “코드 추론”이라고 적었다.
> - 파일 경로는 따로 적지 않으면 `Assets/02. Scripts/` 기준, 줄 번호는 2026-10-02 작업 트리 기준이다. 예: `Unit/HideOrSeekPlayer.cs:53`.

---

## 목차

- **R1. 요약** — 결론, 11개 관점 판정표, 결함 목록, 우선 조치 Top 12, 4차 판 대비 변화
- **R2. 프로젝트 구조** — 엔진·설정·폴더·씬·프리팹·SO·배선표
- **R3. 동작 방식 상세** — 한 판의 흐름(탈출 모드), 네트워크 계약, 도메인별 동작
- **R4. 11개 관점 감사** — 관점마다 판정, 근거(file:line), 영향, 권장
- **R5. 그 밖의 발견 사항**
- **R6. 권장 로드맵**
- **R7. 참조하지 않는 변수·메서드**
- **R8. 효과음·배경음 필요 지점 조사** — 현재 오디오 자산·구성, 지점 목록(file:line), 설계 제약 → 상세 계획은 `Plan.md/SoundPlan.md`
- **부록** — 교차 도메인 의존성, 전역(static) 상태, 전역 설정을 바꾸는 곳, 마스터 전용 Update 폴링

---

## R1. 요약

### R1.1 결론

**탈출 모드는 설계가 좋다.** 방장 한 명이 요청을 검증하고(`EscapeAuthority`, 요청자는 `EventData.Sender`로 판정), 상태 전체를 Room Prop 하나로 쓰며,
맵 연출 5종은 `CompletedAt`·`DepartedAt`과 공통 시계(`PhotonNetwork.Time`)의 차이로만 계산해 **네트워크 메시지를 더 보내지 않고도 모든 화면이 같은 장면**을 본다.
늦게 들어온 사람도 바로 맞는 장면을 본다. 판정 규칙(`EscapeRules`)·칸 배치(`RecipePlanner`)·인벤토리 다음 칸(`PlayerInventory.NextOccupied`)은 순수 함수라 테스트되고,
아이템·도구·레시피·문구는 모두 SO 데이터다. 바닥 아이템은 풀(`GroundItemView`)로 재사용한다.

그 대가로 **구조 지표가 나빠졌다.**

1. **싱글톤이 다시 생겼고 다른 도메인이 그것을 직접 부른다(R4.5, R4.2).** 4차 판의 “`Instance` 싱글톤 0개”가 **2개**(`EscapeManager.Instance`, `EscMenu.Instance`)가 됐다.
   정적 “현재 로컬” 참조도 4개(`PlayerInventory.Local`, `PlayerPaintCanvas.Local`, `ToolUser.Aiming`, `EscapeHud.instance`)다.
   `EscapeManager`의 정적 조회(`IsWaiting`·`HasLeftMap`·`IsActive`·`Instance.State`)를 Unit·Monster·Camera 도메인의 **8개 파일 14곳**이 직접 부른다(부록 A).
2. **프리팹이 실제 구성을 보여 주지 않는다(R4.3).** 쿠키·괴물 프리팹에는 탈출 모드 컴포넌트가 없고, `Awake`에서 `AddComponent`로 6종을 붙인다.
   HUD·조준점·유리병 결과·파티클·마녀 임시 모델은 모두 코드로 만든다(약 900줄).
3. **같은 계산이 여러 벌이다(R4.11).** “탈출했나” 판정 4벌, 로켓 칸 맞춤 2벌, 깊은 자식 찾기 2벌, 연기 파티클 생성 4벌, 승객 인형 생성 3벌, 사망 확정 2벌 등 11건이 새로 생겼다.

4차 판의 남은 결함(그랩 수락 절차, `GamePhaseStarter` 첫 프레임 경합, RaiseEvent 발신자 미검증, 회피 상태 동결)은 **모두 그대로이고**, 회피 동결은 기절이 생기면서 더 자주 일어나게 됐다(📈 R5-14).

### R1.2 11개 관점 판정표

| # | 관점 | 판정 | 한 줄 요약 | 4차 판 대비 |
|---|---|---|---|---|
| 1 | 기존 책임 분리를 무시하는 코드 | ⚠ 일부 있음 | ⏸ 채팅이 전역 설정, UI 패널이 시작 요청 처리. 🆕 ESC 메뉴가 `PlayerInput`을 우회해 키를 직접 읽음. 🆕 방장 쓰기를 UI(`RoomSettingsMenu`)가 직접 함 | 2건 신규 |
| 2 | Manager 간 의존성 과도 증가 | ⚠ **악화** | 🆕 `EscapeManager` 정적 조회를 다른 도메인 8개 파일이 직접 부름. 마스터 폴링 8 → **9개**. 결과 화면·승패 판정이 탈출 모드를 직접 앎 | 📈 |
| 3 | Prefab과 Script 역할 뒤섞임 | ⚠ **악화** | 🆕 런타임 `AddComponent`로 캐릭터 구성(6종). 🆕 HUD·조준점·결과 유리병·파티클을 코드로 생성. 🆕 맵 씬 5개에 테스트용 `__OfflineBoot`(비활성)가 남음 | 📈 |
| 4 | Scene에 직접 의존하는 코드 증가 | ⚠ 증가 | `GameObject.Find` 4 → **5곳**, `Camera.main` 10 → **18회**. 🆕 씬 이름 규칙(`Game_` 접두사)으로 레시피를 찾음. 🆕 연출이 맵 오브젝트 이름(`HAU_MagicOven_Door`)을 찾아 부모를 바꿈 | 📈 |
| 5 | Singleton 남발 | ⚠ **주의** | `Instance` 0 → **2개**. 정적 “로컬” 참조 4개, 정적 인스턴스 2개. 가변 정적 상태 14 → **22곳**(부록 B) | 📈 |
| 6 | ScriptableObject 책임 오용 | ✅ 양호 | 새 SO 5종(카탈로그·레시피·아이템·도구·문구) 모두 읽기 전용 데이터. ✅ `GameSettings.asset` 재저장됨. ⏸ 연출 수치·거리 상수는 여전히 코드에 | 1건 해결 |
| 7 | Unity Lifecycle 순서 문제 | ⚠ 일부 있음 | 🆕 **마녀 내리치기 카메라 흔들림이 같은 프레임 `LateUpdate`에 덮여 보이지 않음**(R4.7-9). 🆕 기절한 원격 쿠키 보간이 멈춤. ⏸ 4차 판 항목 그대로 | 2건 신규 |
| 8 | Event 구독/해제 | ✅ 대체로 양호 | 새 C# 이벤트 4종(`StateChanged`·`Changed`·`Closed`·`RegionSelector.Changed`) 대부분 짝 맞음. 🆕 `EscapeHud`가 인벤토리 `Changed` 해제를 빠뜨림(낮음). ⏸ 콜백을 쓰지 않는 `MonoBehaviourPunCallbacks` 상속 4 → 8개 | 1건 신규(낮음) |
| 9 | Object Pool ↔ Instantiate/Destroy 충돌 | ✅ 충돌 없음 | 🆕 바닥 아이템 풀·기절 별 미리 생성은 적절. ⚠ 🆕 `EscapeVisuals.Tint`가 렌더러마다 머티리얼을 새로 만들고 지우지 않음(판 단위 누수) | 1건 신규 |
| 10 | Photon Ownership/RPC 구조 무시 | ⚠ 일부 있음 | ✅ 탈출 요청은 `EventData.Sender`로 검증. 🆕 **`ToolHit`·`EscapeNotice` 이벤트 발신자 미검증 + 도구 명중 대상을 쓰는 사람이 정함**. 🆕 **방장 교체 뒤 “스파이 잡힘” 알림 중복**. ⏸ 그랩 수락 절차 없음 | 3건 신규 |
| 11 | 중복 로직 | ⚠ **다수(악화)** | 🆕 11건(탈출 판정 4벌, 연기 파티클 4벌, 승객 인형 3벌, 높이 제한 상수 불일치 등). ⏸ 4차 판 항목 대부분 그대로 | 📈 |

### R1.3 결함 목록(현재 코드 기준)

| # | 결함 | 상태 | 절 |
|---|---|---|---|
| 1 | 마녀가 내리칠 때의 카메라 흔들림이 보이지 않는다 — `WitchPresenter.Update`가 카메라를 흔든 뒤 같은 프레임 `Camera_Ctrl`·`MonsterFirstPersonCamera`의 `LateUpdate`가 위치를 덮어쓴다 | 🆕 코드 추론(확실) | R4.7-9 |
| 2 | 방장이 바뀌면 이미 잡힌 스파이에 대해 “스파이가 잡혔다” 알림이 모두에게 다시 뜬다 | 🆕 코드 추론 | R4.10-9 |
| 3 | 아무 클라이언트나 `ToolHit` 이벤트를 직접 보내 모두를 기절시키거나 칠할 수 있고, 정상 경로에서도 명중 대상·지점을 쓰는 사람이 정해 방장이 거리를 검사하지 않는다 | 🆕 | R4.10-8 |
| 4 | 회피 도중 **기절**·들림이 오면 회피 타이머가 멈췄다가, 풀린 뒤 옛 방향으로 2배 속도로 밀려난다(기절이 생겨 발생 빈도 증가) | 📈 코드 추론 | R5-14 |
| 5 | 색칠 시간에 ESC 메뉴를 열면 메뉴 버튼을 누르는 클릭이 뒤의 몸에 칠해지고, 마우스가 몸 위에 있으면 OS 커서가 숨겨진다 | 🆕 코드 추론 | R5-20 |
| 6 | 기절한 쿠키의 원격 복사본이 기절 동안 보간을 멈춰 다른 화면에서 제자리에 얼어 있다가 풀리면 순간이동한다 | 🆕 코드 추론 | R4.7-10 |
| 7 | 쿠키 그랩에 수락 절차·발신자 확인이 없어 두 명이 같은 쿠키를 잡으면 캐리 상태가 꼬인다(연쇄 캐리 가능) | ⏸ | R4.10-1 |
| 8 | 방장의 게임 씬 첫 프레임에 `GamePhaseStarter`가 `PaintPhaseEndTime`을 다시 쓸 수 있다 | ⏸ 코드 추론 | R4.7-7 |
| 9 | PlayerTestScene(탈출 모드 없음)에서 에디터 실행 중 쿠키가 잡히면 `GetComponent<PlayerInventory>()?.`가 가짜 null을 통과해 예외가 날 수 있다 | 🆕 코드 추론 | R4.7-3 |
| 10 | 방장의 탈출 상태 커밋이 연달아 두 번 일어나면, 늦게 도착한 첫 번째 서버 응답이 화면을 잠깐 옛 상태로 되돌린다 | 🆕 코드 추론(낮음) | R4.10-10 |

### R1.4 우선 조치 Top 12

| 순위 | 항목 | 근거 | 난이도 |
|---|---|---|---|
| 1 | 카메라 흔들림을 카메라 쪽 `LateUpdate` 뒤로 옮기기(`CameraShake` 오프셋을 `Camera_Ctrl`·1인칭 카메라가 더함) | R4.7-9 | 하 |
| 2 | `ToolHit`·`EscapeNotice` 수신 시 `e.Sender == MasterClient.ActorNumber` 확인, 방장이 도구 대상의 거리·사거리를 다시 검사 | R4.10-8 | 하~중 |
| 3 | 잡힘 알림을 “스파이 잡힘 처리됨” 집합으로 `EscapeState`에 기록하거나, 새 방장은 Adopt 시점에 이미 잡힌 사람을 `brokenReported`에 미리 채우기 | R4.10-9 | 하 |
| 4 | 기절·들림·파괴가 시작될 때 `DodgeOut()`, 회피 속도는 `baseSpeed * 2`로 계산. 기절 순간 수평 속도 0 | R5-14, R5-19 | 하 |
| 5 | 색칠 입력을 `IsMenuOpen`·`IsPointerOverGameObject`로 막고, 메뉴가 열려 있으면 붓 커서가 OS 커서를 숨기지 않게 | R5-20 | 하 |
| 6 | 원격 쿠키는 기절 중에도 보간(`IsMovementLocked`를 “로컬 조작 잠금”과 “표시 잠금”으로 분리) | R4.7-10, R4.7-6 | 하 |
| 7 | 그랩 수락 절차 + `OnGrabbedByOwner`·`OnReleased` 발신자 검증(4차 판 1순위 유지) | R4.10-1 | 중 |
| 8 | `EscapeManager` 정적 조회를 `IEscapeQuery`(Core 계약) 하나로 묶어 다른 도메인이 구현 클래스를 모르게 | R4.2-2, R4.5 | 중 |
| 9 | 쿠키·괴물 프리팹에 탈출 모드 컴포넌트를 미리 붙이고(비활성 기본), `AddComponent` 대신 `Init`만 | R4.3-13 | 중 |
| 10 | ✅ 맵 씬 5개의 `__OfflineBoot` 제거 + 맵 씬에 Dev 컴포넌트가 없는지 EditMode 테스트 | R4.4-9 | 하 |
| 11 | 중복 정리: `EscapeState.HasLeftMap(actor)` 하나, `RocketRules.FindSlot` 하나, `ParticleFactory`, `PassengerModel.Create` | R4.11-21~31 | 하 |
| 12 | ✅ 런타임 셰이더 키워드(Standard Fade·Emission)를 머티리얼 에셋으로 바꾸거나 셰이더 변형 모음에 넣기 — 빌드 검증 | R5-21 | 중 |

### R1.5 4차 판 대비 변화

| 4차 판 항목 | 현재 상태 | 근거 |
|---|---|---|
| R5-16 `GameSettings.asset` 직렬화가 코드보다 오래됨 | ✅ 해결 | 에셋에 `tentacleDashMaxSlope`·`tentacleDashGroundSnap`이 저장돼 있고 옛 `tentacleDashRadius`가 없다(인원표 등 새 필드도 모두 저장) |
| R4.1-9 입력 억제가 단일 bool | 🔁 부분 | 채팅(`chatSuppressed`)과 ESC 메뉴(`IsMenuOpen`)가 별도 값이라 서로 덮어쓰지 않는다(`Core/PlayerInput.cs:31-39`). 원인이 셋 이상이 되면 같은 문제 |
| R4.10-1 그랩 수락 절차 | ⏸(보강 2건) | 물건을 든 손으로는 못 잡고(`Unit/PlayerGrabController.cs:47`), 드는 쪽이 탈것에 타면 내려놓는다(`:39`). 수락·발신자 검증은 여전히 없음 |
| R5-14 회피 동결 | 📈 | 기절(`StunReceiver`)도 `IsMovementLocked`에 들어가(`Unit/HideOrSeekPlayer.cs:52`) 발생 경로가 늘었다 |
| R4.7-3 `GetComponent<T>()?.` | 📈 2 → 5곳 | `HideOrSeekPlayer.cs:136·152·238`, `CookieLifeStatePresenter.cs:176`, `EscapeCharacterState.cs:46` — 그중 `PlayerInventory`는 탈출 모드가 아닌 씬에서 실제로 없다 |
| R4.4-1·2 씬 탐색·`Camera.main` | 📈 | 4 → 5곳, 10 → 18회 |
| R4.5 Singleton | 📈 | `Instance` 0 → 2개 |
| R4.1-7 `MonsterController` 비대화 | 📈 404 → 430줄 | 기절·1인칭 회전·탈출 상태 부착이 더해졌다 |
| R4.1-2·3·4·5·6·8, R4.3-1~8·11·12, R4.4-3·4·8, R4.7-1·2·4·6·7·8, R4.10-3·4·5·7, R4.11-1~5·7~14·17~20, R5-4·5·8·11·13·15 | ⏸ 유지 | 각 절에서 현재 줄 번호로 다시 적었다 |

---

## R2. 프로젝트 구조

### R2.1 엔진·패키지·설정

- Unity **6000.0.58f2**, Photon **PUN 2**, 내장 렌더 파이프라인(`m_CustomRenderPipeline: 0`), Post Processing 3.5.4, ProBuilder, uGUI, TextMesh Pro.
- 입력: 게임 코드는 레거시 Input Manager(`Core/PlayerInput.cs`), Input System 패키지도 설치돼 `activeInputHandler: 2`(Both).
  UI 입력 모듈은 LobbyScene·GameLobbyScene = `StandaloneInputModule`, GameSceneCore = `InputSystemUIInputModule`(⏸ R5-13, 이번에 다시 확인).
- 그래픽: **항상 포함 셰이더 6개(내장 기본값)** — `Standard`는 목록에 없다(씬 머티리얼이 써서 빌드에 들어가지만, 런타임에 켜는 키워드 변형은 빠질 수 있다 — R5-21).
- TMP 기본 글꼴: `LiberationSans SDF` + 동적 대체 글꼴(`LiberationSans SDF - Fallback`, Dynamic). 한글 글자는 대체 글꼴이 실행 중 채운다(확인: `HasCharacter('스', searchFallbacks)` = true).
- 테스트: `RuleTests` 16 + `EscapeTests` 37 케이스 + `MapCompactTests` 20 케이스(마지막 실행 모두 통과).
- **오디오: 오디오 클립 0개, AudioMixer 0개, 씬·프리팹의 `AudioSource` 0개, 씬마다 `AudioListener` 1개(Main Camera)** — 소리가 전혀 없다(R8).

### R2.2 폴더와 줄 수

| 폴더 | 파일 | 줄 | 역할 | 4차 대비 |
|---|---|---|---|---|
| Core | 20 | 1,161 | 공용 허브(조회·키·단계·입력·설정·씬 전환·레지스트리·스폰·UI 헬퍼·지면 판정) | +185(탈출 키·단계·메뉴 입력) |
| Unit | 9 | 890 | 쿠키 캐릭터, 스킨 | +43 |
| Monster | 19 | 2,002 | 괴물 캐릭터 + 판 진행(선정·합류·승패·결과·관전·방장 정책) | +260 |
| ColorTag | 11 | 1,352 | 색칠 + 쿠키 생존·처형 연출 | +11 |
| **Escape** 🆕 | **38** | **4,812** | 탈출 모드(상태·권위·장치·로켓·도구·HUD·연출·데이터 SO) | 신규 |
| GameManager | 8 | 559 | 채팅, 스폰, 퇴장, 낙하 복귀, 확인창, **ESC 메뉴·방 설정 창** | +172 |
| Lobby | 9 | 980 | 로비, 대기실 패널, 시작 권한, 스킨, **방 설정 칸·지역** | +417 |
| Environment | 3 | 398 | 문, 가마솥 연출(풍덩·발광) | +46 |
| Interaction | 4 | 253 | 상호작용 탐지·안내 아이콘·사물 계약·이름 계약 | +55 |
| Camera | 4 | 399 | 3인칭 궤도, **괴물 1인칭·시점 전환·조준점** | +261 |
| Dev | 2 | 83 | 오프라인 부트스트랩(스파이로 시작 옵션), 괴물 테스트 스포너 | +13 |
| **합계** | **127** | **12,889** | | +48개, +6,275줄 |

250줄이 넘는 파일: `ColorTag/PlayerPaintCanvas.cs` 628, **`Escape/EscapeAuthority.cs` 524**, `Unit/HideOrSeekPlayer.cs` 453, `Monster/MonsterController.cs` 430,
**`Escape/EscapeHud.cs` 356**, `Lobby/LobbyController.cs` 315, `Environment/InteractableDoor.cs` 284, `ColorTag/CookieLifeStatePresenter.cs` 274, **`Escape/EscapeManager.cs` 265**.

### R2.3 에셋

- **전역 SO**: `Resources/GameSettings`(정원 4~8 기본 4, 인원표 4:괴물1·스파이0 / 5·6:1·1 / 7·8:2·1, 선정 30초, 색칠 60초+여유 3초, 제한시간 10~40분 기본 10, 타임어택 1~10분 기본 1,
  돌진 20 m/0.25초/15초/45°/0.6 m, 잡기 2 m·0.4 m, 괴물 기본 1인칭, 동기화 10 Hz, 스탬프 15 Hz, 맵 5개), `Resources/InputBindings`.
- **탈출 모드 SO** 🆕: `Resources/Escape/EscapeCatalog`(아이템·레시피 목록, 뿅망치, 스파이 상자 도구, 승객·유리병 모델), `EscapeTexts`(모든 화면 문구, 한글),
  레시피 5개(맵별), 아이템 SO, 도구 SO 3개(`Tool_WaterBalloon` 사거리 10, `Tool_StunGun` 사거리 15), `Resources/Escape/SoftParticle.mat`(빌드에서 셰이더가 빠지지 않게 만든 파티클 재질).
- **네트워크 프리팹**: `HideOrSeekPlayer`(PhotonView, HideOrSeekPlayer, PlayerPaintCanvas, PlayerSkinApplier, PlayerGrabController, CookieLifeStatePresenter, SpectatorController, FallGuard, 이름표),
  `MonsterPlayer`(PhotonView, MonsterController, MonsterGrabKillTrigger, FallGuard). **탈출 모드 컴포넌트는 프리팹에 없다**(R4.3-13).
- **씬 공용 프리팹**: `GameSceneCore` — 4차 구성 + **Canvas/EscMenu**.
- **맵 탈출 프리팹** 🆕: `CHEST`(×20/맵), `ESC_<맵>`(장치 + 연출), `SPY_Rocket_<맵>`, `WITCH`.
- ~~남은 참조 없는 UI 프리팹: `Resources/UI/Scene/ColorSelectionPanel/ColorSelectionPanel.prefab`~~ ✅ 삭제(R5-15, 2단계).

### R2.4 씬 배선(스크립트 → 컴포넌트 수, YAML 집계)

| 씬 | 씬에 직접 직렬화된 게임 스크립트 | 프리팹 인스턴스 | 오디오 |
|---|---|---|---|
| LobbyScene | LobbyController, RoomSettingField ×3 | LobbyPanel | Listener 1 |
| GameLobbyScene | Camera_Ctrl, Cauldron, ConfirmDialog, **EscMenu**, GameManager, MasterClientPolicy, MonsterAssignmentAuthority, MonsterLobbyWaitController, MonsterRevealController, PlayerSkinSelector, PlayerSpawner, RoomExitController, **RoomSettingField ×3, RoomSettingsMenu**, RoundStateResetter, VoidKillZone | GameLobbyPanel, Cauldron, ConfirmDialog, Witch_Cookie_Environment, 등불·나무 소품 | Listener 1 |
| Game_CandyForest | **CakeRocketSequence**, Camera_Ctrl, **EscapeDevice, EscapeManager, MaterialChest ×20, OfflineModeBootstrap(비활성)**, **SpyRocket**, VoidKillZone, **WitchPresenter** | CHEST ×20, ESC_CandyForest, GameSceneCore, SPY_Rocket_CandyForest, WITCH | Listener 1 |
| Game_GingerbreadVillage | 위와 같은 구성 + **RuneAltarSequence** | … | Listener 1 |
| Game_ChocolateFactory | 위 + **TrainSequence**, InteractableDoor ×16 | … | Listener 1 |
| Game_CursedCandyCarnival | 위 + **CoasterSequence**, InteractableDoor ×3 | … | Listener 1 |
| Game_HauntedBakery | 위 + **OvenSequence**, InteractableDoor ×15 | … | Listener 1 |
| PlayerTestScene(비활성) | Camera_Ctrl, MonsterTestSpawner, OfflineModeBootstrap, VoidKillZone | GameSceneCore, HideOrSeekPlayer(활성), MonsterPlayer(비활성) — ⏸ R4.3-11 | Listener 1 |
| (런타임 생성) | CharacterInteractor(DDOL) + InteractionPromptUI, **EscapeHud, 조준점·결과 유리병 캔버스, 연출 파티클·조명** | | |

문 수가 4차 판(26·3·22)과 달라졌다(16·3·15). 맵 재빌드·축소로 바뀐 것으로 보이며 기능상 문제는 없다.

**GameSceneCore.prefab 구성(확인)**: GameManager(GameManager·PlayerSpawner·RoomExitController) / GameRuleManagers(GamePhaseStarter·MonsterJoinController·GameRuleController·
RoomLifecycleWatcher·PaintPhaseController·MasterClientPolicy) / PaintManagers(BrushCursorController) / Canvas(ConfirmDialog, ColorSlotPanel + 스와치 10 + 도구 버튼 2,
MonsterDepartureBanner, PaintCountdown·SurvivalTimer, SpectatorLabel, ResultScreen, **EscMenu**) / EventSystem.

---

## R3. 동작 방식 상세

### R3.1 한 판의 흐름(탈출 모드)

```
LobbyScene
  LobbyController.Awake: AutomaticallySyncScene=true, GameVersion=1
  Start: 지역 = 기본(kr) 고정(지역 UI 제거됨) → ConnectUsingSettings(FixedRegion) → 로비 → 방 목록
  방 만들기: MaxPlayers(4~8), Room Props { RoomTimeLimit(초), TimeAttackDuration(초) } (Session 수명)
  OnJoinedRoom: 첫 1인만 LoadLevel(GameLobbyScene)
        │
GameLobbyScene (대기실) — 4차 판과 같음 + ESC 메뉴(방장은 "설정" → RoomSettingsMenu: 인원·제한시간·타임어택 변경)
  정원이 차면 괴물 선정(가마솥 선착순 / 30초 뒤 무작위) — 괴물 수는 인원표
  호스트가 시작 → 방장 GameStartAuthority.TryStart
      Room Props { PaintPhaseEndTime, GameMapScene, SpyActorNumbers(인원표 수, 괴물 제외 무작위) } 한 번에 기록 → IsOpen=false → LoadLevel(map)
  괴물: 큐 정지 후 대기실에서 카운트다운(4차와 같음). 대기 중 나가기를 누르면 큐를 되살려 바로 나간다(RoomExitController.cs:46)
        │
Game_<맵> (맵 환경 + GameSceneCore + EscapeSetup)
  EscapeManager.Awake: Instance 설정, 씬 이름에서 "Game_"을 뗀 이름으로 레시피 선택, 상자 자리마다 MaterialChest 연결, EscapeAuthority 생성
  EscapeManager.Start: EscapeHud 생성(코드로 만든 캔버스), Room의 EscapeState 읽기
  쿠키 스폰 → HideOrSeekPlayer.Awake가 StunReceiver·HeldItemPresenter·EscapeCharacterState(전원)·PlayerInventory(+ToolUser, 본인) 부착
  방장 EscapeManager.Update: PaintPhaseEndTime·괴물이 있으면 Initialize
      필요 재료 수 = 쿠키 수(인원 - 괴물 - 스파이), 칸 배치(RecipePlanner), 상자(쿠키×3 + 스파이×2), 재료·뿅망치·스파이 공구상자·스파이 도구 배치 → Commit
  색칠 60초: 상자·줍기·도구·설치·로켓·탈출 모두 막힘(EscapeManager.ActionsAllowed), 인벤토리 바는 흐리게
  색칠 종료 → 강제 도포 / 괴물 합류(GameEndTime = 합류 + 방 제한시간)
  생존·탈출 시간:
    쿠키: 상자 열기(E) → 재료 받음 → 장치 칸에 설치(E). 숫자키·휠로 칸 선택, G로 떨어뜨림, 좌클릭으로 도구
    스파이: 스파이 상자(스턴건·물풍선), 장치에서 재료 훔치기(로켓에 필요한 것만), 로켓에 끼우기 → 2칸이 차면 탑승
    도구: 쓰는 사람이 대상·지점을 정해 요청 → 방장이 횟수·쿨다운 확인 → ToolHit(전원) → 맞은 본인이 기절·아이템 하나 떨어뜨림·(물풍선) 색칠
    장치 완성 → CompletedAt 기록 → 맵 연출(시동·문 열림 등) → BoardReadySeconds 뒤 탑승 가능
    탑승: Waiting에 추가(몸 숨김, 관전 카메라가 탈것을 봄) → 밖에 남은 쿠키가 없으면 DepartedAt(출발) → 탄 쿠키 전원 탈출 성공
    스파이 전원 탑승 → SpyEscapedAt(+남은 쿠키가 있으면 TimeAttackEndTime) → 로켓 발사 연출, 마녀가 5초에 걸쳐 나타나 돌아섬
    타임어택 0 → WitchStrike → 남은 쿠키·괴물 사망(DeathCause=Witch) → 2.5초 뒤 결과
  GameRuleController(방장): EscapeRules.Evaluate — 모두 해결 / 제한시간 끝 / 마녀 → GameResult=EscapeEnded + EscapeEndReason + RevealedSpies
  ResultScreenController.PresentEscape: 사람마다 “닉네임(역할) 결과”, 괴물이 잡기만 했으면 3D 쿠키 유리병(MonsterJarStage), 12초 뒤 대기실
```

### R3.2 네트워크 계약(4차 판에 더해진 것)

**Room Props**(모두 방장만 쓴다)

| 키 | 타입 | 수명 | 쓰는 곳 | 읽는 곳 |
|---|---|---|---|---|
| RoomTimeLimit / TimeAttackDuration | int(초) | Session | LobbyController(방 만들 때), RoomSettingsMenu(대기실 방장) | RoomState.TimeLimitSeconds·TimeAttackSeconds |
| SpyActorNumbers | int[] | Round | GameStartAuthority(시작), OfflineModeBootstrap(개발) | RoomState.IsSpy — **모든 클라이언트가 읽을 수 있다**(R4.10-11) |
| EscapeState | byte[] | Round | EscapeAuthority.Commit | EscapeManager(전원) |
| SpyEscapedAt / TimeAttackEndTime | double | Round | EscapeAuthority.TryLaunchRocket | 단계(TimeAttack), 로켓·마녀 연출, HUD, 카운트다운 |
| WitchStrike | int | Round | GameRuleController | 캐릭터 사망 처리, 마녀 연출 |
| EscapeEndReason / RevealedSpies | int / int[] | Round | GameRuleController.FinishEscape | 결과 화면 |

**Player Props**(본인만 쓴다): `SelectedSlot`(인벤토리 칸), `Escaped`(탈출 성공), `CatchCount`(괴물이 잡은 수 — 괴물 본인이 씀), `DeathCause`(괴물·마녀).

**RaiseEvent** 추가

| 코드 | 이벤트 | 대상 | 발신자 검증 |
|---|---|---|---|
| 7 | EscapeRequest(상자·줍기·떨어뜨리기·설치·훔치기·로켓·탈출·도구) | 방장 | ✅ `EventData.Sender`로 요청자 결정(`Escape/EscapeManager.cs:229`) |
| 8 | EscapeNotice(스파이 잡힘·로켓에 끼움·장치 완성) | 전원 | ❌ 방장인지 확인 안 함(R4.10-8) |
| 9 | ToolHit(도구 명중) | 전원 | ❌ 방장인지 확인 안 함, 대상은 쓰는 사람이 정함(R4.10-8) |

### R3.3 도메인별 동작 요약(새 도메인)

**Escape** — `EscapeManager`(씬 단위 조정자: 상태 사본·화면 갱신·요청 송신·이벤트 수신) ↔ `EscapeAuthority`(방장 전용 순수 C#: 검증·변경·커밋) ↔ `EscapeState`(직렬화 가능한 상태, 버전 2).
화면 쪽은 `MaterialChest`·`GroundItemView`(풀)·`EscapeDevice`·`SpyRocket`·`HeldItemPresenter`·`EscapeHud`가 `StateChanged` 또는 `Refresh`로 상태를 그린다.
연출은 `EscapeSequence` 파생 5종이 `Tick(state, now)`에서 시간 차이로 계산한다. 마녀(`WitchPresenter`)·로켓 발사(`SpyRocket.AnimateLaunch`)도 같은 방식이다.
도구는 `ToolUser`(쓰는 쪽 로컬) → 방장 → `ToolHitReceiver`(전원) → `StunReceiver`·`PlayerInventory`·`PlayerPaintCanvas`(맞은 본인).

**Camera** — `MonsterViewSwitcher`(괴물 본인에게만 `AddComponent`)가 1인칭(`MonsterFirstPersonCamera`)과 3인칭(`Camera_Ctrl`)을 켜고 끄며 조준점(`GrabAimReticle`)을 만든다.
1인칭은 처형 중 잡힌 쿠키 쪽으로 시점을 돌렸다 돌아온다. ESC 메뉴·결과 화면에서는 커서를 푼다.

**GameManager** — `EscMenu`(정적 `Instance`, ESC 키 직접 읽음, `PlayerInput.IsMenuOpen` 설정), `RoomSettingsMenu`(방장이 방 설정 변경).

---

## R4. 11개 관점 감사

### R4.1 기존 책임 분리를 무시하는 코드 — ⚠ 일부 있음

**R4.1-1 ✅(4차 판에서 해결, 유지)** 채팅 입력 억제는 `PlayerInput`.

**R4.1-2 ⏸ 채팅 매니저가 전역 설정을 바꾼다.** `GameManager/GameManager.cs:32-33`(`Time.timeScale`, `IsMessageQueueRunning`). 같은 큐 재개가 `PlayerSpawner.cs:65`, `RoomExitController.cs:46·64`에도 있다(부록 C).

**R4.1-3 ⏸ UI 패널이 네트워크 권한 처리를 맡는다.** `Lobby/GameLobbyController.cs:98-104`(시작 요청 수신). 🆕 같은 성격으로 **`GameManager/RoomSettingsMenu.cs:48-69`**(UI 창)이
`room.MaxPlayers`와 Room Props를 직접 쓴다. 쓰기 자체는 방장만 하므로 소유권 원칙은 지키지만, “방 설정을 바꾸는 규칙”(인원 하한 = 현재 인원, 분 → 초 변환)이 UI 클래스에 있다.
로비의 방 만들기(`LobbyController.cs:243-255`)와 분 → 초 변환이 2벌이다(R4.11-30).
- 권장: `RoomSettingsAuthority.Apply(players, limitMinutes, attackMinutes)`(정적, Lobby 도메인) 하나가 검증·변환·쓰기를 하고 두 UI가 부른다.

**R4.1-4 ⏸(📈) 캐릭터 조정자가 다른 도메인을 직접 호출한다.** 4차 판 목록에 더해:
- `Unit/HideOrSeekPlayer.cs:53, 66`이 `EscapeManager.IsWaiting`, `:136`이 `PlayerInventory`, `:170-172, 217-224`가 탈출 모드 컴포넌트 4종을 직접 붙인다.
- `Monster/MonsterController.cs:132-134`가 `StunReceiver`·`MonsterEscapeState`를 붙인다.
- `Interaction/CharacterInteractor.cs:45, 64`가 `MonsterController`를 타입으로 확인한다(4차 판에서는 “Core만 참조” — 🆕 회귀).
- `Camera/MonsterFirstPersonCamera.cs:92`가 `EscMenu.IsOpen`(GameManager 도메인)을 읽는다. 같은 정보가 `PlayerInput.IsMenuOpen`에 이미 있다.
- 권장: 커서 해제는 `PlayerInput.IsMenuOpen`만 보게 하고, 괴물 잡기 우선 입력은 `IGrabber`(Core 계약 — `bool HasAimTarget; bool TryGrabAimTarget()`)로.

**R4.1-5 ⏸** 퇴장 컨트롤러가 채팅 RPC를 빌려 쓴다(`RoomExitController.cs:50`).

**R4.1-6 ⏸ 폴더와 실제 책임이 어긋난다.** 4차 판 내용 그대로. 🆕 `Escape/` 폴더는 응집도가 높다(좋음). 다만 `EscMenu`·`RoomSettingsMenu`(UI 창)가 채팅 폴더(`GameManager/`)에,
`MonsterJarStage`·`MonsterJarTrophy`(결과 화면 표시)가 `Escape/`에 있다.

**R4.1-7 📈 `MonsterController` 430줄.** 4차 판 권장(돌진 모터·처형 연출 값 분리) 그대로.

**R4.1-8 ⏸ 처형 연출 데이터가 괴물 컨트롤러에 있고 쿠키 표시 컴포넌트가 직접 읽는다.** `Monster/MonsterController.cs:31-39, 72-78`, `ColorTag/CookieLifeStatePresenter.cs:53, 109-130`.
🆕 `Camera/MonsterFirstPersonCamera.cs:52`도 `IsGrabKilling`·`GrabSocket`을 읽는다.

**R4.1-9 🔁 입력 억제 원인이 둘이 됐다.** 채팅·메뉴가 별도 값이라 지금은 덮어쓰지 않는다(`Core/PlayerInput.cs:31-39`). 하지만 둘 다 `set`이 공개돼 있고, 원인이 늘면 같은 문제다.

**R4.1-10 🆕 ESC 메뉴가 입력 창구를 우회한다.** `GameManager/EscMenu.cs:53` `Input.GetKeyDown(KeyCode.Escape)` — 게임에서 `PlayerInput` 밖에서 키를 읽는 유일한 곳이다.
`InputBindingsSO`에 메뉴 키가 없어 키를 바꿀 수 없고, 입력 API를 옮길 때 이 파일도 고쳐야 한다(“`PlayerInput.cs`만 고치면 된다”는 설계 전제가 깨짐).
- 권장: `InputBindingsSO.menuKey` + `PlayerInput.MenuPressed`(억제와 무관하게 동작).

**R4.1-11 🆕 결과 화면·승패 판정이 탈출 모드를 직접 안다.** `Monster/GameRuleController.cs:26-82`이 `EscapeManager.Instance.State`·`IsDeparting`·`EscapeRules`를,
`Monster/ResultScreenController.cs:118-157`이 `EscapeManager.Instance.State`·`MonsterJarTrophy`를 직접 쓴다. 모든 맵이 탈출 모드가 돼
**생존 모드 경로(`AllCookiesBroken`·`Finish`·`Present`)는 출시 맵에서 도달하지 않는다**(PlayerTestScene에서만).
- 권장: 규칙을 `IRoundRules { Decision Evaluate(); }`로 나눠 `SurvivalRules`·`EscapeRules`가 구현하고, 맵(또는 `EscapeManager`의 존재)이 고르게 한다. 결과 화면도 `IResultPresenter` 2개로.

### R4.2 Manager 간 의존성 과도 증가 — ⚠ 악화

**R4.2-1 ⏸ 처형 5클래스 결합**(4차 판 그림 그대로, 줄 번호만 이동).

**R4.2-2 🆕 `EscapeManager` 정적 조회의 확산.** 다른 도메인 8개 파일 14곳이 탈출 모드 구현 클래스를 직접 부른다.

| 호출 | 위치 |
|---|---|
| `EscapeManager.IsWaiting` | `Unit/HideOrSeekPlayer.cs:53, 66` |
| `EscapeManager.Instance`(AttachEscapeMode) | `Unit/HideOrSeekPlayer.cs:219` |
| `EscapeManager.HasLeftMap` | `Unit/PlayerGrabController.cs:39` |
| `PlayerInventory.Local` | `Unit/PlayerGrabController.cs:47` |
| `EscMenu.Instance` | `GameManager/RoomExitController.cs:27` |
| `EscapeManager.IsActive` | `Monster/MonsterController.cs:134`, `Monster/GameRuleController.cs:26` |
| `EscapeManager.Instance.State`·`IsDeparting` | `Monster/GameRuleController.cs:48, 69`, `Monster/ResultScreenController.cs:123`, `Monster/SpectatorController.cs:95, 100` |
| `EscMenu.IsOpen` | `Camera/MonsterFirstPersonCamera.cs:92` |

`EscapeManager`는 반대로 Unit(`HideOrSeekPlayer` — `EscapeCharacterState`·`PlayerInventory`), Monster(`MonsterController` — `MonsterEscapeState`), ColorTag(`PlayerPaintCanvas` — `ToolHitReceiver`)를 안다.
**Unit ↔ Escape, Monster ↔ Escape 양방향 순환**이 새로 생겼다(부록 A).
- 권장: Core에 `IEscapeQuery`(“이 액터가 맵을 떠났나 / 기다리나 / 행동이 허용되나”)를 두고 `EscapeManager`가 구현해 `EscapeQuery.Current`에 등록한다.
  캐릭터 쪽 탈출 모드 부착은 `IEscapeModeAttachment`(캐릭터가 Awake에서 `GetComponents<IEscapeModeAttachment>()`를 `Init`)로 뒤집는다.

**R4.2-3 📈 마스터 전용 폴링 9개.** `EscapeManager.Update`(`Escape/EscapeManager.cs:114-125` — 위치 추적·초기화·잡힘 보고)가 더해졌다(부록 D).
`GameRuleController`는 탈출 모드에서도 매 프레임 `PhotonNetwork.PlayerList` 전원의 Props를 5번씩 조회해 목록을 다시 만든다(`:50-61`).

### R4.3 Prefab과 Script의 역할 뒤섞임 — ⚠ 악화

| # | 내용 | 근거 | 상태 |
|---|---|---|---|
| 1 | 코드가 프리팹 자식 이름을 안다 | `Unit/HideOrSeekPlayer.cs:214` `Mesh_0` | ⏸ |
| 2 | 애니메이터 트리거 = enum 이름 | `PlayerAnimationDriver.cs:28-29, 54`, `MonsterController.cs:417-418` | ⏸ |
| 3 | 클립 길이를 이름 키워드로 | `MonsterController.cs:183`, `InteractableDoor.cs:264` | ⏸ |
| 4 | 같은 렌더러를 두 컴포넌트에 따로 배선 | `PlayerSkinApplier.bodyRenderer` ↔ `PlayerPaintCanvas.bodyRenderer` | ⏸ |
| 5 | 참조 없는 `ColorSelectionPanel.prefab` | R5-15 | ✅ 삭제 |
| 7 | 런타임에 UI를 코드로 생성 | 4차: 상호작용 안내뿐 → 🆕 **`EscapeHud`(356줄), `MonsterJarTrophy`, `ToolUser` 조준점, `GrabAimReticle`** | 📈 |
| 8 | 스폰 겹침 검사 크기가 쿠키 캡슐 상수 | `Core/SpawnPositionFinder.cs:10-12` | ⏸ |
| 10 | 괴물 프리팹에 쿠키 연출 튜닝값 | R4.1-8 | ⏸ |
| 11 | 테스트 씬에 네트워크 캐릭터 프리팹(활성) | PlayerTestScene `HideOrSeekPlayer`(이번에 다시 확인) | ⏸ |
| **13** | **캐릭터 구성을 런타임 `AddComponent`로** | 아래 | 🆕 |
| **14** | **연출 파티클·조명·임시 모델을 코드로 생성** | 아래 | 🆕 |
| **15** | **Blender 모델 자식 이름 규칙에 의존하는 연출** | 아래 | 🆕 |

**R4.3-13 🆕 쿠키·괴물 프리팹이 실제 구성을 보여 주지 않는다.**
- 쿠키: `StunReceiver`(`Unit/HideOrSeekPlayer.cs:170`, `Init(2.3f)` — 머리 높이), `HeldItemPresenter`·`EscapeCharacterState`(`:221-222`, 전원), `PlayerInventory`(`:223`, 본인) → `ToolUser`(`Escape/PlayerInventory.cs:35`).
- 괴물: `StunReceiver`(`Monster/MonsterController.cs:132`, `Init(5.2f)`), `MonsterEscapeState`(`:134`), `MonsterViewSwitcher`(`:150`, 본인) → `GrabAimReticle`(`Camera/MonsterViewSwitcher.cs:40`).
- 장치: 상자 자리에 `MaterialChest`가 없으면 붙인다(`Escape/EscapeManager.cs:97`).
- 영향: 인스펙터에서 값을 조정할 수 없고(별 높이 2.3/5.2가 코드 상수), 프리팹을 열어 봐서는 쿠키가 무엇으로 이뤄졌는지 알 수 없다.
  `Awake` 순서(`EscapeManager.Awake`가 먼저 돌아야 함)에 묶인다 — 지금은 캐릭터가 항상 나중에 생성돼 문제가 없다.
- 권장: 프리팹에 컴포넌트를 미리 붙여 두고(탈출 모드가 없으면 스스로 꺼짐), 캐릭터는 `Init`만 부른다. `StunReceiver`의 머리 높이는 직렬화 필드로.

**R4.3-14 🆕 연출 리소스를 코드로 만든다.** 파티클 6종(`CoasterSequence.CreateSparks`, `OvenSequence.CreateSmoke`·`Flash`, `TrainSequence.CreateSmoke`, `SpyRocket.CreateSmoke`),
조명 5종(오븐·제단·기적·등불·유리병), 마녀 임시 모델(`WitchPresenter.BuildPlaceholder`), 물풍선·섬광·별(`ToolFx`·`ToolShotFx`·`StunReceiver`).
수치가 코드에 흩어져 아티스트가 손댈 수 없고, 빌드에서 셰이더·변형이 빠지는 문제(이미 한 번 겪음 — `SoftParticle.mat`으로 해결)가 다시 생길 수 있다(R5-21).
- 권장: 파티클·조명은 프리팹(`Assets/04. Prefabs/Escape/Fx/`)으로 만들고 `EscapeFxCatalogSO`에서 찾는다. 연출 코드는 `Play/Stop/SetRate`만.

**R4.3-15 🆕 연출이 Blender 자식 이름 규칙에 의존한다.** `Cake_Shard_`, `Path_`, `Bulb_`, `Car_`, `LapBar_`, `Seat_nn`, `Slot_nn`, `Board`, `Mouth`, `Lantern_nn`, `Float_nn`,
`Slot_nn_Empty/_Filled`, `Spin*` 등. 규칙은 `escape_devices.py`와 주석에 적혀 있고 `EscapeTests`가 일부(칸·좌석·탈 곳)를 검사한다 — **현실적인 선택**이다.
남은 위험: 이름 오타는 조용히 연출이 빠진다(대부분 `null`이면 건너뜀). 검사하지 않는 이름(`Bulb_`, `Lantern_`, `Float_`, `Spin`)을 테스트에 더하면 된다.

### R4.4 Scene에 직접 의존하는 코드 — ⚠ 증가

| # | 의존 | 위치 | 상태 |
|---|---|---|---|
| 1 | `GameObject.Find(이름)` | `Core/SceneSpawnPoints.cs:13`, `GameManager/PlayerSpawner.cs:56`, `Monster/MonsterJoinController.cs:72`, `Dev/MonsterTestSpawner.cs:24`, 🆕 `Escape/Sequences/OvenSequence.cs:63` | 📈 5곳 |
| 2 | `Camera.main` | 12개 파일 18회(🆕 `ToolUser`·`WitchPresenter`·`MonsterGrabKillTrigger`·`GrabAimReticle`·`MonsterViewSwitcher` 등) | 📈 |
| 3 | 컴포넌트 존재로 씬 판별 | `PaintPhaseController.IsPaintScene`, 🆕 `EscapeManager.IsActive`(탈출 모드 씬 판별) | ⏸ |
| 4 | PUN 내부 키 `curScn` | `Monster/MonsterJoinController.cs:18` | ⏸ |
| 8 | 맵 빌더가 테스트 씬 내용에 의존 | `Editor/Maps/MapSceneBuilder.cs`(복제 원본 PlayerTestScene) | ⏸ |
| **9** | **맵 씬 5개에 테스트 부트스트랩이 남음** | 아래 | 🆕 |
| **10** | **씬 이름 규칙으로 레시피 선택** | `Escape/EscapeManager.cs:83` `SceneManager.GetActiveScene().name.Replace("Game_", "")` | 🆕 |
| **11** | **연출이 맵 오브젝트를 이름으로 찾아 부모를 바꿈** | `Escape/Sequences/OvenSequence.cs:61-76`(`HAU_MagicOven_Door`를 새 피벗 아래로 옮김) | 🆕 |
| **12** | **월드 원점·먼 좌표 가정** | `WitchPresenter.cs:38`(맵 중심 = 원점), `MonsterJarStage.cs:15`(5000, −5000, 5000에 무대) | 🆕(낮음) |

**R4.4-9 ✅ 맵 씬 5개에 `__OfflineBoot`(OfflineModeBootstrap, `autoCreateRoom=1`)가 저장돼 있다 — 해결(2026-10-02, 2단계).**
- ✅ 다섯 맵 씬과 원본 백업 `Maps/Original/Game_CandyForest.unity`에서 지움. `Editor/Tests/BuildSceneTests.cs` 추가: 빌드 목록의 출시 씬(PlayerTestScene 제외 7개)의
  재귀 의존성에 `Assets/02. Scripts/Dev/` 스크립트가 없어야 한다(꺼진 오브젝트·프리팹 속 컴포넌트도 잡힘). 8/8 통과, PlayerTestScene에서는 Dev 스크립트 2개를 실제로 찾아내 탐지가 동작함을 확인.
  검증 도구는 `stop.py`가 Play Mode를 멈춘 뒤 `__OfflineBoot`를 지운다(1단계).
- YAML 확인: 다섯 맵 모두 `m_Name: __OfflineBoot`, `m_IsActive: 0`. **비활성이라 실행 중에는 아무 일도 하지 않는다**(`Awake`도 불리지 않음).
- 출처: 이 세션의 검증 도구(`play_offline.py`)가 Play Mode 시험용으로 만든 오브젝트가 씬 저장 때 함께 저장된 것으로 보인다 — 작업자(Claude) 쪽 잔여물이다.
- 위험: 누가 실수로 켜면 `OfflineModeBootstrap.Awake`가 `PhotonNetwork.OfflineMode = true`를 시도한다. 온라인 연결 중이면 PUN이
  `"Can't start OFFLINE mode while connected!"` 오류를 남기고 무시하므로(`Photon/.../PhotonNetwork.cs` setter 확인) 판이 깨지지는 않지만 맵마다 콘솔 오류가 생긴다.
- 권장: 다섯 씬에서 지우고, “출시 맵 씬에 `Dev/` 스크립트가 없어야 한다”는 EditMode 테스트(`GameScenes_UseSingleSceneCorePrefab`과 같은 YAML 검사)를 추가한다.
  검증 도구는 씬을 저장하기 전에 `__` 접두사 오브젝트를 지우도록 고친다.

### R4.5 Singleton 남발 — ⚠ 주의(악화)

| 종류 | 4차 | 5차 | 새로 생긴 것 |
|---|---|---|---|
| `public static X Instance` | 0 | **2** | `EscapeManager.Instance`(`Escape/EscapeManager.cs:13`), `EscMenu.Instance`(`GameManager/EscMenu.cs:22`) |
| 정적 “현재 로컬/활성” 참조 | 1 | **4** | `PlayerInventory.Local`(`Escape/PlayerInventory.cs:16`), `ToolUser.Aiming`(`Escape/ToolUser.cs:22`), `PlayerInput.IsMenuOpen` |
| 정적 인스턴스(private) | 1 | **2** | `EscapeHud.instance`(`Escape/EscapeHud.cs:17`) + 정적 API `Toast`·`Notice` |
| SO 캐시 | 2 | **4** | `EscapeCatalogSO.cached`, `EscapeTextsSO.cached` |
| 가변 정적 상태 합계(부록 B) | 14 | **22** | |

- 둘 다 **씬 수명**이고 `OnDestroy`에서 `Instance`를 비운다(✅). `EscMenu.OnDestroy`는 `IsMenuOpen`도 되돌린다(✅).
- 문제는 개수보다 **사용 방식**이다: 정적 접근 덕분에 Unit·Monster·Camera가 탈출 모드를 직접 알게 됐다(R4.2-2).
  `EscapeHud.Toast`(정적)는 `EscapeDevice`·`WitchPresenter`가 HUD 존재를 모른 채 부르는데, HUD가 없으면 조용히 무시된다(적절).
- `EscapeManager.Awake`는 레시피 확인 **전에** `Instance`를 설정하고, 실패하면 `enabled=false`만 한다(`:81-89`). 그래서 `Instance != null`이어도 비활성일 수 있는데,
  `IsActive`는 이를 거르지만 `IsWaiting`·`HasLeftMap`·`SpectatorController.VehicleFocus`는 `Instance`만 본다(`State`가 null이라 결과적으로 안전).
- 권장: R4.2-2의 `IEscapeQuery` 등록(레시피 확인 뒤에만 등록). `EscMenu.IsOpen` 대신 `PlayerInput.IsMenuOpen`만 쓰게 해 `EscMenu.Instance`의 외부 사용을 `RoomExitController` 하나로 줄인다
  (그것도 직렬화 참조로 바꿀 수 있다 — 둘 다 GameSceneCore 안에 있다).

### R4.6 ScriptableObject의 책임 — ✅ 양호

| SO | 평가 |
|---|---|
| GameSettingsSO | ✅ 인원표·방 시간 설정 추가, `OnValidate`가 범위 정리. ✅ R5-16 해결. ⏸ 툴팁 `gameMapScenes`의 “비어 있으면 GameScene을 쓴다”(`Core/GameSettingsSO.cs:51`)는 사실이 아니다(이제 시작을 막음) |
| 🆕 EscapeCatalogSO | 아이템 목록·레시피·공용 모델. 지연 사전 `byId`(런타임 캐시를 SO 인스턴스에 둠 — 도메인 리로드가 켜져 있어 무해) |
| 🆕 EscapeRecipeSO / ItemSO / ToolSO | 읽기 전용 + `EditorSetup`(빌더 전용, `#if UNITY_EDITOR`). 도구 밸런스가 에셋에 있다(좋음) |
| 🆕 EscapeTextsSO | 한글 문구를 에셋에 둔다(규칙 준수). ⚠ 필드가 **public 가변**이다(다른 SO는 모두 private + 읽기 전용 프로퍼티) — 런타임 코드가 실수로 바꿀 수 있다 |
| ColorPaletteSO | ⏸ 주석 “10개 고정”(`:6`)은 사실이 아니다. 🆕 `ToolUser.cs:142` `Random.Range(0, 10)`이 팔레트 색 수 10을 가정(받는 쪽이 `% palette.Count`로 보정) |

SO로 옮길 후보(코드 상수): 연출 시간표(시퀀스 5종 각 6~10개), 상호작용 거리(`EscapeAuthority`의 3.5·6 m, `CharacterInteractor`의 2.5 m, `EscapeDevice.MaxReachHeight` 4 m),
상자 수 규칙(쿠키 × 3, 스파이 × 2 — `EscapeAuthority.cs:58-59`), 조준 확대(`ToolUser.AimZoom` 0.72), 탑승 카메라 거리(`SpectatorController` 2 m·13 m), 처형 연출 값(R4.1-8).

### R4.7 Unity Lifecycle 순서 — ⚠ 일부 있음

**R4.7-1 ⏸** `PaintPhaseController.IsPaintScene` 정적 bool(`:16, 27, 32`).

**R4.7-2 ⏸** `ConfirmDialog`가 `Awake`에서 자기 자신을 끈다(`GameManager/ConfirmDialog.cs:19`).

**R4.7-3 📈 `GetComponent<T>()?.` 5곳.** `Unit/HideOrSeekPlayer.cs:136`(`PlayerInventory`), `:152`·`:238`(`SpectatorController`), `ColorTag/CookieLifeStatePresenter.cs:176`, `Escape/EscapeCharacterState.cs:46`.
에디터에서 `GetComponent`는 컴포넌트가 없으면 “가짜 null” 객체를 돌려줘 `?.`를 통과한다. `SpectatorController`는 프리팹에 늘 있어 안전하지만,
**`PlayerInventory`는 탈출 모드 씬의 본인 쿠키에만 붙는다**. 탈출 모드가 없는 PlayerTestScene을 에디터에서 실행해 쿠키가 잡히면 `RequestDropAll()`이 가짜 객체에서 불려
`manager` 필드(null)를 건드려 예외가 날 수 있다(코드 추론, 미재현). 빌드에서는 진짜 null이라 문제없다.
- 권장: `TryGetComponent(out T c)`로 바꾼다.

**R4.7-4 ⏸** 원격 복사본 첫 프레임 물리(`HideOrSeekPlayer.cs:191`, `MonsterController.cs:161`).

**R4.7-6 ⏸** 처형 중 원격 쿠키 Transform 두 작성자(`HideOrSeekPlayer.cs:267` ↔ `CookieLifeStatePresenter.cs:99`).

**R4.7-7 ⏸** `GamePhaseStarter` 첫 프레임 경합(`Monster/GamePhaseStarter.cs:13-30` — 코드 변화 없음).

**R4.7-8 ⏸** 채팅을 연 채 씬이 내려갈 때 `OnDisable`이 파괴 중인 입력창을 끔(`GameManager.cs:89-93`, 미확인 유지).

**R4.7-9 🆕 마녀 내리치기 카메라 흔들림이 보이지 않는다(코드상 확실).**
- `Escape/WitchPresenter.cs:110-115`가 **`Update`**에서 `cam.transform.position += Random.insideUnitSphere * strength`로 흔든다.
- 같은 프레임 뒤에 실행되는 `Camera_Ctrl.LateUpdate`(`Camera/Camera_Ctrl.cs:114-116`)가 `transform.position = m_BuffPos`로, 괴물 1인칭은 `MonsterFirstPersonCamera.LateUpdate`(`:82`)가
  `SetPositionAndRotation(eye.position, …)`으로 위치를 통째로 다시 쓴다. 렌더링은 `LateUpdate` 뒤라 흔들린 위치가 한 번도 그려지지 않는다.
- 관전(파괴된 쿠키)·탑승 대기 카메라도 `Camera_Ctrl`이므로 **모든 화면에서 흔들림이 없다.**
- 권장: `CameraShake`(Core, 정적 오프셋 + 감쇠)를 두고 두 카메라가 `LateUpdate` 끝에서 오프셋을 더한다. 마녀는 `CameraShake.Add(strength, duration)`만 부른다. 사운드 계획의 “충격음”과 같은 지점이다(R8).

**R4.7-10 🆕 기절한 원격 쿠키가 다른 화면에서 얼어 있다(코드 추론).**
- `ToolHitReceiver.Apply`는 모든 클라이언트에서 `StunReceiver.Stun`을 부른다(`Escape/ToolHitReceiver.cs:48-49`, 별 이펙트용).
- 그러면 원격 복사본에서도 `IsMovementLocked`가 true가 돼(`Unit/HideOrSeekPlayer.cs:52`) `Update`가 **보간(`:267`)·애니메이션 상태 반영(`:268`) 전에** 돌아간다(`:255`).
  기절 동안(쿠키 1.5초, 물풍선 설정값) 다른 화면에서는 위치·자세가 멈췄다가 풀리면 한 번에 따라간다. 본인 화면에서 미끄러지는 동안(R5-19) 차이가 더 커진다.
- 같은 이유로 탈것에 탄 쿠키·탈출한 쿠키(`HasEscaped`·`IsWaiting`)의 원격 복사본도 보간을 멈추지만, 그때는 몸이 숨겨져 문제가 없다.
- 권장: `IsMovementLocked`(로컬 조작 잠금)와 “원격 표시 갱신 여부”를 나눈다. 원격 분기는 잠금과 무관하게 항상 보간한다(처형 중만 예외 — R4.7-6).

**R4.7-11 🆕 탈출 모드 부착이 `EscapeManager.Awake` 순서에 기댄다(낮음).** `HideOrSeekPlayer.Awake → AttachEscapeMode`(`:217-224`)와 `MonsterController.Awake`(`:134`)는
그 시점에 `EscapeManager.Instance`가 이미 설정돼 있어야 한다. 지금은 캐릭터가 항상 씬 로드 뒤에 `PhotonNetwork.Instantiate`되므로 성립하지만,
캐릭터를 씬에 직접 두면(PlayerTestScene처럼) 같은 프레임 `Awake` 순서에 달린다. R4.3-13의 권장(프리팹에 미리 부착 + `Start`에서 `Init`)으로 함께 해결된다.

### R4.8 Event 구독/해제 — ✅ 대체로 양호

| 이벤트 | 구독 | 해제 | 평가 |
|---|---|---|---|
| `EscapeManager.StateChanged` | `EscapeHud.Create`, `EscapeCharacterState.Init`, `HeldItemPresenter.Init/OnEnable`, `PlayerInventory.Init` | 각 `OnDestroy` / `OnDisable` | ✅ 짝 맞음(`HeldItemPresenter`는 `-=` 후 `+=`로 중복 방지) |
| `PlayerInventory.Changed` | `EscapeHud.Update`(`:188-195`, 바뀔 때 재구독) | **`EscapeHud.OnDestroy`에서 해제하지 않음** | 🆕 낮음 — 같은 씬에서 함께 사라져 지금은 무해. `EscapeHud.Create`가 기존 HUD를 지우고 새로 만드는 경로(`:44`)가 실제로 쓰이면 지운 HUD의 `RefreshHotbar`가 불려 예외 |
| `RoomSettingsMenu.Closed` | `EscMenu.Awake`(`:40`) | 없음 | 같은 프리팹·수명이라 무해 |
| `RegionSelector.Changed` | `LobbyController.Start`(`:73`) | `OnDestroy`(`:83`) | ✅ (지역 UI가 제거돼 지금은 구독 자체가 없음 — R7.6) |
| `SpectatorController.SpectateTargetChanged`(static) | `SpectatorLabel` | `OnDisable` | ✅ |
| Photon 콜백 | `MonoBehaviourPunCallbacks` 상속 30개 | — | ✅ `OnEnable/OnDisable`을 재정의한 7개 모두 `base` 호출. ⏸ Photon 콜백을 하나도 재정의하지 않는 상속 **8개**: 4차 판 4개(`BrushCursorController`·`ColorSelectionPanel`·`GamePhaseStarter`·`GameManager`) + `HideOrSeekPlayer`·`MonsterController`(RPC·`IPunObservable`만 사용)·`MonsterJoinController`·`SpectatorController` — 모두 콜백 대상으로 등록만 된다(`MonoBehaviour`로 충분) |
| `AsyncGPUReadback`, Job | ⏸ 4차 판과 같음 | | ✅ |

주의(⏸): 문 16·15개가 각자 `IOnEventCallback`(R4.8 4차 판 내용 그대로).
🆕 주의: `MonsterEscapeState`가 마녀 내리치기 때 `MonsterController.enabled = false`(`Escape/MonsterEscapeState.cs:20`)로 끄면 `OnDisable`이 `CharacterRegistry`에서 빼고 Photon 콜백도 해제한다.
판이 곧 끝나므로 의도에 맞지만, 관전 후보·상호작용 탐색에서 괴물이 사라진다는 점을 주석으로 남길 가치가 있다.

### R4.9 Object Pool과 Instantiate/Destroy 충돌 — ✅ 충돌 없음

Photon 풀(`IPunPrefabPool`)은 여전히 없다. 🆕 로컬 풀 1개(`EscapeManager.groundPool` — `GroundItemView`를 켜고 끄며 재사용)와 미리 만든 효과 1개(`StunReceiver` 별)는 적절하다.

| 생성/파괴 지점(🆕) | 빈도 | 평가 |
|---|---|---|
| `GroundItemView.Show` 모델 교체(`Escape/GroundItemView.cs:29-35`) | 같은 칸의 아이템 종류가 바뀔 때 | 그릇은 풀, 내용은 교체 — 적절 |
| `HeldItemPresenter.Refresh`(`:57-65`) | **누군가 칸을 바꿀 때마다 모든 클라이언트에서** Destroy + 생성 | 8인 판이면 잦다. 칸별 모델 4개를 미리 만들어 켜고 끄는 편이 낫다 |
| `ToolFx.Play`·`ToolShotFx`·`BoardingFx` | 도구 사용·탑승마다 | 짧은 수명, 자체 Destroy — 적절. 다만 아래 머티리얼 누수 |
| 시퀀스 승객 인형(`CoasterSequence.cs:124-125`, `TrainSequence.cs:128-129`, `RuneAltarSequence.cs:133-134`) | 탑승 인원 변화 | 적절 |
| **R4.9-3 🆕 `EscapeVisuals.Tint`가 렌더러마다 `new Material`** | 위 모든 생성마다 | 아래 |

**R4.9-3 🆕 머티리얼 누수.** `Escape/EscapeVisuals.cs:84` `new Material(baseMaterial)`을 만들어 `sharedMaterial`에 넣고 **아무도 지우지 않는다**.
`GameObject`를 `Destroy`해도 머티리얼은 남는다. 칸 바꾸기·도구·탑승마다 쌓이다가 씬 전환(단일 로드의 `UnloadUnusedAssets`) 때 정리되므로 **판 단위로 한정**되지만,
40분 판·8인이면 수천 개가 될 수 있다(코드 추론). `EscapeExitFx.Tick`(`:40-44`)의 `r.material`도 렌더러당 하나씩 복제한다(장치 연출이 없을 때만 — 출시 맵은 모두 연출이 있어 미사용).
- 권장: 색별 머티리얼 캐시(`Dictionary<(Color, float), Material>`) 또는 `MaterialPropertyBlock`.

⏸ R4.9-1(괴물 대기 중 다른 쿠키 로컬 Destroy), ⏸ R4.9-2(대기실 참가자 목록 전부 파괴·재생성 + `HostActor()` 매 프레임 배열 할당 — `Lobby/GameLobbyController.cs:55, 117-123`).

### R4.10 Photon Ownership/RPC 구조 — ⚠ 일부 있음

잘 지켜지는 것(🆕 포함): 탈출 상태는 **방장만 쓰고**(`EscapeAuthority.Commit`), 요청자는 **`EventData.Sender`로 판정**한다(`EscapeManager.cs:229` — 4차 판에서 권한 RaiseEvent 대부분이 하지 않던 것).
방장은 요청마다 상태(살아 있음·탈출 여부·스파이 여부)·거리(마지막으로 본 위치)·인벤토리 규칙을 다시 확인한다. 탈출 성공·사망 원인·잡은 수·고른 칸은 본인이 자기 Player Props에 쓴다.
판 시작 신호·맵·스파이 목록을 **한 요청으로 원자적 기록**(`GameStartAuthority.cs:70-75`).

**R4.10-1 ⏸ 그랩 수락 절차 없음.** `Unit/PlayerGrabController.cs:53-71`(보내자마자 확정, 대상이 이미 들렸는지·들고 있는지 확인 안 함), `HideOrSeekPlayer.cs:80-91`(발신자 확인 없음).
🆕 덧붙임: 탈것에 타거나 기절한 쿠키도 그랩 대상에서 빠지지 않는다 — 숨겨진 쿠키는 콜라이더가 꺼져 `OverlapSphere`에 안 걸리지만, 기절한 쿠키는 잡힌다(의도라면 유지).

**R4.10-2 ✅(유지)** 처형 RPC 발신자 검증(`HideOrSeekPlayer.cs:113-122`).

**R4.10-3 ⏸** 문 대기 캐시가 정적(`InteractableDoor.cs:32`, `OnLeftRoom`에서 비우지 않음).

**R4.10-4 ⏸** 퇴장 시 Player Props를 API 없이 지움(`RoomExitController.cs:54-55`). 🆕 지우는 키가 `RoundPlayerKeys`라 새 키(`SelectedSlot`·`Escaped`·`CatchCount`·`DeathCause`)도 자동 포함(✅ 표 기반 설계의 효과).

**R4.10-5 ⏸** PaintStroke 캐시 없음.

**R4.10-7 ⏸ RaiseEvent 발신자 미검증 3종**(PaintStroke·ClearColor — `PlayerPaintCanvas.cs:466-470`, ClaimMonster — `Cauldron.cs:31`·`MonsterAssignmentAuthority.cs:34`, DoorStateRequest 거리).

**R4.10-8 🆕 도구 명중이 쓰는 사람과 아무 클라이언트를 믿는다.**
- **수신**: `EscapeManager.OnEvent`(`:234-236`)는 `ToolHit`을 **누가 보냈는지 확인하지 않고** `ToolHitReceiver.Handle`로 넘긴다. 정상 경로는 방장만 보내지만,
  수정된 클라이언트가 `RaiseEvent(9, …)`를 직접 보내면 원하는 사람 전원을 기절시키고(아이템 떨어뜨림 포함) 물풍선 색으로 칠할 수 있다. `EscapeNotice`(`:231-233`)도 같다(가짜 “스파이 잡힘” 알림).
- **판정**: `EscapeAuthority.ToolUse`(`:297-314`)는 횟수·쿨다운만 확인하고, **명중 대상(`r.Targets`)과 지점(`r.Pos`)은 쓰는 사람이 정한 값 그대로** 전원에게 보낸다.
  사거리(뿅망치 근접·스턴건 15 m·물풍선 10 m)·시야를 방장이 확인하지 않는다.
- 영향: 이 게임은 스파이·괴물 사이의 은밀한 견제가 핵심이라 원격 기절은 판을 쉽게 망친다. 클라이언트 신뢰 모델이라 “수정된 클라이언트”가 전제지만, 고치는 비용이 작다.
- 권장: ① 수신 측에서 `e.Sender == PhotonNetwork.CurrentRoom.MasterClientId` 확인(`ToolHit`·`EscapeNotice` 둘 다).
  ② 방장이 `lastPositions`로 “쓴 사람 ↔ 대상” 거리를 `tool.Range + 여유`로 확인하고 벗어난 대상은 뺀다(시야는 생략 가능).

**R4.10-9 🆕 방장이 바뀌면 “스파이가 잡혔다” 알림이 다시 뜬다(코드 추론).**
- `EscapeManager.brokenReported`(`:27`)는 **방장 클라이언트의 `Update`에서만 채워진다**(`:114-125, 132-140`). 비방장 클라이언트의 집합은 비어 있다.
- 방장이 나가 새 방장이 되면, 그 첫 `Update`에서 이미 파괴된 모든 사람에 대해 `authority.OnPlayerBroken`을 다시 부르고, 스파이였다면 `Notice(SpyCaught)`가 **전원에게 다시** 간다.
  (`TryDepart`·`TryLaunchRocket`은 멱등이라 무해.)
- 권장: 새 방장이 `Adopt`할 때 현재 파괴된 사람을 `brokenReported`에 미리 넣거나, “알린 스파이” 목록을 `EscapeState`에 기록한다.

**R4.10-10 🆕 방장의 로컬 즉시 적용과 서버 응답이 순서를 뒤집을 수 있다(낮음, 코드 추론).**
`EscapeAuthority.Commit`(`:504-509`)은 Room Prop을 보내고 곧바로 자기 화면에 적용한다. 서버 응답(`OnRoomPropertiesUpdate` → `ApplyLocalState`, `EscapeManager.cs:153-160`)이 나중에 다시 온다.
한 프레임에 커밋이 두 번(예: 탑승 → 출발, 떨어뜨리기 → 줍기) 일어나면 첫 번째 응답이 도착하는 순간 방장 화면이 **잠깐 첫 번째 상태로 돌아갔다가** 두 번째 응답에서 맞춰진다.
권위 상태(`authority.State`)는 바뀌지 않아 판정은 맞고, 깜빡임과 `StateChanged` 구독자의 중복 호출만 생긴다.
- 권장: `EscapeState`에 `Revision`(커밋마다 +1)을 넣고 `ApplyLocalState`가 현재보다 낮은 판을 무시한다(포맷 버전 3). 새 방장의 `Adopt`도 가장 높은 판을 기준으로.

**R4.10-11 🆕 스파이 정체가 Room Prop으로 모두에게 공개된다(설계 메모).**
`SpyActorNumbers`(Round Room Prop)는 모든 클라이언트 캐시에 있다. 정상 클라이언트는 남의 스파이 여부를 화면에 보여 주지 않지만, 수정된 클라이언트는 바로 읽을 수 있다.
같은 이유로 결과용 `RevealedSpies`를 따로 둔 설계 의도(“끝난 뒤 공개”)가 데이터 수준에서는 성립하지 않는다.
PUN에는 플레이어별 비공개 Room Prop이 없으므로 대안은 “방장이 각 스파이에게만 대상 지정 이벤트로 알리고, 방장 교체에 대비해 스파이 목록을 암호화(방 단위 키)해 저장” 정도로 비용이 크다.
지금 구조를 유지한다면 **클라이언트 신뢰 전제**를 문서에 명시해 둔다(4차 판 R4.10-2의 “피해자 클라이언트 권한” 메모와 같은 성격).
`CatchCount`(괴물이 자기 값을 씀)도 같은 전제다.

### R4.11 중복 로직 — ⚠ 다수(악화)

4차 판 항목 상태: #1 스폰 3벌 ⏸, #2 InRoom 대기 ⏸, #3 클립 길이 ⏸, #4 트리거 상태 전환 ⏸, #5 캐릭터 몸 공통 🔁, #7 표시 이름 📈(아래 #31), #8 색칠 남은 시간 2곳 ⏸,
#10 레지스트리 2벌 ⏸, #11 CanvasGroup 캐시 ⏸, #12 `IsPaintPhaseActive` 래퍼 ⏸, #13 괴물 목록 빼기 ⏸, #14 `IsRoomFull` 래퍼 ⏸, #16 ColorSlotPanel ⏸, #17 관전 진입 📈(3곳 + 탈출 1곳),
#18 진행도 끝 판정 ⏸, #19 방장 쓰기 응답 대기 패턴 ⏸(+`GameRuleController.witchRequested`, `EscapeManager.initRequested` → **11벌**), #20 맵 도구 스폰 배치 ⏸.

| # | 중복(🆕) | 위치 | 권장 |
|---|---|---|---|
| 21 | **“맵을 떠났나/탈출했나” 판정 4벌** | `Monster/GameRuleController.cs:53`, `Monster/ResultScreenController.cs:132`, `Escape/EscapeManager.cs:53-54`(`HasLeftMap`), `Escape/EscapeCharacterState.cs:40` + 반대 조건 `EscapeAuthority.cs:373`(`IsActiveEscaper`) | `EscapeState.HasEscaped(actor)`·`HasLeftMap(actor)` 두 메서드 |
| 22 | **로켓 칸 맞춤 2벌** | `Escape/SpyRocket.cs:185-191`(`RocketNeeds`, 화면) ↔ `Escape/EscapeAuthority.cs:423-435`(`RocketNeeds`·`FindRocketSlotFor`, 방장) | `EscapeState.FindRocketSlot(itemId, catalog)` 하나 — 화면과 방장이 같은 규칙을 쓴다는 것이 보장된다 |
| 23 | **깊은 자식 찾기 2벌(구현이 다름)** | `Escape/EscapeSequence.cs:39-48`(재귀) ↔ `Escape/WitchPresenter.cs:162-166`(`GetComponentsInChildren` 할당) | `TransformExtensions.FindDeep` |
| 24 | **흰 스프라이트·코드 UI 헬퍼 3벌** | `Camera/GrabAimReticle.cs:54-62` ↔ `Escape/EscapeHud.cs:347-355`(`WhiteSprite`), `EscapeHud.NewRect/NewText` ↔ `MonsterJarTrophy.Rect/Text` ↔ `ToolUser.CreateCrosshair.Bar` | `RuntimeUi` 정적 헬퍼(또는 R4.3-14대로 프리팹) |
| 25 | **승객 인형 생성 3벌** | `CoasterSequence.cs:129-141` ≡ `TrainSequence.cs:133-145`(같은 코드), `RuneAltarSequence.cs:143-153`, `BoardingFx.cs:28-33` | `PassengerModel.Create(parent, scale)` |
| 26 | **연기·불꽃 파티클 생성 4벌** | `OvenSequence.cs:129-158`, `TrainSequence.cs:158-187`, `SpyRocket.cs:149-182`, `CoasterSequence.cs:186-214` | 파티클 프리팹(R4.3-14) 또는 `ParticleFactory.Smoke(parent, settings)` |
| 27 | **흔들림 계산 3벌** | `CakeRocketSequence.cs:112-117`, `CoasterSequence.cs:216-221`, `SpyRocket.cs:135-136` | 공용 함수(R4.7-9의 `CameraShake`와 별개로 물체용) |
| 28 | **사망 확정 2벌** | `Unit/HideOrSeekPlayer.cs:130-152`(`RequestGrabKill` 본인 처리) ↔ `:227-239`(`KillByWitch`) — HitCount·DeathCause·키네마틱·관전 | `ConfirmBroken(DeathCause cause, bool presented)` |
| 29 | **살아 있는 쿠키 세기 3벌** | `EscapeHud.cs:228-235`, `EscapeAuthority.cs:289-290`(`TryDepart`)·`:363-368`(`AnyCookieStillPlaying`), `GameRuleController` → `EscapeRules` | `EscapeRules.CountActive(players)`를 HUD도 사용 |
| 30 | **방 설정 쓰기 2벌** | `Lobby/LobbyController.cs:243-255`(방 만들기) ↔ `GameManager/RoomSettingsMenu.cs:48-69`(대기실) — 분 → 초 변환, 키 목록 | R4.1-3의 `RoomSettingsAuthority` |
| 31 | **표시 이름 4벌 + 미적용 2곳** | 4차 3벌 + `ResultScreenController.cs:139, 145`(탈출 결과, 빈 닉네임 그대로)·`:100-101` | `RoomState.DisplayName(Player)` |
| 32 | **상호작용 등록 토글 4벌** | `EscapeDevice.cs:83-89`, `GroundItemView.cs:54-60`, `MaterialChest.cs:48-54`, `SpyRocket.cs:59-65` | `InteractableBase : MonoBehaviour, IInteractable` |
| 33 | **높이 제한 상수 불일치** | `CharacterInteractor.cs:13` 2.5 m(안내 아이콘), `EscapeDevice.cs:49`·`EscapeAuthority.cs:487` 4 m(방장 검증) | 한 상수(`InteractionRules.MaxHeightDifference`) — 지금은 2.5~4 m 사이 높이 차에서 아이콘은 안 뜨지만 요청은 통과 |

---

## R5. 그 밖의 발견 사항

1. **R5-4 ⏸** PUN 내부 키 `curScn`(`MonsterJoinController.cs:18`).
2. **R5-5 ⏸** 쓰기만 하는 키 `MonsterRevealTime`(쓰는 곳 `MonsterAssignmentAuthority.cs:92, 118, 166`, 읽는 곳 없음).
3. **R5-8 ⏸** 끝나지 않는 폴링(`GameLobbyController.Update`의 `HostActor()` 할당, 비괴물의 `MonsterJoinController.TryLocalSpawn` 매 프레임).
4. **R5-11 ⏸** 결과 화면 버튼을 비방장이 누르면 카운트다운만 멈춘다(`ResultScreenController.cs:180-185`).
5. **R5-13 ⏸** 입력 백엔드 혼재(이번에 재확인 — R2.1).
6. **R5-14 📈 회피 도중 들림·기절·파괴가 오면 회피가 얼었다가 풀린 뒤 이어진다.** 회피 시작 `speed *= 2f`(`Unit/HideOrSeekPlayer.cs:355`), 종료 `speed *= 0.5f`(`:375`),
   타이머는 `Update`의 `CheckDodgeInput`에서만 줄어드는데 잠기면 `Update`가 `:255`에서 끝난다. 🆕 기절(1.5초)이 생겨 잠김 경로가 늘었다. 4차 권장 그대로.
7. **R5-15 ✅** 참조 없는 `ColorSelectionPanel.prefab` — 프로젝트 전체(씬·프리팹·에셋 의존성, `Resources.Load` 경로) 참조 0건 확인 후 폴더째 삭제(2단계). `ColorSelectionPanel` 스크립트는 맵 씬이 쓰므로 유지.
8. **R5-16 ✅** `GameSettings.asset` 재저장됨.
9. **R5-18 ⏸** `MapPlaytestDriver`의 리플렉션 의존(다시 보지 않음).
10. **R5-19 🆕 기절 순간의 속도가 남아 미끄러질 수 있다(코드 추론).** 기절하면 `FixedUpdate`가 `Move` 전에 끝나(`HideOrSeekPlayer.cs:278-279`) 마지막 수평 속도가 그대로 남는다.
    지면 마찰로 멈추겠지만 달리던 쿠키는 기절 별을 단 채 조금 미끄러진다. 기절 시작 때 수평 속도를 0으로(로컬만).
    🆕 괴물은 기절해도 진행 중인 돌진(최대 0.25초)이 계속된다(`MonsterController.cs:254`가 기절을 보지 않음 — 영향 작음).
11. **R5-20 🆕 색칠 시간에 ESC 메뉴가 열려 있어도 칠해지고 커서가 사라진다(코드 추론).**
    - `PlayerPaintCanvas.HandlePaintInput`(`:224-256`)은 `PlayerInput.PaintHeld`(메뉴 억제 없음)만 본다. UI 위인지(`IsPointerOverGameObject`)도 보지 않는다.
      ESC 메뉴 상자는 화면 가운데(=카메라가 비추는 내 쿠키)에 뜨므로 “계속하기” 같은 버튼을 누르는 클릭이 뒤의 몸에 스탬프를 찍는다. 스와치 줄은 화면 아래라 지금까지 드러나지 않았다.
    - `BrushCursorController.Update`(`:76-77`)는 몸 위면 `Cursor.visible = false`로 OS 커서를 숨긴다 — 메뉴가 열려 있어도 같아서 **메뉴 버튼 위 커서가 안 보일 수 있다**.
      `EscMenu.Update`는 `lockState`만 풀고 `visible`은 건드리지 않는다(`EscMenu.cs:60`).
    - 권장: 색칠 입력·붓 커서 모두 `PlayerInput.IsMenuOpen || EventSystem.IsPointerOverGameObject()`이면 쉬게 한다(인벤토리 도구 쓰기는 이미 UI 확인을 한다 — `PlayerInventory.cs:112`).
12. **R5-21 ✅ 런타임 셰이더 키워드가 빌드에서 빠질 위험 — 해결(2단계, 아래 R6).** 이 세션에서 실제로 겪은 문제(`Shader.Find("Particles/Standard Unlit")`가 빌드에서 null → 분홍 파티클·연출 정지)와 같은 종류가 남아 있다.
    - `EscapeVisuals.Tint`(`:79`, `Shader.Find("Standard")` + `_EMISSION` 키워드), `WitchPresenter.FadeMaterial`(`:213-227`, Standard + **Fade 모드 `_ALPHABLEND_ON`**).
    - `Standard`는 항상 포함 목록에 없고(R2.1), 빌드는 **실제 머티리얼이 쓰는 키워드 변형만** 넣는다. 씬의 어떤 머티리얼도 Standard Fade를 쓰지 않으면 마녀가 서서히 나타나는 대신
      불투명하게 툭 나타나거나 잘못 그려질 수 있다(미검증 — 빌드 확인 필요). Emission 변형은 맵 머티리얼이 써서 들어갈 가능성이 높다.
    - 권장: `Resources/Escape/`에 Fade·Emission 머티리얼 에셋을 두고 복제해서 쓰거나, `ShaderVariantCollection`을 만들어 Preloaded Shaders에 넣는다. 빌드 스모크 테스트(마녀 등장 화면 캡처)를 계획에 넣는다.
13. **R5-22 ✅ “방장”의 두 의미가 방 설정에서 섞인다 — 호스트로 통일(2단계, 아래 R6).** 시작 버튼은 **호스트**(가장 먼저 들어온 사람, 괴물이어도 됨 — `RoomState.HostActor`)에게,
    방 설정 버튼은 **Photon 방장**(괴물이 아닌 최선 입장자 — `EscMenu.cs:67`)에게 보인다. 괴물이 정해진 뒤 방을 만든 사람이 괴물이면 그 사람에게는 시작 버튼만, 두 번째 입장자에게는 설정만 보인다.
    사용자 요구(“방장은 … 방 설정을 변경할 수 있어야”)의 방장이 어느 쪽인지 정하고 한쪽으로 맞춘다(시작 버튼과 같은 호스트 쪽이 자연스럽다 — 호스트가 방장이 아니면 시작처럼 요청 이벤트로).
    또 설정 창이 열린 채 방장이 바뀌면 “적용”이 아무 안내 없이 무시된다(`RoomSettingsMenu.cs:50`).
14. **R5-23 ✅ 오래된 주석·문구 — 정리(2단계).** `Core/GameSettingsSO.cs:51` 툴팁(“GameScene을 쓴다”), `Dev/OfflineModeBootstrap.cs:24-26`(OnDestroy 설명이 OnJoinedRoom 위에 붙음),
    `ColorTag/ColorPaletteSO.cs:6`(“10개 고정”), `Plan.md/Claude.md` 주요 시스템 표(`Scripts/Skill/` 등 이 프로젝트에 없는 폴더 — 다른 프로젝트 템플릿으로 보임).
15. **R5-24 🆕 단계 판정을 한 프레임에 수십 번 계산한다(성능, 낮음).** `GamePhaseState.Current`는 호출마다 Room Props를 4번 조회한다.
    `EscapeManager.ActionsAllowed`가 상호작용 후보마다(`CharacterInteractor` 0.1초 주기 × 상자 20·바닥 아이템·장치·로켓) 불리고, HUD·패널·커서·캔버스·카메라가 각자 부른다.
    지금 규모에서는 미미하지만, 프레임당 한 번 계산해 캐시(`GamePhaseState.Current`가 `Time.frameCount`로 캐시)하면 비용이 상수가 된다.
17. **R5-26 ✅ 진저브레드 시계탑 안에 괴물이 닿지 못하는 구역(MapCompactTests 실패) — 해결(2026-10-02).** 통과성 검사 결과 쿠키 도달 62,026칸 중 시계탑 안 (−2, −3), x −4~1 · z −5~−2의 42칸에
    괴물이 2.5 m 안으로 들어오지 못했다. 쿠키가 여기 숨으면 잡을 수 없었다.
    - 원인: 문 콜라이더가 아니라 **제자리를 잃은 분수 보석**이었다. `Map/Lighting` 그룹 루트에 `GIN_CookieFountain_Candy`(지름 약 2 m 볼록 `MeshCollider`+렌더러)가 붙어 원점, 곧 시계탑 나선 통로 축 한가운데 있었다.
      물체 하나뿐인 그룹 FBX가 루트로 합쳐지면서 배치를 잃었고, 축소(Compact) 때 분수 본체는 지워졌지만 루트에 붙은 보석은 남았다. 이 보석이 나선 안쪽을 막아 괴물(반지름 1.8 m)이 내려가지 못했다.
    - 수정: `MapCompactor.StripGroupRootMeshes`(그룹 루트에 붙은, 놀이 영역보다 작은 메시의 콜라이더·렌더러 제거)를 `Compact` 파이프라인에 넣고 진저브레드 씬에 적용(1개 제거).
      원점 기준의 큰 배경 지형(초콜릿 공장 `Background`)은 맞는 자리라 남긴다. 회귀 테스트 `MapScene_GroupRootsHaveNoDisplacedMesh` 5개 추가.
    - 검증: MapCompactTests 25/25(진저브레드 통과성 포함). Play Mode에서 실제 괴물을 시계탑 입구에서 나선 경사로(반지름 2.8 m)로 걸려 지하 터널 끝 (−24.4, −10.0, 0.0)까지 도착(웨이포인트 89/89, 14.4초), 콘솔 오류·경고 0.
16. **R5-25 ✅ 지역 선택 UI를 지운 뒤 남은 코드 — 정리(2단계, 고정 지역 kr만 남김).** LobbyScene에서 지역 UI를 지웠지만 `LobbyController`의 지역 전환 로직(`:24-28, 47-53, 69-75, 86-129, 257-267`)과
    `PhotonRegions`·`RegionSelector`는 그대로다. 지금은 두 필드가 비어 항상 기본 지역(kr)이다. 다시 쓸 계획이 없으면 정리한다(R7.6).

---

## R6. 권장 로드맵

**1단계 — 결함 수정 ✅ 완료(2026-10-02)**
- ✅ 카메라 흔들림을 카메라 `LateUpdate` 끝으로(R4.7-9) — `Core/CameraShake.cs` 신설, `Camera_Ctrl`·`MonsterFirstPersonCamera`가 오프셋을 더하고 마녀는 `CameraShake.Add`만 부른다.
  검증 중 추가로 발견: 마녀가 내리치면 남은 쿠키가 모두 쓰러져 관전 대상이 없어지고 `Camera_Ctrl.LateUpdate`가 대상이 없으면 바로 끝나 흔들림이 안 보였다 →
  대상이 없을 때도 마지막 위치(`m_RestPos`)에서 흔들리게 했다. Play Mode 측정: 따라가는 중 최대 0.49 m, 대상 없음(마녀 내리치기 실제 경로) 최대 0.62 m.
- ✅ `ToolHit`·`EscapeNotice` 발신자 = 방장 확인(`EscapeManager.SentByMaster`), 도구 대상 거리 검사(`EscapeAuthority.ValidTargets`, 여유 3 m)(R4.10-8).
  측정: 스턴건 12 m 명중·25 m 제외, 물풍선 8 m 명중·지점에서 6 m 떨어진 대상 제외·30 m 지점 제외, 위치 모르는 사람은 통과.
- ✅ 방장 교체 시 잡힘 알림 중복 막기(R4.10-9) — 방장이 아니어도 잡힌 사람을 기록(`TrackBrokenPlayers`), 새 방장은 알림 없이 `Reevaluate()`만. 방장 교체는 오프라인에서 재현할 수 없어 코드 검토로 확인.
- ✅ 잠김 시작 때 `DodgeOut()`·수평 속도 0, 회피 속도 `baseSpeed * 2`(R5-14, R5-19). 측정: 회피 중 기절 0.33초 뒤 회피 해제·수평 속도 0, 기절 뒤 속도 필드 5 그대로.
- ✅ 색칠·붓 커서를 메뉴·UI 위에서 쉬게(R5-20). 측정: 색칠 단계에 몸 위를 가리는 UI 0개(색칠 막힘 없음), 메뉴를 열면 `IsMenuOpen`·커서 보임.
- ✅ 원격 쿠키는 기절 중에도 보간, 처형 중에는 보간하지 않음(R4.7-10, R4.7-6 함께). 원격 복사본은 오프라인에서 만들 수 없어 코드 검토로 확인.
- ✅ `GetComponent<T>()?.` 5곳 → `TryGetComponent`(R4.7-3).
- 결과: 컴파일 오류·경고 0, RuleTests 16/16, EscapeTests 37/37, Play Mode 콘솔 오류 0.
- 🆕 같은 날 발견: **MapCompactTests 20개 중 1개 실패** — `MapScene_PassabilityIsClean("GingerbreadVillage")`: 시계탑 안 (−2, −3) 둘레 42칸이 “쿠키만 들어가고 괴물은 2.5 m 안으로 못 오는” 구역(쿠키가 숨으면 잡히지 않는 자리).
  이번 코드 수정과 무관한 맵 문제였다. ✅ 원인은 시계탑 축에 남은 분수 보석 — 제거·테스트 추가 후 25/25(R5-26).
- 🆕 테스트 도구 정리: `__OfflineBoot`가 맵 씬에 저장되던 원인(검증 도구 `play_offline.py`가 씬에 추가)을 막기 위해 `stop.py`가 Play Mode를 멈춘 뒤 지우게 했다(R4.4-9의 재발 방지 — 이미 저장된 5개 제거는 2단계).

**2단계 — 자산·설정 정리 ✅ 완료(2026-10-02)**
- ✅ 맵 씬 5개(+원본 백업 1개) `__OfflineBoot` 제거 + Dev 스크립트 금지 테스트 `BuildSceneTests`(8/8), 검증 도구는 정지 때 정리(R4.4-9).
- ✅ Fade·Emission 재질 틀 + 빌드 스모크 확인(R5-21).
  - `Resources/Escape/StandardOpaque`·`StandardOpaqueGlow`·`StandardFade`·`StandardFadeGlow.mat`(Standard, 키워드 없음/`_EMISSION`/`_ALPHABLEND_ON`/둘 다).
    런타임 재질은 이 틀을 복제한다(`EscapeVisuals.NewMaterial`·`ApplyFade`, 마녀 `FadeMaterial`도 같은 코드). 틀은 `Tools/TagOfChaos/Escape/Build Material Templates`로 다시 만든다.
  - 테스트: `EscapeTests.MaterialTemplate_ExistsWithKeywords`(4), `WitchMaterials_UseOnlyTemplateKeywords`(마녀 재질 키워드가 틀 밖이면 실패) — EscapeTests 42/42.
  - 빌드 스모크(진저브레드 한 씬, Windows 64, 셰이더 전처리 콜백으로 Standard 변형 집계 — 확인 뒤 임시 도구는 지움):
    틀이 있을 때 Fragment `_ALPHABLEND_ON`+`_EMISSION` 변형 ForwardBase 16·Deferred 8·(두 번째 묶음) ForwardBase 8,
    **틀을 빼고 다시 빌드하면 0** → 마녀의 빛나는 눈이 나타나는 5초 동안 빌드에서 잘못 그려질 문제였다(Fade만·Emission만 변형은 맵 재질 덕에 이미 들어 있었다).
  - Play Mode: 마녀 등장 중 `_ALPHABLEND_ON`·큐 3000·알파 0.22→0.91 증가, 등장 뒤 원래 불투명 재질로 복귀, 장면의 색 입힌 물체가 `StandardOpaque` 틀 복제본을 쓰는 것 확인.
- ✅ 참조 없는 `ColorSelectionPanel.prefab` 삭제(R5-15).
- ✅ 오래된 주석 정리(R5-23): `GameSettingsSO` 맵 툴팁(빈 목록이면 시작 불가), `OfflineModeBootstrap` 주석 위치, `ColorPaletteSO`(“10개 고정” → 개수는 에셋이 정함), `Plan.md/Claude.md` 주요 시스템 표를 실제 폴더·문서로 교체.
- ✅ 지역 코드 정리(R5-25, 추천안 = 정리): `LobbyController`는 `Region = "kr"` 고정 접속만 남기고 볼 지역·만들 지역·재접속·대기 방 생성 경로를 지움, `RegionSelector.cs`(`PhotonRegions` 포함) 삭제(씬·프리팹 참조 0건 확인).
  Play Mode(온라인): “Connected to region kr” → JoinedLobby, 새로고침 버튼으로 로비 재입장, 오류·경고 0.
- ✅ 방 설정 권한을 **호스트**(시작 버튼 주인)로 통일(R5-22): `Lobby/RoomSettingsAuthority.cs` 신설 — 호스트가 방장이면 바로, 아니면 `NetEventCodes.RoomSettingsRequest`(10)로 요청하고
  방장이 요청자=호스트인지·값 범위(인원은 현재 인원~최대, 시간은 설정 SO 범위)를 다시 맞춘다. `EscMenu` 설정 버튼은 `CanLocalEdit()`, 설정 창은 호스트가 아니면 “방장(호스트)만 방 설정을 바꿀 수 있습니다”를 띄운다(씬 문구).
  Play Mode(오프라인 대기실): 설정 버튼 보임, 적용 버튼 → “저장했습니다”, 남의 요청 → NotHost, 범위 밖(99명·999분·0분) → 8명·40분·1분, 요청 이벤트 경로 → 방장 수신 후 7명·25분·4분 반영, 오류·경고 0.
  호스트≠방장(괴물 호스트) 상황은 2인 온라인이 필요해 이벤트 경로를 로컬로 검증했다.
- 결과: 컴파일 오류·경고 0, EditMode RuleTests 16/16·EscapeTests 42/42·BuildSceneTests 8/8·MapCompactTests 25/25, Play Mode 콘솔 오류·경고 0.

**3단계 — 책임 정리(각 1시간 이내)**
- `IEscapeQuery`(Core)로 탈출 모드 정적 조회 대체, 메뉴 상태는 `PlayerInput.IsMenuOpen`만(R4.2-2, R4.5, R4.1-4)
- 프리팹에 탈출 모드 컴포넌트 미리 부착 + `Init`(R4.3-13, R4.7-11)
- 중복 정리 R4.11-21~33(탈출 판정·로켓 칸·FindDeep·승객 인형·사망 확정·표시 이름·상호작용 등록 토글·높이 상수)
- `RoomSettingsAuthority`(R4.1-3, R4.11-30), ESC 키를 `InputBindingsSO`로(R4.1-10)
- `EscapeState.Revision`(R4.10-10), 머티리얼 캐시(R4.9-3), 칸별 손 모델 미리 생성(R4.9)
- 4차 판 3단계 항목(처형 연출 SO, 관전 진입 한 곳, 스폰·InRoom 대기 공용화 등)

**4단계 — 구조(판 규모가 커질 때)**
- 규칙·결과 화면을 `IRoundRules`·`IResultPresenter`로 분리(생존 모드 경로 정리 포함, R4.1-11)
- 연출 파티클·조명을 프리팹 + `EscapeFxCatalogSO`로(R4.3-14), 시퀀스 시간표를 SO로(R4.6)
- 4차 판 4단계 항목(`MasterPropertyWriter`·`RoundDirector`, 폴더 재배치, asmdef 분리 — 부록 A의 순환을 먼저 끊어야 함)

**사운드 작업과의 순서** — 사운드 계획(`SoundPlan.md`)은 1단계 R4.7-9(카메라 흔들림)와 같은 지점(마녀 내리치기)을 쓰고, R4.11-21(“탈출했나” 판정)·R4.10-10(상태 판 번호)이
정리돼 있으면 탈출 모드 소리를 “상태 차이”로 깔끔하게 낼 수 있다. 1단계를 먼저 하거나, 사운드 작업 중 해당 지점을 함께 고친다.

---

## R7. 참조하지 않는 변수·메서드

### R7.1~R7.5

4차 판 결과(컴파일러·Roslyn 분석기·리플렉션·자산 참조 교차 확인)와 정리 완료 기록은 `research.prev.md` R7에 그대로 있다. 이번 판에서는 분석기를 다시 돌리지 않았다.

### R7.6 🆕 이번 판에서 읽으며 찾은 후보(텍스트 검색 기준 — 정리 전에 컴파일러로 다시 확인할 것)

| 후보 | 위치 | 내용 |
|---|---|---|
| `MonsterFirstPersonCamera.KillLookBlend` | `Camera/MonsterFirstPersonCamera.cs:86` | 선언만 있고 읽는 곳 없음(주석상 테스트·확인용) |
| `RoomSettingField.StepBy` | `Lobby/RoomSettingField.cs:71` | 선언만 있음(주석상 테스트·다른 UI용) |
| `PlayerInventory.HeldItemIndex` | `Escape/PlayerInventory.cs:26` | 선언만 있음(`HeldItemIndexAt`은 사용) |
| ~~지역 전환 경로~~ | `Lobby/LobbyController.cs`, `Lobby/RegionSelector.cs` | ✅ 정리됨 — `RegionSelector.cs`(`PhotonRegions` 포함) 삭제, 로비는 kr 고정 접속(R5-25) |
| 생존 모드 결과·판정 경로 | `GameRuleController.AllCookiesBroken`·`Finish`, `ResultScreenController.Present`, `PlayerResultRow.SetMonster(string)`·`SetCookie` | 출시 맵 5개가 모두 탈출 모드라 PlayerTestScene에서만 도달(R4.1-11) |
| 장치 연출 없는 경로 | `EscapeExitFx`, `EscapeDevice`의 `BoardingFx.Play` 대체 분기 | 출시 맵 5개 모두 시퀀스가 있어 미도달(새 맵의 임시 표시로 쓰려면 유지) |

---

## R8. 효과음·배경음 필요 지점 조사

### R8.1 현재 상태

- **오디오 자산 0개**: AudioClip 0, AudioMixer 0, 씬·프리팹의 `AudioSource` 0. 코드에 `AudioSource`·`AudioClip`·`PlayOneShot` 사용 0곳.
- 씬마다 Main Camera에 `AudioListener` 1개(빌드 씬 8개 모두). 3인칭은 카메라가 캐릭터 뒤 3.2~4.5 m, 괴물 1인칭은 눈 위치, 관전·탑승 대기는 대상 뒤 13 m까지 — **듣는 위치가 상황마다 바뀐다**.
  `MonsterJarStage`의 결과용 카메라에는 리스너가 없다(맞음 — 리스너는 하나여야 한다).
- 씬 전환: 로비 ↔ 대기실 ↔ 맵이 모두 단일 로드다. 배경음이 끊기지 않으려면 씬을 넘어 사는 재생기(DDOL)가 필요하다. 괴물은 대기실에서 메시지 큐를 멈추고 혼자 기다린다 — 그동안 소리는 로컬로만 난다.

### R8.2 설계 제약(코드에서 읽은 것)

1. **새 네트워크 메시지를 만들지 않는다.** 이 프로젝트는 연출을 상태(Room/Player Props)·공통 시계·상태 동기화(`NetworkTransformSync`의 상태 int)로 맞춘다.
   소리도 **모든 클라이언트에서 이미 실행되는 지점**에서 낸다:
   - 탈출 모드: `EscapeManager.ApplyLocalState`(`:172`) 직전·직후 상태의 **차이**(상자 열림, 아이템 위치 변화, 칸 채움, 탑승, 로켓 칸) — `EscapeAuthority`(방장 전용)에서 내면 방장만 듣는다.
   - 맵 연출: 시퀀스 `Tick(state, now)`의 시간 경계 통과(예: `CompletedAt + 3초`를 지나는 순간). 늦게 들어온 사람은 지난 소리를 듣지 않는다(맞는 동작).
   - 캐릭터 동작: 원격 복사본도 `animationDriver.ChangeState(networkSync.RemoteState)`(`HideOrSeekPlayer.cs:268`), `MonsterController.ChangeState`(`:199`)가 불리므로
     **상태 전환 지점**에서 3D 소리를 내면 본인·원격 모두 들린다. 단, 본인 점프는 `ReplayJump`(`PlayerAnimationDriver.cs:41`)가 `ChangeState`를 거치지 않으므로 따로 건다.
   - 이벤트로 오는 것: `ToolHit`(`ToolHitReceiver.Handle`), `EscapeNotice`(`EscapeHud.Notice`), `RequestGrabKill`(`CookieLifeStatePresenter.BeginGrabKill`).
2. **소리는 게임 정보다(숨바꼭질).** 쿠키 발소리·스파이 훔치기·상자 열기 소리는 괴물·쿠키에게 위치를 알려 준다. 어디까지 들리게 할지는 **게임 규칙 결정**이다(SoundPlan D3).
3. **숨겨진 캐릭터는 소리를 내지 않는다.** 탑승·탈출한 쿠키는 몸이 숨겨지고(`EscapeCharacterState.ApplyHidden`), 파괴된 쿠키는 렌더러가 꺼진다. 발소리 등은 같은 조건으로 끈다.
4. **메뉴가 열려도 게임은 멈추지 않는다(온라인).** 소리를 멈추지 않는다. 배경음 음량만 살짝 줄이는 정도.
5. **문구 규칙**: 소리 이름·ID는 영문 enum, 표시 문구(음량 설정 라벨)는 씬·SO에 한글로 입력한다.

### R8.3 배경음(BGM)이 필요한 지점

| 상황 | 판단 기준(코드) | 비고 |
|---|---|---|
| 로비 | `SceneNames.Lobby` 로드 | 접속·방 목록 |
| 대기실 | `SceneNames.GameLobby` + `GamePhase.Lobby` | 마녀 과자집·가마솥이 있는 장소 |
| 괴물 선정 카운트다운 | `MonsterSelectDeadline` 있음(`GameLobbyController.cs:72-76`) | 같은 곡에 긴장 층 추가 또는 짧은 스팅어 |
| 괴물 공개 | `MonsterRevealController.Refresh`(`:51`, 목록이 비어 있지 않게 바뀔 때) | 스팅어 |
| 괴물 대기(괴물 본인) | `MonsterLobbyWaitController.EnterWaiting`(`:62`) ~ `LoadLevel`(`:107`) | 큐가 멈춘 동안 로컬. 다른 사람은 이미 맵에 있음 |
| 맵 × 단계 | 씬 이름(`GameSettings.GameMapScenes`) × `GamePhaseState.Current` | 5개 맵 각각 **변장(Paint)**·**추격(Hunt)** 2가지 분위기 |
| 괴물 합류 순간 | `GamePhase` Paint/AwaitingMonster → Hunt | 스팅어 + 곡 전환 |
| 탈출 장치 완성 | `EscapeNotice.DeviceComplete`(`EscapeHud.Notice`) | 희망 스팅어 |
| 타임어택(마녀) | `GamePhase.TimeAttack`(`SpyEscapedAt` + `TimeAttackEndTime`) | 맵 공통 긴박한 곡, 마지막 10초 심장 박동 |
| 결과 | `GamePhase.Result` + 내 결과(탈출 성공/실패/괴물) — `ResultScreenController.PresentEscape`(`:118`) | 승리·패배·괴물 승리 징글 3종 후 짧은 결과 루프 |
| 괴물 전원 이탈 | `MonsterDepartureBanner.OnRoomPropertiesUpdate`(`:24`) | 배경음 줄이고 알림음 |

### R8.4 효과음(SFX)이 필요한 지점

**UI(2D)**

| 지점 | 위치 |
|---|---|
| 모든 버튼 클릭·호버(방 만들기·무작위 입장·새로고침·방 입장·시작·스킨·확인창 예/아니오·ESC 메뉴·방 설정·결과 “대기실로”·스와치·지우개·리셋) | 씬·프리팹의 `Button`(LobbyPanel, RoomListItem, GameLobbyPanel, GameSceneCore Canvas, ConfirmDialog, EscMenu, RoomSettingsPanel, ResultScreen) |
| 범위 밖 입력 경고 | `RoomSettingField.Apply`(`:78-82`, `OutOfRange`) |
| 입장 실패·방 이름 중복 등 | `LobbyController.OnCreateRoomFailed`·`OnJoinRandomFailed`·`OnJoinRoomFailed`(`:295-308`) |
| 창 열기·닫기 | `EscMenu.Open/Close`(`:63-77`), `RoomSettingsMenu.Open/Close`, `ConfirmDialog.Show/Hide` |
| 설정 저장 | `RoomSettingsMenu.Apply`(`:68`) |
| 카운트다운 째깍(마지막 5~10초) | `PhaseCountdownDisplay.ShowSeconds`(`:53`, 색칠·생존), `EscapeHud.Update` 타임어택(`:207-213`), `MonsterLobbyWaitController.Update`(`:91-93`), 괴물 선정 남은 시간 |
| 알림(토스트) | `EscapeHud.ShowToast`(`:290`) — 스파이 잡힘·로켓에 끼움·장치 완성·“당신은 스파이”·마녀 등장, 종류별로 다른 소리 |
| 인벤토리 칸 바꾸기 | `PlayerInventory.TrySelect`(`:116`) |
| 채팅 수신 | `GameManager.LogMsg`(`:105`) |

**캐릭터(3D, 모든 클라이언트)**

| 지점 | 본인 | 원격 |
|---|---|---|
| 발소리(걷기·달리기, 쿠키 가볍게·괴물 무겁게) | 이동 속도·접지 | 보간 위치 변화 + 상태(Walk/Run) |
| 점프·착지 | `ReplayJump`(`PlayerAnimationDriver.cs:41`), 착지 `HideOrSeekPlayer.cs:285-289` | 상태 Jump 진입·이탈 |
| 회피 | `CheckDodgeInput`(`:351-361`) | 상태 Dodge |
| 쿠키 들기·내려놓기·들림 | `PlayerGrabController.TryGrab`(`:64`)·`Release`(`:73`) | `HideOrSeekPlayer.OnGrabbedByOwner`(`:80`)는 들린 본인만 → 원격은 `RemoteIsCarrying` 변화(`PlayerNetworkSync`) |
| 괴물 촉수 돌진 | `MonsterController.ChangeState(TentacleDash)`(`:241`) | `:199` |
| 괴물 잡기(처형) — 붙잡힘·눌림·부서짐 | `CookieLifeStatePresenter.BeginGrabKill`(`:77`), 눌림 구간, `SpawnCrumbs`(`:144`) | 같음(전원 실행) |
| 괴물 조준 대상 포착 | `GrabAimReticle.LateUpdate`(`:75`, 대상 생김) | — (본인만) |
| 기절(별) | `StunReceiver.Stun`(`:21`) | 같음(전원) |
| 낙하 복귀 | `RespawnToSpawnPoint`(쿠키 `:423`, 괴물 `:387`) | 위치 순간이동 |
| 관전 대상 전환 | `SpectatorController.SwitchToNext`(`:113`) | — |

**색칠(2D·본인)**

| 지점 | 위치 |
|---|---|
| 붓질(누르는 동안 작게 반복) | `PlayerPaintCanvas.HandlePaintInput`(`:224-256`) |
| 색 고르기 | `ColorSwatchButton.OnClicked`(`:25`) |
| 슬롯 등록 | `TryRegisterSlotAndStamp`(`:288-292`) |
| 지우개·리셋 | `SetEraseMode`(`:322`), `ResetCanvas`(`:328`) |
| 강제 도포(색칠 끝) | `ApplyForcedColorIfAssignedToMe`(`:566`) |

**대기실 세계(3D)**

| 지점 | 위치 |
|---|---|
| 가마솥 부글부글(반복) | `CauldronGlow`가 붙은 가마솥 |
| 가마솥 풍덩 | `CauldronSplash.OnTriggerEnter`(`:43`) |
| 문 열림·닫힘(대기실·공장·놀이공원·베이커리) | `InteractableDoor.ApplyState`(`:204`, `instant=false`일 때) |

**탈출 모드(3D, 상태 차이 — `EscapeManager.ApplyLocalState`)**

| 지점 | 판단 |
|---|---|
| 상자 열림 | `Chests[i].Opened` false → true (`MaterialChest.Show`, `:30`) |
| 재료가 상자에 다시 생김 | `Opened` true → false |
| 줍기 | 아이템 `Loc` Ground/Chest → Held(본인은 2D, 남은 3D) |
| 떨어뜨리기·모두 떨어뜨리기 | `Loc` Held → Ground(`GroundItemView.Show`) |
| 장치에 설치 | `DeviceSlots[i].ItemIndex` -1 → ≥0 (`EscapeDevice.SetSlotVisual`, `:175`) |
| 훔치기 | `ItemIndex` ≥0 → -1(스파이 정체가 드러나지 않게 소리 크기·거리 결정 필요) |
| 장치 완성 | `CompletedAt` 0 → >0 + `EscapeNotice.DeviceComplete` |
| 탑승 | `Waiting` 증가(`EscapeDevice.PlayBoardingForNewPassengers`, `BoardingFx.Play` — Hop 폴짝 / Suck 빨려 들어감) |
| 출발 | `DepartedAt` 0 → >0 |
| 로켓 칸 채움 | `RocketSlots[i].RocketFilled`(`SpyRocket.Refresh`, `:58`) |
| 로켓 탑승(해치) | `Boarded` 증가(`SpyRocket.cs:63`) |
| 로켓 점화·발사 | `SpyEscapedAt` 기준 0초(점화)·2초(상승)(`SpyRocket.AnimateLaunch`, `:113`) |
| 마녀 등장 | `WitchPresenter.Update` 모델이 켜지는 순간(`:53-60`) |
| 마녀 내리치기 | `WitchStrike` 기록(`WitchPresenter.cs:78-83`, R4.7-9와 같은 지점) |

**도구**

| 지점 | 위치 |
|---|---|
| 스턴건 조준 시작·유지(윙) | `ToolUser.Update`의 `Aiming` false → true(`:36`, 본인) |
| 도구 사용(본인 즉시 반응) | `ToolUser.Use`(`:85`) — 방장 승인 전이라 “헛손질” 소리와 구분 필요 |
| 스턴건 발사·명중 | `ToolHitReceiver.Handle` → `ToolShotFx.Beam`(`:30`) |
| 물풍선 던짐·터짐 | `ToolShotFx.ThrowBalloon`(`:17`) → 도착 `onArrive`(`:62`) |
| 뿅망치 | `ToolHitReceiver.Apply`(`ToolFx.Play`, `:37`) |

**맵 연출(3D, 시퀀스 시간표)**

| 맵 | 지점(시퀀스 상수) |
|---|---|
| 캔디숲 케이크 로켓 | 떨림·금(0~2초), 조각 폭발(2초), 로켓 솟음(3~6초), 해치(탑승), 점화(출발 0~2초), 상승(2초~) — `CakeRocketSequence.cs:11-18` |
| 놀이공원 롤러코스터 | 전구 켜짐(0~2.4초 순차), 불꽃(0.3~3초), 떨림, 안전바(출발 0~0.8초), 출발 덜컹(1초~) — `CoasterSequence.cs:11-17` |
| 베이커리 오븐 | 톱니·피스톤(0~3초 힘 상승), 문 굴러 열림(3~6초), 빨려 들어감(탑승), 문 닫힘(출발 0~1.6초), 번쩍(1.8초) — `OvenSequence.cs:12-19` |
| 공장 기차 | 굴뚝 퍼프(0~3초 빨라짐), 기적(출발 0~0.8초), 칙칙폭폭 가속 — `TrainSequence.cs:11-16` |
| 진저브레드 룬 제단 | 등불 깜빡임(평소), 제단 빛(0~2초), 포탈 열림(2~4초), 빨려 들어감, 번쩍·닫힘(출발) — `RuneAltarSequence.cs:13-18` |

**맵 환경음(반복, 3D 또는 2D 바탕)**: 캔디숲(새·바람·사탕 풍경), 진저브레드 마을(종탑 시계 째깍·바람·지하 유적 동굴 울림), 초콜릿 공장(기계 웅웅·증기), 저주받은 놀이공원(멀리서 들리는 오르골·바람),
유령 베이커리(오븐 타닥·유령 바람). 위치 기준점은 맵마다 있는 랜드마크(장치·시계탑·오븐)와 맵 중심.

### R8.5 결론 — 상세 계획은 `Plan.md/SoundPlan.md`

- 배경음 **15곡 내외**(루프 14 + 징글·스팅어 8), 효과음 **약 75종**(UI 15, 캐릭터 18, 색칠 6, 대기실 4, 탈출 모드 16, 도구 7, 맵 연출 25, 환경음 5).
- 구조: Core에 `SoundId`/`MusicId` enum + 카탈로그 SO + 정적 창구(`GameAudio`) + 씬을 넘어 사는 재생기(소리 풀·음악 2채널 교차 전환·AudioMixer).
  소리는 **도메인별 표시 클래스 하나**가 상태 차이를 보고 낸다(예: `EscapeAudioPresenter` 하나가 `StateChanged`의 이전/현재를 비교). 권위 코드(방장 전용)는 소리를 내지 않는다.
- 결정이 필요한 것: 음원 출처, 분위기, **소리로 드러나는 정보의 범위(발소리·훔치기·상자)**, 음량 설정 위치.

---

## 부록

### A. 교차 도메인 의존성(주석 제외, Core 제외)

| 파일 | 참조하는 다른 도메인 클래스 | 4차 대비 |
|---|---|---|
| Unit/HideOrSeekPlayer.cs | Camera_Ctrl, SpectatorController·MonsterController(Monster), CookieLifeStatePresenter(ColorTag), **EscapeManager·StunReceiver·HeldItemPresenter·EscapeCharacterState·PlayerInventory(Escape)** | 🆕 Escape |
| Unit/PlayerGrabController.cs | **EscapeManager·PlayerInventory(Escape)** | 🆕 |
| Unit/PlayerSkinApplier.cs | PlayerPaintCanvas(ColorTag) | |
| Monster/MonsterController.cs | Camera_Ctrl·MonsterViewSwitcher(Camera), HideOrSeekPlayer(Unit), **StunReceiver·MonsterEscapeState·EscapeManager(Escape)** | 🆕 |
| Monster/GameRuleController.cs | **EscapeManager·EscapeRules·EscapeState(Escape)** | 🆕 |
| Monster/ResultScreenController.cs | CookieLifeStatePresenter(ColorTag), **EscapeManager·MonsterJarTrophy·EscapeState(Escape)** | 🆕 |
| Monster/SpectatorController.cs | Camera_Ctrl, **EscapeManager(Escape)** | 🆕 |
| Monster/MonsterGrabKillTrigger.cs, Cauldron.cs | HideOrSeekPlayer(Unit) | |
| Monster/MonsterLobbyWaitController.cs | GameStartAuthority(Lobby) | |
| Camera/MonsterFirstPersonCamera.cs | MonsterController(Monster), **EscMenu(GameManager)** | 🆕 |
| Camera/GrabAimReticle.cs, MonsterViewSwitcher.cs | MonsterController·MonsterGrabKillTrigger(Monster), HideOrSeekPlayer(Unit) | 🆕 |
| Interaction/CharacterInteractor.cs | **MonsterController(Monster)** | 🆕(4차: Core만) |
| GameManager/RoomExitController.cs | **EscMenu(같은 도메인)** | |
| GameManager/RoomSettingsMenu.cs | **RoomSettingField(Lobby)** | 🆕 |
| GameManager/PlayerSpawner.cs | OfflineModeBootstrap(Dev) | |
| Escape/* | HideOrSeekPlayer(Unit), MonsterController(Monster), PlayerPaintCanvas(ColorTag), SpectatorController(Monster), IInteractable·InteractableRegistry(Interaction) | 🆕 |
| ColorTag/CookieLifeStatePresenter.cs | MonsterController, SpectatorController(Monster) | |

Core는 여전히 Photon·Unity 외에 어떤 도메인도 참조하지 않는다(✅ — `GamePhase.TimeAttack`과 탈출 키는 Core의 키 표에 있다).
순환: 4차 판의 Unit ↔ Monster, ColorTag ↔ Monster에 더해 **Unit ↔ Escape, Monster ↔ Escape, Camera ↔ Monster**가 생겼다.

### B. 가변 전역(static) 상태 목록

| 상태 | 위치 | 수명 | 비고 |
|---|---|---|---|
| `CharacterRegistry.characters` | Core/GameCharacter.cs:39 | 앱 | |
| `InteractableRegistry.interactables` | Interaction/IInteractable.cs:24 | 앱 | |
| `CharacterInteractor.instance` | Interaction/CharacterInteractor.cs:15 | 앱(DDOL) | |
| `PlayerPaintCanvas.Local` | ColorTag/PlayerPaintCanvas.cs:45 | 로컬 쿠키 | |
| `PaintPhaseController.IsPaintScene` | ColorTag/PaintPhaseController.cs:16 | 씬 | R4.7-1 |
| `InteractableDoor.masterPendingWrites` | Environment/InteractableDoor.cs:32 | 앱 | R4.10-3 |
| `GameStartAuthority.lastStartTime`·`lastMap` | Lobby/GameStartAuthority.cs:20, 84 | 앱 | |
| `SpectatorController.SpectateTargetChanged` | Monster/SpectatorController.cs:26 | 앱 | 이벤트 |
| `OfflineModeBootstrap.SpawnAsMonster` | Dev/OfflineModeBootstrap.cs:16 | 씬 | |
| `GameSettings.cached`, `PlayerInput.bindings` | Core | 앱 | SO 캐시 |
| `PlayerInput.chatSuppressed`·`IsMenuOpen` | Core/PlayerInput.cs:36, 39 | 채팅·메뉴 | 🆕 메뉴 |
| `SpawnPositionFinder.OverlapBuffer` | Core | 앱 | |
| 🆕 `EscapeManager.Instance` | Escape/EscapeManager.cs:13 | 맵 씬 | |
| 🆕 `EscMenu.Instance` | GameManager/EscMenu.cs:22 | 씬 | |
| 🆕 `PlayerInventory.Local` | Escape/PlayerInventory.cs:16 | 로컬 쿠키 | |
| 🆕 `ToolUser.Aiming` | Escape/ToolUser.cs:22 | 로컬 쿠키 | |
| 🆕 `EscapeHud.instance` | Escape/EscapeHud.cs:17 | 맵 씬 | 정적 API Toast·Notice |
| 🆕 `EscapeCatalogSO.cached`, `EscapeTextsSO.cached` | Escape/Data | 앱 | SO 캐시 |
| 🆕 `MonsterJarStage.stageCount` | Escape/MonsterJarStage.cs:18 | 앱 | 결과마다 증가(무대 좌표 분리용, 되돌리지 않음) |
| ~~`PhotonRegions`(PlayerPrefs)~~ | ~~Lobby/RegionSelector.cs~~ | — | ✅ 삭제(R5-25). 이전에 저장된 `TOC_Region` 키는 더 이상 읽지 않는다 |
| 🆕 흰 스프라이트·기본 머티리얼 캐시 | GrabAimReticle, EscapeHud, EscapeVisuals | 앱 | 불변에 가까움 |

### C. 전역 네트워크·엔진 설정을 바꾸는 곳

| 설정 | 쓰는 곳 |
|---|---|
| `IsMessageQueueRunning` | false: MonsterLobbyWaitController:79 / true: GameManager:33, PlayerSpawner:65, RoomExitController:46·64, MonsterLobbyWaitController:103 |
| `AutomaticallySyncScene` | LobbyController:58, MonsterLobbyWaitController:58, MonsterJoinController:40 |
| `KeepAliveInBackground` | MonsterLobbyWaitController:77, MonsterJoinController:41, RoomExitController:65 |
| `SerializationRate`, `GameVersion`, 접속 지역 | LobbyController:77, :59, :116-122 |
| `OfflineMode` | OfflineModeBootstrap:20 |
| `Time.timeScale` | GameManager:32 |
| `Cursor.visible` / `lockState` | BrushCursorController, Camera_Ctrl, MonsterFirstPersonCamera, **EscMenu:60** |
| 🆕 `Camera.main.fieldOfView` | **ToolUser:42**(매 프레임 대입), :52 |
| 🆕 `Camera.main.transform.position` | **WitchPresenter:114**(R4.7-9) |
| `PlayerInput.IsGameplaySuppressed` / `IsMenuOpen` | GameManager / **EscMenu** |

### D. 마스터 전용 Update 폴링

| 컴포넌트 | 씬 | 조건 → 동작 | 응답 대기 가드 |
|---|---|---|---|
| MonsterAssignmentAuthority | GameLobby | 정원·기한 → 괴물 확정/초기화 | ✅ 4개 플래그 |
| MasterClientPolicy | GameLobby + GameSceneCore | 첫 프레임 → 방장 정책 | ✅ |
| RoundStateResetter | GameLobby | 첫 프레임 → 판 초기화 | ✅ |
| GamePhaseStarter | GameSceneCore | PaintPhaseEndTime 없음 → 기록 | ✅ — R4.7-7 |
| PaintPhaseController | GameSceneCore | 색칠 종료 → 강제 도포 | ✅ |
| MonsterJoinController | GameSceneCore | 색칠 종료 → 합류·종료 시각(방 제한시간) | ✅ |
| GameRuleController | GameSceneCore | Hunt·TimeAttack → 탈출 판정/마녀 | ✅ `resultRequested`, 🆕 `witchRequested` |
| RoomLifecycleWatcher | GameSceneCore | 괴물 전원 이탈 → 대기실 | ✅ |
| 🆕 **EscapeManager** | 맵 씬 | 위치 추적(매 프레임 캐릭터 전원) · 판 시작 → Initialize · 잡힌 사람 → 알림·출발·발사 재판단 | ✅ `initRequested`, ⚠ 잡힘 집합이 방장 교체 때 비어 있음(R4.10-9) |
