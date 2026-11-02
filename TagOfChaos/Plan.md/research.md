# 조사 보고서: TagOfChaos 구조 점검 9차 — §39~§46 이후 전수 재점검 · 미사용 코드/에셋 · 버그 위험 (2026-11-02)

> **이 문서에 대해**
> - 8차 판(2026-10-23, 911줄 — `A-xx`·`U-xx`·`D-xx` 번호)은 **`Plan.md/archive/research.2026-10-23.md`에 한 글자도 바꾸지 않고 옮겼다.** 7차 이전 번호(`R4.x`·`R5-x`·`F-xx`·`§12.x`)는 `archive/research.2026-10-12.md`에 있다. 코드 주석이 가리키는 옛 번호는 그 보관본에서 찾는다. 이 문서는 새 번호 **`N-xx`(발견)·`K-xx`(조치)** 를 쓴다.
> - 범위: `Assets/02. Scripts` 런타임 **221개 파일 · 25,352줄**, `Assets/Editor` 83개 파일(빌더·시험, 약 23,200줄)을 다시 읽었다. 8차 이후 바뀐 곳(§39~§46: AI 두뇌·캐릭터 5종·도구 개편·춤·스태미나·잡기 되돌림 판정·씬 이동 안전장치)은 줄 단위로 읽었고, 나머지는 8차 결론이 여전히 맞는지 근거 줄을 다시 확인했다.
> - 에셋은 Unity 에디터에서 직접 계산했다: YAML 에셋 1,644개(913 MB)의 GUID 참조 + `.meta`의 FBX 외부 재질 연결 9,544개 + 빌드 설정의 씬 7개 + `Resources` 폴더를 뿌리로 한 **의존성 그래프**, 버튼 `m_MethodName`, `GraphicsSettings`의 "항상 포함" 셰이더.
> - **10차 보정(2026-11-02, 같은 날)**: 사용자 지적(붓 커서)으로 미사용 에셋 조사를 다시 했다.
>   - 뿌리를 `Resources` 폴더 통째에서 "코드가 실제로 부르는 경로"로 좁혔다.
>   - Photon 폴더, `~` 원본 폴더, 3 MB 넘는 파일 209개를 실측했다.
>   - 사운드·음악 ID와 아이템 배포 목록을 대조했다.
>   - Unity에서 판 맵 목록을 확인했다.
>   - **6장 전체와 1장·8장·9장의 해당 줄을 고쳤다.** 나머지 장은 9차 그대로다.
> - **코드·에셋은 하나도 고치지 않았다**(조사만). 권장 조치는 8장에 우선순위로 적었다.
> - 표기: ✅ 문제 없음 / ⚠️ 개선 권장 / ❌ 문제 / 🐞 실제 버그 / 💀 죽은 코드 / 🧪 시험·에디터에서만 씀 / ↗ 8차보다 나빠짐 / ↘ 8차보다 나아짐.

---

## 목차

1. 한눈에 보는 결론
2. 게임 전체 구조와 실행 흐름 (현재)
3. 8차 이후 추가된 시스템 상세 해설 (어떻게 동작하는가)
4. 구조 점검 12항목
5. 미사용 코드 목록
6. 미사용 에셋 (10차 보정 — A 빌드 포함 미사용 / L 옛 `Game_` 맵 전용 / C 고아 / S 원본 폴더 / 크기 규칙)
7. 발견한 버그·위험 (구조 점검과 별개로 바로 영향이 있는 것)
8. 권장 조치 (우선순위)
9. 부록 — 조사 방법·수치·한계

---

## 1. 한눈에 보는 결론

| 영역 | 판정 | 한 줄 요약 |
|---|---|---|
| 🐞 바로 고칠 것 | ❌ 1건 | **물풍선 물방울 셰이더 `TagOfChaos/WaterBubble`이 빌드에 들어가지 않는다.** 코드가 `Shader.Find`로만 찾고(`WaterBubbleView.cs:22`), 그 셰이더를 쓰는 재질·씬·`Resources`가 없으며, "항상 포함" 목록에도 없다 → 빌드에서는 대체 재질(단순 반투명 하늘색)로 그려진다. 에디터에서는 정상이라 눈치채기 어렵다(N-01). |
| 네트워크 권위(Photon) | ✅ 우수(↘) | 방장 권위 Room Prop + 요청 이벤트 구조 유지. 8차 이후 **잡기 확정이 "괴물이 본 순간"으로 되돌려 재는 방식**(위치 기록 `PositionHistory`)으로 바뀌었고, 거절은 즉시 모두에게 알린다. RPC 6개 모두 보낸 사람 검증. Ownership 이전 0건. |
| 씬 이동 | ✅(↘) | PUN `LoadLevel`의 "진행 중 로드 취소 → 이후 로드 모두 막힘" 함정을 `PunSceneLoad`로 막았고, 모든 씬 로드가 한 통로를 지난다(시험으로 강제). 다만 **메시지 큐를 직접 켜는 곳이 5곳**으로 늘었다(N-09). |
| 죽은 코드 | ⚠️(↘) | 8차의 큰 덩어리(손 색칠·NetHold·Purge)는 정리됨. 새로 생긴 것: **몸 색칠 캔버스 전체가 사실상 죽음**(§45에서 물풍선 색칠을 없애 `ForceFillColor` 호출 0, 가루 색은 캐릭터 표에서 옴), **감옥 풍선(JailBalloon) 도구**가 어떤 상자에도 나오지 않음, 작은 멤버 약 25개(5장). |
| 미사용 에셋 | ⚠️ | 10차 재조사(6장): **빌드에 들어가지만 안 쓰는 것 ≈ 16 MB**(붓 커서 3.8 MB, Photon 데모 `Resources` 2 MB, 안 쓰는 사운드 32개 6 MB·음악 6곡 4.5 MB 등), **옛 `Game_` 맵 파이프라인 전용 ≈ 53 MB**(판에서 안 씀), **완전 고아 ≈ 28 MB**(캐릭터 Metallic/Roughness 9.2 MB·`Maps/Common/Models` 6.9 MB 등), `Assets` 안 `Source~` 원본 **≈ 909 MB**. FBX 100 MB 규칙 위반 2건(6.5). |
| 책임 분리 | ⚠️(↗) | 큰 클래스가 더 커졌다: `EscapeAuthority` 1,232 → **1,289줄**, `EscapeManager` 475 → **511줄**, `HideOrSeekPlayer` **682줄**(이동·점프·회피·춤·스태미나·들기 RPC·처형 RPC·동기화·리스폰·순간이동), `DecoyBrain` **519줄**. 위치 기록이 **두 벌**(N-06). |
| Manager 의존 | ⚠️ | `EscapeManager`가 하위 객체 11종을 알고(팬아웃 1위), `EscapeManager` 정적 멤버를 **32개 파일**이 부른다(8차 38 ↘). 서로 참조 짝 11쌍 — 모두 같은 기능 묶음 안. |
| Prefab ↔ Script | ⚠️(↗) | 런타임 `AddComponent` **89곳**(8차 75). 쿠키 9개·괴물 4개·`EscapeManager` 5개(AI 두뇌 포함)·HUD 10개가 실행 중 조립된다. |
| Scene 직접 의존 | ⚠️(↗) | `GameObject.Find` **7곳**(그중 `EscapeManager`의 쿠키 스폰 찾기가 **3곳·호출마다**), `FindObjectsByType` 11곳, `Camera.main` 21곳, 이름 규칙 찾기 29곳. |
| Singleton | ⚠️ | 정적 인스턴스/캐시 **24곳**(DDOL 5 · 씬 싱글턴 8 · SO 캐시 8 · 기타). 새로 `PositionHistory`(DDOL)·`DecoyBrain.Instance`·`DanceCatalogSO.Current`. 모두 파괴 시 해제 ✅. |
| ScriptableObject | ✅/⚠️ | 데이터 전용 원칙 유지(런타임에 SO 값을 쓰는 곳 0). "정적 `Current` 로더" 상용구가 **8벌** 반복, `EscapeRecipeSO` 옛 필드 7개 여전. |
| Lifecycle | ⚠️ | `[DefaultExecutionOrder]` 4곳(새로 `WaterTrapMotion(200)`·`WitchArmsDown(500)`) 의도 분명. 주의: **`GameManager.Start`·`PlayerSpawner`가 메시지 큐를 무조건 켠다** — PUN 씬 로드 중 일시정지와 겹칠 수 있다(N-09). |
| Event 구독/해제 | ✅ | `+=`/`-=` 전수 대조 — 해제 누락 0건(정적 이벤트 8개 포함). 람다 구독은 가드가 있는 1곳뿐. |
| Object Pool ↔ Instantiate | ✅ 충돌 없음 | 풀 4종(보이스·맵 조각·바닥 아이템·별). 가루 VFX는 프리팹 Stop Action = **Destroy 확인**(`CookieCrumbBurst.prefab`). |
| 중복 로직 | ⚠️(↗) | `Flat()` **9벌**, "로컬/액터 캐릭터 찾기" 루프 23곳, 처형 연출(소켓 따라가기·눌림·가루) **3벌**, `FindClipLength` 2벌, `WhiteSprite` 2벌, SO 로더 8벌. |

---

## 2. 게임 전체 구조와 실행 흐름 (현재)

### 2.1 폴더 규모

