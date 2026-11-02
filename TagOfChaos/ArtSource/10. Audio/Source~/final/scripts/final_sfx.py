# S8 최종 효과음 95개(AudioProductionPlan.md §7.3~§7.10). P1 팔레트(쿠키·괴물·마법·환경)를 그대로 이어 쓴다.
# 출력: out/final/SFX/<폴더>/<ID>.wav(짧은 소리) 또는 .ogg(긴 환경음). 파일 이름 = Sound ID(변형은 _n).
# 음량 계층(§1.3): L1 작은 동작 피크 −12 · L2 일반 −6~−8 · L3 위협 −4 · L4 큰 사건 −1~−3 · 환경음은 LUFS로 아주 조용하게.
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from audiolib import *

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "final", "SFX")
made = []


def out(folder, name, x, peak, length=None, release=0.03):
    if length: x = cut(x, length, release)
    x = write(os.path.join(ROOT, folder, name + ".wav"), x, peak)
    made.append((folder, name, len(x) / SR, peak_db(x)))


def out_loop(folder, name, x, seconds, xfade, target_lufs, ogg=False, peak_cap=-3.0):
    y = loopify(x, seconds, xfade)
    y = norm_lufs(y, target_lufs)
    pk = np.max(np.abs(y)); cap = 10 ** (peak_cap / 20)
    if pk > cap: y *= cap / pk
    path = os.path.join(ROOT, folder, name + (".ogg" if ogg else ".wav"))
    if ogg: write_ogg(path, y, peak_cap)
    else: write(path, y, None)
    made.append((folder, name, len(y) / SR, peak_db(y)))


def magic_signature(octave=0, minor=False, detune=0, speed=0.09, spark=True):
    root = 6 + octave
    bell = reverse(inst([(0, 1.0, f"C{root}", 100)], GM["tubular"]))[-int(0.45 * SR):]
    third, seventh = (f"Eb{root}", f"Bb{root}") if minor else (f"E{root}", f"B{root}")
    cel = inst(notes_seq([f"C{root}", third, f"G{root}", seventh], speed, dur=0.5, vel=90), GM["celesta"])
    if detune: cel = detune_layer(cel, detune)
    parts = [(0, bell), (0.42, cel), (0.3, 0.12 * whoosh(0.5, 2000, 6000, seed=3))]
    if spark: parts.append((0.5, 0.25 * sparkle(0.6, 10, base=12 * (root + 1) + 12, seed=5)))
    return mix(*parts)


def note1(p, prog, vel=90, dur=0.3): return inst([(0, dur, p, vel)], GM[prog])


def seq(ps, prog, step, vel=90, dur=None): return inst(notes_seq(ps, step, dur=dur or step * 1.5, vel=vel), GM[prog])


# ======================= UI 15 (2D, 단순) =======================
U = "UI"
out(U, "UiHover", soft_wood(2400, 0.04, seed=101), -16, 0.04, 0.01)
out(U, "UiClick", mix((0, soft_wood(950, 0.06, seed=102)), (0, 0.5 * note1("G5", "marimba", 80, 0.1))), -9, 0.09, 0.03)
out(U, "UiConfirm", mix((0, seq(["C5", "G5"], "marimba", 0.07, 95)), (0.1, 0.15 * sparkle(0.15, 3, base=96, seed=103))), -8, 0.32, 0.08)
out(U, "UiCancel", seq(["G5", "C5"], "marimba", 0.07, 85), -9, 0.3, 0.08)
out(U, "UiStart", mix((0, seq(["C5", "E5", "G5", "A5", "G5"], "marimba", 0.08, 100)), (0, 0.35 * seq(["C6", "E6", "G6", "A6", "G6"], "celesta", 0.08, 75)),
                      (0.35, 0.2 * sparkle(0.4, 6, base=96, seed=104))), -5, 0.8, 0.2)
out(U, "UiError", mix((0, soft_wood(520, 0.08, seed=105)), (0.09, soft_wood(470, 0.08, seed=106)), (0, 0.4 * note1("A3", "marimba", 80, 0.2))), -8, 0.25, 0.06)
out(U, "UiOpen", mix((0, 0.5 * paper(0.1, seed=107)), (0.02, 0.5 * seq(["G5", "C6"], "celesta", 0.04, 75))), -10, 0.16, 0.05)
out(U, "UiClose", mix((0, 0.5 * paper(0.1, seed=108)), (0.02, 0.5 * seq(["C6", "G5"], "celesta", 0.04, 70))), -10, 0.16, 0.05)
out(U, "UiSaved", mix((0, seq(["C6", "E6", "G6"], "celesta", 0.06, 90)), (0.15, 0.3 * sparkle(0.35, 6, base=98, seed=109))), -8, 0.6, 0.15)
out(U, "UiTick", soft_wood(1500, 0.05, seed=110), -10, 0.06, 0.02)
out(U, "UiTickFinal", mix((0, soft_wood(1900, 0.06, seed=111)), (0, 0.6 * note1("C7", "glock", 95, 0.2))), -7, 0.25, 0.08)
out(U, "UiSlotSelect", soft_wood(1100, 0.06, seed=112), -12, 0.08, 0.02)
out(U, "UiToastInfo", seq(["G5", "C6"], "glock", 0.12, 85), -9, 0.5, 0.15)
out(U, "UiToastAlert", mix((0, seq(["Eb5", "C5"], "glock", 0.14, 95)), (0, 0.5 * low_thud(70, 0.3, 0.2))), -6, 0.7, 0.2)
out(U, "UiChat", mix((0, 0.6 * pop(800, 1300, 0.05)), (0.01, 0.4 * note1("E6", "celesta", 80, 0.1))), -12, 0.08, 0.02)

