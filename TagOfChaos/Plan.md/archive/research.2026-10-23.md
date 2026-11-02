# 조사 보고서: TagOfChaos 스크립트 전수 해설 · 구조 점검 · 미사용 코드/에셋 (2026-10-23, 8차)

> **이 문서에 대해**
> - 7차 판(2026-10-12, 1,523줄 — 5·6·7차 감사, `R4.x`·`R5-x`·`§x`·`F-xx` 번호 체계)은 **`Plan.md/archive/research.2026-10-12.md`에 한 글자도 바꾸지 않고 옮겨 두었다.** 코드 주석 50개 파일이 `research.md R5-21`, `R4.7-9`, `§12.4` 같은 번호를 가리키는데, 그 번호는 모두 보관본에서 찾는다(이 문서는 새 번호 `A-xx`·`U-xx`·`D-xx`를 쓴다).
> - 범위: `Assets/02. Scripts` 런타임 스크립트 **206개 파일 · 24,531줄**(Legacy~ 제외)을 모두 읽었다. 참조 확인을 위해 `Assets/Editor`(시험·빌더)와 `Assets/03. SO`도 함께 보았다. 에셋은 Unity 에디터에서 **빌드 씬 + Resources를 뿌리로 한 의존성 그래프**를 직접 계산했다.
> - 코드·에셋은 **하나도 고치지 않았다**(조사만). 권장 조치는 8장에 우선순위로 적었고, 실제 정리는 사용자 결정 뒤 별도 계획서로 진행한다.
> - 표기: ✅ 문제 없음 / ⚠️ 개선 권장 / ❌ 문제 / 💀 죽은 코드(실행되지 않음) / 🧪 시험·에디터에서만 씀.


## 0. 처리 결과 (2026-10-23 — Request1009Plan.md §38)

> 아래 2~9장은 조사 당시 상태다. 그중 A·B·D를 정리했고, 처리한 항목은 이 표가 현재 상태다. 옮긴 파일은 모두 프로젝트 밖 `_CleanupBackup/2026-10-23/`에 원래 경로 그대로 있다(.meta 포함). 확인이 끝나면 지워도 된다.

| 묶음 | 결과 |
|---|---|
| A 죽은 코드 | NetHold(대기실 대기)·손 색칠·마녀 처단(Purge)·작은 멤버 약 30개·`Legacy~` 4폴더·`MonsterTestSpawner` 정리. `PlayerPaintCanvas` 649 → 약 200줄. 네트워크 번호(`NetEventCodes` 3·13, `WitchMode` 2, `EscapeNoticeKind` 2)는 비워 둠 |
| B 씬 | 맵 7개의 꺼진 `PointLights`, 놀이공원 빈 `COL_0` 342개·꺼진 물 프로브 2개, 대기실 `MonsterWaitPanel` 삭제(놀이공원 4,404 → 3,948개). 빌더가 빈 `COL`을 다시 만들지 않게 수정 |
| D 고아 에셋 | 77개(약 54 MB) 이동: 쿠키 애니메이션 FBX 3·백업 anim 2, `VK_Viking.fbx`, `CF_Tile_Grass`·`Ground_Floor_Tile` 프리팹+FBX, 재질 6, `SampleScene/NavMesh.asset`, 스크린샷 57장. `02. Scripts/Player.md` → `Plan.md/archive/Player.md`. 빌드 의존성 1,776개 그대로 |
| 검증 | 컴파일 오류 0 · EditMode 320/320 · 통행 5/5 · Play Mode(물풍선 칠하기·마녀 타임어택·결과·대기실 문·걷기) Console 0건 |
| 남긴 것 | 7.1의 빌더 입력(`Maps/Common` 97 FBX, `*_Effects.fbx`(MapSceneBuilder가 분류로 읽음), `FL_*`, `GD_GyroDrop`, `ITEM_CandyCell_*` 등), `Game_`·`Original/` 씬(C — 결정 대기), `Test_CandyForest`·`Test_CursedCandyCarnival`(실제 맵 원본 틀), 표정 그림·`LobbySceneBackGround.png`(사용 계획 확인 필요), `SoundSettingsPanel.prefab`(설치 도구가 경로로 씀), `EscapeRecipeSO` 옛 필드(에셋 만드는 도구가 채움), `ColorEntry.colorName`(인스펙터 이름표), `SoundId`·`MusicId` 값 |

---

## 목차

1. 한눈에 보는 결론
2. 게임 전체 구조와 실행 흐름
3. 핵심 기법 상세 해설 (Camera_Ctrl 구면좌표 → 이동 물리 → 네트워크 시계 → 방장 권위 → 소리 → 수학 도구)
4. 폴더별 클래스·변수·메서드 해설 (13개 폴더)
5. 구조 점검 12항목
6. 미사용 코드 (클래스·메서드·필드·변수·열거값·문구)
7. 미사용 에셋 (FBX·Prefab·씬·씬 오브젝트·기타 파일)
8. 권장 조치 (우선순위) 와 사용자 결정이 필요한 것
9. 부록 — 조사 방법·수치·한계

---

## 1. 한눈에 보는 결론

| 영역 | 판정 | 한 줄 요약 |
|---|---|---|
| 네트워크 권위(Photon) | ✅ 우수 | 게임 상태는 **방장 한 명만 쓰는 Room Prop**(`EscapeState` 이진 스냅숏 + `Revision`)이고, 요청은 `RaiseEvent`로 방장에게만 간다. 받는 쪽은 **보낸 사람(`photonEvent.Sender` / `info.Sender`)·거리**로 검증한다. 조작된 속성은 `RoomPropertyGuard`가 되돌린다. Ownership 이전(`TransferOwnership`)은 쓰지 않는다 — 구조와 맞다. |
| 결정적 연출 | ✅ 우수 | 롤러코스터·바이킹·로켓·마녀·AI 목소리가 모두 **`PhotonNetwork.Time` + 방 속성만으로 계산**되어, 위치를 보내지 않아도 모든 화면이 같은 장면을 본다(늦게 들어온 사람도). |
| 죽은 코드 | ❌ 많음 | 규칙 개편(§25·§26)으로 **손 색칠·괴물 대기실 대기(NetHold)·마녀 처단(Purge)** 세 갈래가 통째로 실행되지 않는데 코드가 남아 있다 — 약 **1,100줄 이상**(6장). |
| 미사용 에셋 | ❌ 많음 | 빌드(빌드 씬 + Resources)에서 닿지 않는 프로젝트 FBX **177개(61 MB)**, Prefab **25개**, 씬 **12개(옛 `Game_` 5 · `Original/` 5(24 MB) · 빌드 제외 `Test_` 2(실제 맵의 원본 틀이라 유지), 합 38 MB)**, 스크린샷 **57장(14 MB)**, 쓰지 않는 쿠키 애니메이션 FBX 3개(20 MB). 단 그중 상당수는 **에디터 빌더의 입력**이라 "지워도 되는 것"과 "빌드에 안 들어갈 뿐인 원본"을 나눠 적었다(7장). |
| 씬 안 불필요 오브젝트 | ⚠️ | 모든 맵에 **꺼진 점광원 묶음 `MapLighting/PointLights`(72~394개)**, 놀이공원 울타리 아래 **빈 `COL` 오브젝트 342개**, 대기실의 옛 **`MonsterWaitPanel`**, 지운 물 위의 **꺼진 반사 프로브 2개**(7.4절). |
| Singleton | ⚠️ 약간 많음 | 정적 인스턴스 16곳(DDOL 4 · 씬 싱글턴 7 · `Local` 2 · SO 캐시 3). 대부분 "자기 부트스트랩 + 파괴 시 해제"가 정확하지만 **`EscapeManager.Instance`가 8개 파일, `EscapeManager` 정적 멤버가 38개 파일**에서 쓰여 허브가 되었다(5.3·5.6). |
| Prefab ↔ Script 역할 | ⚠️ | 런타임 `AddComponent` **75곳(32개 파일)**. 쿠키 프리팹에는 보이지 않는 컴포넌트 8개가 실행 중에 붙는다 — 인스펙터로 볼 수 없고 프리팹 수정이 반영되지 않는다(5.4). |
| Scene 직접 의존 | ⚠️ | `GameObject.Find` 7곳·`FindFirstObjectByType` 7곳·`Camera.main` 20곳·이름 규칙(`Repair_Point`, `Slot_00_Empty`, `Ground`, `Map`) 다수(5.5). |
| Event 구독/해제 | ✅ | `+=`/`-=` 짝을 전수 확인 — 해제가 빠진 것은 **같은 오브젝트 수명**(EscMenu·LobbyEscMenu)과 **죽은 NetHold** 뿐. Awake 구독·OnDisable 해제 같은 비대칭은 0건(5.9). |
| Object Pool ↔ Instantiate | ⚠️ | 소리(보이스 풀)·맵 박살 조각·바닥 아이템은 풀을 쓰지만, 짧은 효과(도구 명중·풍선·경보등·소음폭탄)는 `CreatePrimitive`/`Destroy`를 매번 한다 — 충돌은 없으나 방식이 섞였다. **`BreakVfx` 가루 파티클 인스턴스는 코드로 지우지 않는다**(프리팹 Stop Action 의존)(5.10). |
| 중복 로직 | ⚠️ | "액터 번호로 캐릭터 찾기"·"내 캐릭터 찾기" 루프 **9곳**, ESC 키 직접 읽기 **6곳**, 코드로 UI 만드는 도우미 **6벌**, 레지스트리 2벌, 애니메이션 상태 전환 2벌(5.12). |
| ScriptableObject | ⚠️ | SO는 데이터 + 정적 `Current` 캐시로 일관되게 쓰인다. 다만 **옛 규칙 필드가 에셋 호환 때문에 남아**(`EscapeRecipeSO` 7개, `EscapeCatalogSO` 3개, `GameSettingsSO` 색칠 5개), SO 안에 런타임 정적 캐시가 있다(5.7). |
| Unity 수명주기 | ✅ | `[DefaultExecutionOrder]` 2곳은 의도가 분명(들린 몸 → 카메라 순서). "Awake에서 PhotonView 콜백 받기 전 준비" 문제를 이미 고쳤다. 남은 주의점은 `ToolUser`와 `WitchRevealCamera`가 **같은 프레임에 카메라 FOV를 둘 다 쓴다**는 것 하나(5.8). |

---

## 2. 게임 전체 구조와 실행 흐름

### 2.1 폴더 규모

| 폴더 | 파일 | 줄 | 역할 |
|---|---:|---:|---|
| `Escape/` | 53 | 8,258 | 탈출 모드 전부 — 상태·방장 규칙·건물·상자·로켓·아이템·도구·AI·마녀 연출·HUD |
| `Audio/` | 26 | 2,829 | 소리 엔진(보이스 풀·거리 감쇠·실내 차단)·음악·추격 북·출력 장치(Windows COM) |
| `Core/` | 31 | 2,336 | 네트워크 키·권위 속성·방 상태 읽기·입력·조작키·음성 채팅·공용 도구 |
| `Monster/` | 21 | 2,129 | 괴물 조작·잡기 판정·게임 규칙(방장)·결과 화면·관전·방장 정책 |
| `Lobby/` | 13 | 1,511 | 로비(방 목록·지역)·대기실(플레이어 목록·강퇴·방 설정·게임 시작) |
| `Unit/` | 11 | 1,247 | 쿠키 조작·애니메이션·들기(Carry)·스킨·이름표 |
| `ColorTag/` | 4 | 1,057 | 몸 색칠 캔버스(지금은 물풍선 색만)·쿠키 생사 표시 |
| `Environment/` | 9 | 940 | 문·롤러코스터·바이킹·가마솥·깜빡이는 빛 |
| `Camera/` | 6 | 913 | 3인칭 궤도 카메라·괴물 1인칭·마녀 연출 카메라·조준점 |
| `GameManager/` | 10 | 687 | 채팅·스폰·방 나가기·ESC 메뉴·낙하 복구 |
| `UI/` | 4 | 467 | 설정 창(조작키·입력 장치)·로비 ESC |
| `Interaction/` | 5 | 370 | E 상호작용(가까운 대상 찾기·길게 누르기·안내 UI) |
| `Dev/` | 2 | 84 | 오프라인 시험 도구 |
| **합계** | **206** | **24,531** | (+ `Legacy~` 11개 파일 — Unity가 컴파일하지 않음) |

### 2.2 씬 흐름

```
LobbyScene ──(방 만들기/참가, LobbyController)──▶ GameLobbyScene(대기실)
   ▲                                              │ 호스트 "시작" → GameStartAuthority(방장이 실행)
   │                                              │   PrepareEndTime·맵·스파이·캐릭터 배정 기록
   │                                              │   OpRemoveCompleteCache → PhotonNetwork.LoadLevel
   │                                              ▼
   │                                   맵 씬(CandyForest / CursedCandyCarnival / Test_*)
   │                                     GameSceneCore 프리팹(규칙·결과·관전·HUD 뿌리)
   │                                     PlayerSpawner/MonsterJoinController → PhotonNetwork.Instantiate
   │                                     EscapeManager(상태 보기) + EscapeAuthority(방장만, 규칙)
   │                                     GameRuleController(방장) → GameResult 기록
   │                                              ▼
   └──(RoomExitController 나가기)◀── ResultScreenController(12초 뒤 대기실로, RoundStateResetter가 판 키 초기화)
```

### 2.3 권위 모델 (누가 무엇을 쓰는가)

| 데이터 | 쓰는 사람 | 통로 | 검증 |
|---|---|---|---|
| 판 상태(아이템·상자·건물·로켓·탈출·폭탄·AI) | **방장** | Room Prop `EscapeState`(이진, `Revision` 증가) | 클라이언트는 `EscapeRequest` 이벤트로 **요청만** → `EscapeAuthority.Handle`이 거리·역할·소지품 확인 |
| 단계 시각(`PrepareEndTime`·`WitchAppearAt`·`TimeAttackEndTime`…) | 방장 | `AuthorityProps.SetRoom` | `RoomPropertyGuard`가 방장 아닌 사람이 쓴 키를 되돌림 |
| 괴물 목록·잡은 수·사망 원인 | 방장 | 같은 경로 | `MonsterAssignmentAuthority`가 보낸 사람·가마솥 거리 확인, `GrabKillAuthority.TryConfirm` |
| 내 위치·애니메이션 | 본인 | `PhotonView` + `NetworkTransformSync<T>` | 방장이 0.2초마다 위치 기록, 너무 빠른 이동은 "의심" 처리(`EscapeAuthority.TrackPositions`) |
| 내 선택 칸·녹음 여부·스킨 | 본인 | Player Prop (`Writer.Self`) | 키별 쓰는 사람 정책 `NetKeys.Scopes` |
| 들기(Carry) | 드는 쪽 → 들리는 쪽 | `PhotonView.RPC`(대상 소유자에게만) | 들리는 쪽이 `info.Sender`·거리·이미 들림 여부로 거절 가능 |
| 채팅 | 각자 | `RPC LogMsg` | 받는 쪽에서 `ChatText.Sanitize`(리치텍스트 차단)·`ChatRateLimiter` |

---

## 3. 핵심 기법 상세 해설

### 3.1 `Camera_Ctrl` — 직교좌표계 ↔ 구면좌표계 변환 (3인칭 궤도 카메라)

**파일**: `Assets/02. Scripts/Camera/Camera_Ctrl.cs` (158줄). 쿠키·괴물(3인칭 모드)·관전이 **모두 이 한 컴포넌트**로 카메라를 쓴다(`SetFollowTarget`).

#### (1) 원리 — 카메라 위치를 "구 위의 한 점"으로 본다

카메라는 대상의 눈높이 점 **T**(= 캐릭터 발 위치 + (0, `m_TargetHeight`, 0))를 중심으로 하는 **반지름 d의 구 위**에 있다. 구 위의 점은 직교좌표 (x, y, z) 세 개 대신 **구면좌표 (d, θ, φ)** 로 표현하면 조작이 쉽다.

| 구면좌표 | 코드 변수 | 의미 | 입력 |
|---|---|---|---|
| 반지름 d | `CurrentDistance` (= `m_Distance` > 0 이면 그것, 아니면 `m_DefaultDist` 3.2) | 대상과의 거리 | 고정(휠 줌은 삭제됨) |
| 방위각 θ (yaw) | `m_RotH` | 수평으로 몇 도 돌았나 | 우클릭 드래그 x × `hSpeed`(5.0) |
| 고도각 φ (pitch) | `m_RotV` | 위에서 내려다보는 각 | 우클릭 드래그 y × `vSpeed`(2.4), **[-7°, 80°]로 제한** |

#### (2) 변환식 — 코드 한 줄이 곧 구면→직교 변환

`Camera_Ctrl.cs:121, 129, 131`

```csharp
m_TargetRotation = Quaternion.Euler(m_RotV, m_RotH, 0.0f);   // (φ, θ)로 회전 만들기
m_BasicPos.z = -CurrentDistance;                            // 기준 벡터 (0, 0, -d)
m_BuffPos = m_TargetPos + (m_CurrentRotation * m_BasicPos); // 회전한 벡터를 T에 더함
```

`Quaternion.Euler(φ, θ, 0)`은 Unity 규칙대로 **Z → X → Y 순서**(실제로는 X로 φ만큼 숙인 뒤 Y로 θ만큼 돌림)다. 기준 벡터 **v₀ = (0, 0, −d)** (대상 "뒤쪽" d m)에 적용하면:

1. X축으로 φ 회전: (0, 0, −d) → (0, **d·sinφ**, −d·cosφ)  
   (Unity는 왼손 좌표계라 X축 양의 회전이 앞(+z)을 아래로 숙인다. 그래서 뒤쪽 벡터 −z는 **위로** 올라간다 = 내려다보는 카메라.)
2. Y축으로 θ 회전: (x, y, z) → (x·cosθ + z·sinθ, y, −x·sinθ + z·cosθ)  
   x = 0, z = −d·cosφ 를 넣으면 → (**−d·cosφ·sinθ**, d·sinφ, **−d·cosφ·cosθ**)

