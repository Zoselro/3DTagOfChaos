# 캔디숲 소리 견본(TwistedCandyPlan.md §5·§9) — "망가진 동화" 방향. 게임에 넣지 않는 청취용.
# 지금(S8) 소리와 새 소리를 나란히 묶어 비교한다. 출력: out/twisted/candy/<개별>.wav, reels/*.wav, listening_guide.md
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import soundfile as sf
from audiolib import *
import final_music as fm

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out", "twisted", "candy")
OLD = os.path.join(HERE, "out", "final")
guide = []


def load(path, sec=None):
    x, sr = sf.read(path); x = x.mean(axis=1) if x.ndim > 1 else x
    return x[:int(sec * SR)] if sec else x


def save(name, x, peak):
    write(os.path.join(OUT, name + ".wav"), x, peak)
    return (name, x, peak)


def section(title, reel_name, items):
    log = reel(os.path.join(OUT, "reels", reel_name + ".wav"), items, gap=1.0)
    guide.append(f"\n### {reel_name}.wav — {title}\n"); guide.extend("- " + l for l in log)


def slowdown(x, start, length, depth=0.12):
    """테이프가 늘어지듯 잠깐 느려졌다 돌아온다(음높이도 같이 내려감)"""
    s, n = int(start * SR), int(length * SR)
    seg = x[s:s + n]
    rate = 1.0 - depth * np.sin(np.linspace(0, np.pi, n)) ** 2
    pos = np.cumsum(rate); pos = pos / pos[-1] * (len(seg) - 1)
    out = x.copy(); out[s:s + n] = np.interp(pos, np.arange(len(seg)), seg)
    return out


# ================= 1. 배경음: 고장 난 오르골 자장가(A단조 3/4, 72 BPM, 12마디 ≈ 30초) =================
chords = ["Am", "F", "Dm", "E", "Am", "F", "Dm", "E", "Am", "F", "E", "Am"]
song = fm.Song(72, 3, chords)
line = [("E5", 2), ("C5", 1), ("A4", 2), ("C5", 1), ("D5", 1), ("E5", 1), ("F5", 1), ("E5", 3),
        ("E5", 2), ("C5", 1), ("A4", 1), ("G4", 1), ("F4", 1), ("D4", 1), ("F4", 1), ("A4", 1), ("G#4", 3),
        ("A4", 2), ("C5", 1), ("B4", 1), ("A4", 1), ("F4", 1), ("E4", 2), ("G#4", 1), ("A4", 3)]
mel, beat = [], 0.0
for p, d in line:
    mel.append((beat, d * 0.95, n(p), 78)); beat += d
song.track(GM["musicbox"], 105, 64, 70).extend(mel)
song.track(GM["musicbox"], 70, 80, 70, bend=700).extend([(b, d, p, int(v * 0.8)) for b, d, p, v in mel])   # 음정이 틀어진 두 번째 오르골(+21 cent)
song.track(GM["celesta"], 55, 50, 80).extend([(b * 3, 2.5, fm.bass_root(c, 3) + 12, 45) for b, c in enumerate(chords) if b % 2 == 0])
pad = []; fm.acc_pad(song, pad, 45, 42, 2)
song.track(GM["strings"], 95, 64, 70).extend(pad)
song.track(GM["contrabass"], 85, 64, 50).extend([(b * 3, 2.9, fm.bass_root(c, 1), 55) for b, c in enumerate(chords)])
music = song.render_loop(stereo=False)
music = lp(tape_wobble(music, 0.006, 0.35), 2600)  # 늘 미세하게 흔들림 + 낡은 소리처럼 고음을 깎음
music = slowdown(music, 20.5, 3.0, 0.14)          # 한 번 크게 늘어짐(10~11마디)
bed = 0.18 * wind(len(music) / SR, 420, 0.07, seed=5) + 0.05 * sub_drone(len(music) / SR, 40, 0.05)
new_bgm = norm_lufs(reverb(music, 2.2, 0.35, bright=4000)[:len(music)] + bed, -18)
old_bgm = load(os.path.join(OLD, "BGM", "CandyForestPaint.ogg"), 20)
section("배경음 비교: 지금(S8) 앞 20초 → 새 견본 30초(고장 난 오르골 자장가, 한 번 테이프처럼 늘어짐)", "T1_BGM_CandyForest",
        [("지금 CandyForestPaint(앞 20초)", fade(norm_lufs(old_bgm, -18), 0.01, 1.5), -3), save("new_CandyForestPaint_sample", new_bgm, -3)])