| 폴더 | 파일 | 줄 | 역할 | 8차 대비 |
|---|---:|---:|---|---|
| `Escape/` | 66 | 10,157 | 탈출 모드 — 상태·방장 규칙·건물·상자·로켓·아이템·도구·**AI 두뇌(DecoyAI/)**·마녀 연출·HUD | +13파일 · +1,899줄 |
| `Audio/` | 26 | 2,796 | 소리 엔진·음악·추격 북·출력 장치 | ≈ |
| `Core/` | 37 | 2,762 | 네트워크 키·권위 속성·방 상태·입력·**위치 기록·스태미나·씬 로드 안전장치** | +6파일 |
| `Monster/` | 22 | 2,285 | 괴물 조작·잡기 판정·게임 규칙·결과·관전·방장 정책·판 초기화 | +1 |
| `Unit/` | 14 | 1,638 | 쿠키 조작·애니메이션·들기·**캐릭터 모델 교체·춤·동작 층** | +3 |
| `Lobby/` | 14 | 1,589 | 로비·대기실·게임 시작·**캐릭터 배정** | +1 |
| `Environment/` | 13 | 1,136 | 문·놀이기구·분수·컨베이어 | +4 |
| `Camera/` | 6 | 835 | 3인칭·괴물 1인칭·마녀 연출·조준점 | ≈ |
| `GameManager/` | 10 | 686 | 채팅·스폰·나가기·ESC | ≈ |
| `ColorTag/` | 3 | 582 | 몸 색칠 캔버스(💀 대부분)·쿠키 생사 표시 | −1파일 |
| `UI/` | 4 | 465 | 설정 창 | ≈ |
| `Interaction/` | 5 | 370 | E 상호작용 | ≈ |
| `Dev/` | 1 | 51 | 오프라인 시험 방 | −1 |
| **합계** | **221** | **25,352** | | +15파일 · +821줄 |

### 2.2 씬 흐름

```
LobbyScene ──(방 만들기/참가, LobbyController → PunSceneLoad.Load(GameLobby))──▶ GameLobbyScene(대기실)
   ▲                                     │ 호스트 "시작" → GameStartAuthority.TryStart(방장)
   │                                     │   거절 조건: 방장 교체 중 · 이미 시작(PrepareEndTime) · 3초 안 중복
   │                                     │              · 방 씬이 이미 맵/로드 중(StartReadiness.IsRoomMoving)   ← §46 후속
   │                                     │   PrepareEndTime·맵·스파이·캐릭터 배정 기록 → 방 닫기(IsOpen=false)
   │                                     │   RoomSceneTransition.LoadLevelForRoom(맵)
   │                                     │     로드 중이면 미뤘다가 끝난 뒤(TickDeferred) · 아니면 OpRemoveCompleteCache → PunSceneLoad.Load
   │                                     ▼
   │                         맵 씬(CandyForest / ChocolateFactory / CursedCandyCarnival / Test_GingerbreadVillage / Test_HauntedBakery)
   │                           PlayerSpawner(쿠키)·MonsterJoinController(괴물) → PhotonNetwork.Instantiate
   │                           EscapeManager(모두: 상태 보기·요청) + EscapeAuthority(방장: 규칙) + DecoyBrain(방장: AI)
   │                           GameRuleController(방장) → GameResult · RoomLifecycleWatcher(괴물 이탈 → 대기실)
   │                                     ▼
   └──(RoomExitController 나가기)◀── ResultScreenController / RoomLifecycleWatcher → LoadLevelForRoom(GameLobby)
                                          대기실: RoundStateResetter가 판 키 지우고 방 다시 열기(될 때까지 매 프레임 재시도 — §46 후속)

어느 씬에서나(DontDestroyOnLoad 자기 부트스트랩): AudioRuntime · RoomPropertyGuard · RoomSceneFollower(방 씬 따라가기·큐 복구·
  취소된 로드 되살리기) · CharacterInteractor · PositionHistory(방장 위치 기록) · VoiceChat(설정된 씬만)
```

### 2.3 권위 모델 (누가 무엇을 쓰는가)

| 데이터 | 쓰는 사람 | 통로 | 검증 |
|---|---|---|---|
| 판 상태(아이템·상자·건물·로켓·탈출·폭탄·AI 목록) | 방장 | Room Prop `EscapeState`(이진 + `Revision`) | `EscapeRequest` 이벤트 → `EscapeAuthority.Handle`(거리·역할·소지품·의심 이동) |
| **AI 경로**(§44) | 방장(`DecoyBrain`) | Room Prop `DR_<id>`(경로 1개 ≤ 92 B, 형식 2) | 받는 쪽 `DecoyRouteBook.Sanitize`(맵 밖·술래방 점 제거), 방장 아닌 사람이 쓰면 `RoomPropertyGuard`가 되돌림 |
| 단계 시각·괴물 목록·결과·사망 | 방장 | `AuthorityProps.SetRoom/SetPlayer`(24곳) | `RoomPropertyGuard` |
| **처형 확정**(§46) | 방장(`GrabKillAuthority`) | `HitCount`·`DeathCause`·`CatchCount` | 괴물이 보낸 "본 순간"의 두 위치·시각을 방장 위치 기록과 대조(4.11) |
| 내 위치·애니메이션·춤 번호·조준 | 본인 | `PhotonView` + `PlayerNetworkSync`(7개 값) | 방장 0.2초 이동 속도 검사 + **스태미나 추정 검사**(로그만) |
| 스태미나 | 본인 화면만 | 없음(움직임으로 드러남) | 방장 `StaminaWatch` 추정 |
| 내 선택 칸·녹음·스킨·들어온 씬 | 본인 | Player Prop(`Writer.Self`) | `NetKeys.Scopes` |
| 들기·처형 연출 | 드는 쪽 / 괴물 | `PhotonView.RPC` | 받는 쪽 `info.Sender`·거리, 처형 거절은 방장만 보낼 수 있음 |
| 채팅 | 각자 | RPC `LogMsg` | `ChatText.Sanitize` + `ChatRateLimiter` |

---

## 3. 8차 이후 추가된 시스템 상세 해설

### 3.1 AI(미끼)가 플레이어처럼 움직이기 — `Escape/DecoyAI/` (§44)

| 클래스 | 줄 | 하는 일 |
|---|---:|---|
| `DecoyNavMesh` | 37 | 맵마다 구운 `NavMeshData`(쿠키 몸 크기: 반지름 0.45·높이 2·턱 0.45·경사 40°)를 `OnEnable`에서 등록, `Ready` 정적 플래그 |
| `DecoyHideSpot` | 28 | 창고·텐트 안 숨을 자리(정적 목록, 자리당 2마리) |
| `DecoyRules` | 94 | 순수 판단: 성격(씨앗 → 겁·배회 범위·행동 가중치), 다음 행동 고르기, 도망 시작/해제 거리, 도망 지점 점수, 춤 확률 3% |
| `DecoyRoute` | 199 | **경로 1개 = 시작 시각 + 걸음 종류 + 속도 + 점 ≤ 12개 + 달리기 끝 지점(`RunUntil`)**. `Sample(route, now)`가 순수 계산으로 위치·방향·`Moving`·`Running`을 낸다. 부호화 형식 2(형식 1도 읽음) |
| `DecoyRouteBook` | 91 | Room Prop `DR_<id>` ↔ 경로 사전(받는 쪽 점 검사) |
| `DecoyBrain` | 519 | **방장만**: 0.3초 주기로 AI를 나눠 생각(프레임당 길 찾기 4회), 행동(배회·쉬기·건물 방문·숨기·도망·춤)을 경로로 만들어 0.1초마다 모아 쓴다. 지나가는 문을 연다. 방장이 되면 남은 경로를 이어받는다(스태미나 50). 술래 위치는 `CharacterRegistry`에서 |
| `DecoyView` | 249 | **모든 화면**: 같은 경로를 `PhotonNetwork.Time`으로 따라가 그린다(위치를 보내지 않음). 바닥 높이 보정, 걷기/달리기/춤/잡힘(Held) 동작, 발소리·목소리, 잡히면 괴물 손을 따라가 눌리고 가루 |
| `DecoyPresenter` | 123 | 상태의 AI 목록 ↔ `DecoyView` 생성·삭제, 캐릭터별 템플릿 |

동작 요약: 방장이 "언제 어디서 어디로 어떤 속도로"만 정해 Room Prop에 쓰면, 모든 화면이 같은 시각 함수로 같은 위치를 계산한다. 위치 동기화 트래픽이 없고, 늦게 들어온 사람도 같은 장면을 본다. AI 스태미나(§46.4)는 방장이 경로별로 "달린 시간 → 걸은 시간 → 멈춘 시간"을 나눠 진행하고, 달리기 경로에는 **남은 스태미나로 달릴 수 있는 거리까지만** `RunUntil`을 넣는다. 그 뒤 구간은 걷기 속도로 `Sample`된다.

### 3.2 캐릭터 5종 — `Unit/CharacterModelSwap`·`PlayerSkinApplier`·`Lobby/CharacterAssignment` (§45)

- 방장이 시작 때 `CharacterAssignments`(입장 순서대로 선택 인정, 겹치면 남은 캐릭터)를 Room Prop으로 쓴다.
- 각 쿠키는 `PlayerSkinApplier.CharacterIndexOf`로 자기 번호를 정하고, `CharacterModelSwap`이 쿠키 뼈대를 `__cookie`로 이름을 바꾼 뒤 새 모델을 붙여 Humanoid 아바타를 바꾸고 `Animator.Rebind`한다. 같은 애니메이션 컨트롤러를 5종이 공유한다.
- 뼈를 캐시하는 쪽(`HeldItemPresenter`·`CarriedBodyPresenter`)은 아바타가 바뀌면 다시 읽는다.
- 가루 색은 `SkinCatalogSO.GetCrumbColors(index)` — 5종 모두 값이 있다(Unity 확인: 3·4·4·4·4색).

### 3.3 도구 개편 — `Escape/ToolUser`·`ToolAim`·`ToolHitReceiver`·`WaterBubbleView`·`WaterTrapMotion`·`Unit/PlayerActionPose` (§45)

