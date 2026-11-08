using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 사이렌 함정 철창(Request1009Plan.md §68 GP5) — 모든 화면. EscapeManager.Start가 붙인다.
// 갇힌(Caged) 함정마다 건물 둘레에 빼빼로 막대 울타리(겉모습 한 메시 + 막는 충돌 상자 고리)를 세우고 사이렌을 울린다.
// 내 캐릭터가 철창 안이면 게이지와 "점프 키 연타" 안내를 보이고, 누를 때마다 방장에게 CageMash를 보낸다.
public class SirenTrapPresenter : MonoBehaviour
{
    private const float SirenInterval = 1.4f;
    private const int ColliderSegments = 48;

    private EscapeManager manager;
    private readonly Dictionary<int, GameObject> cages = new Dictionary<int, GameObject>();
    private readonly List<int> stale = new List<int>();
    private float nextSiren, lastMash;
    private Material biscuit, chocolate;
    private GameObject hud;
    private Image fill;
    private TMP_Text label;

    public void Init(EscapeManager owner)
    {
        manager = owner;
        manager.StateChanged += Sync;
    }

    private void OnDestroy()
    {
        if (manager != null) manager.StateChanged -= Sync;
        if (biscuit != null) Destroy(biscuit);
        if (chocolate != null) Destroy(chocolate);
    }

    private void Sync()
    {
        EscapeState s = manager.State;
        stale.Clear();
        foreach (var kv in cages)
        {
            int i = s != null ? s.BombIndex(kv.Key) : -1;
            if (i < 0 || !s.Bombs[i].Caged) stale.Add(kv.Key);
        }
        foreach (int id in stale)
        {
            if (cages[id] != null) BreakCage(cages[id]);
            cages.Remove(id);
        }
        if (s == null) return;
        foreach (EscapeState.Bomb b in s.Bombs)
        {
            if (!b.Caged || cages.ContainsKey(b.Id)) continue;
            if (!SirenCage.Ring(manager.BuildingTransform(b.Building), out Vector3 c, out float r)) continue;
            cages[b.Id] = BuildCage(c, r);
            GameAudio.PlayAt(SoundId.UiToastAlert, c); // 걸린 순간
        }
    }

    private void Update()
    {
        EscapeState s = manager != null ? manager.State : null;
        bool trapped = false;
        int trapId = -1, mash = 0;
        if (s != null && cages.Count > 0)
        {
            if (Time.time >= nextSiren)
            {
                nextSiren = Time.time + SirenInterval;
                foreach (EscapeState.Bomb b in s.Bombs)
                    if (b.Caged) GameAudio.PlayAt(SoundId.VikingBell, manager.BuildingRepairPoint(b.Building)); // 사이렌(임시 — 종소리, 전용 소리는 GP7)
            }
            IGameCharacter me = LocalCookie();
            if (me != null)
                foreach (EscapeState.Bomb b in s.Bombs)
                {
                    if (!b.Caged || !SirenCage.Ring(manager.BuildingTransform(b.Building), out Vector3 c, out float r)) continue;
                    if (!SirenCage.Inside(me.gameObject.transform.position, c, r)) continue;
                    trapped = true; trapId = b.Id; mash = b.Mash;
                    break;
                }
        }
        ShowHud(trapped, mash);
        if (!trapped || !PlayerInput.JumpPressed || Time.time - lastMash < SirenCage.MashMinInterval) return;
        lastMash = Time.time;
        manager.Request(EscapeOp.CageMash, trapId, 0, Vector3.zero);
    }

