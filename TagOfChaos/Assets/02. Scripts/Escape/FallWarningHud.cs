using Photon.Pun;
using TMPro;
using UnityEngine;

// §90 W2·W3 Heaven Of Bakery 낙하 경고(쿠키·스파이 본인 화면, 이 맵만). EscapeHud가 붙인다.
// W2: 1층 바닥에서 12 m보다 높고 가장자리 3 m 안(아래로 12 m 넘게 비어 있음)이면 화면 위쪽에 노란 경고(0.3초 서서히), 낮아지면 바로 사라짐.
//     패러글라이더를 들고 있으면 "패러글라이더를 펴면 안전합니다"로 바뀐다.
// W3: 준비 시간 중 한 번 안내.
public class FallWarningHud : MonoBehaviour
{
    private const float CheckSeconds = 0.2f;
    private const float FadeSeconds = 0.3f;
    private static readonly Vector3[] Probes = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
        new Vector3(0.7f, 0f, 0.7f), new Vector3(-0.7f, 0f, 0.7f), new Vector3(0.7f, 0f, -0.7f), new Vector3(-0.7f, 0f, -0.7f) };

    private EscapeManager manager;
    private TMP_Text label;
    private CanvasGroup group;
    private float nextCheck, alpha;
    private bool want, introShown;

    public void Init(EscapeManager owner, Transform canvas)
    {
        manager = owner;
        RectTransform rt = EscapeHud.NewRect("FallWarning", canvas, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(900f, 60f));
        group = rt.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f; group.blocksRaycasts = false;
        label = EscapeHud.NewText("Text", rt, 34f, TextAlignmentOptions.Center);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero; label.rectTransform.offsetMax = Vector2.zero;
        label.color = new Color(1f, 0.86f, 0.2f);
    }

    private bool MapOn => manager != null && manager.Recipe != null && SkyFallRules.MapHasSkyFall(manager.Recipe.MapName);

    private void Update()
    {
        if (!MapOn || label == null) return;
        HideOrSeekPlayer me = LocalCookie();
        if (!introShown && me != null && GamePhaseState.Current == GamePhase.Prepare)
        {
            introShown = true;
            EscapeHud.Toast(EscapeTextsSO.Current.fallIntro, alert: true);   // W3
        }
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + CheckSeconds;
            want = me != null && SkyFallRules.ShowWarning(true, me.transform.position.y, NearDropEdge(me.transform.position), me.IsGliding);
            if (want) label.text = HoldsGlider(me) ? EscapeTextsSO.Current.fallWarningGlider : EscapeTextsSO.Current.fallWarning;
        }
        bool high = me != null && me.transform.position.y - SkyFallRules.FloorY > SkyFallRules.LethalDrop;
        if (!high) { alpha = 0f; want = false; }   // 12 m 아래로 내려오면 바로 멈춘다(사용자 고침)
        alpha = Mathf.MoveTowards(alpha, want ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);
        group.alpha = alpha;
    }

    // 둘레 3 m(8방향) 중 아래로 12 m 넘게 빈 곳이 있으면 가장자리.
    public static bool NearDropEdge(Vector3 p)
    {
        foreach (Vector3 d in Probes)
        {
            Vector3 from = p + d.normalized * SkyFallRules.EdgeWarnDistance + Vector3.up * 1f;
            if (!Physics.Raycast(from, Vector3.down, SkyFallRules.LethalDrop + 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return true;
        }
        return false;
    }

    private bool HoldsGlider(HideOrSeekPlayer me)
    {
        EscapeState s = manager.State;
        if (s == null || me.View == null || me.View.Owner == null) return false;
        int actor = me.View.Owner.ActorNumber;
        foreach (EscapeState.Item it in s.Items)
        {
            if (it.Loc != ItemLocation.Held || it.A != actor) continue;
            ItemSO def = manager.Catalog.Find(it.Id);
            if (def != null && def.IsTool && def.Tool.Kind == ToolKind.Paraglider) return true;
        }
        return false;
    }

    private static HideOrSeekPlayer LocalCookie()
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (c is HideOrSeekPlayer h && h.IsMine && CharacterRegistry.IsAlive(c)) return h;
        return null;
    }
}
