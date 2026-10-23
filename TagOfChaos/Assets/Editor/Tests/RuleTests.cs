using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

// 네트워크·씬 없이 검증할 수 있는 게임 규칙의 EditMode 테스트(research.md §12 E10).
// 오프라인 Play Mode 검증으로는 재현이 어려운 규칙(방장 정책, 인원별 수치, 키 수명 분류, 단계 해석, 직렬화 순서)을
// 회귀 방지용으로 고정한다. Test Runner(Window > General > Test Runner)의 EditMode에서 실행한다.
public class RuleTests
{
    // ---- 방장 정책(Bug-fix-plan.md §26.8) ----

    [Test]
    public void DesiredMaster_IsEarliestJoinedNonMonster()
    {
        Assert.AreEqual(1, RoomState.DesiredMasterActor(new[] { 3, 1, 2, 4 }, new int[0]));
        Assert.AreEqual(2, RoomState.DesiredMasterActor(new[] { 1, 2, 3, 4 }, new[] { 1 }));
        Assert.AreEqual(3, RoomState.DesiredMasterActor(new[] { 1, 2, 3, 4 }, new[] { 1, 2 }));
        Assert.AreEqual(2, RoomState.DesiredMasterActor(new[] { 4, 2, 7 }, null));
    }

    [Test]
    public void DesiredMaster_IsNone_WhenOnlyMonstersRemain()
    {
        Assert.AreEqual(-1, RoomState.DesiredMasterActor(new[] { 5 }, new[] { 5 }));
        Assert.AreEqual(-1, RoomState.DesiredMasterActor(new int[0], new int[0]));
    }

    // 호스트(시작 버튼 주인, Bug-fix-plan.md §33)는 괴물 여부와 무관하게 가장 먼저 들어온 사람이다.
    [Test]
    public void Host_IsEarliestJoined_EvenIfMonster()
    {
        int[] actors = { 1, 2, 3, 4 };
        Assert.AreEqual(1, RoomState.HostActor(actors));
        Assert.AreEqual(2, RoomState.DesiredMasterActor(actors, new[] { 1 })); // 진행 권한은 괴물이 아닌 사람에게
        Assert.AreEqual(1, RoomState.HostActor(actors));                       // 시작 버튼은 그대로 방 생성자
        Assert.AreEqual(3, RoomState.HostActor(new[] { 4, 3 }));               // 앞선 입장자들이 나가면 다음 입장자
        Assert.AreEqual(-1, RoomState.HostActor(new int[0]));
    }

    // ---- 인원별 설정 계산 ----

    private static GameSettingsSO CreateSettings(int maxPlayers, int monsterCount)
    {
        var settings = ScriptableObject.CreateInstance<GameSettingsSO>();
        var so = new SerializedObject(settings);
        so.FindProperty("maxPlayers").intValue = maxPlayers;
        so.FindProperty("monsterCount").intValue = monsterCount;
        so.ApplyModifiedPropertiesWithoutUndo();
        return settings;
    }

    [Test]
    public void MonsterCount_LeavesAtLeastOneCookie()
    {
        GameSettingsSO settings = CreateSettings(8, 2);
        Assert.AreEqual(2, settings.MonsterCountFor(8));
        Assert.AreEqual(1, settings.MonsterCountFor(2)); // 쿠키가 최소 1명
        Assert.AreEqual(1, settings.MonsterCountFor(1));
        Object.DestroyImmediate(settings);
    }

    // ---- Props 키 수명 분류(research.md §12 E8) ----

