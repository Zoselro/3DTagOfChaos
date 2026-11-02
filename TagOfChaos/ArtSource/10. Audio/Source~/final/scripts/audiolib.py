# 최종 음원 제작 공용 도구(AudioProductionPlan.md §0·§2). 악기 레이어 = FluidSynth + FluidR3_GM, 질감 레이어 = numpy/scipy 합성.
# 모든 함수는 모노 float64 배열(SR 44100)을 주고받는다. 스테레오는 BGM에서만 따로 다룬다.
import os, subprocess, tempfile, wave
import numpy as np
import mido
from scipy import signal
import pyloudnorm as pyln

SR = 44100
SF2 = "/usr/share/sounds/sf2/FluidR3_GM.sf2"
RNG = np.random.default_rng(2026)

# GM 악기 번호(0부터)
GM = dict(piano=0, harpsichord=6, celesta=8, glock=9, musicbox=10, vibes=11, marimba=12, xylo=13, tubular=14,
          organ=19, accordion=21, contrabass=43, trem_strings=44, pizz=45, harp=46, timpani=47, strings=48,
          choir=52, oohs=53, trombone=57, tuba=58, brass=61, oboe=68, bassoon=70, clarinet=71, flute=73,
          calliope=82, warm_pad=89, sweep_pad=95, crystal=98, woodblock=115, taiko=116, rev_cymbal=119)

NAMES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def n(name):
    """'C5' 'F#4' 'Bb3' → MIDI 번호"""
    p = NAMES[name[0]]; i = 1
    while i < len(name) and name[i] in "#b":
        p += 1 if name[i] == "#" else -1; i += 1
    return p + 12 * (int(name[i:]) + 1)


# ---------------- 기본 ----------------

def t_(sec): return np.arange(int(sec * SR)) / SR


def silence(sec): return np.zeros(int(sec * SR))


def env(sec, a=0.005, d=0.1, sustain=0.0):
    t = t_(sec); return np.minimum(1, t / max(a, 1e-4)) * (sustain + (1 - sustain) * np.exp(-t / max(d, 1e-4)))


def shape(sec, a, r):
    t = t_(sec); return np.clip(np.minimum(t / max(a, 1e-4), (sec - t) / max(r, 1e-4)), 0, 1)


def mix(*parts):
    """mix((시작초, 배열), ...) — 길이가 달라도 된다"""
    n_ = max(int(o * SR) + len(p) for o, p in parts); out = np.zeros(n_)
    for o, p in parts: s = int(o * SR); out[s:s + len(p)] += p
    return out


def fade(x, fin=0.002, fout=0.01):
    x = x.copy(); a = int(fin * SR); b = int(fout * SR)
    if a: x[:a] *= np.linspace(0, 1, a)
    if b: x[-b:] *= np.linspace(1, 0, b)
    return x


def trim(x, thresh=1e-4, tail=0.02):
    idx = np.where(np.abs(x) > thresh)[0]
    if len(idx) == 0: return x
    return x[idx[0]: min(len(x), idx[-1] + int(tail * SR))]


# ---------------- 필터 ----------------

def bp(x, lo, hi, order=2):
    sos = signal.butter(order, [lo, min(hi, SR / 2 - 100)], btype="band", fs=SR, output="sos"); return signal.sosfilt(sos, x)


def lp(x, hz, order=2):
    sos = signal.butter(order, min(hz, SR / 2 - 100), btype="low", fs=SR, output="sos"); return signal.sosfilt(sos, x)


def hp(x, hz, order=2):
    sos = signal.butter(order, hz, btype="high", fs=SR, output="sos"); return signal.sosfilt(sos, x)


def resonator(x, freq, q=30):
    if freq >= SR / 2 - 200: return np.zeros_like(x)  # 들을 수 없는 대역은 버린다
    b, a = signal.iirpeak(freq, q, fs=SR); return signal.lfilter(b, a, x)


# ---------------- 공간·변형 ----------------

_IR = {}


def ir(seconds, bright=6000, seed=7):
    key = (seconds, bright, seed)
    if key not in _IR:
        r = np.random.default_rng(seed); t = t_(seconds)
        tail = r.uniform(-1, 1, len(t)) * np.exp(-6.9 * t / seconds)
        tail = lp(tail, bright)
        tail[:int(0.008 * SR)] *= np.linspace(0, 1, int(0.008 * SR))
        _IR[key] = tail / np.sqrt(np.sum(tail ** 2))
    return _IR[key]


def reverb(x, seconds=1.0, wet=0.25, bright=6000):
    """합성 잔향(지수 감쇠 잡음 IR). 맵마다 seconds로 공간 크기를 바꾼다"""
    y = signal.fftconvolve(np.concatenate([x, silence(seconds)]), ir(seconds, bright))[:len(x) + int(seconds * SR)]
    return np.concatenate([x, silence(seconds)]) * (1 - wet) + y * wet * 3