- 사용 흐름: 본인 `ToolUser`가 조준·대상 고르기(`ToolAim.PickInCone`) → `EscapeRequest(ToolUse)` → 방장 `EscapeAuthority.ToolUse`가 사거리·대상 수(물풍선·뿅망치 1명) 확인 → `ToolHit` 이벤트(맞는 시각 `hitAt` 포함) → 모두의 `ToolHitReceiver`가 **맞은 사람 본인**에게만 기절·갇힘을 적용하고, 효과는 모두가 같은 시각에 그린다.
- 물풍선: `ToolAim.ArcPoint`로 포물선 미리보기(`BalloonAimPreview`), 비행 시간 `FlightSeconds`, 맞으면 `StunReceiver.Trap` → `WaterTrapMotion`(0.6초에 5 m 떠오름, `[DefaultExecutionOrder(200)]`) + `WaterBubbleView`(셰이더 — 7장 N-01).
- 뿅망치: 두 손 들기, 휘두르는 동작의 맞는 순간(0.32)에 명중 판정(`HammerHit` 코루틴), 3초 기절.
- 동작 층: `PlayerActionPose`가 Animator의 Action 층(상체 마스크)과 Trapped 층 가중치를 조절한다.

### 3.4 잡기 확정 — `Monster/MonsterGrabKillTrigger` → `Unit/HideOrSeekPlayer.RequestGrabKill` → `Monster/GrabKillAuthority` (§46 GK)

1. 괴물 본인 화면: 조준(구 캐스트 + 시야선) → E → `PlayGrabKill()` + RPC `RequestGrabKill(viewId, 괴물 위치, 쿠키 위치, ServerTimestamp)`를 **모두에게**.
2. 방장: `GrabKillAuthority.TryConfirm` — 공통 조건(`GrabPreconditions`: 보낸 사람이 괴물·사냥 단계·살아 있음·맵 안·간격) 뒤 `Judge`:
   - 기록이 있으면 **되돌림 판정**(`NetSecurityRules.JudgeGrabClaim`): 본 두 위치의 거리 ≤ 잡기 거리 + 0.5 m, 주장이 0.6초 이내, 쿠키 주장 위치가 방장 기록(`PositionTrack`, 0.05초 × 30칸)의 [본 시각 − 0.5초, 지금] 길에서 2 m 안, 괴물 주장 위치도 같은 검사(방장 화면의 원격 괴물은 늦으므로 **마지막 속도로 지연만큼 앞으로 늘린 길**까지 — `TryMinDistanceLeading`).
   - 기록이 없으면 옛 규칙(지금 위치 + 3 m).
3. 모든 화면: `CookieLifeStatePresenter.BeginGrabKill`(손 소켓 따라가기). 거절이면 방장이 **즉시** RPC `GrabKillRejected` → 연출 취소(자리·크기·충돌 복원), 쿠키 본인 바로 움직임, 괴물 동작·쿨다운 반환.
4. AI 잡기도 같은 원리(`EscapeAuthority.DecoyWithinReach` — 경로를 0.05초 간격으로 되짚고, 괴물 손 거리는 늘린 기록으로).

### 3.5 스태미나 — `Core/StaminaModel`·`StaminaWatch`, `Escape/StaminaHud` (§46 ST)

- `StaminaModel`(순수): 최대 100, 달리기 20/s, 회복 25/s(0.8초 지연, 걸으면 ×0.7), 0이면 35까지 달리기 금지, 회피 25. 값은 `GameSettingsSO.Stamina`.
- 쿠키 본인 `HideOrSeekPlayer`만 계산(대기실처럼 `EscapeManager`가 없으면 `Stamina == null` → 무한). 막대는 인벤토리 위(`StaminaHud`), 회피 칸 덮기(`SkillHud`), 바닥날 때 숨소리(`SoundId.CookieBreath`).
- 방장 `StaminaWatch`: 0.2초 위치 차이로 "달렸는지" 추정해 같은 규칙을 돌리고, 지쳤어야 할 때 3초 넘게 달리면 경고 로그(막지 않음).

### 3.6 씬 이동 안전장치 — `Core/PunSceneLoad`·`RoomSceneTransition`·`RoomSceneFollower`, `Lobby/GameStartAuthority`·`StartReadiness`, `Monster/RoundStateResetter` (§46 후속)

- `PunSceneLoad.Load`: PUN `LoadLevel`이 방장일 때 진행 중이던 로드를 `allowSceneActivation=false`로 버리는데, Unity에서는 그 로드가 90 %에서 멈춰 **이후 모든 로드를 막는다**. 부르기 직전 로드를 잡아 두었다가 취소됐으면 즉시 다시 허용한다. `IsBusy`는 PUN 내부 필드(`_AsyncLevelLoadingOperation`)를 리플렉션으로 읽는다(시험이 필드 존재를 확인).
- `RoomSceneTransition.LoadLevelForRoom`: 로드 중이면 미뤘다가 `TickDeferred`로 이어서.
- `GameStartAuthority.TryStart`: 방 씬이 이미 맵이거나 로드 중이면 거절. 대기실 버튼도 그동안 잠금("게임을 시작하는 중...").
- `RoundStateResetter`: 대기실 첫 프레임에 방장 쪽 `curScn` 응답이 아직 처리 전일 수 있어, 판 키 지우기·방 다시 열기를 **될 때까지 매 프레임** 재시도.

### 3.7 춤 — `Unit/Dance/DanceCatalogSO`, `PlayerAnimationDriver.PlayDance` (§44.9)

- B키: 플레이어용 가중치로 춤 하나를 골라 `Dance` 상태(Animator int `DanceId`). 움직이거나 다른 행동을 하면 멈춘다. 춤 번호는 `PlayerNetworkSync`로 전달.
- AI: 행동이 끝날 때 3% 확률로 AI용 가중치 춤(경로 `Gait.Dance`).
- 새 춤 추가 = 카탈로그 항목 + 클립(빌더 `DanceStateBuilder`가 상태를 만든다).

---

## 4. 구조 점검 12항목

각 항목: **판정 · 근거(파일:줄) · 8차 대비 · 권장**. `K-xx`는 8장 조치표 번호.

### 4.1 쓰이지 않는 메서드·변수·필드·클래스·리소스가 있는가 — ⚠️ 있음(8차보다 적음)

5장(코드)·6장(에셋)에 전부 적었다. 큰 묶음만:

| 묶음 | 규모 | 원인 |
|---|---|---|
| 💀 몸 색칠 캔버스 | `PlayerPaintCanvas` 208줄(+ `ColorPaletteSO`·`PaintStamp`·`PlayerPaintedSkin` 셰이더·`NetEventCodes.PaintStroke`) | §45에서 물풍선 색칠을 없애 `ForceFillColor`(`PlayerPaintCanvas.cs:86`) 호출 0. 남은 일은 ① 쿠키 스킨을 "빈 캔버스 합성" 셰이더로 바꿔 그리기(`:53`)와 ② 가루 색 샘플(`CookieLifeStatePresenter.cs:115`)인데, ②는 캐릭터 표에 5종 모두 가루 색이 있어 **실행되지 않는다**. 쿠키마다 512² RenderTexture 1장을 계속 들고 있다 |
| 💀 감옥 풍선 | `ToolKind.JailBalloon` 분기 8곳(`ToolUser.cs:189`, `ToolHitReceiver`, `HeldItemPresenter`, `EscapeAuthority.IsBalloon`…) + 카탈로그 아이템 | §45에서 스파이 도구 목록에서 뺐다. 카탈로그에는 남아 있지만 어떤 상자·레시피에도 들어가지 않는다(Unity 확인) |
| 💀 옛 레시피 | `EscapeRecipeSO` 필드 7개(`ExitKind`·`Mode`·`Groups`·`CounterLabel`·`CounterItems`·`FillsCapacity`·`SpyToolbox`·`ExtraToolboxesPerSpy`·`MaxRequired`) + `EscapeExitKind`·`RecipeMode` enum | 8차와 같음 — 에셋 빌더만 채움 |
| 💀 작은 멤버 | 약 25개(5.2절) — 그중 6개는 이번 §46 작업에서 생긴 것(`MaxRemoteLead`, `IsRunning`, `DeferredScene` 등) | |

### 4.2 기존 책임 분리를 무시하는 코드가 있는가 — ⚠️(↗)

| # | 근거 | 설명 | 권장 |
|---|---|---|---|
| N-02 | `Unit/HideOrSeekPlayer.cs` 682줄 | 이동 물리·점프·회피·**춤**·**스태미나**·들기 RPC 3개·**처형 RPC 2개**·동기화·리스폰·순간이동·속도 증가를 한 클래스가 한다(8차 이후 +약 100줄) | 처형 수신부(`RequestGrabKill`·`GrabKillRejected`·확정 대기)를 `CookieGrabKillReceiver`로, 춤·스태미나 입력을 `CookieLocomotion`으로 분리 |
| N-03 | `Escape/EscapeAuthority.cs` 1,289줄 | 20종 작업 + 이동 감시 + **스태미나 추정** + 재료 안전장치 + AI 잡기 되돌림 | 8차 A-04 그대로: `partial class` 작업군별 분리. 이동 감시(`TrackPositions`·`WatchStamina`·의심)는 `MovementWatch`로 |
| N-04 | `Escape/EscapeManager.cs` 511줄 | 상태 동기 + 뷰 갱신 + 요청 + 지형 질의에 **NavMesh 도달성 검사**(`IsWalkableFromSpawn`, 2026-10-31)까지 | 지형 질의 묶음을 `EscapeGeometry`로(8차 A-03) |
| A-01(계속) | `GameManager/GameManager.cs` | 이름은 게임 매니저, 하는 일은 채팅 + **시간 배율·메시지 큐 켜기**(`:48-49`) | `ChatController`로 이름 변경, 큐 조작은 제거(N-09) |
| A-02(계속) | `ColorTag/CookieLifeStatePresenter.cs` | 쿠키 생사·처형 연출이 색칠 폴더에 | `Unit/`로 |
| A-06(계속) | `Interaction/CharacterInteractor.cs:51,100` `is MonsterController` | 인터페이스 설계에 구체 타입 분기 | `IGameCharacter`에 "잡기 우선" 속성 |
| ↘ A-05 | `PlayerInput.SlotWheelDelta` | 8차의 `BrushSizeDelta` 이름 문제 해결됨 | — |
| N-05 | `Monster/` 폴더 | 게임 규칙·결과·방장 정책·판 초기화·관전이 "Monster" 폴더에 있다(방 수명 관리) | 이동은 비용이 크니 새 파일부터 `Round/`(또는 `Core/Room/`)에 |

