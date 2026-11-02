# S7 시험음(SoundPlan.md S7 — 맵 바탕 환경음, 20초 이음매 없는 반복). numpy 합성, 귀여운 카툰 톤. 정식 음원은 S8에서 교체한다.
# 네 곡 모두은 순환 처리로 이음매가 없다. 변형은 ID_1, ID_2 …(카탈로그가 무작위로 고른다).
import numpy as np

SR = 44100
rng = np.random.default_rng(707)


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
MAR = (1, 0, 0.25, 0, 0.08)
BELL = (1, 0.5, 0.3, 0.2, 0.1)
E = "out/SFX/Escape/"; D = "out/SFX/Device/"


def lpf(x, a):   # 빠른 1차 저역 통과(긴 신호용) — scipy 없이 누적 필터를 덩어리로
    from itertools import accumulate
    y = np.fromiter(accumulate(x, lambda s, v: s + a * (v - s)), dtype=np.float64, count=len(x))
    return y


def wnoise(sec, cut, seed): return lpf(np.random.default_rng(seed).uniform(-1, 1, int(sec * SR)), cut)


def sweep(f0, f1, sec, harm=(1,)):
    t = t_(sec); f = f0 * (f1 / f0) ** (t / sec); ph = 2 * np.pi * np.cumsum(f) / SR
    return sum(h * np.sin(ph * (k + 1)) for k, h in enumerate(harm))


def shape(sec, a, r):   # 올라가고 내려가는 모양(초)
    t = t_(sec); return np.minimum(1, t / max(a, 1e-3)) * np.minimum(1, (sec - t) / max(r, 1e-3))


def scatter(sec, count, make, t0=0.0, t1=None, accel=1.0, seed=0):   # 짧은 소리를 시간에 흩뿌림(accel>1이면 뒤로 갈수록 촘촘)
    out = np.zeros(int(sec * SR)); r = np.random.default_rng(seed); t1 = sec if t1 is None else t1
    for i in range(count):
        u = (i + r.uniform(0, 0.6)) / count; at = t0 + (t1 - t0) * u ** (1 / accel)
        seg = make(i, r); s = int(at * SR); e = min(len(out), s + len(seg)); out[s:e] += seg[:e - s]
    return out


def thump(f=60, sec=0.4, d=0.1): return tone(f, sec, a=0.003, d=d, harm=(1, 0.6, 0.2), glide=-0.3)


def crack(r, cut=0.85, d=0.006): return noise(0.03, a=0.0003, d=d, cut=cut) * r.uniform(0.4, 1.0)


def puff(r, d=0.09): return lpf(r.uniform(-1, 1, int(0.25 * SR)), 0.15) * env(t_(0.25), 0.01, d)


def shimmer(sec, base, seed, n=40, rise=0.0):
    def mk(i, r):
        m = base + r.integers(0, 24) + rise * i / n
        return tone(note(m), 0.35, d=0.08, harm=BELL) * r.uniform(0.3, 0.9)
    return scatter(sec, n, mk, seed=seed)


A = "out/SFX/Ambience/"
LEN = 20.0
N = int(LEN * SR)


def cyc_noise(cut, seed):   # 순환 저역 잡음: 두 바퀴 걸러 둘째 바퀴만 — 끝과 처음의 필터 상태가 같아 이음매가 없다
    x = np.random.default_rng(seed).uniform(-1, 1, N)
    return lpf(np.concatenate([x, x]), cut)[N:]


def cyc_lfo(hz, phase=0.0):   # 20초에 정수 바퀴가 되도록 맞춘 느린 흔들림
    k = max(1, round(hz * LEN)); return np.sin(2 * np.pi * k / LEN * np.arange(N) / SR + phase)


def place(out, seg, at):   # 순환 배치(끝을 넘으면 처음으로)
    s = int(at * SR) % N; idx = (np.arange(len(seg)) + s) % N; out[idx] += seg


def wind(seed, cut=0.02, gust=0.12):
    w = cyc_noise(cut, seed) - 0.6 * cyc_noise(cut * 0.25, seed + 1)
    return w * (0.55 + 0.3 * cyc_lfo(gust) + 0.15 * cyc_lfo(gust * 2.7, 1.3))