def reverse(x): return x[::-1].copy()


def resample(x, ratio):
    """ratio > 1 이면 높고 짧게"""
    idx = np.arange(0, len(x) - 1, ratio); return np.interp(idx, np.arange(len(x)), x)


def detune_layer(x, cents=15):
    return 0.5 * (x[:min(len(x), len(resample(x, 2 ** (cents / 1200))))] + resample(x, 2 ** (cents / 1200))[:len(x)])


def tape_wobble(x, depth=0.004, rate=0.7):
    """낡은 테이프처럼 음높이가 흔들림(놀이공원)"""
    t = np.arange(len(x)) / SR; warp = np.cumsum(1 + depth * np.sin(2 * np.pi * rate * t))
    return np.interp(np.clip(warp, 0, len(x) - 1), np.arange(len(x)), x)


# ---------------- 악기(FluidSynth) ----------------

def inst(notes, program, length=None, gain=0.6, channel=0, drum=False):
    """notes: [(시작초, 길이초, 'C5'|번호, 세기)] → 모노 배열. 잔향은 끄고(직접 입힌다) 렌더한다"""
    mid = mido.MidiFile(ticks_per_beat=480)
    tr = mido.MidiTrack(); mid.tracks.append(tr)
    ch = 9 if drum else channel
    tr.append(mido.MetaMessage("set_tempo", tempo=500000))  # 120 BPM → 1박 = 0.5초 = 480틱
    if not drum: tr.append(mido.Message("program_change", channel=ch, program=program, time=0))
    tr.append(mido.Message("control_change", channel=ch, control=91, value=0, time=0))
    tr.append(mido.Message("control_change", channel=ch, control=93, value=0, time=0))
    ev = []
    for start, dur, pitch, vel in notes:
        p = n(pitch) if isinstance(pitch, str) else pitch
        ev.append((int(start * 960), 1, mido.Message("note_on", channel=ch, note=p, velocity=int(vel))))
        ev.append((int((start + dur) * 960), 0, mido.Message("note_off", channel=ch, note=p, velocity=0)))
    ev.sort(key=lambda e: (e[0], e[1])); last = 0
    for tick, _, m in ev: tr.append(m.copy(time=tick - last)); last = tick
    end = max(s + d for s, d, _, _ in notes) + 2.0
    tr.append(mido.MetaMessage("end_of_track", time=int(end * 960) - last))
    with tempfile.TemporaryDirectory() as d:
        mp, wp = os.path.join(d, "a.mid"), os.path.join(d, "a.wav")
        mid.save(mp)
        subprocess.run(["fluidsynth", "-ni", "-g", str(gain), "-r", str(SR), "-R", "0", "-C", "0", "-F", wp, SF2, mp], check=True, capture_output=True)
        with wave.open(wp) as w:
            c = w.getnchannels(); x = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float64) / 32768
    x = x.reshape(-1, c).mean(axis=1)
    x = trim(x, 1e-4)
    if length: x = np.pad(x, (0, max(0, int(length * SR) - len(x))))[:int(length * SR)]
    return x


def notes_seq(pitches, step, dur=None, vel=90, start=0.0):
    return [(start + i * step, dur or step, p, vel) for i, p in enumerate(pitches)]


# ---------------- 질감 재료(AudioProductionPlan §2) ----------------

def noise(sec, seed=None):
    r = np.random.default_rng(seed) if seed is not None else RNG
    return r.uniform(-1, 1, int(sec * SR))


def crunch(count=12, spread=0.06, lo=2500, hi=11000, seed=None, size=0.012):
    """쿠키 바삭: 짧은 잡음 그레인을 흩뿌림"""
    r = np.random.default_rng(seed); out = silence(spread + size + 0.02)
    for i in range(count):
        g = bp(noise(size, r.integers(1 << 30)), lo, hi) * np.exp(-t_(size) / (size * 0.25)) * r.uniform(0.3, 1)
        s = int(r.uniform(0, spread) ** 1.3 / spread ** 0.3 * SR); out[s:s + len(g)] += g
    return out


def soft_wood(freq=650, sec=0.12, seed=None):
    """나무 장난감 톡: 감쇠 빠른 공명 몇 개"""
    exc = noise(0.004, seed) * np.hanning(int(0.004 * SR))
    x = np.concatenate([exc, silence(sec)])
    return sum(resonator(x, f, q) * g for f, q, g in ((freq, 25, 1), (freq * 2.37, 30, 0.5), (freq * 3.9, 35, 0.25)))


def pop(f0=500, f1=1100, sec=0.08):
    t = t_(sec); f = f0 * (f1 / f0) ** (t / sec); ph = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(ph) * env(sec, 0.001, sec * 0.35) + 0.15 * bp(noise(sec), 2000, 6000) * env(sec, 0.0005, 0.006)