### 4.3 Manager 간 의존성이 과도한가 — ⚠️

| 대상 | 수치 | 판정 |
|---|---|---|
| `EscapeManager` 팬아웃 | **11종**(`DecoyBrain`, `DecoyPresenter`, `EscapeAudioPresenter`, `EscapeAuthority`, `EscapeHud`, `EscapeState`, `GamePhaseState`, `MonsterRoomGuard`, `NoiseBombPresenter`, `RoomState`, `WitchPresenter`) | ⚠️ 탈출 모드의 조립자 역할이라 어느 정도는 정상. 다만 하위 객체가 다시 `EscapeManager`를 참조하는 **짝 6쌍** |
| `EscapeManager` 정적 멤버 사용 | **32개 파일**(`HasLeftMap` 11, `ActionsAllowed` 10, `IsActive` 7, `Instance` 8 — 겹침 포함) | ⚠️ 8차 A-08 그대로. Audio·Monster·Unit·Camera·GameManager까지 |
| `RoomState` 팬인 | 34 매니저류(전체 56파일) | ✅ 읽기 전용 정적 도우미 |
| `GamePhaseState.Current` | 10곳 · 매 호출 방 속성 3번 조회 | ✅ 동작 문제 없음, 매 프레임 여러 곳에서 불러 작은 낭비 |
| 서로 참조 짝 | 11쌍(`EscapeManager`↔하위 6, `EscapeHud`↔`SkillHud`·`StaminaHud`, `GameManager`↔`RoomExitController`, `MonsterController`↔`MonsterEscapeState`) | ✅ 모두 같은 묶음 안 — 묶음 사이 순환 없음 |

권장(K-08): 8차 A-08 유지 — `HasLeftMap`·`ActionsAllowed`처럼 방 속성만으로 답할 수 있는 질문은 `EscapeQuery`(정적·순수)로.

### 4.4 Prefab과 Script의 역할이 뒤섞이는가 — ⚠️(↗)

- 런타임 `AddComponent<…>` **89곳**(8차 75). 상위: `EscapeHud` 10, `HideOrSeekPlayer` 9(`CookieAudio`·`CarriedBodyPresenter`·`StunReceiver`·**`PlayerActionPose`**·`HeldItemPresenter`·`EscapeCharacterState`·`HatFlight`·`SkateVisual`·`PlayerInventory`), `EscapeManager` 5(**`DecoyBrain`** 포함), `VoiceRecorderPanel` 5, `VoiceChat` 5, `MonsterController` 4, `WitchPresenter` 4.
- 코드로 만드는 시각물: `WaterBubbleView`(구 + 매듭), `BalloonAimPreview`(LineRenderer), `StaminaHud`, `XRayMarker`, `ToolShotFx`, `MonsterJarTrophy`, 자리표시 마녀·무지개 상자.
- 판정: 프리팹을 덜 건드려 병합 충돌이 적은 장점은 그대로지만, **인스펙터에서 값을 볼 수 없는 컴포넌트가 늘었다**. `PlayerActionPose`의 수치(던지기 0.35·휘두르기 0.32)는 상수라 조정하려면 코드를 고쳐야 한다.
- 권장(K-09): 8차 A-09 유지 — 항상 붙는 컴포넌트(쿠키의 `StunReceiver`·`CookieAudio`·`CarriedBodyPresenter`·`PlayerActionPose`, 괴물 4개)는 프리팹에 미리 붙이고 `Init`만 코드로.

### 4.5 Scene에 직접 의존하는 코드가 늘어나는가 — ⚠️(↗)

| 방식 | 횟수 | 위치 |
|---|---:|---|
| `GameObject.Find(이름)` | 7 | `PlayerSpawner.cs:51,69`, `MonsterJoinController.cs:28`, `SceneSpawnPoints.cs:13`, **`EscapeManager.cs:406,451,458`**(쿠키 스폰 — 8차 2곳 → 3곳, `FallbackDropPoint`·`IsNearCookieSpawn`은 **호출마다** 찾음) |
| `FindObjectsByType` 등 | 11 | `GameLobbyController` 3, **`DecoyBrain.cs:65`(문 전체)**, `GameManager`, `MonsterAssignmentAuthority`, `VoiceRecorderPanel`, `SpectatorController`, `AudioListenerAnchor`, `IInteractable`, `GameCharacter` |
| `Camera.main` | 21 / 14파일 | 대부분 매 프레임 |
| 이름 규칙 찾기 | 29 | `EyeSocket`, `Lid`, `Model`/`SlamArm`/`Head`/`Thigh.L`(마녀), `Door_`/`ExitTrigger_`(탈출구), `Flame`/`Hatch`/`Spin`(로켓), `Map`(마녀 길), `CookieStack`, `Label`/`KeyButton`(조작키 화면) |
| `Resources.Load` 프리팹 | 2 | `DecoyBrain.cs:66`·`DecoyView.cs:226`이 쿠키 프리팹을 통째로 불러 걷기 속도·가루 설정을 읽음 |

권장(K-10): `EscapeManager`에 쿠키 스폰 지점 직렬화 필드 하나(3곳의 `GameObject.Find` 제거). `DecoyBrain`의 걷기 속도는 `GameSettingsSO`로, 문 목록은 `InteractableRegistry`에서.

### 4.6 Singleton이 남발되는가 — ⚠️ 24곳, 대부분 정당

| 종류 | 클래스 | 판정 |
|---|---|---|
| DDOL 자기 부트스트랩 | `AudioRuntime`, `RoomPropertyGuard`, `RoomSceneFollower`, `CharacterInteractor`, **`PositionHistory`**(새) | ✅ 앱 전역 서비스. `PositionHistory`는 방장만 기록하고 아니면 비운다 |
| 씬 싱글턴 | `EscapeManager`, `EscMenu`, `EscapeHud`, `WitchPresenter`, `WitchShatter`, `VoiceChat`, `VoiceRecorderPanel`, `AudioListenerAnchor`, **`DecoyBrain`**(새) | ✅ 모두 `OnDestroy`에서 해제 |
| `Local` 정적 | `PlayerInventory.Local` | ✅(8차의 `PlayerPaintCanvas.Local`은 제거됨 ↘) |
| SO 정적 캐시 | `GameSettings`, `EscapeCatalogSO`, `EscapeTextsSO`, `SoundCatalogSO`, `MusicCatalogSO`, `ChaseAudioSettingsSO`, `VoiceChatSettingsSO`, **`DanceCatalogSO`**(새) | ✅ 읽기 전용. 다만 같은 로더 코드 8벌(4.12) |
| 정적 상태 | `GameStartAuthority`(시작 시각·대기 요청·직전 맵), `MasterClientPolicy`(요청), `RoomSceneFollower`(자기 로드), `RoomSceneTransition`(미룬 씬), `PunSceneLoad`(지켜보는 로드), `GrabKillAuthority`(사전·마지막 판정), `CameraShake`, `IndoorZone` 등 73개 정적 가변 필드 | ⚠️ 씬·방이 바뀔 때 초기화하는지 개별 확인 필요. `GrabKillAuthority`는 `sceneLoaded`로 비움 ✅, `GameStartAuthority.lastMap`·`lastStartTime`은 **방을 나가도 남는다**(다른 방에서 3초 안에 시작하면 `AlreadyStarted` — 실제로는 거의 없음) |

### 4.7 ScriptableObject의 책임이 잘못 쓰이는가 — ✅/⚠️

- ✅ SO는 데이터만(설정·카탈로그·문구·경로). 런타임 SO 쓰기 0(에디터 `Editor*` 세터 제외). 새 `StaminaSettings`는 `GameSettingsSO` 안의 직렬화 묶음으로 들어가 원칙에 맞다. 조작키 변경은 SO가 아니라 PlayerPrefs ✅.
- ⚠️ "정적 `Current` + `Resources.Load` + 없으면 경고/기본값" 코드가 **8벌**(`GameSettings`, `EscapeCatalogSO`, `EscapeTextsSO`, `SoundCatalogSO`, `MusicCatalogSO`, `ChaseAudioSettingsSO`, `VoiceChatSettingsSO`, `DanceCatalogSO`). 동작은 맞지만 하나는 SO 밖(`GameSettings`), 나머지는 SO 안. → `ResourceAsset<T>.Get(path)` 제네릭 하나로(K-13).
- ⚠️ 카탈로그에 쓰이지 않는 아이템(`JailBalloon`)과 옛 레시피 필드가 남아 인스펙터가 헷갈린다(4.1).
- ⚠️ `ColorPaletteSO`는 죽은 색칠에만 쓰인다.

### 4.8 Unity Lifecycle 순서 문제가 있는가 — ⚠️ 1건 주의

