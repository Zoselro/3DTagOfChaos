using UnityEngine;
using UnityEngine.Rendering;

// 괴물 본인 클라이언트의 시점(GameFixPlan.md F6). 늘 GameSettings.MonsterDefaultView(1인칭)로 본다 — 전환 키(V)는 없앴다(§27).
// 1인칭에서는 자기 몸이 화면을 가리지 않도록 본인 화면에서만 몸 렌더러를 그림자만 남긴다(다른 사람 화면은 그대로).
// 대신 팔·촉수만 남긴 1인칭 전용 메시(MonsterFirstPersonLimbs)를 켜서 기존 팔과 촉수가 보이게 한다(§69 F4).
// MonsterController.Awake(IsMine)가 AddComponent로 붙인다.
public class MonsterViewSwitcher : MonoBehaviour
{
    private MonsterController monster;
    private Camera_Ctrl thirdPerson;
    private MonsterFirstPersonCamera firstPerson;
    private Renderer[] bodyRenderers;
    private ShadowCastingMode[] originalShadowModes;
    private MonsterViewMode current;
    private readonly System.Collections.Generic.List<SkinnedMeshRenderer> limbRenderers = new System.Collections.Generic.List<SkinnedMeshRenderer>();

    public bool IsFirstPerson => current == MonsterViewMode.FirstPerson && firstPerson != null && firstPerson.enabled;
    public Quaternion YawRotation => firstPerson != null ? firstPerson.YawRotation : transform.rotation;

    public void Init(MonsterController owner, Transform eyeSocket)
    {
        monster = owner;
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[MonsterViewSwitcher] Main Camera not found.");
            enabled = false;
            return;
        }

        thirdPerson = cam.GetComponent<Camera_Ctrl>();
        firstPerson = cam.GetComponent<MonsterFirstPersonCamera>();
        if (firstPerson == null) firstPerson = cam.gameObject.AddComponent<MonsterFirstPersonCamera>();
        firstPerson.Attach(owner.transform, eyeSocket);

        bodyRenderers = owner.GetComponentsInChildren<Renderer>(true);
        originalShadowModes = new ShadowCastingMode[bodyRenderers.Length];
        for (int i = 0; i < bodyRenderers.Length; i++) originalShadowModes[i] = bodyRenderers[i].shadowCastingMode;
        foreach (Renderer r in bodyRenderers)
            if (r is SkinnedMeshRenderer smr)
            {
                SkinnedMeshRenderer limbs = MonsterFirstPersonLimbs.Create(smr);
                if (limbs != null) limbRenderers.Add(limbs);
            }

        gameObject.AddComponent<MonsterTentacleFlourish>().Init(owner, this); // 1인칭 돌진 촉수 연출(본인 화면만)
        GrabAimReticle.Create(owner);
        Apply(GameSettings.Current.MonsterDefaultView);
    }

    private void OnDestroy()
    {
        // 괴물이 사라지면(판 종료·퇴장) 카메라를 3인칭 기본 상태로 되돌려 다음 대상(관전 등)이 그대로 쓸 수 있게 한다.
        if (firstPerson != null) firstPerson.enabled = false;
        if (thirdPerson != null) thirdPerson.enabled = true;
    }

    private void Apply(MonsterViewMode view)
    {
        current = view;
        bool fp = view == MonsterViewMode.FirstPerson;

        if (fp) firstPerson.SyncYaw(thirdPerson != null ? thirdPerson.transform.eulerAngles.y : monster.transform.eulerAngles.y);
        if (thirdPerson != null)
        {
            thirdPerson.enabled = !fp;
            if (!fp) thirdPerson.SetFollowTarget(monster.gameObject, monster.CameraTargetHeight, monster.CameraDistance, keepRotation: false);
        }
        firstPerson.enabled = fp;

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] == null) continue;
            bodyRenderers[i].shadowCastingMode = fp ? ShadowCastingMode.ShadowsOnly : originalShadowModes[i];
        }
        foreach (SkinnedMeshRenderer limbs in limbRenderers)
            if (limbs != null) limbs.gameObject.SetActive(fp);
    }
}
