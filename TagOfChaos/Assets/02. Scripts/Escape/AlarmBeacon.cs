using UnityEngine;

// 사이렌 함정 발동 표시(Request1009Plan.md §25.5 K, §74 P12): 괴물 화면에 철창이 선 재료 건물 위로 빨간 빛기둥을 세운다(벽 너머로도 — 투시).
// 예전에는 5초 뒤 사라졌다 — 이제 철창이 서 있는 동안 계속 보이고, 철창이 부서지면 SirenTrapPresenter가 지운다(탈출구 빛기둥처럼).
public class AlarmBeacon : MonoBehaviour
{
    public static AlarmBeacon Create(Vector3 at)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(go.GetComponent<Collider>());
        go.name = "AlarmBeacon";
        go.transform.position = at + Vector3.up * 15f;
        go.transform.localScale = new Vector3(1.2f, 15f, 1.2f);
        EscapeVisuals.Tint(go, new Color(1f, 0.15f, 0.1f), 3f);
        var xray = go.AddComponent<XRayMarker>();
        xray.Init(new Color(1f, 0.2f, 0.15f));
        xray.SetVisible(true);
        GameAudio.PlayAt(SoundId.UiToastAlert, at);
        return go.AddComponent<AlarmBeacon>();
    }

    private void Update() => transform.Rotate(0f, 120f * Time.deltaTime, 0f);
}