def chirp(r):   # 작은 새: 빠르게 올라갔다 내려가는 짹짹 2~4번
    out = np.zeros(int(0.6 * SR)); f0 = r.uniform(2600, 4200)
    for k in range(r.integers(2, 5)):
        seg = sweep(f0, f0 * r.uniform(1.3, 1.8), 0.07) * shape(0.07, 0.01, 0.04); s = int(k * r.uniform(0.09, 0.13) * SR); out[s:s + len(seg)] += seg
    return out


# ---- 캔디숲: 바람 + 새 + 풍경(바람 종) ----
r = np.random.default_rng(1)
x = 3.0 * wind(10)
birds = np.zeros(N)
for i in range(16): place(birds, 0.25 * chirp(r), r.uniform(0, LEN))
chimes = np.zeros(N)
for i in range(10): place(chimes, 0.12 * tone(note(84 + int(r.choice([0, 2, 4, 7, 9, 12]))), 1.6, d=0.6, harm=BELL), r.uniform(0, LEN))
write_wav(A + "AmbCandyForest.wav", x + birds + chimes, peak=0.5)

# ---- 진저브레드: 바람 + 멀리 시계탑 째깍(1초, 똑-딱) + 가끔 삐걱 ----
r = np.random.default_rng(2)
x = 3.0 * wind(20, cut=0.015, gust=0.08)
tick = np.zeros(N)
for k in range(int(LEN)):
    f = 2200 if k % 2 == 0 else 1700
    place(tick, 0.18 * mix((0, noise(0.02, a=0.0003, d=0.004, cut=0.9, seed=100 + k)), (0, tone(f, 0.05, d=0.01))), k)
creak = np.zeros(N)
for i in range(3):
    ct = t_(0.6); place(creak, 0.08 * np.sin(2 * np.pi * np.cumsum(300 + 60 * ct + 20 * np.sin(2 * np.pi * 19 * ct)) / SR) * shape(0.6, 0.1, 0.3), r.uniform(0, LEN))
write_wav(A + "AmbGingerbread.wav", x + tick + creak, peak=0.45)

# ---- 놀이공원: 바람 + 멀리 단조 오르골(드문 음, 흐릿하게) + 낡은 깃발 펄럭 ----
r = np.random.default_rng(3)
x = 3.0 * wind(30, cut=0.025, gust=0.1)
box = np.zeros(N)
melody = [69, 72, 76, 74, 72, 71, 72, 69, 64, 68, 71, 69]   # 가단조
for i, m in enumerate(melody * 2):
    place(box, 0.1 * tone(note(m + 12), 1.2, d=0.4, harm=BELL), i * LEN / 24 + r.uniform(-0.05, 0.05))
box = lpf(np.concatenate([box, box]), 0.25)[N:]
flap = np.zeros(N)
for i in range(6): place(flap, 0.15 * noise(0.25, a=0.02, d=0.08, cut=0.3, seed=300 + i), r.uniform(0, LEN))
write_wav(A + "AmbCarnival.wav", x + 1.4 * box + flap, peak=0.45)

# ---- 베이커리: 오븐 장작 타닥 + 유령 바람(낮게 우우) ----
r = np.random.default_rng(4)
x = 2.0 * wind(40, cut=0.012, gust=0.06)
crack = np.zeros(N)
for i in range(70): place(crack, 0.2 * noise(0.02, a=0.0003, d=r.uniform(0.002, 0.008), cut=r.uniform(0.5, 0.95), seed=400 + i) * r.uniform(0.3, 1), r.uniform(0, LEN))
ghost = np.zeros(N)
for i in range(3):
    gt = t_(4.0); f = r.uniform(180, 260)
    g = np.sin(2 * np.pi * np.cumsum(f * (1 + 0.15 * np.sin(2 * np.pi * 0.4 * gt)) + 6 * np.sin(2 * np.pi * 5 * gt)) / SR) * shape(4.0, 1.5, 1.8)
    place(ghost, 0.12 * g, r.uniform(0, LEN))
write_wav(A + "AmbBakery.wav", x + crack + ghost, peak=0.45)
print("ok")
