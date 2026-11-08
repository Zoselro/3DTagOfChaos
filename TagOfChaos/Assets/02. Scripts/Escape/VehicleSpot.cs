using UnityEngine;

// 소환형 탈것(호박 마차 §68.7·콜라 대포 §68.9)의 설치 자리 고르기. 쓴 사람이 보는 쪽 distance m(막히면 옆·뒤)에서
// 탈것 상자가 아무것에도 끼지 않고, 바닥 높이가 비슷하고, 방장 검사(IsValidTeleport)도 통과하는 곳. 타는 쪽(faceLocal)이 쓴 사람을 본다.
public static class VehicleSpot
{
    private static readonly float[] Offsets = { 0f, 40f, -40f, 80f, -80f, 180f };

    // 쓴 사람(user)이 보는 쪽(forward)에서 offset만큼 돈 방향으로 distance 떨어진 자리, 그리고 탈것 로컬 faceLocal이 쓴 사람을 보게 하는 Y 회전.
    public static Vector3 Point(Vector3 user, Vector3 forward, float offsetDegrees, float distance, Vector3 faceLocal, out float yaw)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
        Vector3 dir = Quaternion.Euler(0f, offsetDegrees, 0f) * forward.normalized;
        faceLocal.y = 0f;
        yaw = Vector3.SignedAngle(faceLocal.normalized, -dir, Vector3.up);
        return user + dir * distance;
    }

    public static bool TryFind(EscapeManager manager, Vector3 user, Vector3 forward, float distance, Vector3 faceLocal, Vector3 halfExtents,
        out Vector3 spot, out float yaw)
    {
        foreach (float offset in Offsets)
        {
            Vector3 at = Point(user, forward, offset, distance, faceLocal, out yaw);
            Vector3 ground = manager.SnapToGround(at + Vector3.up * 1f);
            if (Mathf.Abs(ground.y - user.y) > 1.5f || !manager.IsValidTeleport(ground) || !BoxClear(ground, yaw, halfExtents)) continue;
            spot = ground;
            return true;
        }
        spot = default;
        yaw = 0f;
        return false;
    }

    private static bool BoxClear(Vector3 ground, float yaw, Vector3 halfExtents)
    {
        Vector3 half = halfExtents;
        half.y -= 0.15f; // 바닥 면은 빼고
        Vector3 center = ground + Vector3.up * (halfExtents.y + 0.15f);
        foreach (Collider c in Physics.OverlapBox(center, half, Quaternion.Euler(0f, yaw, 0f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (c.GetComponentInParent<IGameCharacter>() == null && c.GetComponentInParent<DecoyView>() == null) return false;
        return true;
    }

    // 소환·사라짐·도착 연기(모든 화면).
    public static void Smoke(Vector3 point, float size, Color tint)
    {
        ParticleSystem smoke = EscapeVisuals.CreateParticles("VehicleSmoke", null, new EscapeVisuals.ParticleStyle
        {
            Lifetime = new Vector2(1.1f, 1.8f), Speed = new Vector2(1.2f * size, 2.6f * size), Size = new Vector2(1.2f * size, 2.4f * size),
            Color = new Color(1f, 0.93f, 0.85f, 0.8f), MaxParticles = 70, EmissionOff = true,
            Shape = ParticleSystemShapeType.Sphere, Radius = 0.8f * size,
            GrowTo = 2f, FadeFrom = Color.white, FadeTo = tint, AlphaFrom = 0.8f, Once = true,
        });
        smoke.transform.position = point + Vector3.up * (1f * size);
        smoke.Emit(60);
        Object.Destroy(smoke.gameObject, 2.5f);
    }

    // 탈것 부품(문·해치·포신)을 부모 공간의 축으로 돌린다(원점 = 회전축). rest = 처음 localRotation.
    public static void RotateAround(Transform part, Quaternion rest, float degrees, Vector3 worldAxis)
    {
        Vector3 axis = part.parent != null ? part.parent.InverseTransformDirection(worldAxis) : worldAxis;
        part.localRotation = Quaternion.AngleAxis(degrees, axis) * rest;
    }
}