# ================= 2. 환경음: 마른 나무 사이 바람·삐걱·끈적한 방울·멀리서 꺼져 가는 오르골 =================
T = 24.0; r = np.random.default_rng(31)
amb = 1.4 * wind(T, 380, 0.06, seed=32)
amb += 0.35 * place(T, [(r.uniform(0, T - 1), creak(r.uniform(0.6, 1.2), r.uniform(10, 18), r.uniform(22, 35), (r.uniform(250, 420), r.uniform(700, 1000)), seed=33 + i)) for i in range(6)])
drip = lambda i: mix((0, pop(r.uniform(600, 900), r.uniform(250, 380), 0.05)), (0.01, 0.6 * wet(0.25, 300, 20, seed=40 + i)))
amb += 0.35 * place(T, [(r.uniform(0, T - 1), reverb(drip(i), 1.5, 0.4, bright=3000)) for i in range(7)])
far_box = tape_wobble(inst(notes_seq(["E6", "C6", "A5", "B5", "G#5"], 0.7, dur=1.0, vel=40), GM["musicbox"]), 0.02, 0.3)
far_box = slowdown(np.pad(far_box, (0, SR)), 2.0, 2.5, 0.25)
amb += place(T, [(9.0, 0.35 * reverb(lp(far_box, 2500), 2.5, 0.6, bright=2500))])
glass = 0.04 * (np.sin(2 * np.pi * 1318 * t_(T)) + 0.6 * np.sin(2 * np.pi * 1977 * t_(T))) * (0.5 + 0.5 * np.sin(2 * np.pi * 0.07 * t_(T)))
amb = amb[:int(T * SR)] + glass + 0.08 * sub_drone(T, 38, 0.04)
new_amb = norm_lufs(reverb(amb, 1.2, 0.2)[:int(T * SR)], -24)
old_amb = load(os.path.join(OLD, "SFX", "Ambience", "AmbCandyForest.ogg"), 12)
section("환경음 비교: 지금(새소리·풍경) 12초 → 새 견본 24초(마른 바람·삐걱·끈적한 방울·멀리서 늘어지는 오르골·유리 같은 울림). 둘 다 같은 크기로 키워 들려 드림", "T2_Ambience_CandyForest",
        [("지금 AmbCandyForest(12초)", fade(norm_lufs(old_amb, -20), 0.3, 1.0), -6), save("new_AmbCandyForest_sample", norm_lufs(new_amb, -20), -6)])

# ================= 3. 쿠키 소리: 마림바 음정을 빼고 바삭·마른 나무 질감만 =================
items = []
for nm, pk in (("CookieStep_1", -12), ("CookieStep_3", -12), ("CookieJump", -9), ("CookieLand", -10), ("CookieGrab", -9)):  # 게임에 들어간 크기 그대로
    items.append((f"지금 {nm}", load(os.path.join(OLD, "SFX", "Character", nm + ".wav")), pk))
steps = [mix((0, 0.9 * crunch(9, 0.03, 2200, 8000, seed=60 + k, size=0.01)), (0.003, 0.35 * soft_wood(f, 0.05, seed=60 + k)), (0, 0.15 * lp(noise(0.05, 70 + k), 900))) for k, f in enumerate((420, 380, 460, 400))]
new = [save("new_CookieStep_1", cut(steps[0], 0.09), -12), save("new_CookieStep_3", cut(steps[2], 0.09), -12),
       save("new_CookieJump", cut(mix((0, 0.5 * whoosh(0.18, 600, 1800, seed=71)), (0, 0.6 * crunch(6, 0.02, seed=72)), (0, 0.3 * lp(noise(0.12, 73), 1500) * env(0.12, 0.003, 0.04))), 0.25), -10),
       save("new_CookieLand", cut(mix((0, soft_wood(300, 0.1, seed=74)), (0, 0.8 * crunch(12, 0.04, 2000, 8000, seed=75)), (0, 0.4 * low_thud(110, 0.12, 0.1))), 0.15), -10),
       save("new_CookieGrab", cut(mix((0, 0.6 * paper(0.12, seed=76)), (0.02, 0.6 * crunch(8, 0.05, seed=77)), (0.05, 0.3 * soft_wood(350, 0.08, seed=78))), 0.25), -10)]
section("쿠키 소리 비교: 지금 5개(발소리 2·점프·착지·들기) → 새 5개(같은 순서). 장난감 음정을 빼고 마른 과자 질감만", "T3_Cookie",
        items + new)

