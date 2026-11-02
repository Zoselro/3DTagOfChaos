# V5 뒤틀린 과자 동화 소리(TwistedCandyPlan.md §5.1·§5.2·§9). 캔디숲 견본(twisted_candy_sample.py) 방향 그대로 나머지를 다시 만든다.
# 출력: out/twisted/final/<게임 경로>(BGM/<ID>.ogg, SFX/<폴더>/<ID>.wav|ogg) — S8과 **같은 ID·같은 파일 이름·같은 확장자**라
# 게임에서는 파일만 바뀌고 카탈로그·코드는 그대로다(§5.3). 음량 기준도 S8·S9와 같다(배경음 −18 LUFS 파일, 스팅어 −15, 징글 −16,
# 효과음 피크 같음). 바탕 환경음만 §5.3대로 조금 크게(−24 → −22 LUFS).
#
# 반복 곡: MIDI 세 바퀴 렌더(final_music.Song) → 가운데 바퀴를 잘라 이음매 없는 반복 → 같은 바퀴를 세 번 이어 붙여 "주기적인" 처리
# (테이프 흔들림은 한 바퀴에 정수 번, 늘어짐은 바퀴마다 같은 자리, 잔향은 시불변) 후 다시 가운데만 자른다 → 처리 뒤에도 이음매가 없다.
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from scipy import signal
from audiolib import *
import final_music as fm

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, "out", "twisted", "final")
made = []


# ======================================================================================
# 공용
# ======================================================================================
def slowdown(x, start, length, depth=0.12):
    """테이프가 늘어지듯 잠깐 느려졌다 돌아온다(길이는 그대로, 음높이도 같이 내려감)"""
    s, n_ = int(start * SR), int(length * SR)
    seg = x[s:s + n_]
    if len(seg) < 2: return x
    rate = 1.0 - depth * np.sin(np.linspace(0, np.pi, len(seg))) ** 2
    pos = np.cumsum(rate); pos = pos / pos[-1] * (len(seg) - 1)
    out = x.copy(); out[s:s + len(seg)] = np.interp(pos, np.arange(len(seg)), seg)
    return out


def dropout(x, start, length, floor=0.15):
    """전기가 잠깐 나간 듯 소리가 꺼졌다 돌아온다"""
    s, n_ = int(start * SR), int(length * SR)
    g = np.ones(len(x)); k = np.linspace(0, np.pi, n_)
    g[s:s + n_] = 1 - (1 - floor) * np.sin(k) ** 0.5
    return x * g


