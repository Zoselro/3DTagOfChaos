# S6 시험음(SoundPlan.md S6 — 맵 연출·로켓·마녀). numpy 합성, 귀여운 카툰 톤. 정식 음원은 S8에서 교체한다.
# 반복음(OvenGears·AltarHum)은 순환 처리로 이음매가 없다. 변형은 ID_1, ID_2 …(카탈로그가 무작위로 고른다).
import numpy as np

SR = 44100
rng = np.random.default_rng(606)


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


# ---- 탈출(스파이 로켓·탑승·마녀) ----
write_wav(E + "BoardSuck.wav", fade_end(0.8 * wnoise(0.7, 0.25, 1) * shape(0.7, 0.45, 0.2) + 0.5 * sweep(300, 1400, 0.7, harm=(1, 0.3)) * shape(0.7, 0.4, 0.25)), peak=0.55)
ign = wnoise(2.2, 0.08, 2) * np.linspace(0.2, 1, int(2.2 * SR)) + 0.5 * scatter(2.2, 60, lambda i, r: crack(r), accel=1.8, seed=3)
write_wav(E + "RocketIgnite.wav", fade_end(ign + 0.4 * tone(48, 2.2, a=0.8, d=3, harm=(1, 0.5))), peak=0.75)
lt = t_(4.5); roar = wnoise(4.5, 0.12, 4) * np.exp(-lt / 2.2) * np.minimum(1, lt / 0.1) + 0.4 * sweep(70, 260, 4.5, harm=(1, 0.5)) * np.exp(-lt / 1.8)
write_wav(E + "RocketLiftoff.wav", fade_end(roar, 200), peak=0.8)
wind = wnoise(4.5, 0.03, 5) * shape(4.5, 2.5, 1.2) * (0.6 + 0.4 * np.sin(2 * np.pi * 0.7 * t_(4.5)))
cackle = np.zeros(int(4.5 * SR))
for k in range(5):   # 낮은 마녀 웃음: 하-하-하(내려감)
    seg = tone(260 - 18 * k, 0.16, a=0.01, d=0.07, harm=(1, 0.7, 0.5, 0.3, 0.2), glide=-0.2); s = int((2.2 + 0.19 * k) * SR); cackle[s:s + len(seg)] += seg
write_wav(E + "WitchAppear.wav", fade_end(1.6 * wind + 0.6 * cackle + 0.3 * tone(note(38), 4.5, a=1.5, d=4, harm=(1, 0.5, 0.3)), 300), peak=0.7)
write_wav(E + "WitchSlam.wav", fade_end(mix((0, 1.2 * thump(38, 1.6, 0.45)), (0, wnoise(1.2, 0.3, 6) * env(t_(1.2), 0.002, 0.25)),
                                            (0.05, 0.5 * scatter(1.0, 30, lambda i, r: crack(r, 0.6, 0.02), seed=7))), 100), peak=0.9)

# ---- 캔디숲: 케이크 → 사탕 로켓 ----
rt = t_(2.0)
write_wav(D + "CakeRumble.wav", fade_end(wnoise(2.0, 0.02, 10) * 6 * (0.5 + 0.5 * np.sin(2 * np.pi * 9 * rt)) * shape(2.0, 0.3, 0.3) + 0.4 * tone(45, 2.0, a=0.3, d=3)), peak=0.7)
write_wav(D + "CakeCrack.wav", fade_end(scatter(0.8, 18, lambda i, r: mix((0, crack(r, 0.7, 0.01)), (0, 0.3 * tone(r.uniform(900, 1600), 0.05, d=0.01))), accel=1.5, seed=11)), peak=0.6)
write_wav(D + "CakeBurst.wav", fade_end(mix((0, thump(90, 0.5, 0.12)), (0, wnoise(0.6, 0.4, 12) * env(t_(0.6), 0.002, 0.12)),
                                            (0.05, 0.5 * scatter(1.0, 30, lambda i, r: crack(r, 0.5, 0.012), seed=13)), (0, 0.4 * tone(note(84), 0.5, d=0.15, harm=MAR, glide=0.3)))), peak=0.8)
