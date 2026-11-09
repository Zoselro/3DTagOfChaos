using System.Collections.Generic;
using UnityEngine;

// 술래 본인 1인칭 팔·촉수 연출(Request1009Plan.md §69 F2·F4 — 사용자: 평소에도 촉수와 손이 잘 보이고, 대쉬 때 역동적으로, GrabKill은 원래 동작 그대로).
// 본인 화면에서만 애니메이션 뒤에 촉수·팔 뼈를 돌려 카메라 기준 목표 방향으로 겨눈다(상대 화면은 원래 동작). 머리 위로 휘두르면 카메라를 관통해 보이지 않았다.
// 평소 = 촉수는 화면 위쪽 양옆, 손은 양쪽 아래 / 준비 = 촉수를 치켜들고 손을 당김 / 돌진 = 촉수·손을 가운데 앞으로 뻗음 / 끝 = 출렁이며 평소로 / 잡기 = 원래 동작.
public class MonsterTentacleFlourish : MonoBehaviour
{
    private enum Kind { Tentacle, ArmTop, ArmLow }

    private struct Chain
    {
        public Kind Kind;
        public float Side;          // 왼쪽 -1, 오른쪽 +1
        public Transform[] Bones;   // 뿌리 → 끝
        public Vector3 Aim;         // 지금 섞고 있는 목표 지점(옆, 위, 앞 m — 카메라 기준, 괴물 크기 3.14배 기준)
        public float Weight;
    }

    private struct Pose { public Vector3 Aim; public float Weight; public Pose(float x, float y, float z, float w) { Aim = new Vector3(x, y, z); Weight = w; } }

    // 끝이 향할 카메라 앞 지점(옆, 위, 앞 m)과 섞는 정도. 순서: 촉수, 위팔, 아래팔. 뿌리가 눈에서 옆으로 1~2 m 떨어져 있어 방향이 아니라 지점을 겨눈다.
    private static readonly Pose[] Idle = { new Pose(1.5f, 1.0f, 3.6f, 0.85f), new Pose(1.3f, -0.7f, 2.7f, 0.8f), new Pose(1.1f, -1.4f, 2.9f, 0.85f) };
    private static readonly Pose[] Windup = { new Pose(1.45f, 1.45f, 1.9f, 0.95f), new Pose(1.35f, -0.25f, 1.3f, 0.9f), new Pose(1.15f, -1.2f, 1.4f, 0.9f) };
    private static readonly Pose[] Strike = { new Pose(0.75f, 0.6f, 6f, 1f), new Pose(0.85f, -0.5f, 4.6f, 1f), new Pose(0.7f, -1.05f, 4.6f, 1f) };
    private const float ReferenceScale = 3.14f; // MonsterPlayer 크기

    private const float StrikeSeconds = 0.1f;
    private const float HoldSeconds = 0.3f;
    private const float SettleSeconds = 0.6f;
    private const float Follow = 14f;          // 평소 목표 따라가기
    private const float FollowFast = 40f;      // 돌진 순간
    private const float WaveDegrees = 9f;
    private const float WaveSpeed = 16f;

    private MonsterController monster;
    private MonsterViewSwitcher view;
    private readonly List<Chain> chains = new List<Chain>();
    private float dashStart = -10f;
    private bool wasDashing;

    public void Init(MonsterController owner, MonsterViewSwitcher switcher)
    {
        monster = owner;
        view = switcher;
        var byName = new Dictionary<string, Transform>();
        foreach (Transform t in owner.GetComponentsInChildren<Transform>(true)) byName[t.name] = t;
        foreach (string side in new[] { "L", "R" })
        {
            float s = side == "L" ? -1f : 1f;
            var tent = new string[6];
            for (int i = 0; i < 6; i++) tent[i] = $"Tentacle_{i + 1:00}_{side}";
            Add(byName, Kind.Tentacle, s, tent);
            Add(byName, Kind.ArmTop, s, $"UpperArm_Top_{side}", $"Forearm_Top_{side}", $"Hand_Top_{side}");
            Add(byName, Kind.ArmLow, s, $"UpperArm_Low_{side}", $"Forearm_Low_{side}", $"Hand_Low_{side}");
        }
    }