def low_thud(f=55, sec=0.5, drop=0.35):
    t = t_(sec); fr = f * (1 + drop * np.exp(-t / 0.03)); ph = 2 * np.pi * np.cumsum(fr) / SR
    body = (np.sin(ph) + 0.35 * np.sin(2 * ph)) * env(sec, 0.002, sec * 0.3)
    click = lp(noise(0.03), 900) * env(0.03, 0.0005, 0.008)
    return mix((0, body), (0, 0.5 * click))


def sub_drone(sec, f=38, wobble=0.15):
    t = t_(sec); return np.sin(2 * np.pi * f * t + 2 * np.sin(2 * np.pi * wobble * t)) * (0.8 + 0.2 * np.sin(2 * np.pi * 0.11 * t))


def wet(sec=0.3, f=180, rate=35, seed=None):
    """괴물·시럽 질척: 저역 잡음 + 빠르게 움직이는 공명"""
    t = t_(sec); x = lp(noise(sec, seed), 1200)
    fc = f * (1 + 0.6 * np.sin(2 * np.pi * rate * t + np.random.default_rng(seed).uniform(0, 6)))
    out = np.zeros_like(x); blk = 256
    for i in range(0, len(x), blk):
        seg = x[max(0, i - 512):i + blk]
        y = resonator(seg, float(np.clip(fc[i], 60, 4000)), 6)
        out[i:i + blk] = y[-len(x[i:i + blk]):]
    return out * env(sec, 0.01, sec * 0.4)


def whoosh(sec=0.4, f0=400, f1=2500, q_lo=0.6, q_hi=1.6, seed=None):
    t = t_(sec); x = noise(sec, seed); out = np.zeros_like(x); blk = 512
    fc = f0 * (f1 / f0) ** (t / sec)
    for i in range(0, len(x), blk):
        c = fc[i]; seg = x[max(0, i - 1024):i + blk]
        y = bp(seg, c * q_lo, c * q_hi)
        out[i:i + blk] = y[-len(x[i:i + blk]):]
    return out * np.sin(np.pi * np.clip(t / sec, 0, 1)) ** 1.5


def sparkle(sec=1.0, count=18, base=96, seed=None, scale=(0, 4, 7, 11, 12, 16)):
    r = np.random.default_rng(seed); out = silence(sec + 0.6)
    for i in range(count):
        f = 440 * 2 ** ((base + r.choice(scale) - 69) / 12); d = 0.25
        g = np.sin(2 * np.pi * f * t_(d)) * env(d, 0.001, 0.06) * r.uniform(0.2, 0.7)
        s = int(r.uniform(0, sec) * SR); out[s:s + len(g)] += g
    return out


def tin_metal(f=900, sec=0.35, seed=None):
    """카툰 금속(양철 장난감): 비조화 배음 링"""
    exc = noise(0.003, seed) * np.hanning(int(0.003 * SR)); x = np.concatenate([exc, silence(sec)])
    return sum(resonator(x, f * k, 80) * g for k, g in ((1, 1), (2.76, 0.6), (5.4, 0.35), (8.9, 0.2)))


def steam(sec=0.6, seed=None):
    t = t_(sec); return hp(noise(sec, seed), 3000) * np.minimum(1, t / 0.02) * np.exp(-t / (sec * 0.45))


def fire_crackle(sec=1.0, rate=25, seed=None):
    r = np.random.default_rng(seed); out = 0.15 * lp(noise(sec, seed), 400)
    for i in range(int(rate * sec)):
        c = bp(noise(0.006, r.integers(1 << 30)), 1500, 9000) * np.exp(-t_(0.006) / 0.0012) * r.uniform(0.2, 1)
        s = int(r.uniform(0, sec - 0.01) * SR); out[s:s + len(c)] += c
    return out


def paper(sec=0.15, seed=None):
    return bp(noise(sec, seed), 1500, 9000) * shape(sec, 0.02, sec * 0.6) * (0.6 + 0.4 * np.abs(np.sin(2 * np.pi * 40 * t_(sec))))


def wind(sec, cut=500, gust=0.12, seed=None):
    x = lp(noise(sec, seed), cut) - 0.5 * lp(noise(sec, (seed or 0) + 1), cut * 0.25)
    t = t_(sec); return x * (0.55 + 0.3 * np.sin(2 * np.pi * gust * t) + 0.15 * np.sin(2 * np.pi * gust * 2.7 * t + 1.3))


