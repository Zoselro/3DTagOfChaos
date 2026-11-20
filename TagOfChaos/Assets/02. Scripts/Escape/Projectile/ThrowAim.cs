using UnityEngine;

// "지금 보는 곳으로 던지면"의 시작점·처음 속도·목표(Request1009Plan.md §70.3.4). 미리보기와 실제 던지기가 같은 함수를 쓴다.
public readonly struct ThrowAim
{
    public readonly Vector3 Origin, Velocity, Target;

    private ThrowAim(Vector3 origin, Vector3 velocity, Vector3 target)
    {
        Origin = origin;
        Velocity = velocity;
        Target = target;
    }

    // §90 B3 곧게 나는 로켓: 시작점에서 dir로 speed(목표 = 사거리 끝).
    public static ThrowAim Straight(Vector3 origin, Vector3 dir, float speed, float range) =>
        new ThrowAim(origin, dir.normalized * speed, origin + dir.normalized * range);

    public static ThrowAim FromCamera(Camera cam, Vector3 hand, Vector3 fallbackForward, ProjectileConfigSO c)
    {
        Ray aim = cam != null ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)) : new Ray(hand, fallbackForward);
        float cameraToHand = Mathf.Max(0f, Vector3.Dot(hand - aim.origin, aim.direction)); // 3인칭 카메라는 몸 뒤에 있다
        Vector3 target = ThrowSolver.TargetPoint(aim, hand, c, ProjectileWorld.Mask, cameraToHand);
        return new ThrowAim(hand, ThrowSolver.LaunchVelocity(hand, target, c.launchAngle, c.gravity), target);
    }
}
