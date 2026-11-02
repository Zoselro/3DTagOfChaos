# S8 최종 배경음 15개 + 회전목마(AudioProductionPlan.md §7.1·§7.2, 맵 팔레트 §3).
# 반복 곡: A → B → A' 구조. MIDI 작곡 → FluidSynth(FluidR3_GM) 세 바퀴 렌더 → 가운데 바퀴만 잘라 이음매 없는 반복(첫 바퀴 잔향이 이어짐).
# 선율은 화음 진행 위에서 규칙으로 만든다(강박 = 화음음, 약박 = 음계 순차, 4마디 끝 = 긴 화음음). 반복되는 악구는 같은 음을 다시 써 기억에 남게 한다.
import os, subprocess, sys, tempfile, wave
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from audiolib import *

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "final", "BGM")
PC = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6, "G": 7, "G#": 8, "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}
QUAL = {"": (0, 4, 7), "m": (0, 3, 7), "7": (0, 4, 7, 10), "m7": (0, 3, 7, 10), "dim": (0, 3, 6), "maj7": (0, 4, 7, 11)}


def chord(name):
    root = name[:2] if len(name) > 1 and name[1] in "#b" else name[:1]
    q = name[len(root):]
    return PC[root], [(PC[root] + i) % 12 for i in QUAL[q]]


def scale_of(key, minor):
    steps = (0, 2, 3, 5, 7, 8, 10) if minor else (0, 2, 4, 5, 7, 9, 11)
    if minor: steps = (0, 2, 3, 5, 7, 8, 11)  # 화성 단음계(7음 이끔음) — 단조의 신비로움
    return [(PC[key] + s) % 12 for s in steps]


def nearest(pc_set, ref, lo, hi):
    cands = [p for p in range(lo, hi + 1) if p % 12 in pc_set]
    return min(cands, key=lambda p: (abs(p - ref), p))


def step_in(scale, p, d, lo, hi):
    cands = sorted(q for q in range(lo - 2, hi + 3) if q % 12 in scale)
    i = min(range(len(cands)), key=lambda k: abs(cands[k] - p))
    j = max(0, min(len(cands) - 1, i + d)); q = cands[j]
    return max(lo, min(hi, q))


RHYTHMS = {
    4: [[1, 1, 1, 1], [1.5, 0.5, 1, 1], [0.5, 0.5, 1, 2], [1, 0.5, 0.5, 2], [2, 1, 1], [0.5, 0.5, 0.5, 0.5, 1, 1]],
    3: [[1, 1, 1], [2, 1], [1.5, 0.5, 1], [0.5, 0.5, 1, 1], [1, 0.5, 0.5, 1]],
}


def phrase(chords, bpb, scale, seed, lo, hi, start=None):
    """4마디 악구 선율 → [(박, 길이, 음)]"""
    r = np.random.default_rng(seed); ev = []; p = start if start is not None else (lo + hi) // 2
    for b, cname in enumerate(chords):
        _, tones = chord(cname)
        rh = [bpb] if b == len(chords) - 1 else RHYTHMS[bpb][r.integers(len(RHYTHMS[bpb]))]
        beat = 0.0
        for i, d in enumerate(rh):
            strong = beat in (0.0, 2.0) if bpb == 4 else beat == 0.0
            if strong or b == len(chords) - 1: p = nearest(set(tones), p + r.choice([-2, 0, 2, 3]), lo, hi)
            else: p = step_in(scale, p, int(r.choice([-1, 1, 1, -2, 2])), lo, hi)
            ev.append((b * bpb + beat, d, p)); beat += d
    return ev