def growl(sec=0.8, f=95, seed=None):
    """카툰 괴물 목소리 흉내: 거친 톱니 + 포먼트 + 떨림(사람 목소리처럼 만들지 않음)"""
    r = np.random.default_rng(seed); t = t_(sec)
    jitter = lp(r.uniform(-1, 1, len(t)), 30) * 8
    fr = f * (1 + 0.08 * np.sin(2 * np.pi * 6 * t)) + jitter; ph = np.cumsum(fr) / SR
    saw = 2 * (ph % 1) - 1
    rough = saw * (1 + 0.5 * lp(r.uniform(-1, 1, len(t)), 60))
    v = resonator(rough, 420, 4) + 0.7 * resonator(rough, 900, 5) + 0.3 * resonator(rough, 2300, 6)
    return lp(v, 3500) * shape(sec, 0.06, 0.25)


# ---------------- 마무리 ----------------

def peak_db(x): return 20 * np.log10(np.max(np.abs(x)) + 1e-12)


def lufs(x):
    if len(x) < int(0.4 * SR): x = np.pad(x, (0, int(0.4 * SR) - len(x)))
    return pyln.Meter(SR).integrated_loudness(x)


def to_peak(x, db): return x / (np.max(np.abs(x)) + 1e-12) * 10 ** (db / 20)


def write(path, x, peak=-6.0):
    """peak=None 이면 음량을 바꾸지 않는다(견본 묶음 — 소리마다 맞춘 크기를 그대로 둔다)"""
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    x = fade(np.asarray(x, dtype=np.float64))
    if peak is not None: x = to_peak(x, peak)
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())
    return x


def reel(path, items, gap=0.9):
    """견본 묶음: (이름, 배열, 피크) 목록을 간격을 두고 이어 붙인다 → 들어 보기용"""
    parts = []; log = []; pos = 0.0
    for name, x, pk in items:
        y = to_peak(fade(x), pk); parts += [y, silence(gap)]; log.append(f"{pos:6.1f}s  {name}  ({len(y) / SR:.2f}s, peak {pk} dB)"); pos += len(y) / SR + gap
    write(path, np.concatenate(parts), peak=None)
    return log


def place(total, parts):
    """정해진 길이(초) 안에 (시작초, 배열) 들을 놓는다(넘치는 부분은 자른다)"""
    out = silence(total)
    for at, x in parts:
        s = int(at * SR); e = min(len(out), s + len(x))
        if e > s: out[s:e] += x[:e - s]
    return out


def cut(x, sec, release=0.04):
    """명세 길이에 맞춰 자르고 끝을 부드럽게 닫는다"""
    y = x[:int(sec * SR)].copy(); r = min(len(y), int(release * SR))
    if r: y[-r:] *= np.linspace(1, 0, r) ** 2
    return y


def centroid(x):
    sp = np.abs(np.fft.rfft(x)); f = np.fft.rfftfreq(len(x), 1 / SR); return float(np.sum(sp * f) / (np.sum(sp) + 1e-12))


def loopify(x, seconds, xfade=0.5):
    """이음매 없는 반복: 길이 seconds로 자르고, 넘친 꼬리를 앞부분에 같은 세기 교차로 겹친다(x는 seconds + xfade 이상)"""
    L = int(seconds * SR); X = int(xfade * SR)
    assert len(x) >= L + X, (len(x), L + X)
    y = x[:L].copy(); k = np.linspace(0, np.pi / 2, X)
    y[:X] = x[:X] * np.sin(k) + x[L:L + X] * np.cos(k)
    return y


def creak(sec=0.5, f0=35, f1=70, res=(650, 1500), seed=None):
    """나무 삐걱: 빠르기가 바뀌는 작은 충격 열 + 나무 공명"""
    r = np.random.default_rng(seed); out = silence(sec); t = 0.0
    while t < sec - 0.01:
        rate = f0 + (f1 - f0) * (t / sec); s = int(t * SR); out[s] += r.uniform(0.5, 1.0)
        t += 1.0 / rate * r.uniform(0.85, 1.15)
    y = sum(resonator(out, f, 18) * g for f, g in zip(res, (1.0, 0.5)))
    return y * shape(sec, 0.03, 0.12)


def norm_lufs(x, target):
    l = lufs(x if x.ndim == 1 else x)
    return x * 10 ** ((target - l) / 20)


def write_ogg(path, x, peak_limit=-1.0):
    import soundfile as sf
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    pk = np.max(np.abs(x)); lim = 10 ** (peak_limit / 20)
    if pk > lim: x = x * lim / pk
    # libsndfile의 Vorbis 인코더는 큰 배열을 한 번에 쓰면 비정상 종료할 수 있어 조금씩 나눠 쓴다
    ch = 1 if x.ndim == 1 else x.shape[1]
    with sf.SoundFile(path, "w", SR, ch, format="OGG", subtype="VORBIS") as f:
        for i in range(0, len(x), 4096): f.write(x[i:i + 4096])
    return x
