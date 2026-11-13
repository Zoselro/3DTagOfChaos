using UnityEngine;

// 괴물 소음폭탄 하나의 모습(Request1009Plan.md §25.5 K) — 괴물에게만 보이고, 놓은 괴물은 E로 치운다(치워도 20초 쿨타임).
public class NoiseBombView : MonoBehaviour, IInteractable, IInteractionLabel
{
    private EscapeManager manager;
    private EscapeState.Bomb bomb;
    private Renderer blink;
    private const float PlacedModelScale = 3f; // 모델 높이 0.15 m → 0.45 m(예전 도형 0.5 m와 비슷하게)

    public Vector3 InteractionPoint => transform.position;
    public float InteractionRange => 2.2f;

    public static NoiseBombView Create(EscapeManager owner, EscapeState.Bomb data, Transform parent)
    {
        var go = new GameObject("NoiseBomb_" + data.Id);
        go.transform.SetParent(parent, false);
        go.transform.position = data.Pos;
        // 놓인 함정도 손에 드는 것과 같은 Blender 모델(ITEM_NoiseBomb)로(§74 P14). 모델이 없으면 기본 도형.
        ItemSO def = EscapeCatalogSO.Current != null ? EscapeCatalogSO.Current.NoiseBomb : null;
        if (def != null && def.ModelPrefab != null)
        {
            GameObject model = EscapeVisuals.CreateItemModel(def, go.transform);
            model.transform.localScale *= PlacedModelScale;
        }
        else
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = Vector3.up * 0.25f;
            body.transform.localScale = Vector3.one * 0.5f;
            EscapeVisuals.Tint(body, new Color(0.15f, 0.12f, 0.15f), 0f);
        }
        var light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(light.GetComponent<Collider>());
        light.transform.SetParent(go.transform, false);
        light.transform.localPosition = Vector3.up * (def != null && def.ModelPrefab != null ? 0.5f : 0.55f); // 모델 꼭대기(0.47 m)에 붙게
        light.transform.localScale = Vector3.one * 0.14f;
        EscapeVisuals.Tint(light, new Color(1f, 0.1f, 0.1f), 2.5f);
        var view = go.AddComponent<NoiseBombView>();
        view.manager = owner;
        view.bomb = data;
        view.blink = light.GetComponent<Renderer>();
        InteractableRegistry.Register(view);
        return view;
    }

    private void Update()
    {
        if (blink != null) blink.enabled = Mathf.Repeat(Time.time, 1f) < 0.5f;
    }

    private void OnDestroy() => InteractableRegistry.Unregister(this);

    public bool CanInteract(IGameCharacter character) =>
        character.Role == CharacterRole.Monster && character.View != null && character.View.Owner != null && character.View.Owner.ActorNumber == bomb.Owner;
    public void Interact(IGameCharacter character) => manager.Request(EscapeOp.RemoveBomb, bomb.Id);
    public string GetLabel(IGameCharacter viewer) => EscapeTextsSO.Current.removeBomb;
}
