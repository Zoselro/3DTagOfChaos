# P1 정체성 견본(AudioProductionPlan.md §10 P1): 모티프 3 · Cookie · Monster · Magic · Environment 팔레트 + 맵 5곳 10초 스케치.
# 원본(개별 파일)은 out/P1/<묶음>/ 에, 들어 보기용 묶음은 out/P1/reels/ 에 저장한다. 게임에 넣는 파일이 아니다(방향 확인용).
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from audiolib import *

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "P1")
guide = []


def save(group, name, x, peak):
    write(os.path.join(OUT, group, name + ".wav"), x, peak)
    return (name, x, peak)


def section(title, reel_name, items):
    log = reel(os.path.join(OUT, "reels", reel_name + ".wav"), items)
    guide.append(f"\n### {reel_name}.wav — {title}\n")
    guide.extend(f"- {line}" for line in log)


# =============== 모티프 3 ===============
cookie_motif = mix((0, inst(notes_seq(["C5", "E5", "G5", "A5", "G5"], 0.15, vel=95), GM["marimba"])),
                   (0, 0.35 * inst(notes_seq(["C6", "E6", "G6", "A6", "G6"], 0.15, vel=70), GM["celesta"])))
cookie_motif = reverb(cookie_motif, 0.8, 0.18)

monster_motif = mix((0, inst([(0, 1.1, "Bb1", 110), (1.2, 1.6, "A1", 115)], GM["contrabass"], gain=0.8)),
                    (0, 0.7 * inst([(0, 1.1, "Bb2", 100), (1.2, 1.6, "A2", 105)], GM["bassoon"])),
                    (0, 0.5 * sub_drone(3.2, 36) * shape(3.2, 0.4, 0.8)),
                    (1.2, 0.35 * low_thud(45, 0.8, 0.2)))
monster_motif = reverb(monster_motif, 1.4, 0.2, bright=2500)


def magic_signature(octave=0, minor=False, detune=0):
    root = 6 + octave
    bell = reverse(inst([(0, 1.0, f"C{root}", 100)], GM["tubular"]))[-int(0.45 * SR):]
    third = f"Eb{root}" if minor else f"E{root}"
    seventh = f"Bb{root}" if minor else f"B{root}"
    cel = inst(notes_seq([f"C{root}", third, f"G{root}", seventh], 0.09, dur=0.5, vel=90), GM["celesta"])
    if detune: cel = detune_layer(cel, detune)
    spk = sparkle(0.6, 10, base=12 * (root + 1) + 12, seed=5) * 0.25
    return reverb(mix((0, bell), (0.42, cel), (0.5, spk), (0.3, 0.12 * whoosh(0.5, 2000, 6000, seed=3))), 1.2, 0.3)


magic_sig = magic_signature()
items = [save("motifs", "motif_cookie", cookie_motif, -3), save("motifs", "motif_monster", monster_motif, -3),
         save("motifs", "motif_magic_signature", magic_sig, -3)]
section("모티프 3개: 쿠키(로비 곡 A에서 따온 5음) → 괴물(낮은 반음 B♭→A) → 마법 서명(역재생 종 → 셀레스타 4음)", "P1_1_Motifs", items)

# =============== Cookie 팔레트 ===============
ck = []
for k, (f, seed) in enumerate(((620, 11), (700, 12), (560, 13), (760, 14))):
    step = mix((0, 0.8 * crunch(7, 0.025, 3000, 10000, seed=seed, size=0.008)), (0.004, 0.6 * soft_wood(f, 0.06, seed=seed)))
    ck.append(save("cookie", f"CookieStep_{k + 1}", step, -12))
jump = mix((0, pop(420, 1000, 0.09)), (0.02, 0.5 * inst([(0, 0.2, "G5", 85)], GM["marimba"])), (0, 0.25 * crunch(4, 0.02, seed=21)))
land = mix((0, soft_wood(480, 0.1, seed=22)), (0, 0.6 * crunch(8, 0.03, 2500, 9000, seed=23)), (0, 0.4 * low_thud(140, 0.12, 0.1)))
dodge = mix((0, 0.8 * whoosh(0.26, 900, 3500, seed=24)), (0.02, 0.4 * paper(0.12, seed=25)))
grab = mix((0, pop(500, 800, 0.07)), (0.06, pop(650, 1100, 0.08)), (0.02, 0.5 * inst(notes_seq(["C5", "E5"], 0.06, dur=0.2, vel=80), GM["marimba"])))
release = mix((0, pop(800, 600, 0.07)), (0.06, pop(650, 420, 0.08)), (0.1, 0.5 * soft_wood(560, 0.08, seed=26)))
crumb = mix((0, crunch(45, 0.6, 1500, 9000, seed=27, size=0.02)),
            (0.05, 0.5 * inst(notes_seq(["A4", "G4", "E4"], 0.16, dur=0.4, vel=80), GM["marimba"])),
            (0.25, 0.2 * sparkle(0.5, 8, base=91, seed=28)), (0, 0.3 * low_thud(120, 0.25, 0.1)))