따라서 카메라 위치는

```
P = T + ( −d·cosφ·sinθ ,  d·sinφ ,  −d·cosφ·cosθ )
```

이는 교과서의 구면좌표 공식 (r sinϑ cosϕ, r sinϑ sinϕ, r cosϑ)을 **Unity 축(Y가 위)·"뒤에서 본다"(부호 −)·고도각 φ = 90° − 극각** 으로 바꿔 쓴 것과 같다. 수평 반지름이 d·cosφ, 높이가 d·sinφ — 고도각이 커질수록 카메라가 위로 올라가며 수평으로는 가까워진다(φ = 80°면 거의 머리 위).

그 뒤 `transform.LookAt(m_TargetPos)`(`:133`)로 시선을 T에 맞춘다. 위치가 구 위에 있고 시선이 중심을 향하므로 "대상을 중심으로 도는" 카메라가 된다.

#### (3) 보조 기법

| 기법 | 위치 | 설명 |
|---|---|---|
| **각도 정규화 후 제한** | `ClampAngle` `:153-157` | `Mathf.DeltaAngle(0, angle)`로 (−180, 180]로 접은 뒤 `Clamp`. 350°가 −10°로 바뀌어 아래 한계(−7°)에 정확히 걸린다(그냥 Clamp하면 350이 80으로 튐). |
| **지수형 회전 보간** | `:122-123` | `Slerp(current, target, clamp(dt / 0.08))` — 매 프레임 남은 각도의 (dt/0.08)만큼 다가간다. 60fps면 약 21%씩 → 시정수 약 0.08초의 부드러운 따라가기. 각도(오일러)가 아니라 **쿼터니언을 Slerp**해서 짐벌락·360° 경계 튐이 없다. |
| **거리 고정** | `:129` | 휠 줌은 삭제(휠은 지금 인벤토리 칸 고르기). 대상별 거리만 `SetFollowTarget(…, distance)`로 바꾼다(괴물은 체격에 맞춰 더 멀리). |
| **흔들림은 마지막에 더하기** | `:134-136` | 정해진 위치를 `m_RestPos`로 저장하고 `CameraShake.Offset`을 **그 위에 더한다** — 다음 프레임 계산이 흔들린 위치에서 시작하지 않아 흔들림이 누적되지 않는다. 대상이 없을 때도(`:101-105`) 마지막 자리에서 흔들린다(마녀가 모두 쓰러뜨린 뒤의 관전). |
| **커서 잠금은 드래그 중에만** | `UpdateCursorLock` `:141-145` | 우클릭을 누르는 순간 `Locked`, 떼는 순간 `None`. 컴포넌트가 꺼지면(`OnDisable`) 반드시 해제. |
| **초기화 순서 독립** | `Start`·`SetFollowTarget` | `InitCamera`가 Start 전/후 어느 쪽에 불려도 `ResetToDefaultView()`가 대상이 있을 때만 실행 — 순서에 기대지 않는다. |
| **소리 듣는 위치 공유** | `FocusPoint` `:41` | 카메라가 바라보는 점을 `AudioListenerAnchor`가 "귀 위치"로 쓴다(소리가 카메라가 아니라 캐릭터 기준으로 들림). |

#### (4) 변수·메서드 표

| 이름 | 종류 | 역할 |
|---|---|---|
| `m_Player` | `[SerializeField] GameObject` | 따라갈 대상(쿠키·괴물·관전 대상) |
| `m_TargetPos` | `Vector3` | 이번 프레임 구의 중심 T |
| `m_RotH` / `m_RotV` | `float` | 방위각 θ / 고도각 φ (도) |
| `hSpeed` 5.0 / `vSpeed` 2.4 | `float` | 마우스 감도(가로가 더 빠름) |
| `vMinLimit` −7 / `vMaxLimit` 80 | `float` | 고도각 범위(땅 아래로 파고들지 않게 −7, 머리 위 짐벌 근처 80) |
| `m_DefaultRotH` 0 / `m_DefaultRotV` 25 | `float` | 초기 각도(정면 뒤 25° 위) — 값이 바뀌지 않으므로 `const`가 맞다 ⚠️ |
| `m_DefaultDist` 3.2 | `[SerializeField] float` | 기본 거리 |
| `m_CurrentRotation` / `m_TargetRotation` | `Quaternion` | 보간 중인 회전 / 목표 회전 |
| `m_BasicPos` | `Vector3` | 기준 벡터 (0, 0, −d) |
| `m_BuffPos` | `Vector3` | 계산된 카메라 위치 P |
| `rotationSmoothTime` 0.08 | `float` | 회전 보간 시정수 |
| `m_TargetHeight` / `m_Distance` | `float` | 대상별 눈높이·거리(괴물이 바꿈) |
| `CookieTargetHeight` | `const` 1.5 | 쿠키 눈높이(다른 클래스가 참조) |
| `m_RestPos` / `m_HasRestPos` | `Vector3`/`bool` | 흔들림을 뺀 위치 |
| `InitCamera(…)` | 메서드 | 처음 붙일 때 — 기본 각도로 초기화 |
| `SetFollowTarget(target, h, d, keepRotation)` | 메서드 | 대상만 바꾸기(관전 순환 시 `keepRotation=true`로 각도 유지) |
| `ResetToDefaultView()` | 메서드 | 각도·위치를 기본값으로 즉시 맞춤 |
| `LateUpdate()` | 메서드 | 캐릭터가 Update/FixedUpdate로 움직인 **뒤** 카메라 계산(떨림 방지) |
| `ClampAngle(…)` | 메서드 | 위 (3) 참조 |

**문제점**: ⚠️ 주석이 오래됨 — `:16`, `:20`, `:125-127`이 "휠은 붓 크기 조절 전용", `PlayerPaintCanvas.HandleBrushSizeInput()`을 말하지만 손 색칠은 삭제되었고 휠은 `PlayerInventory`의 칸 고르기다. `:140` 주석에 깨진 글자 **"풀어 둬EEAAAAAAAA야"**. 감도·한계값이 하드코딩(설정 SO나 `KeyBindings` 감도 옵션으로 옮길 만함).

### 3.2 다른 카메라들

| 클래스 | 기법 |
|---|---|
| `MonsterFirstPersonCamera` | 요/피치 1인칭. 잡기 처형 때는 `killBlend`를 `MoveTowards`로 0→1 올리며 `SmoothStep` 곡선으로 **눈 위치 → 잡는 손(소켓) 쪽 시점**으로 Slerp. 근접 클립 0.1 m로 바꿔 몸 안이 안 보이게. |
| `MonsterViewSwitcher` | 괴물 1인칭/3인칭 전환. 1인칭일 때 자기 몸 렌더러를 `ShadowsOnly`로(그림자는 남김). `MonsterController`가 실행 중 `AddComponent`. |
| `WitchRevealCamera` | `[DefaultExecutionOrder(1000)]` — 다른 모든 카메라 계산 **뒤에** 실행되어 "각자 시점"과 "연출 시점"을 가중치 w로 섞는다(`BlendPose`). 장면(`WitchShot`)은 `WitchTimeline.ShotAt(now, …)` 순수 함수가 공통 시계로 정하므로 모든 화면이 같은 장면. 포즈 함수(`CloseUpPose`·`LeadPose`·`FullBodyPose`·`LowAnglePose`·`StompPose`·`EndPose`)는 전부 **정적 순수 함수**라 EditMode 시험 가능. |
| ↳ `FramePose` `:105-112` | 두 점 a, b를 한 화면에 담는 회전·시야각: 시선 = 정규화한 두 방향의 **합(이등분선)**, FOV = 두 방향 사이 각 + 여유(`margin`), [최소, 최대]로 제한. |
| ↳ 가림막 숨기기 | `OccluderCandidates`가 카메라→마녀 얼굴 선분과 겹치는 렌더러를 `Bounds.IntersectRay`로 골라 잠시 끈다. `LineBlocked`는 `RaycastNonAlloc`. |
| `GrabAimReticle` | 괴물 조준 대상 머리 위 표시. `WorldToScreenPoint`, UI는 코드로 생성. |
| `CutsceneFade` | 정적 지연 생성 오버레이 캔버스(정렬 32000) — 검은 화면 페이드. |
| `CameraShake`(Core) | 정적 진폭 누적 + 선형 감소, `Random.insideUnitSphere × 진폭`. 카메라들이 LateUpdate 끝에서 더한다. |

### 3.3 캐릭터 이동 물리

| 기법 | 위치 | 설명 |
|---|---|---|
| **입력은 Update, 물리는 FixedUpdate** | `HideOrSeekPlayer.Update`/`FixedUpdate`, `MonsterController` 동일 | 키 눌림(`JumpPressed`)은 프레임마다 읽어 플래그(`jumpRequested`)로 저장하고, 속도 적용은 물리 스텝에서 — 프레임/물리 주기가 달라도 입력이 사라지지 않는다. |
| **카메라 기준 이동** | `PlayerInput.CameraRelativeMove` `Core/PlayerInput.cs:57-64` | 카메라 앞·오른쪽 벡터를 `ProjectOnPlane(…, up)`으로 수평면에 눕혀 정규화 → `forward·y + right·x` 후 다시 정규화(대각선이 √2배 빨라지지 않게). |
| **수직 속도 보존** | `HideOrSeekPlayer.Move` `:539-573` | `rb.linearVelocity = (수평x, 기존 y, 수평z)` — 중력·점프는 물리 엔진 값 그대로. |
| **회전은 `rb.MoveRotation`** | 같은 곳 | `transform.rotation` 직접 대입은 Rigidbody 보간과 어긋나 떨림 → 물리 회전 API만 사용. |
| **공중 벽 붙기 방지** | `CharacterGroundDetector.RemoveWallPush` `Core/CharacterGroundDetector.cs:66-77` | 직전 물리 스텝에 닿은 벽 법선 n들에 대해 `into = v·n`이 음수(벽을 향함)면 `v −= into·n` — **벽 방향 성분만 빼고 벽을 따라 미끄러지는 성분은 남긴다**(벡터 사영 제거). 기록이 1.5스텝보다 오래되면 무시. |
| **회피·질주·스케이트 배율** | `Move` | 회피 중에는 시작 방향으로 `baseSpeed × 2` 강제 이동(키를 떼도), 질주 ×1.3, 스케이트 `SpeedBoost`(만료 시각까지) 곱. |
| **점프 자세 고정** | `jumpFreezeNormalizedTime` | 점프 애니메이션을 정규화 시간 0.5에서 `animator.speed = 0`으로 멈춰 공중 자세 유지, 착지 시 해제. |
| **원격 캐릭터** | `NetworkTransformSync<TState>` | 원격은 Rigidbody `isKinematic`, 받은 위치·회전으로 Lerp(멀면 순간이동 Snap). 상태 enum을 `UnsafeUtility.As<TState,int>`로 박싱 없이 int 변환, 정적 생성자에서 "TState가 int 크기 enum인지" 검증. |
| **괴물 돌진 — 지면 따라가기** | `MonsterController.FixedUpdateDash` `:290-332` | 초속 80 m 돌진 속도를 **지면 법선에 사영**(`ProjectOnPlane`)해 오르막·내리막을 따라감. 붙이기 거리 = 기본 0.6 m + **v·dt·tan(최대 경사)** — 한 스텝(1.6 m) 동안 최대 경사에서 땅이 떨어질 수 있는 높이만큼 늘려서 언덕 꼭대기에서 날아오르지 않게. 틈(gap)이 있으면 `velocity.y −= gap/dt`로 이번 스텝에 메움. 낭떠러지면 위 속도를 0 이하로(발사대 방지). 쿠키 앞 `SphereCastNonAlloc`으로 멈춤. |
| **들기(Carry) 자세** | `CarryPose`(순수 수학) + `CarriedBodyPresenter[DefaultExecutionOrder(-50)]` | 드는 쪽 두 손 뼈의 중점에 들린 몸을 놓고 `LookRotation(face, bodyUp)`. 실행 순서 −50으로 **카메라 LateUpdate보다 먼저** 붙여 한 프레임 늦는 떨림을 없앤다. |
| **충돌 무시 쌍** | `PlayerGrabController.SetCollisionIgnored`, `PlayerCarryFollower` | 들린 동안 두 캐릭터 모든 콜라이더 쌍에 `Physics.IgnoreCollision`. |

### 3.4 네트워크 시계 기반 결정적 연출 (위치를 보내지 않는 동기화)

모든 화면이 공유하는 시계 `PhotonNetwork.Time`과 **방장이 한 번 쓴 시각 하나**만 있으면, 연출 상태를 각자 계산해도 같은 결과가 나온다. 늦게 들어온 사람도 즉시 맞는 자리에 있다.

| 대상 | 식 | 위치 |
|---|---|---|
| 롤러코스터 | 거리 s(t) = 속도표(`CoasterPathSO`)를 시간으로 적분한 표에서 **이분 탐색** → 레일 위 점. 실제 위치가 계산값과 크게 다르면 Snap, 작으면 따라잡기 보간 | `CoasterRide`, `CoasterTrain`(앞뒤 두 바퀴를 따로 샘플해 차체 방향) |
| 바이킹 | 각도 = A(t)·sin(2π·t/T + 위상), 진폭 포락선 A(t). 속도 부호가 바뀌는 순간 소리 | `VikingRide` |
| 로켓 발사 | 높이 = ½·a·(t − 점화)², a = 5 m/s², 점화 전 2초 불꽃·떨림 | `SpyRocket.AnimateLaunch` |
| 마녀 걷기 | 진행률 = (now − walkStart)/(walkEnd − walkStart), 위치 = Lerp(시작, 내리칠 곳). **걸음 애니메이션 속도 = 거리/시간/(2·걸음 길이)·주기** → 발이 미끄러지지 않음 | `WitchPresenter.UpdateWalk`, `WalkAnimatorSpeed` |
| 마녀 장면 | `WitchTimeline.ShotAt(mode, now, …)` — 시각표를 순수 함수로 | `WitchTimeline` |
| AI 목소리 간격 | k번째 쉬는 시간 = 5 + 15·hash(seed, k) — 정수 해시(곱셈·xor-shift)로 모든 화면이 같은 순간 재생 | `DecoyVoice.Gap`, `TryCurrentPlay` |
| 경계 소리 규칙 | `EscapeCue.ShouldCue(at, last, now)` — 지난 프레임 < at ≤ 이번 프레임일 때만 1번, 늦게 들어온 화면은 지난 소리 생략 | `EscapeCue` |

### 3.5 방장 권위 상태 머신 (탈출 모드의 심장)

| 구성 | 설명 |
|---|---|
| `EscapeState` (424줄) | 판 전체를 담는 클래스: `Items`(아이템 위치 `ItemLocation`: 상자/바닥/손/장치/사라짐 + A·B 인덱스 + 좌표 + 남은 횟수), `Chests`, `Buildings`(재료·수리 진행·수리자·완료), `RocketSlots`, `OpenExits`, `Escaped`/`Boarded`, `Rainbow`, `Flights`(모자 비행), `Bombs`, `Decoys`, `NextId`, `Revision`. |
| 직렬화 | `Encode()`가 `BinaryWriter`로 **버전 번호(`FormatVersion` = 6) + 모든 필드**를 바이트 배열로. `Decode`는 반대. `Clone() => Decode(Encode())`. 방 속성 하나(`EscapeState` 키)에 통째로 넣는다 — 부분 갱신 경합이 없다. |
| `Revision` | 방장이 쓸 때마다 +1. 방장 화면은 **자기가 이미 적용한 것보다 오래된 상태가 서버에서 돌아오면 무시**(속성 반영 지연으로 되돌아가는 문제 방지). |
| `Commit()` `EscapeAuthority.cs:1215` | 상태를 방 속성으로 쓰고 **방장 화면에는 즉시 적용**(`manager.ApplyLocalState(State.Clone())`) — 서버 왕복을 기다리지 않는다. |
| 수리 진행 | `Building.ProgressAtTime(now)` = 저장된 진행 + (now − 기록 시각) × (수리 중이면 +1/10초, 아니면 −1%/초) — **값을 매 프레임 보내지 않고 시각만 저장**, 화면은 시계로 계산. |
| 요청 처리 | `EscapeManager.Request(op, a, b, pos, targets)` → `RaiseEvent(EscapeRequest, 방장에게)` → `EscapeAuthority.Handle`이 `EscapeOp`(20종)별로 거리(`IsNear`), 역할(`IsActiveEscaper`), 위치 신뢰(`NeedsTrustedPosition`·의심 이동), 손 상태 확인 후 상태 변경 + `Commit`. |
| 이동 검사 | `TrackPositions` — 0.2초마다 각자 위치를 기록, `PlausibleSpeedFactor`를 넘는 순간 이동은 `SuspectSeconds` 동안 요청 거절. 텔레포트(호박마차·술래방 내보내기·리스폰)는 `moveExemptUntil`로 예외. |
| 안전장치 | `RestoreMissingMaterials` — 재료가 사라져 판을 못 끝내면 빈 상자에 다시 생성(`MaterialCheckSeconds`마다), 도달 가능한 상자인지 `CountReachable`. |
| 알림 | `Notice(kind, itemId)` → `RaiseEvent(EscapeNotice)`. 훔침 알림은 **보여 줄 시각**을 같이 보내 받는 쪽(`StolenNotice.Decide`)이 그때 다시 확인(그사이 되돌려 놓았으면 취소). |
| 방장 교체 | 새 방장은 방 속성에서 상태를 읽어 `Adopt` — 진행 중인 판을 이어받는다. 개인 기록(도구 쿨다운 등)은 의도적으로 초기화. |