def limiter(x, ceil_db=-1.0, lookahead=0.004, release=0.12):
    """부드러운 피크 제한(앞을 내다보는 최소값 + 이동 평균 회복) — 저역 쿵 때문에 크기를 통째로 낮추지 않게"""
    from scipy.ndimage import minimum_filter1d, uniform_filter1d
    c = 10 ** (ceil_db / 20)
    a = np.abs(x) if x.ndim == 1 else np.abs(x).max(axis=1)
    g = np.minimum(1.0, c / (a + 1e-12))
    g = minimum_filter1d(g, size=2 * int(lookahead * SR) + 1)
    g = minimum_filter1d(g, size=int(release * SR), origin=-(int(release * SR) // 2) + 1)
    g = np.minimum(g, uniform_filter1d(g, size=int(lookahead * SR) * 2 + 1))
    return x * (g if x.ndim == 1 else g[:, None])


def loud(x, target, ceil_db=-1.0, loop=False):
    """LUFS 목표에 맞추고 피크는 리미터로 누른다(두 번 — 누른 만큼 다시 맞춤). loop면 세 바퀴로 처리해 가운데만 — 이음매가 생기지 않게"""
    L = len(x)
    if loop: x = np.concatenate([x, x, x])
    x = hp(x, 28) if x.ndim == 1 else np.stack([hp(x[:, 0], 28), hp(x[:, 1], 28)], axis=1)
    for _ in range(2):
        x = limiter(norm_lufs(x, target), ceil_db)
    return x[L:2 * L] if loop else x


def stereo_verb(x, seconds, wet, bright):
    out = []
    for seed in (11, 23):
        y = signal.fftconvolve(x, ir(seconds, bright, seed))[:len(x)]
        out.append(x * (1 - wet) + y * wet * 3)
    return np.stack(out, axis=1)


def periodic(lap, fx):
    """한 바퀴(lap)를 세 번 이어 fx를 적용하고 가운데만 — fx가 바퀴마다 같으면 이음매 없음"""
    L = len(lap)
    y = fx(np.concatenate([lap, lap, lap]), L)
    return y[L:2 * L]


def bed_lap(seconds, make):
    """바퀴 길이의 이음매 없는 배경 질감(바람 등)"""
    return loopify(make(seconds + 1.0), seconds, 1.0)


def finish_bgm(name, song, wobble=(0.006, 2), slows=(), drops=(), cut_hz=2600, verb=(2.2, 0.32, 4000), bed=None, extra=None, lufs_target=-18.0):
    lap = song.render_loop(stereo=False)
    L = len(lap); sec = L / SR
    if extra is not None: lap = lap + extra(sec)

    def fx(x3, L_):
        y = tape_wobble(x3, wobble[0], wobble[1] / sec)
        for k in range(3):
            for st, ln, dp in slows: y = slowdown(y, k * sec + st, ln, dp)
            for st, ln, fl in drops: y = dropout(y, k * sec + st, ln, fl)
        y = lp(y, cut_hz)
        if bed is not None: y = y + np.tile(bed_lap(sec, bed), 3)[:len(y)]
        return stereo_verb(y, *verb)

    y = periodic(lap, fx)
    y = loud(y, lufs_target, loop=True)
    write_ogg(os.path.join(ROOT, "BGM", name + ".ogg"), y, -1.0)
    seam = float(np.abs(y[-1] - y[0]).max()); step = float(np.percentile(np.abs(np.diff(y, axis=0)), 99))
    made.append(("BGM", name, sec, peak_db(y), lufs(y), f"seam {seam:.4f} p99 {step:.4f}"))


def wind_bed(cut=420, amt=0.18, drone=38):
    return lambda s: amt * wind(s, cut, 0.07, seed=5) + 0.05 * sub_drone(s, drone, 0.05)


def line_ev(line, start=0.0, vel=78, legato=0.95):
    ev, b = [], start
    for p, d in line:
        if p is not None: ev.append((b, d * legato, n(p) if isinstance(p, str) else p, vel))
        b += d
    return ev


# ======================================================================================
# 배경음 7 (반복)
# ======================================================================================
def bgm_candy():
    """★ 캔디숲: 견본 그대로의 고장 난 오르골 자장가를 A–B–A'(36마디, 90초)로"""
    A = ["Am", "F", "Dm", "E", "Am", "F", "Dm", "E", "Am", "F", "E", "Am"]
    B = ["Dm", "Am", "Dm", "E", "F", "C", "Dm", "E", "F", "Dm", "E", "E"]
    s = fm.Song(72, 3, A + B + A)
    a_line = [("E5", 2), ("C5", 1), ("A4", 2), ("C5", 1), ("D5", 1), ("E5", 1), ("F5", 1), ("E5", 3),
              ("E5", 2), ("C5", 1), ("A4", 1), ("G4", 1), ("F4", 1), ("D4", 1), ("F4", 1), ("A4", 1), ("G#4", 3),
              ("A4", 2), ("C5", 1), ("B4", 1), ("A4", 1), ("F4", 1), ("E4", 2), ("G#4", 1), ("A4", 3)]
    b_line = [("F5", 3), ("E5", 2), ("C5", 1), ("D5", 2), ("F5", 1), ("E5", 3), ("C5", 2), ("A4", 1), ("G4", 3),
              ("A4", 2), ("B4", 1), ("G#4", 3), ("A4", 2), ("F4", 1), ("E4", 3), (None, 3), ("E4", 1), ("G#4", 1), ("B4", 1)]
    mel = line_ev(a_line) + line_ev(b_line, 36, 70) + line_ev(a_line, 72)
    s.track(GM["musicbox"], 105, 64, 70).extend(mel)
    s.track(GM["musicbox"], 70, 80, 70, bend=700).extend([(b, d, p, int(v * (0.8 if b >= 72 else 0.6))) for b, d, p, v in mel])  # 틀어진 두 번째 오르골(A'에서 더 크게)
    s.track(GM["celesta"], 55, 50, 80).extend([(b * 3, 2.5, fm.bass_root(c, 3) + 12, 45) for b, c in enumerate(s.chords) if b % 2 == 0])
    pad = []; fm.acc_pad(s, pad, 45, 42, 2)
    s.track(GM["strings"], 95, 64, 70).extend(pad)
    s.track(GM["strings"], 70, 64, 70).extend([(b, d, p - 12, 50) for b, d, p, v in line_ev(b_line, 36)])  # B: 낮은 현이 선율을 받음
    s.track(GM["contrabass"], 85, 64, 50).extend([(b * 3, 2.9, fm.bass_root(c, 1), 55) for b, c in enumerate(s.chords)])
    finish_bgm("CandyForestPaint", s, wobble=(0.006, 3), slows=((20.5, 3.0, 0.14), (80.5, 2.5, 0.18)), cut_hz=2600, bed=wind_bed())


def bgm_gingerbread():
    """★★ 진저브레드: 멈췄다 가는 시계 오르골(D단조 4/4 76, 32마디). 똑딱이 가끔 빠지고, 오르골은 태엽이 풀리듯 늘어진다"""
    A = ["Dm", "Dm", "Gm", "A", "Dm", "Bb", "Gm", "A", "Dm", "Dm", "Gm", "A", "Bb", "Gm", "A", "Dm"]
    B = ["Bb", "F", "Gm", "Dm", "Bb", "Gm", "A", "A"]
    s = fm.Song(76, 4, A + B + A[:8])
    mel = fm.melody_from(s, fm.scale_of("D", True), ["a", "b", "a", "c", "d", "e", "a", "b"], 69, 86, dict(a=401, b=402, c=403, d=404, e=405))
    s.track(GM["musicbox"], 105, 64, 65).extend(mel)
    s.track(GM["glock"], 50, 84, 60, bend=-600).extend(fm.octave(fm.only_bars(mel, 4, 16, 24), 0, 0.6))  # B: 틀어진 종이 따라 함
    pizz = [(b * 4 + k, 0.6, fm.bass_root(c, 2) + (7 if k == 2 else 0), 70) for b, c in enumerate(s.chords) for k in (0, 2)]
    s.track(GM["pizz"], 85, 56, 40).extend(pizz)
    pad = []; fm.acc_pad(s, pad, 48, 40, 2)
    s.track(GM["strings"], 80, 64, 70).extend(pad)
    skip = {(5, 2), (11, 1), (11, 2), (19, 3), (26, 0), (26, 1)}
    s.track(0, 55, 64, 30, drum=True).extend([(b * 4 + k, 0.15, 76 if k % 2 == 0 else 77, 55 if k % 2 == 0 else 42) for b in range(s.bars) for k in range(4) if (b, k) not in skip])
    s.track(GM["contrabass"], 70, 64, 40).extend([(b * 4, 3.9, fm.bass_root(c, 1), 50) for b, c in enumerate(s.chords) if b >= 16 and b < 24])
    finish_bgm("GingerbreadPaint", s, wobble=(0.007, 4), slows=((27.0, 2.6, 0.16), (70.0, 3.2, 0.22)), cut_hz=2800, bed=wind_bed(450, 0.16, 36))


def bgm_factory():
    """★★ 초콜릿 공장: 쉬지 않는 기계 박자 위에서 지친 오르골(D 프리지안 4/4 96, 32마디). 낮은 금관 숨소리"""
    A = ["Dm", "Eb", "Dm", "Dm", "Gm", "Eb", "Dm", "A", "Dm", "Eb", "Dm", "Dm", "Bb", "Eb", "A", "A"]
    s = fm.Song(96, 4, A + A)
    sc = [2, 3, 5, 7, 9, 10, 0]  # D 프리지안
    mel = fm.melody_from(s, sc, ["a", "b", "a", "c", "d", "b", "d", "c"], 64, 79, dict(a=501, b=502, c=503, d=504))
    s.track(GM["musicbox"], 95, 60, 50).extend(mel)
    s.track(GM["clarinet"], 60, 70, 40).extend(fm.octave(fm.only_bars(mel, 4, 16, 32), -1, 0.7))
    s.track(GM["bassoon"], 90, 64, 25).extend([(b * 4 + i * 0.5, 0.35, fm.bass_root(c, 2) + (12 if i % 4 == 2 else 0), 80) for b, c in enumerate(s.chords) for i in range(8)])
    s.track(GM["tuba"], 70, 64, 30).extend([(b * 4, 3.5, fm.bass_root(c, 1), 60) for b, c in enumerate(s.chords) if b % 2 == 0])
    s.track(0, 70, 64, 20, drum=True).extend([(b * 4 + i * 0.5, 0.12, 75 if i % 2 == 0 else 76, 70 if i % 4 == 0 else 45) for b in range(s.bars) for i in range(8)])

    def machine(sec):
        hiss = place(sec, [(0.25 + 2.5 * i, 0.35 * steam(0.7, seed=520 + i % 7)) for i in range(int(sec / 2.5))])
        clank = place(sec, [(i * 60 / 96 * 4, 0.25 * mix((0, tin_metal(420, 0.25, seed=530)), (0, low_thud(70, 0.25, 0.2)))) for i in range(int(sec / (60 / 96 * 4)))])
        return 0.6 * (hiss + clank)
    finish_bgm("FactoryPaint", s, wobble=(0.005, 5), slows=((35.0, 2.0, 0.12),), cut_hz=3000, verb=(1.8, 0.28, 3500),
               bed=wind_bed(300, 0.12, 44), extra=machine)


def bgm_carnival():
    """★★★ 놀이공원: 거의 부서진 칼리오페 왈츠(A단조 3/4 92, 48마디). 크게 틀어지고, 자꾸 늘어지고, 전기가 나간 듯 끊긴다"""
    A = ["Am", "E", "Am", "Dm", "Am", "E", "Am", "E", "Am", "E", "Am", "Dm", "F", "E", "Am", "Am"]
    B = ["F", "C", "Dm", "E", "F", "C", "Dm", "E7", "F", "C", "Dm", "Am", "Dm", "Am", "E7", "E7"]
    s = fm.Song(92, 3, A + B + A)
    mel = fm.melody_from(s, fm.scale_of("A", True), ["a", "b", "a", "c", "d", "e", "d", "f", "a", "b", "a", "c"], 69, 88, dict(a=61, b=62, c=63, d=64, e=65, f=66))  # S8 주제 그대로(기억)
    s.track(GM["calliope"], 90, 64, 55).extend(mel)
    s.track(GM["calliope"], 70, 80, 55, bend=900).extend([(b, d, p, int(v * 0.7)) for b, d, p, v in mel])       # 크게 틀어진 두 번째 관
    s.track(GM["musicbox"], 70, 44, 60, bend=-700).extend(fm.octave(fm.only_bars(mel, 3, 32, 48), 0, 0.7))
    bass, ch = [], []; fm.acc_waltz(s, bass, ch, 2, 57, 80, 50)
    s.track(GM["piano"], 80, 50, 40, bend=600).extend(bass + ch)
    s.track(GM["contrabass"], 60, 64, 40).extend([(16 * 3 + b * 3, 2.9, fm.bass_root(c, 1), 45) for b, c in enumerate(B)])
    finish_bgm("CarnivalPaint", s, wobble=(0.012, 6), slows=((14.0, 2.4, 0.2), (41.0, 3.0, 0.26), (70.0, 2.2, 0.3)),
               drops=((28.5, 0.6, 0.05), (56.0, 0.9, 0.1), (83.0, 0.5, 0.08)), cut_hz=2200, verb=(2.6, 0.38, 3500), bed=wind_bed(500, 0.2, 34))


def bgm_bakery():
    """★★ 유령 베이커리: 차가운 유령 자장가(E단조 4/4 66, 24마디). 온음 음계 셀레스타·사람 없는 합창·틀어진 하프시코드"""
    A = ["Em", "C", "Am", "B", "Em", "C", "D", "B", "Em", "C", "Am", "B"]
    B = ["C", "D", "Bm", "Em", "Am", "B", "Em", "B", "C", "Am", "B", "B"]
    s = fm.Song(66, 4, A + B)
    mel = fm.melody_from(s, fm.scale_of("E", True), ["a", "b", "c", "a", "d", "e"], 71, 88, dict(a=71, b=72, c=73, d=74, e=75))
    s.track(GM["celesta"], 100, 64, 70).extend(mel)
    s.track(GM["musicbox"], 55, 80, 70, bend=600).extend(fm.octave(mel, 0, 0.6))
    wt = [n(p) for p in ("E5", "F#5", "G#5", "A#5", "C6", "D6")]  # 온음 음계 반짝임(불안)
    s.track(GM["celesta"], 45, 30, 80).extend([(b * 4 + 2.5, 1.2, wt[(b * 3) % 6], 40) for b in range(s.bars) if b % 3 == 1])
    hc = []; fm.acc_arp(s, hc, 52, 1.0, 50, (0, 1, 2, 1))
    s.track(GM["harpsichord"], 60, 50, 50, bend=-500).extend(hc)
    s.track(GM["oohs"], 75, 64, 80).extend([(b * 4, 4, p, 45) for b in range(s.bars) for p in fm.voicing(s.chords[b], 52, 2)])
    s.track(GM["contrabass"], 75, 64, 40).extend([(b * 4, 3.8, fm.bass_root(c, 1), 55) for b, c in enumerate(s.chords)])
    finish_bgm("BakeryPaint", s, wobble=(0.006, 3), slows=((40.0, 3.5, 0.15),), cut_hz=2600, verb=(3.0, 0.4, 3000), bed=wind_bed(320, 0.15, 36))


def bgm_game_lobby():
    """★★ 대기실(마녀 과자집 앞): 느린 단조 오르골 왈츠(D단조 3/4 84, 48마디), 지하에서 괴물 모티프(B♭→A)가 지나간다"""
    A = ["Dm", "A", "Dm", "Gm", "Dm", "Bb", "Gm", "A", "Dm", "A", "Dm", "Gm", "Bb", "A", "Dm", "Dm"]
    B = ["Gm", "Dm", "Gm", "A", "Bb", "F", "Gm", "A", "Gm", "Dm", "Bb", "Gm", "Gm", "A", "A", "A"]
    s = fm.Song(84, 3, A + B + A)
    mel = fm.melody_from(s, fm.scale_of("D", True), ["a", "b", "a", "c", "d", "e", "d", "f", "a", "b", "a", "c"], 65, 84, dict(a=11, b=12, c=13, d=14, e=15, f=16))  # S8 선율 시드(기억)
    s.track(GM["musicbox"], 105, 64, 65).extend(mel)
    s.track(GM["celesta"], 55, 84, 65, bend=600).extend(fm.octave(fm.only_bars(mel, 3, 32, 48), 0, 0.7))
    bass, ch = [], []; fm.acc_waltz(s, bass, ch, 2, 57, 75, 45)
    s.track(GM["bassoon"], 85, 50, 35).extend(bass)
    s.track(GM["pizz"], 70, 76, 40).extend(ch)
    s.track(GM["contrabass"], 95, 64, 30).extend([(28 * 3, 3, n("Bb1"), 75), (29 * 3, 6, n("A1"), 80), (44 * 3, 3, n("Bb1"), 70), (45 * 3, 6, n("A1"), 75)])
    finish_bgm("GameLobby", s, wobble=(0.006, 4), slows=((60.0, 2.6, 0.15),), cut_hz=2800, bed=wind_bed(400, 0.15, 36))


def bgm_monster_wait():
    """괴물 대기: 무거운 저음 모티프(B♭→A) + 떨리는 현 + 아주 멀리서 망가진 오르골 조각(D단조 4/4 70, 24마디)"""
    A = ["Dm", "Dm", "Bb", "A", "Dm", "Dm", "Gm", "A"]
    B = ["Bb", "Bb", "A", "A", "Gm", "Gm", "A7", "A7"]
    s = fm.Song(70, 4, A + B + A)
    pad = []; fm.acc_pad(s, pad, 48, 55, 3)
    s.track(GM["trem_strings"], 90, 64, 55).extend(pad)
    motif = []
    for b in range(0, 24, 2): motif += [(b * 4, 3.8, n("Bb1"), 90), (b * 4 + 4, 3.8, n("A1"), 95)]
    s.track(GM["contrabass"], 110, 64, 30).extend(motif)
    s.track(GM["tuba"], 70, 64, 30).extend(fm.octave(motif, 0, 0.7))
    s.track(GM["timpani"], 85, 64, 35).extend([(b * 4, 0.6, n("D2"), 85) for b in range(24)])
    s.track(GM["choir"], 60, 64, 70).extend([(b * 4, 4, p, 45) for b in range(8, 16) for p in fm.voicing(s.chords[b], 48, 2)])
    frag = line_ev([("A5", 1), ("F5", 1), ("D5", 1), ("C#5", 3)], 0, 55)
    s.track(GM["musicbox"], 55, 90, 90, bend=-800).extend([(b0 + bb, d, p, v) for b0 in (10 * 4, 20 * 4 + 2) for bb, d, p, v in frag])
    finish_bgm("MonsterWait", s, wobble=(0.004, 2), slows=((34.0, 3.0, 0.18),), cut_hz=3200, verb=(2.4, 0.3, 3500), bed=wind_bed(260, 0.12, 33))


# ======================================================================================
# 스팅어 5 · 징글 3
# ======================================================================================
def dark_signature(root="A5", minor_run=("A5", "F5", "D5", "C#5"), seconds=2.2):
    """견본 T4의 어두운 마법 서명: 긴 역재생 종 + 단조로 내려가는 틀어진 셀레스타 + 낮은 울림"""
    bell = reverse(inst([(0, 2.0, root, 100)], GM["tubular"]))[-int(1.1 * SR):]
    cel = detune_layer(inst(notes_seq(list(minor_run), 0.22, dur=1.0, vel=80), GM["celesta"]), 28)
    return mix((0, 0.9 * bell), (1.05, cel), (0.9, 0.12 * sub_drone(seconds, 44) * shape(seconds, 0.2, 1.0)), (1.2, 0.1 * sparkle(1.0, 6, base=88, seed=81)))


def broken_note(p="F#5", depth=0.3):
    return slowdown(np.pad(inst([(0, 1.5, p, 90)], GM["musicbox"]), (0, SR)), 0.3, 1.2, depth)


def stingers():
    out = {}
    rev_cym = inst([(0, 1.6, 49, 110)], 0, drum=True)
    out["StMonsterReveal"] = (reverb(mix((0, 0.6 * reverse(rev_cym)[-int(0.9 * SR):]), (0.9, inst([(0, 0.8, "Bb1", 120), (0.85, 1.8, "A1", 125)], GM["tuba"], gain=0.8)),
                                         (0.9, 0.8 * inst([(0, 0.8, "Bb2", 110), (0.85, 1.8, "A2", 115)], GM["trombone"])), (0.9, 1.2 * low_thud(38, 1.4, 0.4)),
                                         (0, 0.6 * sub_drone(3.2, 34) * shape(3.2, 0.8, 1.0)), (1.8, 0.35 * broken_note())), 2.0, 0.25, bright=3000), 3.4)   # 견본 그대로
    out["StMonsterArrive"] = (reverb(mix((0, 0.6 * reverse(whoosh(0.7, 200, 2000, seed=1201))), (0.6, inst([(0, 2.2, p, 110) for p in ("D2", "D3", "Eb3", "A3")], GM["organ"])),
                                         (0.6, 1.3 * low_thud(36, 1.4, 0.4)), (0.6, inst([(0, 0.3, "D2", 125), (0.5, 0.3, "D2", 105), (1.0, 0.7, "A1", 125)], GM["taiko"])),
                                         (0.75, 0.6 * growl(0.9, 75, seed=1202)), (1.9, 0.3 * broken_note("D5", 0.35))), 2.2, 0.25, bright=2800), 3.2)
    hope = inst([(0, 1.4, p, 70) for p in ("A4", "C#5", "E5")], GM["celesta"])  # 단조로 내려왔다가 장3화음으로 살짝 풀림(진행)
    out["StDeviceComplete"] = (reverb(mix((0, dark_signature()), (1.85, 0.6 * hope), (1.85, 0.3 * inst([(0, 1.4, "A3", 70), (0, 1.4, "E4", 60)], GM["strings"]))), 1.6, 0.25, bright=3500), 3.0)
    rise = mix(*[(0.7 * i, inst([(0, 0.8, n(p), 80 + 10 * i) for p in ps], GM["trem_strings"])) for i, ps in enumerate((("A3", "C4", "E4"), ("Bb3", "Db4", "F4"), ("B3", "D4", "F4"), ("C4", "Eb4", "Gb4")))])
    roll = inst([(i * 0.06, 0.05, 38, int(40 + 70 * i / 45)) for i in range(45)], 0, drum=True)
    out["StSpyLaunch"] = (reverb(mix((0, rise), (0, 0.6 * roll), (2.3, 0.3 * whoosh(0.7, 800, 4000, seed=1203)), (2.4, 0.25 * broken_note("C6", 0.25))), 1.4, 0.2, bright=3500), 3.0)
    witch = mix((0, 1.1 * wind(3.5, 600, 0.3, seed=1204) * shape(3.5, 1.2, 0.8)), (0.2, inst([(0, 3.0, p, 100) for p in ("A2", "A3", "C4", "Eb4")], GM["choir"])),
                (0, 0.7 * sub_drone(3.5, 31) * shape(3.5, 0.8, 0.8)), (0.9, 0.55 * dark_signature("Eb5", ("Eb5", "C5", "A4", "Gb4"), 2.6)))
    out["StWitchAppear"] = (reverb(witch, 2.2, 0.25, bright=2800), 3.5)
    # 징글: 탈출 성공만 밝게(§5.1) — 그래도 낡은 오르골 질감은 남긴다
    bright = tape_wobble(inst(notes_seq(["C5", "E5", "G5", "A5", "G5", "C6"], 0.16, dur=0.6, vel=100), GM["musicbox"]), 0.003, 1.5)
    out["JgEscapeSuccess"] = (reverb(mix((0, bright), (0, 0.45 * inst(notes_seq(["C6", "E6", "G6", "A6", "G6", "C7"], 0.16, vel=75), GM["celesta"])),
                                         (0.95, inst([(0, 2.6, p, 95) for p in ("C4", "E4", "G4", "C5", "E5")], GM["harp"])),
                                         (0.95, 0.5 * inst([(0, 2.6, p, 80) for p in ("C5", "G5", "E6")], GM["strings"])), (1.1, 0.3 * sparkle(1.5, 18, base=96, seed=1205))), 1.6, 0.2), 5.0)
    winding = slowdown(np.pad(inst(notes_seq(["E5", "C5", "A4", "G#4", "A4", "E4", "C4"], 0.36, dur=0.6, vel=85), GM["musicbox"]), (0, SR)), 1.2, 1.9, 0.45)  # 태엽이 풀리며 멈춤
    out["JgEscapeFail"] = (reverb(mix((0, winding), (0, 0.55 * inst(notes_seq(["A2", "A2", "F2", "E2", "A1"], 0.5, vel=75), GM["contrabass"])),
                                      (1.9, 0.5 * inst([(0, 2.4, p, 65) for p in ("A3", "C4", "E4")], GM["strings"])), (3.6, 0.4 * soft_wood(200, 0.3, seed=1207))), 2.0, 0.3, bright=3000), 5.0)
    laugh = mix(*[(0.17 * k, detune_layer(inst([(0, 0.13, n("Bb5") - 2 * k, 90)], GM["musicbox"]), 30)) for k in range(6)])  # 망가진 오르골 웃음
    out["JgMonsterWin"] = (reverb(mix((0, inst([(0, 0.9, "Bb2", 115), (1.0, 1.7, "A2", 120)], GM["trombone"])), (0, 0.8 * inst([(0, 0.9, "Bb1", 115), (1.0, 1.7, "A1", 120)], GM["tuba"])),
                                      (1.0, 1.2 * low_thud(40, 1.3, 0.35)), (1.0, 0.4 * inst([(0, 2.0, 49, 100)], 0, drum=True)), (2.5, 0.7 * laugh), (2.7, 0.45 * growl(1.2, 80, seed=1206))), 2.0, 0.25, bright=3000), 5.0)
    return out


def build_stingers():
    for name, (x, length) in stingers().items():
        x = cut(x, length, 0.5 if name.startswith("Jg") else 0.35)
        x = loud(x, -15.0 if name.startswith("St") else -16.0)
        write_ogg(os.path.join(ROOT, "BGM", name + ".ogg"), x, -1.0)
        made.append(("BGM", name, len(x) / SR, peak_db(x), lufs(x), ""))


# ======================================================================================
# 효과음: 쿠키 11(음정 빼고 질감만) · 마법 서명 8(어둡게) · UI 12(장난감 음색 줄임)
# ======================================================================================
OLD = os.path.join(HERE, "out", "final")


def out(folder, name, x, peak, length=None, release=0.03):
    """피크 기준으로 만든 뒤, S8 같은 소리의 크기(LUFS)에 맞춘다 — S9 믹스(묶음 음량·거리 감쇠)를 그대로 쓰려고. 피크는 −1 dB를 넘지 않는다"""
    import soundfile as sf
    if length: x = cut(x, length, release)
    x = to_peak(fade(np.asarray(x, dtype=np.float64)), peak)
    old = os.path.join(OLD, "SFX", folder, name + ".wav")
    note = ""
    if os.path.exists(old):
        o, _ = sf.read(old)
        gain = lufs(o) - lufs(x)
        x = x * 10 ** (gain / 20)
        if np.max(np.abs(x)) > 10 ** (-1 / 20): x = to_peak(x, -1)
        note = f"(S8 {lufs(o):.1f} LUFS)"
    x = write(os.path.join(ROOT, "SFX", folder, name + ".wav"), x, None)
    made.append(("SFX/" + folder, name, len(x) / SR, peak_db(x), lufs(x), note))


def felt(f=700, sec=0.08, seed=None):
    """펠트 덮인 나무 — 장난감 음정 없이 부드러운 딸깍"""
    return lp(soft_wood(f, sec, seed), 3500)


def sfx():
    C = "Character"
    for k, (f, sd) in enumerate(((420, 60), (380, 61), (460, 62), (400, 63))):            # 견본 T3: 마른 과자 바삭 + 나무
        st = mix((0, 0.9 * crunch(9, 0.03, 2200, 8000, seed=sd, size=0.01)), (0.003, 0.35 * soft_wood(f, 0.05, seed=sd)), (0, 0.15 * lp(noise(0.05, 70 + k), 900)))
        out(C, f"CookieStep_{k + 1}", st, -12, 0.09, 0.02)
    out(C, "CookieJump", mix((0, 0.5 * whoosh(0.18, 600, 1800, seed=71)), (0, 0.6 * crunch(6, 0.02, seed=72)), (0, 0.3 * lp(noise(0.12, 73), 1500) * env(0.12, 0.003, 0.04))), -10, 0.25, 0.08)
    out(C, "CookieLand", mix((0, soft_wood(300, 0.1, seed=74)), (0, 0.8 * crunch(12, 0.04, 2000, 8000, seed=75)), (0, 0.4 * low_thud(110, 0.12, 0.1))), -10, 0.15, 0.04)
    out(C, "CookieDodge", mix((0, 0.8 * whoosh(0.26, 700, 2600, seed=224)), (0.02, 0.4 * paper(0.12, seed=225)), (0.05, 0.3 * crunch(4, 0.03, seed=226))), -10, 0.28, 0.05)
    out(C, "CookieGrab", mix((0, 0.6 * paper(0.12, seed=76)), (0.02, 0.6 * crunch(8, 0.05, seed=77)), (0.05, 0.3 * soft_wood(350, 0.08, seed=78))), -10, 0.25, 0.08)
    out(C, "CookieRelease", mix((0, 0.5 * paper(0.1, seed=79)), (0.03, 0.5 * crunch(5, 0.04, seed=80)), (0.08, 0.4 * soft_wood(300, 0.08, seed=81))), -10, 0.25, 0.06)
    crumble = mix((0, crunch(45, 0.6, 1500, 9000, seed=227, size=0.02)), (0, 0.35 * low_thud(110, 0.3, 0.1)),
                  (0.3, 0.25 * broken_note("E5", 0.35)))                                    # 부서짐 + 멈추는 오르골 한 음(음정 장식 대신)
    out(C, "CookieCrumble", reverb(crumble, 0.7, 0.18, bright=3500), -6, 1.0, 0.25)
    out(C, "Respawn", mix((0, reverse(inst([(0, 0.8, "A5", 90)], GM["tubular"]))[-int(0.3 * SR):]), (0.28, soft_wood(330, 0.12, seed=241)), (0.3, 0.5 * crunch(6, 0.05, seed=242))), -8, 0.7, 0.15)

    # 마법 서명이 들어간 연출 — 어두운 서명(dark_signature)으로
    L, E, D = "Lobby", "Escape", "Device"
    out(L, "CauldronSplash", reverb(mix((0, 1.2 * wet(0.7, 230, 14, seed=304)), (0, 0.9 * low_thud(60, 0.5, 0.25)), (0.2, 0.45 * dark_signature("D5", ("D5", "Bb4", "G4", "F#4"), 1.6))), 1.0, 0.2, bright=3000), -4, 1.6, 0.35)
    spin = place(1.0, [(0.06 * i * (1 - i / 22), 0.5 * tin_metal(600 + 30 * i, 0.12, seed=410 + i)) for i in range(12)])
    out(E, "DeviceComplete", reverb(mix((0, spin), (0.3, dark_signature()), (2.1, 0.5 * inst([(0, 1.0, p, 70) for p in ("A4", "C#5", "E5")], GM["celesta"]))), 1.4, 0.22, bright=3500), -2, 2.4, 0.4)
    out(E, "ChestOpen", mix((0, creak(0.25, 25, 45, (350, 900), seed=401)), (0.05, soft_wood(320, 0.2, seed=402)), (0.12, 0.35 * detune_layer(inst(notes_seq(["E5", "C5"], 0.08, dur=0.4, vel=70), GM["musicbox"]), 25))), -6, 0.6, 0.15)
    out(E, "BoardSuck", mix((0, 0.6 * reverse(inst([(0, 0.8, "A5", 90)], GM["tubular"]))[-int(0.35 * SR):]), (0, 0.9 * whoosh(0.6, 300, 2400, seed=420)),
                             (0.35, 0.4 * detune_layer(inst(notes_seq(["A5", "D5"], 0.07, vel=80), GM["celesta"]), 25))), -6, 0.75, 0.15)
    out(D, "OvenFlash", reverb(mix((0, 0.9 * whoosh(0.7, 250, 1800, seed=723)), (0.15, 0.8 * dark_signature("E5", ("E5", "C5", "A4", "G#4"), 1.4))), 1.2, 0.2, bright=3000), -2, 1.5, 0.3)
    out(D, "AltarGlow", reverb(mix((0, dark_signature("C5", ("C5", "Ab4", "F4", "E4"), 2.0)), (0, 0.4 * inst([(0, 1.8, "C3", 70), (0, 1.8, "G3", 65), (0.4, 1.4, "Eb4", 60)], GM["choir"]))), 1.4, 0.25, bright=3000), -3, 2.4, 0.4)
    out(D, "PortalOpen", reverb(mix((0, 0.8 * whoosh(2.0, 150, 1500, seed=791)), (0.4, dark_signature("C5", ("C5", "Ab4", "F4", "E4"), 2.2)),
                                    (0.3, 0.45 * inst([(0, 1.6, "C3", 80), (0, 1.6, "G3", 70), (0.5, 1.1, "Eb4", 70)], GM["choir"])), (0, 0.35 * sub_drone(2.4, 40) * shape(2.4, 0.5, 0.6))), 1.6, 0.25, bright=2800), -2, 2.4, 0.4)
    out(D, "PortalClose", mix((0, whoosh(1.0, 1800, 150, seed=793)), (1.0, 0.8 * pop(700, 350, 0.1)), (0.2, 0.35 * detune_layer(inst(notes_seq(["E5", "C5", "A4"], 0.15, vel=70), GM["celesta"]), 25))), -4, 1.25, 0.15)

    U = "UI"   # 정보 전달은 그대로, 음색만 장난감(마림바·실로폰) → 펠트 나무·낮게 튼 오르골
    mb = lambda ps, step, vel=75: lp(detune_layer(inst(notes_seq(ps, step, dur=step * 1.6, vel=vel), GM["musicbox"]), 12), 4000)
    out(U, "UiClick", mix((0, felt(900, 0.07, seed=102)), (0, 0.6 * felt(520, 0.08, seed=116)), (0, 0.25 * lp(noise(0.02, 1), 2500) * env(0.02, 0.001, 0.008))), -9, 0.09, 0.02)
    out(U, "UiConfirm", mix((0, felt(800, 0.06, seed=103)), (0.03, 0.45 * mb(["A4", "E5"], 0.08))), -8, 0.32, 0.08)
    out(U, "UiCancel", mix((0, felt(650, 0.06, seed=104)), (0.03, 0.45 * mb(["E5", "A4"], 0.08))), -9, 0.3, 0.08)
    out(U, "UiStart", reverb(mix((0, felt(700, 0.08, seed=105)), (0.05, 0.6 * mb(["A4", "C5", "E5", "A5"], 0.12, 85)), (0.5, 0.25 * inst([(0, 0.8, "A3", 70), (0, 0.8, "E4", 60)], GM["strings"]))), 0.8, 0.18), -5, 0.95, 0.25)
    out(U, "UiError", mix((0, felt(380, 0.1, seed=106)), (0.1, felt(340, 0.1, seed=107)), (0, 0.35 * inst([(0, 0.25, "Bb2", 90)], GM["contrabass"]))), -8, 0.3, 0.06)
    out(U, "UiOpen", mix((0, 0.5 * paper(0.1, seed=108)), (0.02, 0.4 * felt(1100, 0.05, seed=109))), -10, 0.16, 0.05)
    out(U, "UiClose", mix((0, 0.5 * paper(0.1, seed=110)), (0.02, 0.4 * felt(800, 0.05, seed=111))), -10, 0.16, 0.05)
    out(U, "UiSaved", mix((0, felt(900, 0.06, seed=112)), (0.04, 0.45 * mb(["E5", "A5"], 0.1))), -8, 0.5, 0.12)
    out(U, "UiTickFinal", mix((0, felt(1500, 0.07, seed=113)), (0, 0.45 * lp(inst([(0, 0.3, "A5", 90)], GM["tubular"]), 3500))), -7, 0.35, 0.1)
    out(U, "UiToastInfo", mix((0, felt(1000, 0.05, seed=114)), (0.05, 0.4 * mb(["E5", "B5"], 0.1, 70))), -9, 0.45, 0.12)
    out(U, "UiToastAlert", mix((0, 0.5 * mb(["Bb4", "A4"], 0.16, 90)), (0, 0.5 * low_thud(60, 0.35, 0.2)), (0, 0.3 * inst([(0, 0.4, "Bb1", 90)], GM["contrabass"]))), -6, 0.7, 0.2)
    out(U, "UiChat", mix((0, 0.5 * pop(700, 1000, 0.05)), (0.01, 0.35 * felt(1300, 0.04, seed=115))), -12, 0.08, 0.02)


# ======================================================================================
# 환경음 7 (반복 OGG)
# ======================================================================================
def out_loop(name, x, seconds, xfade, target_lufs, peak_cap=-3.0):
    y = loopify(x, seconds, xfade)
    y = norm_lufs(y, target_lufs)
    pk = np.max(np.abs(y)); cap = 10 ** (peak_cap / 20)
    if pk > cap: y *= cap / pk
    write_ogg(os.path.join(ROOT, "SFX", "Ambience", name + ".ogg"), y, peak_cap)
    made.append(("SFX/Ambience", name, len(y) / SR, peak_db(y), lufs(y), ""))


def far_musicbox(notes, depth=0.25, seed=0):
    box = tape_wobble(inst(notes_seq(notes, 0.7, dur=1.0, vel=40), GM["musicbox"]), 0.02, 0.3)
    return reverb(lp(slowdown(np.pad(box, (0, SR)), 1.5, 2.5, depth), 2500), 2.5, 0.6, bright=2500)


def ambience():
    r = np.random.default_rng(31)
    T = 61.0
    sc = lambda count, make: place(T, [(r.uniform(0, T - 1.5), make(i)) for i in range(count)])
    drip = lambda i: reverb(mix((0, pop(r.uniform(600, 900), r.uniform(250, 380), 0.05)), (0.01, 0.6 * wet(0.25, 300, 20, seed=40 + i))), 1.5, 0.4, bright=3000)

    # 캔디숲: 견본 T2를 60초로 — 마른 바람·삐걱·끈적한 방울·멀리서 늘어지는 오르골·유리 울림(새소리 없음)
    glass = 0.04 * (np.sin(2 * np.pi * 1318 * t_(T)) + 0.6 * np.sin(2 * np.pi * 1977 * t_(T))) * (0.5 + 0.5 * np.sin(2 * np.pi * 0.07 * t_(T)))
    candy = mix((0, 1.4 * wind(T, 380, 0.06, seed=32)), (0, 0.35 * sc(14, lambda i: creak(r.uniform(0.6, 1.2), r.uniform(10, 18), r.uniform(22, 35), (r.uniform(250, 420), r.uniform(700, 1000)), seed=33 + i))),
                (0, 0.35 * sc(16, drip)), (9.0, 0.35 * far_musicbox(["E6", "C6", "A5", "B5", "G#5"])), (39.0, 0.3 * far_musicbox(["A5", "F5", "E5", "D5", "C#5"], 0.3)),
                (0, glass), (0, 0.08 * sub_drone(T, 38, 0.04)))
    out_loop("AmbCandyForest", reverb(candy, 1.2, 0.2)[:int(T * SR)], 60.0, 1.0, -22)

    # 진저브레드: 차가운 바람, 멈칫거리는 시계, 과자 집 삐걱, 쿠키 금 가는 소리, 멀리 틀어진 종
    tick_t = [float(i) for i in range(61) if i % 13 not in (6, 7)]
    ticks = place(T, [(t, mix((0, soft_wood(1500 if i % 2 else 1150, 0.08, seed=950 + i % 5)), (0, 0.15 * tin_metal(2600, 0.05, seed=950)))) for i, t in enumerate(tick_t)])
    ginger = mix((0, 1.1 * wind(T, 420, 0.07, seed=951)), (0, 0.22 * ticks), (0, 0.3 * sc(8, lambda i: creak(0.7, 18, 35, (400, 1000), seed=960 + i))),
                 (0, 0.2 * sc(5, lambda i: crunch(14, 0.25, 1500, 7000, seed=970 + i, size=0.015))),
                 (14.0, 0.22 * reverb(detune_layer(inst([(0, 2.5, "F3", 70)], GM["tubular"]), 35), 2.5, 0.45)), (44.0, 0.18 * reverb(detune_layer(inst([(0, 2.5, "C4", 65)], GM["tubular"]), 35), 2.5, 0.45)),
                 (0, 0.06 * sub_drone(T, 36, 0.05)))
    out_loop("AmbGingerbread", reverb(ginger, 1.0, 0.18)[:int(T * SR)], 60.0, 1.0, -22)

    # 놀이공원: 바람, 찢어진 천 펄럭임, 삐걱이는 놀이기구, 지지직 전구, 멀리서 꺼져 가는 칼리오페, 아주 작게 굳은 웃음
    flap = lambda i: bp(noise(0.5, 1010 + i), 300, 2500) * np.abs(np.sin(2 * np.pi * 7 * t_(0.5))) * shape(0.5, 0.05, 0.3)
    buzz = lambda i: 0.5 * fire_crackle(0.4, 120, seed=1030 + i) * shape(0.4, 0.02, 0.2)
    calliope = lambda ps, d: reverb(lp(slowdown(np.pad(tape_wobble(inst(notes_seq(ps, 0.4, dur=0.6, vel=50), GM["calliope"]), 0.02, 0.5), (0, SR)), 1.0, 2.0, d), 2000), 2.5, 0.6, bright=2000)
    laugh = place(1.2, [(0.18 * k, growl(0.12, 330 - 15 * k, seed=1000 + k)) for k in range(4)])
    carnival = mix((0, 1.0 * wind(T, 480, 0.09, seed=1001)), (0, 0.25 * sc(10, flap)), (0, 0.3 * sc(8, lambda i: creak(1.0, 12, 25, (300, 800), seed=1020 + i))),
                   (0, 0.15 * sc(12, buzz)), (6.0, 0.3 * calliope(["E5", "A5", "C6", "B5", "A5", "G#5"], 0.3)), (34.0, 0.25 * calliope(["A5", "E5", "F5", "E5", "D5"], 0.4)),
                   (24.5, 0.035 * reverb(laugh, 2.0, 0.6)), (0, 0.07 * sub_drone(T, 34, 0.05)))
    out_loop("AmbCarnival", carnival[:int(T * SR)], 60.0, 1.0, -22)

    # 유령 베이커리: 차가운 바람 틈새, 오븐 낮은 울림(유일하게 따뜻), 틀어진 유령 합창, 유리병 달그락, 물방울
    ghost = lambda p: detune_layer(inst([(0, 5.0, p, 50)], GM["oohs"]), 30)
    jar = lambda i: 0.4 * tin_metal(r.uniform(2200, 3200), 0.25, seed=1060 + i)
    bakery = mix((0, 0.9 * wind(T, 300, 0.06, seed=1031)), (0, 0.18 * np.sin(2 * np.pi * 62 * t_(T)) * (0.7 + 0.3 * np.sin(2 * np.pi * (1 / 30) * t_(T)))),
                 (0, 0.25 * fire_crackle(T, 8, seed=1030)), (12.0, 0.22 * ghost("E3")), (41.0, 0.2 * ghost("D#3")), (0, 0.25 * sc(6, jar)), (0, 0.25 * sc(10, drip)))
    out_loop("AmbBakery", reverb(bakery, 1.6, 0.22, bright=3000)[:int(T * SR)], 60.0, 1.0, -22)

    # 공장 기계(16초, 3D 랜드마크): 같은 박자, 더 지치고 무겁게 — 신음하는 철, 끈적한 초콜릿 방울
    T17 = 17.0
    fbub = place(T17, [(r.uniform(0, 16.5), pop(r.uniform(70, 120), r.uniform(130, 200), 0.1) * r.uniform(0.4, 1)) for _ in range(36)])
    groan = lambda i: creak(1.2, 6, 12, (180, 420), seed=1080 + i)
    factory = mix((0, 0.25 * (np.sin(2 * np.pi * 48 * t_(T17)) + 0.4 * np.sin(2 * np.pi * 96 * t_(T17)))),
                  (0, 0.4 * place(T17, [(0.5 + 2.0 * i, steam(0.8, seed=970 + i)) for i in range(8)])),
                  (0, 0.4 * place(T17, [(1.0 + 1.0 * i, mix((0, 0.5 * tin_metal(520, 0.35, seed=980)), (0, 0.7 * low_thud(70, 0.35)))) for i in range(16)])),
                  (0, 0.5 * place(T17, [(3.0, groan(0)), (11.0, groan(1))])), (0, 0.7 * lp(fbub, 500)))
    out_loop("AmbFactory", lp(reverb(factory, 1.2, 0.18, bright=3500), 3800)[:int(T17 * SR)], 16.0, 1.0, -24)  # 증기 쉿 소리를 눌러 더 어둡고 무겁게

    # 동굴(진저브레드 지하): 낮은 울림, 물방울, 먼 신음 바람, 아주 멀리 오르골 메아리
    drips2 = place(T, [(r.uniform(0, 60), pop(r.uniform(1400, 2200), r.uniform(700, 1100), 0.03) * r.uniform(0.4, 1)) for _ in range(30)])
    moan = lambda i: lp(wind(4.0, 250, 0.2, seed=1090 + i), 400) * shape(4.0, 1.5, 1.5)
    cave = mix((0, 0.6 * sub_drone(T, 34, 0.06)), (0, 0.45 * wind(T, 260, 0.05, seed=1050)), (0, 0.6 * drips2), (0, 0.5 * place(T, [(5.0, moan(0)), (33.0, moan(1))])),
               (20.0, 0.18 * far_musicbox(["A5", "F5", "D5", "C#5"], 0.35)))
    out_loop("AmbCave", reverb(cave, 2.8, 0.45, bright=2500)[:int(T * SR)], 60.0, 1.0, -26)

    # 회전목마(3D, 30초 반복): 늘어지고 끊기는 오르간 — 같은 주제, 박자마다 기계 덜컹
    chords_ = ["Am", "E", "Am", "Dm", "Am", "E", "Am", "E", "F", "C", "Dm", "E", "Am", "E", "Am", "Am"]
    s = fm.Song(92, 3, chords_)
    mel = fm.melody_from(s, fm.scale_of("A", True), ["a", "b", "c", "a"], 69, 86, dict(a=61, b=62, c=65))
    s.track(GM["calliope"], 100, 64, 50).extend(mel)
    s.track(GM["calliope"], 70, 64, 50, bend=900).extend([(b, d, p, int(v * 0.7)) for b, d, p, v in mel])
    bass, ch = [], []; fm.acc_waltz(s, bass, ch, 2, 57)
    s.track(GM["piano"], 75, 64, 40, bend=500).extend(bass + ch)
    lap = s.render_loop(stereo=False); sec = len(lap) / SR; bar = 3 * 60 / 92
    clank = place(sec, [(b * bar + 0.01, mix((0, 0.3 * tin_metal(420, 0.25, seed=1100 + b % 3)), (0, 0.45 * low_thud(80, 0.18, 0.2)))) for b in range(16)])
    lap = lap / (np.max(np.abs(lap)) + 1e-9) * 0.7 + clank

    def fx(x3, L_):
        y = tape_wobble(x3, 0.014, 3 / sec)
        for k in range(3):
            y = slowdown(y, k * sec + 9.0, 2.2, 0.28)
            y = dropout(y, k * sec + 21.0, 0.6, 0.1)
        return lp(y, 2400) + np.tile(0.05 * loopify(lp(noise(sec + 1, 1110), 300), sec, 1.0), 3)[:len(y)]
    y = loud(periodic(lap, fx), -20, -3, loop=True)
    write_ogg(os.path.join(ROOT, "SFX", "Ambience", "CarouselSpin.ogg"), y, -3)
    made.append(("SFX/Ambience", "CarouselSpin", len(y) / SR, peak_db(y), lufs(y), f"seam {abs(y[-1] - y[0]):.4f}"))


if __name__ == "__main__":
    only = sys.argv[1] if len(sys.argv) > 1 else "all"
    if only in ("all", "bgm"):
        for f in (bgm_candy, bgm_gingerbread, bgm_factory, bgm_carnival, bgm_bakery, bgm_game_lobby, bgm_monster_wait): f()
    if only in ("all", "stingers"): build_stingers()
    if only in ("all", "sfx"): sfx()
    if only in ("all", "amb"): ambience()
    for folder, name, sec, pk, lu, note in made:
        print(f"{folder:14s} {name:20s} {sec:6.2f}s peak {pk:6.1f} dB {lu:6.1f} LUFS {note}")