ck += [save("cookie", "CookieJump", cut(jump, 0.3), -9), save("cookie", "CookieLand", land, -10), save("cookie", "CookieDodge", dodge, -10),
       save("cookie", "CookieGrab", cut(grab, 0.32), -9), save("cookie", "CookieRelease", release, -9), save("cookie", "CookieCrumble", cut(reverb(crumb, 0.6, 0.15), 1.0, 0.2), -6)]
section("Cookie 팔레트: 발소리 4종(바삭+나무 톡, 작게) → 점프 → 착지 → 회피 → 들기 → 내려놓기 → 부서짐", "P1_2_Cookie", ck)

# =============== Monster 팔레트 ===============
mo = []
for k, (f, seed) in enumerate(((50, 31), (56, 32), (46, 33))):
    step = mix((0, low_thud(f, 0.55, 0.3)), (0.01, 0.55 * wet(0.22, 160, 30, seed=seed)), (0.015, 0.18 * hp(wet(0.15, 900, 50, seed=seed + 10), 1500)))
    mo.append(save("monster", f"MonsterStep_{k + 1}", cut(reverb(step, 0.5, 0.12, bright=3000), 0.55, 0.15), -4))
far = reverb(lp(mo[0][1], 380), 0.9, 0.35, bright=1500) * 0.3
mo.append(("MonsterStep_far_preview (게임에서 25 m 쯤 들리는 모습 흉내 — 거리 필터·감쇠)", far, -16))
dash = mix((0, 0.9 * whoosh(0.4, 250, 1600, seed=34)), (0.05, 0.7 * wet(0.3, 220, 45, seed=35)), (0.3, 0.9 * low_thud(55, 0.45, 0.3)),
           (0, 0.25 * lp(noise(0.35, 36), 700) * shape(0.35, 0.05, 0.2)))
grabm = mix((0, 0.9 * growl(0.95, 92, seed=37)), (0, 0.6 * wet(0.6, 190, 25, seed=38)),
            (0.1, 0.35 * detune_layer(reverse(inst([(0, 1, "F5", 90)], GM["tubular"]))[-int(0.5 * SR):], 35)),
            (0, 0.5 * sub_drone(0.9, 40) * shape(0.9, 0.05, 0.4)))
squash = mix((0, low_thud(44, 0.55, 0.25)), (0.02, 0.7 * wet(0.28, 130, 20, seed=39)), (0.01, 0.5 * crunch(14, 0.08, 2000, 8000, seed=40)))
breath = mix((0, 0.6 * bp(noise(1.6, 41), 200, 900) * shape(1.6, 0.6, 0.6)), (0.2, 0.25 * growl(1.0, 70, seed=42)))
mo += [save("monster", "MonsterDash", cut(reverb(dash, 0.5, 0.12), 0.75, 0.15), -4), save("monster", "MonsterGrab", cut(reverb(grabm, 0.7, 0.15), 1.2, 0.2), -4),
       save("monster", "MonsterSquash", cut(reverb(squash, 0.5, 0.1), 0.5, 0.12), -4), save("monster", "monster_breath_texture", reverb(breath, 0.8, 0.2), -10)]
section("Monster 팔레트: 발소리 3종(저음 쿵 + 질척 중역 + 고역 질감) → 멀리서 들리는 발소리 흉내 → 돌진 → 잡기(으르렁) → 눌림 → 숨결 질감", "P1_3_Monster", mo)

# =============== Magic 팔레트 ===============
cauldron = mix((0, magic_signature(octave=-1)), (0, 0.6 * reverb(place(1.6, [(0.1 + 0.18 * i, pop(160 + 40 * i, 320 + 60 * i, 0.07)) for i in range(7)]), 0.4, 0.2)[:int(1.6 * SR)]), (0, 0.4 * wet(1.6, 150, 8, seed=51)))
portal = mix((0, 0.8 * whoosh(2.0, 200, 1800, seed=52)), (0.6, magic_signature()), (0.3, 0.45 * inst([(0, 1.6, "C4", 80), (0, 1.6, "G4", 70), (0.5, 1.1, "E5", 70)], GM["choir"])),
             (0, 0.3 * sub_drone(2.2, 42) * shape(2.2, 0.5, 0.6)))
