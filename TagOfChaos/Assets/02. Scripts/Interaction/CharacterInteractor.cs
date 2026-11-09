using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// 내가 조종하는 캐릭터(쿠키·괴물) 주변의 상호작용 사물을 찾아 안내 문구를 띄우고, 상호작용 키를 누르면 사물에
// 전달한다(GameLobbyScene.md §14). 쿠키·괴물 프리팹을 고치지 않도록 게임 시작 때 하나만 자동으로 만들어
// 씬이 바뀌어도 유지한다 — 조종 중인 캐릭터는 CharacterRegistry, 사물은 InteractableRegistry에서 찾는다.
// 쓸 수 있는 캐릭터: IGameCharacter.CanInteract가 true인 캐릭터(파괴·들림 중인 쿠키, 처형·돌진 중인 괴물 제외). 채팅 중에는
// PlayerInput.InteractPressed가 억제되고, 안내 문구는 IsTypingInUi로 숨긴다.
// 길게 누르는 사물(IHoldInteractable — 상자·장치, Request1009Plan.md §1)은 정한 시간 동안 E를 누르고 있어야 Interact를 부르고,
// 그동안 아이콘 둘레에 진행 링을 채운다. 떼거나 · 다른 사물로 바뀌거나 · 쓸 수 없게 되면 취소된다(HoldProgress).
public class CharacterInteractor : MonoBehaviour
{
    private const float CheckInterval = 0.1f;
    private const float MaxHeightDifference = 2.5f; // 층이 다른 사물(예: 지붕 위)은 무시

    private static CharacterInteractor instance;

    private InteractionPromptUI prompt;
    private IGameCharacter localCharacter;
    private IInteractable focused;
    private float nextCheckTime;
    private readonly HoldProgress hold = new HoldProgress();

    // 지금 E 아이콘이 떠 있는 사물이 있는지 — 없을 때만 E가 손에 든 헬리콥터 모자를 쓴다(Request1009Plan.md §25.5 H).
    public static bool HasFocus => instance != null && instance.focused != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject(nameof(CharacterInteractor));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<CharacterInteractor>();
    }

    private void Awake()
    {
        prompt = InteractionPromptUI.Create(transform);
    }

    private void Update()
    {
        if (Time.time >= nextCheckTime)
        {
            nextCheckTime = Time.time + CheckInterval;
            Refresh();
        }

        // 술래 잡기는 따로 잡기 키(기본 마우스 왼쪽, §69 F3)로 한다 — MonsterGrabKillTrigger가 받는다. E는 문 등 상호작용만.

        if (hold.Active)
        {
            UpdateHold();
            return;
        }

        if (focused == null || !PlayerInput.InteractPressed) return;

        // 검사 주기(0.1초) 사이에 상태가 바뀌었을 수 있으므로 누른 순간 다시 확인한다.
        Refresh();
        if (focused == null) return;
        float seconds = focused is IHoldInteractable holdable ? holdable.HoldSecondsFor(localCharacter) : 0f;
        if (seconds > 0f)
        {
            hold.Begin(focused, seconds, Time.time);
            prompt?.SetProgress(0f);
            (focused as IHoldStartListener)?.OnHoldStarted(localCharacter);
            return;
        }
        focused.Interact(localCharacter);
    }

    // 누르고 있는 동안: 초점 사물은 0.1초마다 갱신되므로 같은 사물을 계속 보고 있는지·쓸 수 있는지 매 프레임 확인한다.
    private void UpdateHold()
    {
        var target = hold.Target as IInteractable;
        bool usable = localCharacter != null && localCharacter.CanInteract && InteractableRegistry.IsAlive(target) && target.CanInteract(localCharacter);
        HoldProgress.Result result = hold.Tick(focused, PlayerInput.InteractHeld, usable, Time.time);
        float shown = target is IHoldProgressDisplay display ? display.DisplayProgress(hold.Progress) : hold.Progress;
        prompt?.SetProgress(result == HoldProgress.Result.Holding ? shown : hold.Progress);
        if (result == HoldProgress.Result.Canceled) (target as IHoldEndListener)?.OnHoldEnded(localCharacter, false);
        if (result != HoldProgress.Result.Completed) return;
        Refresh(); // 완료 순간 상태를 다시 확인(사물이 그 사이 사라졌을 수 있다)
        if (focused == target) target.Interact(localCharacter);
        else (target as IHoldEndListener)?.OnHoldEnded(localCharacter, false);
    }

    private void Refresh()
    {
        localCharacter = FindLocalCharacter();
        // 연출 중(마녀 등장 등 — 조작이 멈춘 동안)에는 안내 아이콘도 띄우지 않는다(Request1009Plan.md §23)
        focused = localCharacter != null && localCharacter.CanInteract && !IsTypingInUi() && !PlayerInput.CutsceneActive
            ? FindNearest(localCharacter)
            : null;

        if (prompt == null) return;
        if (focused != null)
            prompt.Show(KeyBindings.Get(KeyAction.Interact), focused.InteractionPoint, (focused as IInteractionLabel)?.GetLabel(localCharacter));
        else prompt.Hide();
        if (!hold.Active) prompt.SetProgress(0f);
    }

    private static IGameCharacter FindLocalCharacter()
    {
        foreach (IGameCharacter c in CharacterRegistry.All)
            if (CharacterRegistry.IsAlive(c) && c.View != null && c.View.IsMine) return c;
        return null;
    }

    // 수평 거리가 가장 가까운, 범위 안의 사용 가능한 사물.
    private static IInteractable FindNearest(IGameCharacter character)
    {
        Vector3 position = character.gameObject.transform.position;
        IInteractable best = null;
        float bestSqr = float.MaxValue;

        foreach (IInteractable candidate in InteractableRegistry.All)
        {
            if (!InteractableRegistry.IsAlive(candidate)) continue;

            Vector3 delta = candidate.InteractionPoint - position;
            if (Mathf.Abs(delta.y) > MaxHeightDifference) continue;
            delta.y = 0f;
            float sqr = delta.sqrMagnitude;
            float range = candidate.InteractionRange;
            if (sqr > range * range || sqr >= bestSqr) continue;
            if (!candidate.CanInteract(character)) continue;

            best = candidate;
            bestSqr = sqr;
        }
        return best;
    }

    // 채팅 입력 중에는 안내 문구를 띄우지 않는다(키 입력 자체는 PlayerInput.IsGameplaySuppressed가 막는다).
    private static bool IsTypingInUi()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        return selected != null && selected.TryGetComponent(out TMP_InputField field) && field.isFocused;
    }
}
