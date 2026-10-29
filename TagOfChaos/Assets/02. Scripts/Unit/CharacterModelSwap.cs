using UnityEngine;

// 캐릭터 겉모습 바꾸기(Request1009Plan.md §45.3 CH6). 쿠키 프리팹(루트 Animator + Armature + Mesh_0)에 다른 캐릭터 모델을 입힌다.
// - 모델 프리팹(CHR_<이름>, Humanoid)을 루트 아래 "CharacterModel"로 붙이고, 루트 Animator의 아바타를 그 모델 것으로 바꾼 뒤 Rebind한다.
//   컨트롤러(PlayerAnimator)는 그대로 — Humanoid 리타깃이라 쿠키 클립을 모든 캐릭터가 같이 쓴다.
// - 쿠키 뼈는 이름 끝에 HiddenSuffix를 붙이고 끈다(같은 이름 뼈가 두 벌이면 Humanoid 연결이 꼬인다). 쿠키 몸(Mesh_0) 렌더러는 끈다 —
//   Mesh_0의 콜라이더(괴물 조준·시야 검사용)는 그대로 둔다.
// - 모델이 없는 칸(0번 쿠키)이면 되돌린다. Rebind 뒤 Animator는 기본 상태(Idle, 팔 내림)부터 돈다 — T-Pose가 보이지 않는다(§45.2 자세 규칙).
// 플레이어(PlayerSkinApplier)·AI 본보기(DecoyPresenter)가 같은 코드를 쓴다. 마녀 대역은 살아 있는 캐릭터를 복제하므로 따라온다.
public static class CharacterModelSwap
{
    public const string ModelName = "CharacterModel";
    public const string CookieArmatureName = "Armature";
    public const string CookieBodyName = "Mesh_0";
    private const string HiddenSuffix = "__cookie";

    // 바꿨으면 true(같은 모델이면 아무것도 안 한다).
    public static bool Apply(Transform root, GameObject modelPrefab)
    {
        if (root == null) return false;
        Animator animator = root.GetComponent<Animator>();
        Transform current = root.Find(ModelName);
        string wanted = modelPrefab != null ? modelPrefab.name : null;
        string shown = current != null && current.childCount > 0 ? current.GetChild(0).name : null;
        if (wanted == shown) return false;

        if (current != null)
        {
            current.SetParent(null); // 같은 프레임의 Find·Rebind가 옛 모델을 보지 않게
            if (Application.isPlaying) Object.Destroy(current.gameObject);
            else Object.DestroyImmediate(current.gameObject); // 에디터 시험(Request1029Tests)
        }

        if (modelPrefab == null)
        {
            SetCookieVisible(root, true);
            if (animator != null && CookieAvatar != null) { animator.avatar = CookieAvatar; animator.Rebind(); }
            return true;
        }

        Animator modelAnimator = modelPrefab.GetComponentInChildren<Animator>(true);
        Avatar avatar = modelAnimator != null ? modelAnimator.avatar : null;
        if (avatar == null || !avatar.isHuman)
        {
            Debug.LogWarning($"[CharacterModelSwap] {modelPrefab.name} has no humanoid avatar — keeping the cookie body.");
            return false;
        }
        if (animator != null && animator.avatar != null && animator.avatar.name.StartsWith("Cookie")) CookieAvatar = animator.avatar;

        SetCookieVisible(root, false);
        var holder = new GameObject(ModelName).transform;
        holder.SetParent(root, false);
        GameObject model = Object.Instantiate(modelPrefab, holder, false);
        model.name = modelPrefab.name;
        foreach (Animator a in model.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a); // 루트 Animator 하나만 뼈를 움직인다
        model.layer = root.gameObject.layer;
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.gameObject.layer;
        if (animator != null)
        {
            animator.avatar = avatar;
            animator.Rebind();
        }
        return true;
    }

    // 쿠키 기본 아바타(되돌릴 때). 처음 바꿀 때 기억한다.
    public static Avatar CookieAvatar { get; private set; }

    public static bool HasModel(Transform root) => root != null && root.Find(ModelName) != null;

    // 지금 보이는 몸 렌더러(새 모델이면 그 모델, 아니면 쿠키 Mesh_0).
    public static Renderer[] BodyRenderers(Transform root)
    {
        Transform model = root != null ? root.Find(ModelName) : null;
        if (model != null) return model.GetComponentsInChildren<Renderer>(true);
        Transform body = root != null ? root.Find(CookieBodyName) : null;
        return body != null ? body.GetComponents<Renderer>() : new Renderer[0];
    }

    private static void SetCookieVisible(Transform root, bool visible)
    {
        Transform body = root.Find(CookieBodyName);
        if (body != null)
            foreach (Renderer r in body.GetComponents<Renderer>()) r.enabled = visible;
        Transform armature = root.Find(CookieArmatureName) ?? root.Find(CookieArmatureName + HiddenSuffix);
        if (armature == null) return;
        foreach (Transform t in armature.GetComponentsInChildren<Transform>(true))
        {
            bool hidden = t.name.EndsWith(HiddenSuffix);
            if (visible && hidden) t.name = t.name.Substring(0, t.name.Length - HiddenSuffix.Length);
            else if (!visible && !hidden) t.name += HiddenSuffix;
        }
        armature.gameObject.SetActive(visible);
    }
}
