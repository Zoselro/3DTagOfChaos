using UnityEngine;
using UnityEngine.Rendering;

// 괴물 본인 클라이언트의 시점(GameFixPlan.md F6). 늘 GameSettings.MonsterDefaultView(1인칭)로 본다 — 전환 키(V)는 없앴다(§27).
// 1인칭에서는 자기 몸이 화면을 가리지 않도록 본인 화면에서만 몸 렌더러를 그림자만 남긴다(다른 사람 화면은 그대로).
// 대신 팔·촉수만 남긴 1인칭 전용 메시(MonsterFirstPersonLimbs)를 켜서 기존 팔과 촉수가 보이게 한다(§69 F4).
// §74 P15: 팔은 늘 보이고, 촉수는 잡기·돌진(준비 포함)·스킬(ShowAction) 동안만 보인다.
// MonsterController.Awake(IsMine)가 AddComponent로 붙인다.
public class MonsterViewSwitcher : MonoBehaviour
{
    private MonsterController monster;
    private Camera_Ctrl thirdPerson;
    private MonsterFirstPersonCamera firstPerson;
    private Renderer[] bodyRenderers;
    private ShadowCastingMode[] originalShadowModes;
    private MonsterViewMode current;
    private readonly System.Collections.Generic.List<SkinnedMeshRenderer> limbRenderers = new System.Collections.Generic.List<SkinnedMeshRenderer>(); // 팔
    private readonly System.Collections.Generic.List<SkinnedMeshRenderer> tentacleRenderers = new System.Collections.Generic.List<SkinnedMeshRenderer>();
    private const float DashTentacleSeconds = 1.2f; // 돌진이 끝난 뒤에도 촉수가 출렁이며 돌아오는 동안(MonsterTentacleFlourish) 보인다
    private float actionShownUntil = -10f;
    private bool tentaclesShown;

    // 본인 괴물의 시점(없으면 null) — 스킬·잡기 코드가 촉수를 잠깐 보이게 할 때 쓴다.
    public static MonsterViewSwitcher Local { get; private set; }

    // 잡기 시도·수색 폭탄·사이렌 함정처럼 촉수를 쓰는 동작이 있을 때 그 동안 1인칭 촉수를 보인다(§74 P15).
    public static void ShowActionLocal(float seconds = 0.8f)
    {
        if (Local != null) Local.actionShownUntil = Mathf.Max(Local.actionShownUntil, Time.time + seconds);
    }

    public bool IsFirstPerson => current == MonsterViewMode.FirstPerson && firstPerson != null && firstPerson.enabled;
    public Quaternion YawRotation => firstPerson != null ? firstPerson.YawRotation : transform.rotation;

    public void Init(MonsterController owner, Transform eyeSocket)
    {
        monster = owner;
        Local = this;
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
                SkinnedMeshRenderer arms = MonsterFirstPersonLimbs.Create(smr, MonsterFirstPersonLimbs.Part.Arms);
                if (arms != null) limbRenderers.Add(arms);
                SkinnedMeshRenderer tentacles = MonsterFirstPersonLimbs.Create(smr, MonsterFirstPersonLimbs.Part.Tentacles);
                if (tentacles != null) tentacleRenderers.Add(tentacles);
            }

        gameObject.AddComponent<MonsterTentacleFlourish>().Init(owner, this); // 1인칭 돌진 촉수 연출(본인 화면만)
        GrabAimReticle.Create(owner);
        Apply(GameSettings.Current.MonsterDefaultView);
    }

    private void OnDestroy()
    {
        if (Local == this) Local = null;
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
        tentaclesShown = !fp; // 다음 Update에서 다시 맞춘다
        UpdateTentacles();
    }

    private void Update() => UpdateTentacles();

    private void UpdateTentacles()
    {
        if (monster == null) return;
        if (monster.IsTentacleDashing) actionShownUntil = Mathf.Max(actionShownUntil, Time.time + DashTentacleSeconds);
        bool acting = monster.IsGrabKilling || monster.IsTentacleDashing || Time.time < actionShownUntil;
        bool show = IsFirstPerson && acting;
        if (show == tentaclesShown) return;
        tentaclesShown = show;
        foreach (SkinnedMeshRenderer t in tentacleRenderers)
            if (t != null) t.gameObject.SetActive(show);
    }
}