class Song:
    def __init__(self, bpm, bpb, chords):
        self.bpm, self.bpb, self.chords = bpm, bpb, chords
        self.tracks = []

    @property
    def bars(self): return len(self.chords)

    @property
    def seconds(self): return self.bars * self.bpb * 60 / self.bpm

    def track(self, program, vol=100, pan=64, rev=40, drum=False, bend=0):
        ev = []; self.tracks.append(dict(program=program, vol=vol, pan=pan, rev=rev, drum=drum, bend=bend, ev=ev)); return ev

    def midi(self, repeats):
        mid = mido.MidiFile(ticks_per_beat=480); meta = mido.MidiTrack(); mid.tracks.append(meta)
        meta.append(mido.MetaMessage("set_tempo", tempo=mido.bpm2tempo(self.bpm)))
        beats = self.bars * self.bpb; ch_next = 0
        for tr in self.tracks:
            if tr["drum"]: ch = 9
            else:
                ch = ch_next; ch_next += 1
                if ch_next == 9: ch_next = 10
            t = mido.MidiTrack(); mid.tracks.append(t)
            if not tr["drum"]: t.append(mido.Message("program_change", channel=ch, program=tr["program"], time=0))
            for cc, v in ((7, tr["vol"]), (10, tr["pan"]), (91, tr["rev"]), (93, 0)): t.append(mido.Message("control_change", channel=ch, control=cc, value=v, time=0))
            if tr["bend"]: t.append(mido.Message("pitchwheel", channel=ch, pitch=tr["bend"], time=0))
            msgs = []
            for rpt in range(repeats):
                for beat, dur, pitch, vel in tr["ev"]:
                    t0 = int((beat + rpt * beats) * 480); t1 = int((beat + rpt * beats + dur) * 480) - 2
                    msgs.append((t0, 1, mido.Message("note_on", channel=ch, note=int(pitch), velocity=int(vel))))
                    msgs.append((t1, 0, mido.Message("note_off", channel=ch, note=int(pitch), velocity=0)))
            msgs.sort(key=lambda m: (m[0], m[1])); last = 0
            for tk, _, m in msgs: t.append(m.copy(time=tk - last)); last = tk
        return mid

    def render_loop(self, stereo=True):
        with tempfile.TemporaryDirectory() as d:
            mp, wp = os.path.join(d, "s.mid"), os.path.join(d, "s.wav")
            self.midi(3).save(mp)
            subprocess.run(["fluidsynth", "-ni", "-g", "0.45", "-r", str(SR), "-F", wp, SF2, mp], check=True, capture_output=True)
            with wave.open(wp) as w:
                c = w.getnchannels(); x = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float64) / 32768
        x = x.reshape(-1, c)
        L = int(round(self.seconds * SR)); y = x[L:2 * L]
        return y if stereo else y.mean(axis=1)


# ---------------- 반주 패턴 ----------------

def bass_root(cname, octave=2): return 12 * (octave + 1) + chord(cname)[0]


def voicing(cname, lo=55, size=3):
    _, tones = chord(cname); out = []; p = lo
    while len(out) < size:
        if p % 12 in tones: out.append(p)
        p += 1
    return out


def acc_waltz(song, bass_ev, chord_ev, oct_b=2, lo=57, vb=85, vc=60):
    for b, c in enumerate(song.chords):
        bass_ev.append((b * 3, 0.9, bass_root(c, oct_b), vb))
        for k in (1, 2): chord_ev += [(b * 3 + k, 0.7, p, vc) for p in voicing(c, lo)]


def acc_oompa(song, bass_ev, chord_ev, oct_b=2, lo=55, vb=85, vc=62):
    for b, c in enumerate(song.chords):
        r = bass_root(c, oct_b)
        bass_ev += [(b * 4, 0.9, r, vb), (b * 4 + 2, 0.9, r + 7, vb - 8)]
        for k in (1, 3): chord_ev += [(b * 4 + k, 0.6, p, vc) for p in voicing(c, lo)]


def acc_arp(song, ev, lo=55, step=0.5, vel=60, pattern=(0, 1, 2, 1)):
    for b, c in enumerate(song.chords):
        v = voicing(c, lo, 3) + [voicing(c, lo, 3)[0] + 12]
        for i in range(int(song.bpb / step)): ev.append((b * song.bpb + i * step, step * 1.6, v[pattern[i % len(pattern)] % len(v)], vel))


