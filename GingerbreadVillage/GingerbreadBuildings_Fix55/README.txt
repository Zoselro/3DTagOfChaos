진저브레드 건물 20채 수정본 (2026-11-05, Request1009Plan.md §55.4)

들어 있는 것
- GinStaging/Buildings/<이름>/<이름>.fbx : 건물 20채 FBX (텍스처는 바뀌지 않아 넣지 않음 — 프로젝트에 있는 것을 그대로 씀)
- Editor/GingerbreadBuildingSetup.cs : Unity 건물 프리팹 도구 (프로젝트의 Assets/Editor/Gingerbread/ 안 파일과 같음)

바뀐 것
- 원본 아치 문: 문짝을 원본 벽 속으로(1~5.5 m) 넣어 닫았을 때·옆에서 볼 때 튀어나오지 않음, 새 문틀 없이 원본 아치가 문틀
- 새 문틀 문(원본에 문이 없던 뒷문, 방앗간·해골 가마솥 앞문): 문틀과 문짝을 함께 벽 속으로 넣음, 문턱은 문틀 크기로
- 문 앞 분홍 돌길 판 없앰, 원본 아치 문의 넓은 문턱 → 문짝 아래 통로 폭 문턱
- 문짝 앞 통로 면은 바로 옆 원본 벽 텍스처(재료 건물 모자이크 없앰)
- 문은 안쪽으로만 열림(프리팹 만들 때 설정)

Unity에 넣는 법 (Play Mode가 아닐 때)
1. GinStaging 폴더를 Unity 프로젝트 폴더(Assets와 같은 곳)의 GinStaging에 덮어쓴다
2. 메뉴 Tools/TagOfChaos/Gingerbread/1. Import Staged Buildings
3. 메뉴 Tools/TagOfChaos/Gingerbread/2. Build Building Prefabs
4. 메뉴 Tools/TagOfChaos/Gingerbread/3. Build Rune Building Prefab
   → 프리팹: Assets/04. Prefabs/Environment/GingerbreadBuildings/ (맵의 건물은 프리팹을 따라 자동으로 바뀜)
5. 진저브레드 맵 NavMesh 다시 굽기(DecoyNavMeshBaker)
