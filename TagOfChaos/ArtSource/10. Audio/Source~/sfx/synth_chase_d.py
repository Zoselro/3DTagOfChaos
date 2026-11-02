# 추격음 D(북소리 추격) 확정판 — 층 나누기(2026-10-03, DistanceFadePlan.md §9.5).
# 바탕(현 16분)은 그대로 반복하고, 북은 같은 템포(140 BPM)에서 치는 횟수만 다른 4단계로 따로 만든다.
# 게임에서 모든 층을 같은 순간에 시작해 함께 돌리고, 술래가 가까워질수록 더 촘촘한 북 단계로 교차 전환 → "비트가 빨라지는" 느낌.
# 모든 층은 같은 길이(8마디 = 13.714초)·같은 배율로 저장해 서로의 크기 비율을 유지한다.
import numpy as np
from synth_chase import Song, write, OUT, SR

BPM, BARS = 140, 8
BEATS = BARS * 4
TAIKO, TIMPANI = 116, 47


def base():
    s = Song(BPM, BEATS)
    for bar in range(BARS):
        b0 = bar * 4
        for k in range(16):                                      # 현 16분 E 프리지안
            p = (52, 53, 52, 55)[k % 4] + (0 if bar % 4 < 2 else 1)
            s.add(0, 48, b0 + k * 0.25, 0.2, p, 70 + (20 if k % 4 == 0 else 0))
    return s.render("D_base")


def brass():
    """2마디마다 낮은 금관 경고("따~단", 반음 아래로). 2026-10-03: 먼 단계에만 남기고 가까운 단계에서는 뺀다."""
    s = Song(BPM, BEATS)
    for bar in range(BARS):
        b0 = bar * 4
        if bar % 2 == 1:
            s.add(3, 61, b0 + 2, 1, 41, 100); s.add(3, 61, b0 + 3, 1, 40, 105)
            s.add(3, 57, b0 + 2, 1, 29, 100); s.add(3, 57, b0 + 3, 1, 28, 105)
    return s.render("D_brass")


def drums(level):
    """1 = 쿵…쿵(2박마다), 2 = 원래 패턴, 3 = 8분, 4 = 16분 연타."""
    s = Song(BPM, BEATS)
    for bar in range(BARS):
        b0 = bar * 4
        if level == 1:
            hits = [(0, 115), (2, 95)]
            timp = [(0, 90)]
        elif level == 2:
            hits = [(0, 120), (1.5, 90), (2, 120), (3, 90), (3.5, 90)]
            timp = [(0, 100), (2, 85)]
        elif level == 3:
            hits = [(k * 0.5, 120 if k % 2 == 0 else 88) for k in range(8)]
            timp = [(k, 100 if k % 2 == 0 else 80) for k in range(4)]
        else:
            hits = [(k * 0.25, 122 if k % 4 == 0 else (100 if k % 2 == 0 else 78)) for k in range(16)]
            timp = [(k * 0.5, 105 if k % 2 == 0 else 85) for k in range(8)]
            if bar % 2 == 1:                                     # 2마디마다 탐 내려오기(긴박감)
                for i, p in enumerate((50, 48, 47, 45, 43, 41)):
                    s.add(9, 0, b0 + 2.5 + i * 0.25, 0.2, p, 100)
        for b, v in hits: s.add(1, TAIKO, b0 + b, 0.3, 41, v)
        for b, v in timp: s.add(2, TIMPANI, b0 + b, 0.5, 40, v)
    return s.render(f"D_drum{level}")


def smooth(control, seconds):
    a = 1 - np.exp(-1 / (seconds * SR)); y = np.empty_like(control); s = control[0]
    for i in range(len(control)): s += a * (control[i] - s); y[i] = s
    return y


def approach(base_loop, drum_loops, radii, far=25.0, near=2.0, audible=20.0, full=5.0, move=20.0, hold=4.0):
    """술래가 far → near로 move초 동안 다가오고 hold초 머문다. 북 단계는 radii(1~4단계 시작 거리)를 지날 때 0.4초 교차 전환."""
    n = int((move + hold) * SR); t = np.arange(n) / SR
    d = np.where(t < move, far + (near - far) * t / move, near)
    k = np.clip((audible - d) / (audible - full), 0, 1)
    edge = np.clip((audible - d) / 1.5, 0, 1)                    # 경계에서 1.5 m 동안 0 → 바닥값
    master = edge * (0.3 + 0.7 * k * k * (3 - 2 * k))
    x = np.resize(base_loop, n) * 0.85
    level = np.zeros(n, dtype=int)
    for i, r in enumerate(radii): level[d < r] = i + 1
    for i, loop in enumerate(drum_loops):
        x += np.resize(loop, n) * smooth((level == i + 1).astype(float), 0.4)
    cutoff = 900 + (18000 - 900) * k
    a = 1 - np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty(n); s = 0.0
    for i in range(n): s += a[i] * (x[i] - s); y[i] = s
    return y * master


def limit_layer(bed, layer, ceiling):
    """bed + layer의 피크가 ceiling을 넘지 않도록 layer에만 이득을 줄인다(5 ms 앞보기 최대값, 80 ms 회복). 순환 처리라 반복 이음매 유지."""
    n = len(layer); w = int(0.005 * SR)
    room = np.maximum(ceiling - np.abs(bed), 0.05)
    need = np.abs(layer) / room
    padded = np.concatenate([need[-w:], need, need[:w]])
    peak = np.lib.stride_tricks.sliding_window_view(padded, 2 * w + 1).max(axis=1)
    target = np.minimum(1.0, 1.0 / np.maximum(peak, 1e-9))
    rel = np.exp(-1 / (0.08 * SR)); g = np.empty(n); x = target[-1]
    for _ in range(2):
        for i in range(n):
            x = target[i] if target[i] < x else target[i] + (x - target[i]) * rel
            g[i] = x
    return layer * g


NEAR_DRUM_GAIN = 1.7   # 가까운 단계 북을 더 크게(약 +4.6 dB, 2026-10-03 요청)

if __name__ == "__main__":
    # 2026-10-03 사용자 결정: 두 단계만 — 먼 단계(기존 Level1 그대로: 북 1단계 + 금관) / 가까운 단계(북 4단계를 더 크게, 금관 없음)
    out = f"{OUT}/D2"
    b = base()
    brass_ = brass()
    far = drums(1) + brass_
    near = drums(4) * NEAR_DRUM_GAIN
    # 먼 단계는 사용자가 고른 Level1과 같은 크기로(이전 배율 = 바탕+금관+북 4단계 원래 크기 기준), 가까운 단계 북만 리미터로 피크를 막는다.
    scale = 0.89 / np.max(np.abs(b + brass_ + drums(4)))
    near = limit_layer(b * scale, near * scale, ceiling=0.89) / scale
    write(f"{out}/Chase_Base.wav", b * scale, normalize=False)
    write(f"{out}/Chase_Far.wav", far * scale, normalize=False)
    write(f"{out}/Chase_Near.wav", near * scale, normalize=False)
    write(f"{out}/Preview_Far.wav", np.tile((b + far) * scale, 2), normalize=False)
    write(f"{out}/Preview_Near.wav", np.tile((b + near) * scale, 2), normalize=False)
    ap = approach(b * scale, [far * scale, near * scale], radii=(20.0, 5.0))
    write(f"{out}/Chase_D_approach_v3.wav", ap / max(1e-9, np.max(np.abs(ap))) * 0.89, normalize=False)