witch = mix((0, 1.2 * wind(3.5, 700, 0.3, seed=53) * shape(3.5, 1.5, 0.8)),
            (0.4, 0.7 * inst([(0, 2.6, "A3", 95), (0, 2.6, "C4", 90), (0, 2.6, "Eb4", 90)], GM["choir"])),
            (0, 0.6 * sub_drone(3.5, 33) * shape(3.5, 1.0, 1.0)), (1.6, 0.6 * magic_signature(minor=True, detune=30)))
paint = mix((0, 0.35 * sparkle(0.4, 6, base=96, seed=54)), (0, 0.5 * paper(0.3, seed=55)), (0.05, 0.3 * inst([(0, 0.3, "E6", 70)], GM["celesta"])))
mg = [save("magic", "magic_signature", magic_sig, -6), save("magic", "magic_cauldron", reverb(cauldron, 1.0, 0.2), -6),
      save("magic", "magic_portal", reverb(portal, 1.5, 0.25), -3), save("magic", "magic_witch_dark", reverb(witch, 2.0, 0.25, bright=3000), -3),
      save("magic", "magic_paint_small", paint, -12)]
section("Magic 팔레트: 서명 → 가마솥 변형(낮게 + 거품) → 포탈 변형(소용돌이 + 합창) → 마녀 변형(단조·디튠 + 저음 + 바람) → 색칠 변형(아주 작게)", "P1_4_Magic", mg)

# =============== Environment 재료(각 6초, 조용하게) ===============
E = 6.0
chimes = place(E, [(at, inst([(0, 1.2, p, 50)], GM["glock"])) for at, p in ((0.6, "E6"), (2.4, "G6"), (3.1, "C7"), (4.8, "A6"))])
candy = mix((0, wind(E, 600, 0.15, seed=61)), (0, 0.3 * chimes), (0, 0.25 * place(E, [(0.9 + 1.3 * i, paper(0.2, seed=62 + i)) for i in range(4)])),
            (2.0, 0.08 * sparkle(2.0, 6, base=100, seed=63)), (0, 0.06 * sub_drone(E, 41)))
ticks = place(E, [(i, mix((0, soft_wood(1700 if i % 2 else 1300, 0.08, seed=70 + i)), (0, 0.2 * tin_metal(3000, 0.05, seed=70)))) for i in range(6)])
ginger = mix((0, 0.8 * wind(E, 450, 0.1, seed=64)), (0, 0.35 * ticks), (4.5, 0.2 * reverb(inst([(0, 1.4, "F3", 70)], GM["tubular"]), 1.5, 0.4)))
bubbles = place(E, [(0.2 + 0.37 * i, pop(90 + 15 * (i % 4), 160 + 20 * (i % 3), 0.09)) for i in range(15)])
factory = mix((0, 0.3 * (np.sin(2 * np.pi * 55 * t_(E)) + 0.4 * np.sin(2 * np.pi * 110 * t_(E)))),
              (0, 0.5 * place(E, [(0.5 + 1.5 * i, steam(0.5, seed=65 + i)) for i in range(4)])),
              (0, 0.35 * place(E, [(1 + 2 * i, mix((0, tin_metal(700, 0.4, seed=66)), (0, 0.6 * low_thud(80, 0.3)))) for i in range(3)])),
              (0, 0.6 * lp(bubbles, 600)))
box = tape_wobble(inst(notes_seq(["A5", "C6", "E6", "D6", "C6", "B5", "C6", "A5"], 0.6, dur=0.9, vel=60), GM["musicbox"]), 0.012, 0.45)
carnival = mix((0, 0.9 * wind(E, 500, 0.12, seed=67)), (0.3, 0.45 * reverb(box, 1.5, 0.45)),
               (0, 0.2 * place(E, [(2.2 + 2.1 * i, 0.5 * soft_wood(300, 0.4, seed=68)) for i in range(2)])))