`AuthorityProps` + `RoomPropertyGuard`(Core): 방장이 쓰는 키마다 **"기대값"을 기억**해 두고, 속성 변경 이벤트(`EventCode.PropertiesChanged`)에서 **쓴 사람(Sender)** 을 확인해 방장이 아닌 사람이 바꾼 키를 기대값으로 되돌린다. 방장이 막 바뀐 직후 1.5초 유예. `NetKeys.Scopes` 표 하나가 "키 → 대상(방/플레이어)·수명(판/세션)·쓰는 사람(방장/본인)"을 정하고, 판 초기화(`RoundStateResetter`)와 감시가 같은 표를 쓴다 — **규칙이 한 곳에 있는 좋은 설계**.

### 3.6 소리 엔진

| 기법 | 설명 |
|---|---|
| **보이스 풀(오브젝트 풀)** | `AudioRuntime`(DDOL, `BeforeSceneLoad` 자동 생성)이 시작할 때 **3D 24칸 + 2D 8칸 + 음악 3칸 + 추격 북 레이어**의 `AudioSource`를 미리 만든다. 소리마다 `Instantiate` 하지 않는다. |
| **빼앗기 정책** | 칸이 모자라면 `AudioVoicePool`이 **중요도 → 지금 들리는 크기 → 가장 오래됨** 순으로 하나를 빼앗는다. |
| **세대 번호 핸들** | `AudioHandle`(readonly struct)이 (칸 번호, 세대)를 갖는다. 칸이 다른 소리에 넘어가면 세대가 올라가 **옛 핸들로 새 소리를 끄는 사고가 불가능**. |
| **직접 만든 거리 감쇠** | `SpatialGain.Distance` = **(min/d)^p × (1 − progress^taper)**, progress = (d − min)/(max − min). 앞항은 실제 소리처럼 거리에 반비례, 뒤항은 최대 거리에서 정확히 0으로 닫는다. 종류별 p·taper(발소리 0.7·4, 괴물 0.5·3, 도구 0.6·6, 랜드마크 0.8·2). Unity 자체 감쇠는 끄고 이 값만 쓴다. |
| **층 감쇠** | 높이 차 4 m 이하 1 → 7 m에서 0.15로 선형. |
| **먼 소리 먹먹하게** | 저역 통과 차단 주파수를 22 kHz → 3 kHz로 **로그 보간**(`LogLerp` = exp(lerp(ln a, ln b, t))) — 귀가 주파수를 로그로 느끼므로. |
| **실내 차단** | `IndoorZone`(정적 목록) — 소리와 듣는 사람이 다른 공간이면 벽 너머 처리. |
| **추격 북 동기** | `ChaseDirector`가 여러 북 레이어를 `PlayScheduled(같은 dspTime)`로 **샘플 단위 동시 시작** 후 거리 기반 강도로 음량만 조절(히스테리시스로 깜빡임 방지). |
| **출력 장치 선택** | `WindowsAudioEndpoints`가 문서화되지 않은 Windows `IPolicyConfig` COM을 **vtable 함수 포인터로 직접 호출**(`Marshal.GetDelegateForFunctionPointer`), Windows 버전별 IID 2벌. 비Windows는 빈 구현. |
| **상태 차이로 소리** | `EscapeAudioPresenter`가 이전 `EscapeState`(Clone)와 새 상태를 비교해 "상자 열림·수리 완료·탈출" 등을 소리로 — 소리 이벤트를 따로 보내지 않는다. |

### 3.7 그 밖의 수학·알고리즘 도구

| 도구 | 위치 | 설명 |
|---|---|---|
| 에르미트 스플라인 + 이분 탐색 | `CoasterPathSO` | 레일 중심선 점들을 Catmull-Rom 접선의 3차 에르미트로 보간·샘플, 누적 거리/시간 표에서 이분 탐색, 위 벡터는 Slerp 후 직교화. |
| 원판 균등 표본 | `MapArea` | r = R·√u, θ = 2πv (√ 없으면 가운데 몰림). |
| 광선-사각형 교차(slab) | `WitchPath.TryEntryPoint`/`Slab` | x·z 축별 [tmin, tmax] 교집합 — 마녀 발끝이 Ground 경계에 닿는 점. |
| 충격파 일정 | `WitchShatter.OnImpact` | 각 물체 부서질 시각 = 지금 + 수평거리/120 m/s, 시각 순 정렬 후 매 프레임 앞에서부터 처리. |
| RequireComponent 위상 정렬 삭제 | `WitchFinaleStage.StripToVisuals` | 다른 컴포넌트가 요구하는 것은 나중에 지우도록 반복 제거(최대 16회), 타입별 요구 목록 캐시. |
| 꺼진 부모 아래 복제 | `WitchFinaleStage.Clone`, `DecoyPresenter.BuildTemplate` | `Instantiate`를 비활성 부모 아래에서 하면 **Awake/OnEnable이 실행되지 않는다** → PhotonView 등록·입력·카메라를 건드리지 않고 겉모습만 복제. |
| μ-law 8비트 압축 | `VoiceStore.Encode/Decode` | m = sign(s)·ln(1+255|s|)/ln 256 → 0~255. 5초 8 kHz 목소리 ≈ 40 KB, 8 KB 조각으로 `RaiseEvent`, **보낸 사람 번호로 조립**(남의 목소리 위장 불가). |
| 구간 평균 다운샘플 | `VoiceRecorderPanel.Downsample` | 48 kHz → 8 kHz를 구간 평균(간단한 저역 필터 겸)으로. 피크 정규화(최대 12배, 무음은 그대로). |
| 한글 받침 판별 | `KoreanText.HasFinalConsonant` | (코드 − 0xAC00) % 28 ≠ 0 이면 받침 — "이/가" 고르기. |
| 튀어나오기 곡선 | `MaterialChest.AppearScale` | 0→1.08(70%)→1, SmoothStep 두 구간. |
| 정수 해시 | `DecoyVoice.Gap` | Knuth 곱셈 상수 2654435761 + xor-shift. |
| 공용 재질 캐시 | `EscapeVisuals.Tint` | (색, 발광) 키로 재질 하나 공유, 씬 언로드 때 목록만 비움. **셰이더 변형 보존**을 위해 Resources에 재질 틀 4개(불투명/페이드 × 발광 여부)를 두고 복제. |

---

## 4. 폴더별 클래스·변수·메서드 해설

> 표의 "핵심 멤버"는 그 클래스를 이해하는 데 필요한 변수·메서드다. 줄 번호는 `파일:줄`. 💀 = 실행되지 않는 멤버(6장 근거), 🧪 = 시험·에디터만 씀.

### 4.1 `Camera/` (6개 · 913줄)

| 클래스 | 역할 | 핵심 멤버 | 기법·주의 |
|---|---|---|---|
| `Camera_Ctrl` | 3인칭 궤도 카메라(쿠키·괴물 3인칭·관전 공용) | 3.1절 표 | 구면좌표·Slerp·DeltaAngle. ⚠️ 낡은 주석·깨진 글자 `:140` |
| `MonsterFirstPersonCamera` | 괴물 1인칭(요·피치), 처형 때 손 쪽으로 시점 이동 | `yaw`, `pitch`, `killBlend`, `nearClip`; `LateUpdate` | `SmoothStep`+`Slerp` 블렌드 |
| `MonsterViewSwitcher` | 1인칭↔3인칭 전환, 1인칭 때 몸을 그림자만 | `Init(owner, eye)`, 렌더러 목록 | `MonsterController`가 `AddComponent` ⚠️(5.4) |
| `WitchRevealCamera` | 마녀 연출 카메라(10종 장면) | `CurrentShot(...)`, 정적 포즈 함수 9개, `OccluderCandidates`, `LineBlocked` | `[DefaultExecutionOrder(1000)]`; 💀 `ApproachPose`·`ApproachEyeHeightFor`·Purge 분기 `:274` |
| `GrabAimReticle` | 괴물 조준 대상 표시 | 코드로 만든 Image, `LateUpdate`에서 `Camera.main.WorldToScreenPoint` | UI 코드 생성 도우미 중복(5.12) |
| `CutsceneFade` | 화면 검게 덮기(정적) | `static Show(alpha)`, 지연 생성 캔버스 | 정렬 32000 |

### 4.2 `Core/` (31개 · 2,336줄) — 네트워크·입력·공용 규칙

| 클래스 | 역할 | 핵심 멤버 | 기법·주의 |
|---|---|---|---|
| `AuthorityProps` | 방장이 방/플레이어 속성을 쓰는 **유일한 통로**, 기대값 기억 | `SetRoom(Hashtable)`, `SetPlayer(p, h)`, 기대값 사전 | 방장 교체 1.5초 유예 |
| `RoomPropertyGuard` | 방장 아닌 사람이 바꾼 방장 키를 되돌림 | `WrittenByMaster(key)`, `RestoredCount`, `restoreBuffer`; `OnEvent(PropertiesChanged)` | DDOL 자기 부트스트랩(`RuntimeInitializeOnLoadMethod`), 같은 파일에 `HashtableCopyExtensions.Merged` ⚠️(한 파일 두 타입) |
| `NetSecurityRules` | 검증 규칙 순수 함수(거리·속도·문자열 길이…) | 정적 메서드 | EditMode 시험 대상 |
| `NetKeys` | 모든 속성 키 상수 + **`Scopes` 표**(대상·수명·쓰는 사람) | 상수 34개, `RoundKeys`, `SessionKeys`, `Writer` | 규칙 한 곳 ✅. 💀 `ForcedPaintActorNumbers`·`ForcedPaintColors`(아무도 안 씀)·`RegisteredSlotCount`(색칠)·`MonsterRevealTime`(쓰기만)·`MonsterSelectDeadline`(옛 정리용) |
| `NetEventCodes` | RaiseEvent 코드 13개 | `EscapeRequest`, `ToolHit`, `VoiceClip`, … | 💀 `LobbyDoorState`(13)·`PaintStroke`/`ClearColor` 경로 대부분 |
| `NetHold` | 💀 괴물 대기실 대기 중 PhotonView 인스턴스화를 "보관"하는 시스템 | `Begin()`, `Discard()`, `IsHolding`, `ParkedCount`, `HoldingPool : IPunPrefabPool` | **`Begin/Discard` 호출자 0**(호출하던 `MonsterLobbyWaitController`는 `Legacy~`). 그런데도 `PhotonNetwork.PrefabPool`을 `HoldingPool`로 감싼다(`:55`) ❌ |
| `HeldSyncRecorder` | 💀 NetHold 보관 중 동기화 값 기록 | `OnPhotonSerializeView`, 리플렉션 `currentItem` | NetHold와 함께 죽음 |
| `NetworkDefaults` | 공용 네트워크 기본값, 앱 시작 때 `SerializationRate` 설정 | `ApplyRates()` (`BeforeSceneLoad`) | |
| `NetworkTransformSync<TState>` | 위치·회전·상태 동기화 기반(쿠키·괴물) | `OnPhotonSerializeView`, 보간/Snap, `UnsafeUtility.As` | 제네릭 + 정적 생성자 검증 ✅ |
| `RoomState` | 방·플레이어 속성 **타입 안전 읽기** 모음 | `TryGetInt/Double/IntArray`, `IsMonster`, `IsSpy`, `IsBroken`, `Players`, `IsLocalMonster`… | 56개 파일이 사용(허브). 💀 `CanSelectMonster` |
| `GamePhaseState` | 방 속성 → 현재 단계(`Lobby/Prepare/Hunt/TimeAttack/Result`) 해석 **한 곳** | `Current`, `Evaluate(...)` | 💀 `IsPaintActive => false` 상수 `:31` |
| `GameSettings` / `GameSettingsSO` | 전역 규칙 SO(인원·시간·돌진·잡기·스폰…) 지연 로드 | `Current`, `PickGameMap`, `MonsterCountFor` | 💀 색칠 필드 5개(`maxColorSlots`, `paintColliderRefreshInterval`, `paintStrokeSendRate`, `paintStrokeReferencePlayers`, `minPaintStrokeSendRate`) |
| `PlayerInput` | 입력 **정적 파사드**(키 → 행동), 게임 입력 억제 | `Move`, `CameraRelativeMove`, `JumpPressed`, `SlotKeyPressed`, `BrushSizeDelta`, `IsGameplaySuppressed`, `#if UNITY_EDITOR` 가짜 입력 | ⚠️ `BrushSizeDelta`는 이제 **휠 칸 고르기**(`PlayerInventory`)에 쓰는데 이름이 옛 색칠. 💀 `PaintHeld`·`PaintButton` |
| `KeyBindings` / `InputBindingsSO` | 행동별 키를 PlayerPrefs에 저장 / 기본값 SO | `Get(action)`, `Set`, `DisplayName`, `Changed` 이벤트 | |
| `InputDevices` | 마이크 장치 목록·선택 | `Selected`, `Changed` | 💀 `HasAny` |
| `VoiceChat` / `VoiceChatSettings(SO)` | Photon Voice 음성 채팅 — 씬마다 생성/파괴, 발화자 위치 추적 | `IsCapturing`, `BodyOf(actor)`, 리플렉션 `usePrimaryRecorder` | 💀 `IsTransmitting`·`SpeakerCount`; `BodyOf`는 `CharacterRegistry.FindByOwner` 중복 |
| `CameraShake` | 정적 흔들림 누적·감소 | `Add(strength, seconds)`, `Offset` | |
| `CharacterGroundDetector` | 접지 판정·벽 접촉 기록 | `IsGrounded`, `RecordContacts`, `RemoveWallPush` | 3.3절 |
| `GameCharacter.cs` | `IGameCharacter`·`ICameraFollowTarget`·`CharacterRole`·**`CharacterRegistry`** | `All`, `Register`, `IsAlive`, `FindByOwner` | 레지스트리 패턴 ✅. 단 `FindByOwner`를 3곳만 쓰고 6곳은 같은 루프를 다시 씀(5.12) |
| `SpawnPositionFinder` | 겹치지 않는 스폰 자리 찾기 | `OverlapCapsuleNonAlloc` 무작위 시도 | |
| `SceneSpawnPoints` | 이름으로 스폰 지점 찾기 | `GameObject.Find(name)` | 씬 이름 의존 ⚠️ |
| `RoomSceneTransition` | 씬 이동 전 캐시 정리 | `OpRemoveCompleteCache` 후 `LoadLevel` | |
| `UiListBuilder` | 자식 행 재사용/추가/삭제 | `Build(parent, template, count, bind)` | `GameLobbyController`는 이걸 안 씀(5.12) |
| `CanvasGroupVisibility` | SetActive 대신 CanvasGroup으로 숨김 | `Ensure`, `Set` | Photon 콜백이 꺼지지 않게 ✅ |
| `PhaseCountdownDisplay` | 단계 남은 시간 표시(씬) | `phase` | 💀 `CountdownPhase.Survival`(직렬화 호환용) |
| `SceneNames`, `IRespawnable`, `GameSettings` 등 | 상수·계약 | | |

### 4.3 `Unit/` (11개 · 1,247줄) — 쿠키

#### `HideOrSeekPlayer` (606줄) — 쿠키 조작기

| 그룹 | 멤버 (줄) | 역할 |
|---|---|---|
| 설정 | `speed`(10), `jumpPower`(12), `groundLayer`(13), `groundCheckOffset`(14), `jumpFreezeNormalizedTime`(15), `dodgeDuration`(16) | 인스펙터 값 |
| 속도 | `baseSpeed`(20), `DodgeSpeedMultiplier`(22, ×2), `MaxMoveSpeed`(23), `speedBoost`/`speedBoostUntil`/`SpeedBoost`(25-29), `ApplySpeedBoost`(30) | 스케이트 배율·만료 |
| 순간이동 | `TeleportTo`(36), `TeleportAfter`(45), `TeleportRoutine`(50) | 호박마차·술래방 내보내기 |
| 상태 플래그 | `isJump`(59), `isDodge`(62), `keepMovingAfterDodge`(63), `rotation`/`rotation_value`(64-66), `dodgeRotation`/`dodgeMoveDir`/`dodgeTimer`(67-69), `jumpRequested`(70) | ⚠️ `rotation`은 회전이 아니라 **이동 방향 벡터**(이름 오해), `rotation_value`는 스네이크 표기 |
| 부품 | `animator`, `rb`, `groundDetector`, `animationDriver`, `networkSync`, `grabController`, `carryFollower`, `carriedBody`, `lifePresenter`(71-80) | 조합된 순수 클래스·컴포넌트 |
| 들기 RPC | `RpcOnGrabbedByOwner`·`RpcOnReleased`·`RpcRequestGrabKill`·`RpcOnGrabRejected`(86-91), `OnGrabbedByOwner`(147), `OnReleased`(167), `OnGrabRejected`(177), `RequestGrabKill`(204) | **`info.Sender` 검증** 후 수락/거절 ✅. `RequestGrabKill`은 모든 화면이 연출을 시작하고 방장만 `GrabKillAuthority.TryConfirm`. ⚠️ `:210`의 `if (pv == null) return; // … NetHold` 주석은 죽은 NetHold 설명 |
| 계약 | `IsMine`, `IsLocallyControlled`, `Role`, `View`, `IsSpectatable`, `CanInteract`, `CameraTargetHeight`, `CameraDistance`, `IsBroken`(103-118), `IsMovementLocked`(95) | `IGameCharacter` 구현 |
| 사망 대기 | `GrabKillConfirmTimeout`(124, 4초), `awaitingDeathUntil`, `CancelAwaitedDeath` | 방장 확정을 기다리되 시간 초과면 풀림 |
| 탈출 모드 부착 | `AttachEscapeMode`(291/340) | ⚠️ 실행 중 **8개 컴포넌트 `AddComponent`**: `CookieAudio`, `CarriedBodyPresenter`, `StunReceiver`, `HeldItemPresenter`, `EscapeCharacterState`, `HatFlight`, `SkateVisual`, `PlayerInventory` |
| 입력·이동 | `CheckMovementInput`(460), `CheckJumpInput`(489), `CheckDodgeInput`(502, 쿨다운 `DodgeCooldown` 3초), `DodgeOut`(526), `Move`(539), `UpdateRemote`(392) | 3.3절 |
| 복구 | `RespawnToSpawnPoint`(573) | `IRespawnable`(FallGuard·VoidKillZone) |

