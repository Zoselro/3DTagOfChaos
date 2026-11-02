# S4 시험음(SoundPlan.md S4 — 캐릭터·색칠·대기실). numpy 합성, 귀여운 카툰 톤. 정식 음원은 S8에서 교체한다.
# 반복음(PaintStroke·CauldronBubble)은 순환 처리로 이음매가 없다. 변형은 ID_1, ID_2 …(카탈로그가 무작위로 고른다).
import numpy as np

SR = 44100
rng = np.random.default_rng(404)


def write_wav(path, x, peak=0.7):
    import os, wave
    os.makedirs(os.path.dirname(path), exist_ok=True)
    x = np.asarray(x, dtype=np.float64)
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())


def t_(sec): return np.arange(int(sec * SR)) / SR


def env(t, a, d): return np.minimum(1, t / max(a, 1e-4)) * np.exp(-t / d)


def tone(f, sec, a=0.002, d=0.05, harm=(1,), glide=0.0):
    t = t_(sec)
    f_t = f * (1 + glide * t / sec)
    ph = 2 * np.pi * np.cumsum(f_t) / SR
    return env(t, a, d) * sum(h * np.sin(ph * (k + 1)) for k, h in enumerate(harm))


def lp(x, a):
    y = np.empty_like(x); s = 0.0
    for i in range(len(x)): s += a * (x[i] - s); y[i] = s
    return y


def noise(sec, a=0.003, d=0.05, cut=0.2, seed=None):
    r = np.random.default_rng(seed) if seed is not None else rng
    t = t_(sec)
    return env(t, a, d) * lp(r.uniform(-1, 1, len(t)), cut)


def mix(*parts):
    n = max(int(o * SR) + len(p) for o, p in parts); out = np.zeros(n)
    for o, p in parts: out[int(o * SR):int(o * SR) + len(p)] += p
    return out


def fade_end(x, ms=6):
    n = int(SR * ms / 1000); x = x.copy(); x[-n:] *= np.linspace(1, 0, n); return x


def note(m): return 440.0 * 2 ** ((m - 69) / 12)


MAR = (1, 0, 0.25, 0, 0.08)
BELL = (1, 0.5, 0.3, 0.2, 0.1)
E = "out/SFX/Escape/"; T = "out/SFX/Tool/"
rng = np.random.default_rng(505)
# S5 시험음(SoundPlan.md S5 — 탈출 모드·도구). S4와 같은 도구 함수(위). 정식 음원은 S8에서 교체.

# ---- 탈출 모드 ----
write_wav(E + "ChestRespawn.wav", fade_end(mix((0, tone(note(79), 0.3, d=0.1, harm=BELL)), (0.1, tone(note(86), 0.4, d=0.15, harm=BELL)), (0, 0.4 * noise(0.3, a=0.05, d=0.1, cut=0.3, seed=1)))), peak=0.5)
write_wav(E + "ItemPickup.wav", fade_end(mix((0, tone(note(72), 0.12, d=0.04, harm=MAR, glide=0.3)), (0.06, tone(note(84), 0.18, d=0.06, harm=MAR)))), peak=0.55)
write_wav(E + "ItemDrop.wav", fade_end(mix((0, tone(160, 0.2, d=0.05, harm=(1, 0.5), glide=-0.3)), (0, 0.5 * noise(0.12, a=0.001, d=0.03, cut=0.4, seed=2)))), peak=0.55)
write_wav(E + "DeviceInsert.wav", fade_end(mix((0, 0.6 * noise(0.04, a=0.0005, d=0.01, cut=0.9, seed=3)), (0, tone(240, 0.15, d=0.04, harm=(1, 0.7, 0.4))), (0.06, tone(note(88), 0.3, d=0.1, harm=BELL)))), peak=0.6)
dc = mix(*[(i * 0.09, tone(note(m), 0.6, d=0.25, harm=BELL)) for i, m in enumerate((72, 76, 79, 84, 88, 91))], (0, 0.5 * tone(note(48), 1.4, a=0.05, d=0.6, harm=(1, 0.5, 0.3))))
write_wav(E + "DeviceComplete.wav", fade_end(dc), peak=0.75)
write_wav(E + "BoardHop.wav", fade_end(mix((0, tone(300, 0.2, d=0.06, glide=1.2)), (0.12, tone(note(84), 0.25, d=0.08, harm=MAR)))), peak=0.55)
write_wav(E + "RocketInsert.wav", fade_end(mix((0, 0.6 * noise(0.05, a=0.0005, d=0.012, cut=0.8, seed=4)), (0.02, tone(180, 0.25, d=0.07, harm=(1, 0.6, 0.3))), (0.1, tone(note(81), 0.3, d=0.1, harm=BELL)))), peak=0.6)
ht = t_(0.7)
hiss = lp(rng.uniform(-1, 1, len(ht)), 0.4) * env(ht, 0.01, 0.25)
write_wav(E + "RocketHatch.wav", fade_end(mix((0, hiss), (0.3, tone(140, 0.25, d=0.06, harm=(1, 0.5))))), peak=0.6)
hb = mix((0, tone(60, 0.25, d=0.07, harm=(1, 0.4), glide=-0.3)), (0.18, 0.75 * tone(55, 0.25, d=0.06, harm=(1, 0.4), glide=-0.3)))
write_wav(E + "HeartBeat.wav", fade_end(hb), peak=0.8)
write_wav(E + "EscapeSuccessSelf.wav", fade_end(mix(*[(i * 0.11, tone(note(m), 0.8, d=0.3, harm=BELL)) for i, m in enumerate((72, 76, 79, 84, 79, 84, 88))])), peak=0.7)

