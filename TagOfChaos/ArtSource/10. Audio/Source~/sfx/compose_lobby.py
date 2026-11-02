# 로비 배경음 후보 4곡(SoundPlan S3 보완, 2026-10-03 요청). MIDI 작곡 → FluidSynth(FluidR3_GM) 렌더 → 이음매 없는 반복으로 자르기.
# 반복 이음매: 같은 곡을 두 번 이어서 렌더하고 두 번째 바퀴만 잘라 쓴다 — 첫 바퀴의 잔향이 두 번째 바퀴 앞부분에 겹쳐 있으므로,
# 잘라낸 구간을 반복해도 끝 → 처음이 실제 연주처럼 이어진다.
import os, subprocess, sys, wave
import numpy as np
import mido

SF2 = "/usr/share/sounds/sf2/FluidR3_GM.sf2"
SR = 44100
TPB = 480  # ticks per beat

NOTE = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
def n(name):
    """'C5', 'F#4', 'Bb3' -> MIDI 번호"""
    p = NOTE[name[0]]; i = 1
    while i < len(name) and name[i] in "#b":
        p += 1 if name[i] == "#" else -1; i += 1
    return p + 12 * (int(name[i:]) + 1)

class Song:
    def __init__(self, bpm, beats_per_bar, bars):
        self.bpm, self.bpb, self.bars = bpm, beats_per_bar, bars
        self.tracks = []  # (channel, program, [(beat, dur, pitch, vel)])
    def track(self, channel, program):
        events = []; self.tracks.append((channel, program, events)); return events
    @property
    def beats(self): return self.bpb * self.bars
    def midi(self, repeats):
        mid = mido.MidiFile(ticks_per_beat=TPB)
        meta = mido.MidiTrack(); mid.tracks.append(meta)
        meta.append(mido.MetaMessage("set_tempo", tempo=mido.bpm2tempo(self.bpm)))
        meta.append(mido.MetaMessage("time_signature", numerator=self.bpb, denominator=4))
        for ch, prog, events in self.tracks:
            msgs = []
            for r in range(repeats):
                off = r * self.beats
                for beat, dur, pitch, vel in events:
                    t0 = int((beat + off) * TPB); t1 = int((beat + off + dur) * TPB) - 2
                    msgs.append((t0, 1, mido.Message("note_on", channel=ch, note=pitch, velocity=vel)))
                    msgs.append((t1, 0, mido.Message("note_off", channel=ch, note=pitch, velocity=0)))
            msgs.sort(key=lambda m: (m[0], m[1]))
            tr = mido.MidiTrack(); mid.tracks.append(tr)
            if ch != 9: tr.append(mido.Message("program_change", channel=ch, program=prog, time=0))
            tr.append(mido.Message("control_change", channel=ch, control=91, value=50, time=0))  # 잔향
            last = 0
            for t, _, m in msgs:
                tr.append(m.copy(time=t - last)); last = t
        return mid

def render(song, path):
    tmp_mid = path + ".mid"; tmp_wav = path + ".raw.wav"
    song.midi(repeats=3).save(tmp_mid)  # 3바퀴: 두 번째 바퀴를 잘라 쓴다(세 번째는 두 번째 끝의 잔향 확보용)
    subprocess.run(["fluidsynth", "-ni", "-g", "0.5", "-r", str(SR), "-F", tmp_wav, SF2, tmp_mid], check=True, capture_output=True)
    with wave.open(tmp_wav) as w:
        ch = w.getnchannels(); data = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768
    data = data.reshape(-1, ch)
    L = int(round(song.beats * 60 / song.bpm * SR))
    loop = data[L:2 * L]
    rms = np.sqrt(np.mean(loop ** 2)) + 1e-9
    loop = loop * min(0.1 / rms, 0.89 / (np.max(np.abs(loop)) + 1e-9))  # 대략 -20 dBFS RMS, 피크 -1 dB 이하
    with wave.open(path, "wb") as w:
        w.setnchannels(ch); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(loop, -1, 1) * 32767).astype(np.int16).tobytes())
    os.remove(tmp_wav)
    seam = np.abs(loop[-1] - loop[0]).max()
    print(f"{os.path.basename(path)}: {len(loop) / SR:.1f}s, {song.bars} bars, seam jump {seam:.4f}")

def chord_notes(names): return [n(x) for x in names.split()]

def add_melody(ev, start_bar, bpb, line, vel=88, octave_shift=0):
    """line: [(note|None, beats)] 를 start_bar부터 이어 붙인다"""
    beat = start_bar * bpb
    for name, dur in line:
        if name: ev.append((beat, dur * 0.95, n(name) + 12 * octave_shift, vel))
        beat += dur
    return beat