| 나머지 클래스 | 역할 | 핵심 멤버 | 기법·주의 |
|---|---|---|---|
| `PlayerAnimationDriver` (순수 클래스) | 이동 상태 → 애니메이터 트리거 | `ChangeState(PlayerMoveState)`, `StateChanged` 이벤트, `ReplayJump`, Carry 레이어 가중치(`CarryReason [Flags]`) | 트리거 이름 = enum 이름 문자열 계약. `MonsterController.ChangeState`와 거의 같은 코드 ⚠️ |
| `CarriedBodyPresenter` | 들린 몸을 드는 쪽 손에 붙임(모든 화면) | `[DefaultExecutionOrder(-50)]`, `LateUpdate` | |
| `CarryPose` | 두 손 중점·들린 몸 회전 수학 | 정적 함수 | 순수 계산 ✅ |
| `PlayerCarryFollower` (순수) | 들린 동안 kinematic, 충돌 무시, 내려놓기 1.2 m 앞 | | |
| `PlayerGrabController` | F로 들기/내려놓기 | `OverlapSphereNonAlloc`, 대상 소유자에게 RPC, `SetCollisionIgnored` | `ReferenceEquals` vs Unity null 구분 ✅. 모든 콜라이더 쌍 O(n·m) |
| `PlayerNetworkSync` | `NetworkTransformSync<PlayerMoveState>` + 들기 상태 | `isCarrying`, `carrierViewId` | |
| `PlayerSkinApplier` | 판 캐릭터 색/재질 적용 | 속성 변경 시 다시 적용 | 헤더 주석 `InitPaintCanvas` 낡음 |
| `SkinCatalogSO` | 캐릭터 4종 재질·모델 목록 | `GetMaterial`, 💀 `GetModel` | |
| `PlayerBillBoard` | 이름표 빌보드 | `Camera.main` 매 LateUpdate, 0.25초 가시성 | |
| `PlayerMoveState` | enum | | |

### 4.4 `Monster/` (21개 · 2,129줄)

#### `MonsterController` (449줄)

| 그룹 | 멤버 (줄) | 역할 |
|---|---|---|
| 설정 | `eyeSocket`, `cameraTargetHeight`, `cameraDistance`, `speed`, `groundLayer`, `grabKillTrigger`, `animator`, `grabSocket`(16-24) | |
| 처형 타이밍(정규화 시간) | `grabKillSquashStart/End`, `grabKillShrinkStart`, `grabKillCrumbTime`, `grabKillCrushNormalizedTime`(28-38) + 공개 getter | **애니메이션 진행도 하나로** 쿠키 눌림·가루·소멸 시점을 모든 화면이 맞춤 |
| 처형 상태 | `GrabKillClipKeyword`(58), `grabKillDuration/Remaining`(65-66), `IsGrabKilling`(67), `GrabKillProgress`(79), `PlayGrabKill`(415), `FindClipLength`(195) | 클립 길이를 **이름 키워드로 검색** ⚠️ 문자열 계약 |
| 돌진 | `tentacleDash`(53, 순수 `MonsterTentacleDash`), `wasDashing`/`lastDash*`(50-52), `FixedUpdateDash`(290), `TryProbeGround`(334), `TryStopBeforeCookieOnDashPath`(361), `DashStopMargin`(357), `dashSweepHits`(100), `DashCooldownLeft`(103) | 3.3절 |
| 보안 | `RejectIfNotMonster`(153) | 괴물이 아닌 사람이 만든 괴물 캐릭터를 방장이 `PhotonNetwork.Destroy` ✅ |
| 조준 | `TryGrabAimTarget`(165), `HasGrabAimTarget`(168) | `MonsterGrabKillTrigger`에 위임 |
| 애니메이션 | `currentState`/`previousState`(55-56), `ChangeState`(212~429 여러 오버로드), `StateChanged`(425) | ⚠️ 쿠키 쪽과 중복 |
| 계약 | `CameraTargetHeight`, `CameraDistance`, `IsLocallyControlled`, `Role`, `View`, `IsSpectatable`, `CanInteract`, `IsStunned`(104-116) | |
| 부착 | `Awake`에서 `StunReceiver`, `MonsterAudio`, `MonsterEscapeState`, `MonsterViewSwitcher` `AddComponent` ⚠️ | |

| 나머지 클래스 | 역할 | 핵심 멤버 | 기법·주의 |
|---|---|---|---|
| `MonsterTentacleDash` (순수) | 돌진 시간·쿨다운·속도·진행 확인 | `DashVelocity(nearGround, normal, maxSlope)`, `ReportProgress`, `Cancel` | 막히면 자동 취소 |
| `MonsterGrabKillTrigger` | 조준 + E로 잡기 | 화면 중앙 `SphereCastNonAlloc`, 시야선(눈/손 높이), 감옥 풍선 확인, `RpcRequestGrabKill` | RPC를 `RpcTarget.All`로 보내는 것은 의도 — 모든 화면이 처형 연출을 시작하고, 확정(`TryConfirm`)은 받는 쪽 중 방장만, 보낸 사람이 그 괴물 주인인지 `info.Sender`로 검증 ✅ |
| `GrabKillAuthority` (정적) | 방장의 잡기 확정 | `TryConfirm(cookie, monster, sender)`, 정적 사전(씬 로드 때 초기화) | `RuntimeInitializeOnLoadMethod` |
| `GameRuleController` | 방장이 매 프레임 판정(`EscapeRules`) → `GameResult` | 목록 재사용(할당 없음), `WriteWitchDeaths`, `FinishEscape` | 💀 비탈출 분기(`MonsterWins`/`CookiesWin` 옛 경로) |
| `ResultScreenController` | 결과 화면, 12초 뒤 대기실 | CanvasGroup 숨김, `RoomPropertyGuard.WrittenByMaster` 확인, 유리병 트로피 | 행 `Instantiate`(한 번만) |
| `PlayerResultRow` | 결과 행 | `SetEscaper`, `SetMonster(name, catches, witch)` | 💀 `SetMonster(name)`·`SetCookie` 옛 오버로드 |
| `SpectatorController` | 관전(쿠키 프리팹) | 정적 이벤트 `SpectateTargetChanged`·`SpectatingVehicleChanged`, `SpectateRules` 사용 | 정적 이벤트로 `SpectatorLabel`과 분리 ✅ |
| `SpectateRules` (순수) / `SpectatorLabel` | 관전 대상 규칙 / 관전 이름 표시(씬) | `IsCandidate`, `Merge` | |
| `MonsterAssignmentAuthority` | 가마솥 지원 → 괴물 확정(방장) | Sender·정원·가마솥 거리 검증, `MonsterRoster.TryAppend` | 💀 `ClearLegacyDeadline`(`:81`) |
| `MonsterRoster` (순수) | 괴물 목록 계산 | `Current`, `Remaining`, `ReturnWhenMonstersGone(…, witchPurging, …)`, `TryAppend` | `witchPurging` 인자는 늘 false 💀 |
| `MasterClientPolicy` | 방장은 "괴물 아닌 가장 작은 액터"로 되찾기 | 속도 제한 카운터 | |
| `Cauldron` / `MonsterRevealController` | 대기실 가마솥 트리거 → `ClaimMonster` 이벤트 / 괴물 발표 배너 | | |
| `MonsterJoinController` | 괴물 본인이 `MonsterPlayer` 생성 | `GameObject.Find(MonsterSpawnPos)` ⚠️ | |
| `GamePhaseStarter` | `PrepareEndTime`이 안 오면 방장이 대신 씀(안전장치) | | |
| `RoomLifecycleWatcher` | 괴물이 모두 나가면 대기실로 | 💀 `witchPurging` `:28` | |
| `RoundStateResetter` | `NetKeys` 판 키 일괄 삭제, 방 다시 열기 | | |
| `MonsterDepartureBanner` | 괴물 이탈 경고(CanvasGroup) | | |
| `MonsterMoveState` | enum | | |

### 4.5 `ColorTag/` (4개 · 1,057줄)

| 클래스 | 역할 | 살아 있는 부분 | 💀 죽은 부분 |
|---|---|---|---|
| `PlayerPaintCanvas` (649줄) | 쿠키 몸의 RenderTexture 색칠 캔버스 | `ForceFillColor`(물풍선 색), `ApplyRemote`의 ForceFill, `SampleSurfaceColors`(`AsyncGPUReadback` — 부서질 때 가루 색), `TrySetBaseSkin`, 칠해진 스킨 재질 | 손 색칠 전체: `HandlePaintInput`, `StampAtScreenPoint`, `TryRegisterSlotAndStamp`, `SetBrushColor`(321), `SetEraseMode`(327), `ResetCanvas`(332), `SendResetEvent`, `ClearColor` 이벤트(463), Erase 종류, `LateStrokeGrace`(417), `InPaintWindowNow`(475, 늘 false), `PaintableCollider`·`CurrentBrushRadius`·`CurrentBrushColorIndex`·`RegisteredColorSlots`(47-51), `ApplyForcedColorIfAssignedToMe`(Forced 키를 아무도 안 씀), 획 묶음 전송률 계산 |
| ↳ 기법 | GL.QUADS로 스탬프, 스탬프 셰이더 하드웨어 블렌드(OneMinusDstAlpha)로 "먼저 칠한 색 고정", 획을 배열 묶음(SoA)으로 `RaiseEvent`, 보낸 사람 == 주인 검증 | | |
| `PaintColliderUpdater` | 칠한 메시로 충돌체 굽기(IJob, `Physics.BakeMesh` 작업자 스레드, 이중 버퍼) | — | 💀 Tick이 `IsPaintActive`일 때만 → **하지만 로컬 쿠키마다 생성되어 Mesh 2개를 할당**한다 ❌ |
| `CookieLifeStatePresenter` (310줄) | 쿠키 생사(부서짐·잡힘) 표시 | 멱등 `Reconcile`(0.5초 확인 + 콜백 + 재접속), 잡힘 때 괴물 손 소켓 따라가기, `CrushScale`, `ParticleSystem.Emit`(샘플 색) | ⚠️ **폴더가 틀림**(색칠이 아니라 생사 — `Unit/`이 맞음) |
| `ColorPaletteSO` | 색 팔레트 | `colors` | 💀 `ColorEntry.colorName` |

### 4.6 `Escape/` (53개 · 8,258줄) — 탈출 모드

#### 4.6.1 상태·규칙·네트워크

| 클래스 | 역할 | 핵심 멤버 | 기법·주의 |
|---|---|---|---|
| `EscapeManager` (475줄) | **모든 화면**의 탈출 모드 허브: 상태 읽기·뷰 갱신·요청 보내기·지형 도우미 | 정적 `Instance`(13), `State`(40), `StateChanged`(48), 정적 `HasLeftMap`/`HasEscaped`/`IsActive`/`ActionsAllowed`/`ChestsVisibleIn`(49-64), `ReadStateFromRoom`(156/205), `ApplyLocalState`(212/237), `RefreshViews`(245), `Request`(286), `SentByMaster`(332), `ItemDef`(342), `InMonsterRoom`(347), `TryRandomMapPoint`(366), `IsValidTeleport`(380), `SafeGroundPoint`(395), `DropPointNear`(409-434), `SnapToGround`(444) | ⚠️ 책임이 많다(상태 동기 + 뷰 + 요청 + 지형 질의 + 시각 상수). 💀 `MapRect`(46), `BuildingView`(78), `ExitView`(79). `IsNearCookieSpawn`·`FallbackDropPoint`가 호출마다 `GameObject.Find`(`:415,422`). `SnapToGround`가 `RaycastAll`(할당) |
| `EscapeAuthority` (1,232줄) | **방장만** 실행하는 규칙 엔진 | 상수(13-29: 칸 4, 줍기 거리, 폭탄 3칸·20초…), `rng`, `toolReadyAt`, `lastPositions`, `suspectUntil`, `moveExemptUntil`, `Initialize`(63), `Tick`(259), `Handle`(394), 작업별 `OpenChest`/`PickUp`/`Drop`/`RepairBegin`/`RepairStop`/`CompleteRepair`/`OpenExits`/`Dismantle`/`Exit`/`RocketInsert`/`RocketBoard`/`TryLaunchRocket`/`StartWitchTimer`/`RainbowOpen`/`RainbowBreak`/`HatFly`/`PlaceBomb`/`RemoveBomb`/`DetonateBombsAt`/`DecoyCatch`/`ToolUse`/`AttachRecorder`/`ValidTargets`, `RestoreMissingMaterials`(307), `OnPlayerLeft`(988), `OnPlayerBroken`(1000), `Commit`(1215) | 3.5절. ⚠️ 1,232줄 한 클래스(작업별 핸들러를 `partial` 또는 전략 객체로 나눌 만함). 요청마다 `IsNear`·역할·의심 검사 ✅ |
| `EscapeState` (424줄) | 판 상태 데이터 + 이진 직렬화 | 3.5절, 중첩 struct `Item/Chest/Building/Slot/Flight/Bomb/Decoy` | `FormatVersion` 6 |
| `EscapeNet` | 요청 페이로드(`EscapeOp` 20종), 알림 종류(`EscapeNoticeKind`) | `ToPayload`, `FromPayload` | 💀 `DecodeSlot`, `EscapeNoticeKind.StolenToRocket` |
| `EscapeRules` (순수) | 게임 끝 판정 | `PlayerInfo`, `Decision`, `WitchSlamSeconds` | ✅ 시험 가능 |
| `WitchTimeline` (순수) | 마녀 시각표 | 상수 15개, `ShotAt`, `FinaleShotAt`, `WalkStart/End`, `TryGetLeadInStart` | 💀 `PurgeAppearAt`, `PurgeStrikeAt`, `PurgeWalkSeconds`, `ShouldStartPurge`, `WitchMode.Purge`, `WitchShot.Approach` 분기, `CookieDiesInStrike`의 Purge 분기 |
| `StolenNotice` (struct) | 훔침/다시 생김 알림 지연 판단 | `Decide(now, loc)`, `Template` | 순수 ✅ |
| `MapArea` (readonly struct) | 맵 원판·무작위 점 | √ 균등 표본 | |
| `KoreanText` | 받침 판별 | | |
| `EscapeCue` | 경계 시각 소리 규칙, `FindDeep` | | 깊은 자식 찾기 한 곳 ✅ |

#### 4.6.2 맵 사물(상호작용)

| 클래스 | 역할 | 핵심 멤버 | 기법·주의 |
|---|---|---|---|
| `QuadrantBuilding` | 사분면 재료 건물(수리·해체) | `Bind`, `Refresh`, `CurrentMode`(Repair/WrongMaterial/Dismantle/Busy), `HoldSecondsFor`, `DisplayProgress`(재시도 0.6초×3), `GetLabel` | `RepairHoldSeconds = 3600` — 끝은 방장이 정함 ✅. `FindPrefix`(Blender `.001` 접미사 대응). 게이지 빌보드 `Camera.main` 매 프레임 |
| `EscapeGate` | 탈출구(문 내려가기·트리거 닿으면 탈출 요청) | `ExitTouchRadius` 3.5, `Bind`, `Refresh`, `UpdateVisuals` | `XRayMarker`로 벽 너머 표시 |
| `MaterialChest` | 재료/스파이 상자 | `Show(index, chest, appear)`, `Hide`, `AppearScale`, 뚜껑 닫힘 연출 | |
| `RainbowChest` | 무지개 상자(모자) | `Refresh`, 열기 2초/부수기 3초 | 모델 없으면 코드 도형 |
| `SpyRocket` | 스파이 로켓(재료·탑승·발사) | `PlaceAt`, `Refresh`, `AnimateLaunch`, `RocketNeeds` | ⚠️ `Refresh`가 칸마다 `transform.Find($"Slot_{i:00}_Empty")` 문자열 생성·검색(`:97-98`) — `Awake`에서 `_Filled`만 캐시. `slotParts` 크기 4 vs `filledLooks` 2 불일치 |
| `MonsterRoomDoor` | 괴물만 지나가는 문(본인 화면 충돌 무시) | `LocalMonster()` 루프 | 중복 루프(5.12) |
| `MonsterRoomGuard` | 술래방에 들어간 쿠키 내보내기 | `IsInside`(순수), `TryEjectPoint`(반원 탐색), `IsEjectMove` | 🧪 `EditorBind` |
| `CookieHole` | 쿠키만 지나가는 구멍(로컬 충돌 무시) | | |
| `GroundItemView` | 바닥 아이템(풀 사용) | 모델이 바뀌면 `Destroy` 후 다시 생성 | |
| `NoiseBombPresenter` / `NoiseBombView` | 괴물 소음폭탄 설치·표시 | `Sync`(상태와 뷰 맞추기), `LocalChargesLeft` | 뷰는 `CreatePrimitive`/`Destroy` |
| `AlarmBeacon` | 폭탄 터진 건물 경보등(괴물만) | 정적 `Show(pos)` | `CreatePrimitive` + 시간 뒤 `Destroy` |

#### 4.6.3 캐릭터 부착물·아이템·도구

