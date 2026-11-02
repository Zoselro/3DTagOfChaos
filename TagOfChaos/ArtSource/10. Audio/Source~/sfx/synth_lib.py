# 표준 라이브러리만 쓰는 간단한 합성 도구(SoundPlan S8 전 시험음용).
import math, random, struct, wave, os
SR = 44100
def write(path, samples, peak=0.7):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    m = max(1e-9, max(abs(s) for s in samples)); scale = peak / m
    fade = int(SR * 0.004)  # 끝 클릭 방지
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        data = bytearray()
        n = len(samples)
        for i, s in enumerate(samples):
            g = min(1.0, (n - 1 - i) / fade) if i > n - fade else 1.0
            data += struct.pack("<h", int(max(-1, min(1, s * scale * g)) * 32767))
        w.writeframes(bytes(data))
def env(t, a, d): return (min(1.0, t / a) if a > 0 else 1.0) * math.exp(-t / d)
def tone(freq, dur, a=0.002, d=0.05, harm=(1.0,), gain=1.0):
    return [gain * env(i / SR, a, d) * sum(h * math.sin(2 * math.pi * freq * (k + 1) * i / SR) for k, h in enumerate(harm)) for i in range(int(SR * dur))]
def sweep(f0, f1, dur, a=0.003, d=0.05, gain=1.0):
    out, ph = [], 0.0
    for i in range(int(SR * dur)):
        t = i / SR; f = f0 + (f1 - f0) * (t / dur); ph += 2 * math.pi * f / SR
        out.append(gain * env(t, a, d) * math.sin(ph))
    return out
def noise(dur, a=0.01, d=0.1, lp=0.2, gain=1.0, seed=1):
    random.seed(seed); out, y = [], 0.0
    for i in range(int(SR * dur)):
        y += lp * (random.uniform(-1, 1) - y); out.append(gain * env(i / SR, a, d) * y)
    return out
def mix(*parts):
    n = max(int(off * SR) + len(p) for off, p in parts); out = [0.0] * n
    for off, p in parts:
        o = int(off * SR)
        for i, s in enumerate(p): out[o + i] += s
    return out
def note(m): return 440.0 * 2 ** ((m - 69) / 12)
MARIMBA = (1, 0.0, 0.25, 0.0, 0.08)
BELL = (1, 0.5, 0.3, 0.2, 0.1)
