# Plan.md 안내

계획 문서가 많아져 2026-10-07에 정리했다. **지금 읽어야 할 문서는 아래 "현재 문서"뿐**이고, 끝난 계획은 `archive/`에 그대로(이름 그대로) 옮겼다.
코드 주석이 `EscapeVisualPlan.md §…`, `DistanceFadePlan.md §…`처럼 문서 이름을 가리키는 곳이 있으면 `archive/`에서 같은 이름을 찾으면 된다.

## 현재 문서
| 문서 | 무엇 | 상태 |
|---|---|---|
| `Claude.md` | 작업 규칙(폴더·코드·언어) | 항상 |
| **`Request1009Plan.md`** | 2026-10-09 요청 11건(상자 길게 누르기·상자/로비/로켓 소리·대기실·오븐·관전·회전목마·놀이공원 충돌체/잡기) — 결과 기록 §15. **§16 2026-10-11 추가 8건**(상자 변장 시간 숨김·아이템 그림·쿠키 들기·아이템 들기 자세·비네트 제거·출력 장치·베이커리 고장 소리) — 결과 §16.11. **§18 2026-10-11 요청 6건**(포탈 인형 삭제·새 마녀 리깅/주먹 내리치기·열린 상자 재생성·마녀 등장 카메라·베이커리 곡·로비 곡 — 28초 한 번 재생 곡으로 교체). **§19 요청 4건**(로켓 뒤 마녀 클로즈업·쿠키가 모두 탈출하면 마녀가 괴물·스파이 처단·훔치기 10초(방장 검사)·대기실 강퇴) — 결과 §19.10, §19.11 처단 내리치기 클로즈업. **§20 소리**(게임 뒤 결과 화면 곡 `Result_A`·베이커리 고장 톱니 `GearB2` 넣음 — 결과 §20.3). **§21 2026-10-12 요청 2건**(스파이 로켓 뒤 괴물이 모두 나가도 판 계속·대기실에서 기다리는 괴물끼리 안 보임) — ✅ L0~L4(결과 §21.7), L5 멀티 확인 대기. **§22 8인 시험 3건**(대기실 문·공장 원유 칸 닿지 않음·로켓 재료 보장·다시 생김 알림·들린 쿠키가 길을 막음) — ✅ M0~M6(결과 §22.9), M7 멀티 확인 대기. **§23 마녀 연출 다시 짜기**(로켓 3초·등진 마녀 돌아섬·얼굴·멀리서 걸어옴·처단 10초·Ground 경계에서 내리침·맵 박살·배경 넓히기) — ✅ X0~X8(결과 §23.9), X9 멀티 확인 대기. **§24 마녀 도착 뒤 연출** — ✅ 확정 3테이크(로우 앵글·밑에서 본 밟기·박살 End, 마녀 3배) Y0~Y8(결과 §24.12), Y9 멀티 확인 대기 — 결과 §18.9. **§25 2026-10-15 게임 구성 개편**(사분면 맵·건물 4곳 10초 수리·탈출구 4개 중 2개·첫 탈출 마녀 타이머·무지개 상자/헬리콥터 모자·AI 복제·새 아이템/스킬·색칠/게임 시간 삭제·4~5명·캐릭터 4종) — ✅ G0~G15 구현·시험 완료(결과 §25.9, 2026-10-15), G16 멀티 확인 대기(§25.9.4), G17 사용자 3D 대기. **§26 2026-10-16 후속**(재료 안전장치·수리 게이지 버그·로켓 재료/수리키트 2번=1칸·훔친 재료는 스파이에게·폭탄 1개·이름표·AI 잡기 동작·AI 목소리 5~20초·마녀 타임 AI 삭제·모자 자동 비행·스킬 UI·입력 장치·대기실 음성 채팅·안 쓰는 것 정리) + §26.8 관전(탈출해도 스파이 시점·자동 관전) — ✅ H0~H13·H15 구현·시험 완료(결과 §26.10, 2026-10-16), H14 멀티 확인 대기. **§27 설정 메뉴 개편**(조작키 설정·중복 경고·방 설정 이름·설정 = 조작키/출력 장치/입력 장치·세 씬 ESC) — ✅ K0~K8 완료(결과 §26.10). **§28 캔디숲 실제 모델 맵 v2**(받은 FBX 11개·재료 건물+내부·배치 그림 기준·둥근 맵·통행 안전) — 계획 확정(질문 Q·F·G·H 답 반영 §28.11.7), C1 시작 대기 | ✅ R0~R6·R8~R10 · ✅ R7 결과 화면 곡(§20.3) · ✅ N0~N9 · §17 출력 장치 선택 ✅ D0~D6 · §18 ✅ W0~W8, 로비 곡 교체 완료 (2026-10-11) · §19 ✅ K0~K8, §19.11 ✅ · §20 ✅ |
| **`TwistedCandyPlan.md`** | 뒤틀린 과자 동화 개편(맵 분위기·망가진 소품·숨을 곳·색칠 위장·배경음/효과음 다시 만들기) — 작업 기록·다시 실행 순서 §10 | ✅ V0~V6 (2026-10-07) |
| **`AuditFixPlan.md`** | 감사(research.md 6차) F-01~F-25 + 클라이언트 조작 보안 S-01~S-15 수정 — 단계 P0~P9 결과, **16장 멀티 시험 절차(PC 4대)** | ✅ P1~P9 (2026-10-12) · F-08 보류(Q6) · 16장 멀티 시험 대기 |
| `GameRule.md` | 게임 규칙 명세(숨바꼭질·괴물·라운드) | 기준 문서 |
| `EscapePlan.md` | 탈출 모드 명세(장치·재료·스파이·마녀) | 기준 문서 |
| `SoundPlan.md` | 소리 시스템 구조와 작업 기록(S0~S9 ✅) | 기준 문서 |
| `SoundReplacementList.md` | 음원 110개 명세(ID·시점·길이·반복·2D/3D) | 기준 문서 — 개편 때도 그대로 |
| `research.md` | 코드 전수 조사(R 번호 — 코드 주석이 참조) | 참고 |
| `images/` | 계획서에 쓰는 그림(`images/twisted/` — 개편 목업) | |