| 클래스 | 역할 | 핵심 멤버 | 기법·주의 |
|---|---|---|---|
| `PlayerInventory` | 4칸 가방(로컬) | 정적 `Local`, `Selected`, `HeldItem`, `HandsLocked`, `Changed`, `TrySelect`, `RequestDropHeld`, `NextOccupied`(순수) | 💀 `RequestDropAll`. 휠 = `PlayerInput.BrushSizeDelta`(이름 낡음) |
| `ToolUser` | 도구 쓰기(대상 고르기 → 방장 요청) | 정적 `Aiming`, `Use(slot, item)`, `UseSound`, `CollectInSphere`, `SortedHits` | ⚠️ 스턴건 조준 확대를 `Camera.main.fieldOfView`에 **매 프레임** 씀 — 마녀 연출 카메라도 FOV를 씀(5.8). `new System.Random()` 매번, `SphereCastAll` 할당 |
| `ToolHitReceiver` (정적) | 방장 승인 명중 처리(모든 화면 효과 / 본인 상태) | `Handle(data)`, `Apply`, `ApplySelf`, `HandOf` | `HandOf`는 `FindByOwner` 중복 |
| `ToolFx` / `ToolShotFx` | 명중 구 / 풍선 포물선·스턴 빔 | `Play`, `ThrowBalloon`, `Beam` | 매번 생성·파괴 |
| `StunReceiver` | 기절 별·감옥 풍선 | `Stun`, `Jail`, `IsStunned`, `IsJailed` | 별은 한 번 만들어 켜고 끄기 ✅. 감옥 풍선 재질 `NewMaterial` 해제 없음 ⚠️ |
| `HeldItemPresenter` | 손에 든 아이템 모양(모든 화면) | `SelectedSlot` 속성 반영 | |
| `HatFlight` | 헬리콥터 모자 비행 | `NearestEdgeDirection`(🧪) | 공통 시계 |
| `SkateVisual` | 스케이트 바퀴 | `SpeedBoost > 1`일 때 | |
| `EscapeCharacterState` | 탈출·탑승 시 숨기기·관전 전환 | `OnStateChanged`, `ApplyHidden` | |
| `XRayMarker` | 벽 너머 실루엣 재질 추가 | `Init(color)`, `SetVisible` | |
| `MonsterEscapeState` | 괴물 마녀 사망 반영 | `ApplyIfDead` | 방장이 쓴 `DeathCause`만 믿음 ✅ |

#### 4.6.4 AI(미끼)·목소리

| 클래스 | 역할 | 핵심 멤버 | 기법 |
|---|---|---|---|
| `DecoyPresenter` | 상태의 `Decoys`를 각 화면이 그림 | `Sync`, `Create`, `BuildTemplate`(Resources 쿠키 프리팹을 겉모습만), `ApplySkin` | 꺼진 부모 아래 복제 |
| `DecoyView` | AI 하나(잡히는 연출·목소리) | `IsCatchable`, `RequestCatch`, `Tick`, `TickCaught`, `Vanish` | 진짜 쿠키와 같은 진행도로 눌림·가루 |
| `DecoyVoice` (순수) | 목소리 재생 시각 | `Gap`, `TryCurrentPlay` | 정수 해시 |
| `VoiceRecorderPanel` | 대기실 5초 녹음 버튼 | `IsRecording`, `RecordWhenMicFree`, `Downsample`, `Normalize` | 자기 부트스트랩, 음성 채팅과 마이크 넘겨받기. 🧪 `EditorFakeRecording` |
| `VoiceStore` / `VoiceNet` | 녹음 저장·전송·조립 | `SetLocal`, `ClipFor`, `Encode/Decode`, `SendLocalVoice`, `Receive` | μ-law, 크기 제한 |

#### 4.6.5 마녀 연출

| 클래스 | 역할 | 핵심 멤버 | 기법·주의 |
|---|---|---|---|
| `WitchPresenter` (442줄) | 마녀 등장·걷기·밟기 | 정적 `Current`, 정적 이벤트 `Impacted`, 상수(키 270 m 등), `BuildPath`, `EnsureStage`, `UpdateImpact`, `LateUpdate`(고개 숙임·발 들기 본 추가 회전), `StompLegAngle`, `WalkAnimatorSpeed`, `ApplyFog`, `UseFadeMaterials` | ⚠️ `Awake`에서 `WitchShatter`·`WitchFinaleStage` `AddComponent`, **`Camera.main.gameObject.AddComponent<WitchRevealCamera>()`**(`:347`) — 씬 카메라에 의존. 💀 `Facing`의 Purge 분기(`:259`). 🧪 `FadeAlpha`. 자리표시 마녀 재질은 해제하지 않음 |
| `WitchFinaleStage` | 도착 연출 대역(쿠키·괴물 복제) | `Choose`/`Arrange`(순수), `GroundHeight`, `Clone`, `StripToVisuals`, `HideRealCharacters`, `OnImpact` | 3.7절 |
| `WitchShatter` | 맵 박살 | 정적 `Current`, 조각 풀(`Stack<Piece>`, 미리 25개/프레임, 최대 400), `FindTargetGroups`, `IsBreakable`, `ShatterBody` | 자체 풀 ✅ |
| `WitchPath` (정적) | 마녀 길(Ground 사각형) | `TryGetGroundRect`, `FindGround`, `TryEntryPoint`, `StrikePoint` | slab 교차 |

#### 4.6.6 HUD·결과·공용 시각

| 클래스 | 역할 | 핵심 멤버 | 주의 |
|---|---|---|---|
| `EscapeHud` (471줄) | 탈출 HUD(건물 상태·가방·알림·스파이 표시·마녀 타이머) | 정적 `instance`, 정적 API `Toast`/`Notice`/`ScheduleStolen`/`ScheduleRespawned`, `Build`, `RefreshRequirements`, `RefreshHotbar`, `CookieCaughtText` | ⚠️ `RefreshRequirements`가 0.25초마다 `List<(…)>`·문자열 생성. `Update`에서 `PlayerInventory.Local`을 폴링해 다시 묶음 |
| `SkillHud` | 스킬 칸 2개 | `Set(slot, …)`, `BombState`, `LocalCharacter()` | 중복 루프 |
| `MonsterJarTrophy` / `MonsterJarStage` | 괴물 승리 유리병(2D/3D RenderTexture) | `Build`, `Init`, `StackCookies` | `OnDestroy`에서 RT 해제 ✅ |
| `EscapeVisuals` (정적) | 모델/도형 생성·재질 캐시·파티클 빌더 | `CreateItemModel`, `Tint`, `NewMaterial`, `ApplyFade`, `CreateParticles(ParticleStyle)`, `CreatePassenger` | 셰이더 변형 보존 ✅ |
| 데이터 SO | `EscapeCatalogSO`(아이템·레시피 목록), `EscapeRecipeSO`(맵별 재료), `EscapeTextsSO`(모든 문구), `ItemSO`, `ToolSO` | 정적 `Current` 캐시 | 💀 옛 필드(6.4절) |

### 4.7 `Audio/` (26개 · 2,829줄)

| 클래스 | 역할 | 핵심 멤버 | 주의 |
|---|---|---|---|
| `GameAudio` (정적 파사드) | 모든 소리 진입점 | `Play`, `PlayAt`, `PlayOn`, `PlayCharacter`, `StartLoop`, `SetLoopFade`, `PlayMusic`, `Stinger` | 💀 `StopMusic`, `CurrentMusic` |
| `AudioRuntime` | DDOL 실행기(보이스·음악·추격 소유) | `Boot()`(BeforeSceneLoad), `Instance`, `Music`, `Chase`, `quitting` | 자기 부트스트랩 싱글턴 ✅ |
| `AudioVoicePool` (순수) | 칸 배정·빼앗기·세대 | `Acquire`(빼앗기 여부 `out stolen`), `Release`, `GenerationOf`, `SetAudibility`, `CountPlaying` | 💀 `CountBusy`. 🧪 `SoundAt` |
| `SpatialGain` (순수) | 거리·층·저역 통과 계산 | `Distance`, `Floor`, `Evaluate`, `Cutoff`, `LogLerp` | 3.6절 |
| `IndoorZone` | 실내 상자(정적 목록) | `Separated(src, listener)` | |
| `AudioListenerAnchor` | 귀를 캐릭터 머리로, 씬 리스너 끄기 | `FindObjectsByType<AudioListener>` (씬 로드마다) | |
| `AudioVolumeSettings` | 버스 음량 4개(PlayerPrefs) | `Changed` 정적 이벤트, 🧪 `ToDecibels` | |
| `MusicPlayer` / `MusicDirector` / `MusicRules` | 크로스페이드 덱 2 + 스팅어 / 0.25초마다 상황 → 곡 / 곡 선택 순수 함수 | `Select(MusicContext)`, `LocalMonsterWaiting` | 💀 `LocalMonsterWaiting`(늘 false), `MusicId` Hunt 5종·TimeAttack(번호 보존용) |
| `ChaseDirector` / `ChaseIntensity` / `ChaseAudioSettingsSO` | 괴물 접근 북소리 | `PlayScheduled` 동기, 히스테리시스 | 🧪 `Intensity` getter |
| `CookieAudio` / `MonsterAudio` | 애니메이션 상태 이벤트 → 발소리·점프·회피 | `StateChanged` 구독 | 💀 `CookieAudio.TickPaint`(`:70`) |
| `EscapeAudioPresenter` | 상태 차이 → 소리 | `previous`(Clone), `PlayOnActor` | `PlayOnActor`는 `FindByOwner` 중복 |
| `AmbientEmitter` / `AmbientZone` | 제자리 반복음 / 영역 2D 환경음 | `ShouldPlay`(순수, 히스테리시스) | |
| `OutputDevices` / `WindowsAudioEndpoints` | 출력 장치 목록·선택(COM) | `Select(id)`, `Changed` | 3.6절 |
| `SoundSettingsPanel` | **옛 소리 설정 창**(§27 설정 메뉴 이전) | `PickOutput`(🧪), `closeOnEscape` | ⚠️ `SettingsMenu.prefab` 안에서 아직 컴포넌트로 쓰임, 단독 `SoundSettingsPanel.prefab`은 어디서도 안 씀 |
| `UiSound` / `UiSoundCues` | 버튼 소리·프레임 중복 제거 | `OnPointerEnter`(인터페이스) | |
| `SoundCatalogSO` / `MusicCatalogSO` / `SoundId` | ID → 클립·음량·감쇠 표 | `Find`, `IsLoopById`(🧪) | 💀 `SoundId` 20종(Legacy 시퀀스·색칠 전용), 6.5절 |

### 4.8 `Environment/` (9개 · 940줄)

| 클래스 | 역할 | 핵심 멤버 | 주의 |
|---|---|---|---|
| `InteractableDoor` (321줄) | 방장 권위 문(대기실·맵) | 방 속성 Hashtable 하나에 모든 문 상태 + 정적 `masterPendingWrites`(응답 전 덮어쓰기), 요청자 거리 검증, 애니메이터 bool | 💀 `NetHold.IsHolding` 분기, `LobbyDoorState` 이벤트(`:145`), 메시지 큐 정지 분기 |
| `CoasterPathSO` | 레일 중심선·속도표 | `Evaluate(s)`, `TimeAt`(🧪), `Segment`(이분 탐색) | 💀 `DistanceAt`, `Gauge` |
| `CoasterRide` / `CoasterTrain` / `CoasterTrainAudio` | 공통 시계 롤러코스터 / 차량 자세 / 속도 기반 소리 | `FixedUpdate`에서 kinematic `MovePosition` | 🧪 `TrainCount`, `Train`, `EditorPlaceAt`, `CarCount`, `CarBody` |
| `VikingRide` | 공통 시계 바이킹 | 진폭 포락선 × sin | 💀 `CurrentAngle`, 🧪 `EditorSetPhase` |
| `CauldronGlow` / `CauldronSplash` | 가마솥 빛(MaterialPropertyBlock) / 튀김 | `CauldronSplash`는 Update를 스스로 끔 | |
| `FlickerLight` | Perlin 깜빡임 + 돌발 상태 | | |

### 4.9 `GameManager/` (10개 · 687줄)

| 클래스 | 역할 | 주의 |
|---|---|---|
| `GameManager` | **채팅만** 담당(이름 오해 ⚠️) — `RPC LogMsg`, `ChatText.Sanitize`, `ChatRateLimiter`, `ChatRules` | `Start`에서 `IsMessageQueueRunning = true`(옛 대기실 대기 흔적), `FindFirstObjectByType<RoomExitController>` |
| `PlayerSpawner` | 쿠키 스폰(사분면 순서) | `GameObject.Find` 2곳, 낡은 `MonsterLobbyWaitController` 주석 |
| `RoomExitController` | 나가기 확인 → `LeaveRoom` → 로비 | 옛 대기 복구 코드(큐·KeepAlive) |
| `EscMenu` | 게임 씬 ESC 메뉴 | 정적 `Instance`, `Input.GetKeyDown(KeyCode.Escape)` 직접(`:63`) ⚠️ |
| `RoomSettingsMenu` / `ConfirmDialog` | 방 설정 / 확인 창 | |
| `FallGuard` / `VoidKillZone` | 낙하 복구(`IRespawnable`) | 정적 공유 쿨다운 |
| `ChatText` / `ChatRules` | 채팅 정리·표시 규칙 | 순수 ✅ |

### 4.10 `Interaction/` (5개 · 370줄)

| 클래스 | 역할 | 핵심 멤버 | 주의 |
|---|---|---|---|
| `CharacterInteractor` | E 상호작용 — 0.1초마다 가장 가까운 대상 | DDOL 자기 부트스트랩, `HasFocus`, `FindLocalCharacter`, `HoldProgress` 사용 | ⚠️ `is MonsterController` 구체 타입 분기(잡기 우선) |
| `HoldProgress` (순수) | 길게 누르기 상태 기계 | | ✅ |
| `IInteractable.cs` | `IInteractable`·`IHoldInteractable`·`IHoldStartListener`·`IHoldEndListener`·`IHoldProgressDisplay` + `InteractableRegistry` | 인터페이스 분리 원칙 ✅ | 레지스트리가 `CharacterRegistry`와 같은 모양(중복) |
| `IInteractionLabel` | 안내 문구 | | |
| `InteractionPromptUI` | E 안내 UI(Resources 프리팹) | | |

### 4.11 `Lobby/` (13개 · 1,511줄)

| 클래스 | 역할 | 주의 |
|---|---|---|
| `LobbyController` (371줄) | 연결·지역·방 목록(차이 갱신)·만들기/참가 | UnityEvent 버튼 메서드(`OnMakeRoomButtonClicked` 등)는 씬에서 연결 — 이름 검색으로는 "미사용"처럼 보이지만 사용 중 |
| `GameLobbyController` (316줄) | 대기실 플레이어 목록·강퇴·시작·지역 이동 | ⚠️ 목록을 매번 `Destroy`/`Instantiate`(`UiListBuilder` 미사용), `FindFirstObjectByType<RoomExitController>` 3번(`:183,215,297`) |
| `GameStartAuthority` (정적) | 호스트 요청 → 방장 실행 | 💀 `CurrentMapScene`(`:128`) |
| `CharacterAssignment` / `PlayerSkinSelector` | 캐릭터 배정(순수) / 선택 UI | |
| `LobbyKick` / `RegionMove` / `RoomSettingsAuthority` / `RoomSettingStepper` / `RoomSettingField` | 강퇴 / 지역 이동(정적 대기) / 방 설정 요청 / 단계 계산 / 설정 행 | 💀 `RoomSettingField.StepBy` |
| `RegionSelector` (+`PhotonRegions`) | 지역 드롭다운 | ⚠️ 한 파일에 두 타입 |
| `PlayerListItem` / `RoomListItem` | 행 | |

### 4.12 `UI/` (4개 · 467줄)

| 클래스 | 역할 | 주의 |
|---|---|---|
| `SettingsMenu` | 설정 탭 | 💀 `CurrentTab`, ESC 직접 읽기 |
| `KeyBindingsPage` | 조작키 다시 정하기 | 정적 `listening`/`confirming` → `ConsumesEscape`, 💀 `IsConfirmingReset`, 🧪 `EditorBind`·`EditorKeyText`(런타임 클래스 안 시험 훅) |
| `InputDevicePage` | 마이크 선택·레벨 | `Peak` |
| `LobbyEscMenu` | 로비 ESC | ESC 직접 읽기, `OpenSettings`(UnityEvent) |

### 4.13 `Dev/` (2개 · 84줄)

| 클래스 | 역할 | 판정 |
|---|---|---|
| `OfflineModeBootstrap` | 오프라인 시험 모드 시작 | 출시 씬에서는 제거됨(7차 F-02). 시험 도구로 유지 |
| `MonsterTestSpawner` | `PlayerTestScene` 전용 괴물 스폰 | 💀 **어떤 씬·프리팹·코드도 참조하지 않음** — `PlayerTestScene`은 디스크에서 삭제됨(README) |

---

## 5. 구조 점검 12항목

각 항목: **판정 · 근거(파일:줄) · 영향 · 권장**. 번호 `A-xx`는 8장 조치표와 연결된다.

### 5.1 쓰이지 않는 메서드·변수·필드·클래스·파일이 있는가 — ❌ 있음

6장(코드)·7장(에셋)에 전부 적었다. 요약:

| 묶음 | 규모 | 원인 |
|---|---|---|
| 손 색칠(ColorTag) | `PlayerPaintCanvas` 약 300줄 + `PaintColliderUpdater` 78줄 + `NetKeys` 3개 + `GameSettingsSO` 필드 5개 + `SoundId` 3개 + `CookieAudio.TickPaint` | §26 H12 "손 색칠 삭제" 뒤 `GamePhaseState.IsPaintActive => false`로 **입구만 막음** |
| 괴물 대기실 대기(NetHold) | `NetHold` 203줄 + `HeldSyncRecorder` 25줄 + `InteractableDoor`·`GameManager`·`PlayerSpawner`·`RoomExitController`의 대기 분기·주석 + `MusicRules.LocalMonsterWaiting` + 대기실 씬 `MonsterWaitPanel` | 호출자 `MonsterLobbyWaitController`를 `Legacy~`로 옮김 → `NetHold.Begin()` 호출 0 |
| 마녀 처단(Purge) | `WitchTimeline` 5개 멤버 + `WitchShot.Approach` + `WitchRevealCamera` 2개 함수·분기 + `WitchPresenter` 분기 + `RoomLifecycleWatcher`·`MonsterRoster` 인자 | §25 개편 뒤 `WitchMode.Purge`를 **런타임 어디서도 쓰지 않음**(시험 코드만) |
| 옛 비탈출 결과 | `GameRuleController`·`ResultScreenController`·`PlayerResultRow`의 `MonsterWins/CookiesWin` 경로 | 모든 맵이 탈출 모드 |
| `Legacy~` 폴더 | 런타임 11개 파일(약 2,300줄) + 에디터 2개 | Unity가 컴파일하지 않는 보관 폴더 |

