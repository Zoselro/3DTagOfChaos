# 맵 환경음(2026-10-03 요청): 공장 기계 소리(AmbFactory), 회전목마 소리(CarouselSpin), 진저브레드 지하 동굴(AmbCave).
# numpy 합성 + FluidSynth(회전목마 오르간). 반복 이음매: 사건(쿵·칙)은 반복 길이의 약수 주기로 놓고, 지속음은 정수 주기로 맞춘다.
import os, subprocess, wave
import numpy as np
import mido

SR = 44100
SF2 = "/usr/share/sounds/sf2/FluidR3_GM.sf2"
rng = np.random.default_rng(7)

def write(path, x, peak=0.8):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())
    print(path, f"{len(x) / SR:.2f}s seam {abs(x[-1] - x[0]):.4f}")

def lowpass(x, a):  # 1차 저역 통과(a: 0~1, 작을수록 어둡다), 순환 처리로 이음매 유지
    y = np.empty_like(x); s = x[-1]
    for _ in range(2):
        for i in range(len(x)):
            s += a * (x[i] - s); y[i] = s
    return y

def place(buf, t, ev):  # 순환 버퍼에 사건을 더한다(끝을 넘으면 앞으로 감김)
    i = int(t * SR) % len(buf); n = len(ev)
    end = min(len(buf), i + n); buf[i:end] += ev[:end - i]
    if i + n > len(buf): buf[:i + n - len(buf)] += ev[end - i:]

def factory(seconds=8.0):
    n = int(seconds * SR); t = np.arange(n) / SR
    x = np.zeros(n)
    # 모터 웅웅: 55 Hz 계열(8초 × 55 = 정수 주기), 2 Hz로 살짝 맥동
    hum = sum(a * np.sin(2 * np.pi * f * t) for f, a in ((55, 1.0), (110, 0.55), (165, 0.3), (220, 0.12)))
    x += 0.35 * hum * (0.85 + 0.15 * np.sin(2 * np.pi * 2 * t))
    # 기어 틱: 초당 8번
    tick_len = int(0.012 * SR); tt = np.arange(tick_len) / SR
    tick = np.exp(-tt / 0.003) * np.sin(2 * np.pi * 3200 * tt)
    for k in range(int(seconds * 8)): place(x, k / 8, 0.12 * tick * (1.0 if k % 2 == 0 else 0.7))
    # 금속 쿵: 1초마다(번갈아 높낮이)
    cl_len = int(0.35 * SR); ct = np.arange(cl_len) / SR
    for k in range(int(seconds)):
        f0 = 300 if k % 2 == 0 else 260
        clank = sum(a * np.exp(-ct / d) * np.sin(2 * np.pi * f0 * m * ct) for m, a, d in ((1, 1.0, 0.08), (2.76, 0.6, 0.05), (5.4, 0.4, 0.03), (8.9, 0.25, 0.02)))
        thump = np.exp(-ct / 0.05) * np.sin(2 * np.pi * 70 * ct)
        place(x, k + 0.02, 0.5 * clank + 0.6 * thump)
    # 증기 피스톤 칙: 2초마다
    hs_len = int(0.45 * SR); ht = np.arange(hs_len) / SR
    for k in range(int(seconds / 2)):
        hiss = rng.standard_normal(hs_len) * np.minimum(1, ht / 0.02) * np.exp(-ht / 0.15)
        place(x, k * 2 + 0.55, 0.35 * hiss)
    x = lowpass(x, 0.35)
    return x