ghost = detune_layer(inst([(0, 3.5, "E3", 50)], GM["oohs"]), 25)
bakery = mix((0, 0.6 * fire_crackle(E, 22, seed=69)), (0, 0.2 * np.sin(2 * np.pi * 70 * t_(E)) * (0.7 + 0.3 * np.sin(2 * np.pi * 0.3 * t_(E)))),
             (1.5, 0.25 * ghost), (0, 0.4 * wind(E, 300, 0.08, seed=70)))
drips = place(E, [(0.5 + 1.25 * i, pop(1800 + 300 * (i % 3), 900, 0.03)) for i in range(5)])
cave = mix((0, 0.5 * sub_drone(E, 36, 0.08)), (0, 0.5 * wind(E, 300, 0.05, seed=71)), (0, 0.5 * drips),
           (2.5, 0.12 * inst([(0, 3, "C4", 50), (0, 3, "G4", 40)], GM["choir"])))
en = [save("environment", "env_candy_wind_chimes", reverb(candy, 0.6, 0.15), -14),
      save("environment", "env_gingerbread_clock", cut(reverb(ginger, 0.8, 0.2), 7.0, 0.8), -14),
      save("environment", "env_factory_steam_chocolate", reverb(factory, 1.2, 0.2), -14),
      save("environment", "env_carnival_musicbox_wind", carnival, -14),
      save("environment", "env_bakery_fire_ghost", reverb(bakery, 1.0, 0.2), -14),
      save("environment", "env_cave_underground", reverb(cave, 2.5, 0.45, bright=3000), -14)]
section("Environment 재료(각 6초, 실제보다 크게 들려 드림 — 게임에서는 훨씬 작음): 캔디숲 → 진저브레드 시계 → 공장 증기·초콜릿 거품 → 놀이공원 오르골 → 베이커리 장작·유령 → 지하 동굴", "P1_5_Environment", en)

# =============== 맵 5곳 10초 스케치(배경음 맛보기 + 환경 + 대표 사건음) ===============
S = 10.0


def bars(bpm, beats): return beats * 60 / bpm


def sketch_candy():
    b = 60 / 104
    mel = notes_seq(["E5", "G5", "C6", "B5", "A5", "G5", "E5", "G5", "A5", "C6", "D6", "C6", "G5", "E5", "D5", "C5"], b * 0.5, vel=90)
    harp = [(i * b, b, p, 60) for i, p in enumerate(["C4", "E4", "G4", "C5"] * 4)]
    flute = [(b * 4, b * 4, "E5", 60), (b * 8, b * 4, "F5", 60), (b * 12, b * 4, "E5", 60)]
    music = mix((0, inst(mel, GM["marimba"])), (0, 0.5 * inst(harp, GM["harp"])), (0, 0.35 * inst(flute, GM["flute"])), (0, 0.15 * inst(mel[::2], GM["celesta"])))
    return mix((0, reverb(music, 0.6, 0.15)), (0, 0.25 * np.pad(candy, (0, int(S * SR)))[:int(S * SR)]), (7.5, 0.5 * magic_sig))


def sketch_ginger():
    b = 60 / 96
    acc = [(i * b, b * 0.9, p, 70) for i, p in enumerate(["F4", "A4", "C5", "A4", "Bb4", "D5", "F5", "D5", "C5", "E5", "G5", "E5", "F4", "A4", "C5", "F5"])]
    glock = notes_seq(["C6", "A5", "F5", "A5", "D6", "Bb5", "G5", "E5"], b * 2, dur=b, vel=70)
    pizz = [(i * b, b * 0.5, p, 80) for i, p in enumerate(["F2", "C3", "Bb2", "F3", "C3", "G3", "F2", "C3"] * 2)]
    music = mix((0, 0.7 * inst(acc, GM["accordion"])), (0, 0.5 * inst(glock, GM["glock"])), (0, 0.6 * inst(pizz, GM["pizz"])))
    return mix((0, reverb(music, 0.8, 0.18)), (0, 0.35 * np.pad(ginger, (0, int(4 * SR))) [:int(S * SR)]))


def sketch_factory():
    b = 60 / 112
    wood = [(i * b * 0.5, 0.1, 76 if i % 4 == 0 else 77, 100 if i % 2 == 0 else 70) for i in range(36)]
    clar = notes_seq(["D5", "F5", "A5", "G5", "F5", "E5", "D5", "A4"], b, dur=b * 0.9, vel=85)
    bass = notes_seq(["D2", "D2", "A1", "A1", "Bb1", "Bb1", "A1", "A1"] * 2, b, dur=b * 0.6, vel=95)
    music = mix((0, 0.6 * inst(wood, 0, drum=True)), (0.0, 0.6 * inst(clar, GM["clarinet"])), (0, 0.7 * inst(bass, GM["bassoon"])))
    return mix((0, reverb(music, 1.2, 0.18)), (0, 0.3 * np.pad(factory, (0, int(4 * SR)))[:int(S * SR)]), (6.5, 0.5 * reverb(mix((0, steam(0.8, seed=80)), (0, 0.5 * tin_metal(500, 0.5))), 1.2, 0.3)))


