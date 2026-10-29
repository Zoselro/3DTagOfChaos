using UnityEngine;

// 기절(EscapePlan.md §1.8, D24·D29). 쿠키·스파이·괴물 모두 도구에 맞으면 잠깐 움직일 수 없고, 머리 위에 별이 빙글빙글 돈다.
// 움직임 잠금은 본인 클라이언트의 캐릭터 코드가 IsStunned를 보고 한다(HideOrSeekPlayer.IsMovementLocked,
// MonsterController). 별 이펙트는 모든 클라이언트에 보인다. 캐릭터가 Awake에서 붙인다.
public class StunReceiver : MonoBehaviour
{
    private const int StarCount = 3;

    private float stunnedUntil;
    private Transform starRoot;
    private float headHeight = 2.2f;

    public bool IsStunned => Time.time < stunnedUntil;

    // 물풍선에 갇힘(Request1009Plan.md §45.7 — 옛 감옥 풍선을 합쳤다): 맞는 순간(hitAt, PhotonNetwork.Time)부터 5 m 공중의 물방울 안에
    // seconds 동안 갇힌다 — 움직일 수 없고, 괴물은 갇힌 쿠키를 잡지 못한다(MonsterGrabKillTrigger). 그 뒤 물방울이 터지고 떨어진다.
    // 별 대신 물방울이 보인다. 모든 화면이 같은 시각표로 돈다(늦게 받은 화면은 지난 만큼 건너뛴다).
    public const float FallStunSeconds = 0.8f; // 터진 뒤 떨어져 착지할 때까지 움직이지 못함
    private float jailedUntil;
    private WaterBubbleView bubble;
    private Animator trappedAnimator;
    private float animatorSpeed = 1f;
    public bool IsJailed => Time.time < jailedUntil;

    public void Trap(double hitAt, float seconds)
    {
        if (seconds <= 0f) return;
        float start = Time.time - (float)(Photon.Pun.PhotonNetwork.Time - hitAt);
        float release = start + seconds;
        if (release <= Time.time) return;
        jailedUntil = Mathf.Max(jailedUntil, release);
        stunnedUntil = Mathf.Max(stunnedUntil, release + FallStunSeconds);
        if (starRoot != null) starRoot.gameObject.SetActive(false);
        if (bubble == null) bubble = WaterBubbleView.Create(transform);
        var pose = GetComponent<PlayerActionPose>();
        if (pose != null) pose.SetTrapped(true);
        else
        {
            Animator a = GetComponentInChildren<Animator>();
            if (a != null && trappedAnimator == null) { trappedAnimator = a; animatorSpeed = a.speed; a.speed = 0.35f; } // 괴물: 느린 대기 동작
        }
        var view = GetComponent<Photon.Pun.PhotonView>();
        if (view == null || view.IsMine) WaterTrapMotion.Begin(gameObject, start, release);
        GameAudio.PlayAt(SoundId.BalloonSplash, transform.position);
    }

    private void EndTrap()
    {
        if (bubble != null) bubble.Burst();
        bubble = null;
        var pose = GetComponent<PlayerActionPose>();
        if (pose != null) pose.SetTrapped(false);
        if (trappedAnimator != null) trappedAnimator.speed = animatorSpeed;
        trappedAnimator = null;
    }

    // 예전 감옥 풍선 호출(아이템 목록에서는 뺐다) — 지금 맞은 것으로 같은 갇힘.
    public void Jail(float seconds) => Trap(Photon.Pun.PhotonNetwork.Time, seconds);

    public void Init(float characterHeight)
    {
        headHeight = characterHeight;
    }

    public void Stun(float seconds)
    {
        if (seconds <= 0f) return;
        if (!IsStunned && TryGetComponent(out IGameCharacter character)) GameAudio.PlayCharacter(SoundId.StunStars, character); // 별이 도는 순간(S4)
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
        EnsureStars();
        starRoot.gameObject.SetActive(!IsJailed); // 물방울 안에서는 별을 띄우지 않는다
    }

    private void Update()
    {
        if (bubble != null && !IsJailed) EndTrap();
        if (starRoot == null || !starRoot.gameObject.activeSelf) return;
        if (IsJailed) { starRoot.gameObject.SetActive(false); return; }
        if (!IsStunned)
        {
            starRoot.gameObject.SetActive(false);
            return;
        }
        starRoot.position = transform.position + Vector3.up * headHeight;
        starRoot.Rotate(0f, 240f * Time.deltaTime, 0f, Space.World);
    }

    // 별 이펙트는 미리 한 번 만들어 두고 켜고 끈다(최적화). 크기는 캐릭터 키에 맞춘다.
    private void EnsureStars()
    {
        if (starRoot != null) return;
        starRoot = new GameObject("StunStars").transform;
        starRoot.SetParent(transform, true);
        float radius = Mathf.Max(0.35f, headHeight * 0.18f);
        float size = Mathf.Max(0.12f, headHeight * 0.06f);
        for (int i = 0; i < StarCount; i++)
        {
            var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(star.GetComponent<Collider>());
            star.name = "Star";
            star.transform.SetParent(starRoot, false);
            star.transform.localPosition = Quaternion.Euler(0f, i * 360f / StarCount, 0f) * Vector3.forward * radius;
            star.transform.localScale = Vector3.one * size;
            EscapeVisuals.Tint(star, new Color(1f, 0.92f, 0.2f), 2f);
        }
        starRoot.gameObject.SetActive(false);
    }
}
