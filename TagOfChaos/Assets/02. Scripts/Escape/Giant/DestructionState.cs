// 부서지는 물체마다 맞은 횟수(Request1009Plan.md §66.3). Room Prop NetKeys.Destruction(byte[]) 하나에 4비트씩(0~15) 담는다.
// 800개면 400 B. 방장만 쓴다.
public static class DestructionState
{
    public const int MaxLevel = 15;

    public static int ByteLength(int count) => (count + 1) / 2;

    public static int Get(byte[] data, int id)
    {
        if (data == null || id < 0 || (id >> 1) >= data.Length) return 0;
        int b = data[id >> 1];
        return (id & 1) == 0 ? b & 0x0F : b >> 4;
    }

    public static void Set(byte[] data, int id, int level)
    {
        if (data == null || id < 0 || (id >> 1) >= data.Length) return;
        level = level < 0 ? 0 : level > MaxLevel ? MaxLevel : level;
        int i = id >> 1;
        data[i] = (id & 1) == 0
            ? (byte)((data[i] & 0xF0) | level)
            : (byte)((data[i] & 0x0F) | (level << 4));
    }

    // 길이가 다르면(맵이 다른 화면 등) 맞춰서 복사한다.
    public static byte[] Resize(byte[] data, int count)
    {
        var copy = new byte[ByteLength(count)];
        if (data != null) System.Array.Copy(data, copy, System.Math.Min(data.Length, copy.Length));
        return copy;
    }
}
