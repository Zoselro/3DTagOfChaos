# S3 시험곡(표준 라이브러리). 맵마다 조성·템포를 달리하고 Paint/Hunt는 같은 조성·템포(Hunt는 저음 박동 추가).
# 정식 곡은 SoundPlan S8에서 MIDI → FluidSynth로 같은 이름에 덮어쓴다.
from synth_lib import *
R = "out/BGM/"
def loop(path, bpm, chords, beats_per_bar=4, hunt=False, timbre=(1, 0.25, 0.08), arp_octave=0):
    beat = 60 / bpm; bars = len(chords); total = bars * beats_per_bar * beat
    parts = []
    for b, ch in enumerate(chords):
        start = b * beats_per_bar * beat
        steps = beats_per_bar * 2
        for k in range(steps):
            m = ch[k % len(ch)] + 12 * arp_octave + (12 if k >= steps // 2 else 0)
            parts.append((start + k * beat / 2, tone(note(m), beat * 1.6, d=beat * 0.6, harm=timbre, gain=0.5)))
        parts.append((start, tone(note(ch[0] - 24), beats_per_bar * beat, a=0.02, d=beats_per_bar * beat * 0.5, harm=(1, 0.4), gain=0.6)))
        if hunt:
            for k in range(beats_per_bar):
                parts.append((start + k * beat, tone(note(ch[0] - 36), beat * 0.4, a=0.002, d=0.08, harm=(1, 0.6, 0.3), gain=0.9)))
                parts.append((start + k * beat + beat / 2, noise(0.06, a=0.001, d=0.02, lp=0.6, gain=0.25, seed=b * 10 + k)))
    out = mix(*parts); L = int(total * SR)
    tail = out[L:]; out = out[:L]
    for i, s in enumerate(tail): out[i % L] += s
    write(path, out, peak=0.5)
C = [[60, 64, 67, 72], [57, 60, 64, 69], [53, 57, 60, 65], [55, 59, 62, 67]]
def tr(chs, k): return [[n + k for n in c] for c in chs]
Am = [[57, 60, 64, 69], [53, 57, 60, 65], [55, 59, 62, 67], [52, 56, 59, 64]]
loop(R + "GameLobby.wav", 92, tr(Am, 3), beats_per_bar=3, timbre=BELL)
loop(R + "MonsterWait.wav", 80, tr(Am, -2), hunt=True, timbre=(1, 0.6, 0.4))
maps = {"CandyForest": (104, tr(C, 2), MARIMBA), "Gingerbread": (96, tr(C, 5), BELL), "Factory": (112, tr(C, -3), (1, 0.5, 0.3, 0.2)),
        "Carnival": (96, tr(Am, 0), BELL), "Bakery": (88, tr(Am, -4), (1, 0.3, 0.2, 0.1))}
for name, (bpm, chs, timbre) in maps.items():
    bpb = 3 if name == "Carnival" else 4
    loop(R + name + "Paint.wav", bpm, chs, beats_per_bar=bpb, timbre=timbre)
    loop(R + name + "Hunt.wav", bpm, chs, beats_per_bar=bpb, hunt=True, timbre=timbre)
loop(R + "TimeAttack.wav", 140, tr(Am, 2), hunt=True, timbre=(1, 0.6, 0.4, 0.2))
def stinger(path, notes, step, d, timbre, low=None):
    parts = [(i * step, tone(note(m), d * 2.5, d=d, harm=timbre)) for i, m in enumerate(notes)]
    if low: parts.append((0, tone(note(low), d * 4, a=0.02, d=d * 2, harm=(1, 0.6, 0.4, 0.2))))
    write(path, mix(*parts), peak=0.6)
stinger(R + "StMonsterArrive.wav", [48, 51, 55, 58], 0.12, 0.6, (1, 0.7, 0.5, 0.3), low=36)
stinger(R + "StDeviceComplete.wav", [72, 76, 79, 84, 88], 0.09, 0.35, BELL)
stinger(R + "StSpyLaunch.wav", [60, 62, 65, 67, 70, 72, 74, 77], 0.11, 0.3, (1, 0.5, 0.3), low=36)
stinger(R + "StWitchAppear.wav", [45, 48, 51], 0.4, 0.9, (1, 0.8, 0.6, 0.4), low=33)
stinger(R + "JgEscapeSuccess.wav", [72, 76, 79, 84, 79, 84, 88], 0.14, 0.4, MARIMBA, low=48)
stinger(R + "JgEscapeFail.wav", [67, 66, 65, 64, 60], 0.22, 0.5, (1, 0.4, 0.2), low=40)
stinger(R + "JgMonsterWin.wav", [43, 46, 50, 46, 43, 39], 0.2, 0.6, (1, 0.8, 0.6, 0.4, 0.2), low=31)
print("ok")
