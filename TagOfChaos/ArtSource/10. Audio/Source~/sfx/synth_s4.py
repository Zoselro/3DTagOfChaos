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
C = "out/SFX/Character/"; P = "out/SFX/Paint/"; L = "out/SFX/Lobby/"

# ---- 쿠키(작고 바삭) ----
for k in range(3):   # 달리기 발소리 3종: 바삭한 짧은 부스럭 + 작은 톡
    crunch = noise(0.07, a=0.001, d=0.018, cut=0.55, seed=10 + k)
    tap = tone(180 + 25 * k, 0.07, d=0.02, harm=(1, 0.4))
    write_wav(C + f"CookieStep_{k + 1}.wav", fade_end(mix((0, 0.7 * crunch), (0, 0.6 * tap))), peak=0.5)
write_wav(C + "CookieLand.wav", fade_end(mix((0, tone(120, 0.18, d=0.05, harm=(1, 0.5), glide=-0.3)), (0, 0.8 * noise(0.14, d=0.04, cut=0.5, seed=21)))), peak=0.6)
wt = t_(0.28)
whoosh = lp(rng.uniform(-1, 1, len(wt)), 0.12) * np.sin(np.pi * wt / 0.28) ** 2
write_wav(C + "CookieDodge.wav", fade_end(whoosh + 0.25 * tone(600, 0.28, d=0.1, glide=0.8)), peak=0.5)
write_wav(C + "CookieGrab.wav", fade_end(mix((0, tone(note(76), 0.12, d=0.04, harm=MAR, glide=0.25)), (0.05, tone(note(83), 0.14, d=0.05, harm=MAR)))), peak=0.55)
write_wav(C + "CookieRelease.wav", fade_end(mix((0, tone(note(83), 0.12, d=0.04, harm=MAR, glide=-0.2)), (0.05, tone(note(74), 0.16, d=0.05, harm=MAR)))), peak=0.5)
crumb = np.zeros(int(0.8 * SR))
for i in range(40):   # 바스러짐: 작은 바삭 조각 여러 개가 흩어짐
    at = rng.uniform(0, 0.55) ** 1.5
    seg = noise(0.03, a=0.0005, d=0.008, cut=rng.uniform(0.4, 0.9)) * rng.uniform(0.3, 1.0) * (1 - at / 0.7)
    s = int(at * SR); crumb[s:s + len(seg)] += seg
crumb += 0.6 * tone(90, 0.8, d=0.12, harm=(1, 0.5))
write_wav(C + "CookieCrumble.wav", fade_end(crumb), peak=0.7)

# ---- 괴물(무겁고 낮게) ----
for k in range(2):
    thud = tone(52 + 6 * k, 0.4, a=0.004, d=0.12, harm=(1, 0.6, 0.2), glide=-0.25)
    write_wav(C + f"MonsterStep_{k + 1}.wav", fade_end(mix((0, thud), (0, 0.4 * noise(0.35, a=0.002, d=0.07, cut=0.06, seed=30 + k)))), peak=0.75)
dt = t_(0.5)
dash = mix((0, lp(rng.uniform(-1, 1, len(dt)), 0.25) * np.exp(-((dt - 0.12) / 0.07) ** 2)), (0.15, 0.7 * noise(0.2, a=0.001, d=0.04, cut=0.7, seed=41)), (0, 0.5 * tone(140, 0.5, d=0.15, glide=-0.5)))
write_wav(C + "MonsterDash.wav", fade_end(dash), peak=0.75)
gt = t_(1.1)
growl = np.sin(2 * np.pi * np.cumsum(70 + 8 * np.sin(2 * np.pi * 9 * gt)) / SR) * env(gt, 0.08, 0.5)
growl = growl + 0.5 * lp(rng.uniform(-1, 1, len(gt)), 0.05) * env(gt, 0.05, 0.4)
write_wav(C + "MonsterGrab.wav", fade_end(growl + 0.6 * np.pad(noise(0.2, a=0.001, d=0.05, cut=0.6, seed=52), (0, len(gt) - int(0.2 * SR)))), peak=0.75)
write_wav(C + "MonsterSquash.wav", fade_end(mix((0, tone(60, 0.35, d=0.1, harm=(1, 0.5))), (0, noise(0.3, a=0.001, d=0.06, cut=0.35, seed=61)), (0.02, 0.5 * tone(300, 0.2, d=0.05, glide=-0.6)))), peak=0.8)
write_wav(C + "MonsterAimLock.wav", fade_end(mix((0, tone(note(88), 0.08, d=0.03, harm=BELL)), (0.06, tone(note(95), 0.12, d=0.04, harm=BELL)))), peak=0.45)