### 5.2 기존 책임 분리를 무시하는 코드가 있는가 — ⚠️ 몇 곳

| # | 근거 | 설명 | 권장 |
|---|---|---|---|
| A-01 | `GameManager/GameManager.cs` 전체 | 이름은 "게임 매니저"인데 **채팅만** 한다 | `ChatController`로 이름 변경(.meta GUID 유지) |
| A-02 | `ColorTag/CookieLifeStatePresenter.cs` | 쿠키 생사 표시가 색칠 폴더에 있음 | `Unit/`로 이동 |
| A-03 | `Escape/EscapeManager.cs` (475줄) | 상태 동기 + 뷰 갱신 + 요청 전송 + **지형 질의**(`SnapToGround`·`DropPointNear`·`IsValidTeleport`·`TryRandomMapPoint`) + 시각 상수 | 지형 질의를 `EscapeGeometry`(정적/순수)로 분리 |
| A-04 | `Escape/EscapeAuthority.cs` (1,232줄) | 20종 작업 처리 + 이동 감시 + 재료 안전장치 + 마녀 타이머 + 알림이 한 클래스 | `partial class`로 작업군(상자·건물·로켓·도구·AI·폭탄)별 파일 분리 — 동작 변화 없이 읽기 쉬워짐 |
| A-05 | `PlayerInput.BrushSizeDelta` → `PlayerInventory.cs:306` | 입력 이름이 옛 의미(붓 크기) | `SlotWheelDelta`로 이름 변경 |
| A-06 | `Interaction/CharacterInteractor.cs` `is MonsterController` | 인터페이스 기반 설계에 구체 타입 분기 | `IGameCharacter`에 "잡기 우선" 속성 추가 |
| A-07 | `UI/KeyBindingsPage`·`VoiceRecorderPanel`·`MonsterRoomGuard`·`EscapeCatalogSO` 등의 `Editor*` 메서드 | 시험 훅이 런타임 클래스 안에(`#if UNITY_EDITOR`로 감싼 것도 있고 아닌 것도 있음) | 모두 `#if UNITY_EDITOR`로 통일 |

### 5.3 Manager 간 의존성이 과도한가 — ⚠️ 허브 2개

| 대상 | 참조 파일 수 | 판정 |
|---|---:|---|
| `RoomState` | 56 | ✅ 읽기 전용 정적 도우미 — 허브여도 상태를 바꾸지 않아 괜찮다 |
| `NetKeys` | 51 | ✅ 상수 표 |
| `EscapeManager`(정적 멤버 포함) | **38** | ⚠️ `EscapeManager.ActionsAllowed`·`HasLeftMap`·`IsActive`가 Audio·Monster·Unit·Interaction·Camera까지 퍼졌다. 탈출 모드가 아닌 씬(대기실)에서도 이 정적 멤버를 부른다 |
| `EscapeManager.Instance` | 8 | ⚠️ 정적 싱글턴 접근(`ToolHitReceiver`, `WitchFinaleStage`, `MonsterRoomGuard` 등) |
| `GameSettings.Current` | 27 | ✅ 설정 SO |
| `GameAudio` | 27 | ✅ 파사드 |
| `CharacterRegistry` | 23 | ✅ |
| `EscapeAuthority` | 11 | ⚠️ 정적 상수(`BombMaxCharges`, `BombCooldown`, `InventorySlots`)를 HUD·인벤토리가 읽음 — 규칙 상수는 `EscapeRules`나 SO로 |

**순환 의존**: `EscapeManager` ↔ `EscapeHud`(HUD가 매니저 상태를 읽고 매니저가 HUD를 만듦), `EscapeManager` ↔ `EscapeAuthority`(서로 참조) — 같은 기능 묶음 안이라 심각하지 않다. 다른 묶음 사이 순환은 찾지 못했다.

**권장(A-08)**: 탈출 모드 밖에서도 묻는 질문(`ActionsAllowed`, `HasLeftMap`)을 `GamePhaseState`처럼 **방 속성만 읽는 정적 해석기**(`EscapeQuery`)로 옮기면 `EscapeManager` 의존이 절반 이하로 준다.

### 5.4 Prefab과 Script의 역할이 뒤섞이는가 — ⚠️ 런타임 조립이 많다

- `AddComponent<…>` **75곳 / 32개 파일**. 대표:
  - 쿠키 프리팹: `HideOrSeekPlayer.AttachEscapeMode`가 8개 컴포넌트를 붙임.
  - 괴물 프리팹: `MonsterController.Awake`가 4개(`StunReceiver`, `MonsterAudio`, `MonsterEscapeState`, `MonsterViewSwitcher`).
  - 마녀: `WitchPresenter.Awake`가 `WitchShatter`·`WitchFinaleStage`, 그리고 **`Camera.main`에 `WitchRevealCamera`** 를 붙임(`:345-347`).
  - HUD·패널: `EscapeHud`·`SkillHud`·`VoiceRecorderPanel`·`GrabAimReticle`·`CutsceneFade`·`ToolUser.CreateCrosshair`·`MonsterJarTrophy`가 **UI 전체를 코드로** 만든다.
- 장점: 프리팹·씬을 덜 건드려 병합 충돌이 적고, 탈출 모드가 아닌 씬에는 붙지 않는다.
- 단점: 인스펙터에서 보이지 않아 값 조정·디버깅이 어렵고, 디자이너가 HUD 배치를 바꾸려면 코드를 고쳐야 한다. 같은 UI 도우미가 6벌(5.12).
- **권장(A-09)**: "항상 붙는" 컴포넌트(쿠키의 `StunReceiver`·`CookieAudio`·`CarriedBodyPresenter`, 괴물의 4개)는 **프리팹에 미리 붙이고** `Init`만 코드로. 모드별 선택 부착(`PlayerInventory` 등)은 지금처럼 코드로 두어도 된다. HUD는 당장은 유지하되 UI 도우미를 하나(`UiFactory`)로 합친다.

### 5.5 Scene에 직접 의존하는 코드가 늘어나는가 — ⚠️

| 방식 | 횟수 | 위치 |
|---|---:|---|
| `GameObject.Find(이름)` | 7 | `PlayerSpawner.cs:51,69`, `MonsterJoinController.cs:28`, `EscapeManager.cs:415,422`(**호출마다**), `SceneSpawnPoints.cs:13`, `MonsterTestSpawner.cs:24`(💀) |
| `FindFirstObjectByType` 등 | 7 | `GameLobbyController.cs:183,215,297`(같은 대상 3번), `GameManager.cs:40`, `MonsterAssignmentAuthority.cs:64`, `VoiceRecorderPanel.cs:37`, `AudioListenerAnchor.cs:52` |
| `Camera.main` | 20 / 13개 파일 | 대부분 매 프레임(`ToolUser`, `QuadrantBuilding`, `PlayerBillBoard`, `HideOrSeekPlayer.CheckMovementInput`…). Unity 2020+는 내부 캐시라 성능 문제는 작지만, **"주 카메라가 하나"라는 씬 가정**이 퍼져 있다 |
| 이름 규칙 | 다수 | `Repair_Point`/`Socket_`/`ESC_Glow`(건물), `Slot_00_Empty/Filled`·`Flame`·`Hatch`(로켓), `Ground`/`Map`/`FarBackdrop`(마녀), `EyeSocket`·`Grab_Socket`(괴물), `Lid`(상자), `PlayerSpawnPos`/`MonsterSpawnPos`/`Spawn_Cookie_Q*` |

- 7차 감사(F-21) 뒤 레시피·오븐 문은 씬 직렬화 참조로 바뀌었고 이름 규칙은 예비로만 남았다 — 방향은 맞다.
- **권장(A-10)**: `EscapeManager`는 이미 `chestAnchors`·`buildings`·`exits`를 직렬화로 받으므로 **쿠키 스폰 지점도 직렬화 필드**로(매 호출 `GameObject.Find` 제거). `GameLobbyController`는 `RoomExitController`를 직렬화 필드 하나로.

### 5.6 Singleton이 남발되는가 — ⚠️ 16곳, 대부분 정당

| 종류 | 클래스 | 판정 |
|---|---|---|
| DDOL 자기 부트스트랩 | `AudioRuntime`, `RoomPropertyGuard`, `CharacterInteractor`, `NetHold`💀 | ✅ 앱 전역 서비스(NetHold만 죽음) |
| 씬 싱글턴(`Instance`/`Current`/`instance`) | `EscapeManager`, `EscMenu`, `EscapeHud`, `WitchPresenter`, `WitchShatter`, `VoiceChat`, `VoiceRecorderPanel`, `AudioListenerAnchor` | ⚠️ 모두 `OnDestroy`에서 `if (x == this) x = null` 처리 ✅. 그러나 `EscapeHud`·`ToolHitReceiver`·`WitchFinaleStage`가 서로를 정적 접근으로 찾음 — 시험에서 대체 불가 |
| "로컬 플레이어" 정적 | `PlayerInventory.Local`, `PlayerPaintCanvas.Local` | ⚠️ 관전·재접속 때 이전 값이 남지 않게 해제는 되어 있음 ✅ |
| 정적 상태(싱글턴 대용) | `ToolUser.Aiming`, `KeyBindingsPage.listening/confirming`, `RegionMove` 대기, `GrabKillAuthority` 사전, `InteractableDoor.masterPendingWrites` | ⚠️ 씬 로드 때 초기화하는지 개별 확인 필요 — `GrabKillAuthority`는 `sceneLoaded`로 초기화 ✅, `masterPendingWrites`는 방장 교체 시 비움 ✅ |
| SO 정적 캐시 | `GameSettings`, `EscapeCatalogSO.Current`, `EscapeTextsSO.Current`, `SoundCatalogSO`, `MusicCatalogSO`, `VoiceChatSettingsSO` | ✅ 읽기 전용 데이터 |

**권장(A-11)**: 새로 만들 때는 정적 `Instance` 대신 **`EscapeManager`가 하위 객체에 자신을 넘기는 방식**(지금 `Bind(owner)`·`Init(owner)` 패턴이 이미 있다)을 기본으로. 남은 정적 접근 8곳(`EscapeManager.Instance`)은 `Init`으로 넘길 수 있다.

### 5.7 ScriptableObject 책임이 잘못 쓰이는가 — ⚠️ 경미

- ✅ SO는 **데이터만**(아이템·도구 수치·레시피·문구·소리 표·조작키 기본값·롤러코스터 경로). 런타임 상태를 SO에 쓰는 곳은 없다(에디터 빌더 `Editor*` 세터 제외).
- ⚠️ 정적 `Current` 캐시를 SO 클래스 안에 둔다(`EscapeCatalogSO.cached`, `EscapeTextsSO.cached`) — 동작은 맞지만 "SO = 데이터, 로더 = 다른 클래스" 원칙(`GameSettings` ↔ `GameSettingsSO`)과 섞였다. `GameSettings` 방식으로 통일 권장.
- ⚠️ `EscapeTextsSO.Current`는 에셋이 없으면 `CreateInstance`로 영어 기본값을 만든다 — 조용히 실패를 숨긴다. 빌드에서 에셋 누락 시 경고 로그 권장.
- ❌ **옛 규칙 필드가 남아 있음**(6.4절): 인스펙터에 쓰이지 않는 칸이 보여 혼란. `[HideInInspector]` 또는 삭제(삭제해도 YAML 값은 무시되므로 에셋은 깨지지 않는다).
- ⚠️ `CoasterPathSO`처럼 굽는 데이터(에디터가 생성)와 손으로 조정하는 데이터(`ToolSO`)가 같은 `03. SO`에 섞여 있다 — 굽는 것은 `Generated` 하위 폴더 권장.

### 5.8 Unity Lifecycle 순서 문제가 있는가 — ✅ 대체로 안전, 1건 주의

| 확인 | 결과 |
|---|---|
| `[DefaultExecutionOrder]` | `CarriedBodyPresenter(-50)`: 들린 몸을 카메라보다 먼저 → 떨림 없음 ✅. `WitchRevealCamera(1000)`: 모든 카메라 뒤에 연출 섞기 ✅ |
| Awake 의존 | `Camera_Ctrl.Start`/`InitCamera` 순서 독립 ✅. 쿠키가 PhotonView 콜백 전에 부품을 준비(`Awake`) ✅ |
| `RuntimeInitializeOnLoadMethod` | 8곳 — `BeforeSceneLoad`(AudioRuntime·NetworkDefaults)와 `AfterSceneLoad`(Guard·Interactor·VoiceChat·VoiceRecorderPanel·GrabKillAuthority·NetHold) 구분이 의도대로 ✅ |
| SetActive로 숨기면 Photon 콜백이 끊기는 문제 | `CanvasGroupVisibility`로 해결 ✅(결과 화면·괴물 이탈 배너) |
| ⚠️ A-12 **FOV 쓰기 경합** | `ToolUser.Update`가 매 프레임 `cam.fieldOfView = baseFov × zoom`(`ToolUser.cs:44`), `WitchRevealCamera`(실행 순서 1000)가 연출 중 FOV를 정함. 연출 중에는 `WitchRevealCamera`가 나중에 써서 이기지만, **연출이 끝난 프레임에 `ToolUser`의 `baseFov`가 연출 전 값인지**에 기대고 있다. 또 `ToolUser`가 처음 본 FOV를 `baseFov`로 저장하므로, 괴물 1인칭(근접 클립·FOV 다름)으로 바뀐 뒤 쿠키로 관전하는 경우 등 기준값이 섞일 수 있다. → 확대는 `Camera_Ctrl`이 소유하고 `ToolUser`는 "조준 중" 플래그만 주는 구조 권장 |
| `OnDestroy`에서 정적 해제 | 확인한 모든 싱글턴 ✅ |

### 5.9 Event 구독/해제가 제대로 되는가 — ✅

전수 스캔(`+=` 메서드 그룹·람다 → 같은 파일 `-=` 확인, Awake/Start 구독 ↔ OnDisable 해제 비대칭 검사) 결과:

| 해제가 없는 구독 | 판정 |
|---|---|
| `EscMenu.cs:44,50` `roomSettings.Closed += Open`, `settingsMenu.Closed += Open`; `LobbyEscMenu.cs:33` | ✅ 같은 오브젝트(자식 패널)라 함께 파괴 — 누수 아님 |
| `NetHold.cs:56` `SceneManager.sceneLoaded += …` | 💀 DDOL·죽은 코드 |
| `EscapeVisuals.cs:93` `sceneUnloaded += _ => tintCache.Clear()` | ✅ 정적 한 번(가드 플래그) |
| `EscapeHud.cs:61` `owner.StateChanged += instance.RefreshRequirements` | ✅ `OnDestroy`에서 `manager.StateChanged -= RefreshRequirements` |
| 정적 이벤트 8개(`SpectatorController` 2, `WitchPresenter.Impacted`, `VoiceChatSettings/InputDevices/KeyBindings/OutputDevices/AudioVolumeSettings.Changed`) | 구독자 모두 `OnEnable/OnDisable` 또는 `OnDestroy` 짝 ✅ |

Awake에서 구독하고 OnDisable에서 해제(다시 켜지면 끊김) 같은 비대칭은 **0건**.

### 5.10 Object Pool과 Instantiate/Destroy가 충돌하는가 — ⚠️ 충돌은 없음, 방식 혼재·해제 누락 3건

| 풀을 쓰는 곳 | 풀 없이 생성/파괴 |
|---|---|
| 소리 보이스(사전 생성) · `WitchShatter` 조각(`Stack<Piece>`) · `GroundItemView`(바닥 아이템 뷰) · `StunReceiver` 별(한 번 만들어 켜고 끄기) | `ToolFx`·`ToolShotFx`(명중마다), `AlarmBeacon`, `NoiseBombView`, `QuadrantBuilding` 힌트·넣은 재료 모델, `SpyRocket` 칸 모델, `GroundItemView` 안의 **모델은 아이템이 바뀌면 Destroy 후 재생성**, `GameLobbyController` 목록 행, `ResultScreenController` 행 |

- 풀 객체를 `Destroy`하거나 파괴된 것을 풀에 돌려주는 **충돌은 없다**.
- ⚠️ A-13 **파티클 인스턴스 해제를 코드가 하지 않는 곳**: `WitchFinaleStage.SpawnCrumbs`(`:385`), `DecoyView.SpawnCrumbs`, `CookieLifeStatePresenter`의 가루 `Instantiate(BreakVfx…)` — 프리팹 `ParticleSystem`의 **Stop Action = Destroy**에 기대고 있다. 프리팹 설정이 바뀌면 오브젝트가 쌓인다. → `Destroy(burst, 수명)`을 코드로 보장 권장.
- ⚠️ A-14 **런타임 재질 해제 누락**: `StunReceiver` 감옥 풍선 재질(`EscapeVisuals.NewMaterial`, 캐릭터당 1개), `WitchPresenter` 자리표시 마녀 재질(모델이 있으면 안 쓰임). 판마다 수 개 수준이라 영향은 작다.
- 명중 효과는 짧고 드물어 풀이 꼭 필요하지는 않다. 다만 `ToolFx`는 `CreatePrimitive`(메시 콜라이더 생성 후 바로 삭제)라 **풀 하나(8개)** 로 바꾸면 GC·물리 등록 비용이 사라진다.

### 5.11 Photon의 Ownership/RPC 구조를 무시하는가 — ✅ 잘 지킴

