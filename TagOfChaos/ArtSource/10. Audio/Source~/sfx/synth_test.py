# S1 시험음(표준 라이브러리만). S8에서 같은 파일 이름의 정식 음원으로 교체한다.
import math, random, struct, wave, os
SR = 44100
def write(path, samples, channels=1):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    peak = max(1e-9, max(abs(s) for s in samples))
    scale = 0.7 / peak  # 피크 약 -3 dBFS
    with wave.open(path, "wb") as w:
        w.setnchannels(channels); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s * scale)) * 32767)) for s in samples))
def env(t, a, d): return min(1.0, t / a) * math.exp(-t / d) if a > 0 else math.exp(-t / d)
def tone(freq, dur, a=0.002, d=0.05, harm=(1.0,)):
    n = int(SR * dur); out = []
    for i in range(n):
        t = i / SR
        out.append(env(t, a, d) * sum(h * math.sin(2 * math.pi * freq * (k + 1) * t) for k, h in enumerate(harm)))
    return out
def mix(*parts):
    n = max(len(p) for _, p in parts); out = [0.0] * n
    for off, p in parts:
        o = int(off * SR)
        for i, s in enumerate(p):
            if o + i < n: out[o + i] += s
            else: out.append(s)
    return out
def note(m): return 440.0 * 2 ** ((m - 69) / 12)
root = "out/"
# UiClick: 짧은 나무 톡
write(root + "SFX/UI/UiClick.wav", tone(1800, 0.06, d=0.012, harm=(1, 0.3, 0.1)))
# ChestOpen: 뚜껑 퐁 + 반짝 3음
write(root + "SFX/Escape/ChestOpen.wav", mix((0, tone(220, 0.25, d=0.06, harm=(1, 0.5))), (0.08, tone(note(84), 0.3, d=0.08)), (0.14, tone(note(88), 0.3, d=0.08)), (0.2, tone(note(91), 0.4, d=0.12))))
# CookieJump: 위로 올라가는 뿅
n = int(SR * 0.18); jump = []
ph = 0.0
for i in range(n):
    t = i / SR; f = 400 + 900 * (t / 0.18); ph += 2 * math.pi * f / SR
    jump.append(env(t, 0.003, 0.06) * math.sin(ph))
write(root + "SFX/Character/CookieJump.wav", jump)
# Lobby: 100 BPM 4마디 오르골 아르페지오(정확히 4마디 = 9.6초, 이음매 없는 반복)
beat = 60 / 100; bars = 4; total = bars * 4 * beat
chords = [[60, 64, 67, 72], [57, 60, 64, 69], [53, 57, 60, 65], [55, 59, 62, 67]]
parts = []
for b, ch in enumerate(chords):
    for k in range(8):
        m = ch[k % 4] + (12 if k >= 4 else 0)
        parts.append((b * 4 * beat + k * beat / 2, tone(note(m), 0.9, d=0.35, harm=(1, 0.25, 0.08))))
    parts.append((b * 4 * beat, tone(note(ch[0] - 24), 2.2, a=0.01, d=0.9, harm=(1, 0.4))))
loop = mix(*parts)
L = int(total * SR)
tail = loop[L:]; loop = loop[:L]
for i, s in enumerate(tail): loop[i % L] += s  # 꼬리를 앞으로 접어 이음매를 없앤다
write(root + "BGM/Lobby.wav", loop)
# StMonsterReveal: 낮은 금관 느낌 2초
write(root + "BGM/StMonsterReveal.wav", mix((0, tone(note(43), 2.0, a=0.05, d=0.8, harm=(1, 0.7, 0.5, 0.3, 0.2))), (0, tone(note(50), 2.0, a=0.05, d=0.8, harm=(1, 0.6, 0.4)))))
print("ok")