# ---- 공통 ----
stars = np.zeros(int(0.9 * SR))
for i, m in enumerate((96, 100, 103, 100, 96, 103)):
    s = int(i * 0.12 * SR); seg = tone(note(m), 0.25, d=0.06, harm=BELL); stars[s:s + len(seg)] += seg * (1 - i * 0.1)
write_wav(C + "StunStars.wav", fade_end(stars), peak=0.5)
write_wav(C + "Respawn.wav", fade_end(mix((0, tone(note(72), 0.3, d=0.1, harm=BELL)), (0.08, tone(note(79), 0.3, d=0.1, harm=BELL)), (0.16, tone(note(84), 0.5, d=0.2, harm=BELL)), (0, 0.3 * noise(0.4, a=0.1, d=0.15, cut=0.3, seed=71)))), peak=0.55)
ct = t_(0.55)   # 문 열림: 삐걱(떨리는 높은 음) + 걸쇠 딸깍
creak = np.sin(2 * np.pi * np.cumsum(380 + 90 * ct + 25 * np.sin(2 * np.pi * 23 * ct)) / SR) * np.sin(np.pi * ct / 0.55) * (1 + 0.6 * np.sign(np.sin(2 * np.pi * 31 * ct)))
write_wav(C + "DoorOpen.wav", fade_end(mix((0, 0.5 * noise(0.03, a=0.0005, d=0.008, cut=0.8, seed=81)), (0.02, 0.5 * creak))), peak=0.5)
write_wav(C + "DoorClose.wav", fade_end(mix((0, tone(95, 0.25, d=0.07, harm=(1, 0.6))), (0, 0.5 * noise(0.12, a=0.001, d=0.03, cut=0.4, seed=82)), (0.03, 0.4 * noise(0.03, a=0.0005, d=0.008, cut=0.8, seed=83)))), peak=0.6)

# ---- 색칠(본인 2D) ----
n = SR   # 붓질 반복 1초: 부드러운 쓱쓱(0.25초 주기로 세기가 흔들림) — 순환이라 이음매 없음
brush = lp(rng.uniform(-1, 1, n), 0.3) - lp(rng.uniform(-1, 1, n), 0.04)
bt = np.arange(n) / SR
brush *= 0.6 + 0.4 * np.sin(2 * np.pi * 4 * bt) ** 2
write_wav(P + "PaintStroke.wav", brush, peak=0.3)
write_wav(P + "PaintSlotRegistered.wav", fade_end(mix((0, tone(note(84), 0.25, d=0.08, harm=MAR)), (0.07, tone(note(91), 0.35, d=0.12, harm=MAR)))), peak=0.5)
ft = t_(0.6)
splat = lp(rng.uniform(-1, 1, len(ft)), 0.2) * env(ft, 0.01, 0.15)
write_wav(P + "PaintForceFill.wav", fade_end(splat + 0.6 * tone(300, 0.6, d=0.2, glide=-0.5)), peak=0.6)

# ---- 대기실 ----
n = 4 * SR   # 가마솥 부글부글 4초 반복: 낮은 끓는 소리 + 거품 톡톡(순환 배치)
bub = 0.5 * lp(rng.uniform(-1, 1, n), 0.02) * 6
for i in range(26):
    f0 = rng.uniform(250, 600); pl = int(0.07 * SR); tt = np.arange(pl) / SR
    pop = np.sin(2 * np.pi * np.cumsum(f0 * (1 + 2.5 * tt / 0.07)) / SR) * np.exp(-tt / 0.02) * rng.uniform(0.3, 0.8)
    s = int(rng.uniform(0, 4) * SR); idx = (np.arange(pl) + s) % n; bub[idx] += pop
write_wav(L + "CauldronBubble.wav", bub, peak=0.5)
st = t_(0.9)
splash = lp(rng.uniform(-1, 1, len(st)), 0.35) * env(st, 0.005, 0.18) + 0.7 * tone(110, 0.9, d=0.2, glide=-0.4)
write_wav(L + "CauldronSplash.wav", fade_end(splash), peak=0.7)
write_wav(L + "MonsterDeparted.wav", fade_end(mix((0, tone(note(45), 1.4, a=0.05, d=0.6, harm=(1, 0.6, 0.4, 0.2))), (0.15, tone(note(52), 1.2, a=0.05, d=0.5, harm=(1, 0.5, 0.3))))), peak=0.6)
print("ok")
