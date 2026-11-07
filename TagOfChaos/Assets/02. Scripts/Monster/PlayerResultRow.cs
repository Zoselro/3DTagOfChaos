using TMPro;
using UnityEngine;

// 결과 화면 플레이어 목록 행(이름+상태) — GameRule.md §8.2, §10.2.
public class PlayerResultRow : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statusText;
    // 표시 문구는 프리팹 인스펙터에서 입력한다(코드에 한글 금지). "✕"는 기본 폰트에 글리프가 없어 □로 보였다(Bug-fix-plan.md §24.6).
    [SerializeField] private string monsterLabel = "(Monster)";
    [SerializeField] private string aliveLabel = "O Alive";
    [SerializeField] private string brokenLabel = "X Broken";

    [Header("Escape mode (EscapePlan.md §1.3)")]
    [SerializeField] private string cookieRoleLabel = "(Cookie)";
    [SerializeField] private string spyRoleLabel = "(Spy)";
    [SerializeField] private string monsterRoleLabel = "(Monster)";
    [SerializeField] private string escapedLabel = "Escaped";
    [SerializeField] private string failedLabel = "Failed to escape";
    [SerializeField] private string catchFormat = "Caught {0}";
    [SerializeField] private string witchKilledLabel = "Killed by the witch";
    [SerializeField] private string giantKilledLabel = "Stomped by the giant"; // 거대 괴물에게 밟힘(Request1009Plan.md §64)
    [SerializeField] private string statusSeparator = " / ";

    // 닉네임(쿠키) 탈출 성공 / 탈출 실패, 닉네임(스파이) 탈출 성공 / 탈출 실패
    public void SetEscaper(string nickname, bool isSpy, bool escaped, bool stomped = false)
    {
        if (nameText != null) nameText.text = nickname + (isSpy ? spyRoleLabel : cookieRoleLabel);
        if (statusText != null) statusText.text = escaped ? escapedLabel : stomped ? failedLabel + statusSeparator + giantKilledLabel : failedLabel;
    }

    // 닉네임(괴물) 잡은 횟수 n회 / 마녀에 의해 사망
    public void SetMonster(string nickname, int catches, bool killedByWitch, bool killedByGiant = false)
    {
        if (nameText != null) nameText.text = nickname + monsterRoleLabel;
        if (statusText == null) return;
        string caught = string.Format(catchFormat, catches);
        statusText.text = killedByWitch ? caught + statusSeparator + witchKilledLabel
            : killedByGiant ? caught + statusSeparator + giantKilledLabel : caught;
    }

#if UNITY_EDITOR
    public void EditorSetGiantLabel(string label) => giantKilledLabel = label;
#endif

    public void SetMonster(string nickname)
    {
        if (nameText != null) nameText.text = nickname;
        if (statusText != null) statusText.text = monsterLabel;
    }

    public void SetCookie(string nickname, bool alive)
    {
        if (nameText != null) nameText.text = nickname;
        if (statusText != null) statusText.text = alive ? aliveLabel : brokenLabel;
    }
}