def carousel_organ(bars=16, bpm=132):
    beat = 60 / bpm; tpb = 480
    mid = mido.MidiFile(ticks_per_beat=tpb)
    meta = mido.MidiTrack(); mid.tracks.append(meta)
    meta.append(mido.MetaMessage("set_tempo", tempo=mido.bpm2tempo(bpm)))
    def track(ch, prog, notes, vel):
        tr = mido.MidiTrack(); mid.tracks.append(tr)
        tr.append(mido.Message("program_change", channel=ch, program=prog, time=0))
        ev = []
        for rep in range(3):
            for b, d, p in notes:
                s = int((b + rep * bars * 3) * tpb); e = s + int(d * tpb) - 4
                ev += [(s, 1, mido.Message("note_on", channel=ch, note=p, velocity=vel)), (e, 0, mido.Message("note_off", channel=ch, note=p, velocity=0))]
        ev.sort(key=lambda m: (m[0], m[1])); last = 0
        for t0, _, m in ev: tr.append(m.copy(time=t0 - last)); last = t0
    # A단조 왈츠(저주받은 놀이공원) — 오르간(칼리오페) 선율, 튜바 쿵·글로켄 짝짝
    prog = [[45, 57, 60, 64], [45, 57, 60, 64], [40, 56, 59, 64], [40, 56, 59, 64], [41, 57, 60, 65], [38, 57, 62, 65], [40, 56, 59, 64], [45, 57, 60, 64]]
    mel = [76, 77, 76, 75, 76, 72, 71, 72, 74, 72, 69, 71, 72, 74, 71, 69]
    lead, bass, chords = [], [], []
    for bar in range(bars):
        ch = prog[bar % 8]; b0 = bar * 3
        bass.append((b0, 0.9, ch[0] - 12))
        for k in (1, 2):
            for p in ch[1:]: chords.append((b0 + k, 0.5, p + 12))
        m = mel[bar % 16]
        lead += [(b0, 1.5, m), (b0 + 1.5, 0.5, m - 1 if bar % 2 else m + 2), (b0 + 2, 1, m - 3 if bar % 4 == 3 else m)]
    track(0, 82, lead, 80)    # Lead 3 (calliope)
    track(1, 58, bass, 90)    # Tuba
    track(2, 9, chords, 55)   # Glockenspiel
    path = "out/_carousel.mid"; os.makedirs("out", exist_ok=True); mid.save(path)
    subprocess.run(["fluidsynth", "-ni", "-g", "0.5", "-r", str(SR), "-F", "out/_carousel.wav", SF2, path], check=True, capture_output=True)
    with wave.open("out/_carousel.wav") as w:
        c = w.getnchannels(); d = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768
    d = d.reshape(-1, c).mean(axis=1)
    L = int(round(bars * 3 * beat * SR))
    return d[L:2 * L]

def carousel():
    org = carousel_organ()
    n = len(org); t = np.arange(n) / SR; seconds = n / SR
    org = org / (np.max(np.abs(org)) + 1e-9)
    # 아주 살짝 늘어지는 음정(낡은 오르간): 0.5 Hz 진폭 흔들림으로 대신
    org *= 0.9 + 0.1 * np.sin(2 * np.pi * 0.5 * t)
    # 회전 기계: 낮은 굴림 + 바퀴 덜컹(1.5초마다) + 가끔 삐걱
    rumble = lowpass(rng.standard_normal(n), 0.02) * 6
    x = 0.8 * org + 0.25 * rumble
    kl = int(0.08 * SR); kt = np.arange(kl) / SR
    knock = np.exp(-kt / 0.02) * np.sin(2 * np.pi * 120 * kt)
    for k in range(int(seconds / 1.5)): place(x, k * 1.5, 0.25 * knock)
    cl = int(0.5 * SR); ct = np.arange(cl) / SR
    creak = np.sin(2 * np.pi * (420 + 120 * np.sin(2 * np.pi * 3 * ct)) * ct) * np.sin(np.pi * ct / 0.5) * (1 + 0.5 * np.sign(np.sin(2 * np.pi * 37 * ct)))
    for k in range(3): place(x, seconds * (k + 0.3) / 3, 0.08 * creak)
    return x


def cave(seconds=24.0):
    """Wind through the ruins + water drops that echo off the stone (2026-10-03, Request1003Plan.md §4)."""
    n = int(seconds * SR); t = np.arange(n) / SR
    # wind: dark noise with slow swells (periods divide the loop so it repeats seamlessly)
    wind = lowpass(rng.standard_normal(n), 0.015) * 8

    swell = 0.55 + 0.25 * np.sin(2 * np.pi * t / seconds) + 0.12 * np.sin(2 * np.pi * 3 * t / seconds + 1.3) + 0.08 * np.sin(2 * np.pi * 5 * t / seconds + 0.4)
    x = 0.55 * wind * swell
    # water drops: pitch falls quickly, with stone echoes
    dl = int(0.09 * SR); dt = np.arange(dl) / SR
    times = np.sort(rng.uniform(0.5, seconds - 0.5, 8))
    for k, at in enumerate(times):
        f0 = rng.uniform(1300, 2100)
        f = f0 * (1 - 0.45 * np.minimum(1, dt / 0.03))
        ph = 2 * np.pi * np.cumsum(f) / SR
        drop = np.sin(ph) * np.exp(-dt / 0.035) * np.minimum(1, dt / 0.002)
        gain = rng.uniform(0.35, 0.6)
        for echo, (delay, g) in enumerate(((0.0, 1.0), (0.13, 0.38), (0.29, 0.2), (0.47, 0.1))):
            place(x, at + delay, gain * g * lowpass(drop, 0.6 - echo * 0.12))
    m = int(0.015 * SR)                                  # both ends meet at 0 (15 ms dip, inaudible under the wind) -> no click
    r = np.linspace(0, 1, m)
    x[:m] *= r
    x[-m:] *= r[::-1]
    return x

if __name__ == "__main__":
    write("out/SFX/Ambience/AmbFactory.wav", factory(), peak=0.8)
    write("out/SFX/Ambience/CarouselSpin.wav", carousel(), peak=0.8)
    write("out/SFX/Ambience/AmbCave.wav", cave(), peak=0.8)