| 확인 | 결과 |
|---|---|
| `[DefaultExecutionOrder]` | `CarriedBodyPresenter(-50)`, **`WaterTrapMotion(200)`**(이동 뒤 몸을 띄움), **`WitchArmsDown(500)`**(Animator 뒤 팔 내림), `WitchRevealCamera(1000)` — 모두 의도 분명 ✅ |
| Awake에서 다른 싱글턴 읽기 | `HideOrSeekPlayer.Awake`·`MonsterController.Awake`가 `EscapeManager.Instance/IsActive`를 읽는다. 캐릭터는 씬 로드 뒤 `PhotonNetwork.Instantiate`로 생기므로 `EscapeManager.Awake`가 늘 먼저 ✅(씬에 캐릭터를 미리 두면 깨지는 가정) |
| `RuntimeInitializeOnLoadMethod` | 9곳(`PositionHistory` 추가) — 첫 씬의 `Awake`보다 늦은 `AfterSceneLoad`라 첫 씬 코드가 `Awake`에서 이들을 찾으면 없다. 지금은 그런 코드 없음 ✅ |
| 스킨·모델 교체 순서 | `PlayerPaintCanvas.Start`가 `PlayerSkinApplier.Awake` 뒤를 가정(주석으로 명시). 모델 교체 뒤 뼈 캐시는 아바타 변경 감지로 다시 읽음 ✅ |
| ⚠️ N-09 **메시지 큐를 직접 켜는 곳 5곳** | `GameManager.cs:49`(Start), `PlayerSpawner.cs:78`, `RoomExitController.cs:32,50`, `RegionMove.cs:53`, `RoomSceneFollower.cs:115`. PUN은 씬 로드 동안 큐를 멈췄다가 로드가 끝나면 스스로 켠다. 위 코드는 대부분 "로드가 끝난 씬의 Start"라 평소엔 무해하지만, **미뤄진 두 번째 로드·취소된 로드를 되살린 경우**(3.6) 중간 씬의 `Start`가 큐를 켜면 다음 씬으로 갈 메시지(인스턴스 생성·RPC)를 사라질 씬에서 처리할 수 있다. → 큐 제어를 `RoomSceneFollower` 한 곳으로(K-04) |

### 4.9 Event 구독/해제가 제대로 되는가 — ✅

- 인스턴스 이벤트(`EscapeManager.StateChanged` 구독자 7곳, `PlayerAnimationDriver.StateChanged`, `MonsterController.StateChanged`): 모두 `Init`에서 구독 → `OnDestroy`에서 `manager != null` 확인 후 해제 ✅. `HeldItemPresenter`는 다시 붙을 때 `-=` 후 `+=`로 중복 방지 ✅.
- 정적 이벤트 8개(`SpectatorController` 2, `WitchPresenter.Impacted`, `KeyBindings/InputDevices/OutputDevices/AudioVolumeSettings/VoiceChatSettings.Changed`): 구독자 전부 짝 ✅.
- `SceneManager.sceneLoaded`: 정적 부트스트랩 3곳은 `-=` 후 `+=`로 중복 방지 ✅, `AudioListenerAnchor`는 OnEnable/OnDisable 짝 ✅. 람다 구독은 `EscapeVisuals.cs:93` 한 곳(가드 플래그로 1회) ✅.
- `Button.onClick.AddListener` 36곳 / 해제 3곳 — 모두 **자기 자식 버튼**이라 함께 파괴된다 ✅. `RoomListItem`·`ConfirmDialog`는 `Awake`에서 한 번만 등록해 다시 써도 중복 없음 ✅.

### 4.10 Object Pool과 Instantiate/Destroy가 충돌하는가 — ✅ 충돌 없음

| 풀 | 생성/파괴 방식 |
|---|---|
| `AudioVoicePool`(사전 생성 칸·세대 번호) · `WitchShatter`(`Stack<Piece>`, 미리 만들기) · `EscapeManager.groundPool`(`GroundItemView`, 숨김/재사용) · `StunReceiver` 별 | 짧은 효과(`ToolFx`·`ToolShotFx`·`WaterBubbleView`·`AlarmBeacon`·`NoiseBombView`·`XRayMarker`)는 매번 `new GameObject`/`Destroy`. `GameLobbyController` 목록은 매번 전부 파괴·재생성(같은 일을 하는 `UiListBuilder`가 있는데 안 씀) |

- 풀 객체를 `Destroy`하거나 파괴된 것을 풀에 돌려주는 경우 **없음** ✅.
- 가루 VFX(`CookieCrumbBurst.prefab`)는 Stop Action = **Destroy**, Loop 끔, 1초 — 코드가 지우지 않아도 사라짐 ✅(8차 A-13의 우려는 프리팹 설정으로 확인).
- 런타임 `new GameObject` 52곳 — 대부분 판 1회. 매 사용 생성하는 것은 도구 효과뿐이라 풀은 선택 사항.

### 4.11 Photon의 Ownership/RPC 구조를 무시하는가 — ✅ 잘 지킴

| 확인 | 결과 |
|---|---|
| `PhotonNetwork.Instantiate` | 2곳(쿠키·괴물 — 자기 캐릭터만) ✅ |
| `PhotonNetwork.Destroy` | 1곳(방장이 가짜 괴물 제거) ✅ |
| Ownership 이전 | 0곳 ✅ |
| `[PunRPC]` | **6개**: `HideOrSeekPlayer` 5(`OnGrabbedByOwner`·`OnReleased`·`OnGrabRejected`·`RequestGrabKill`·**`GrabKillRejected`**), `GameManager.LogMsg` — 모두 `info.Sender` 검증. `GrabKillRejected`는 방장만 받아들임 ✅ |
| `RaiseEvent` | 13곳 — 요청 → 방장, 방장 → 모두/대상. 받는 쪽 `SentByMaster`·`Sender` 확인 ✅ |
| Room Prop 쓰기 | `AuthorityProps` 24곳 + `OfflineModeBootstrap`(🧪) ✅. AI 경로 `DR_*`도 `AuthorityProps.SetRoom` ✅ |
| Player Prop 쓰기 | 본인 키만(`SkinIndex`·`SelectedSlot`·`HasVoice`·`LoadedScene`·판 초기화) ✅ |
| 위치 신뢰 | 처형은 괴물 주장을 **방장 기록과 대조**해 받아들임(조작 시 `CookieMismatch`·`MonsterMismatch`·`ClaimTooOld`) ✅ |
| ⚠️ N-06 **위치 기록 두 벌** | `EscapeAuthority.lastPositions`(액터 번호 키, 0.2초, 모든 클라이언트, 이동 검사·드롭 위치·도구 거리) ↔ `PositionHistory`(ViewID 키, 0.05초, 방장, 처형). 같은 데이터를 두 곳에서 다른 주기·다른 키로 모은다. **액터 키 쪽은 한 사람이 캐릭터 둘을 가지면 섞인다**(오프라인 시험에서 `[NetGuard]` 경고가 이것 때문) |
| ⚠️ N-07 PUN 내부 의존 | `PunSceneLoad`가 PUN의 private 필드를 리플렉션으로 읽는다 — PUN 업데이트로 이름이 바뀌면 `IsBusy`가 옛 방식(진행도)으로 떨어진다(시험 `Sources_AllSceneLoadsGoThroughPunSceneLoad`가 필드 존재를 확인하므로 업데이트 시 바로 드러남) |

### 4.12 중복 로직이 있는가 — ⚠️(↗)

| # | 중복 | 위치 | 권장 |
|---|---|---|---|
| D-10 | **수평 거리 `Flat()`** 9벌 | `EscapeAuthority:1023`, `GrabKillAuthority:115`, `ToolUser:273`, `ToolAim:57`, `MapArea`, `DecoyRules:82`, `MonsterRoomGuard:116`, `WitchShatter:287`, `WitchRevealCamera:158` | `VectorX.FlatDistance/Flat` 하나 |
| D-01(계속) | **액터로 캐릭터 찾기 / 내 캐릭터 찾기** | `CharacterRegistry.All` 순회 23곳: `SkillHud.LocalCharacter`, `CookieHole.LocalCharacter`, `StaminaHud.LocalStamina`, `DecoyView.FindMonster`, `VoiceChat.BodyOf`, `EscapeAudioPresenter.PlayOnActor`, `ToolHitReceiver.HandOf`, `NoiseBombPresenter`, `ChaseDirector` 2, `MonsterRoomGuard`, `MonsterRoomDoor`, `EscapeGate`, `DecoyBrain.CollectMonsters`… | `CharacterRegistry.Local`·`Monsters`·`FindByOwner`로 |
| D-09(계속·↗) | **처형 연출(소켓 따라가기·눌림·가루·소리)** 3벌 | `CookieLifeStatePresenter.FollowGrabSocket:188`·`SpawnCrumbs`, `DecoyView.TickCaught`·`SpawnCrumbs:202`, `WitchFinaleStage.SpawnCrumbs` | `GrabCrushPresenter`(진행도 → 크기·소리·가루) 하나를 쿠키·AI가 공유 |
| D-11 | `FindClipLength` 2벌 | `MonsterController:196`, `InteractableDoor:290` | 정적 `AnimatorUtil` |
| D-04(계속) | 코드 UI 도우미 | `EscapeHud.NewRect/NewImage/NewText/WhiteSprite`, `GrabAimReticle.WhiteSprite`(같은 코드), `VoiceRecorderPanel`, `MonsterJarTrophy`, `CutsceneFade` | `UiFactory` |
| D-12 | SO 정적 로더 8벌 | 4.7 | `ResourceAsset<T>` |
| D-13 | 스폰 지점 찾기 | `SceneSpawnPoints.TryFindClearPosition`(정식) vs `PlayerSpawner`·`MonsterJoinController`·`EscapeManager`가 각자 `GameObject.Find` | 정식 하나로 |
| D-14 | 위치 기록 | N-06 | `PositionHistory` 하나로 합치고 `lastPositions`는 거기서 읽기 |
| D-07(계속) | 목록 UI | `GameLobbyController.cs:175-179`가 `UiListBuilder`를 안 씀 | `UiListBuilder` |
| D-05·D-06·D-08(계속) | 레지스트리 2벌, 애니메이션 상태 전환 2벌, "등록 플래그" 상용구 6벌 | 8차와 같음 | 8차 권장 그대로 |