| 확인 | 결과 |
|---|---|
| `PhotonNetwork.Instantiate` | 4곳 — 쿠키(본인), 괴물(본인, `MonsterJoinController`), 시험 도구(💀). 모두 **자기 캐릭터만 자기가 생성** ✅ |
| `PhotonNetwork.Destroy` | 1곳 — 방장이 가짜 괴물 제거(`MonsterController.cs:161`). PUN은 방장의 남의 오브젝트 파괴를 허용 ✅ |
| Ownership 이전 | 0곳 — 들기도 소유권을 옮기지 않고 RPC + kinematic 따라가기로 처리 ✅(소유권 경합 없음) |
| `[PunRPC]` | 5개(`HideOrSeekPlayer` 4, `GameManager` 1) — **모두 `PhotonMessageInfo.Sender` 검증** ✅ |
| `RaiseEvent` | 15곳 — 요청은 `ReceiverGroup.MasterClient`, 방장 → 모두는 `Others/All`, 개인 통지는 `TargetActors` ✅. 받는 쪽은 `photonEvent.Sender`로 위장 방지 ✅ |
| Room Prop 쓰기 | 방장 키는 `AuthorityProps` 한 통로(23곳). 직접 `CurrentRoom.SetCustomProperties`는 `AuthorityProps` 자신과 `OfflineModeBootstrap`(시험)뿐 ✅ |
| Player Prop 쓰기 | 본인 키(`SelectedSlot`, `HasVoice`, `SkinIndex`)만 `LocalPlayer.SetCustomProperties` 6곳 ✅ |
| `IsMasterClient` 분기 | 39곳/21파일 — 방장 교체 시 `Adopt`·`MasterClientPolicy`가 이어받음 ✅ |
| ⚠️ 남은 것 | `PlayerPaintCanvas`가 `PaintStroke`·`ClearColor` 이벤트를 보낼 수 있는 코드가 남아 있음(💀 경로지만, 받는 쪽 검증은 있음). `NetHold.HoldingPool`이 `PhotonNetwork.PrefabPool`을 감싸 **모든 PhotonView 생성 경로에 불필요한 층**을 넣는다 |

### 5.12 중복 로직이 있는가 — ⚠️

| # | 중복 | 위치 | 권장 |
|---|---|---|---|
| D-01 | **액터 번호로 캐릭터 찾기** | `CharacterRegistry.FindByOwner`(정식, 3곳 사용) vs `VoiceChat.BodyOf`, `EscapeAudioPresenter.PlayOnActor`, `ToolHitReceiver.HandOf`, `DecoyView.FindMonster` | 모두 `FindByOwner`로 |
| D-02 | **내 캐릭터 찾기** | `CharacterInteractor.FindLocalCharacter`, `SkillHud.LocalCharacter`, `MonsterRoomGuard.LocalEscaper`, `MonsterRoomDoor.LocalMonster`, `NoiseBombPresenter.Update`, `ChaseDirector` | `CharacterRegistry.FindLocal(role?)` 추가 |
| D-03 | **ESC 키 직접 읽기** | `EscMenu:63`, `LobbyEscMenu:39`, `SettingsMenu:41`, `SoundSettingsPanel:114`, `KeyBindingsPage:126,133` | `PlayerInput.CancelPressed` 하나(조작키 화면의 소비 규칙 포함) |
| D-04 | **코드로 UI 만들기** | `EscapeHud.NewRect/NewImage/NewText/WhiteSprite`(+`SkillHud`가 재사용), `VoiceRecorderPanel.NewButton`, `ToolUser.CreateCrosshair.Bar`, `MonsterJarTrophy.Rect/Img/Text`, `GrabAimReticle`, `CutsceneFade` | `UiFactory` 정적 클래스 하나 |
| D-05 | **레지스트리** | `CharacterRegistry` ↔ `InteractableRegistry` (같은 `List` + `Register/Unregister/IsAlive`) | `Registry<T>` 제네릭 |
| D-06 | **애니메이션 상태 전환** | `PlayerAnimationDriver.ChangeState` ↔ `MonsterController.ChangeState`(enum 이름 = 트리거 이름, 이전 상태 기록, 이벤트) | `AnimatorStateDriver<TState>` 제네릭 |
| D-07 | **목록 UI 다시 그리기** | `UiListBuilder`(정식) vs `GameLobbyController` 목록 직접 Destroy/Instantiate | `UiListBuilder` 사용 |
| D-08 | **"Register on enable" 상용구** | `QuadrantBuilding`, `SpyRocket`, `MaterialChest`, `RainbowChest`, `NoiseBombView`, `GroundItemView`의 `registered` 플래그 + `SetRegistered` | 추상 기반 `RegisteredInteractable : MonoBehaviour` |
| D-09 | **들린 쿠키 손 소켓 따라가기** | `CookieLifeStatePresenter`, `DecoyView.TickCaught`, `WitchFinaleStage.LateUpdate` — 같은 `ProjectOnPlane(monster − socket)` → `LookRotation` → `socket − up·bodyCenter` 계산 | `CarryPose.HeldBySocket(...)` 순수 함수로 |

---

## 6. 미사용 코드 목록

> 판정 방법: ① 이름 기반 전수 집계(선언 7,631개, 주석 제거) → ② 씬·프리팹·SO의 스크립트 GUID 참조(`usage.tsv`) → ③ UnityEvent(버튼)·애니메이션 이벤트·`[RuntimeInitializeOnLoadMethod]`·인터페이스 구현·리플렉션 사용을 제외 → ④ 남은 후보를 직접 열어 확인. **"거짓 양성"으로 제외한 것**: `AudioRuntime.Boot`·`NetworkDefaults.ApplyRates`(자동 실행), `UiSound.OnPointerEnter`·`PaintColliderUpdater.Execute`(인터페이스), `LobbyController.On…ButtonClicked` 3개·`GameLobbyController.OnStartGameButtonClicked`·`LobbyEscMenu.OpenSettings`(씬 버튼), 씬·프리팹에 붙은 컴포넌트 클래스 20개(`PhaseCountdownDisplay`, `GamePhaseStarter`, `SpectatorLabel`, `PlayerBillBoard` 등), `HashtableCopyExtensions`(확장 메서드 `Merged`로 사용).

### 6.1 클래스·파일 (실행되지 않음)

| 대상 | 근거 | 조치 |
|---|---|---|
| `Dev/MonsterTestSpawner.cs` | 씬·프리팹·코드 참조 0, 대상 씬 `PlayerTestScene` 삭제됨 | 삭제 |
| `Core/NetHold.cs` + `Core/HeldSyncRecorder.cs` | `Begin()/Discard()` 호출 0 → `IsHolding` 늘 false. 그런데 `HoldingPool`이 PrefabPool을 감쌈(`NetHold.cs:55`) | 삭제 + 관련 분기 제거(아래 6.2) |
| `Escape/Legacy~/*` 10개, `Monster/Legacy~/MonsterLobbyWaitController.cs`, `Editor/Escape/Legacy~/EscapeMapSetup_Old.cs`, `Editor/Tests/Legacy~/EscapeReachabilityTests.cs` | 컴파일 안 됨 | git 기록에 있으므로 삭제 권장(보관이 필요하면 저장소 밖) |
| `ColorTag/PaintColliderUpdater.cs` | 색칠 충돌체 — Tick 조건이 늘 거짓, **그래도 로컬 쿠키마다 생성·Mesh 2개 할당** | 생성 코드와 함께 삭제 |

### 6.2 메서드·속성·필드 (죽은 경로 포함)

| 파일:줄 | 멤버 | 상태 |
|---|---|---|
| `GamePhaseState.cs:31` | `IsPaintActive => false` | 💀 입구 막음 — 이걸 보는 모든 분기가 죽음 |
| `PlayerPaintCanvas.cs` | `HandlePaintInput`, `StampAtScreenPoint`, `TryRegisterSlotAndStamp`, `ReportSlotCount`, `SetBrushColor`(321), `SetEraseMode`(327), `ResetCanvas`(332), `SendResetEvent`, `IsColliderRefreshWanted`, `IsPointerOverBounds`, `InPaintWindowNow`(475), `ApplyForcedColorIfAssignedToMe`, `LateStrokeGrace`(417), `PaintableCollider`/`CurrentBrushRadius`/`CurrentBrushColorIndex`/`RegisteredColorSlots`(47-51), `pendingStrokeCounts`, `registeredColorSlots`, `minStrokesToRegister`, `stampSpacing`, `maxStampsPerFrame`, Erase 종류, `ClearColor` 송수신 | 💀 |
| `CookieAudio.cs:66-70` | `TickPaint` (`LastStampTime`이 바뀌지 않음) | 💀 |
| `InteractableDoor.cs` | `NetHold.IsHolding` 분기, `LobbyDoorState` 이벤트 송수신(`:145`), 메시지 큐 정지 분기 | 💀 |
| `GameManager.cs`, `PlayerSpawner.cs`, `RoomExitController.cs` | `IsMessageQueueRunning = true`·KeepAlive 복구·`MonsterLobbyWaitController` 주석 | 💀 방어 코드 — 무해하지만 오해 유발 |
| `HideOrSeekPlayer.cs:210` | `if (pv == null) return; // … NetHold` 주석 | 낡은 설명 |
| `MusicRules.cs:9,57` / `MusicDirector.cs:37` | `LocalMonsterWaiting`(늘 false) | 💀 |
| `WitchTimeline.cs` | `PurgeAppearAt`, `PurgeStrikeAt`, `PurgeWalkSeconds`, `ShouldStartPurge`, `WalkStart/WalkEnd`·`ShotAt`의 Purge 분기, `CookieDiesInStrike`의 Purge 분기 | 💀 (시험에서만 Purge 사용) |
| `WitchRevealCamera.cs` | `ApproachPose`, `ApproachEyeHeightFor`, `:274` Purge 분기, `WitchShot.Approach` 처리 | 💀 |
| `WitchPresenter.cs:259` | `Facing`의 Purge 분기; `FadeAlpha`(🧪) | 💀/🧪 |
| `RoomLifecycleWatcher.cs:28` / `MonsterRoster.ReturnWhenMonstersGone` | `witchPurging` | 💀 늘 false |
| `GameRuleController` / `ResultScreenController` / `PlayerResultRow` | 비탈출 승패(`MonsterWins`/`CookiesWin`), `SetMonster(name)`, `SetCookie` | 💀 모든 맵이 탈출 모드 |
| `MonsterAssignmentAuthority.cs:77-81` | `ClearLegacyDeadline` | 💀 쓰는 곳 없는 키 지우기 |
| `RoomState.cs:105` | `CanSelectMonster` | 💀 |
| `GameStartAuthority.cs:128` | `CurrentMapScene` | 💀 |
| `EscapeManager.cs:46,78,79` | `MapRect`, `BuildingView`, `ExitView` | 💀 |
| `EscapeNet.cs:6` | `DecodeSlot` | 💀 |
| `PlayerInventory.cs:130` | `RequestDropAll` | 💀 |
| `GameAudio.cs:63,79` | `CurrentMusic`, `StopMusic` | 💀 |
| `AudioVoicePool.cs:82` | `CountBusy` | 💀 |
| `VoiceChat.cs:26-27` | `IsTransmitting`, `SpeakerCount` | 💀 |
| `InputDevices.cs:16` | `HasAny` | 💀 |
| `NetHold.cs:42` | `ParkedCount` | 💀 |
| `CoasterPathSO.cs:23,30` | `Gauge`, `DistanceAt` | 💀 |
| `VikingRide.cs:34` | `CurrentAngle` | 💀 |
| `CauldronGlow.cs:12` | (속성 `ColorUsage`는 특성 — 거짓 양성) | — |
| `KeyBindingsPage.cs:36` | `IsConfirmingReset` | 💀 |
| `SettingsMenu.cs:27` | `CurrentTab` | 💀 |
| `RoomSettingField.cs:72` | `StepBy` | 💀 |
| `SkinCatalogSO.cs:26` | `GetModel` | 💀 |
| `WitchPresenter.cs:86` | `FinaleSince` | 💀 |
| `ColorPaletteSO.cs:18` | `ColorEntry.colorName` | 💀 |
| `PlayerInput` | `PaintHeld`, `PaintButton` | 💀 |

### 6.3 시험·에디터에서만 쓰는 런타임 멤버 (🧪, 지우면 시험이 깨짐)

`VoiceRecorderPanel.EditorFakeRecording`, `KeyBindingsPage.EditorBind/EditorKeyText`, `SoundSettingsPanel.PickOutput/HasOutputDropdown`, `VikingRide.EditorSetPhase`, `CoasterRide.EditorPlaceAt/TrainCount/Train`, `CoasterTrain.CarCount/CarBody`, `CoasterPathSO.TimeAt/EditorSet`, `EscapeManager.EditorSetMapRadius/EditorBind`, `MonsterRoomGuard.EditorBind/InnerHalf`, `ChaseDirector.Intensity`, `ChaseAudioSettingsSO.EditorSetRadius`, `AudioVolumeSettings.ToDecibels`, `AudioVoicePool.SoundAt`, `MusicCatalogSO.IsLoopById`, `HatFlight.NearestEdgeDirection`, `EscapeState.ActiveBuildingCount`, `PlayerAnimationDriver.IsCarryPoseOn`, `WitchPresenter.FadeAlpha`, `WitchTimeline.PurgeAppearAt/ShouldStartPurge`, `GameSettingsSO.MinCookies/GameMapScenes`(빌더). → 유지하되 `#if UNITY_EDITOR`로 감싸는 것을 권장(5.2 A-07).

### 6.4 ScriptableObject 필드·문구

| SO | 쓰이지 않는 필드 | 근거 |
|---|---|---|
| `EscapeRecipeSO` | `mode`, `groups`, `counterLabel`, `counterItems`, `fillsCapacity`, `spyToolbox`, `extraToolboxesPerSpy`, `MaxRequired`(+`RecipeMode`·`SlotGroup`), `exitKind`(에디터만) | 툴팁에 "예전 규칙(쓰지 않음, 에셋 호환)"이라 명시. 런타임 읽기 0 |
| `EscapeCatalogSO` | `hammer`/`Hammer`(에디터만), `spyChestTools`, `hammersPerCookie` | "개편 뒤 쓰지 않음" |
| `ItemSO` | `fitsAnyRocketSlot` | 읽기 0 (수리키트는 `EscapeCatalogSO.RepairKit`로 판별) |
| `GameSettingsSO` | `maxColorSlots`, `paintColliderRefreshInterval`, `paintStrokeSendRate`, `paintStrokeReferencePlayers`, `minPaintStrokeSendRate` (+`PaintStrokeSendIntervalFor`) | 색칠 경로에서만 |
| `EscapeTextsSO` 문구 | `escapeDevice`, `cannotEnter`, `witchTimerFormat`, `boardingFormat`(어디서도 안 씀); `repairing`, `noiseBombLabel`, `decoyCatch`, `exitLabel`, `recorded`, `departing`(에디터 빌더가 값만 채움) | 런타임 읽기 0 |

### 6.5 열거값

| enum | 쓰이지 않는 값 | 비고 |
|---|---|---|
| `SoundId` | `CakeRumble`, `CakeBurst`, `CakeRocketRise`, `CakeIgnite`, `CakeLaunch`, `CoasterBulbOn`, `CoasterSparks`, `CoasterLapBar`, `CoasterDepart`, `OvenPiston`, `OvenDoorRoll`, `OvenFlash`, `TrainPuff`, `TrainWhistle`, `TrainChug`, `TrainTunnel`, `AltarGlow`, `PortalClose`, `UiSlotSelect`, `UiChat` (20개) | 앞 18개는 `Legacy~` 탈출 시퀀스 전용 → **카탈로그 항목과 오디오 클립도 빌드에 들어간다**(Resources 카탈로그가 참조). 코드엔 없지만 씬 직렬화로 쓰는 값(`Amb*`, `CarouselSpin`, `OvenGears`, `LanternFlicker` 등)은 사용 중이므로 제외 |
| `SoundId` (코드에선 시험만) | `PaintPickColor`, `PaintErase`, `PaintReset`, `MonsterDeparted`, `BoardHop`, `BoardSuck`, `UiCancel`, `UiStart` | 코드 호출은 시험뿐 — 씬·프리팹 직렬화 값(`AmbientEmitter.sound` 등)으로 쓰이는지 정리 전에 확인 필요 |
| `MusicId` | `CandyForestHunt`, `GingerbreadHunt`, `FactoryHunt`, `CarnivalHunt`, `BakeryHunt`, `TimeAttack` | 주석에 "ID 번호를 지키려고 남김" — 의도적. 단 카탈로그 클립이 있으면 빌드 용량 |
| `EscapeNoticeKind` | `StolenToRocket` | |
| `DeathCause` | `None` | 기본값 의미로 유지 가능 |
| `WitchMode` / `WitchShot` | `Purge` / `Approach` | 6.2 |
| `PhaseCountdownDisplay.CountdownPhase` | `Survival` | 씬 직렬화 호환 |
| `NetKeys` | `ForcedPaintActorNumbers`, `ForcedPaintColors`(쓰는 곳 0), `RegisteredSlotCount`(색칠), `MonsterRevealTime`(쓰기만, 읽기 0), `MonsterSelectDeadline`(옛 정리용) | `Scopes` 표에서도 함께 제거 |
| `NetEventCodes` | `LobbyDoorState`(13), `PaintStroke`·`ClearColor`(💀 경로) | 번호는 비워 두고 재사용 금지 주석 |

---

## 7. 미사용 에셋

> 기준: **빌드에 실제로 들어가는 것** = 빌드 목록에서 켜진 씬 7개(`LobbyScene`, `GameLobbyScene`, `CandyForest`, `CursedCandyCarnival`, `Test_GingerbreadVillage`, `Test_ChocolateFactory`, `Test_HauntedBakery` — 맵 순환 `GameMapScenes`와 정확히 일치) + `Resources/` 아래 모든 에셋을 뿌리로 `AssetDatabase.GetDependencies(…, recursive)`. 결과 1,779개가 "사용". 아래는 그 밖의 것을 **누가 참조하는지**로 다시 나눴다. Photon·TextMesh Pro 패키지 데모·셰이더는 제외(패키지 소유).