    private void Add(Dictionary<string, Transform> byName, Kind kind, float side, params string[] names)
    {
        var bones = new Transform[names.Length];
        for (int i = 0; i < names.Length; i++) if (!byName.TryGetValue(names[i], out bones[i])) return;
        chains.Add(new Chain { Kind = kind, Side = side, Bones = bones, Aim = Idle[(int)kind].Aim, Weight = 0f });
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (monster == null || view == null || cam == null || !view.IsFirstPerson || chains.Count == 0) return;
        float now = Time.time, dt = Time.deltaTime;
        bool dashing = monster.CurrentMoveState == MonsterMoveState.TentacleDash && monster.IsTentacleDashing;
        if (dashing && !wasDashing) dashStart = now;
        wasDashing = dashing;
        float t = now - dashStart;

        // 지금 단계의 목표
        Pose[] target; float wobble = 0f, wave = 0.35f, follow = Follow;
        if (monster.IsGrabKilling || monster.IsStunned) { target = null; wave = 0f; }              // 잡기·기절: 원래 동작 그대로
        else if (monster.IsDashWindingUp) { target = Windup; wave = 1f; }
        else if (t < StrikeSeconds + HoldSeconds) { target = Strike; wave = 0.9f; follow = FollowFast; }
        else if (t < StrikeSeconds + HoldSeconds + SettleSeconds)
        {
            float s = (t - StrikeSeconds - HoldSeconds) / SettleSeconds;
            target = Idle; wave = 1f - 0.6f * s;
            wobble = (1f - s) * Mathf.Sin(s * Mathf.PI * 3f) * 0.35f; // 출렁임
        }
        else target = Idle;
        if (monster.CurrentMoveState == MonsterMoveState.Walk) wave = Mathf.Max(wave, 0.6f);

        float k = 1f - Mathf.Exp(-follow * dt);
        Transform c = cam.transform;
        float scale = monster.transform.lossyScale.y / ReferenceScale;
        for (int i = 0; i < chains.Count; i++)
        {
            Chain ch = chains[i];
            Pose goal = target != null ? target[(int)ch.Kind] : new Pose(ch.Aim.x, ch.Aim.y, ch.Aim.z, 0f);
            Vector3 goalAim = goal.Aim + new Vector3(0f, wobble * 2f, -wobble);
            ch.Aim = Vector3.Lerp(ch.Aim, goalAim, k);
            ch.Weight = Mathf.Lerp(ch.Weight, goal.Weight, k);
            chains[i] = ch;
            if (ch.Weight < 0.002f) continue;

            Transform[] b = ch.Bones;
            Vector3 point = c.position + (c.right * (ch.Aim.x * ch.Side) + c.up * ch.Aim.y + c.forward * ch.Aim.z) * scale;
            Vector3 want = (point - b[0].position).normalized;
            Vector3 have = b[b.Length - 1].position - b[0].position;
            if (have.sqrMagnitude > 1e-6f)
                b[0].rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(have, want), ch.Weight) * b[0].rotation;

            // 마디마다 지나가는 물결(촉수는 크게, 팔은 작게)
            float amp = (ch.Kind == Kind.Tentacle ? WaveDegrees : WaveDegrees * 0.35f) * wave * ch.Weight;
            if (amp < 0.01f) continue;
            Vector3 axis = Vector3.Cross(want, c.up);
            if (axis.sqrMagnitude < 1e-6f) axis = c.right;
            axis.Normalize();
            for (int s = 1; s < b.Length; s++)
                b[s].rotation = Quaternion.AngleAxis(Mathf.Sin(now * WaveSpeed - s * 0.9f + ch.Side + (int)ch.Kind) * amp, axis) * b[s].rotation;
        }
    }
}