# ======================= 색칠 6 (2D, 본인) =======================
P = "Paint"
out(P, "PaintPickColor", mix((0, pop(600, 900, 0.04)), (0.01, 0.4 * note1("E6", "celesta", 70, 0.15))), -12, 0.15, 0.05)
out(P, "PaintErase", mix((0, paper(0.09, seed=121)), (0.11, paper(0.09, seed=122))), -12, 0.24, 0.04)
out(P, "PaintReset", mix((0, 0.8 * whoosh(0.32, 3500, 800, seed=123)), (0.08, 0.4 * seq(["G6", "E6", "C6"], "celesta", 0.06, 75))), -9, 0.45, 0.1)
brush_len = 1.5 + 0.4
brush = bp(noise(brush_len, 124), 1200, 6500) * (0.55 + 0.45 * np.abs(np.sin(2 * np.pi * 2.25 * t_(brush_len))))  # 쓱쓱 4.5회/초
out_loop(P, "PaintStroke", brush, 1.5, 0.4, -30, peak_cap=-14)
out(P, "PaintSlotRegistered", mix((0, seq(["C6", "G6"], "marimba", 0.07, 90)), (0.1, 0.3 * sparkle(0.3, 6, base=98, seed=125))), -9, 0.45, 0.12)
out(P, "PaintForceFill", mix((0, wet(0.45, 320, 18, seed=126)), (0, 0.8 * whoosh(0.5, 2200, 400, seed=127)), (0.1, 0.35 * seq(["Eb6", "C6", "Ab5"], "celesta", 0.08, 75))), -7, 0.7, 0.15)

# ======================= 캐릭터 16 =======================
C = "Character"
for k, (f, sd) in enumerate(((620, 211), (700, 212), (560, 213), (760, 214))):  # Q3: 4개
    out(C, f"CookieStep_{k + 1}", mix((0, 0.8 * crunch(7, 0.025, 3000, 10000, seed=sd, size=0.008)), (0.004, 0.6 * soft_wood(f, 0.06, seed=sd))), -12, 0.08, 0.02)
out(C, "CookieJump", mix((0, pop(420, 1000, 0.09)), (0.02, 0.5 * note1("G5", "marimba", 85, 0.2)), (0, 0.25 * crunch(4, 0.02, seed=221))), -9, 0.3, 0.1)
out(C, "CookieLand", mix((0, soft_wood(480, 0.1, seed=222)), (0, 0.6 * crunch(8, 0.03, 2500, 9000, seed=223)), (0, 0.4 * low_thud(140, 0.12, 0.1))), -10, 0.15, 0.04)
out(C, "CookieDodge", mix((0, 0.8 * whoosh(0.26, 900, 3500, seed=224)), (0.02, 0.4 * paper(0.12, seed=225))), -10, 0.28, 0.05)
out(C, "CookieGrab", mix((0, pop(500, 800, 0.07)), (0.06, pop(650, 1100, 0.08)), (0.02, 0.5 * seq(["C5", "E5"], "marimba", 0.06, 80))), -9, 0.3, 0.08)
out(C, "CookieRelease", mix((0, pop(800, 600, 0.07)), (0.06, pop(650, 420, 0.08)), (0.1, 0.5 * soft_wood(560, 0.08, seed=226))), -9, 0.25, 0.06)
out(C, "CookieCrumble", reverb(mix((0, crunch(45, 0.6, 1500, 9000, seed=227, size=0.02)), (0.05, 0.5 * seq(["A4", "G4", "E4"], "marimba", 0.16, 80)),
                                    (0.25, 0.2 * sparkle(0.5, 8, base=91, seed=228)), (0, 0.3 * low_thud(120, 0.25, 0.1))), 0.6, 0.15), -6, 0.9, 0.2)
for k, (f, sd) in enumerate(((50, 231), (56, 232), (46, 233))):  # Q3: 3개 — 저역 쿵 + 질척 중역 + 고역 질감(거리 필터가 먼저 깎음)
    step = mix((0, low_thud(f, 0.55, 0.3)), (0.01, 0.55 * wet(0.22, 160, 30, seed=sd)), (0.015, 0.18 * hp(wet(0.15, 900, 50, seed=sd + 10), 1500)))
    out(C, f"MonsterStep_{k + 1}", reverb(step, 0.4, 0.1, bright=3000), -4, 0.5, 0.12)
out(C, "MonsterDash", reverb(mix((0, 0.9 * whoosh(0.4, 250, 1600, seed=234)), (0.05, 0.7 * wet(0.3, 220, 45, seed=235)), (0.28, 0.9 * low_thud(55, 0.3, 0.3)),
                                 (0, 0.25 * lp(noise(0.35, 236), 700) * shape(0.35, 0.05, 0.2))), 0.4, 0.1), -4, 0.6, 0.12)
