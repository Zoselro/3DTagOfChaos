using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 괴물 돌진 준비 자세 클립(Request1009Plan.md §69 F2 — 37번). 돌진 클립(TentacleDash)의 첫 자세를 리그에 샘플하고, 그 자세에서 상체를 숙이고 팔·촉수를 뒤로 당긴
// 자세로 0.35초 동안 옮겨 가는 클립(Monster_TentacleDashWindup.anim)을 만든다. 같은 리그·같은 뼈 축으로 만들어 돌진 동작과 이어 붙는다.
// MonsterAnimator에 TentacleDashWindup 상태·트리거(다른 상태와 같은 AnyState 방식)도 넣는다.
public static class MonsterWindupClipBuilder
{
    public const string ClipPath = "Assets/Animation/Monster/NewAnimation/Monster_TentacleDashWindup.anim";
    public const string ControllerPath = "Assets/Animation/MonsterAnimator.controller";
    public const string StateName = "TentacleDashWindup";
    private const float Fps = 30f;

    // (뼈, 각도, 움직임 확인 대상, 그 대상이 가야 할 방향: +1 = 앞, -1 = 뒤)
    private struct Bend { public string Bone; public float Degrees; public string Probe; public int Toward; }
    private static readonly Bend[] Bends =
    {
        new Bend { Bone = "Spine", Degrees = 18f, Probe = "Head", Toward = +1 },
        new Bend { Bone = "Chest", Degrees = 10f, Probe = "Head", Toward = +1 },
        new Bend { Bone = "UpperArm_Top_L", Degrees = 50f, Probe = "Hand_Top_L", Toward = -1 },
        new Bend { Bone = "UpperArm_Top_R", Degrees = 50f, Probe = "Hand_Top_R", Toward = -1 },
        new Bend { Bone = "UpperArm_Low_L", Degrees = 40f, Probe = "Hand_Low_L", Toward = -1 },
        new Bend { Bone = "UpperArm_Low_R", Degrees = 40f, Probe = "Hand_Low_R", Toward = -1 },
        new Bend { Bone = "Tentacle_01_L", Degrees = 40f, Probe = "Tentacle_06_L", Toward = -1 },
        new Bend { Bone = "Tentacle_01_R", Degrees = 40f, Probe = "Tentacle_06_R", Toward = -1 },
        new Bend { Bone = "Head", Degrees = 14f, Probe = "Horn", Toward = -1 }, // 고개는 다시 들어 앞을 본다
    };
    private static readonly float[] Times = { 0f, 4f / Fps, 7f / Fps, 10f / Fps };
    private static readonly float[] Amounts = { 0f, 0.75f, 1f, 1.08f };