텍스트 그대로 복사된 6줄 이상 덩어리는 2곳뿐이다(`MonsterController:174`↔`HideOrSeekPlayer:337` 충돌 무시 설정, `CookieLifeStatePresenter` 안). 중복은 대부분 "같은 생각을 각자 다시 쓴" 형태다.

---

## 5. 미사용 코드 목록

> 판정: 이름 기반 전수 집계(주석 제거, 런타임 + 에디터 + 시험 전체에서 참조 수) → 씬·프리팹 GUID 참조(Unity 계산) → 버튼 `m_MethodName`(`OnMakeRoomButtonClicked`·`OnRandomJoinButtonClicked`·`OnRefreshButtonClicked`·`OnStartGameButtonClicked`·`OpenSettings`는 **씬 버튼이 부름 → 제외**) → 자동 실행(`AudioRuntime.Boot`·`NetworkDefaults.ApplyRates` — `[RuntimeInitializeOnLoadMethod]`, 제외) → 확장 메서드(`HashtableCopyExtensions.Merged` 사용 중, 제외) → 남은 후보를 직접 열어 확인.
> 런타임 MonoBehaviour·SO 스크립트는 **모두** 씬·프리팹·에셋에 놓였거나 코드가 붙인다(놓이지 않은 스크립트 0개).

### 5.1 죽은 기능 (호출 경로가 없음)

| 기능 | 근거 | 지울 때 함께 |
|---|---|---|
| 몸 색칠 | `PlayerPaintCanvas.ForceFillColor:86` 호출 0 / 가루 색 샘플은 캐릭터 표가 대신 | `PlayerPaintCanvas`(스킨 합성 재질 적용만 남기거나 통째로), `ColorPaletteSO`·팔레트 에셋, `PaintStamp`·`PlayerPaintedSkin` 셰이더, `NetEventCodes.PaintStroke`(번호는 비워 둠), `CookieLifeStatePresenter`의 샘플 분기 |
| 감옥 풍선 | 카탈로그에 있으나 배포 경로 0 | `ToolKind.JailBalloon` 분기 8곳(enum 값은 직렬화 번호라 남김), 카탈로그 아이템 |

### 5.2 작은 멤버 (선언만 있음 — 참조 0)

| 파일:줄 | 멤버 | 비고 |
|---|---|---|
| `Core/NetSecurityRules.cs:81` | `MaxRemoteLead` | §46 후속에서 생김 — `RemoteLead`가 `GrabClaimMaxAge`로 상한 |
| `Unit/HideOrSeekPlayer.cs:562` | `IsRunning` | §46 ST1 |
| `Core/RoomSceneTransition.cs:16` | `DeferredScene` | §46 후속(Play Mode 확인용으로만 썼음) |
| `Unit/PlayerActionPose.cs:32` | `IsAiming` | §45 |
| `Unit/CharacterModelSwap.cs:72` | `BodyRenderers()` | §45 |
| `Escape/DecoyAI/DecoyBrain.cs:58` | `AgentCount` | §44 |
| `Escape/EscapeNet.cs:3-6` | 클래스 `EscapeNet` + `EncodeSlot` | 파일 안 enum·구조체만 쓰임 |
| `Escape/EscapeVisuals.cs:232` | `CreatePassenger` | |
| `Escape/WitchPresenter.cs:78-81` | `GroundContact`·`MapCenter`·`StartPoint` 속성 | 필드는 내부에서 씀 |
| `Audio/GameAudio.cs:64` | `PlayMusic` | 음악은 `MusicDirector`가 직접 |
| `Audio/SoundSettingsPanel.cs:102` | `PickOutput` | |
| `Environment/FountainWater.cs:23`, `SpinPart.cs:15` | `WaterMaterialIndex`, `DegreesPerSecond` | |
| 🧪 `Environment/VikingRide.cs:120` `EditorSetPhase`, `Escape/VoiceRecorderPanel.cs:211` `EditorFakeRecording`, `UI/KeyBindingsPage.cs:170` `EditorKeyText` | 시험 훅인데 **시험에서도 안 씀** | |
| `ColorTag/ColorPaletteSO` `colorName` | 인스펙터 이름표(8차 결정: 유지) | |
| `EscapeRecipeSO` 옛 필드 7개 + `MaxRequired` | 8차 결정: 에셋 빌더가 채움 — 유지 또는 `[HideInInspector]` | |

---

## 6. 미사용 에셋 — 재조사(2026-11-02, 10차 보정)

> ✅ **조치 완료(같은 날, Request1009Plan.md §47):** 아래 A1~A6·C·L은 `_CleanupBackup/2026-11-02/`로 옮겼고(357항목, 되돌릴 수 있음), S(`Source~` 5곳, 907 MB)는 프로젝트 루트 `ArtSource/`로 옮겼다. 예외로 남긴 것은 셋이다: 빌드 씬이 쓰는 `*_Terrain_Decor` 4개, 대기실이 쓰는 Twisted 소품 3종, `MT_*` 재질. 6.5(큰 FBX)는 그대로다. 아래 표는 조치 전 조사 기록이다.

> **조사 방법.**
> - **뿌리**는 세 가지다.
>   - 빌드 설정에서 켜진 씬 7개.
>   - 코드가 `Resources.Load`로 실제로 부르는 경로 19종. 폴더 통째가 아니다. 9차는 `Resources` 폴더 전체를 뿌리로 잡는 실수를 했다.
>   - ProjectSettings가 참조하는 에셋.
> - **간선**은 YAML GUID 참조와 `.meta`의 외부 재질 연결이다.
> - **ID 수준**도 대조했다.
>   - 사운드·음악 카탈로그 항목: 코드의 `SoundId.X`·`MusicId.X`, 그리고 씬·프리팹에 직렬화된 `sound`·`click`·`hover` 값과 맞춰 봤다.
>   - 아이템: 상자·레시피 배포 목록과 맞춰 봤다.
> - **판의 맵 순서**는 Unity에서 `GameSettings.gameMapScenes`로 확인했다. 순서는 `CandyForest, Test_GingerbreadVillage, ChocolateFactory, CursedCandyCarnival, Test_HauntedBakery`다. **`Game_*` 씬은 하나도 쓰이지 않는다.**
> - 디스크 실측: `Assets` 아래 3 MB 넘는 파일 209개, `~` 폴더, Photon 폴더. 9차는 Photon을 아예 훑지 않았다.
> - 9차 6장 표의 "닿지 않는 365개 · 90.7 MB"는 아래 분류로 대체한다.

### 6.1 A — 빌드에 들어가지만 아무도 쓰지 않음 (빌드 크기에 직접 영향, 우선 정리)

| # | 에셋 | 크기 | 근거 |
|---|---|---:|---|
| A1 | `04. Prefabs/Resources/BrushCursor.prefab` + `Brush.fbx`, `ColorTag/Shaders/BrushCursorMaterial.mat` | 3.8 MB | 옛 손 색칠 붓 커서. 불러오는 코드·참조 0. `Resources` 안이라 빌드에 포함된다 |
| A1 | `Resources/UI/Button/BackArrow.png` | 1 KB | 참조 0 |
| A2 | **Photon 데모의 `Resources` 4곳**: `PhotonChat/Demos/Demo Chat/Resources`(1.7 MB·9개), `PUN/Demos/DemoAsteroids/Resources`(251 KB), `PunBasics-Tutorial/Resources`(60 KB), `DemoSlotRacer/Resources` | 2.0 MB | `Resources` 폴더는 위치와 관계없이 빌드에 들어간다. 데모 폴더 전체(`*/Demos` 3곳, **300개 · 30.9 MB**)는 게임과 무관하다. ⚠️ `PhotonUnityNetworking/Resources`(PhotonServerSettings)와 `PhotonVoice/Resources`는 **필수라 유지** |
| A3 | 사운드 카탈로그 항목 **32개**: `UiSlotSelect`(111)·`UiChat`(114)·Paint 300~303·305·`MonsterDeparted`(403)·`BoardHop`(506)·`BoardSuck`(507)·옛 탈출 장치 케이크 700~705·롤러코스터 710~714·오븐 720~724·기차 730~733·제단 740/741·`PortalClose`(744)·`LanternFlicker`(745) | 6.0 MB | 코드의 `SoundId.X` 0회, 직렬화 값 0회. `SoundCatalog`가 클립을 참조해 빌드에 포함된다. 정리할 때는 **enum 번호는 남기고**, 카탈로그 항목과 wav만 뺀다. 304 `PaintReset`은 버튼 `click: 304`로 쓰여서 제외했다 |
| A4 | 음악 `CandyForestHunt`(11)·`GingerbreadHunt`(13)·`FactoryHunt`(15)·`CarnivalHunt`(17)·`BakeryHunt`(19)·`TimeAttack`(20) | 4.5 MB | `MusicRules.cs:48`이 "쓰지 않지만 ID 번호를 지키려고 남긴다"고 명시한다. 번호는 enum이 지키므로 **카탈로그 클립은 빼도 된다** |
| A5 | 아이템 `Item_JailBalloon` | 작음 | 어떤 상자·레시피에도 없다. 코드 분기 8곳과 함께 정리한다(4.1) |
| A6 | `FinalizeStampMaterial.mat`·`PaintStamp.shader`·`PlayerPaintedSkin.shader`·`DefaultColorPalette.asset` | 작음 | 쿠키 프리팹의 `PlayerPaintCanvas`가 참조해 빌드에 들어간다. 죽은 몸 색칠(5.1)을 지울 때 같이 빠진다 |

