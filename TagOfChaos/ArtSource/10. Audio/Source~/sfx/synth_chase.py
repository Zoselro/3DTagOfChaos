# 술래 접근 추격음 후보 4종(2026-10-03 요청, DistanceFadePlan.md §9). 쿠키 본인만 듣는 2D 반복음이며,
# 게임에서는 술래와의 거리에 따라 음량·먹먹함이 바뀐다. 각 후보는 반복 원본(Chase_X.wav)과
# 술래가 25 m → 2 m로 다가오는 미리듣기(Chase_X_approach.wav)를 만든다.
# 반복 이음매: MIDI는 세 바퀴 렌더 후 가운데 바퀴만 자르고(잔향 꼬리 포함), numpy 소리는 반복 길이의 약수 주기만 쓴다.
import os, subprocess, wave
import numpy as np
import mido

SR = 44100
SF2 = "/usr/share/sounds/sf2/FluidR3_GM.sf2"
OUT = "out/chase"
rng = np.random.default_rng(11)


def write(path, x, normalize=True):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if normalize: x = x / (np.sqrt(np.mean(x ** 2)) + 1e-9) * 0.16  # 후보끼리 비슷한 크기(RMS 약 −16 dBFS)
    peak = np.max(np.abs(x))
    if peak > 0.89: x = x / peak * 0.89                         # 피크 −1 dBFS 이하
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())
    print(f"{path}  {len(x) / SR:.2f}s  seam {abs(x[-1] - x[0]):.4f}")


def lowpass_circ(x, a):
    y = np.empty_like(x); s = x[-1]
    for _ in range(2):
        for i in range(len(x)):
            s += a * (x[i] - s); y[i] = s
    return y


def place(buf, t, ev):
    i = int(t * SR) % len(buf); n = len(ev)
    end = min(len(buf), i + n); buf[i:end] += ev[:end - i]
    if i + n > len(buf): buf[:i + n - len(buf)] += ev[end - i:]


# ---------------- MIDI 렌더 ----------------

class Song:
    def __init__(self, bpm, beats):
        self.bpm, self.beats, self.tpb = bpm, beats, 480
        self.tracks = {}

    def add(self, ch, prog, beat, dur, pitch, vel, bend=0):
        self.tracks.setdefault((ch, prog, bend), []).append((beat, dur, pitch, vel))

    def render(self, name):
        mid = mido.MidiFile(ticks_per_beat=self.tpb)
        meta = mido.MidiTrack(); mid.tracks.append(meta)
        meta.append(mido.MetaMessage("set_tempo", tempo=mido.bpm2tempo(self.bpm)))
        for (ch, prog, bend), notes in self.tracks.items():
            tr = mido.MidiTrack(); mid.tracks.append(tr)
            tr.append(mido.Message("program_change", channel=ch, program=prog, time=0))
            if bend: tr.append(mido.Message("pitchwheel", channel=ch, pitch=bend, time=0))
            ev = []
            for rep in range(3):
                for b, d, p, v in notes:
                    s = int((b + rep * self.beats) * self.tpb); e = s + max(10, int(d * self.tpb) - 6)
                    ev += [(s, 1, mido.Message("note_on", channel=ch, note=p, velocity=v)),
                           (e, 0, mido.Message("note_off", channel=ch, note=p, velocity=0))]
            ev.sort(key=lambda m: (m[0], m[1])); last = 0
            for t0, _, m in ev: tr.append(m.copy(time=t0 - last)); last = t0
        mp, wp = f"{OUT}/_{name}.mid", f"{OUT}/_{name}.wav"
        os.makedirs(OUT, exist_ok=True); mid.save(mp)
        subprocess.run(["fluidsynth", "-ni", "-g", "0.6", "-r", str(SR), "-F", wp, SF2, mp], check=True, capture_output=True)
        with wave.open(wp) as w:
            c = w.getnchannels(); d = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float64) / 32768
        d = d.reshape(-1, c).mean(axis=1)
        L = int(round(self.beats * 60 / self.bpm * SR))
        return d[L:2 * L]