write_wav(D + "CakeRocketRise.wav", fade_end(0.6 * sweep(80, 240, 3.0, harm=(1, 0.5, 0.3)) * shape(3.0, 0.3, 0.6) + 0.4 * wnoise(3.0, 0.05, 14) * shape(3.0, 0.3, 0.6) + 0.4 * shimmer(3.0, 84, 15, 14, rise=12)), peak=0.65)
write_wav(D + "CakeIgnite.wav", fade_end(wnoise(2.0, 0.1, 16) * np.linspace(0.2, 1, int(2 * SR)) + 0.35 * sweep(600, 1800, 2.0) * np.linspace(0, 1, int(2 * SR)) + 0.4 * scatter(2.0, 40, lambda i, r: crack(r), accel=2, seed=17)), peak=0.7)
ct = t_(5.0)
write_wav(D + "CakeLaunch.wav", fade_end(wnoise(5.0, 0.15, 18) * np.exp(-ct / 2.5) * np.minimum(1, ct / 0.05) + 0.5 * sweep(300, 1200, 5.0, harm=(1, 0.3)) * np.exp(-ct / 1.5) + 0.3 * shimmer(5.0, 88, 19, 25), 300), peak=0.8)

# ---- 놀이공원: 롤러코스터 ----
write_wav(D + "CoasterBulbOn.wav", fade_end(scatter(2.6, 12, lambda i, r: mix((0, 0.6 * noise(0.02, a=0.0003, d=0.004, cut=0.9)), (0, tone(note(72 + i), 0.18, d=0.05, harm=BELL))), t1=2.4, seed=20)), peak=0.5)
write_wav(D + "CoasterSparks.wav", fade_end(scatter(2.7, 120, lambda i, r: crack(r, 0.95, 0.004), seed=21) * shape(2.7, 0.2, 0.8)), peak=0.45)
st = t_(3.0); motor = sweep(40, 110, 3.0, harm=(1, 0.8, 0.6, 0.4, 0.3)) * (0.7 + 0.3 * np.sign(np.sin(2 * np.pi * 14 * st)))
write_wav(D + "CoasterStartup.wav", fade_end(0.6 * motor * shape(3.0, 0.4, 0.5) + 0.5 * scatter(3.0, 30, lambda i, r: 0.5 * thump(140, 0.08, 0.02), accel=1.5, seed=22)), peak=0.65)
clunk = lambda f: mix((0, tone(f, 0.25, d=0.05, harm=(1, 0.8, 0.5, 0.3))), (0, 0.6 * noise(0.04, a=0.0003, d=0.008, cut=0.9)))
write_wav(D + "CoasterLapBar.wav", fade_end(mix((0, clunk(320)), (0.18, clunk(280)))), peak=0.6)
clack = scatter(6.0, 34, lambda i, r: mix((0, thump(110, 0.1, 0.02)), (0.07, 0.8 * thump(100, 0.1, 0.02))), accel=1.8, seed=23)
write_wav(D + "CoasterDepart.wav", fade_end(clack * shape(6.0, 0.1, 1.8) + 0.4 * wnoise(6.0, 0.05, 24) * np.linspace(0.2, 1, int(6 * SR)) * shape(6.0, 0.5, 1.8), 200), peak=0.7)

# ---- 베이커리: 마법 오븐 ----
n = int(3.6 * SR); gear = np.zeros(n)   # 3.6초 반복: 피스톤 쿵 4번(0.9초) + 톱니 딸각 24번 + 웅웅(순환 배치)
for k in range(4):
    seg = mix((0, 0.8 * thump(70, 0.35, 0.08)), (0.05, 0.4 * puff(np.random.default_rng(30 + k), 0.1))); s = int(k * 0.9 * SR); idx = (np.arange(len(seg)) + s) % n; gear[idx] += seg
for k in range(24):
    seg = 0.35 * noise(0.03, a=0.0003, d=0.006, cut=0.8, seed=40 + k) + 0.15 * tone(1200 + 60 * (k % 3), 0.03, d=0.008); s = int(k * 0.15 * SR); idx = (np.arange(len(seg)) + s) % n; gear[idx] += seg
gear += 0.25 * np.sin(2 * np.pi * 55 * np.arange(n) / SR) + 0.1 * np.sin(2 * np.pi * 110 * np.arange(n) / SR)
write_wav(D + "OvenGears.wav", gear, peak=0.55)
write_wav(D + "OvenPiston.wav", fade_end(mix((0, thump(65, 0.5, 0.12)), (0.05, wnoise(0.9, 0.5, 31) * env(t_(0.9), 0.02, 0.3)))), peak=0.65)
dt = t_(3.0)
write_wav(D + "OvenDoorRoll.wav", fade_end(wnoise(3.0, 0.03, 32) * 5 * (0.6 + 0.4 * np.sin(2 * np.pi * 3 * dt)) * shape(3.0, 0.3, 0.8) + 0.3 * tone(55, 3.0, a=0.3, d=4, harm=(1, 0.5)) + 0.4 * mix((2.6, thump(80, 0.4, 0.1)))[:int(3 * SR)]), peak=0.7)
write_wav(D + "OvenFlash.wav", fade_end(mix((0, wnoise(1.0, 0.3, 33) * env(t_(1.0), 0.01, 0.25)), (0, 0.5 * sweep(400, 2400, 0.6, harm=(1, 0.3)) * shape(0.6, 0.05, 0.4)), (0.05, 0.5 * shimmer(1.2, 90, 34, 20)))), peak=0.7)