**A 합계 ≈ 16.3 MB** (빌드 압축 전 기준).

### 6.2 L — 옛 `Game_` 맵 파이프라인 전용 (판에서 안 쓰임 · 빌드 제외 · 지우려면 에디터 도구도 같이)

`Game_<Map>` 씬은 MapSceneBuilder(맵별 통짜 FBX 배치)와 MapCompactor(결과 메시 굽기)가 만든 **1세대 맵**이다. 지금 판의 맵은 실제 모델 맵 3개와 `Test_` 2개다. 아래는 `Game_` 씬에서만 닿거나, 그 빌더만 쓰는 파일이다.

| MB | 파일 | 내용 |
|---:|---:|---|
| 8.5 | 5 | `Scenes/Maps/Game_*.unity` 5개 |
| 23.0 | 40 | `Maps/<Map>/Compact/` 5맵 — `*_COL_Boundary*`·`*_Terrain_Walkable*` 등 MapCompactor 결과 |
| 17.6 | 39 | `Maps/<Map>/Models/<Map>_<분류>.fbx`(맵별 통짜 FBX — Ground·Decoration·Effects·GameplayProps…), 그리고 `CursedCandyCarnival/Models`의 임시 소켓 재질 |
| 2.8 | 5 | `09. Environment/Escape/Models/ESC_<Map>.fbx` — 옛 탈출 장치(`CoasterExitBuilder`가 `LegacyDevice`로 지우는 대상) |
| 1.3 | 36 | `09. Environment/Twisted/Models` 18 + `04. Prefabs/Twisted` 18 — `TwistedPropPlacer`가 `Game_` 씬에만 놓는다 |
| 0.1 | 17 | `Maps/GingerbreadVillage/Materials/*GingerDim*`, `CursedCandyCarnival/Materials`, `HauntedBakery/Materials`, `Common/Doors`·`Animations`(관람차·마법 오븐) |

**L 합계 ≈ 53 MB · 142개.** 그 원본인 `Maps/Source~`(.blend, 163 MB)도 이 파이프라인용이다.

⚠️ **지우려면 함께 정리할 에디터 코드가 있다.**
- `MapSceneBuilder`, `MapCompactor`, `TwistedPropPlacer`, `GingerbreadNeonDimmer`.
- `TwistedAtmosphere`·`MapPassabilityCheck`·`MapPlaytestDriver`의 `Game_` 분기.
- 시험: `MapCompactTests`, `TwistedTests`, `BuildSceneTests`의 `Game_` 부분.

사용자 규칙("Build Map Scenes·Compact 실행 금지")과도 맞는다. 다만 1세대 맵을 다시 쓸 계획이 있으면 유지해야 하므로 **결정 필요**다.

### 6.3 C — 아무도 쓰지 않음 (빌드 제외 · 에디터 도구도 안 씀 · 삭제 안전)

| MB | 파일 | 에셋 | 확인 내용 |
|---:|---:|---|---|
| 9.2 | 8 | `Animation/Characters/<4종>/*_Metallic.png`·`*_Roughness.png` | §45에서 만들었지만 `CHR_*.mat`이 쓰지 않는다. `CharacterBuilder`는 `_Metallic`을 0으로만 설정한다. 재질에 연결할 계획이 없으면 삭제 |
| 6.9 | 97 | `Maps/Common/Models/*.fbx` | Blender에서 소품을 하나씩 내보낸 것. 맵별 통짜 FBX에 이미 합쳐져 있다. 코드·씬 참조 0(8차 "빌더 입력" 판단 정정 — 경로를 쓰는 코드 없음) |
| 4.7 | 1 | `Maps/ChocolateFactory/Generated/CHO_Bld_Cogworks_Interior_NoBelt.asset` | 예전 생성 메시. 지금 프리팹은 다른 메시를 쓴다. 참조 0 |
| 3.3 | 1 | `Maps/CursedCandyCarnival/Models/GyroDrop/GD_GyroDrop.fbx` | `CarnivalMapAssets.BuildGyroDrop`은 부품 4개(`GD_Platform`·`Tower`·`Ring`·`Crown`)만 불러온다. 통짜 FBX는 참조 0 |
| 2.1 | 1 | `08. Resources/LobbySceneBackGround.png` | 로비는 `LobbySceneBackGround_Twisted.png`를 쓴다(`TwistedAtmosphere.cs:22`). 원본은 참조 0 |
| 0.1 | 5 | `09. Environment/Escape/Models/ITEM_CandyCell_*.fbx` 4 + `08. Resources/ItemIcons/Icon_CandyCell_Orange.png` | 재료 프리팹은 `CF_Lollipop_*.fbx`로 만든다(`CandyForestAssets.cs:229`). FBX는 대체 경로라 쓰이지 않는다 |
| 0.1 | 20 | `07. Expression/얼굴1~5_*.png` | 참조 0. 지금 표정은 다른 방식이다. 파일 이름이 한글이라 경로 문제 위험도 있다 |
| 작음 | 4 | `04. Prefabs/Maps/CandyForest/CF_Lollipop_*.prefab` | 빌더가 만들지만 맵·아이템이 쓰지 않는다(아이템은 `ITEM_CandyCell_*`) |
| 작음 | 5 | `05. Materials/Character/Cookie_BaseSkin_B~D` + 색 텍스처 | `CharacterBuilder`는 `_A`만 쓴다 |
| 작음 | 3 | `ColorTag/Shaders/BrushStampMaterial.mat`·`EraseStampMaterial.mat`·`PaintErase.shader` | 옛 손 색칠 |
| 작음 | 2 | `06. Physics/PlayerNoFriction.physicMaterial`·`_tmp` | 참조 0. 시험 드라이버는 실행 중에 직접 만든다 |
| 작음 | 4 | `_TerrainAutoUpgrade/*.terrainlayer` | Unity가 자동 변환하며 남긴 것. 지형 참조 0 |
| 작음 | 1 | `Maps/HauntedBakery/Materials/M_Glow_Yellow_HauntedBakery.mat` | `Game_HauntedBakery`도 쓰지 않는다 |
| 1.8 | 2 | `Scenes/Maps/Test/Test_ChocolateFactoryBuildings.unity` + `M_CHO_PreviewGround.mat` | §41 건물 확인용 씬. 확인이 끝났다면 삭제 후보(`ChocolateFactoryBuildingsScene` 도구와 함께) |

**C 합계 ≈ 28 MB · 154개.**

목록에서 **제외(오탐)**한 것:
- `TagOfChaos.Scripts.asmdef`: 필수다.
- `09. Environment/Shaders/WaterBubble.shader`: 쓰이지 않는 것이 아니라 **빌드에서 빠지는 버그**다(N-01).
- `SoundSettingsPanel.prefab`: `SoundSettingsInstaller`가 만들고 시험이 검사하는 원본이다.
- `03. SO/Maps/CandyForestLayout.asset`: 캔디숲 빌더의 위치표다.
- 빌드 제외 `Test_CandyForest`·`Test_ChocolateFactory`·`Test_CursedCandyCarnival`: 실제 맵 빌더의 원본 틀이다.

### 6.4 S — `Assets` 안의 원본·작업 파일 (Unity가 가져오지 않음 · 빌드 영향 0 · 저장소·디스크만 차지)

| 크기 | 위치 | 내용 |
|---:|---|---|
| **430 MB** | `09. Environment/CandyForest/Source~` (178개) | 캔디숲 실제 모델 원본(.blend 분할본 + 중간 FBX) |
| **190 MB** | `Maps/CursedCandyCarnival/Source~` (89개) | 놀이공원 놀이기구 원본(Horse·Entrance·Viking.blend…) |
| **163 MB** | `Maps/Source~` (88개) | 1세대 `Game_` 맵 .blend — 6.2 L의 원본 |
| **126 MB** | `10. Audio/Source~` (528개) | 소리 원본·생성 스크립트 |
| 0.3 MB | `Maps/*`·`09. Environment/*` 안의 `.json`·`README.md` 80개 | Blender 내보내기 메타·설명 |

`~`로 끝나는 폴더는 Unity가 무시하므로 게임에는 영향이 없다. 합계 약 **909 MB**는 `Assets` 밖(예: 프로젝트 루트 `ArtSource/`)으로 옮기는 것을 권장한다. 그러면 백업·복제가 가벼워진다. `Maps/Source~`는 L을 정리할 때 함께 결정한다.

### 6.5 크기 규칙 위반 (미사용은 아님)

- **FBX 100 MB 규칙 위반 2건**:
  - `Maps/ChocolateFactory/Models/Buildings/CHO_Bld_MacaronWheel.fbx` **133 MB**.
  - `CHO_Bld_Gear.fbx` **115 MB**.
  - 생성 메시 `Generated/CHO_Bld_MacaronWheel_Exterior.asset`도 **101 MB**다.
- 프리팹은 FBX에서 문·내부·소켓만 쓰고, 외벽은 생성 메시(**정점 77만 개**)를 쓴다. FBX 안의 원래 외벽 메시는 빌드에 들어가지 않는다. 그래서 디스크 규칙 문제이면서 동시에 **실행 성능 위험**(외벽 한 채가 정점 77만 개)이다.
- 같은 폴더의 다른 건물 FBX도 63~84 MB로 한계에 가깝다.

### 6.6 코드 쪽 재확인

- 런타임 `MonoBehaviour` 중 빌드 씬·프리팹에 붙어 있지 않은 46종을 하나씩 확인했다. **모두** 코드가 `AddComponent`하거나 `[RuntimeInitializeOnLoadMethod]`로 만든다. **붙일 곳 없는 죽은 컴포넌트는 0개**다.
- 죽은 코드는 5장 그대로다: 몸 색칠 캔버스, 감옥 풍선, 옛 레시피 필드, 작은 멤버 약 25개.