grab = mix((0, 0.9 * growl(0.95, 92, seed=237)), (0, 0.6 * wet(0.6, 190, 25, seed=238)),
           (0.1, 0.35 * detune_layer(reverse(note1("F5", "tubular", 90, 1.0))[-int(0.5 * SR):], 35)), (0, 0.5 * sub_drone(0.9, 40) * shape(0.9, 0.05, 0.4)))
out(C, "MonsterGrab", reverb(grab, 0.5, 0.12), -4, 1.1, 0.2)
out(C, "MonsterSquash", reverb(mix((0, low_thud(44, 0.45, 0.25)), (0.02, 0.7 * wet(0.28, 130, 20, seed=239)), (0.01, 0.5 * crunch(14, 0.08, 2000, 8000, seed=240))), 0.4, 0.1), -4, 0.45, 0.1)
out(C, "MonsterAimLock", detune_layer(seq(["C7", "F#6"], "celesta", 0.06, 90), 30), -10, 0.2, 0.06)
out(C, "StunStars", reverb(seq(["C7", "E7", "G7", "E7", "C7", "G7", "E7"], "glock", 0.12, 75), 0.5, 0.15), -9, 1.0, 0.25)
out(C, "Respawn", mix((0, reverse(note1("C6", "tubular", 90, 0.8))[-int(0.25 * SR):]), (0.24, seq(["G5", "C6"], "marimba", 0.09, 95)), (0.3, 0.25 * sparkle(0.3, 5, base=96, seed=241))), -8, 0.7, 0.15)
out(C, "DoorOpen", mix((0, 0.5 * soft_wood(1200, 0.05, seed=242)), (0.03, creak(0.5, 30, 60, (600, 1400), seed=243))), -8, 0.6, 0.1)
out(C, "DoorClose", mix((0, soft_wood(300, 0.2, seed=244)), (0, 0.6 * low_thud(110, 0.2, 0.15)), (0.05, 0.4 * soft_wood(1300, 0.04, seed=245))), -7, 0.3, 0.08)

# ======================= 대기실 3 =======================
L = "Lobby"
bub_len = 8.0 + 1.0
bubbles = place(bub_len, [(RNG.uniform(0, bub_len - 0.2), RNG.uniform(0.3, 1.0) * pop(RNG.uniform(80, 170), RNG.uniform(180, 320), 0.09)) for _ in range(46)])
cauldron = mix((0, lp(bubbles, 900)), (0, 0.35 * wet(bub_len, 140, 6, seed=301)), (0, 0.05 * sub_drone(bub_len, 40)),
               (2.3, 0.06 * sparkle(0.6, 4, base=93, seed=302)), (6.1, 0.06 * sparkle(0.6, 4, base=95, seed=303)))
out_loop(L, "CauldronBubble", reverb(cauldron, 0.6, 0.15)[:int(bub_len * SR)], 8.0, 1.0, -24, peak_cap=-6)
out(L, "CauldronSplash", reverb(mix((0, 1.2 * wet(0.7, 250, 14, seed=304)), (0, 0.8 * low_thud(70, 0.4, 0.2)), (0.25, 0.45 * magic_signature(octave=-1, speed=0.07))), 0.8, 0.15), -4, 1.0, 0.25)
out(L, "MonsterDeparted", reverb(mix((0, inst([(0, 0.6, "Bb2", 100), (0.65, 0.9, "A2", 105)], GM["bassoon"])), (0, 0.7 * inst([(0, 0.6, "Bb1", 100), (0.65, 0.9, "A1", 105)], GM["contrabass"], gain=0.8)),
                                       (0.65, 0.4 * low_thud(50, 0.6, 0.2))), 1.0, 0.18, bright=2500), -6, 1.6, 0.4)