# ---------------- 후보 ----------------

def chase_a():
    """A 심장 박동: 빨라진 심장(96 BPM) + 바닥을 긁는 저음 드론 + 거친 숨 + 높은 쇳소리 휘파람. 15초."""
    sec = 15.0; n = int(sec * SR); t = np.arange(n) / SR
    x = np.zeros(n)
    # 드론: E1·E2·F2(반음 충돌) — 주파수 × 15초가 정수라 이음매 없음
    for f, a in ((41.2, 1.0), (82.4, 0.5), (87.33, 0.35), (123.6, 0.15)):
        x += 0.22 * a * np.sin(2 * np.pi * f * t)
    x *= 0.8 + 0.2 * np.sin(2 * np.pi * t / 3.75)               # 3.75초마다 부풀었다 가라앉음
    # 심장: 쿵-쿵(lub-dub), 0.625초 간격
    hl = int(0.25 * SR); ht = np.arange(hl) / SR
    def thump(f, d): return np.sin(2 * np.pi * f * ht * (1 - 0.35 * ht / 0.25)) * np.exp(-ht / d) * np.minimum(1, ht / 0.004)
    for k in range(24):
        place(x, k * 0.625, 0.9 * thump(58, 0.06))
        place(x, k * 0.625 + 0.17, 0.6 * thump(52, 0.05))
    # 숨: 들숨·날숨 노이즈(3.75초 주기)
    breath = lowpass_circ(rng.standard_normal(n), 0.08) * 2.2
    phase = (t % 3.75) / 3.75
    x += 0.12 * breath * np.where(phase < 0.45, np.sin(np.pi * phase / 0.45), 0.6 * np.sin(np.pi * (phase - 0.45) / 0.55)) ** 2
    # 쇳소리 휘파람: 7.5초마다 떨리는 높은 음
    wl = int(3.0 * SR); wt = np.arange(wl) / SR
    whistle = np.sin(2 * np.pi * (1480 + 30 * np.sin(2 * np.pi * 5.5 * wt)) * wt) * np.sin(np.pi * wt / 3.0) ** 2
    place(x, 2.0, 0.05 * whistle); place(x, 9.5, 0.04 * whistle * 0.9)
    return x


def chase_b():
    """B 현악 공포: 낮은 현 트레몰로 반음 뭉치 + 콘트라베이스 8분 박동 + 높은 바이올린 찌르기. 120 BPM 8마디(16초)."""
    s = Song(120, 32)
    for bar in range(8):
        b0 = bar * 4
        for p in (40, 41, 46):                                   # E2·F2·Bb2 트레몰로
            s.add(0, 44, b0, 4, p, 70)
        for k in range(8):                                       # 콘트라베이스 박동(첫 박 강하게)
            s.add(1, 45, b0 + k * 0.5, 0.3, 28 if k % 4 else 28, 110 if k % 4 == 0 else 78)
        if bar % 2 == 1:                                         # 2마디마다 찌르기(반음 충돌)
            for p in (88, 89):
                s.add(2, 48, b0 + 2.5, 0.25, p, 95)
                s.add(2, 48, b0 + 3.0, 0.25, p, 100)
                s.add(2, 48, b0 + 3.5, 0.25, p, 105)
        if bar in (3, 7):                                        # 위로 미끄러지는 비올라
            for k, p in enumerate((64, 65, 67, 68, 70, 71, 73, 74)):
                s.add(3, 41, b0 + k * 0.5, 0.5, p, 60 + k * 5)
    return s.render("B")