# ---- 공장: 초콜릿 기차 ----
write_wav(D + "TrainPuff.wav", fade_end(scatter(3.2, 10, lambda i, r: puff(r, 0.08), accel=2.2, seed=50) + 0.3 * wnoise(3.2, 0.02, 51) * 4 * shape(3.2, 1, 0.5)), peak=0.6)
wt = t_(1.3)
whistle = sum(np.sin(2 * np.pi * f * wt * (1 + 0.004 * np.sin(2 * np.pi * 5 * wt))) * a for f, a in ((587, 1), (740, 0.8), (880, 0.6)))
write_wav(D + "TrainWhistle.wav", fade_end((whistle + 0.3 * wnoise(1.3, 0.6, 52)) * shape(1.3, 0.08, 0.35)), peak=0.6)
write_wav(D + "TrainChug.wav", fade_end(scatter(6.5, 26, lambda i, r: mix((0, puff(r, 0.06)), (0, 0.5 * thump(90, 0.12, 0.03))), accel=2.0, seed=53) * shape(6.5, 0.05, 1.5), 200), peak=0.7)
tt = t_(2.5); tun = wnoise(2.5, 0.06, 54) * env(tt, 0.3, 0.8)
echo = tun.copy()
for k, g in ((0.12, 0.5), (0.25, 0.3), (0.4, 0.2)): s = int(k * SR); echo[s:] += g * tun[:-s]
write_wav(D + "TrainTunnel.wav", fade_end(echo + 0.3 * tone(65, 2.5, a=0.2, d=1)), peak=0.6)

# ---- 진저브레드: 룬 제단 → 포탈 ----
n = 4 * SR; at_ = np.arange(n) / SR   # 4초 반복: 정수 주기 화음(이음매 없음) + 느린 맥놀이
hum = sum(a * np.sin(2 * np.pi * f * at_) for f, a in ((110, 1), (165, 0.6), (220.25, 0.5), (329.5, 0.25), (440.5, 0.15)))
hum *= 0.8 + 0.2 * np.sin(2 * np.pi * 0.5 * at_)
write_wav(D + "AltarHum.wav", hum, peak=0.4)
write_wav(D + "AltarGlow.wav", fade_end(0.6 * shimmer(2.0, 79, 60, 24, rise=12) + 0.4 * sweep(220, 660, 2.0, harm=(1, 0.5)) * shape(2.0, 0.5, 0.5)), peak=0.6)
pt = t_(2.0)
write_wav(D + "PortalOpen.wav", fade_end(wnoise(2.0, 0.2, 61) * shape(2.0, 1.2, 0.6) * (0.6 + 0.4 * np.sin(2 * np.pi * (2 + 4 * pt / 2) * pt)) + 0.5 * sweep(150, 900, 2.0, harm=(1, 0.5)) * shape(2.0, 1.0, 0.6) + 0.3 * shimmer(2.0, 86, 62, 18)), peak=0.65)
write_wav(D + "PortalFlash.wav", fade_end(mix((0, wnoise(1.2, 0.35, 63) * env(t_(1.2), 0.01, 0.3)), (0, 0.6 * shimmer(1.2, 91, 64, 30)), (0, 0.4 * tone(note(60), 1.2, a=0.01, d=0.5, harm=BELL)))), peak=0.7)
write_wav(D + "PortalClose.wav", fade_end(mix((0, 0.6 * sweep(900, 120, 1.0, harm=(1, 0.5)) * shape(1.0, 0.05, 0.3)), (0, wnoise(1.0, 0.15, 65) * shape(1.0, 0.1, 0.4)), (0.95, tone(note(84), 0.2, d=0.05, harm=MAR)))), peak=0.65)
write_wav(D + "LanternFlicker.wav", fade_end(scatter(0.35, 6, lambda i, r: crack(r, 0.9, 0.004), seed=70) + 0.15 * np.sign(np.sin(2 * np.pi * 120 * t_(0.35))) * shape(0.35, 0.02, 0.2)), peak=0.35)
print("ok")