# ======================= 탈출 모드 16 =======================
E = "Escape"
out(E, "ChestOpen", mix((0, soft_wood(350, 0.2, seed=401)), (0.08, 0.5 * seq(["C6", "E6", "G6"], "celesta", 0.05, 80)), (0.15, 0.15 * sparkle(0.3, 4, base=98, seed=402))), -6, 0.6, 0.15)
out(E, "ChestRespawn", mix((0, 0.6 * reverse(note1("G5", "tubular", 85, 0.8))[-int(0.3 * SR):]), (0.3, soft_wood(330, 0.15, seed=403))), -8, 0.5, 0.1)
out(E, "ItemPickup", mix((0, pop(450, 900, 0.06)), (0.02, 0.5 * seq(["E5", "A5"], "marimba", 0.05, 85))), -8, 0.25, 0.06)
out(E, "ItemDrop", mix((0, soft_wood(420, 0.1, seed=404)), (0, 0.5 * crunch(6, 0.03, seed=405))), -8, 0.2, 0.05)
out(E, "DeviceInsert", mix((0, tin_metal(1200, 0.2, seed=406)), (0, 0.6 * soft_wood(800, 0.06, seed=407)), (0.04, 0.35 * note1("G6", "celesta", 80, 0.25))), -6, 0.4, 0.1)
spin = place(1.0, [(0.06 * i * (1 - i / 22), 0.6 * tin_metal(700 + 40 * i, 0.12, seed=410 + i)) for i in range(12)])
out(E, "DeviceComplete", reverb(mix((0, spin), (0.35, magic_signature()), (0.5, 0.5 * inst([(0, 1.0, p, 85) for p in ("C6", "E6", "G6", "C7")], GM["glock"]))), 1.0, 0.2), -2, 1.6, 0.4)
out(E, "BoardHop", mix((0, 1.2 * pop(380, 1100, 0.1)), (0.02, 0.5 * seq(["C5", "E5", "G5"], "marimba", 0.06, 90))), -6, 0.4, 0.1)
out(E, "BoardSuck", mix((0, 0.6 * reverse(note1("C6", "tubular", 90, 0.8))[-int(0.3 * SR):]), (0, 0.8 * whoosh(0.6, 400, 3000, seed=420)), (0.35, 0.4 * seq(["C6", "G6"], "celesta", 0.06, 85))), -6, 0.7, 0.15)
spring = np.sin(2 * np.pi * np.cumsum(300 + 600 * t_(0.25) / 0.25 + 40 * np.sin(2 * np.pi * 30 * t_(0.25))) / SR) * env(0.25, 0.002, 0.08)
out(E, "RocketInsert", mix((0, tin_metal(900, 0.25, seed=421)), (0.02, 0.4 * spring)), -6, 0.45, 0.1)
out(E, "RocketHatch", mix((0, tin_metal(500, 0.3, seed=422)), (0.15, 0.6 * steam(0.35, seed=423)), (0.45, 0.6 * tin_metal(450, 0.2, seed=424))), -6, 0.7, 0.12)
it = t_(2.2)
ignite = mix((0, fire_crackle(2.2, 40, seed=425) * np.linspace(0.2, 1, len(it))), (0, 0.7 * steam(2.2, seed=426) * np.linspace(0.1, 1, len(it)) / (np.exp(-it / 0.99) + 0.3)),
             (0, 0.7 * np.sin(2 * np.pi * np.cumsum(40 + 40 * it / 2.2) / SR) * np.linspace(0.1, 1, len(it))))
out(E, "RocketIgnite", ignite, -3, 2.2, 0.1)
lt = t_(4.5)
roar = lp(noise(4.5, 427), 1800) * np.exp(-lt / 2.0) * np.minimum(1, lt / 0.05)
lift = mix((0, roar), (0, 0.8 * np.sin(2 * np.pi * np.cumsum(60 + 50 * lt / 4.5) / SR) * np.exp(-lt / 1.6)),
           (0.3, 0.35 * np.sin(2 * np.pi * np.cumsum(600 + 1400 * t_(4.0) / 4.0) / SR) * np.exp(-t_(4.0) / 1.5)))
out(E, "RocketLiftoff", reverb(lift, 1.5, 0.15), -2, 4.5, 0.6)
cackle = place(2.2, [(0.19 * k, 0.8 * mix((0, growl(0.15, 290 - 18 * k, seed=430 + k)), (0, 0.6 * inst([(0, 0.13, n("A4") - 2 * k, 90)], GM["oboe"])))) for k in range(6)])
witch = mix((0, 1.3 * wind(5.0, 700, 0.3, seed=431) * shape(5.0, 2.5, 0.9)), (0.5, 0.6 * inst([(0, 4.0, "A3", 90), (0, 4.0, "C4", 85), (0, 4.0, "Eb4", 85)], GM["choir"])),
            (0, 0.6 * sub_drone(5.0, 33) * shape(5.0, 1.5, 1.0)), (2.4, 0.9 * cackle), (1.0, 0.25 * detune_layer(seq(["A5", "Eb6"], "celesta", 0.3, 70), 30)))
out(E, "WitchAppear", reverb(witch, 2.0, 0.2, bright=3000), -3, 5.0, 0.8)
glass = mix((0, tin_metal(2600, 0.8, seed=432)), (0, 0.5 * detune_layer(note1("C7", "tubular", 100, 1.0), 40)))
slam = mix((0, 1.4 * low_thud(30, 1.5, 0.5)), (0, 0.6 * lp(noise(0.8, 433), 500) * env(0.8, 0.002, 0.2)), (0.03, 0.6 * crunch(50, 0.8, 1500, 8000, seed=434, size=0.02)), (0.05, 0.25 * glass))
out(E, "WitchSlam", reverb(slam, 2.0, 0.2, bright=2500), -1, 1.8, 0.5)
out(E, "HeartBeat", mix((0, low_thud(55, 0.25, 0.3)), (0.18, 0.7 * low_thud(50, 0.25, 0.3))), -6, 0.5, 0.1)
out(E, "EscapeSuccessSelf", reverb(mix((0, seq(["C5", "E5", "G5", "A5", "G5", "C6"], "marimba", 0.09, 100)), (0, 0.4 * seq(["C6", "E6", "G6", "A6", "G6", "C7"], "celesta", 0.09, 80)),
                                        (0.5, 0.3 * sparkle(0.6, 10, base=98, seed=435))), 0.8, 0.15), -4, 1.4, 0.3)