    [MenuItem("TagOfChaos/Monster/Build Dash Windup Clip")]
    public static void Build()
    {
        var prefab = Resources.Load<GameObject>("MonsterPlayer");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimationClip dash = null;
        foreach (AnimationClip c in controller.animationClips) if (c.name == "TentacleDash") dash = c;
        if (prefab == null || dash == null) { Debug.LogError("[Windup] MonsterPlayer or TentacleDash clip not found."); return; }

        GameObject inst = Object.Instantiate(prefab);
        try
        {
            Animator animator = inst.GetComponentInChildren<Animator>();
            Transform root = animator.transform;
            dash.SampleAnimation(root.gameObject, 1f / Fps);

            var paths = new List<string>();
            foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(dash))
                if (b.type == typeof(Transform) && !paths.Contains(b.path)) paths.Add(b.path);
            var bones = new Dictionary<string, Transform>();
            var basePos = new Dictionary<string, Vector3>();
            var baseRot = new Dictionary<string, Quaternion>();
            foreach (string p in paths)
            {
                Transform t = root.Find(p);
                if (t == null) continue;
                bones[p] = t; basePos[p] = t.localPosition; baseRot[p] = t.localRotation;
            }
            Transform Bone(string name) { foreach (var kv in bones) if (kv.Value.name == name) return kv.Value; return null; }
            void Restore() { foreach (var kv in bones) { kv.Value.localPosition = basePos[kv.Key]; kv.Value.localRotation = baseRot[kv.Key]; } }

            // 각 굽힘의 부호: 확인 대상이 원하는 쪽(앞/뒤)으로 가는 쪽을 고른다
            Vector3 fwd = root.forward, axis = root.right;
            var signs = new float[Bends.Length];
            for (int i = 0; i < Bends.Length; i++)
            {
                Transform bone = Bone(Bends[i].Bone), probe = Bone(Bends[i].Probe);
                if (bone == null || probe == null) { Debug.LogWarning("[Windup] Missing bone " + Bends[i].Bone + "/" + Bends[i].Probe); continue; }
                Restore();
                Vector3 before = probe.position;
                bone.rotation = Quaternion.AngleAxis(Bends[i].Degrees, axis) * bone.rotation;
                float moved = Vector3.Dot(probe.position - before, fwd);
                signs[i] = Mathf.Sign(moved) == Bends[i].Toward ? 1f : -1f;
            }

            var clip = new AnimationClip { frameRate = Fps, name = StateName };
            var curves = new Dictionary<string, AnimationCurve[]>();
            foreach (string p in bones.Keys) curves[p] = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            var last = new Dictionary<string, Quaternion>();
            for (int k = 0; k < Times.Length; k++)
            {
                Restore();
                for (int i = 0; i < Bends.Length; i++)
                {
                    Transform bone = Bone(Bends[i].Bone);
                    if (bone != null) bone.rotation = Quaternion.AngleAxis(signs[i] * Bends[i].Degrees * Amounts[k], axis) * bone.rotation;
                }
                foreach (var kv in bones)
                {
                    Quaternion q = kv.Value.localRotation;
                    if (last.TryGetValue(kv.Key, out Quaternion prev) && Quaternion.Dot(prev, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    last[kv.Key] = q;
                    Vector3 pos = kv.Value.localPosition;
                    AnimationCurve[] c = curves[kv.Key];
                    c[0].AddKey(Times[k], pos.x); c[1].AddKey(Times[k], pos.y); c[2].AddKey(Times[k], pos.z);
                    c[3].AddKey(Times[k], q.x); c[4].AddKey(Times[k], q.y); c[5].AddKey(Times[k], q.z); c[6].AddKey(Times[k], q.w);
                }
            }
            string[] props = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
            foreach (var kv in curves)
                for (int j = 0; j < props.Length; j++)
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(kv.Key, typeof(Transform), props[j]), kv.Value[j]);
            clip.EnsureQuaternionContinuity();

            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (existing != null) { EditorUtility.CopySerialized(clip, existing); clip = existing; }
            else AssetDatabase.CreateAsset(clip, ClipPath);
            AddState(controller, clip);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Windup] Saved {ClipPath} ({clip.length:F2}s, {curves.Count} bones, signs {string.Join(",", signs)}).");
        }
        finally { Object.DestroyImmediate(inst); }
    }

    private static void AddState(AnimatorController controller, AnimationClip clip)
    {
        bool hasParam = false;
        foreach (AnimatorControllerParameter p in controller.parameters) if (p.name == StateName) hasParam = true;
        if (!hasParam) controller.AddParameter(StateName, AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState state = null;
        foreach (ChildAnimatorState s in sm.states) if (s.state.name == StateName) state = s.state;
        if (state == null) state = sm.AddState(StateName, new Vector3(250f, 300f, 0f));
        state.motion = clip;
        foreach (AnimatorStateTransition t in sm.anyStateTransitions) if (t.destinationState == state) return;
        AnimatorStateTransition any = sm.AddAnyStateTransition(state);
        any.hasExitTime = false; any.duration = 0.05f; any.canTransitionToSelf = false;
        any.AddCondition(AnimatorConditionMode.If, 0f, StateName);
        EditorUtility.SetDirty(controller);
    }
}
