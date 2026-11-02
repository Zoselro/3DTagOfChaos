# Writes Assets/Maps/<Map>/README.md and Assets/Maps/README.md from the actual exported files + scene data.
import bpy, os, json, collections

MAPS_DIR = r"F:\3DClaudeTagOfChaos\TagOfChaos\Assets\Maps"
NAMES = ['CandyForest', 'GingerbreadVillage', 'ChocolateFactory', 'CursedCandyCarnival', 'HauntedBakery']
KO = {'CandyForest': '사탕숲', 'GingerbreadVillage': '과자마을', 'ChocolateFactory': '초콜릿공장',
      'CursedCandyCarnival': '저주받은 사탕 놀이공원', 'HauntedBakery': '마녀의 유령 빵집'}
LANDMARK = {'CandyForest': '중앙 거대 막대사탕 나무 (`CAN_Landmark_LollipopTree`, 높이 약 77 m)',
            'GingerbreadVillage': '광장 과자 시계탑 (`GIN_Landmark_ClockTower`, 약 47 m)',
            'ChocolateFactory': '초콜릿 탱크 + 줄무늬 첨탑 (`CHO_Landmark_ChocolateTank`, `..._Spire`, 약 96 m)',
            'CursedCandyCarnival': '북쪽 언덕 위 사탕 관람차 (`CUR_CandyFerrisWheel_*`, 약 86 m)',
            'HauntedBakery': '작업실 북쪽 마법 오븐 (`HAU_Landmark_MagicOven`, 굴뚝 약 45 m)'}
ANIM = {'CandyForest': [],
        'GingerbreadVillage': [],
        'ChocolateFactory': [],
        'CursedCandyCarnival': ['`CUR_Carousel_Top` — 회전목마 윗부분. Y축(Unity) 회전. 바닥 `CUR_Carousel_Base`는 고정.',
                                '`CUR_CandyFerrisWheel_Pivot` — 관람차 허브 위치(빈 오브젝트). 림·살·곤돌라를 이 축 기준으로 묶어 회전.'],
        'HauntedBakery': ['`HAU_MagicOven_Door` — 원점이 경첩. Unity Y축 회전. 현재 반쯤 열린 상태'
                          '(Blender Z −109° = Unity Y 약 +109°), 0°가 닫힘.']}
COLL = {'Ground': True, 'Terrain': True, 'MainStructures': True, 'GameplayProps': True}