# ---------------- A: 쿠키 마을 아침 (C장조 4/4 100) ----------------
def song_a():
    s = Song(100, 4, 32)
    box = s.track(0, 10); pizz = s.track(1, 45); mar = s.track(2, 12); bass = s.track(3, 32); fl = s.track(4, 73)
    prog = ["C4 E4 G4", "A3 C4 E4", "F3 A3 C4", "G3 B3 D4", "C4 E4 G4", "E3 G3 B3", "F3 A3 C4", "G3 B3 D4"]
    for bar in range(32):
        ch = chord_notes(prog[bar % 8]); b0 = bar * 4
        for k, p in enumerate([ch[0], ch[1], ch[2], ch[1]] * 2):  # 피치카토 8분 아르페지오
            pizz.append((b0 + k * 0.5, 0.45, p, 62 if k % 2 else 72))
        bass.append((b0, 1.8, ch[0] - 12, 80)); bass.append((b0 + 2, 1.8, ch[2] - 24, 70))
        if bar % 2 == 1: mar.append((b0 + 3.5, 0.5, ch[2] + 12, 60))
    mel = [("E5", 1), ("G5", 1), ("C6", 1.5), ("B5", 0.5), ("A5", 1), ("G5", 1), ("E5", 2),
           ("F5", 1), ("A5", 1), ("G5", 1), ("F5", 1), ("E5", 1), ("D5", 1), ("G4", 2),
           ("E5", 1), ("G5", 1), ("C6", 1.5), ("D6", 0.5), ("E6", 1), ("D6", 1), ("C6", 2),
           ("A5", 1), ("F5", 1), ("G5", 1), ("B4", 1), ("C5", 3), (None, 1)]
    add_melody(box, 0, 4, mel); add_melody(box, 8, 4, mel, vel=80)
    mel_b = [("A5", 1.5), ("G5", 0.5), ("F5", 1), ("E5", 1), ("D5", 2), ("G5", 2),
             ("G5", 1.5), ("F5", 0.5), ("E5", 1), ("D5", 1), ("C5", 2), ("E5", 2),
             ("F5", 1), ("A5", 1), ("C6", 1), ("A5", 1), ("G5", 2), ("E5", 2),
             ("D5", 1), ("E5", 1), ("F5", 1), ("B4", 1), ("C5", 4)]
    add_melody(fl, 16, 4, mel_b, vel=70); add_melody(box, 24, 4, mel, vel=84)
    for bar in range(16, 24): mar.append((bar * 4, 0.5, n("C6") if bar % 2 == 0 else n("G5"), 55))
    return s

# ---------------- B: 캔디 왈츠 (F장조 3/4 108) ----------------
def song_b():
    s = Song(108, 3, 32)
    cel = s.track(0, 8); harp = s.track(1, 46); pizz = s.track(2, 45); bsn = s.track(3, 70); glk = s.track(4, 9)
    prog = ["F3 A3 C4", "C3 E3 G3 Bb3", "D3 F3 A3", "Bb2 D3 F3", "F3 A3 C4", "G3 Bb3 D4", "C3 E3 G3 Bb3", "F3 A3 C4"]
    for bar in range(32):
        ch = chord_notes(prog[bar % 8]); b0 = bar * 3
        pizz.append((b0, 0.9, ch[0] - 12, 82))                          # 쿵
        for k in (1, 2): pizz.append((b0 + k, 0.5, ch[1] + 12, 58)); pizz.append((b0 + k, 0.5, ch[2] + 12, 58))  # 짝짝
        if bar % 4 == 3:
            for k, p in enumerate(ch + [ch[0] + 12]): harp.append((b0 + k * 0.5, 1.5, p + 12, 60))
        if bar >= 16: bsn.append((b0, 2.8, ch[0], 55))
    mel = [("C5", 1), ("F5", 1), ("A5", 1), ("G5", 2), ("E5", 1), ("F5", 1), ("D5", 1), ("A5", 1), ("Bb5", 3),
           ("G5", 1), ("Bb5", 1), ("D6", 1), ("C6", 2), ("A5", 1), ("G5", 1.5), ("F5", 0.5), ("E5", 1), ("F5", 3),
           ("C5", 1), ("F5", 1), ("A5", 1), ("C6", 2), ("A5", 1), ("Bb5", 1), ("A5", 1), ("G5", 1), ("D5", 3),
           ("E5", 1), ("G5", 1), ("Bb5", 1), ("A5", 1.5), ("G5", 0.5), ("E5", 1), ("F5", 3), (None, 3)]
    add_melody(cel, 0, 3, mel, vel=86); add_melody(cel, 16, 3, mel, vel=80)
    counter = [("A5", 3), ("G5", 3), ("F5", 3), ("D5", 3), ("C5", 3), ("D5", 3), ("E5", 3), ("F5", 3)]
    add_melody(glk, 16, 3, counter * 2, vel=50)
    return s