## 남은 확인(옛 문서에서 옮김)
| 항목 | 출처 |
|---|---|
| 멀티 4인: 보안·경합 시험(`AuditFixPlan.md` 16장 M-1~M-15, 에디터 1대는 `Tools/TagOfChaos/Net Tamper Test` 메뉴) | AuditFixPlan P9 |
| `PlayerTestScene.unity`는 디스크에 없고 빌드 목록에서도 뺐다(2026-10-12). 맵 씬을 새로 만드는 도구(`MapSceneBuilder`)는 이 씬을 틀로 쓰므로, 맵을 다시 만들려면 씬을 되살려야 한다 | AuditFixPlan P9 |
| 빌드에서 소리·음량 설정 저장을 직접 들어 보고 확인, 멀티에서 다른 사람 소리 확인 | SoundPlan S9 |
| `Builds/S9Check/`·`Builds/V6Check/`·`Builds/R1009Check/`·`Builds/R1011Check/`·`Builds/AuditFixCheck/`·`Builds/Reform25Check/`·`Builds/Reform26Check/` — 확인용 빌드, 필요 없으면 삭제 | SoundPlan S9, TwistedCandyPlan V6, Request1009 R6 |
| 두 사람 멀티에서 숨을 곳·실내 소리·새 소리 확인, 새 소리 크기를 실제 스피커로 조정 | TwistedCandyPlan V6 |
| 대기실 점광원 14개 프레임 확인(프로파일링 미실시) — Request1009 R3에서 마녀 집 스포트 4개 추가 | archive/GameLobbyScene.md, Request1009 R3 |
| 결과 화면 곡 후보(`Source~/twisted/review/Result_candidate.ogg`·징글 묶음 3개) 청취 → 승인되면 R7(`MusicId.Result`)로 넣기 | Request1009 R2·R7 |
| 두 사람 이상 멀티: 관전 순환(탈출 = 일반 쿠키만, 부서짐 = 스파이 포함), 다른 사람을 따라 진저브레드 지하로 내려갈 때 동굴 소리, 길게 누르기 중 다른 사람이 먼저 연 상자 취소 | Request1009 R1·R4 |
| 두 사람 이상 멀티: 다른 쿠키를 F로 들고 걷기·달리기 — **세 번째 사람 화면**에서도 들린 쿠키가 드는 쪽 팔 위에 붙어 있는지(새 동기화 값 `RemoteCarrierViewId`), 두 손·한 손 아이템이 남의 화면에서도 손에 있는지, 변장 시간이 끝날 때 모든 화면에서 상자가 함께 나타나는지 | Request1009 §16 N1·N3·N4 |
| 베이커리 고장 소리 청취(`Source~/r1009/review/reel_N7_oven_broken.wav`) — 이미 넣음, 마음에 안 들면 `SFX/Device/OvenBroken.wav`만 교체 | Request1009 §16 N7 |
| 설정 → 출력 장치에서 이어폰·헤드셋을 골라 실제로 그 장치에서 소리가 나는지 귀로 확인(빌드 .exe에서도). Windows 내부 기능이라 Windows 업데이트 뒤 다시 확인 | Request1009 §17 |
| 쿠키 손에 손가락 뼈가 없어 한 손 아이템은 주먹 쥐는 동작 없이 오른손 위치만 따라간다 — 모델에 손가락이 생기면 쥐는 레이어 추가 | Request1009 §16 N4 |
| 새 베이커리 곡 청취(`Source~/r1011/review/reel_bakery_before_after.ogg`) — 이미 넣음, 예전 곡은 `Source~/r1011_backup/` | Request1009 §18.5 |
| 두 사람 이상 멀티: 마녀 등장 7초 카메라 연출이 모든 화면(괴물·쿠키·관전)에서 같은 때 나오는지, 주먹이 닿을 때 다른 화면의 쿠키·괴물도 그때 사라지는지 | Request1009 §18.2·18.4 |
| 마녀 모델을 다시 손보려면 `마녀FBX/Witch_Rigged.blend` → FBX → `Assets/09. Environment/Witch/WITCH_Scarecrow.fbx` 교체 후 `Tools/Escape/Setup Witch Model (Scarecrow)` | Request1009 §18.2 |
| 두 사람 이상 온라인: 대기실 강퇴(호스트가 방장이 아닐 때 포함), 쿠키가 모두 탈출한 뒤 스파이·괴물 화면의 탈출구 → 마녀 클로즈업과 처단, 로켓 발사 뒤 모든 화면의 로켓 → 마녀 클로즈업, 스파이 훔치기 10초(네트워크 지연에서도 거절되지 않는지) | Request1009 §19 |

## archive/ (끝난 계획·옛 판)
| 묶음 | 문서 |
|---|---|
| 소리 | `AudioProductionPlan.md`(S8 음원 방향 — TwistedCandyPlan §5로 대체), `AudioP1ListeningGuide.md`, `AudioS8ListeningGuide.md`, `DistanceFadePlan.md` |
| 탈출·게임 수정 | `EscapeVisualPlan.md`, `GameFixPlan.md`, `Request1003Plan.md`, `Request1003bPlan.md`, `Bug-fix-plan.md` |
| 맵·씬 | `GameScenePlan.md`, `GameLobbyScene.md`, `MapReplacePlan.md`, `MapReplacePlan.v1.md`, `Cauldron.md` |
| 캐릭터·구조 | `PlayerControllPlan.md`, `Monster-Rerig-Plan.md`, `GameManager.md`, `RoomItemPlan.md`, `architecture-review.md`, `architecture-review-plan.md` |
| 처음 기획·옛 조사 | `UserPlan.md`(첫 요청), `Plan.md`(옛 조치 계획), `research.prev.md`(조사 4차 판) |