def build_doc(name):
    sc = bpy.data.scenes[name]
    models = os.path.join(MAPS_DIR, name, 'Models')
    files = sorted(f for f in os.listdir(models) if f.endswith('.fbx'))
    cats = collections.Counter()
    assets = collections.Counter()
    for o in sc.objects:
        for c in o.users_collection:
            if c.name.startswith(name + '_'):
                cats[c.name.split('_', 1)[1]] += 1
        if o.get('asset'):
            assets[o['asset']] += 1
    lt = json.load(open(os.path.join(MAPS_DIR, name, f'{name}_Lighting.json'), encoding='utf-8'))
    L = []
    L.append(f'# {name} ({KO[name]})\n')
    L.append('350 × 350 m 플레이 공간(지면 두께 3 m 이상, 윗면 높이 ≈ 0)의 환경 맵. 참고 이미지: '
             '`C:\\Program Files\\Blender Foundation\\Map`. Blender 원본: `Assets/Maps/Source~/Maps/'
             f'{name}.blend` (전체 작업 파일 `Source~/TagOfChaos_Maps.blend`, 재생성 스크립트 `Source~/Scripts/`).\n')
    L.append(f'**랜드마크**: {LANDMARK[name]}\n')
    L.append('## FBX (Models/)\n')
    L.append('| 파일 | 내용 | 충돌체 |\n|---|---|---|')
    desc = {'Ground': '걷는 지형 `*_Terrain_Walkable` + 외곽 투명 벽 `COL_Boundary_*`',
            'Terrain': '언덕 위 정원·작업대·경사로 등 지형성 구조물', 'Water': '물 표면(얕은 물, 시각용)',
            'MainStructures': '건축물·대형 구조물·랜드마크', 'GameplayProps': '엄폐물·가구 등 플레이 공간 소품',
            'Decoration': '장식 소품(길가 사탕, 꽃, 난간 등)', 'Background': '외곽 장식 지형 `*_Terrain_Decor` + 배경 나무/건물',
            'Lighting': '발광 메시(창문·룬 띠·램프 구체 등)', 'Effects': '이펙트 배치 기준점(빈 오브젝트 `FX_*`)'}
    for f in files:
        key = f[len(name) + 1:-4]
        L.append(f'| `{f}` | {desc.get(key, key)} | {"Generate Colliders (MeshCollider)" if key in COLL else "없음"} |')
    L.append('\n- 모든 FBX 원점은 맵 원점이다. 씬에 **위치 (0,0,0) / 회전 (0,0,0) / 스케일 1**로 넣으면 서로 맞물린다.')
    L.append('- 축: Blender (x, y, z) → Unity (−x, z, −y). 1 unit = 1 m. 축 변환이 이미 적용돼 오브젝트에 −90° 회전이 없다.')
    L.append('- 재질은 `.meta`에서 이름으로 연결된다. 기존 프로젝트 재질(`09. Environment/*/Materials`)을 재사용하고, '
             '없는 것만 `Assets/Maps/Common/Materials`에 있다(Built-in Standard, 텍스처 없음, 색·광택·발광만).')
    L.append('- `COL_Boundary_*`는 `M_Invisible_Collider`(완전 투명)를 쓴다. 필요하면 MeshRenderer만 꺼도 된다.')
    L.append('- 움직이지 않는 오브젝트는 Static으로 지정하고, 재질의 GPU Instancing을 켜 두면 같은 에셋 인스턴스가 한 번에 그려진다.\n')
    L.append('## 오브젝트 수\n')
    L.append('| 카테고리 | 오브젝트 |\n|---|---|')
    for c in ('Ground', 'Terrain', 'MainStructures', 'GameplayProps', 'Decoration', 'Background', 'Lighting', 'Effects'):
        L.append(f'| {c} | {cats.get(c, 0)} |')
    L.append('\n## 사용 에셋 (인스턴스 수, 개별 FBX: `Assets/Maps/Common/Models/<이름>.fbx`)\n')
    L.append(', '.join(f'`{a}` ×{n}' for a, n in assets.most_common()))
    L.append('\n## 조명 (`' + f'{name}_Lighting.json' + '`)\n')
    L.append(f'- Environment Lighting: Color = {tuple(round(c, 2) for c in lt["ambient_color"])}, 강도 {lt["ambient_intensity"]}')
    L.append(f'- Fog: Exponential, 색 {tuple(round(c, 2) for c in lt["fog_color"])}, 밀도 {lt["fog_density"]}')
    d = lt['directional']
    L.append(f'- Directional Light: 색 {tuple(round(c, 2) for c in d["color"])}, 강도 {d["intensity"]}, '
             f'회전 X {d["euler_x"]}° / Y {d["euler_y"]}° (근사)')
    L.append(f'- Point Light {len(lt["point_lights"])}개: `pos_unity`, 색, `range`, 강도, 그림자 여부. 발광 재질만으로 충분하면 생략해도 된다.\n')
    L.append('## MonsterPlayer 통과\n')
    L.append('괴물(루트 스케일 3.14: 팔 폭 6.3 m, 키 5.3 m)이 드나드는 문과 실내 통로는 폭 7 m 이상, 높이 7 m 이상이다. '
             '문 안팎 7 m는 막힘 없이 비워 두었다(`Source~/Scripts/door_sweep.py`로 검사).')
    if ANIM[name]:
        L.append('\n## 애니메이션용 분리 오브젝트\n')
        L += ['- ' + a for a in ANIM[name]]
    L.append('\n## 렌더 (`Assets/Maps/Source~/Renders/Blockout/`)\n')
    L.append(f'`{name}_TopCam.png`(탑 뷰), `{name}_GameCam_*.png`(게임 카메라 시점), `{name}_Mood.png`(조명), '
             f'`{name}_Terrain_HeightMap.png` / `_Terrain_Relief_x3.png`(지형)')
    with open(os.path.join(MAPS_DIR, name, 'README.md'), 'w', encoding='utf-8') as f:
        f.write('\n'.join(L) + '\n')
    return files


def build_index():
    L = ['# Maps — TagOfChaos 환경 맵 5종\n',
         '| 맵 | 폴더 | 분위기 |', '|---|---|---|',
         '| CandyForest (사탕숲) | `CandyForest/` | 분홍 설탕눈 숲, 거대 막대사탕 나무 |',
         '| GingerbreadVillage (과자마을) | `GingerbreadVillage/` | 밤의 진저브레드 마을, 과자 시계탑 |',
         '| ChocolateFactory (초콜릿공장) | `ChocolateFactory/` | 유리 지붕 공장, 초콜릿 탱크와 강 |',
         '| CursedCandyCarnival (저주받은 놀이공원) | `CursedCandyCarnival/` | 네온 관람차, 서커스 천막 |',
         '| HauntedBakery (마녀의 유령 빵집) | `HauntedBakery/` | 마법 오븐, 보라 천과 나무 인테리어 |\n',
         '- `Common/Materials`: 기존 프로젝트에 없던 재질만(Built-in Standard, 텍스처 없음). 기존 `M_*` 재질은 이름으로 재사용된다.',
         '- `Common/Models`: 에셋 하나당 FBX 하나(프리팹 원본). 원점은 바닥 중앙이고, 앞은 Unity +Z다.',
         '- `Source~/`: Blender 원본·스크립트·렌더·문서. `~`로 끝나는 폴더라 Unity가 가져오지 않는다.',
         '- 각 맵 `README.md`에 FBX 목록, 충돌체, 배치, 조명, 괴물 통과 기준을 정리했다.',
         '- Unity 코드와 씬은 변경하지 않았다. 씬 구성은 각 맵 FBX를 원점에 배치해서 만든다.']
    with open(os.path.join(MAPS_DIR, 'README.md'), 'w', encoding='utf-8') as f:
        f.write('\n'.join(L) + '\n')


DOCS = {n: build_doc(n) for n in NAMES}
build_index()