# ======================= 도구 7 =======================
T = "Tool"
hum_len = 1.5 + 0.3; ht = t_(hum_len)
hum = (np.sin(2 * np.pi * 220 * ht) + 0.5 * np.sin(2 * np.pi * 330 * ht) + 0.25 * np.sign(np.sin(2 * np.pi * 440 * ht))) * (0.7 + 0.3 * np.sin(2 * np.pi * 12 * ht))
hum = lp(hum, 3000) + 0.1 * fire_crackle(hum_len, 8, seed=501)
out_loop(T, "StunAimHum", hum, 1.5, 0.3, -30, peak_cap=-12)
zap = np.sign(np.sin(2 * np.pi * np.cumsum(1800 * (400 / 1800) ** (t_(0.3) / 0.3)) / SR)) * env(0.3, 0.002, 0.1)
out(T, "StunFire", mix((0, 0.5 * lp(zap, 5000)), (0, 0.5 * fire_crackle(0.25, 60, seed=502))), -6, 0.35, 0.08)
out(T, "StunHit", mix((0, fire_crackle(0.45, 90, seed=503) * shape(0.45, 0.01, 0.2)), (0.05, 0.4 * note1("E7", "glock", 90, 0.3))), -6, 0.5, 0.1)
rub = np.sin(2 * np.pi * np.cumsum(200 + 300 * t_(0.2) / 0.2 + 25 * np.sin(2 * np.pi * 25 * t_(0.2))) / SR) * env(0.2, 0.01, 0.08)
out(T, "BalloonThrow", mix((0, 0.7 * whoosh(0.28, 700, 2500, seed=504)), (0, 0.3 * rub)), -9, 0.3, 0.06)
drops = place(0.8, [(0.05 + RNG.uniform(0, 0.6), 0.3 * pop(RNG.uniform(1200, 2400), RNG.uniform(600, 1200), 0.03)) for _ in range(10)])
out(T, "BalloonSplash", mix((0, 1.1 * pop(250, 120, 0.06)), (0, 0.9 * wet(0.5, 900, 25, seed=505)), (0.02, drops), (0, 0.5 * bp(noise(0.4, 506), 1500, 7000) * env(0.4, 0.002, 0.1))), -4, 0.8, 0.2)
out(T, "HammerSwing", whoosh(0.22, 800, 3000, seed=507), -9, 0.25, 0.05)
bonk = np.sin(2 * np.pi * np.cumsum(900 * (300 / 900) ** (t_(0.3) / 0.3)) / SR) * env(0.3, 0.001, 0.09)
out(T, "HammerBonk", mix((0, resonator(bonk, 600, 3) + bonk), (0, 0.6 * soft_wood(380, 0.1, seed=508))), -4, 0.35, 0.08)

# ======================= 맵 연출 25 (3D 60 m, 등불 10 m) =======================
D = "Device"
rt = t_(2.0)
out(D, "CakeRumble", mix((0, lp(noise(2.0, 601), 160) * 6 * (0.6 + 0.4 * np.sin(2 * np.pi * 9 * rt)) * shape(2.0, 0.3, 0.3)), (0, 0.5 * wet(2.0, 110, 9, seed=602)), (0, 0.4 * sub_drone(2.0, 42) * shape(2.0, 0.3, 0.3))), -3, 2.0, 0.3)
out(D, "CakeCrack", mix((0, crunch(30, 0.7, 2000, 9000, seed=603, size=0.015)), (0.1, 0.3 * tin_metal(2600, 0.3, seed=604)), (0.35, 0.25 * tin_metal(3100, 0.3, seed=605))), -4, 0.8, 0.15)
out(D, "CakeBurst", reverb(mix((0, 1.2 * low_thud(60, 0.6, 0.3)), (0, crunch(55, 0.5, 1500, 9000, seed=606, size=0.02)), (0, 0.8 * wet(0.5, 260, 20, seed=607)), (0.1, 0.35 * sparkle(0.8, 14, base=96, seed=608))), 1.0, 0.18), -1, 1.4, 0.3)
gliss = inst(notes_seq(["C4", "E4", "G4", "C5", "E5", "G5", "C6", "E6", "G6", "C7"], 0.24, dur=0.6, vel=80), GM["harp"])
rise = mix((0, 0.5 * place(3.0, [(0.25 * i, 0.5 * tin_metal(600 + 60 * i, 0.15, seed=610 + i)) for i in range(12)])), (0.1, 0.8 * gliss),
           (0, 0.6 * np.sin(2 * np.pi * np.cumsum(50 + 60 * t_(3.0) / 3.0) / SR) * shape(3.0, 0.3, 0.6)))
out(D, "CakeRocketRise", reverb(rise, 0.8, 0.15), -3, 3.0, 0.4)
ct = t_(2.0)
whistle = np.sin(2 * np.pi * np.cumsum(700 + 1100 * ct / 2.0) / SR) * np.linspace(0.1, 1, len(ct)) + 0.2 * bp(noise(2.0, 620), 1500, 4000) * np.linspace(0, 1, len(ct))
out(D, "CakeIgnite", mix((0, fire_crackle(2.0, 40, seed=621) * np.linspace(0.2, 1, len(ct))), (0, 0.45 * whistle)), -3, 2.0, 0.15)
lt5 = t_(5.0)
launch = mix((0, lp(noise(5.0, 622), 2200) * np.exp(-lt5 / 2.3) * np.minimum(1, lt5 / 0.05)), (0, 0.5 * np.sin(2 * np.pi * np.cumsum(500 + 1500 * lt5 / 5.0) / SR) * np.exp(-lt5 / 1.8)),
             (0.4, 0.3 * sparkle(3.5, 30, base=98, seed=623)), (1.2, 0.35 * note1("G6", "glock", 85, 1.5)), (0, 0.6 * low_thud(45, 1.5, 0.2)))