def acc_pad(song, ev, lo=48, vel=55, size=3):
    for b, c in enumerate(song.chords): ev += [(b * song.bpb, song.bpb, p, vel) for p in voicing(c, lo, size)]


def melody_from(song, scale, plan, lo, hi, seeds):
    """plan: 4마디 묶음마다 악구 이름(같은 이름 = 같은 선율). seeds: 이름 → 시드"""
    ev = []; cache = {}; prev = (lo + hi) // 2
    for i, name in enumerate(plan):
        chs = song.chords[i * 4:(i + 1) * 4]
        key = (name, tuple(chs))
        if key not in cache: cache[key] = phrase(chs, song.bpb, scale, seeds[name], lo, hi, prev)
        for beat, dur, p in cache[key]: ev.append((i * 4 * song.bpb + beat, dur * 0.95, p, 92 if beat % song.bpb == 0 else 80))
        prev = cache[key][-1][2]
    return ev


def octave(ev, k, vel_scale=1.0): return [(b, d, p + 12 * k, int(v * vel_scale)) for b, d, p, v in ev]


def only_bars(ev, bpb, start, end): return [e for e in ev if start * bpb <= e[0] < end * bpb]


# ---------------- 반복 곡 7 ----------------

def song_game_lobby():
    A = ["F", "C7", "F", "Bb", "F", "Dm", "G7", "C7", "F", "C7", "F", "Bb", "F", "C7", "F", "F"]
    B = ["Dm", "A7", "Dm", "Gm", "Bb", "F", "Gm", "A7", "Dm", "A7", "Dm", "Gm", "Bb", "Gm", "A7", "A7"]
    s = Song(92, 3, A + B + A)
    sc = scale_of("F", False)
    mel = melody_from(s, sc, ["a", "b", "a", "c", "d", "e", "d", "f", "a", "b", "a", "c"], 65, 84, dict(a=11, b=12, c=13, d=14, e=15, f=16))
    s.track(GM["celesta"], 110, 64, 50).extend(mel)
    s.track(GM["glock"], 60, 80, 50).extend(octave(only_bars(mel, 3, 32, 48), 1, 0.7))  # A'에서 반짝임 더하기
    bass, chords_ = [], []; acc_waltz(s, bass, chords_, 2, 57)
    s.track(GM["bassoon"], 95, 50, 30).extend(bass)
    s.track(GM["pizz"], 80, 76, 35).extend(chords_)
    # B 구간: 괴물 모티프의 그림자(낮은 반음 B♭→A)가 한 번 지나간다
    s.track(GM["contrabass"], 85, 64, 30).extend([(28 * 3, 3, n("Bb1"), 70), (29 * 3, 6, n("A1"), 75)])
    return s


def song_monster_wait():
    A = ["Dm", "Dm", "Bb", "A", "Dm", "Dm", "Gm", "A"]
    B = ["Bb", "Bb", "A", "A", "Gm", "Gm", "A7", "A7"]
    s = Song(80, 4, A + B + A)
    pad = []; acc_pad(s, pad, 50, 60, 3)
    s.track(GM["trem_strings"], 95, 64, 50).extend(pad)
    motif = []
    for b in range(0, 24, 2): motif += [(b * 4, 3.8, n("Bb1"), 85), (b * 4 + 4, 3.8, n("A1"), 90)]
    s.track(GM["contrabass"], 110, 64, 30).extend(motif)
    s.track(GM["timpani"], 90, 64, 30).extend([(b * 4 + k, 0.5, n("D2") if k == 0 else n("A1"), 90 if k == 0 else 70) for b in range(24) for k in (0, 2)])
    s.track(GM["choir"], 70, 64, 60).extend([e for e in [(b * 4, 4, p, 50) for b in range(8, 16) for p in voicing(s.chords[b], 48, 2)]])
    mel = melody_from(s, scale_of("D", True), ["x", "y", "z", "w", "x", "y"], 62, 74, dict(x=21, y=22, z=23, w=24))
    s.track(GM["oboe"], 75, 70, 50).extend(only_bars(mel, 4, 16, 24))
    return s


