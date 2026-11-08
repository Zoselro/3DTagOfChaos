using UnityEditor;
using UnityEngine;

// 콜라 대포 탈것 프리팹 만들기(Request1009Plan.md §68.9): Blender FBX(받침 > 회전대 > 포신 > 해치, 각 원점 = 회전축)
// → Resources/Escape/ColaCannon.prefab(메시 충돌체 + ColaCannonView). 손에 드는 작은 모델은 ITEM_ColaCannon.fbx.
public static class ColaCannonSetup
{
    public const string ModelPath = "Assets/09. Environment/Escape/Models/PROP_ColaCannon.fbx";
    public const string PrefabPath = "Assets/Resources/Escape/ColaCannon.prefab";

    [MenuItem("TagOfChaos/Escape/Build Cola Cannon Prefab")]
    public static void BuildMenu() => Debug.Log(Build() ? "[ColaCannon] Prefab built: " + PrefabPath : "[ColaCannon] Build failed.");

    public static bool Build() => PumpkinCarriageSetup.BuildVehicle<ColaCannonView>(ModelPath, PrefabPath, "ColaCannon");
}