out(D, "CakeLaunch", reverb(launch, 1.5, 0.15), -2, 5.0, 0.8)
bulbs = place(2.6, [(0.2 * i + (0.12 if i in (5, 9) else 0), mix((0, 0.8 * soft_wood(2600, 0.03, seed=630 + i)), (0, 0.25 * fire_crackle(0.05, 200, seed=640 + i)),
                                                                  (0, 0.15 * note1(n("C5") + i, "vibes", 70, 0.15)))) for i in range(12)])
out(D, "CoasterBulbOn", bulbs, -6, 2.6, 0.2)
st = t_(3.0)
motor = lp(2 * ((np.cumsum(30 + 40 * st / 3.0) / SR) % 1) - 1, 400) * shape(3.0, 0.3, 0.4)
ticks = place(3.0, [(sum(0.25 * 0.9 ** j for j in range(i)), 0.4 * tin_metal(1500, 0.05, seed=650 + i)) for i in range(22)])
out(D, "CoasterStartup", mix((0, motor), (0, ticks), (2.0, 0.35 * detune_layer(note1("A4", "calliope", 85, 0.8), 25))), -4, 3.0, 0.3)
out(D, "CoasterSparks", fire_crackle(2.7, 160, seed=660) * shape(2.7, 0.15, 0.8), -10, 2.7, 0.3)
out(D, "CoasterLapBar", mix((0, tin_metal(600, 0.2, seed=661)), (0.18, tin_metal(560, 0.25, seed=662)), (0, 0.4 * soft_wood(300, 0.1, seed=663))), -5, 0.6, 0.1)
clack_times = []; t = 0.6; gap = 0.55
while t < 5.8: clack_times.append(t); t += gap; gap = max(0.12, gap * 0.88)
clacks = place(6.0, [(tt, mix((0, low_thud(120, 0.12, 0.2)), (0.06, 0.8 * low_thud(110, 0.12, 0.2)), (0, 0.15 * tin_metal(900, 0.06, seed=670)))) for tt in clack_times])
cal = tape_wobble(inst(notes_seq(["E5", "A5", "C6", "B5", "A5", "G#5", "A5"], 0.35, dur=0.5, vel=70), GM["calliope"]), 0.01, 0.6)
depart = mix((0, clacks), (0, 0.5 * lp(noise(6.0, 671), 300) * np.linspace(0.2, 1, int(6 * SR)) * shape(6.0, 0.5, 1.8)), (0.5, 0.3 * cal * np.linspace(1, 0.1, len(cal))))
out(D, "CoasterDepart", depart, -3, 6.0, 1.2)
g_len = 3.6 + 0.6  # 피스톤 0.9초 주기(연출 1.1 Hz) — 4번 + 톱니 24번, 교차 0.6초
gears = place(g_len, [(0.9 * k, mix((0, 0.8 * low_thud(70, 0.3, 0.2)), (0.04, 0.35 * steam(0.25, seed=700 + k)))) for k in range(5)] +
              [(0.15 * k, 0.3 * tin_metal(1300 + 80 * (k % 3), 0.05, seed=710 + k)) for k in range(28)])