def song_candy():
    A = ["C", "Am", "F", "G", "C", "Am", "Dm", "G", "C", "Am", "F", "G", "F", "G", "C", "C"]
    B = ["F", "G", "Em", "Am", "Dm", "G", "C", "G"]
    s = Song(104, 4, A + B + A)
    mel = melody_from(s, scale_of("C", False), ["a", "b", "a", "c", "d", "e", "a", "b", "a", "c"], 67, 86, dict(a=31, b=32, c=33, d=34, e=35))
    s.track(GM["marimba"], 110, 64, 40).extend(mel)
    s.track(GM["celesta"], 55, 84, 50).extend(octave(only_bars(mel, 4, 24, 40), 1, 0.7))
    fl = [(b * 4, 4, voicing(c, 72, 2)[1], 60) for b, c in enumerate(s.chords) if b % 2 == 1]
    s.track(GM["flute"], 70, 44, 55).extend(fl)
    harp = []; acc_arp(s, harp, 55, 0.5, 55, (0, 1, 2, 3, 2, 1, 0, 1))
    s.track(GM["harp"], 80, 70, 45).extend(harp)
    s.track(GM["pizz"], 85, 60, 30).extend([(b * 4 + k, 0.8, bass_root(c, 2) + (7 if k == 2 else 0), 80) for b, c in enumerate(s.chords) for k in (0, 2)])
    s.track(GM["contrabass"], 55, 64, 50).extend([(16 * 4, 16, n("E1"), 45), (20 * 4, 16, n("G1"), 45)])  # B: 멀리서 아주 약한 저음
    return s


def song_gingerbread():
    A = ["F", "C", "F", "Bb", "F", "Gm", "C7", "F", "F", "C", "F", "Bb", "Bb", "C7", "F", "F"]
    B = ["Dm", "A", "Dm", "Gm", "Bb", "F", "C", "C7"]
    s = Song(96, 4, A + B + A)
    mel = melody_from(s, scale_of("F", False), ["a", "b", "a", "c", "d", "e", "a", "b", "a", "c"], 72, 89, dict(a=41, b=42, c=43, d=44, e=45))
    s.track(GM["glock"], 100, 64, 45).extend(mel)
    s.track(GM["musicbox"], 70, 84, 55).extend(only_bars(octave(mel, -1, 0.8), 4, 24, 40))
    bass, ch = [], []; acc_oompa(s, bass, ch, 2, 57)
    s.track(GM["accordion"], 85, 54, 35).extend(ch)
    s.track(GM["pizz"], 90, 70, 30).extend(bass)
    s.track(0, 70, 64, 20, drum=True).extend([(b * 4 + k, 0.2, 76 if k % 2 == 0 else 77, 70 if k % 2 == 0 else 55) for b in range(s.bars) for k in range(4)])  # 시계 똑딱
    return s


def song_factory():
    A = ["Dm", "Dm", "Bb", "A", "Dm", "Dm", "Gm", "A", "Dm", "Dm", "Bb", "A", "Gm", "A", "Dm", "Dm"]
    B = ["Gm", "Gm", "Dm", "Dm", "Bb", "Bb", "A", "A", "Gm", "Gm", "Dm", "Dm", "Bb", "Gm", "A", "A"]
    s = Song(112, 4, A + B + A)
    mel = melody_from(s, scale_of("D", True), ["a", "b", "a", "c", "d", "e", "d", "f", "a", "b", "a", "c"], 62, 79, dict(a=51, b=52, c=53, d=54, e=55, f=56))
    s.track(GM["clarinet"], 105, 60, 35).extend(mel)
    s.track(GM["xylo"], 70, 80, 35).extend(octave(only_bars(mel, 4, 16, 32), 1, 0.75))
    s.track(GM["bassoon"], 95, 64, 25).extend([(b * 4 + i * 0.5, 0.4, bass_root(c, 2) + (12 if i % 4 == 2 else 0) + (7 if i % 8 == 5 else 0), 85) for b, c in enumerate(s.chords) for i in range(8)])
    s.track(0, 80, 64, 20, drum=True).extend([(b * 4 + i * 0.5, 0.15, 76 if i % 2 == 0 else 77, 90 if i % 4 == 0 else 60) for b in range(s.bars) for i in range(8)])
    s.track(GM["timpani"], 90, 64, 30).extend([(b * 4, 0.8, bass_root(c, 2), 95) for b, c in enumerate(s.chords)])
    return s


