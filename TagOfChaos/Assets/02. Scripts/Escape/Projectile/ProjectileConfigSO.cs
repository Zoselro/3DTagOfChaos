using UnityEngine;

// 던지는 물체 하나의 수치(Request1009Plan.md §70.3.4). 새 투척 아이템은 이 에셋을 하나 만들어 ToolSO에 연결하면 된다 —
// 던지는 계산(ThrowSolver)·날아가는 계산(BallisticSim)·미리보기·방장 판정이 모두 이 값을 읽는다.
[CreateAssetMenu(menuName = "TagOfChaos/Escape/Projectile", fileName = "Projectile_")]
public class ProjectileConfigSO : ScriptableObject
{
    [Header("Throw")]
    [Tooltip("Distance (m) when aiming at the sky — the projectile lands this far ahead.")]
    [Min(1f)] public float baseRange = 12f;
    [Tooltip("Maximum horizontal distance (m) even when aiming at a far wall or floor.")]
    [Min(1f)] public float maxRange = 20f;
    [Range(10f, 75f)] public float launchAngle = 45f;

    [Header("Flight")]
    [Min(0.02f)] public float radius = 0.15f;
    [Min(1f)] public float gravity = 9.81f;
    [Tooltip("Share of the speed into a surface that bounces back.")]
    [Range(0f, 0.9f)] public float bounciness = 0.4f;
    [Tooltip("Share of the speed along a surface lost on each bounce.")]
    [Range(0f, 1f)] public float surfaceFriction = 0.2f;
    [Tooltip("Rolling slow-down (m/s per second) while touching the ground.")]
    [Min(0f)] public float rollDeceleration = 4f;
    [Tooltip("Below this speed on the ground the projectile stops.")]
    [Min(0f)] public float restSpeed = 0.35f;

    [Header("Trigger")]
    [Tooltip("Water balloon: bursts on the first touch — a character is hit, anything else just splashes.")]
    public bool burstOnImpact;
    [Tooltip("Seconds after the first touch until it goes off (bombs).")]
    [Min(0f)] public float fuseAfterImpact = 2f;
    [Tooltip("Goes off at this age even if it never touched anything.")]
    [Min(0.5f)] public float maxLifeSeconds = 5f;

    [Header("Look")]
    [Tooltip("Model shown while flying. Empty = the item's model (or a built-in shape).")]
    public GameObject model;
    [Min(0.01f)] public float modelScale = 1f;
    [Tooltip("Spin while flying (degrees per second).")]
    public float spinSpeed = 540f;
}