### 7.1 FBX (프로젝트 177개 · 61 MB 미사용)

| 분류 | 개수 | 예 | 판정 |
|---|---:|---|---|
| **어디서도 참조 없음(고아)** | 약 120 | `Animation/Cookie/Cookie_Dodge.fbx`·`Cookie_Idle.fbx`·`Cookie_StandUp.fbx`(각 6.4~6.6 MB, **합 19.5 MB**), `Maps/*/Models/*_Effects.fbx` 5개, `09. Environment/Escape/Models/ITEM_CandyCell_{Green,Orange,Red,Yellow}.fbx`, `ESC_CursedCandyCarnival.fbx`, `Maps/CursedCandyCarnival/Models/Viking/VK_Viking.fbx`(받은 선체로 교체됨), `…/GyroDrop/GD_GyroDrop.fbx`, `…/Floor/FL_*.fbx` 8개 | 애니메이션 3개·`_Effects`·`VK_Viking`은 **삭제 후보**. 나머지 일부는 빌더 입력(아래) |
| ↳ 그중 **에디터 빌더가 경로로 읽는 원본** | — | `Maps/Common/Models/*.fbx` **97개(7.2 MB)** ← `MapSceneBuilder`·`EscapeModelImportPostprocessor`; `FL_*` ← `CarnivalMapBuilder`; `GD_GyroDrop` ← `CarnivalRideModelsImporter`; `ITEM_CandyCell_*` ← `CandyForestAssets` | **맵을 다시 만들 때만 필요**. `MapSceneBuilder`는 삭제된 `PlayerTestScene`을 틀로 써서 지금은 실행 불가(README 기록) → 옛 맵 파이프라인 전체를 보관 여부 결정(8장 Q2) |
| 빌드 제외 `Game_*` 씬만 참조 | 27 + 1 | `ESC_CandyForest/Gingerbread/HauntedBakery/ChocolateFactory.fbx`, `CandyForest_Decoration/GameplayProps/MainStructures.fbx`, `ChocolateFactory_*`, `CursedCandyCarnival_*` 4, `GingerbreadVillage_*` 3, `HauntedBakery_*` 3 | `Game_` 씬을 지우면 함께 고아 |
| `Original/` 씬만 참조 | 16 | `*_Ground.fbx`, `*_Water.fbx`, `*_Lighting.fbx`, `*_Terrain.fbx`, `09. Environment/Escape/Models/WITCH.fbx`(옛 마녀) | `Original/` 씬과 함께 |
| 미사용 프리팹만 참조 | 20 | `09. Environment/Twisted/Models/TW_*.fbx` 18개, `CF_Tile_Grass.fbx`, `Ground_Floor_Tile.fbx` | 프리팹과 함께 |

### 7.2 Prefab (프로젝트 25개 미사용)

| 프리팹 | 참조 | 판정 |
|---|---|---|
| `04. Prefabs/Twisted/TW_*.prefab` **18개**(망가진 소품·숨을 곳 텐트/진저 집) | 빌드 제외 `Game_*` 씬에서만 사용 | 지금 맵(CandyForest·놀이공원·Test_ 3개)에는 없음. `TwistedPropPlacer`로 다시 배치할 계획이 없으면 삭제 후보 |
| `04. Prefabs/Maps/CandyForest/CF_Lollipop_{Blue,Green,Red,Yellow}.prefab` | 코드 `CandyForestAssets`·`CandyForestLayout`만 이름으로 참조, 씬 배치 0 | 빌더 재실행용 |
| `04. Prefabs/Maps/CandyForest/CF_Tile_Grass.prefab` | 없음 | 삭제 후보(새 초록 바닥으로 교체됨) |
| `04. Prefabs/Environment/WitchCookieProps/Ground/Ground_Floor_Tile.prefab` | 없음 | 삭제 후보 |
| `04. Prefabs/UI/SoundSettingsPanel.prefab` | 없음(설치 도구 `SoundSettingsInstaller`만 경로 문자열) | §27 `SettingsMenu`로 대체 — 삭제 후보(컴포넌트 클래스는 `SettingsMenu.prefab` 안에서 아직 사용) |

### 7.3 씬

| 씬 | 크기 | 상태 | 판정 |
|---|---:|---|---|
| `Scenes/Maps/Game_{CandyForest,ChocolateFactory,CursedCandyCarnival,GingerbreadVillage,HauntedBakery}.unity` | 8.7 MB | 빌드 목록에 있으나 **꺼짐** | 옛 판 맵(§25 이전). `Test_*`·새 실제 맵으로 대체 — 보관 여부 결정(Q1) |
| `Scenes/Maps/Original/*.unity` 5개 | **24.4 MB** | 빌드 목록에 없음 | `MapCompactor` 이전 원본 — 삭제 후보 |
| `Scenes/Maps/Test/Test_CandyForest.unity`, `Test_CursedCandyCarnival.unity` | 4.7 MB | 빌드 목록에서 꺼짐 | ⚠️ **삭제 금지** — 실제 맵의 **원본 틀**이다. `CandyForestMapBuilder`(`SourceScene`, `:19,51`)와 `CarnivalMapBuilder`(`:82-84`)가 실행될 때마다 이 Test 씬을 `CopyAsset`으로 복사한 뒤 그 위에 실제 맵을 짓는다(카메라·관리자·마녀·환경음은 Test 씬 그대로). 지우면 실제 맵을 다시 만들 수 없다. 빌드에서 뺀 것은 의도된 상태 |
| `Scenes/SampleScene/NavMesh.asset` | 작음 | 씬 없이 내비메시만 | 삭제 |

### 7.4 빌드 씬 안의 필요 없어진 오브젝트

| 씬 | 오브젝트 | 수 | 근거 | 판정 |
|---|---|---:|---|---|
| 모든 맵 5개 | `MapLighting/PointLights` (**꺼진 채로 저장**) | Test_Gingerbread 391 · Test_Chocolate 72 · Test_Bakery 80 · CandyForest 394 · Carnival 112 (자식 포함) | 빌더(`TestMapBuilder:115`, `CandyForestMapBuilder:637`, `CarnivalMapBuilder:921`)가 "옛 맵 점광원은 새 맵과 안 맞아 끈다"며 비활성화만 함. 코드에서 다시 켜는 곳 0 | **삭제 후보** — 씬 용량·로딩·에디터 탐색 비용. 빌더는 `Find` 실패 시 그냥 넘어가므로 지워도 안전 |
| `CursedCandyCarnival` | `MapLighting/ReflectionProbes/Probe_CUR_Water_CursedPond`, `Probe_CUR_Water_GlowPond_West` (꺼짐) | 2 | 빌더가 "옛 연못 자리"라 끔 | 삭제 후보 |
| `CursedCandyCarnival` | `MO_Fence/COL` — **컴포넌트가 Transform뿐인 빈 오브젝트** | **342** | 충돌체가 부모로 옮겨진 뒤 남은 껍데기로 보임 | 삭제 후보(빌더 쪽 생성 코드 확인 후) |
| `GameLobbyScene` | `GameLobbyUICanvas/MonsterWaitPanel` (꺼짐) | 1(+자식 1) | 옛 괴물 대기실 대기 UI, 코드 참조 0 | 삭제 |
| `CandyForest` | 꺼진 `MeshRenderer` 420개(`COL_Mesh` 368, `COL_Shell` 43 …) | 420 | **충돌 전용 메시**라 렌더러를 끈 것 — 정상. 다만 렌더러 컴포넌트 자체가 불필요 | 선택: 렌더러 제거(조금의 메모리) |
| `CandyForest` | `Map/Edge/CF_MonsterHouse (1)`, `CF_Tent (1)` | 2 | 이름이 복제본 기본 이름 — 의도된 배치인지 확인 | 확인 후 이름 정리 |
| 모든 게임 씬 | `GameSceneCore/Canvas/ResultScreen`, `EscMenu`, `InputFieldChat`, `…/RowTemplate`·`…/Template` (꺼짐) | — | 실행 중 켜지는 UI·템플릿 | ✅ 정상 |
| 모든 씬 | Missing Script | **0** | | ✅ |

### 7.5 그 밖의 파일

| 대상 | 크기 | 판정 |
|---|---:|---|
| `Assets/Screenshots/` 57장 | 14.1 MB | 작업 기록 스크린샷 — 프로젝트 밖(또는 `Plan.md/images/`)으로 이동 |
| `Assets/07. Expression/얼굴*_*.png` 20장 | 작음 | 표정 텍스처 — 어디서도 안 씀 |
| `Assets/08. Resources/LobbySceneBackGround.png` | 2.2 MB | 안 씀 |
| `Assets/08. Resources/ItemIcons/Icon_CandyCell_Orange.png` | 작음 | 안 씀(다른 색 아이콘은 사용) |
| `Assets/Animation/Monster/Monster_Rigged_textures/` 3장 | 9.9 MB | 안 씀(같은 폴더 2장은 사용) |
| `Assets/Animation/Cookie/Cookie_Dodge_Original.anim`, `Cookie_Dodge_Retimed_Backup_PreRootFix.anim` | 2.7 MB | 백업 — 삭제 |
| `Assets/Maps/Common/Doors/*.controller` 2개, `Maps/Common/Animations/*.anim` 3개 | 작음 | 옛 문 |
| `Assets/05. Materials/Lobby_{Crate,Fence,Grass,Parasol}.mat`, `TestGround.mat`, `02. Scripts/ColorTag/Shaders/ColorReplaceMaterial.mat` | 작음 | 안 씀 |
| `Assets/03. SO/Maps/CandyForestLayout.asset` | 작음 | 빌더 입력(코드가 이름으로 읽음) |
| `Assets/Maps/*/Compact/*.asset` 42개 | 27 MB | 빌드 제외 `Game_`/`Test_` 씬의 압축 메시 — 그 씬들과 함께 |
| `Assets/09. Environment/TestMaps/Materials/{CandyForest,CursedCandyCarnival}/*` 25개 | 작음 | 빌드 제외 Test 씬 전용 — 그 Test 씬이 실제 맵의 원본 틀이므로 **유지** |
| `Assets/02. Scripts/Player.md` (33 KB) | — | 스크립트 폴더 안 문서 → `Plan.md/archive/` |
| `~` 폴더: `09. Environment/CandyForest/Source~` **429 MB**(받은 원본 FBX·텍스처), `Maps/CursedCandyCarnival/Source~` 189 MB, `Maps/Source~` 162 MB(.blend·파이썬 빌드 스크립트), `10. Audio/Source~` 125 MB(합성 스크립트·후보 음원) — **합 약 905 MB** | 큼 | Unity가 가져오지 않으므로 빌드·임포트 영향 없음. **저장소 용량만** — Git LFS 또는 저장소 밖 보관 권장 |

---

## 8. 권장 조치 (우선순위)

> 모두 **제안**이다. 실제 작업은 사용자 승인 뒤 `Plan.md`에 별도 계획서를 만들어 단계별로 진행한다(규칙: 계획 → 승인 → 실행 → 컴파일·Console·Play Mode 확인).

| 순위 | 묶음 | 내용 | 위험 | 효과 |
|---|---|---|---|---|
| **P1** | 죽은 시스템 제거 ① NetHold | `NetHold`·`HeldSyncRecorder` 삭제, `InteractableDoor`·`GameManager`·`PlayerSpawner`·`RoomExitController`·`HideOrSeekPlayer` 대기 분기/주석, `MusicRules.LocalMonsterWaiting`, 대기실 `MonsterWaitPanel` | 낮음(호출 0 확인) | PrefabPool 감싸기 제거, 약 350줄 |
| **P1** | ② 손 색칠 | `PlayerPaintCanvas` 색칠 입력·전송·Forced·Erase·Clear 경로, `PaintColliderUpdater`, `GamePhaseState.IsPaintActive`, `CookieAudio.TickPaint`, 관련 `NetKeys`·`NetEventCodes`·`GameSettingsSO` 필드·`SoundId`. **남길 것**: `ForceFillColor`·`ApplyRemote(ForceFill)`·`SampleSurfaceColors`·스킨 | 중간(물풍선 색·가루 색 시험 필요) | 약 450줄 + 쿠키마다 Mesh 2개 할당 제거 |
| **P1** | ③ 마녀 처단(Purge) | `WitchTimeline`/`WitchRevealCamera`/`WitchPresenter`/`RoomLifecycleWatcher`/`MonsterRoster`의 Purge·Approach, 관련 시험 갱신 | 중간(시험 수정) | 약 150줄, 연출 코드 단순화 |
| **P1** | ④ 작은 죽은 멤버 | 6.2 표의 나머지(약 30개), `MonsterTestSpawner`, `Legacy~` 폴더들 | 낮음 | 정리 |
| **P2** | 에셋 정리 | 7.1~7.5의 "삭제 후보"(고아 FBX·백업 anim·스크린샷·표정 PNG·`Original/` 씬·`SampleScene`·꺼진 `PointLights`·빈 `COL` 342개·꺼진 프로브) | 낮음~중간(씬 저장) | 저장소 약 80 MB↓, 씬 로딩·에디터 탐색 개선 |
| **P2** | 중복 통합 | D-01·D-02(`CharacterRegistry.FindLocal/FindByOwner` 일원화), D-03(ESC 입력 하나), D-07(`UiListBuilder`) | 낮음 | 버그 표면 감소 |
| **P2** | 안전성 | A-12 FOV 소유권 정리, A-13 가루 파티클 `Destroy(burst, t)` 보장, A-14 런타임 재질 해제, `EscapeTextsSO` 에셋 누락 경고 | 낮음 | |
| **P3** | 구조 | A-01 `GameManager`→`ChatController`, A-02 폴더 이동, A-03/A-04 `EscapeManager` 지형 분리·`EscapeAuthority` partial 분할, A-05 입력 이름, A-08 `EscapeQuery`, A-09 상시 컴포넌트 프리팹화, D-04 `UiFactory`, D-05/D-06 제네릭화 | 중간(이동·이름 변경은 .meta GUID 유지 필요) | 가독성·확장성 |
| **P3** | 문서·주석 | `Camera_Ctrl` 낡은 주석·깨진 글자(`:16,20,125-127,140`), `OfflineModeBootstrap`·`MonsterTestSpawner`·`PlayerSkinApplier`·`MonsterController.PlayerGrabKill` 낡은 머리 주석, 코드 주석의 `research.md R…` 참조를 `archive/research.2026-10-12.md`로 | 없음 | |

### 사용자 결정이 필요한 것

| # | 질문 | 선택지 |
|---|---|---|
| Q1 | 옛 `Game_*` 맵 5개(빌드 목록에서 꺼짐)를 남길까? | (a) 삭제 — FBX 27개·Compact 42개·TW 프리팹 18개·Twisted FBX 18개가 함께 정리됨 / (b) 보관 폴더로 이동 / (c) 그대로 |
| Q2 | 옛 맵 생성 파이프라인(`MapSceneBuilder` + `Maps/Common/Models` 97개 FBX + `CandyForestLayout`)을 남길까? — `PlayerTestScene`이 없어 지금은 실행 불가 | (a) 삭제 / (b) 보관 |
| Q3 | 망가진 소품 `TW_*`(§V3)를 지금 맵에 다시 배치할 계획이 있나? | 있으면 유지, 없으면 Q1과 함께 정리 |
| Q4 | 시험용 Purge 시나리오(시험 코드)도 함께 지울까? | 처단 모드를 되살릴 가능성이 없으면 삭제 |
| Q5 | `Legacy~`·`Source~` 원본을 저장소에 계속 둘까? | Git LFS / 저장소 밖 / 그대로 |

---

## 9. 부록 — 조사 방법·수치·한계

| 항목 | 내용 |
|---|---|
| 정독 | `Assets/02. Scripts` 206개 파일 전부(Legacy~ 제외), `Editor`·`03. SO`는 참조 확인 목적 |
| 이름 기반 집계 | 주석 제거 후 식별자 토큰 빈도, 선언 7,631개 → 런타임에서 선언 외 등장 0인 83개 → 거짓 양성 제거 후 6장 |
| 에셋 참조 | 씬·프리팹·SO YAML의 스크립트 GUID(`m_Script`) 역색인 — 206개 중 120개는 직접 에셋 참조 0(정적·순수·AddComponent 클래스) |
| 의존성 그래프 | Unity 에디터에서 `EditorBuildSettings.scenes`(켜진 7개) + `Resources/` 62개 뿌리 → `GetDependencies(recursive)` = 1,779개 |
| 씬 스캔 | 켜진 빌드 씬 7개를 추가 모드로 열어 비활성 하위 트리·Missing Script·빈 오브젝트·꺼진 렌더러/빛/충돌체 집계(씬은 저장하지 않고 닫음) |
| 이벤트 감사 | `+=`/`-=` 짝, 구독 위치(Awake/Start/OnEnable) ↔ 해제 위치 비대칭 자동 검사 |
| 한계 | ① 문자열로 부르는 호출(`Invoke("…")`·`InvokeRepeating`·`StartCoroutine("…")`·`SendMessage`)은 검색 결과 0건, 리플렉션은 `VoiceChat`(`usePrimaryRecorder`)·`HeldSyncRecorder`(`currentItem`)만 — 그래도 동적 호출을 100% 보장하지는 못한다. ② 애니메이션 이벤트는 0건 확인. ③ `SoundId` 값은 씬 직렬화(int)로 쓰일 수 있어 "코드 미사용" 중 일부는 실제 사용 — 6.5에 구분. ④ 에디터 빌더가 경로 문자열로 읽는 에셋은 "빌드 미사용이지만 도구 입력"으로 따로 표시. ⑤ 멀티플레이 실행 경로(방장 교체 중간 상태 등)는 정적 분석만 했다 |
| 이전 판 | 7차(2026-10-12): `Plan.md/archive/research.2026-10-12.md` — F-01~F-25 상태표, 5·6차 원문(부록 Z) 포함. 4차: `Plan.md/archive/research.prev.md` |