def song_carnival(bars_a=None):
    A = ["Am", "E", "Am", "Dm", "Am", "E", "Am", "E", "Am", "E", "Am", "Dm", "F", "E", "Am", "Am"]
    B = ["F", "C", "Dm", "E", "F", "C", "Dm", "E7", "F", "C", "Dm", "Am", "Dm", "Am", "E7", "E7"]
    s = Song(96, 3, A + B + A)
    mel = melody_from(s, scale_of("A", True), ["a", "b", "a", "c", "d", "e", "d", "f", "a", "b", "a", "c"], 69, 88, dict(a=61, b=62, c=63, d=64, e=65, f=66))
    s.track(GM["musicbox"], 110, 64, 55).extend(mel)
    s.track(GM["calliope"], 55, 80, 45).extend(octave(mel, -1, 0.7))
    bass, ch = [], []; acc_waltz(s, bass, ch, 2, 57)
    s.track(GM["piano"], 85, 50, 40).extend(bass + ch)
    s.track(GM["piano"], 70, 78, 40, bend=600).extend(bass + ch)  # 같은 반주를 살짝 높게 디튠(+18 cent) — 고장 난 피아노
    return s


def song_bakery():
    A = ["Em", "C", "Am", "B", "Em", "C", "D", "B", "Em", "C", "Am", "B", "C", "B", "Em", "Em"]
    B = ["C", "D", "Bm", "Em", "Am", "B", "Em", "B"]
    s = Song(88, 4, A + B + A)
    mel = melody_from(s, scale_of("E", True), ["a", "b", "a", "c", "d", "e", "a", "b", "a", "c"], 71, 88, dict(a=71, b=72, c=73, d=74, e=75))
    s.track(GM["celesta"], 105, 64, 55).extend(mel)
    hc = []; acc_arp(s, hc, 52, 0.5, 60, (0, 1, 2, 3, 2, 1, 2, 1))
    s.track(GM["harpsichord"], 75, 50, 40).extend(hc)
    pad = []; acc_pad(s, pad, 40, 55, 2)
    s.track(GM["strings"], 75, 64, 50).extend(pad)
    s.track(GM["choir"], 60, 70, 70).extend([(b * 4, 4, p, 45) for b in range(16, 24) for p in voicing(s.chords[b], 55, 2)])
    s.track(GM["contrabass"], 80, 64, 30).extend([(b * 4, 3.8, bass_root(c, 1), 70) for b, c in enumerate(s.chords)])
    return s


def carousel_spin():
    """회전목마(3D, 모노): A단조 왈츠 16마디(30초) + 도는 기계 덜컹(매 마디 첫 박, 같은 길이 안에서 순환)"""
    chords_ = ["Am", "E", "Am", "Dm", "Am", "E", "Am", "E", "F", "C", "Dm", "E", "Am", "E", "Am", "Am"]
    s = Song(96, 3, chords_)
    mel = melody_from(s, scale_of("A", True), ["a", "b", "c", "a"], 69, 86, dict(a=61, b=62, c=65))  # CarnivalPaint와 같은 주제(a, b)
    s.track(GM["calliope"], 100, 64, 50).extend(mel)
    s.track(GM["musicbox"], 80, 64, 50).extend(octave(mel, 1, 0.8))
    bass, ch = [], []; acc_waltz(s, bass, ch, 2, 57)
    s.track(GM["piano"], 80, 64, 40, bend=500).extend(bass + ch)
    x = s.render_loop(stereo=False)
    bar = 3 * 60 / 96; total = len(x) / SR
    clank = place(total, [(b * bar + 0.01, mix((0, 0.3 * tin_metal(500, 0.2, seed=1100 + b % 3)), (0, 0.4 * low_thud(90, 0.15, 0.2)))) for b in range(16)])
    whirr = 0.05 * lp(noise(total, 1110), 300)
    return x / (np.max(np.abs(x)) + 1e-9) * 0.7 + clank + whirr