    [Test]
    public void EveryNetKey_IsDeclaredInScopesExactlyOnce()
    {
        var declared = new Dictionary<string, int>();
        foreach (NetKeys.Scope scope in NetKeys.Scopes)
        {
            declared.TryGetValue(scope.Key, out int count);
            declared[scope.Key] = count + 1;
        }

        foreach (FieldInfo field in typeof(NetKeys).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!field.IsLiteral || field.FieldType != typeof(string)) continue;
            string key = (string)field.GetRawConstantValue();
            Assert.IsTrue(declared.TryGetValue(key, out int count), $"NetKeys.{field.Name} is missing from NetKeys.Scopes (declare its target and lifetime).");
            Assert.AreEqual(1, count, $"NetKeys.{field.Name} is declared more than once in NetKeys.Scopes.");
        }
    }

    [Test]
    public void RoundKeys_MatchDeclaredLifetimes()
    {
        CollectionAssert.Contains(NetKeys.RoundRoomKeys, NetKeys.MonsterActorNumbers);
        CollectionAssert.Contains(NetKeys.RoundRoomKeys, NetKeys.PrepareEndTime);
        CollectionAssert.Contains(NetKeys.RoundRoomKeys, NetKeys.GameResult);
        CollectionAssert.Contains(NetKeys.RoundPlayerKeys, NetKeys.HitCount);
        CollectionAssert.Contains(NetKeys.RoundPlayerKeys, NetKeys.SelectedSlot);
        CollectionAssert.DoesNotContain(NetKeys.RoundPlayerKeys, NetKeys.SkinIndex); // 스킨은 판이 바뀌어도 유지
        CollectionAssert.Contains(NetKeys.RoundRoomKeys, NetKeys.DoorStates);
        CollectionAssert.Contains(NetKeys.RoundRoomKeys, NetKeys.GameMapScene); // 이번 판 맵은 판이 끝나면 지운다

        // 개수는 선언 표에서 센다 — 키를 추가할 때마다 이 숫자를 손으로 고치다 빠뜨리던 문제(GameScenePlan.md P13)
        int declaredRoundRoomKeys = 0;
        foreach (NetKeys.Scope scope in NetKeys.Scopes)
            if (scope.Target == NetKeys.Target.Room && scope.Lifetime == NetKeys.Lifetime.Round) declaredRoundRoomKeys++;
        Assert.AreEqual(declaredRoundRoomKeys, NetKeys.RoundRoomKeys.Length);
    }

    [Test]
    public void GameMap_PicksFromListExcludingPrevious()
    {
        var settings = ScriptableObject.CreateInstance<GameSettingsSO>();
        var so = new UnityEditor.SerializedObject(settings);
        var list = so.FindProperty("gameMapScenes");
        list.arraySize = 2;
        list.GetArrayElementAtIndex(0).stringValue = "MapA";
        list.GetArrayElementAtIndex(1).stringValue = "MapB";
        so.ApplyModifiedPropertiesWithoutUndo();

        for (int i = 0; i < 20; i++) Assert.AreEqual("MapB", settings.PickGameMap("MapA"));
        Assert.AreEqual("MapA", settings.FirstGameMap);

        var empty = ScriptableObject.CreateInstance<GameSettingsSO>();
        Assert.IsNull(empty.PickGameMap(null), "An empty map list must not fall back to a scene outside the build.");
        Assert.IsNull(empty.FirstGameMap);
        Object.DestroyImmediate(settings);
        Object.DestroyImmediate(empty);
    }

    // 판에서 고르는 맵은 모두 활성화된 빌드 씬이어야 한다 — 빌드에서 빠진 씬을 고르면 빌드에서만 로드가 실패한다(Bug-fix-plan.md §41 ㊷).
    [Test]
    public void GameMaps_MatchEnabledBuildMapScenes()
    {
        var settings = Resources.Load<GameSettingsSO>("GameSettings");
        Assert.IsNotNull(settings, "Resources/GameSettings is missing.");
        Assert.IsNotNull(settings.FirstGameMap, "gameMapScenes is empty.");

        var enabled = new HashSet<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled) enabled.Add(System.IO.Path.GetFileNameWithoutExtension(scene.path));

        foreach (string map in settings.GameMapScenes)
            Assert.IsTrue(enabled.Contains(map), $"Game map '{map}' is not an enabled build scene.");

        // 반대 방향: 빌드에 들어간 맵 씬(Assets/Scenes/Maps/)은 모두 판에서 선택될 수 있어야 한다 — 예전에는 두 맵이 빌드에만 있고
        // 목록에 없어 절대 선택되지 않았다(Bug-fix-plan.md §41 ㊺).
        var listed = new HashSet<string>(settings.GameMapScenes);
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled || !scene.path.StartsWith("Assets/Scenes/Maps/")) continue;
            string name = System.IO.Path.GetFileNameWithoutExtension(scene.path);
            Assert.IsTrue(listed.Contains(name), $"Build map scene '{name}' is missing from GameSettings.gameMapScenes.");
        }
    }

    // ---- 게임 씬 공통 구성(Bug-fix-plan.md §41 ㊶) ----

    // 게임 규칙·HUD는 프리팹 GameSceneCore 하나에만 있어야 한다. 예전에는 GameScene을 복제한 맵 씬 6개가 같은 매니저·UI를 각자
    // 직렬화해, 한 씬만 고치면 특정 맵에서만 다르게 동작했다. 씬 파일을 직접 읽어 검사한다(씬을 열지 않음).
    [Test]
    public void GameScenes_UseSingleSceneCorePrefab()
    {
        string coreGuid = AssetDatabase.AssetPathToGUID("Assets/04. Prefabs/Scene/GameSceneCore.prefab");
        Assert.IsNotEmpty(coreGuid, "GameSceneCore prefab is missing.");

        var sceneOwnedTypes = new[] { "GameManager", "GameRuleController", "MonsterJoinController", "ResultScreenController" };
        var scriptGuids = new List<string>();
        foreach (string type in sceneOwnedTypes)
        {
            string[] found = AssetDatabase.FindAssets($"t:MonoScript {type}", new[] { "Assets/02. Scripts" });
            foreach (string guid in found)
                if (System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)) == type) scriptGuids.Add(guid);
        }
        Assert.AreEqual(sceneOwnedTypes.Length, scriptGuids.Count, "Could not resolve every scene-core script.");

        var corePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/04. Prefabs/Scene/GameSceneCore.prefab");
        int sceneViewCount = corePrefab.GetComponentsInChildren<Photon.Pun.PhotonView>(true).Length;

        // 개발용 PlayerTestScene은 빌드 목록에서 뺐다(2026-10-12) — 디스크에 있을 때만 함께 검사한다.
        var paths = new List<string>();
        if (System.IO.File.Exists("Assets/Scenes/PlayerTestScene.unity")) paths.Add("Assets/Scenes/PlayerTestScene.unity");
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.path.StartsWith("Assets/Scenes/Maps/")) paths.Add(scene.path);

        foreach (string path in paths)
        {
            string text = System.IO.File.ReadAllText(path);
            int instances = System.Text.RegularExpressions.Regex.Matches(text, $"m_SourcePrefab: {{fileID: 100100000, guid: {coreGuid}").Count;
            Assert.AreEqual(1, instances, $"{path} must contain exactly one GameSceneCore instance.");

            // PUN은 프리팹 안의 PhotonView를 sceneViewId 0으로 되돌린다 — 씬 인스턴스가 0이 아닌 ID를 오버라이드로 가져야 한다.
            // GameSceneCore를 대상으로 한 오버라이드만 센다(테스트 씬에 직접 둔 캐릭터 프리팹 등 다른 PhotonView는 제외).
            // 0이면 실행 중 ViewID가 0이 되어 채팅 RPC가 실패했다(Bug-fix-plan.md §41 ㊶ 검증 중 발견).
            var ids = new HashSet<string>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, $@"guid: {coreGuid}, type: 3}}\s*\n\s*propertyPath: sceneViewId\s*\n\s*value: (\d+)"))
                if (m.Groups[1].Value != "0") ids.Add(m.Groups[1].Value);
            Assert.AreEqual(sceneViewCount, ids.Count, $"{path}: every GameSceneCore PhotonView needs a unique non-zero sceneViewId override.");
            foreach (string guid in scriptGuids)
                StringAssert.DoesNotContain($"m_Script: {{fileID: 11500000, guid: {guid}", text,
                    $"{path} serializes a scene-core component directly ({AssetDatabase.GUIDToAssetPath(guid)}). Edit GameSceneCore.prefab instead.");
        }
    }

    // ---- 괴물 촉수 돌진 지면 따라가기(Bug-fix-plan.md §41 ㊳) ----

    [Test]
    public void TentacleDash_FollowsWalkableSlope_KeepsHorizontalOnSteepOrAir()
    {
        Vector3 forward = Vector3.forward;
        Vector3 uphill = Quaternion.AngleAxis(-30f, Vector3.right) * Vector3.up;   // 앞으로 올라가는 30° 경사의 법선
        Vector3 downhill = Quaternion.AngleAxis(30f, Vector3.right) * Vector3.up;  // 앞으로 내려가는 30° 경사
        Vector3 cliff = Quaternion.AngleAxis(-60f, Vector3.right) * Vector3.up;    // 60° — 최대 경사(45°) 초과

        Vector3 up = MonsterTentacleDash.DirectionOnGround(forward, true, uphill, 45f);
        Vector3 down = MonsterTentacleDash.DirectionOnGround(forward, true, downhill, 45f);
        Assert.AreEqual(30f, Vector3.Angle(forward, up), 0.01f);
        Assert.Greater(up.y, 0f, "Uphill dash must climb the slope instead of tunneling into it.");
        Assert.Less(down.y, 0f, "Downhill dash must follow the slope instead of flying off it.");
        Assert.AreEqual(1f, up.magnitude, 1e-4f);

        Assert.AreEqual(forward, MonsterTentacleDash.DirectionOnGround(forward, true, cliff, 45f));
        Assert.AreEqual(forward, MonsterTentacleDash.DirectionOnGround(forward, false, uphill, 45f));
    }

    // ---- 게임 단계 해석(research.md §12 E2) ----

    [Test]
    public void GamePhase_IsDerivedFromRoomProps()
    {
        Assert.AreEqual(GamePhase.Lobby, GamePhaseState.Evaluate(false, 0, false, 100));
        Assert.AreEqual(GamePhase.Prepare, GamePhaseState.Evaluate(true, 160, false, 100));
        Assert.AreEqual(GamePhase.Hunt, GamePhaseState.Evaluate(true, 160, false, 160), "monster doors open at PrepareEndTime");
        Assert.AreEqual(GamePhase.TimeAttack, GamePhaseState.Evaluate(true, 160, false, 400, witchTimer: true));
        Assert.AreEqual(GamePhase.Result, GamePhaseState.Evaluate(true, 160, true, 900, witchTimer: true));
    }

    // ---- 스킨 목록 ----

    [Test]
    public void SkinCatalog_ClampsOutOfRangeIndex()
    {
        var catalog = ScriptableObject.CreateInstance<SkinCatalogSO>();
        var so = new SerializedObject(catalog);
        SerializedProperty skins = so.FindProperty("skins");
        skins.arraySize = 3;
        for (int i = 0; i < 3; i++) skins.GetArrayElementAtIndex(i).FindPropertyRelative("label").stringValue = ((char)('A' + i)).ToString();
        so.ApplyModifiedPropertiesWithoutUndo();

        Assert.AreEqual(3, catalog.Count);
        Assert.AreEqual("A", catalog.GetLabel(-5));
        Assert.AreEqual("C", catalog.GetLabel(99));
        Object.DestroyImmediate(catalog);
    }

    // ---- 캐릭터 동기화 직렬화(research.md §12 E1 — 공용 제네릭으로 바꿔도 순서·값이 같아야 기존 빌드와 호환) ----

    // 쓰기 스트림의 버퍼는 PUN 내부(SetWriteStream, internal)에서만 설정되므로 테스트에서는 리플렉션으로 빈 버퍼를 넣는다.
    private static PhotonStream CreateWriteStream()
    {
        var stream = new PhotonStream(true, null);
        typeof(PhotonStream).GetField("writeData", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(stream, new List<object>());
        return stream;
    }

    [Test]
    public void PlayerSync_RoundTripsStateAndCarryFlag()
    {
        var go = new GameObject("SyncTest");
        try
        {
            go.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(0, 90, 0));
            PhotonStream writer = CreateWriteStream();
            new PlayerNetworkSync().Write(writer, go.transform, PlayerMoveState.Dodge, true, 1007);

            object[] sent = writer.ToArray();
            Assert.AreEqual(5, sent.Length);
            Assert.AreEqual((int)PlayerMoveState.Dodge, sent[2]); // 상태는 int로 전송

            var reader = new PhotonStream(false, sent);
            var sync = new PlayerNetworkSync();
            var target = new GameObject("SyncTarget");
            try
            {
                sync.Read(reader, target.transform);
                Assert.AreEqual(PlayerMoveState.Dodge, sync.RemoteState);
                Assert.IsTrue(sync.RemoteIsCarrying);
                Assert.AreEqual(1007, sync.RemoteCarrierViewId);
                Assert.AreEqual(new Vector3(1, 2, 3), target.transform.position); // 첫 수신은 스냅
            }
            finally { Object.DestroyImmediate(target); }
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void MonsterSync_RoundTripsState()
    {
        var go = new GameObject("MonsterSyncTest");
        try
        {
            PhotonStream writer = CreateWriteStream();
            new NetworkTransformSync<MonsterMoveState>().Write(writer, go.transform, MonsterMoveState.GrabKill);
            var sync = new NetworkTransformSync<MonsterMoveState>();
            sync.Read(new PhotonStream(false, writer.ToArray()), go.transform);
            Assert.AreEqual(MonsterMoveState.GrabKill, sync.RemoteState);
        }
        finally { Object.DestroyImmediate(go); }
    }

    // ---- UI 배치(Bug-fix-plan.md §28 R2의 검사 도구를 회귀 테스트로) ----

    [Test]
    public void UiLayout_HasNoOffscreenElements()
    {
        Assert.AreEqual(0, UILayoutValidator.ValidateBuildScenesAndPrefabs(), "See [UILayoutValidator] warnings in the Console.");
    }

    // ---- 서버 지역(Request1003Plan.md §1) ----

    [TestCase("kr/*", "kr")]
    [TestCase("JP", "jp")]
    [TestCase("asia", "asia")]
    [TestCase("", null)]
    public void Region_Normalize(string cloud, string expected)
    {
        Assert.AreEqual(expected, PhotonRegions.Normalize(cloud));
    }

    [Test]
    public void RegionMove_EncodeDecode_RoundTrips()
    {
        Assert.IsTrue(RegionMove.TryDecode(RegionMove.Encode("jp", "ab12cd34"), out string region, out string token));
        Assert.AreEqual("jp", region);
        Assert.AreEqual("ab12cd34", token);
        Assert.IsFalse(RegionMove.TryDecode("xx|tok", out _, out _), "unknown region");
        Assert.IsFalse(RegionMove.TryDecode("jp|", out _, out _), "empty token");
        Assert.IsFalse(RegionMove.TryDecode(42, out _, out _), "not a string");
        Assert.AreNotEqual(RegionMove.NewToken(), RegionMove.NewToken());
    }

    // 판이 시작되기 전(대기실)에, 다른 올바른 지역을 골랐을 때만 방을 옮긴다.
    [TestCase("jp", "kr", GamePhase.Lobby, true)]
    [TestCase("kr", "kr", GamePhase.Lobby, false)]
    [TestCase(null, "kr", GamePhase.Lobby, false)]
    [TestCase("zz", "kr", GamePhase.Lobby, false)]
    [TestCase("jp", "kr", GamePhase.Prepare, false)]
    [TestCase("jp", null, GamePhase.Lobby, false)]
    public void RegionMove_OnlyFromLobbyToAnotherRegion(string requested, string current, GamePhase phase, bool expected)
    {
        Assert.AreEqual(expected, RoomSettingsAuthority.ShouldMoveRegion(requested, current, phase));
    }
}