def sketch_carnival():
    b = 60 / 96
    waltz = []
    prog = [("A2", ["C4", "E4"]), ("E2", ["B3", "E4"]), ("A2", ["C4", "E4"]), ("D2", ["D4", "F4"]), ("E2", ["G#3", "D4"])]
    for k in range(5):
        root, ch = prog[k]
        waltz.append((k * 3 * b, b * 0.8, root, 85))
        for j in (1, 2): waltz += [(k * 3 * b + j * b, b * 0.6, p, 65) for p in ch]
    mel = notes_seq(["E5", "A5", "C6", "B5", "A5", "G#5", "A5", "B5", "C6", "E6", "D6", "C6", "B5", "E5", "A5"], b, dur=b * 0.9, vel=80)
    music = mix((0, 0.8 * tape_wobble(inst(mel, GM["musicbox"]), 0.01, 0.5)), (0, 0.45 * inst(mel, GM["calliope"])),
                (0, 0.5 * detune_layer(inst(waltz, GM["piano"]), 18)))
    return mix((0, reverb(music, 0.9, 0.2)), (0, 0.3 * np.pad(carnival, (0, int(4 * SR)))[:int(S * SR)]))


def sketch_bakery():
    b = 60 / 88
    hc = [(i * b * 0.5, b * 0.45, p, 75) for i, p in enumerate(["E4", "G4", "B4", "E5", "C4", "E4", "A4", "C5", "D4", "F#4", "A4", "D5", "B3", "D#4", "F#4", "B4"] * 2)]
    cel = notes_seq(["B5", "G5", "E5", "F#5", "G5", "A5", "B5", "E6"], b * 1.5, dur=b, vel=70)
    low = [(0, b * 4, "E2", 70), (b * 4, b * 4, "C2", 70), (b * 8, b * 4, "D2", 70), (b * 12, b * 2, "B1", 70)]
    music = mix((0, 0.6 * inst(hc, GM["harpsichord"])), (0, 0.5 * inst(cel, GM["celesta"])), (0, 0.5 * inst(low, GM["strings"])),
                (b * 4, 0.12 * inst([(0, b * 6, "E4", 50), (0, b * 6, "B4", 45)], GM["choir"])))
    return mix((0, reverb(music, 1.0, 0.2)), (0, 0.3 * np.pad(bakery, (0, int(4 * SR)))[:int(S * SR)]))


sk = []
for name, fn in (("map_CandyForest", sketch_candy), ("map_Gingerbread", sketch_ginger), ("map_ChocolateFactory", sketch_factory),
                 ("map_CursedCarnival", sketch_carnival), ("map_HauntedBakery", sketch_bakery)):
    x = fn()[:int(S * SR)]
    x = fade(x, 0.01, 1.0)
    sk.append(save("map_sketches", name, x, -3))
section("맵 5곳 10초 스케치(배경음 악기·조성·템포 + 환경 재료): 캔디숲 C장조 104 → 진저브레드 F장조 96 → 공장 D단조 112 → 놀이공원 A단조 3/4 96 → 베이커리 E단조 88", "P1_6_MapSketches", sk)

with open(os.path.join(OUT, "listening_guide.md"), "w", encoding="utf-8") as f:
    f.write("# P1 정체성 견본 — 듣기 안내\n\n각 묶음 파일(reels/)에서 몇 초에 어떤 소리가 나오는지 적었다. 개별 원본은 같은 폴더의 하위 폴더에 있다.\n")
    f.write("\n".join(guide) + "\n")
print("\n".join(guide))
import glob
print("\n---- check")
for group in ("cookie", "monster"):
    for f in sorted(glob.glob(os.path.join(OUT, group, "*.wav"))):
        import wave as _w
        with _w.open(f) as w: x = np.frombuffer(w.readframes(w.getnframes()), np.int16) / 32768
        print(f"{group:8s} {os.path.basename(f):28s} {len(x) / SR:5.2f}s  centroid {centroid(x):6.0f} Hz  peak {peak_db(x):5.1f} dB")