LOOPS = {
    "GameLobby": song_game_lobby, "MonsterWait": song_monster_wait, "CandyForestPaint": song_candy, "GingerbreadPaint": song_gingerbread,
    "FactoryPaint": song_factory, "CarnivalPaint": song_carnival, "BakeryPaint": song_bakery,
}


# ---------------- 스팅어 5 · 징글 3 (모노) ----------------

def magic_sig(minor=False, detune=0):
    bell = reverse(inst([(0, 1.0, "C6", 100)], GM["tubular"]))[-int(0.45 * SR):]
    t3, t7 = ("Eb6", "Bb6") if minor else ("E6", "B6")
    cel = inst(notes_seq(["C6", t3, "G6", t7], 0.09, dur=0.5, vel=90), GM["celesta"])
    if detune: cel = detune_layer(cel, detune)
    return mix((0, bell), (0.42, cel), (0.5, 0.25 * sparkle(0.6, 10, base=96, seed=5)))


def stingers():
    out = {}
    rev_cym = inst([(0, 1.6, 49, 110)], 0, drum=True)
    out["StMonsterReveal"] = (mix((0, 0.5 * reverse(rev_cym)[-int(0.75 * SR):]), (0.75, inst([(0, 0.7, "Bb2", 115), (0.75, 1.4, "A2", 120)], GM["trombone"])),
                                  (0.75, 0.8 * inst([(0, 0.7, "Bb1", 110), (0.75, 1.4, "A1", 115)], GM["tuba"])), (0.75, low_thud(45, 1.0, 0.3)),
                                  (0, 0.5 * sub_drone(2.6, 36) * shape(2.6, 0.6, 0.8)), (1.6, 0.3 * detune_layer(inst([(0, 0.8, "F#6", 80)], GM["celesta"]), 35))), 2.6)
    out["StMonsterArrive"] = (mix((0, 0.6 * reverse(whoosh(0.6, 300, 2500, seed=1201))), (0.55, inst([(0, 2.2, p, 105) for p in ("D3", "F3", "A3", "D4")], GM["organ"])),
                                  (0.55, 1.1 * low_thud(40, 1.2, 0.35)), (0.55, inst([(0, 0.3, "D2", 120), (0.5, 0.3, "D2", 100), (1.0, 0.6, "A1", 120)], GM["taiko"])),
                                  (0.7, 0.5 * growl(0.7, 85, seed=1202))), 3.0)
    out["StDeviceComplete"] = (mix((0, magic_sig()), (0.4, inst(notes_seq(["C5", "E5", "G5"], 0.1, dur=0.5, vel=100), GM["marimba"])),
                                   (0.75, 0.6 * inst([(0, 1.2, p, 90) for p in ("C6", "E6", "G6", "C7")], GM["glock"]))), 2.2)
    rise = mix(*[(0.7 * i, inst([(0, 0.8, n(p), 80 + 10 * i) for p in ps], GM["trem_strings"])) for i, ps in enumerate((("A3", "C4", "E4"), ("Bb3", "D4", "F4"), ("B3", "D#4", "F#4"), ("C4", "E4", "G4")))])
    roll = inst([(i * 0.06, 0.05, 38, int(40 + 70 * i / 45)) for i in range(45)], 0, drum=True)
    out["StSpyLaunch"] = (mix((0, rise), (0, 0.6 * roll), (2.3, 0.3 * whoosh(0.7, 800, 4000, seed=1203))), 3.0)
    out["StWitchAppear"] = (mix((0, 1.1 * wind(3.5, 700, 0.3, seed=1204) * shape(3.5, 1.2, 0.8)), (0.2, inst([(0, 3.0, p, 100) for p in ("A2", "A3", "C4", "Eb4")], GM["choir"])),
                                (0, 0.6 * sub_drone(3.5, 33) * shape(3.5, 0.8, 0.8)), (1.0, 0.5 * magic_sig(minor=True, detune=30))), 3.5)
    out["JgEscapeSuccess"] = (mix((0, inst(notes_seq(["C5", "E5", "G5", "A5", "G5"], 0.15, vel=105), GM["marimba"])),
                                  (0, 0.5 * inst(notes_seq(["C6", "E6", "G6", "A6", "G6"], 0.15, vel=80), GM["celesta"])),
                                  (0.85, inst([(0, 2.5, p, 95) for p in ("C4", "E4", "G4", "C5", "E5")], GM["harp"])),
                                  (0.85, 0.7 * inst([(0, 2.5, p, 90) for p in ("C6", "E6", "G6")], GM["glock"])), (1.0, 0.35 * sparkle(1.5, 20, base=96, seed=1205))), 5.0)
    out["JgEscapeFail"] = (mix((0, inst(notes_seq(["C5", "Eb5", "G5", "Ab5", "G5"], 0.32, vel=85), GM["celesta"])),
                               (0, 0.6 * inst(notes_seq(["C3", "C3", "Eb3", "F3", "G2"], 0.32, vel=80), GM["bassoon"])),
                               (1.7, 0.6 * inst([(0, 2.0, p, 70) for p in ("C4", "Eb4", "G4")], GM["strings"])), (3.4, 0.6 * pop(700, 250, 0.25))), 5.0)
    laugh = mix(*[(0.16 * k, inst([(0, 0.12, n("Bb3") - k, 100)], GM["bassoon"])) for k in range(6)])
    out["JgMonsterWin"] = (mix((0, inst([(0, 0.9, "Bb2", 115), (1.0, 1.6, "A2", 120)], GM["trombone"])), (0, 0.8 * inst([(0, 0.9, "Bb1", 110), (1.0, 1.6, "A1", 115)], GM["tuba"])),
                               (1.0, low_thud(45, 1.2, 0.3)), (1.0, 0.5 * inst([(0, 2.0, 49, 100)], 0, drum=True)), (2.4, laugh), (2.6, 0.4 * growl(1.2, 90, seed=1206))), 5.0)
    return out


def build():
    made = []
    for name, fn in LOOPS.items():
        s = fn(); y = s.render_loop(stereo=True)
        y = norm_lufs(y, -18.0)
        write_ogg(os.path.join(ROOT, name + ".ogg"), y, -1.0)
        seam = float(np.abs(y[-1] - y[0]).max()); step = float(np.percentile(np.abs(np.diff(y, axis=0)), 99))
        made.append((name, len(y) / SR, s.bars, peak_db(y), lufs(y), seam, step))
    for name, (x, length) in stingers().items():
        x = cut(reverb(x, 1.6, 0.18), length, 0.5 if name.startswith("Jg") else 0.35)
        x = norm_lufs(x, -15.0 if name.startswith("St") else -16.0)
        write_ogg(os.path.join(ROOT, name + ".ogg"), x, -1.0)
        made.append((name, len(x) / SR, 0, peak_db(x), lufs(x), 0, 0))
    return made


if __name__ == "__main__":
    for name, sec, bars, pk, lu, seam, step in build():
        print(f"{name:18s} {sec:6.1f}s  bars {bars:2d}  peak {pk:5.1f} dB  {lu:6.1f} LUFS" + (f"  seam {seam:.4f} (p99 step {step:.4f})" if bars else ""))