# ---- 도구 ----
n = SR   # 스턴건 조준 윙: 1초 반복(110 Hz + 220 Hz, 1초에 정수 주기 → 이음매 없음) + 4 Hz 떨림
at = np.arange(n) / SR
hum = (np.sin(2 * np.pi * 110 * at) + 0.5 * np.sin(2 * np.pi * 220 * at) + 0.2 * np.sin(2 * np.pi * 880 * at)) * (0.8 + 0.2 * np.sin(2 * np.pi * 4 * at))
write_wav(T + "StunAimHum.wav", hum, peak=0.25)
zap = tone(1800, 0.3, d=0.08, glide=-0.75) + 0.5 * noise(0.3, a=0.001, d=0.05, cut=0.9, seed=5)
write_wav(T + "StunFire.wav", fade_end(zap), peak=0.6)
crackle = np.zeros(int(0.5 * SR))
for i in range(18):
    s0 = int(rng.uniform(0, 0.4) * SR); seg = noise(0.02, a=0.0005, d=0.006, cut=0.95) * rng.uniform(0.4, 1.0); crackle[s0:s0 + len(seg)] += seg
write_wav(T + "StunHit.wav", fade_end(mix((0, crackle), (0, 0.5 * tone(900, 0.5, d=0.15, glide=-0.5)))), peak=0.6)
bt = t_(0.35)
swish = lp(rng.uniform(-1, 1, len(bt)), 0.15) * np.sin(np.pi * bt / 0.35) ** 2
write_wav(T + "BalloonThrow.wav", fade_end(swish + 0.2 * tone(500, 0.35, d=0.15, glide=0.6)), peak=0.45)
st = t_(0.8)
splash = lp(rng.uniform(-1, 1, len(st)), 0.45) * env(st, 0.002, 0.16)
drops = np.zeros(len(st))
for i in range(10):
    s0 = int(rng.uniform(0.05, 0.6) * SR); seg = tone(rng.uniform(900, 1600), 0.06, d=0.015, glide=-0.4) * rng.uniform(0.2, 0.5); drops[s0:s0 + len(seg)] += seg
write_wav(T + "BalloonSplash.wav", fade_end(mix((0, splash), (0, drops), (0, 0.5 * tone(220, 0.3, d=0.06, glide=-0.5)))), peak=0.7)
wt = t_(0.25)
whoosh = lp(rng.uniform(-1, 1, len(wt)), 0.2) * np.sin(np.pi * wt / 0.25) ** 3
write_wav(T + "HammerSwing.wav", fade_end(whoosh), peak=0.45)
write_wav(T + "HammerBonk.wav", fade_end(mix((0, tone(520, 0.35, d=0.08, harm=(1, 0.6, 0.3), glide=0.15)), (0.0, 0.5 * noise(0.05, a=0.0005, d=0.01, cut=0.7, seed=6)), (0.05, tone(note(91), 0.2, d=0.05, harm=BELL)))), peak=0.7)
print("ok")
