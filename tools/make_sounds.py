"""Generates the dashboard sound effects in src/CompTracker/wwwroot/sounds/.

Everything is synthesized from scratch (no samples), so the files are ours to use and ship.
Standard library only:  python tools/make_sounds.py

  ping.wav      a new score was posted (short two-note chime)
  crown.wav     someone took 1st place in a game (rising chiptune arpeggio)
  champion.wav  someone took the overall #1 spot (retro victory fanfare)
"""

import math
import os
import struct
import wave

RATE = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "src", "CompTracker", "wwwroot", "sounds")


def freq(note):
    """'C5' / 'F#4' -> Hz (A4 = 440)."""
    names = {"C": -9, "D": -7, "E": -5, "F": -4, "G": -2, "A": 0, "B": 2}
    semis = names[note[0]] + (1 if "#" in note else 0)
    octave = int(note[-1])
    return 440.0 * 2 ** ((semis + (octave - 4) * 12) / 12)


def osc(kind, phase):
    p = phase % 1.0
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    if kind == "triangle":
        return 4 * abs(p - 0.5) - 1
    if kind == "pulse":  # 25% duty, the classic NES lead
        return 1.0 if p < 0.25 else -1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    raise ValueError(kind)


def tone(buf, start, dur, hz, kind="sine", vol=0.5, attack=0.005, decay=None, vibrato=0.0, slide_to=None):
    """Mix one note into buf. decay=None holds then releases; a number is an exponential decay rate."""
    first = int(start * RATE)
    n = int(dur * RATE)
    release = min(0.04, dur / 3)
    phase = 0.0
    for i in range(n):
        t = i / RATE
        f = hz if slide_to is None else hz + (slide_to - hz) * min(1.0, t / dur)
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * 6 * t) * min(1.0, t / 0.25)
        phase += f / RATE
        env = min(1.0, t / attack) if attack else 1.0
        if decay is not None:
            env *= math.exp(-decay * t)
        if t > dur - release:
            env *= max(0.0, (dur - t) / release)
        idx = first + i
        while idx >= len(buf):
            buf.append(0.0)
        buf[idx] += vol * env * osc(kind, phase)


def lowpass(buf, cutoff):
    """One-pole low-pass to take the harsh edge off square/pulse waves."""
    a = 1 - math.exp(-2 * math.pi * cutoff / RATE)
    out, y = [], 0.0
    for x in buf:
        y += a * (x - y)
        out.append(y)
    return out


def write(name, buf, peak=0.7, tail=0.05):
    buf = buf + [0.0] * int(tail * RATE)
    top = max(abs(x) for x in buf) or 1.0
    scale = peak / top
    path = os.path.join(OUT, name)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, x * scale)) * 32767)) for x in buf))
    print(f"wrote {os.path.normpath(path)} ({len(buf) / RATE:.2f}s)")


def ping():
    buf = []
    for start, note in [(0.0, "E6"), (0.09, "B6")]:
        tone(buf, start, 0.45, freq(note), "sine", 0.55, attack=0.002, decay=9)
        tone(buf, start, 0.25, freq(note) * 2, "sine", 0.12, attack=0.002, decay=18)  # sparkle overtone
    return buf


def crown():
    buf = []
    notes = ["C5", "E5", "G5", "C6", "E6", "G6"]
    step = 0.075
    for i, note in enumerate(notes):
        tone(buf, i * step, step * 1.6, freq(note), "pulse", 0.32, decay=4)
    end = len(notes) * step
    # Held major chord with a little vibrato on top.
    for note, vol in [("C6", 0.28), ("E6", 0.2), ("G6", 0.2)]:
        tone(buf, end, 0.75, freq(note), "square" if note == "C6" else "triangle", vol, vibrato=0.006, decay=2.2)
    tone(buf, end, 0.75, freq("C4"), "triangle", 0.35, decay=2.5)  # bass
    return lowpass(buf, 5500)


def champion():
    buf = []
    beat = 0.13
    # "da-da-da DAAA, da-DAAA!"
    lead = [(0, "G5", 1), (1, "G5", 1), (2, "G5", 1), (3, "C6", 3), (6.5, "A5", 1), (7.5, "C6", 6)]
    for at, note, length in lead:
        held = length > 2
        tone(buf, at * beat, length * beat * 0.95, freq(note), "pulse", 0.34,
             vibrato=0.008 if held else 0.0, decay=0.9 if held else 6)
    # Harmony a third below on the long notes.
    for at, note, length in [(3, "E5", 3), (7.5, "E5", 6), (7.5, "G5", 6)]:
        tone(buf, at * beat, length * beat * 0.95, freq(note), "square", 0.16, vibrato=0.006, decay=0.9)
    # Bass walking up to the root.
    for at, note, length in [(0, "C3", 3), (3, "C3", 3), (6.5, "F3", 1), (7.5, "C3", 6)]:
        tone(buf, at * beat, length * beat * 0.95, freq(note), "triangle", 0.45, decay=0.8)
    # Noise-free "cymbal" sparkle: fast high arpeggio over the final note.
    for i, note in enumerate(["C7", "G6", "E6", "C7", "G6", "E6"]):
        tone(buf, 7.5 * beat + i * 0.05, 0.12, freq(note), "sine", 0.08, decay=20)
    return lowpass(buf, 6000)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    write("ping.wav", ping())
    write("crown.wav", crown())
    write("champion.wav", champion())