# ================= 4. 마법 서명·괴물 공개 스팅어 =================
bell = reverse(inst([(0, 2.0, "A5", 100)], GM["tubular"]))[-int(1.1 * SR):]
cel = detune_layer(inst(notes_seq(["A5", "F5", "D5", "C#5"], 0.22, dur=1.0, vel=80), GM["celesta"]), 28)
dark_sig = reverb(mix((0, 0.9 * bell), (1.05, cel), (0.9, 0.12 * sub_drone(2.2, 44) * shape(2.2, 0.2, 1.0)), (1.2, 0.12 * sparkle(1.0, 6, base=88, seed=81))), 2.0, 0.35, bright=3500)
rev_cym = inst([(0, 1.6, 49, 110)], 0, drum=True)
broken_note = slowdown(np.pad(inst([(0, 1.5, "F#5", 90)], GM["musicbox"]), (0, SR)), 0.3, 1.2, 0.3)
reveal = reverb(mix((0, 0.6 * reverse(rev_cym)[-int(0.9 * SR):]), (0.9, inst([(0, 0.8, "Bb1", 120), (0.85, 1.8, "A1", 125)], GM["tuba"], gain=0.8)),
                    (0.9, 0.8 * inst([(0, 0.8, "Bb2", 110), (0.85, 1.8, "A2", 115)], GM["trombone"])), (0.9, 1.2 * low_thud(38, 1.4, 0.4)),
                    (0, 0.6 * sub_drone(3.2, 34) * shape(3.2, 0.8, 1.0)), (1.8, 0.35 * broken_note)), 2.0, 0.25, bright=3000)
section("마법 서명·괴물 공개 비교: 지금 마법 서명 → 새(길고 어두운 역재생 종 + 단조로 내려가는 셀레스타) / 지금 괴물 공개 스팅어 → 새(더 낮고 무겁게 + 끝에 늘어지는 오르골 한 음)", "T4_Magic_Reveal",
        [("지금 마법 서명(장치 완성 효과음)", load(os.path.join(OLD, "SFX", "Escape", "DeviceComplete.wav")), -4), save("new_MagicSignature_dark", cut(dark_sig, 3.2, 0.8), -4),
         ("지금 StMonsterReveal", load(os.path.join(OLD, "BGM", "StMonsterReveal.ogg")), -2), save("new_StMonsterReveal", cut(reveal, 3.4, 0.6), -2)])

# ================= 5. 장면 믹스: 캔디숲에서 숨어 있는데 괴물이 다가온다(약 30초) =================
L = 30.0
scene = 0.45 * np.pad(new_bgm, (0, int(L * SR)))[:int(L * SR)] + 0.5 * np.pad(norm_lufs(new_amb, -26), (0, int(L * SR)))[:int(L * SR)]
cookie_steps = place(L, [(1.0 + 0.3 * i, 0.25 * steps[i % 4]) for i in range(10)])
mon_steps = []
for i in range(14):   # 멀리서(먹먹) → 가까이(또렷)
    t = 12.0 + i * 0.9; near = i / 13
    stp = mix((0, low_thud(50, 0.55, 0.3)), (0.01, 0.55 * wet(0.22, 160, 30, seed=90 + i)), (0.015, 0.18 * hp(wet(0.15, 900, 50, seed=110 + i), 1500)))
    stp = lp(stp, 300 + 4000 * near ** 2) * (0.15 + 0.85 * near)
    mon_steps.append((t, 0.7 * stp))
scene += place(L, mon_steps)
chase_old = load(os.path.join(HERE, "..", "out", "chase", "D2", "Chase_Far.wav")) if os.path.exists(os.path.join(HERE, "..", "out", "chase", "D2", "Chase_Far.wav")) else None
if chase_old is not None:
    ch = chase_old[:int(12 * SR)] * np.linspace(0, 0.5, int(12 * SR))
    scene += place(L, [(16.0, ch)])
scene += place(L, [(26.5, 0.9 * cut(reveal, 3.4, 0.6))])
scene = norm_lufs(scene, -16)
section("장면 믹스(약 30초): 새 배경음·환경음 속에 쿠키가 걷다 멈추고, 괴물 발소리가 멀리서 먹먹하게 → 점점 또렷하게, 추격음 D(확정)가 들어오고, 마지막에 새 괴물 스팅어", "T5_Scene",
        [save("scene_candy_monster_approach", scene, -1)])

with open(os.path.join(OUT, "listening_guide.md"), "w", encoding="utf-8") as f:
    f.write("# 캔디숲 소리 견본 — 듣기 안내(TwistedCandyPlan §9)\n\n각 묶음은 '지금 소리 → 새 소리' 순서. 몇 초에 무엇이 나오는지 적었다.\n" + "\n".join(guide) + "\n")
print("\n".join(guide))