gears += 0.2 * np.sin(2 * np.pi * 55 * t_(g_len)) + 0.08 * inst([(0, g_len, "C3", 60), (0, g_len, "G3", 50)], GM["choir"], length=g_len)
out_loop(D, "OvenGears", gears, 3.6, 0.6, -20, peak_cap=-6)
out(D, "OvenPiston", mix((0, 1.1 * steam(0.9, seed=720)), (0, low_thud(65, 0.5, 0.25))), -4, 1.0, 0.2)
dt3 = t_(3.0)
roll = mix((0, lp(noise(3.0, 721), 220) * 6 * (0.6 + 0.4 * np.sin(2 * np.pi * 3 * dt3)) * shape(3.0, 0.3, 0.6)), (0.2, 0.4 * creak(2.4, 12, 20, (300, 700), seed=722)), (2.55, 0.8 * low_thud(80, 0.4, 0.2)))
out(D, "OvenDoorRoll", roll, -3, 3.0, 0.3)
out(D, "OvenFlash", reverb(mix((0, 0.9 * whoosh(0.7, 300, 2200, seed=723)), (0.2, magic_signature(speed=0.06)), (0.3, 0.3 * sparkle(0.6, 12, base=100, seed=724))), 1.0, 0.18), -2, 1.25, 0.3)
puffs = []; t = 0.0; gap = 0.6
while t < 2.9: puffs.append(t); t += gap; gap = max(0.17, gap * 0.8)
out(D, "TrainPuff", place(3.0, [(tt, 0.8 * mix((0, steam(0.28, seed=730 + i)), (0, 0.4 * lp(noise(0.2, 740 + i), 600) * env(0.2, 0.005, 0.06)))) for i, tt in enumerate(puffs)]), -5, 3.0, 0.2)
wt = t_(1.3)
wh = sum(a * np.sin(2 * np.pi * f * (1 + 0.02 * np.exp(-wt / 0.08)) * wt) for f, a in ((622, 1), (784, 0.8), (932, 0.6))) * shape(1.3, 0.08, 0.35)
out(D, "TrainWhistle", reverb(mix((0, wh), (0, 0.3 * bp(noise(1.3, 750), 1500, 6000) * shape(1.3, 0.05, 0.3))), 1.0, 0.15), -4, 1.4, 0.3)
chug_t = []; t = 0.0; gap = 0.5
while t < 6.3: chug_t.append(t); t += gap; gap = max(0.13, gap * 0.9)
chug = place(6.5, [(tt, mix((0, 0.7 * steam(0.18, seed=760 + i % 9)), (0, 0.5 * low_thud(90, 0.12, 0.2)), (0.02, 0.3 * soft_wood(700 + 60 * (i % 2), 0.05, seed=770 + i % 7)))) for i, tt in enumerate(chug_t)])
out(D, "TrainChug", mix((0, chug * shape(6.5, 0.05, 1.5)), (1.0, 0.25 * wet(4.0, 150, 4, seed=780))), -3, 6.5, 1.0)
out(D, "TrainTunnel", reverb(mix((0, 0.9 * whoosh(1.2, 600, 150, seed=781)), (0, 0.5 * low_thud(50, 1.0, 0.1))), 2.0, 0.45, bright=1500), -5, 2.5, 0.6)
a_len = 6.0 + 1.0; at_ = t_(a_len)
glass_hum = sum(a * np.sin(2 * np.pi * f * at_) for f, a in ((261.5, 0.4), (392.0, 0.3), (523.0, 0.2), (784.5, 0.1)))  # 6초에 정수 주기
altar = mix((0, inst([(0, a_len, "C3", 75), (0, a_len, "G3", 70), (0, a_len, "E4", 65)], GM["choir"], length=a_len)), (0, 0.4 * glass_hum), (0, 0.25 * sub_drone(a_len, 41, 0.1)))
out_loop(D, "AltarHum", altar[:int(a_len * SR)], 6.0, 1.0, -22, peak_cap=-6)
out(D, "AltarGlow", reverb(mix((0, magic_signature(speed=0.12)), (0, 0.4 * inst([(0, 1.8, "C4", 70), (0, 1.8, "G4", 65), (0.4, 1.4, "E5", 60)], GM["choir"])), (0.3, 0.3 * sparkle(1.6, 24, base=96, seed=790))), 1.2, 0.2), -3, 2.0, 0.4)
out(D, "PortalOpen", reverb(mix((0, 0.8 * whoosh(2.0, 200, 1800, seed=791)), (0.6, magic_signature()), (0.3, 0.45 * inst([(0, 1.6, "C4", 80), (0, 1.6, "G4", 70), (0.5, 1.1, "E5", 70)], GM["choir"])),
                                (0, 0.3 * sub_drone(2.0, 42) * shape(2.0, 0.5, 0.6))), 1.5, 0.22), -2, 2.0, 0.4)
out(D, "PortalFlash", reverb(mix((0, 1.1 * low_thud(50, 0.8, 0.3)), (0, 0.6 * inst([(0, 1.0, p, 100) for p in ("C5", "E5", "G5", "C6")], GM["tubular"])), (0, 0.45 * sparkle(0.8, 30, base=98, seed=792))), 1.2, 0.2), -1, 1.2, 0.35)
out(D, "PortalClose", mix((0, whoosh(1.0, 2000, 200, seed=793)), (1.0, 0.8 * pop(900, 500, 0.08)), (0.2, 0.3 * seq(["G6", "E6", "C6"], "celesta", 0.15, 70))), -4, 1.2, 0.15)
lt_ = t_(0.35)
out(D, "LanternFlicker", mix((0, fire_crackle(0.3, 50, seed=794)), (0, 0.15 * np.sign(np.sin(2 * np.pi * 120 * lt_)) * shape(0.35, 0.02, 0.2))), -14, 0.35, 0.08)

# ======================= 환경음 7 (반복, 아주 조용 — OGG) =======================
A = "Ambience"
r = np.random.default_rng(900)


def scatter(total, count, make):
    return place(total, [(r.uniform(0, total - 0.5), make(i)) for i in range(count)])


T61 = 61.0
candy = mix((0, wind(T61, 600, 0.12, seed=901)),
            (0, 0.25 * scatter(T61, 22, lambda i: inst([(0, 1.5, ["C6", "E6", "G6", "A6", "C7", "D6"][i % 6], 45)], GM["glock"]))),
            (0, 0.2 * scatter(T61, 30, lambda i: paper(0.25, seed=902 + i))),
            (0, 0.08 * scatter(T61, 8, lambda i: sparkle(1.5, 6, base=100, seed=940 + i))),
            (0, 0.05 * sub_drone(T61, 41, 0.05)))
out_loop(A, "AmbCandyForest", reverb(candy, 0.6, 0.12)[:int(T61 * SR)], 60.0, 1.0, -24, ogg=True)  # S9: 바탕음 −30 → −24 LUFS(공장 기계·가마솥과 같은 수준)
ticks = place(T61, [(float(i), mix((0, soft_wood(1700 if i % 2 else 1300, 0.08, seed=950 + i % 5)), (0, 0.2 * tin_metal(3000, 0.05, seed=950)))) for i in range(61)])
ginger = mix((0, 0.8 * wind(T61, 450, 0.08, seed=951)), (0, 0.3 * ticks),
             (0, 0.25 * scatter(T61, 6, lambda i: creak(0.6, 25, 45, (500, 1200), seed=960 + i))),
             (14.0, 0.25 * reverb(note1("F3", "tubular", 70, 2.5), 2.0, 0.4)), (44.0, 0.2 * reverb(note1("C4", "tubular", 65, 2.5), 2.0, 0.4)))
