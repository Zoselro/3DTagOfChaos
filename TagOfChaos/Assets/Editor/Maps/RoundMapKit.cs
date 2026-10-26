using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// 둥근 실제 맵 빌더(놀이공원 CarnivalMapBuilder §36, 초콜릿 공장 ChocolateFactoryMapBuilder §42)가 함께 쓰는 부분:
//   극좌표·방향, 묶음·상자 충돌, 놓은 자리 기록(Placement), 술래방 벽 고리·문, 탈출구 충돌·받침·열린 표시, 둘레 울타리 충돌 고리,
//   표시 자리, 점광원, 스폰·마녀 앵커 옮기기, 빌드 설정, 통행 검사 목표.
// 크기·모양은 맵마다 다르므로 값은 인자로 받는다(같은 값이면 두 맵 결과가 같다).
public static class RoundMapKit
{
    public const string BeaconShader = "TagOfChaos/Beacon";
    public const float ExitBeamHeight = 120f;

    // ---------------------------------------------------------------- directions

    public static Vector3 Polar(float radius, float deg) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0f, Mathf.Sin(deg * Mathf.Deg2Rad)) * radius;
    public static Vector3 Polar(Vector2 radiusDeg, float baseDeg = 0f) => Polar(radiusDeg.x, baseDeg + radiusDeg.y);
    // 로컬 +Z가 맵 가운데를 본다(재료 건물·탈출구 루트 — 건물 FBX는 앞이 +Z).
    public static Quaternion FaceCenter(Vector3 p) => new Vector2(p.x, p.z).sqrMagnitude < 0.01f ? Quaternion.identity : Quaternion.LookRotation(new Vector3(-p.x, 0f, -p.z));
    // 소품 FBX(ride_common 내보내기)는 앞(Blender −Y)이 Unity −Z — 앞이 맵 가운데를 보게 +Z를 바깥으로.
    public static Quaternion FrontToCenter(Vector3 p) => new Vector2(p.x, p.z).sqrMagnitude < 0.01f ? Quaternion.identity : Quaternion.LookRotation(new Vector3(p.x, 0f, p.z));
    public static float AngleTo(float deg, IEnumerable<float> others) => others.Min(e => Mathf.Abs(Mathf.DeltaAngle(deg, e)));

    // ---------------------------------------------------------------- scene objects

    public static Transform Group(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return t;
        t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    public static Transform LocalBox(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = center;
        t.gameObject.AddComponent<BoxCollider>().size = size;
        return t;
    }

    // 프리팹을 놓는다. colliders = false면 충돌체와 충돌체만 담던 COL_* 자식을 지운다(겉모습만).
    public static GameObject Spawn(GameObject asset, Transform parent, Vector3 pos, Quaternion rot, bool colliders = true)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.SetPositionAndRotation(pos, rot);
        if (!colliders) StripColliders(go);
        return go;
    }

    public static void StripColliders(GameObject go)
    {
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        // 충돌체만 담던 자식(COL_*)은 빈 껍데기가 되므로 함께 지운다(Request1009Plan.md §38 B2)
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            if (t != go.transform && t.name.StartsWith("COL") && t.childCount == 0 && t.GetComponents<Component>().Length == 1) Object.DestroyImmediate(t.gameObject);
    }

    // FBX 모델(프리팹 아님)을 로컬 자세로 놓는다.
    public static GameObject Model(string assetPath, Transform parent, Vector3 localPos, Quaternion localRot, string name = null)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null) { Debug.LogWarning($"[RoundMapKit] model {assetPath} missing"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        if (name != null) go.name = name;
        return go;
    }

    // t 기준 범위: 메시 자기 범위의 꼭짓점을 옮겨 잰다(돌아간 물체의 월드 범위로 재면 부풀려진다).
    public static Bounds LocalBounds(Transform t)
    {
        var b = new Bounds();
        bool first = true;
        foreach (MeshFilter mf in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.name == "ESC_Glow" || mf.sharedMesh == null || mf.GetComponent<Renderer>() == null) continue;
            Bounds w = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = t.InverseTransformPoint(mf.transform.TransformPoint(new Vector3(i % 2 == 0 ? w.min.x : w.max.x, i / 2 % 2 == 0 ? w.min.y : w.max.y, i / 4 == 0 ? w.min.z : w.max.z)));
                if (first) { b = new Bounds(corner, Vector3.zero); first = false; }
                else b.Encapsulate(corner);
            }
        }
        return b;
    }

    // 프리팹 원점에서 겉모습 끝까지(수평). 프리팹 에셋의 bounds는 비어 있어 잠깐 꺼내 잰다.
    public static float Reach(GameObject asset)
    {
        if (asset == null) return 2f;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        float r = 0f;
        try
        {
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (Renderer x in go.GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = x.bounds;
                r = Mathf.Max(r, new Vector2(Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)), Mathf.Max(Mathf.Abs(b.min.z), Mathf.Abs(b.max.z))).magnitude);
            }
        }
        finally { Object.DestroyImmediate(go); }
        return r;
    }

    // ---------------------------------------------------------------- placement record

    // 놓은 것(수평 원) — 흩어 놓기·표시 자리 검사. 괴물 출발점(통행 검사)도 같이 모은다.
    public sealed class Placement
    {
        private readonly List<(Vector2 c, float r)> taken = new List<(Vector2, float)>();
        public readonly List<Vector3> MonsterSeeds = new List<Vector3>();
        public void Take(Vector3 p, float r) => taken.Add((new Vector2(p.x, p.z), r));
        public bool IsFree(Vector3 p, float r) => taken.All(t => (t.c - new Vector2(p.x, p.z)).magnitude >= t.r + r);
        public void Clear() { taken.Clear(); MonsterSeeds.Clear(); }
    }

    // ---------------------------------------------------------------- monster room

    // 둥근 술래방 벽: 겹친 상자 고리(RoomRing_, 문 자리 비움) + 문 옆 기둥(RoomRing_Post) — 고리 조각 사이 틈 없이 문 폭 바깥 끝을 막는다.
    public static void RoomRing(Transform room, float inner, float outer, float wallHeight, float doorWidth, float[] doorDeg)
    {
        Transform ring = Group(room, "RoomRings");
        const int segments = 72;
        float step = 360f / segments, doorHalfDeg = (doorWidth / 2f) / inner * Mathf.Rad2Deg;
        for (int i = 0; i < segments; i++)
        {
            float a = (i + 0.5f) * step;
            if (doorDeg.Any(d => Mathf.Abs(Mathf.DeltaAngle(a, d)) < doorHalfDeg + step / 2f)) continue;
            Vector3 dir = Polar(1f, a);
            var t = new GameObject($"RoomRing_{i:00}").transform;
            t.SetParent(ring, false);
            t.localPosition = dir * ((inner + outer) / 2f) + Vector3.up * wallHeight / 2f;
            t.localRotation = Quaternion.LookRotation(dir);
            float chord = 2f * outer * Mathf.Sin(step * Mathf.Deg2Rad / 2f) + 0.2f;
            t.gameObject.AddComponent<BoxCollider>().size = new Vector3(chord, wallHeight, outer - inner + 0.1f);
        }
        for (int i = 0; i < doorDeg.Length; i++)
        {
            Vector3 dir = Polar(1f, doorDeg[i]);
            Quaternion rot = Quaternion.LookRotation(dir);
            foreach (int s in new[] { -1, 1 })
            {
                var post = new GameObject($"RoomRing_Post{i}{(s < 0 ? "L" : "R")}").transform;
                post.SetParent(ring, false);
                post.localPosition = dir * ((inner + outer) / 2f) + rot * Vector3.right * (s * (doorWidth / 2f + 0.5f)) + Vector3.up * wallHeight / 2f;
                post.localRotation = rot;
                post.gameObject.AddComponent<BoxCollider>().size = new Vector3(1f, wallHeight, outer - inner + 0.3f);
            }
        }
    }

    // 술래방 문(MonsterDoor_i): 문 메시(겉) + 문 폭 상자. MonsterRoomGuard가 괴물만 지나가게 한다.
    public static Transform RoomDoor(Transform room, int index, Vector3 dir, float inner, float outer, float wallHeight, float doorWidth, GameObject doorModel)
    {
        var door = new GameObject("MonsterDoor_" + index);
        door.transform.SetParent(room, false);
        door.transform.localPosition = dir * ((inner + outer) / 2f);
        door.transform.localRotation = Quaternion.LookRotation(dir);
        MeshFilter source = doorModel != null ? doorModel.GetComponentInChildren<MeshFilter>() : null;
        if (source != null)
        {
            door.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            door.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
        }
        var box = door.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, wallHeight / 2f, 0f);
        box.size = new Vector3(doorWidth + 0.2f, wallHeight, outer - inner + 0.3f);
        return door.transform;
    }

    // ---------------------------------------------------------------- exits

    public struct ExitSize
    {
        public float Opening, PostWidth, Height, PadDepth;
        public float WallX => Opening / 2f + PostWidth + 0.4f;
    }

    // 탈출구 문틀 기둥·문 위·울타리 밖 받침(양옆·뒤 막음) 충돌 + 트리거 자리(ExitTrigger_). 겉모습은 맵마다.
    public static void ExitColliders(Transform root, string key, ExitSize s)
    {
        float postX = s.Opening / 2f + s.PostWidth / 2f;
        LocalBox(root, "COL_PostL", new Vector3(-postX, 5f, 0f), new Vector3(s.PostWidth, 10f, 2.2f));
        LocalBox(root, "COL_PostR", new Vector3(postX, 5f, 0f), new Vector3(s.PostWidth, 10f, 2.2f));
        LocalBox(root, "COL_Lintel", new Vector3(0f, (s.Height + 10f) / 2f, 0f), new Vector3(s.Opening, 10f - s.Height, 2.2f));
        float wallX = s.WallX;
        LocalBox(root, "COL_PadL", new Vector3(-wallX, 3f, -s.PadDepth / 2f - 0.5f), new Vector3(0.8f, 6f, s.PadDepth));
        LocalBox(root, "COL_PadR", new Vector3(wallX, 3f, -s.PadDepth / 2f - 0.5f), new Vector3(0.8f, 6f, s.PadDepth));
        LocalBox(root, "COL_PadBack", new Vector3(0f, 3f, -s.PadDepth - 0.5f), new Vector3(wallX * 2f + 0.8f, 6f, 0.8f));
        var trigger = new GameObject("ExitTrigger_" + key).transform;
        trigger.SetParent(root, false);
        trigger.localPosition = new Vector3(0f, 1f, -4f);
    }

    // 닫힌 문(Door_<key>, 열리면 EscapeGate가 땅으로 내린다): 문 폭 상자 + 문짝 두 장(경첩 = 문 폭 양끝).
    public static Transform ExitDoor(Transform root, string key, ExitSize s, string leftModel, string rightModel)
    {
        var door = new GameObject("Door_" + key).transform;
        door.SetParent(root, false);
        var doorBox = door.gameObject.AddComponent<BoxCollider>();
        doorBox.center = new Vector3(0f, s.Height / 2f, 0f);
        doorBox.size = new Vector3(s.Opening, s.Height, 0.8f);
        Model(leftModel, door, new Vector3(-s.Opening / 2f, 0f, 0f), Quaternion.identity);
        Model(rightModel, door, new Vector3(s.Opening / 2f, 0f, 0f), Quaternion.identity);
        return door;
    }

    // 열린 탈출구 표시(ExitOpenLook_ — EscapeGate가 열릴 때 켠다, §37 L1): 받침 위 하늘까지 빛기둥(안개 무시), 문틀 안쪽 빛 띠,
    // 바닥 빛 원, 점광원. 충돌 없음. 재질 이름 앞머리(prefix)와 폴더는 맵마다.
    public static void ExitOpenLook(Transform root, string key, ExitSize s, Color color, string materialFolder, string prefix)
    {
        var look = new GameObject("ExitOpenLook_" + key).transform;
        look.SetParent(root, false);
        Mesh cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"), quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        Material core = BeaconMaterial(materialFolder, prefix + "ExitBeamCore", color * 1.6f, 0f, 1.2f, 1.2f);
        Material haze = BeaconMaterial(materialFolder, prefix + "ExitBeamHaze", color * 0.55f, 0f, 0.8f, 2.5f);
        Material frame = BeaconMaterial(materialFolder, prefix + "ExitFrame", color * 1.8f, 0f, 0.1f, 0.1f);
        Material ground = BeaconMaterial(materialFolder, prefix + "ExitGround", color * 1.1f, 1f, 1.5f, 1f);
        BeaconPart(look, "Beam_Core", cylinder, core, new Vector3(0f, ExitBeamHeight / 2f, -4f), Quaternion.identity, new Vector3(1.8f, ExitBeamHeight / 2f, 1.8f));
        BeaconPart(look, "Beam_Haze", cylinder, haze, new Vector3(0f, ExitBeamHeight / 2f, -4f), Quaternion.identity, new Vector3(6f, ExitBeamHeight / 2f, 6f));
        BeaconPart(look, "Ground_Glow", quad, ground, new Vector3(0f, 0.1f, -1f), Quaternion.Euler(90f, 0f, 0f), new Vector3(14f, 14f, 1f));
        foreach (int side in new[] { -1, 1 })
            BeaconPart(look, side < 0 ? "Frame_L" : "Frame_R", quad, frame, new Vector3(side * (s.Opening / 2f + 0.2f), s.Height / 2f, 1.2f), Quaternion.identity, new Vector3(0.4f, s.Height, 1f));
        BeaconPart(look, "Frame_Top", quad, frame, new Vector3(0f, s.Height + 0.2f, 1.2f), Quaternion.identity, new Vector3(s.Opening + 0.8f, 0.4f, 1f));
        var light = new GameObject("Light_Exit").AddComponent<Light>();
        light.transform.SetParent(look, false);
        light.transform.localPosition = new Vector3(0f, 4f, 2.5f);
        light.type = LightType.Point;
        light.range = 26f;
        light.intensity = 4.5f;
        light.color = color;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
    }

    public static void BeaconPart(Transform parent, string name, Mesh mesh, Material material, Vector3 localPos, Quaternion localRot, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    // Beacon 재질(맵 Generated/에 두고 다시 만들 때 값만 고친다)
    public static Material BeaconMaterial(string folder, string name, Color color, float radial, float fadePower, float edgePower)
    {
        string path = $"{folder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find(BeaconShader));
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = Shader.Find(BeaconShader);
        m.SetColor("_Color", color);
        m.SetFloat("_Radial", radial);
        m.SetFloat("_FadePower", fadePower);
        m.SetFloat("_EdgePower", edgePower);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------------------------------------------------------------- fence

    // 둘레 울타리 충돌: 겹친 상자 고리 240조각, 탈출구 자리(문 폭 + 기둥)만 정확히 비운다(문 기둥이 막는다).
    public static void FenceColliders(Transform parent, float radius, float[] exitDeg, float exitHalf, float height, float thickness)
    {
        const int colSegments = 240;
        float step = 360f / colSegments;
        for (int i = 0; i < colSegments; i++)
        {
            float a0 = i * step, a1 = a0 + step;
            float lo = a0, hi = a1;
            bool skip = false;
            foreach (float center in exitDeg)
            {
                float half = exitHalf / radius * Mathf.Rad2Deg;
                float d0 = Mathf.DeltaAngle(center, a0), d1 = Mathf.DeltaAngle(center, a1);
                if (d0 >= -half && d1 <= half) skip = true;
                else if (d0 < -half && d1 > -half && d1 <= half) hi = a1 - (d1 + half);
                else if (d0 >= -half && d0 < half && d1 > half) lo = a0 + (half - d0);
            }
            if (skip) continue;
            float mid = (lo + hi) / 2f, span = Mathf.DeltaAngle(lo, hi);
            Vector3 p = Polar(radius + thickness / 2f, mid);
            var t = new GameObject($"COL_Fence_{i:000}").transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(p + Vector3.up * height / 2f, Quaternion.LookRotation(p));
            float chord = 2f * radius * Mathf.Sin(span * Mathf.Deg2Rad / 2f) + 0.2f;
            t.gameObject.AddComponent<BoxCollider>().size = new Vector3(chord, height, thickness);
        }
    }

    // ---------------------------------------------------------------- markers, lights, spawns

    // 상자·로켓·무지개·스폰 자리(캔디숲 규칙). 놓은 것과 떨어지고, 몸이 들어가며, 바로 아래가 땅인 자리만.
    public static int Markers(Transform map, Placement placed, float ringRadius, float ringHalf)
    {
        Transform markers = Group(map, "Markers");
        bool Clear(Vector3 p, float radius)
        {
            if (!placed.IsFree(p, radius)) return false;
            if (Mathf.Abs(new Vector2(p.x, p.z).magnitude - ringRadius) < ringHalf) return false;
            if (Physics.CheckCapsule(p + Vector3.up * (radius + 0.1f), p + Vector3.up * (radius + 2.4f), radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            return Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.point.y < 0.3f;
        }
        int n = 0;
        foreach ((string name, Vector3 pos) in CandyForestLayout.PlaceMarkers(Clear))
        {
            var t = new GameObject(name).transform;
            t.SetParent(markers, false);
            t.position = pos;
            n++;
        }
        return n;
    }

    public static Light Point(Transform parent, Vector3 pos, float range, float intensity, Color color)
    {
        var go = new GameObject("Light_" + parent.childCount.ToString("000"));
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = range;
        l.intensity = intensity;
        l.color = color;
        l.shadows = LightShadows.None;
        return l;
    }

    public static void MoveSpawn(Scene scene, string spawnName, Transform map, string markerName)
    {
        GameObject spawn = scene.GetRootGameObjects().FirstOrDefault(g => g.name == spawnName);
        Transform marker = map.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == markerName);
        if (spawn != null && marker != null) spawn.transform.position = marker.position + Vector3.up * 0.5f;
    }

    // 마녀 앵커를 울타리 밖 beyond m로(방향은 그대로).
    public static void MoveWitchAnchor(Scene scene, float mapRadius, float beyond)
    {
        Transform anchor = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "WITCH_Anchor");
        if (anchor == null) return;
        var flat = new Vector2(anchor.position.x, anchor.position.z);
        if (flat.sqrMagnitude < 1f) flat = new Vector2(-1f, -1f);
        Vector2 at = flat.normalized * (mapRadius + beyond);
        anchor.position = new Vector3(at.x, anchor.position.y, at.y);
    }

    // 마녀가 걸어오는 먼 배경 중 넓어진 맵에 걸리는 것은 그만큼 바깥으로(캔디숲 §32와 같음).
    public static void SpreadFarBackdrop(Transform map, float mapRadius, float clear, float oldRadius = 80f)
    {
        Transform far = map.Find(WitchPath.FarBackdropName);
        if (far == null) return;
        float push = mapRadius - oldRadius;
        foreach (Transform t in far.Cast<Transform>().ToList())
        {
            Renderer r = t.GetComponentInChildren<Renderer>(true);
            if (r == null || r.bounds.size.x > clear) continue;
            var flat = new Vector2(r.bounds.center.x, r.bounds.center.z);
            if (flat.magnitude >= clear || flat.sqrMagnitude < 1f) continue;
            Vector2 move = flat.normalized * push;
            t.position += new Vector3(move.x, 0f, move.y);
        }
    }

    // 실제 맵 씬을 빌드 설정에 넣고 테스트 씬은 끈다. 판마다 고르는 맵 목록(GameSettings.gameMapScenes)의 테스트 씬 이름을 바꾼다.
    public static void AddToBuildSettings(string scenePath, string mapName)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == scenePath)) scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        foreach (EditorBuildSettingsScene s in scenes)
            if (s.path == TestMapBuilder.ScenePath(mapName)) s.enabled = false;
        EditorBuildSettings.scenes = scenes.ToArray();

        var settings = AssetDatabase.LoadAssetAtPath<GameSettingsSO>("Assets/Resources/GameSettings.asset");
        if (settings == null) return;
        var so = new SerializedObject(settings);
        SerializedProperty list = so.FindProperty("gameMapScenes");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).stringValue == TestMapBuilder.SceneName(mapName)) list.GetArrayElementAtIndex(i).stringValue = mapName;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    // 건물 재료 고정(간판이 재료를 말하는 맵): Building_Q1~Q4 순서대로 itemNames.
    public static string FixRecipe(string recipePath, string[] itemNames)
    {
        var recipe = AssetDatabase.LoadAssetAtPath<EscapeRecipeSO>(recipePath);
        if (recipe == null) return "recipe missing";
        ItemSO[] materials = itemNames.Select(n => AssetDatabase.FindAssets(n + " t:ItemSO").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ItemSO>).FirstOrDefault(i => i != null && i.name == n)).ToArray();
        if (materials.Any(m => m == null)) return "recipe items missing";
        recipe.EditorSetBuildingMaterials(materials, true);
        EditorUtility.SetDirty(recipe);
        AssetDatabase.SaveAssets();
        return "recipe fixed " + string.Join(",", materials.Select(m => m.ItemId));
    }

    // 통행 검사 목표: 상자·로켓·무지개·쿠키 스폰·수리 자리·탈출구·숨을 곳 안.
    public static IEnumerable<(string, Vector3)> PassTargets(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.StartsWith("CHEST_Slot_") || n.StartsWith("RocketSpawn_") || n.StartsWith("RainbowSpawn_") || n.StartsWith("Spawn_Cookie_")
                    || n.StartsWith("Repair_Point") || n.StartsWith("ExitTrigger_") || n == "Zone_Inside")
                    yield return (n + (n.StartsWith("Repair_Point") || n == "Zone_Inside" ? "@" + t.parent.parent.name + "/" + t.parent.name : ""), t.position);
            }
    }
}