    private static IGameCharacter LocalCookie()
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c.Role != CharacterRole.Monster && c.View != null && c.View.IsMine && CharacterRegistry.IsAlive(c)) return c;
        return null;
    }

    // ---------------- 철창 ----------------

    private GameObject BuildCage(Vector3 center, float radius)
    {
        var root = new GameObject("SirenCage");
        root.transform.position = center;
        if (biscuit == null) { biscuit = EscapeVisuals.NewMaterial(new Color(0.98f, 0.82f, 0.55f), 0.15f, fade: false); biscuit.SetFloat("_Glossiness", 0.2f); }
        if (chocolate == null) { chocolate = EscapeVisuals.NewMaterial(new Color(0.5f, 0.27f, 0.13f), 0.1f, fade: false); chocolate.SetFloat("_Glossiness", 0.75f); }

        // 막대: 아래 25 %는 과자, 위 75 %는 초콜릿(빼빼로). 모두 한 메시(과자·초콜릿 두 재질)로 합쳐 그리기 횟수를 줄인다.
        Mesh cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        int n = SirenCage.BarCount(radius);
        var stick = new CombineInstance[n];
        var coat = new CombineInstance[n];
        float d = SirenCage.BarRadius * 2f, h = SirenCage.Height;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            Vector3 p = new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
            float lean = Mathf.Sin(i * 1.7f) * 2f; // 살짝씩 다르게 기울여 손으로 꽂은 느낌
            Quaternion q = Quaternion.Euler(lean, 0f, Mathf.Cos(i * 1.3f) * 2f);
            stick[i] = new CombineInstance { mesh = cylinder, transform = Matrix4x4.TRS(p + q * Vector3.up * (h * 0.125f), q, new Vector3(d * 0.8f, h * 0.125f, d * 0.8f)) };
            coat[i] = new CombineInstance { mesh = cylinder, transform = Matrix4x4.TRS(p + q * Vector3.up * (h * 0.25f + h * 0.375f), q, new Vector3(d * 1.15f, h * 0.375f, d * 1.15f)) };
        }
        var sticks = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        sticks.CombineMeshes(stick, true, true);
        var coats = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        coats.CombineMeshes(coat, true, true);
        var mesh = new Mesh { name = "SirenCageBars", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.CombineMeshes(new[] { new CombineInstance { mesh = sticks }, new CombineInstance { mesh = coats } }, false, false);
        Destroy(sticks); Destroy(coats);
        var bars = new GameObject("Bars");
        bars.transform.SetParent(root.transform, false);
        bars.AddComponent<MeshFilter>().sharedMesh = mesh;
        bars.AddComponent<MeshRenderer>().sharedMaterials = new[] { biscuit, chocolate };

        // 막는 고리: 상자 ColliderSegments개를 둘레에 이어 붙인다(막대 사이로 빠져나가지 못하게)
        float seg = 2f * Mathf.PI * radius / ColliderSegments * 1.08f;
        for (int i = 0; i < ColliderSegments; i++)
        {
            float a = (i + 0.5f) * Mathf.PI * 2f / ColliderSegments;
            var col = new GameObject("Wall" + i);
            col.transform.SetParent(root.transform, false);
            col.transform.localPosition = new Vector3(Mathf.Sin(a) * radius, h * 0.5f, Mathf.Cos(a) * radius);
            col.transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
            var box = col.AddComponent<BoxCollider>();
            box.size = new Vector3(seg, h, 0.5f);
        }
        StartCoroutine(Rise(root.transform));
        return root;
    }

    // 땅에서 솟아오름(0.35초)
    private static System.Collections.IEnumerator Rise(Transform t)
    {
        Vector3 end = t.position;
        for (float e = 0f; e < 0.35f && t != null; e += Time.deltaTime)
        {
            t.position = end - Vector3.up * SirenCage.Height * (1f - e / 0.35f);
            yield return null;
        }
        if (t != null) t.position = end;
    }

    private void BreakCage(GameObject cage)
    {
        GameAudio.PlayAt(SoundId.CakeCrack, cage.transform.position); // 철창이 부서짐
        Transform bars = cage.transform.Find("Bars");
        Mesh m = bars != null ? bars.GetComponent<MeshFilter>().sharedMesh : null;
        foreach (Collider c in cage.GetComponentsInChildren<Collider>()) c.enabled = false;
        StartCoroutine(Sink(cage, m));
    }

    private static System.Collections.IEnumerator Sink(GameObject cage, Mesh mesh)
    {
        Vector3 start = cage.transform.position;
        for (float e = 0f; e < 0.6f && cage != null; e += Time.deltaTime)
        {
            cage.transform.position = start - Vector3.up * SirenCage.Height * (e / 0.6f);
            yield return null;
        }
        if (mesh != null) Destroy(mesh);
        if (cage != null) Destroy(cage);
    }

    // ---------------- 게이지 ----------------

    private void ShowHud(bool on, int mash)
    {
        if (!on) { if (hud != null && hud.activeSelf) hud.SetActive(false); return; }
        if (hud == null) BuildHud();
        if (!hud.activeSelf) hud.SetActive(true);
        fill.fillAmount = SirenCage.Progress(mash);
        label.text = string.Format(EscapeTextsSO.Current.cageMash, KeyBindings.DisplayName(KeyBindings.Get(KeyAction.Jump)));
    }

    private void BuildHud()
    {
        hud = new GameObject("SirenCageHud", typeof(Canvas), typeof(CanvasScaler));
        var canvas = hud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = hud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        RectTransform panel = EscapeHud.NewRect("Panel", hud.transform, new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f), Vector2.zero, new Vector2(520f, 90f));
        Image bg = panel.gameObject.AddComponent<Image>();
        bg.sprite = EscapeHud.WhiteSprite(); bg.color = new Color(0.12f, 0.06f, 0.04f, 0.85f); bg.raycastTarget = false;
        fill = EscapeHud.NewImage("Fill", panel, new Vector2(0.5f, 0.25f), Vector2.zero, new Vector2(480f, 18f));
        fill.color = new Color(1f, 0.55f, 0.75f);
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
        label = EscapeHud.NewText("Label", panel, 30f, TextAlignmentOptions.Center);
        label.rectTransform.anchorMin = new Vector2(0f, 0.45f); label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
    }
}