def chase_c():
    """C 망가진 오르골: 단조 오르골 선율 + 살짝 어긋난 두 번째 오르골(음이 틀어짐) + 낮은 어둠 패드. 3/4 84 BPM 12마디."""
    s = Song(84, 36)
    mel = [76, 74, 72, 71, 72, 69, 68, 69, 71, 72, 74, 76,
           77, 76, 74, 72, 71, 68, 69, 71, 72, 71, 69, 68,
           76, 77, 76, 74, 72, 71, 69, 68, 64, 65, 64, 63]
    for i, p in enumerate(mel):
        s.add(0, 10, i, 1, p, 82)                                # 오르골
        s.add(1, 10, i + 0.04, 1, p, 52, bend=-1400)             # 틀어진 오르골(약 −1/3 반음, 살짝 늦게)
    chords = [(45, 52, 57), (45, 52, 57), (44, 52, 56), (44, 52, 56), (41, 48, 53), (41, 48, 53),
              (40, 47, 52), (40, 47, 51), (45, 51, 57), (44, 50, 56), (40, 46, 52), (40, 47, 51)]
    for bar, ch in enumerate(chords):
        for p in ch: s.add(2, 95, bar * 3, 3, p - 12, 60)        # 어둠 패드(Sweep)
        s.add(3, 47, bar * 3, 0.5, ch[0] - 12, 70 if bar % 2 == 0 else 45)  # 팀파니 한 번
    return s.render("C")


def chase_d():
    """D 북소리 추격: 타이코·팀파니 질주 + 현 16분 오스티나토 + 낮은 금관 경고. 140 BPM 8마디."""
    s = Song(140, 32)
    for bar in range(8):
        b0 = bar * 4
        for k in range(16):                                      # 현 16분 E 프리지안
            p = (52, 53, 52, 55)[k % 4] + (0 if bar % 4 < 2 else 1)
            s.add(0, 48, b0 + k * 0.25, 0.2, p, 70 + (20 if k % 4 == 0 else 0))
        for k in (0, 1.5, 2, 3, 3.5):                            # 타이코
            s.add(1, 116, b0 + k, 0.4, 41, 120 if k in (0, 2) else 90)
        s.add(2, 47, b0, 1, 40, 100); s.add(2, 47, b0 + 2, 1, 40, 85)   # 팀파니
        if bar % 2 == 1:                                         # 금관 경고(반음 아래로)
            s.add(3, 61, b0 + 2, 1, 41, 100); s.add(3, 61, b0 + 3, 1, 40, 105)
            s.add(3, 57, b0 + 2, 1, 29, 100); s.add(3, 57, b0 + 3, 1, 28, 105)
    return s.render("D")


# ---------------- 다가오는 미리듣기 ----------------

def approach(loop, far=25.0, near=2.0, audible=20.0, full=5.0, seconds=16.0):
    """술래가 far → near로 다가온다(앞 12초), 마지막 4초는 바로 옆. 계획서 §9의 기본값(20 m부터 들림, 5 m 안 최대)을 쓴다."""
    n = int(seconds * SR); t = np.arange(n) / SR
    x = np.resize(loop, n)
    d = np.where(t < 12, far + (near - far) * t / 12, near)
    k = np.clip((audible - d) / (audible - full), 0, 1)        # 0 = 들리지 않음, 1 = 최대
    gain = k * k * (3 - 2 * k)                                   # 부드러운 S자
    cutoff = 900 + (18000 - 900) * k                             # 멀면 먹먹하게
    a = 1 - np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty(n); s = 0.0
    for i in range(n):
        s += a[i] * (x[i] - s); y[i] = s
    return y * gain


if __name__ == "__main__":
    for name, fn in (("A_Heartbeat", chase_a), ("B_HorrorStrings", chase_b), ("C_BrokenMusicBox", chase_c), ("D_DrumChase", chase_d)):
        loop = fn()
        write(f"{OUT}/Chase_{name}.wav", loop)
        write(f"{OUT}/Chase_{name}_approach.wav", approach(loop / (np.sqrt(np.mean(loop ** 2)) + 1e-9) * 0.16), normalize=False)