### 6.7 정리 순서 제안 (모두 사용자 확인 후 진행 — 삭제는 되돌리기 어려움)

1. **A1·A2·C**: 참조 0이 확인된 것. 지워도 동작이 바뀌지 않는다. A2는 `Demos` 폴더째 지운다. 약 32 MB(빌드 2 MB+3.8 MB 감소).
2. **A3·A4**: 카탈로그 항목과 wav/ogg만 뺀다. enum은 유지한다. 빌드 10.5 MB 감소.
3. **A5·A6**: 5.1 죽은 코드 정리와 한 묶음으로 한다. 코드 변경이 있으므로 시험으로 확인한다.
4. **L**: 1세대 맵을 다시 쓸지 결정한 다음, 에디터 도구·시험과 함께 정리한다.
5. **S**: `Source~` 4곳을 `Assets` 밖으로 옮긴다.
6. **6.5**: 큰 건물 FBX를 다시 내보내고(분할·감량), 외벽 생성 메시를 LOD로 낮춘다.

---

## 7. 발견한 버그·위험

| # | 등급 | 내용 | 근거 | 권장 |
|---|---|---|---|---|
| N-01 | 🐞 **버그** | 물풍선 물방울 셰이더가 빌드에 없다. `Shader.Find("TagOfChaos/WaterBubble")`는 셰이더가 재질·씬·`Resources`에서 참조되거나 Graphics Settings "항상 포함"에 있어야 빌드에 들어간다. 지금은 둘 다 아니어서 빌드에서는 `EscapeVisuals.NewMaterial`의 대체(투명 하늘색 Standard)로 보인다 | `WaterBubbleView.cs:8,22`, 의존성 그래프에서 닿지 않음, `GraphicsSettings.m_AlwaysIncludedShaders`는 내장 6개뿐 | `Resources/`에 `WaterBubble.mat`을 두고 `Resources.Load`로 쓰거나, 셰이더를 직렬화 필드로 받기(K-01) |
| N-09 | ⚠️ 위험 | 메시지 큐를 켜는 곳이 5곳 — 씬 로드 중 PUN의 일시정지와 겹치면 사라질 씬에서 메시지를 처리할 수 있다 | 4.8 | 큐 제어 단일화(K-04) |
| N-06 | ⚠️ 위험 | 위치 기록 두 벌, 액터 키 쪽은 한 사람이 캐릭터 둘이면 섞여 이동 검사·스태미나 추정이 엉뚱한 경고를 낸다(실제 게임에선 1인 1캐릭터라 시험에서만) | 4.11 | ViewID 키 하나로(K-05) |
| N-10 | ⚠️ 위험 | `ToolUser`와 `WitchRevealCamera`가 같은 카메라의 FOV를 각자 저장·복원 — 8차 A-12 그대로 | `ToolUser.cs:47-49,102`, `WitchRevealCamera.cs:292-293,349` | FOV 소유자 하나(K-11) |
| N-11 | 낮음 | `GameStartAuthority`의 정적 `lastStartTime`·`lastMap`이 방을 나가도 남는다 | 4.6 | 방을 나갈 때 초기화 |
| N-12 | 낮음 | 쿠키마다 쓰지 않는 512² 색칠 RenderTexture(약 1 MB) + 합성 셰이더 | 5.1 | 색칠 제거 시 함께 |

---

## 8. 권장 조치 (우선순위)

| 순위 | 번호 | 조치 | 효과 | 위험 |
|---|---|---|---|---|
| 1 ✅ | K-01 | **물방울 셰이더를 빌드에 포함** — §47 CL3: GraphicsSettings "항상 포함"에 추가, `BuildSceneTests.RuntimeFoundShaders_AreAlwaysIncluded`(재질을 `Resources`에 두거나 직렬화 참조) | 빌드에서 §45 물방울이 의도대로 보임 | 매우 낮음 |
| 2 | K-04 | 메시지 큐 켜기를 `RoomSceneFollower` 한 곳으로(`GameManager`·`PlayerSpawner`의 무조건 켜기 제거) | 씬 이동 안정성 | 낮음 — 씬 이동 Play Mode 시험 필요 |
| 3 | K-05 | 위치 기록 통합(`PositionHistory`를 모든 클라이언트·ViewID 키, `lastPositions`는 그 마지막 값) | 중복 제거, 시험 거짓 경고 제거 | 중간 — 이동 검사·드롭 위치 시험 |
| 4 ✅ | K-02 | 죽은 몸 색칠·감옥 풍선 정리(5.1) + 6.1 A5·A6 — §47 CL4 완료 | 약 300줄·셰이더 3·RT 1장/쿠키 감소 | 낮음 — 쿠키 스킨이 합성 셰이더를 쓰므로 원래 재질로 되돌리는 확인 필요 |
| 5 | K-03 | 작은 미사용 멤버 정리(5.2, 이번 작업에서 생긴 6개 포함) | 읽기 쉬움 | 매우 낮음 |
| 6 | K-06 | 처형 연출 공용화(D-09) + `Flat` 공용화(D-10) + 캐릭터 찾기(D-01) | 중복 제거 | 낮음 |
| 7 | K-07 | 큰 클래스 분리(N-02·N-03·N-04) — 동작 변화 없는 이동만 | 유지보수 | 중간 — 회귀 시험 넓게 |
| 8 | K-08~K-13 | 8차 A-08~A-11 유지 + SO 로더 제네릭(K-13) + FOV 소유자(K-11) | 구조 | 낮음~중간 |
| ✅ §47 | K-14 | 6.1 A1·A2(붓 커서·`BackArrow`·Photon `Demos` 3폴더) + 6.3 C 전체 삭제 | 빌드 −5.8 MB, 저장소 약 −60 MB | 매우 낮음(참조 0 확인) — **사용자 확인 후** |
| ✅ §47 | K-15 | 6.1 A3·A4 — 안 쓰는 사운드 32개·음악 6곡을 카탈로그에서 빼고 파일 삭제(enum 번호 유지) | 빌드 −10.5 MB | 낮음 — 카탈로그 시험 |
| ✅ §47 | K-16 | 6.2 L — 옛 `Game_` 맵 파이프라인(씬 5·Compact·통짜 FBX·Twisted 소품 + 에디터 도구·시험) 정리 | 저장소 −53 MB(+ `Maps/Source~` 163 MB) | 중간 — 1세대 맵을 다시 쓸 계획이 없을 때만. **사용자 결정** |
| ✅ §47 | K-17 | 6.4 S — `Source~` 4곳(약 909 MB)을 `Assets` 밖으로 이동 | 프로젝트 가벼워짐 | 매우 낮음(Unity가 무시하는 폴더) |
| 결정 | K-18 | 6.5 — `CHO_Bld_MacaronWheel`(133 MB)·`CHO_Bld_Gear`(115 MB) FBX 재내보내기, 외벽 생성 메시(정점 77만) 감량 | 규칙 준수·실행 성능 | 중간 — 원본 Blender 작업 필요 |

---

## 9. 부록 — 조사 방법·수치·한계

| 항목 | 값 |
|---|---|
| 런타임 코드 | 221파일 · 25,352줄(`Assets/02. Scripts`) |
| 에디터·시험 | 83파일 · 약 23,200줄(`Assets/Editor`) |
| YAML 에셋 스캔 | 1,644파일 · 913 MB(Photon 제외), `.meta` 외부 참조 9,544개. 10차 보정에서 Photon 647파일(69.9 MB)·`~` 폴더 4곳(약 909 MB)·3 MB 넘는 파일 209개 실측 추가 |
| GUID 표 | 2,926개 |
| 빌드 씬 | 켜짐 7 / 꺼짐 3 |
| 닿는 에셋 | 2,279(10차 보정 뿌리 기준) |
| 미사용 분류(10차 보정) | A 빌드 포함 미사용 ≈ 16.3 MB · L 옛 `Game_` 전용 142개 ≈ 53 MB · C 고아 154개 ≈ 28 MB · S 원본 ≈ 909 MB · 작업 파일 80개 0.3 MB |
| `AddComponent` | 89 |
| `GameObject.Find` / `FindObjectsByType` / `Camera.main` / 이름 찾기 | 7 / 11 / 21 / 29 |
| 정적 가변 필드 | 73 |
| `[PunRPC]` / `RaiseEvent` / `AuthorityProps.Set*` / `IsMasterClient` | 6 / 13 / 24 / 48(24파일) |

**한계**
- 의존성 그래프는 직렬화 참조만 본다. 코드의 문자열 로드(`Resources.Load`는 뿌리에 포함), `Shader.Find`, 이름 찾기는 따로 확인했다. Addressables는 쓰지 않는다.
- 10차 보정 전에는 `Resources` 폴더 전체를 뿌리로 잡아, 그 안의 안 쓰는 파일(붓 커서 등)을 놓쳤다. 지금은 코드의 `Resources.Load` 경로 19종만 뿌리로 잡는다. `Resources`의 하위 폴더를 문자열로 조합해 부르는 코드가 새로 생기면 이 목록도 고쳐야 한다.
- "닿지 않음"은 "빌드에 안 들어감"이다. 에디터 빌더(`MapSceneBuilder` 등)의 입력일 수 있어 삭제 판단은 사용자 몫이다.
- 미사용 멤버는 이름 기반이라 같은 이름이 다른 곳에 있으면 놓칠 수 있다(거짓 음성). 거짓 양성은 직접 열어 걸렀다.
- 런타임 동작(성능 계측·메모리 프로파일)은 이번 범위가 아니다.
