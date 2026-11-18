# HauntedBakery (마녀의 유령 빵집)

350 × 350 m 플레이 공간(지면 두께 3 m 이상, 윗면 높이 ≈ 0)의 환경 맵. 참고 이미지: `C:\Program Files\Blender Foundation\Map`. Blender 원본: `Assets/Maps/Source~/Maps/HauntedBakery.blend` (전체 작업 파일 `Source~/TagOfChaos_Maps.blend`, 재생성 스크립트 `Source~/Scripts/`).

**랜드마크**: 작업실 북쪽 마법 오븐 (`HAU_Landmark_MagicOven`, 굴뚝 약 45 m)

## FBX (Models/)

| 파일 | 내용 | 충돌체 |
|---|---|---|
| `HauntedBakery_Background.fbx` | 외곽 장식 지형 `*_Terrain_Decor` + 배경 나무/건물 | 없음 |
| `HauntedBakery_Decoration.fbx` | 장식 소품(길가 사탕, 꽃, 난간 등) | 없음 |
| `HauntedBakery_Effects.fbx` | 이펙트 배치 기준점(빈 오브젝트 `FX_*`) | 없음 |
| `HauntedBakery_GameplayProps.fbx` | 엄폐물·가구 등 플레이 공간 소품 | Generate Colliders (MeshCollider) |
| `HauntedBakery_Ground.fbx` | 걷는 지형 `*_Terrain_Walkable` + 외곽 투명 벽 `COL_Boundary_*` | Generate Colliders (MeshCollider) |
| `HauntedBakery_Lighting.fbx` | 발광 메시(창문·룬 띠·램프 구체 등) | 없음 |
| `HauntedBakery_MainStructures.fbx` | 건축물·대형 구조물·랜드마크 | Generate Colliders (MeshCollider) |
| `HauntedBakery_Terrain.fbx` | 언덕 위 정원·작업대·경사로 등 지형성 구조물 | Generate Colliders (MeshCollider) |
| `HauntedBakery_Water.fbx` | 물 표면(얕은 물, 시각용) | 없음 |

- 모든 FBX 원점은 맵 원점이다. 씬에 **위치 (0,0,0) / 회전 (0,0,0) / 스케일 1**로 넣으면 서로 맞물린다.
- 축: Blender (x, y, z) → Unity (−x, z, −y). 1 unit = 1 m. 축 변환이 이미 적용돼 오브젝트에 −90° 회전이 없다.
- 재질은 `.meta`에서 이름으로 연결된다. 기존 프로젝트 재질(`09. Environment/*/Materials`)을 재사용하고, 없는 것만 `Assets/Maps/Common/Materials`에 있다(Built-in Standard, 텍스처 없음, 색·광택·발광만).
- `COL_Boundary_*`는 `M_Invisible_Collider`(완전 투명)를 쓴다. 필요하면 MeshRenderer만 꺼도 된다.
- 움직이지 않는 오브젝트는 Static으로 지정하고, 재질의 GPU Instancing을 켜 두면 같은 에셋 인스턴스가 한 번에 그려진다.

## 오브젝트 수

| 카테고리 | 오브젝트 |
|---|---|
| Ground | 5 |
| Terrain | 10 |
| MainStructures | 211 |
| GameplayProps | 124 |
| Decoration | 180 |
| Background | 92 |
| Lighting | 74 |
| Effects | 4 |

## 사용 에셋 (인스턴스 수, 개별 FBX: `Assets/Maps/Common/Models/<이름>.fbx`)

`Pumpkin` ×57, `TwistedTree_A` ×44, `Bakery_BeamPost` ×40, `FlourSack_Pile` ×39, `TwistedTree_B` ×37, `TwistedTree_C` ×31, `Chair_Purple` ×24, `WallLantern_Post` ×23, `ChocolateBarrel` ×13, `CandyBasket` ×13, `Shelf_Potion` ×12, `CandyFlower_Patch` ×11, `CandyJar_A` ×10, `GingerHouse_Cottage` ×9, `Table_Round` ×8, `WitchSign_A` ×6, `Booth_Neon_B` ×5, `StarRug` ×5, `CandyJar_B` ×5, `GumdropBush_B` ×5, `Bakery_MixingBowl` ×4, `DeliveryCart` ×4, `Chalkboard_Awning` ×4, `GingerHouse_Cursed` ×3, `Bakery_DoughTable` ×3, `Counter_Purple` ×3, `DisplayWindow` ×2, `GingerHouse_Twin` ×2, `Bakery_DoughTable_Small` ×2, `GiantCake` ×2, `Landmark_MagicOven` ×1, `MagicOven_Door` ×1, `GingerHouse_Tall` ×1, `CookieCrate_B` ×1, `CookieCrate_A` ×1, `Bakery_FloorPlanks` ×1

## 조명 (`HauntedBakery_Lighting.json`)

- Environment Lighting: Color = (0.05, 0.04, 0.07), 강도 0.5
- Fog: Exponential, 색 (0.1, 0.08, 0.12), 밀도 0.005
- Directional Light: 색 (0.7, 0.62, 0.9), 강도 0.12, 회전 X 55° / Y 40° (근사)
- Point Light 61개: `pos_unity`, 색, `range`, 강도, 그림자 여부. 발광 재질만으로 충분하면 생략해도 된다.

## MonsterPlayer 통과

괴물(루트 스케일 3.14: 팔 폭 6.3 m, 키 5.3 m)이 드나드는 문과 실내 통로는 폭 7 m 이상, 높이 7 m 이상이다. 문 안팎 7 m는 막힘 없이 비워 두었다(`Source~/Scripts/door_sweep.py`로 검사).

## 애니메이션용 분리 오브젝트

- `HAU_MagicOven_Door` — 원점이 경첩. Unity Y축 회전. 현재 반쯤 열린 상태(Blender Z −109° = Unity Y 약 +109°), 0°가 닫힘.

## 렌더 (`Assets/Maps/Source~/Renders/Blockout/`)

`HauntedBakery_TopCam.png`(탑 뷰), `HauntedBakery_GameCam_*.png`(게임 카메라 시점), `HauntedBakery_Mood.png`(조명), `HauntedBakery_Terrain_HeightMap.png` / `_Terrain_Relief_x3.png`(지형)