# ---------------- C: 과자 행진 (G장조 4/4 112) ----------------
def song_c():
    s = Song(112, 4, 32)
    glk = s.track(0, 9); clar = s.track(1, 71); tuba = s.track(2, 58); xyl = s.track(3, 13); drum = s.track(9, 0)
    prog = ["G3 B3 D4", "D3 F#3 A3", "E3 G3 B3", "C3 E3 G3", "G3 B3 D4", "C3 E3 G3", "D3 F#3 A3", "G3 B3 D4"]
    for bar in range(32):
        ch = chord_notes(prog[bar % 8]); b0 = bar * 4
        tuba.append((b0, 0.9, ch[0] - 12, 85)); tuba.append((b0 + 2, 0.9, ch[2] - 24, 80))
        for k in (1, 3):
            xyl.append((b0 + k, 0.4, ch[1] + 12, 55)); xyl.append((b0 + k, 0.4, ch[2] + 12, 55))
        for k in range(4): drum.append((b0 + k, 0.2, 42, 45))                     # 하이햇
        drum.append((b0 + 1, 0.2, 38, 50)); drum.append((b0 + 3, 0.2, 38, 50))   # 작은북
        if bar % 4 == 3: drum.extend([(b0 + 3.5, 0.2, 38, 40), (b0 + 3.75, 0.2, 38, 45)])
    mel = [("D5", 0.5), ("G5", 0.5), ("G5", 1), ("A5", 0.5), ("B5", 0.5), ("G5", 1),
           ("F#5", 0.5), ("A5", 0.5), ("D6", 1), ("C6", 0.5), ("B5", 0.5), ("A5", 1),
           ("B5", 0.5), ("G5", 0.5), ("E5", 1), ("G5", 0.5), ("E5", 0.5), ("C5", 1),
           ("D5", 1), ("E5", 0.5), ("F#5", 0.5), ("G5", 2),
           ("D5", 0.5), ("G5", 0.5), ("G5", 1), ("A5", 0.5), ("B5", 0.5), ("G5", 1),
           ("C6", 0.5), ("E6", 0.5), ("D6", 1), ("C6", 0.5), ("B5", 0.5), ("A5", 1),
           ("B5", 1), ("A5", 0.5), ("G5", 0.5), ("A5", 1), ("D5", 1),
           ("G5", 3), (None, 1)]
    add_melody(glk, 0, 4, mel, vel=80); add_melody(clar, 8, 4, mel, vel=75, octave_shift=-1)
    add_melody(glk, 16, 4, mel, vel=78); add_melody(clar, 16, 4, mel, vel=60, octave_shift=-1)
    add_melody(clar, 24, 4, mel, vel=78, octave_shift=-1)
    return s

# ---------------- D: 포근한 오후 (Bb장조 4/4 84) ----------------
def song_d():
    s = Song(84, 4, 32)
    ep = s.track(0, 4); vib = s.track(1, 11); bass = s.track(2, 32); pad = s.track(3, 89); drum = s.track(9, 0)
    prog = ["Bb3 D4 F4 A4", "G3 Bb3 D4 F4", "Eb3 G3 Bb3 D4", "F3 A3 C4 Eb4", "Bb3 D4 F4 A4", "D3 F3 A3 C4", "Eb3 G3 Bb3 D4", "F3 A3 C4 Eb4"]
    for bar in range(32):
        ch = chord_notes(prog[bar % 8]); b0 = bar * 4
        for k, off in enumerate((0, 1.5, 2.5)):
            for p in ch: ep.append((b0 + off, 1.2 if k == 0 else 0.9, p, 58))
        bass.append((b0, 1.4, ch[0] - 12, 78)); bass.append((b0 + 1.5, 0.9, ch[0] - 12, 66)); bass.append((b0 + 3, 0.9, ch[2] - 12, 66))
        if bar >= 8:
            for k in range(4): drum.append((b0 + k + 0.5, 0.2, 42, 30))
            drum.append((b0, 0.2, 36, 50)); drum.append((b0 + 2.5, 0.2, 36, 40)); drum.append((b0 + 1, 0.2, 37, 35)); drum.append((b0 + 3, 0.2, 37, 35))
        if bar >= 16: pad.append((b0, 3.9, ch[1] + 12, 35))
    mel = [("F5", 1.5), ("D5", 0.5), ("C5", 1), ("Bb4", 1), ("D5", 2), (None, 1), ("F5", 1),
           ("G5", 1.5), ("F5", 0.5), ("D5", 1), ("Bb4", 1), ("C5", 3), (None, 1),
           ("Eb5", 1), ("G5", 1), ("Bb5", 1.5), ("A5", 0.5), ("G5", 1), ("F5", 1), ("D5", 2),
           ("C5", 1), ("D5", 1), ("Eb5", 1), ("A4", 1), ("Bb4", 3), (None, 1)]
    add_melody(vib, 8, 4, mel, vel=80); add_melody(vib, 16, 4, mel, vel=76, octave_shift=1); add_melody(vib, 24, 4, mel, vel=80)
    return s

if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "out/lobby"
    os.makedirs(out, exist_ok=True)
    for key, fn in (("A_CookieMorning", song_a), ("B_CandyWaltz", song_b), ("C_SweetMarch", song_c), ("D_CozyAfternoon", song_d)):
        render(fn(), os.path.join(out, f"Lobby_{key}.wav"))