out_loop(A, "AmbGingerbread", reverb(ginger, 0.8, 0.15)[:int(T61 * SR)], 60.0, 1.0, -24, ogg=True)  # S9: 바탕음 −30 → −24 LUFS(공장 기계·가마솥과 같은 수준)
T17 = 17.0
fbub = place(T17, [(r.uniform(0, 16.5), pop(r.uniform(80, 140), r.uniform(150, 240), 0.09) * r.uniform(0.4, 1)) for _ in range(40)])
factory = mix((0, 0.25 * (np.sin(2 * np.pi * 55 * t_(T17)) + 0.4 * np.sin(2 * np.pi * 110 * t_(T17)))),
              (0, 0.45 * place(T17, [(0.5 + 2.0 * i, steam(0.6, seed=970 + i)) for i in range(8)])),
              (0, 0.35 * place(T17, [(1.0 + 1.0 * i, mix((0, 0.5 * tin_metal(700, 0.3, seed=980)), (0, 0.6 * low_thud(80, 0.3)))) for i in range(16)])),
              (0, 0.3 * place(T17, [(0.25 * i, tin_metal(1400, 0.04, seed=990)) for i in range(66)])), (0, 0.6 * lp(fbub, 600)))
out_loop(A, "AmbFactory", reverb(factory, 1.0, 0.15)[:int(T17 * SR)], 16.0, 1.0, -24, ogg=True)
box_phrase = lambda i: tape_wobble(inst(notes_seq(["A5", "C6", "E6", "D6", "C6", "B5", "C6", "A5"][i % 2::1], 0.6, dur=0.9, vel=55), GM["musicbox"]), 0.014, 0.4)
laugh = place(1.2, [(0.18 * k, growl(0.12, 330 - 15 * k, seed=1000 + k)) for k in range(4)])
carnival = mix((0, 0.9 * wind(T61, 500, 0.1, seed=1001)), (6.0, 0.35 * reverb(box_phrase(0), 1.8, 0.5)), (33.0, 0.3 * reverb(box_phrase(1), 1.8, 0.5)),
               (0, 0.2 * scatter(T61, 10, lambda i: paper(0.4, seed=1010 + i))), (0, 0.25 * scatter(T61, 6, lambda i: creak(0.7, 20, 35, (400, 900), seed=1020 + i))),
               (24.5, 0.04 * reverb(laugh, 2.0, 0.6)))  # Q4: 거의 안 들리게
out_loop(A, "AmbCarnival", carnival[:int(T61 * SR)], 60.0, 1.0, -24, ogg=True)  # S9: 바탕음 −30 → −24 LUFS(공장 기계·가마솥과 같은 수준)
ghost = lambda p: detune_layer(inst([(0, 4.0, p, 50)], GM["oohs"]), 25)
bakery = mix((0, 0.6 * fire_crackle(T61, 20, seed=1030)), (0, 0.15 * np.sin(2 * np.pi * 70 * t_(T61)) * (0.7 + 0.3 * np.sin(2 * np.pi * 0.25 * t_(T61)))),
             (12.0, 0.22 * ghost("E3")), (41.0, 0.2 * ghost("D3")), (0, 0.35 * wind(T61, 300, 0.06, seed=1031)),
             (0, 0.15 * scatter(T61, 12, lambda i: paper(0.5, seed=1040 + i))))
out_loop(A, "AmbBakery", reverb(bakery, 1.0, 0.15)[:int(T61 * SR)], 60.0, 1.0, -24, ogg=True)  # S9: 바탕음 −30 → −24 LUFS(공장 기계·가마솥과 같은 수준)
drips = place(T61, [(r.uniform(0, 60), pop(r.uniform(1500, 2400), r.uniform(800, 1200), 0.03) * r.uniform(0.4, 1)) for _ in range(30)])
cave = mix((0, 0.6 * sub_drone(T61, 36, 0.06)), (0, 0.5 * wind(T61, 300, 0.05, seed=1050)), (0, 0.6 * drips),
           (8.0, 0.1 * inst([(0, 10, "C4", 50), (0, 10, "G4", 40)], GM["choir"])), (38.0, 0.08 * inst([(0, 10, "C4", 50), (0, 10, "E4", 40)], GM["choir"])))
out_loop(A, "AmbCave", reverb(cave, 2.5, 0.45, bright=3000)[:int(T61 * SR)], 60.0, 1.0, -26, ogg=True)

import final_music  # 회전목마(CarouselSpin)는 곡이라 음악 모듈에서 만든다
x = final_music.carousel_spin()
path = os.path.join(ROOT, A, "CarouselSpin.ogg")
write_ogg(path, norm_lufs(x, -20), -3); made.append((A, "CarouselSpin", len(x) / SR, peak_db(x)))

print(f"{len(made)} files")
for f, nme, ln, pk in made: print(f"{f:10s} {nme:22s} {ln:6.2f}s  peak {pk:6.1f} dB")
