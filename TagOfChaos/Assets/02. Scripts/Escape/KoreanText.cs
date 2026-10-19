// 코드에 한글을 쓰지 않고 받침 여부를 판단한다(유니코드 한글 음절 계산, EscapePlan.md §5.10).
// "스파이가 기어를 훔쳐…"처럼 조사를 고를 때 쓴다. 문구 틀(을/를)은 에셋(EscapeTextsSO)에 두 가지로 입력한다.
public static class KoreanText
{
    private const int SyllableStart = 0xAC00;
    private const int SyllableEnd = 0xD7A3;
    private const int FinalCount = 28;

    // 마지막 글자가 한글 음절인지(아니면 "이(가)"처럼 두 조사를 함께 쓴다).
    public static bool EndsWithHangul(string word) =>
        !string.IsNullOrEmpty(word) && word[word.Length - 1] >= SyllableStart && word[word.Length - 1] <= SyllableEnd;

    public static bool HasFinalConsonant(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;
        char last = word[word.Length - 1];
        if (last < SyllableStart || last > SyllableEnd) return false; // 한글이 아니면 받침 없음으로 본다
        return (last - SyllableStart) % FinalCount != 0;
    }
}
