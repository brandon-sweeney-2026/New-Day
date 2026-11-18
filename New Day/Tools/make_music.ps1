# Composes and records the game's background music. No samples and no music program: every instrument is built from
# maths (sine waves, saw waves, filtered noise) by the small synthesizer written in C# below. Run it again any time
# you change a note, a tempo or an instrument:
#
#     powershell -ExecutionPolicy Bypass -File Tools\make_music.ps1 -Project "<project folder>"
#
# It writes four seamless loops (16-bit stereo WAV) into Assets\Project\Audio\Music:
#     music_farm.wav      - Farm AND Suburbs (the same tune, so it keeps playing when you walk between them). Upbeat, D major, 112 BPM.
#     music_city.wav      - City. A little faster and jazzier, F major, 124 BPM.
#     music_forest.wav    - Forest. Solemn, A minor, a slow waltz (3/4) at 72 BPM with harp, flute and cello.
#     music_mountain.wav  - Mountain trail (scene "Mine") AND the mine shaft. Grand, D minor, 88 BPM, brass, strings, timpani.
#
# Then open Unity and run Farm > Set Up > Music once (it links the files to the scenes). Add -Only farm (or city, forest,
# mountain) to rebuild just one track.
#
# HOW A SONG IS WRITTEN (see the NdSongs class near the bottom):
#   - A melody is a string of "note/length" words, for example "F#5/0.5 A5/1 r/0.5". The length is in beats; "r" is a rest.
#   - A chord list is one chord name per bar ("D", "Bm", "F#m", "Gm7", "Bbmaj7", "A7"...). The bass, the arpeggios, the pads
#     and the drums are generated from that list bar by bar.
#   - Each song is 32 bars: an A section, A again with other instruments, a B section, then A to finish. The last bar always
#     leads back into the first, and the reverb tail of the end is folded onto the start, so the loop has no seam.
param([string]$Project = (Split-Path -Parent $PSScriptRoot), [string]$Only = "")

$out = Join-Path $Project "Assets\Project\Audio\Music"
New-Item -ItemType Directory -Force $out | Out-Null

$code = @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

// ---------------------------------------------------------------------------------------------------------------- synth
// Holds one song while it is being recorded: a stereo "dry" track, plus a "send" track that goes through the reverb.
public class NdSynth
{
    public const int SR = 32000;            // samples per second (32 kHz keeps the files smaller and is plenty for this music)
    public double Bpm;
    public int BeatsPerBar;
    public int LoopSamples;                 // length of one loop, in samples
    public float[] L, R, SendL, SendR;
    private readonly Random rng;
    private readonly Dictionary<string, double> energy = new Dictionary<string, double>();

    public NdSynth(double bpm, int beatsPerBar, int bars, int seed)
    {
        Bpm = bpm; BeatsPerBar = beatsPerBar; rng = new Random(seed);
        LoopSamples = (int)Math.Round(bars * beatsPerBar * 60.0 / bpm * SR);
        int n = LoopSamples + SR * 7;       // 7 extra seconds for notes and reverb that ring past the end
        L = new float[n]; R = new float[n]; SendL = new float[n]; SendR = new float[n];
    }

    public double BeatSec { get { return 60.0 / Bpm; } }
    private double Rand() { return rng.NextDouble() * 2.0 - 1.0; }
    private static float[] Buf(double sec) { return new float[Math.Max(1, (int)(sec * SR))]; }
    private static double Smooth(double x) { x = Math.Max(0, Math.Min(1, x)); return x * x * (3 - 2 * x); }

    // ---- one note: render the instrument, then mix it in at the right time, place (pan) and reverb amount
    public void Note(string inst, double beat, double lenBeats, int midi, double vel, double pan, double reverb)
    {
        bool drum = IsDrum(inst);
        double start = beat * BeatSec + Rand() * (drum ? 0.003 : 0.006);   // tiny timing wobble so it sounds played, not typed
        if (start < 0) start = 0;
        double f = 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);
        double v = Math.Max(0.05, Math.Min(1.2, vel * (0.92 + rng.NextDouble() * 0.16)));
        float[] b = Voice(inst, f, lenBeats * BeatSec, v);

        int fade = Math.Min(b.Length, SR / 200);                            // 5 ms fade at the very end: no clicks
        for (int i = 0; i < fade; i++) b[b.Length - 1 - i] *= (float)i / fade;

        double a = (Math.Max(-1, Math.Min(1, pan)) + 1) * Math.PI / 4;       // equal-power pan
        float gl = (float)Math.Cos(a), gr = (float)Math.Sin(a), rv = (float)reverb;
        int s0 = (int)(start * SR);
        double e = 0;
        for (int i = 0; i < b.Length; i++)
        {
            int k = s0 + i; if (k >= L.Length) break;
            float x = b[i]; e += x * x;
            L[k] += x * gl; R[k] += x * gr; SendL[k] += x * gl * rv; SendR[k] += x * gr * rv;
        }
        double old; energy.TryGetValue(inst, out old); energy[inst] = old + e;
    }

    private static bool IsDrum(string i)
    {
        return i == "kick" || i == "snare" || i == "hat" || i == "ohat" || i == "shaker" || i == "timpani" || i == "taiko" || i == "crash" || i == "swell";
    }

    private float[] Voice(string inst, double f, double dur, double v)
    {
        switch (inst)
        {
            case "pluck":   return Pluck(f, dur, v, false);
            case "harp":    return Pluck(f, dur, v, true);
            case "marimba": return Mallet(f, dur, v, false);
            case "vibes":   return Mallet(f, dur, v, true);
            case "epiano":  return EPiano(f, dur, v);
            case "celesta": return Celesta(f, dur, v);
            case "flute":   return Flute(f, dur, v);
            //                                voices detune attack release cutoff vibrato gain
            case "pad":     return Saws(f, dur, v, 3, 9.0, 0.6, 1.2, 1000, 0.0, 0.11);
            case "strings": return Saws(f, dur, v, 4, 7.0, 0.2, 0.45, 2300, 0.003, 0.13);
            case "spicc":   return Saws(f, dur, v, 3, 6.0, 0.012, 0.09, 2800, 0.0, 0.13);
            case "solo":    return Saws(f, dur, v, 2, 4.0, 0.12, 0.3, 1700, 0.006, 0.22);
            case "brass":   return Brass(f, dur, v, 2600, 0.15);
            case "horn":    return Brass(f, dur, v, 1400, 0.18);
            case "bass":    return Bass(f, dur, v);
            case "kick":    return Kick(v);
            case "snare":   return Snare(v);
            case "hat":     return Hat(v, 55, 0.1, 0.16);
            case "ohat":    return Hat(v, 9, 0.4, 0.11);
            case "shaker":  return Shaker(v);
            case "timpani": return Timpani(f, v);
            case "taiko":   return Taiko(v);
            case "crash":   return Crash(v);
            case "swell":   return Swell(dur, v);
        }
        throw new ArgumentException("Unknown instrument: " + inst);
    }

    // ---- a filter (a "state variable filter"): gives low-pass, band-pass and high-pass versions of a sound
    private class Svf
    {
        private double ic1, ic2;
        public double lp, bp, hp;
        public void Run(double x, double fc, double q)
        {
            fc = Math.Max(20, Math.Min(fc, SR * 0.45));
            double g = Math.Tan(Math.PI * fc / SR), k = 1.0 / q;
            double a1 = 1.0 / (1.0 + g * (g + k)), a2 = g * a1, a3 = g * a2;
            double v3 = x - ic2, v1 = a1 * ic1 + a2 * v3, v2 = ic2 + a2 * ic1 + a3 * v3;
            ic1 = 2 * v1 - ic1; ic2 = 2 * v2 - ic2;
            lp = v2; bp = v1; hp = x - k * v1 - v2;
        }
        public double Lp(double x, double fc, double q) { Run(x, fc, q); return lp; }
    }

    // A sine-wave partial that is turned by a fixed step every sample (much faster than calling Math.Sin each time).
    private struct Osc
    {
        public double s, c, sw, cw, amp, decay, level;
        public Osc(double freq, double amp, double decayPerSec)
        {
            double w = 2 * Math.PI * freq / SR;
            s = 0; c = 1; sw = Math.Sin(w); cw = Math.Cos(w); this.amp = amp;
            decay = Math.Exp(-decayPerSec / SR); level = 1;
        }
        public double Next()
        {
            double ns = s * cw + c * sw; c = c * cw - s * sw; s = ns; level *= decay;
            return s * amp * level;
        }
    }

    // ---- plucked string (guitar, harp): harmonics that fade, high ones faster
    private float[] Pluck(double f, double dur, double v, bool harp)
    {
        double ring = harp ? 2.2 : 0.35;
        float[] b = Buf(dur + ring);
        List<Osc> parts = new List<Osc>();
        double pos = harp ? 0.4 : 0.22, bright = Math.Sqrt(f / 220.0);
        for (int n = 1; n <= 16 && n * f < SR * 0.42; n++)
        {
            double amp = Math.Abs(Math.Sin(n * Math.PI * pos)) / Math.Pow(n, harp ? 1.5 : 1.1) * (n == 1 ? 1 : 0.5 + 0.5 * v);
            double dec = (harp ? 0.8 : 1.8) * bright * (1 + 0.25 * (n - 1) * (n - 1));
            parts.Add(new Osc(n * f, amp, dec));
        }
        Osc[] p = parts.ToArray();
        double gain = harp ? 0.3 : 0.28;
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / SR, s = 0;
            for (int k = 0; k < p.Length; k++) s += p[k].Next();
            s += Rand() * Math.Exp(-t * 300) * 0.12 * v;                     // the "pick" noise
            double env = Math.Min(1, t / 0.002);
            if (!harp && t > dur) env *= Math.Max(0, 1 - (t - dur) / ring);
            b[i] = (float)(gain * v * env * s);
        }
        return b;
    }

    // ---- marimba (short, woody) and vibraphone (long, with a gentle wobble)
    private float[] Mallet(double f, double dur, double v, bool vibes)
    {
        double len = vibes ? dur + 0.6 : 1.3;
        float[] b = Buf(len);
        List<Osc> parts = new List<Osc>();
        parts.Add(new Osc(f, 1.0, vibes ? 1.1 : 3.2));
        if (4 * f < SR * 0.42) parts.Add(new Osc(4 * f, 0.1 + 0.3 * v, vibes ? 5 : 13));
        if (!vibes && 10 * f < SR * 0.42) parts.Add(new Osc(10 * f, 0.07, 30));
        Osc[] p = parts.ToArray();
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / SR, s = 0;
            for (int k = 0; k < p.Length; k++) s += p[k].Next();
            s += Rand() * Math.Exp(-t * 500) * 0.1;
            double env = Math.Min(1, t / 0.0015);
            if (vibes)
            {
                env *= 1 - 0.2 * (0.5 + 0.5 * Math.Sin(2 * Math.PI * 5.5 * t));
                if (t > dur + 0.1) env *= Math.Max(0, 1 - (t - dur - 0.1) / 0.45);
            }
            b[i] = (float)(0.42 * v * env * s);
        }
        return b;
    }

    // ---- electric piano (FM: one sine wave wobbling another, brighter when hit harder)
    private float[] EPiano(double f, double dur, double v)
    {
        float[] b = Buf(dur + 0.3);
        double w = 2 * Math.PI * f / SR;
        bool tine = 14 * f < SR * 0.42;
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / SR;
            double idx = (0.4 + 1.6 * v) * Math.Exp(-t * 4) + 0.15;
            double s = Math.Sin(w * i + idx * Math.Sin(w * i));
            if (tine) s += 0.08 * Math.Sin(14 * w * i) * Math.Exp(-t * 30);
            double env = Math.Min(1, t / 0.003) * Math.Exp(-t * 0.8);
            if (t > dur) env *= Math.Max(0, 1 - (t - dur) / 0.25);
            b[i] = (float)(0.26 * v * env * s);
        }
        return b;
    }

    // ---- celesta / music box bell
    private float[] Celesta(double f, double dur, double v)
    {
        float[] b = Buf(2.2);
        double w = 2 * Math.PI * f / SR;
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / SR;
            double idx = 1.2 * Math.Exp(-t * 6);
            double s = Math.Sin(w * i + idx * Math.Sin(4 * w * i)) + 0.2 * Math.Sin(2 * w * i) * Math.Exp(-t * 4);
            b[i] = (float)(0.22 * v * Math.Min(1, t / 0.002) * Math.Exp(-t * 2.5) * s);
        }
        return b;
    }

    // ---- flute: a soft tone with a little breath noise and vibrato that grows on long notes
    private float[] Flute(double f, double dur, double v)
    {
        double rel = 0.18;
        float[] b = Buf(dur + rel);
        Svf nf = new Svf();
        double ph = 0, vr = 5.0 + rng.NextDouble() * 0.5;
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / SR;
            double vib = Smooth((t - 0.3) / 0.5) * 0.0045;
            ph += 2 * Math.PI * f * (1 + vib * Math.Sin(2 * Math.PI * vr * t)) / SR;
            double s = Math.Sin(ph) + 0.25 * Math.Sin(2 * ph) + 0.09 * Math.Sin(3 * ph) + 0.03 * Math.Sin(4 * ph);
            nf.Run(Rand(), 2.2 * f, 2.0);
            s += nf.bp * 0.25;
            double env = Smooth(t / 0.08) * (0.85 + 0.15 * Smooth(t / 0.4));
            if (t > dur) env *= Math.Max(0, 1 - (t - dur) / rel);
            b[i] = (float)(0.3 * v * env * s);
        }
        return b;
    }

    // Saw wave with the sharp corner smoothed ("PolyBLEP"), so high notes don't sound harsh or out of tune.
    private static double Blep(double t, double dt)
    {
        if (t < dt) { t /= dt; return t + t - t * t - 1.0; }
        if (t > 1.0 - dt) { t = (t - 1.0) / dt; return t * t + t + t + 1.0; }
        return 0.0;
    }

    // ---- pads and strings: several slightly out-of-tune saw waves through a soft low-pass filter
    private float[] Saws(double f, double dur, double v, int voices, double detune, double att, double rel, double cutoff, double vibDepth, double gain)
    {
        float[] b = Buf(dur + rel);
        double[] ph = new double[voices], ratio = new double[voices];
        for (int k = 0; k < voices; k++)
        {
            double spread = voices == 1 ? 0 : (k / (voices - 1.0)) * 2 - 1;
            ratio[k] = Math.Pow(2, spread * detune / 1200.0);
            ph[k] = rng.NextDouble();
        }
        Svf f1 = new Svf(), f2 = new Svf();
        double fc = Math.Max(cutoff * (0.6 + 0.4 * v), f * 2.5), vr = 5.0 + rng.NextDouble() * 0.6, norm = 1.0 / Math.Sqrt(voices);
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / SR;
            double vib = 1 + vibDepth * Smooth(t / 0.6) * Math.Sin(2 * Math.PI * vr * t);
            double s = 0;
            for (int k = 0; k < voices; k++)
            {
                double dt = f * ratio[k] * vib / SR;
                ph[k] += dt; if (ph[k] >= 1) ph[k] -= 1;
                s += 2 * ph[k] - 1 - Blep(ph[k], dt);
            }
            double y = f2.Lp(f1.Lp(s * norm, fc, 0.7), fc, 0.7);
            double env = Smooth(t / att);
            if (t > dur) env *= Math.Max(0, 1 - (t - dur) / rel);
            b[i] = (float)(gain * v * env * y);
        }
        return b;
    }

    // ---- brass and horns: saw waves whose filter opens quickly at the start of the note (the "blare")
    private float[] Brass(double f, double dur, double v, double bright, double gain)
    {
        double rel = 0.22;
        float[] b = Buf(dur + rel);
        double p1 = rng.NextDouble(), p2 = rng.NextDouble(), r1 = Math.Pow(2, 5 / 1200.0), r2 = 1 / r1;
        Svf f1 = new Svf(), f2 = new Svf();
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / SR;
            double vib = 1 + 0.003 * Smooth((t - 0.4) / 0.4) * Math.Sin(2 * Math.PI * 5.2 * t);
            double d1 = f * r1 * vib / SR, d2 = f * r2 * vib / SR;
            p1 += d1; if (p1 >= 1) p1 -= 1;
            p2 += d2; if (p2 >= 1) p2 -= 1;
            double s = (2 * p1 - 1 - Blep(p1, d1) + 2 * p2 - 1 - Blep(p2, d2)) * 0.7;
            double open = t < 0.08 ? t / 0.08 : 0.6 + 0.4 * Math.Exp(-(t - 0.08) * 3.5);
            double fc = 250 + f * 1.5 + bright * v * open;
            double y = f2.Lp(f1.Lp(s, fc, 0.8), fc, 0.8);
            double env = Smooth(t / 0.04);
            if (t > dur) env *= Math.Max(0, 1 - (t - dur) / rel);
            b[i] = (float)(gain * v * env * y);
        }
        return b;
    }

    // ---- bass: round sine with a little filtered saw on top so it is heard on small speakers
    private float[] Bass(double f, double dur, double v)
    {
        double rel = 0.07;
        float[] b = Buf(dur + rel);
        Svf lp = new Svf();
        double ph = 0, dt = f / SR;
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / SR;
            ph += dt; if (ph >= 1) ph -= 1;
            double s = Math.Sin(2 * Math.PI * ph) + 0.3 * Math.Sin(4 * Math.PI * ph) + 0.3 * lp.Lp(2 * ph - 1 - Blep(ph, dt), 450, 0.7);
            double env = Math.Min(1, t / 0.004) * (0.7 + 0.3 * Math.Exp(-t * 6));
            if (t > dur) env *= Math.Max(0, 1 - (t - dur) / rel);
            b[i] = (float)(0.45 * v * env * s);
        }
        return b;
    }

    // ---- drums
    private float[] Kick(double v)
    {
        float[] b = Buf(0.5); double ph = 0;
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / SR;
            ph += 2 * Math.PI * (48 + 95 * Math.Exp(-t * 28)) / SR;
            b[i] = (float)(0.9 * v * (Math.Sin(ph) * Math.Exp(-t * 7.5) + Rand() * Math.Exp(-t * 250) * 0.15));
        }
        return b;
    }

    private float[] Snare(double v)
    {
        float[] b = Buf(0.35); Svf n = new Svf();
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / SR;
            n.Run(Rand(), 2500, 0.8);
            double s = 0.45 * Math.Sin(2 * Math.PI * 185 * t) * Math.Exp(-t * 22) + n.bp * 1.6 * Math.Exp(-t * 16);
            b[i] = (float)(0.6 * v * Math.Min(1, t / 0.001) * s);
        }
        return b;
    }

    private float[] Hat(double v, double decay, double len, double gain)
    {
        float[] b = Buf(len); Svf n = new Svf();
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / SR;
            n.Run(Rand(), 7000, 0.7);
            b[i] = (float)(gain * v * n.hp * Math.Exp(-t * decay));
        }
        return b;
    }

    private float[] Shaker(double v)
    {
        float[] b = Buf(0.12); Svf n = new Svf();
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / SR;
            n.Run(Rand(), 5500, 0.7);
            double env = t < 0.015 ? t / 0.015 : Math.Exp(-(t - 0.015) * 40);
            b[i] = (float)(0.13 * v * n.hp * env);
        }
        return b;
    }

    private float[] Timpani(double f, double v)
    {
        float[] b = Buf(2.6); Svf n = new Svf();
        double[] ratio = { 1, 1.5, 1.99, 2.44 }, amp = { 1, 0.45, 0.25, 0.12 };
        double[] ph = new double[4];
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / SR, s = 0, drop = 1 + 0.015 * Math.Exp(-t * 10);
            for (int k = 0; k < 4; k++)
            {
                ph[k] += 2 * Math.PI * f * ratio[k] * drop / SR;
                s += amp[k] * Math.Sin(ph[k]) * Math.Exp(-t * (1.7 + k));
            }
            s += n.Lp(Rand(), 300, 0.7) * Math.Exp(-t * 25) * 1.5;
            b[i] = (float)(0.42 * v * Math.Min(1, t / 0.002) * s);
        }
        return b;
    }

    private float[] Taiko(double v)
    {
        float[] b = Buf(1.0); Svf n = new Svf(); double ph = 0;
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / SR;
            ph += 2 * Math.PI * (58 + 45 * Math.Exp(-t * 18)) / SR;
            double s = Math.Sin(ph) * Math.Exp(-t * 4.5) + n.Lp(Rand(), 500, 0.7) * Math.Exp(-t * 20) * 1.2;
            b[i] = (float)(0.85 * v * Math.Min(1, t / 0.002) * s);
        }
        return b;
    }

    private float[] Crash(double v)
    {
        float[] b = Buf(3.5); Svf n = new Svf();
        for (int i = 0; i < b.Length; i++)
        {
            double t = (double)i / SR;
            n.Run(Rand(), 3500, 0.6);
            b[i] = (float)(0.15 * v * Math.Min(1, t / 0.002) * Math.Exp(-t * 1.3) * n.hp);
        }
        return b;
    }

    // A cymbal swell that grows over the note's length and stops on the next beat (used before big moments).
    private float[] Swell(double dur, double v)
    {
        float[] b = Buf(dur); Svf n = new Svf();
        for (int i = 0; i < b.Length; i++)
        {
            double x = (double)i / b.Length;
            n.Run(Rand(), 2000 + 6000 * x, 0.6);
            b[i] = (float)(0.16 * v * Math.Pow(x, 2.5) * n.hp);
        }
        return b;
    }

    // ---- reverb (the classic "Freeverb": 8 echoing delay lines and 4 smearing ones per ear)
    private static void Reverb(float[] inL, float[] inR, float[] outL, float[] outR, double room, double damp)
    {
        int[] combs = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
        int[] aps = { 556, 441, 341, 225 };
        double scale = SR / 44100.0;
        int n = inL.Length, pre = SR / 40;                                   // 25 ms before the echoes start
        float[] mono = new float[n];
        Svf hp = new Svf();
        for (int i = 0; i < n; i++) { hp.Run((inL[i] + inR[i]) * 0.5, 180, 0.7); mono[i] = (float)(hp.hp * 0.03); }
        for (int ch = 0; ch < 2; ch++)
        {
            float[] o = ch == 0 ? outL : outR;
            int spread = ch == 0 ? 0 : 23;
            double[][] cb = new double[8][]; int[] ci = new int[8]; double[] store = new double[8];
            for (int k = 0; k < 8; k++) cb[k] = new double[(int)((combs[k] + spread) * scale)];
            double[][] ab = new double[4][]; int[] ai = new int[4];
            for (int k = 0; k < 4; k++) ab[k] = new double[(int)((aps[k] + spread) * scale)];
            for (int i = 0; i < n; i++)
            {
                double x = i >= pre ? mono[i - pre] : 0, sum = 0;
                for (int k = 0; k < 8; k++)
                {
                    double y = cb[k][ci[k]];
                    store[k] = y * (1 - damp) + store[k] * damp;
                    cb[k][ci[k]] = x + store[k] * room;
                    if (++ci[k] >= cb[k].Length) ci[k] = 0;
                    sum += y;
                }
                for (int k = 0; k < 4; k++)
                {
                    double y = ab[k][ai[k]];
                    ab[k][ai[k]] = sum + y * 0.5;
                    sum = y - sum;
                    if (++ai[k] >= ab[k].Length) ai[k] = 0;
                }
                o[i] = (float)sum;
            }
        }
    }

    // ---- mix everything, fold the tail onto the start (seamless loop), set the loudness and write the WAV file
    public string Finish(string path, double room, double damp, double wet)
    {
        int n = L.Length;
        float[] wl = new float[n], wr = new float[n];
        Reverb(SendL, SendR, wl, wr, room, damp);
        double[] ml = new double[n], mr = new double[n];
        for (int i = 0; i < n; i++) { ml[i] = L[i] + wl[i] * wet; mr[i] = R[i] + wr[i] * wet; }
        for (int i = 0; i + LoopSamples < n; i++) { ml[i] += ml[LoopSamples + i]; mr[i] += mr[LoopSamples + i]; }

        double meanL = 0, meanR = 0;
        for (int i = 0; i < LoopSamples; i++) { meanL += ml[i]; meanR += mr[i]; }
        meanL /= LoopSamples; meanR /= LoopSamples;
        double peak = 1e-9, sq = 0;
        for (int i = 0; i < LoopSamples; i++)
        {
            ml[i] -= meanL; mr[i] -= meanR;
            peak = Math.Max(peak, Math.Max(Math.Abs(ml[i]), Math.Abs(mr[i])));
            sq += ml[i] * ml[i] + mr[i] * mr[i];
        }
        double rms = Math.Sqrt(sq / (2.0 * LoopSamples)) + 1e-9;
        double gain = Math.Min(0.15 / rms, 1.4 / peak);                      // about -16 dB average, peaks softly rounded

        using (FileStream fs = new FileStream(path, FileMode.Create))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            int data = LoopSamples * 4;
            w.Write("RIFF".ToCharArray()); w.Write(36 + data); w.Write("WAVE".ToCharArray());
            w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)2);
            w.Write(SR); w.Write(SR * 4); w.Write((short)4); w.Write((short)16);
            w.Write("data".ToCharArray()); w.Write(data);
            for (int i = 0; i < LoopSamples; i++)
            {
                w.Write((short)Math.Round(Math.Tanh(ml[i] * gain) * 32000));
                w.Write((short)Math.Round(Math.Tanh(mr[i] * gain) * 32000));
            }
        }

        double total = 0;
        foreach (double e in energy.Values) total += e;
        List<string> parts = new List<string>();
        foreach (KeyValuePair<string, double> kv in energy) parts.Add(kv.Key + " " + Math.Round(100 * kv.Value / total) + "%");
        return string.Format(CultureInfo.InvariantCulture, "{0:0.0} s, peak/rms {1:0.0}, gain {2:0.00} | energy: {3}",
            (double)LoopSamples / SR, peak / rms, gain, string.Join(", ", parts.ToArray()));
    }

    // ---------------------------------------------------------------------------------------------- music helpers
    private static int Pc(string n, ref int pos)
    {
        int[] basePc = { 9, 11, 0, 2, 4, 5, 7 };                            // A B C D E F G
        int pc = basePc[n[0] - 'A']; pos = 1;
        if (pos < n.Length && n[pos] == '#') { pc++; pos++; }
        else if (pos < n.Length && n[pos] == 'b') { pc--; pos++; }
        return (pc + 12) % 12;
    }

    /// "C#4" -> MIDI note number (middle C = C4 = 60).
    public static int Midi(string n) { int p = 0; int pc = Pc(n, ref p); return 12 * (int.Parse(n.Substring(p)) + 1) + pc; }

    /// The intervals (in semitones above the root) of a chord name such as "Bm", "Gm7", "Bbmaj7", "A7", "Esus4".
    public static int[] Intervals(string c)
    {
        int p = 0; Pc(c, ref p);
        switch (c.Substring(p))
        {
            case "": return new[] { 0, 4, 7 };
            case "m": return new[] { 0, 3, 7 };
            case "7": return new[] { 0, 4, 7, 10 };
            case "maj7": return new[] { 0, 4, 7, 11 };
            case "m7": return new[] { 0, 3, 7, 10 };
            case "sus4": return new[] { 0, 5, 7 };
            case "sus2": return new[] { 0, 2, 7 };
            case "dim": return new[] { 0, 3, 6 };
        }
        throw new ArgumentException("Unknown chord: " + c);
    }

    public static int Root(string c, int octave) { int p = 0; return 12 * (octave + 1) + Pc(c, ref p); }
    public static int Third(string c) { return Intervals(c)[1]; }

    /// The chord's notes, each placed as close as possible to "center" (so chords move smoothly from one to the next).
    public static int[] Voicing(string c, int center)
    {
        int p = 0, rp = Pc(c, ref p);
        List<int> list = new List<int>();
        foreach (int iv in Intervals(c))
        {
            int pc = (rp + iv) % 12, low = center - 6;
            list.Add(low + ((pc - low) % 12 + 12) % 12);
        }
        list.Sort();
        return list.ToArray();
    }

    /// Plays a melody string ("F#5/0.5 A5/1 r/0.5 ...") from startBeat. Returns the beat after the last note.
    public double Seq(string inst, double startBeat, string notes, double vel, double pan, double reverb, int transpose, double legato)
    {
        double b = startBeat;
        foreach (string tok in notes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] part = tok.Split('/');
            double len = double.Parse(part[1], CultureInfo.InvariantCulture);
            if (part[0] != "r") Note(inst, b, len * legato, Midi(part[0]) + transpose, vel, pan, reverb);
            b += len;
        }
        return b;
    }

    public static string[] Cat(params string[][] parts)
    {
        List<string> all = new List<string>();
        foreach (string[] p in parts) all.AddRange(p);
        return all.ToArray();
    }
}

// ---------------------------------------------------------------------------------------------------------------- songs
public static class NdSongs
{
    // ============ FARM + SUBURBS: "Sunny Rows". Upbeat, D major, 112 BPM. Guitar, marimba, flute, bass, light drums.
    public static string Farm(string path)
    {
        NdSynth s = new NdSynth(112, 4, 32, 101);
        string[] A = { "D", "A", "Bm", "G", "D", "A", "G", "A" };
        string[] B = { "G", "A", "F#m", "Bm", "G", "A", "Em7", "A7" };
        string[] ch = NdSynth.Cat(A, A, B, A);
        string melA =
            "F#5/0.5 A5/0.5 r/0.5 A5/0.5 B5/0.5 A5/0.5 F#5/1 " +
            "E5/0.5 C#5/0.5 E5/1 r/0.5 E5/0.5 F#5/0.5 E5/0.5 " +
            "D5/0.5 F#5/0.5 B5/1 A5/0.5 F#5/0.5 D5/1 " +
            "E5/1.5 D5/0.5 B4/1 r/1 " +
            "F#5/0.5 A5/0.5 r/0.5 A5/0.5 D6/0.5 C#6/0.5 A5/1 " +
            "C#6/0.5 B5/0.5 A5/0.5 E5/0.5 r/0.5 E5/0.5 A5/1 " +
            "B5/0.5 A5/0.5 G5/0.5 F#5/0.5 E5/1 G5/1 " +
            "F#5/1 E5/1 C#5/1 r/1";
        string melB =
            "B5/1 A5/0.5 G5/0.5 A5/1 B5/1 " +
            "C#6/1.5 B5/0.5 A5/2 " +
            "A5/0.5 F#5/0.5 C#5/0.5 F#5/0.5 A5/1 C#6/1 " +
            "D6/1.5 C#6/0.5 B5/1 F#5/1 " +
            "G5/0.5 A5/0.5 B5/0.5 D6/0.5 r/0.5 B5/0.5 D6/1 " +
            "E6/1 C#6/1 A5/1 C#6/1 " +
            "B5/1 G5/0.5 E5/0.5 D5/1 E5/1 " +
            "C#5/1 E5/1 G5/1 A5/0.5 r/0.5";

        s.Seq("marimba", 0, melA, 0.8, 0.15, 0.25, 0, 1.0);          // A: marimba
        s.Seq("flute", 32, melA, 0.75, 0.1, 0.3, 0, 0.95);           // A again: flute
        s.Seq("flute", 64, melB, 0.8, 0.1, 0.3, 0, 0.95);            // B: flute, marimba an octave below
        s.Seq("marimba", 64, melB, 0.55, -0.2, 0.25, -12, 1.0);
        s.Seq("marimba", 96, melA, 0.8, 0.15, 0.25, 0, 1.0);         // A to finish: marimba with a music-box sparkle on top
        s.Seq("celesta", 96, melA, 0.35, 0.4, 0.35, 12, 1.0);

        for (int bar = 0; bar < 32; bar++)
        {
            string c = ch[bar]; double b0 = bar * 4; int sec = bar / 8;
            int r2 = NdSynth.Root(c, 2), r3 = r2 + 12, t = NdSynth.Third(c);
            bool fill = bar == 15 || bar == 31;

            // bass: bouncy roots and fifths
            s.Note("bass", b0, 1.3, r2, 0.85, 0, 0.03);
            s.Note("bass", b0 + 1.5, 0.4, r2 + 7, 0.6, 0, 0.03);
            s.Note("bass", b0 + 2, 1.0, r2, 0.75, 0, 0.03);
            s.Note("bass", b0 + 3.5, 0.4, r2 + 12, 0.55, 0, 0.03);

            // guitar picking the chord in eighth notes
            int[] arp = { 0, 7, 12, t + 12, 12, 7, t + 12, 7 };
            for (int k = 0; k < 8; k++) s.Note("pluck", b0 + k * 0.5, 0.9, r3 + arp[k], k % 2 == 0 ? 0.7 : 0.5, -0.35, 0.25);

            // soft pad holding the chord
            foreach (int m in NdSynth.Voicing(c, 64)) s.Note("pad", b0, 4.0, m, 0.5, 0, 0.5);

            // second time through A: little marimba chord "chops" on the off-beats
            if (sec == 1) foreach (int m in NdSynth.Voicing(c, 69)) { s.Note("marimba", b0 + 1.5, 0.3, m, 0.28, 0.45, 0.2); s.Note("marimba", b0 + 3.5, 0.3, m, 0.24, 0.45, 0.2); }

            // drums: kick and shaker from the start, snare from the second section, hi-hats in the B section
            s.Note("kick", b0, 0.5, 36, 0.85, 0, 0);
            s.Note("kick", b0 + 2, 0.5, 36, 0.75, 0, 0);
            for (int k = 0; k < 8; k++) s.Note("shaker", b0 + k * 0.5, 0.25, 60, k % 2 == 1 ? 0.6 : 0.35, 0.3, 0.1);
            if (sec >= 1)
            {
                if (bar % 2 == 1) s.Note("kick", b0 + 2.5, 0.5, 36, 0.5, 0, 0);
                s.Note("snare", b0 + 1, 0.5, 60, 0.5, 0.05, 0.2);
                if (!fill) s.Note("snare", b0 + 3, 0.5, 60, 0.55, 0.05, 0.2);
            }
            if (sec == 2) for (int k = 0; k < 16; k++) s.Note("hat", b0 + k * 0.25, 0.1, 60, k % 4 == 2 ? 0.5 : 0.25, -0.3, 0.05);
            if (fill) for (int k = 0; k < 4; k++) s.Note("snare", b0 + 3 + k * 0.25, 0.25, 60, 0.35 + k * 0.1, 0.05, 0.2);
            if (bar == 16) s.Note("crash", b0, 1, 60, 0.5, -0.2, 0.3);
            if (bar == 24) s.Note("crash", b0, 1, 60, 0.4, 0.2, 0.3);
        }
        return s.Finish(path, 0.78, 0.35, 1.0);
    }

    // ============ CITY: "Downtown Stroll". A bit faster and jazzier, F major, 124 BPM. Electric piano, vibes, flute, funky bass.
    public static string City(string path)
    {
        NdSynth s = new NdSynth(124, 4, 32, 202);
        string[] A = { "Fmaj7", "Dm7", "Gm7", "C7", "Fmaj7", "Am7", "Bbmaj7", "C7" };
        string[] B = { "Bbmaj7", "C7", "Am7", "Dm7", "Gm7", "Am7", "Bbmaj7", "C7" };
        string[] ch = NdSynth.Cat(A, A, B, A);
        string melA =
            "r/0.5 C5/0.5 F5/0.5 A5/0.5 G5/0.5 F5/0.5 E5/1 " +
            "r/0.5 D5/0.5 F5/0.5 A5/0.5 C6/1 A5/1 " +
            "Bb5/0.5 A5/0.5 G5/0.5 F5/0.5 G5/1 D5/1 " +
            "E5/1.5 G5/0.5 Bb5/1 r/1 " +
            "r/0.5 C5/0.5 F5/0.5 A5/0.5 C6/0.5 A5/0.5 G5/1 " +
            "E5/0.5 G5/0.5 A5/0.5 C6/0.5 E6/1 C6/1 " +
            "D6/0.5 C6/0.5 Bb5/0.5 A5/0.5 F5/1 D5/1 " +
            "E5/1 G5/1 C6/1 r/1";
        string melB =
            "D6/1.5 C6/0.5 A5/1 F5/1 " +
            "G5/0.5 A5/0.5 Bb5/0.5 C6/0.5 E5/2 " +
            "r/0.5 E5/0.5 A5/0.5 C6/0.5 E6/1 D6/1 " +
            "C6/0.5 A5/0.5 F5/1 D5/2 " +
            "Bb4/0.5 D5/0.5 F5/0.5 A5/0.5 Bb5/1 A5/1 " +
            "G5/0.5 E5/0.5 C5/1 E5/1 G5/1 " +
            "A5/1 F5/1 D5/1 F5/1 " +
            "G5/1.5 E5/0.5 C5/1 r/1";

        s.Seq("vibes", 0, melA, 0.8, 0.2, 0.3, 0, 0.9);
        s.Seq("flute", 32, melA, 0.75, 0.15, 0.3, 0, 0.85);
        s.Seq("flute", 64, melB, 0.75, 0.15, 0.3, 0, 0.9);
        s.Seq("vibes", 64, melB, 0.5, -0.25, 0.3, -12, 0.9);
        s.Seq("vibes", 96, melA, 0.8, 0.2, 0.3, 0, 0.9);
        s.Seq("pluck", 96, melA, 0.55, -0.3, 0.2, -12, 0.6);

        for (int bar = 0; bar < 32; bar++)
        {
            string c = ch[bar]; double b0 = bar * 4; int sec = bar / 8;
            int r2 = NdSynth.Root(c, 2), iv7 = NdSynth.Intervals(c)[3];
            bool fill = bar == 15 || bar == 31;

            // funky bass line in eighths and sixteenths
            s.Note("bass", b0, 0.6, r2, 0.9, 0, 0.02);
            s.Note("bass", b0 + 0.75, 0.2, r2 + 12, 0.55, 0, 0.02);
            s.Note("bass", b0 + 1.5, 0.45, r2 + 7, 0.65, 0, 0.02);
            s.Note("bass", b0 + 2.5, 0.45, r2, 0.75, 0, 0.02);
            s.Note("bass", b0 + 3, 0.45, r2 + iv7, 0.6, 0, 0.02);
            s.Note("bass", b0 + 3.5, 0.4, r2 + 7, 0.6, 0, 0.02);

            // electric piano chords, pushed a little ahead of the beat
            int[] chord = NdSynth.Voicing(c, 62);
            double[] at = { 0, 1.5, 2.5 }, len = { 0.9, 0.45, 1.2 };
            for (int h = 0; h < 3; h++) foreach (int m in chord) s.Note("epiano", b0 + at[h], len[h], m, h == 0 ? 0.6 : 0.45, -0.3, 0.3);

            foreach (int m in NdSynth.Voicing(c, 60)) s.Note("pad", b0, 4.0, m, 0.35, 0.1, 0.45);

            // drums: hats from the start, kick and snare from bar 5, open hat and shaker later
            for (int k = 0; k < 8; k++)
            {
                bool open = sec >= 1 && k == 7;
                s.Note(open ? "ohat" : "hat", b0 + k * 0.5, 0.25, 60, k % 2 == 1 ? 0.6 : 0.4, 0.3, 0.05);
            }
            if (bar >= 4)
            {
                s.Note("kick", b0, 0.5, 36, 0.9, 0, 0);
                s.Note("kick", b0 + 2, 0.5, 36, 0.75, 0, 0);
                s.Note("kick", b0 + 2.75, 0.5, 36, 0.55, 0, 0);
                s.Note("snare", b0 + 1, 0.5, 60, 0.6, -0.05, 0.15);
                if (!fill) s.Note("snare", b0 + 3, 0.5, 60, 0.65, -0.05, 0.15);
            }
            if (sec >= 2) for (int k = 0; k < 16; k++) s.Note("shaker", b0 + k * 0.25, 0.2, 60, k % 2 == 1 ? 0.45 : 0.25, -0.35, 0.05);
            if (fill) for (int k = 0; k < 4; k++) s.Note("snare", b0 + 3 + k * 0.25, 0.25, 60, 0.35 + k * 0.12, -0.05, 0.15);
            if (bar == 8 || bar == 16) s.Note("crash", b0, 1, 60, 0.5, 0.25, 0.3);
        }
        return s.Finish(path, 0.72, 0.4, 0.9);
    }

    // ============ FOREST: "Old Roots". Solemn, A minor, 3/4 at 72 BPM. Harp, flute, cello, celesta, soft timpani.
    public static string Forest(string path)
    {
        NdSynth s = new NdSynth(72, 3, 32, 303);
        string[] A = { "Am", "F", "C", "G", "Am", "F", "Dm", "E" };
        string[] B = { "F", "G", "Em", "Am", "Dm", "Am", "F", "E" };
        string[] ch = NdSynth.Cat(A, A, B, A);
        string melA =
            "E5/1.5 D5/0.5 C5/1 " +
            "A4/2 C5/1 " +
            "G4/1 C5/1 E5/1 " +
            "D5/2 B4/1 " +
            "C5/1.5 B4/0.5 A4/1 " +
            "F5/1.5 E5/0.5 C5/1 " +
            "D5/1 F5/1 A5/1 " +
            "G#4/2 B4/1";
        string melB =
            "A5/2 G5/0.5 F5/0.5 " +
            "G5/1.5 F5/0.5 D5/1 " +
            "E5/1 G5/1 B5/1 " +
            "A5/2.5 r/0.5 " +
            "F5/1.5 E5/0.5 D5/1 " +
            "C5/1 E5/1 A5/1 " +
            "A5/1 G5/1 F5/1 " +
            "E5/2 G#4/1";

        s.Seq("flute", 0, melA, 0.6, 0.15, 0.45, 0, 0.97);          // A: a lonely flute
        s.Seq("solo", 24, melA, 0.7, -0.15, 0.45, -12, 0.97);       // A again: cello, an octave lower
        s.Seq("flute", 48, melB, 0.7, 0.15, 0.45, 0, 0.97);         // B: flute with the cello doubling below
        s.Seq("solo", 48, melB, 0.45, -0.15, 0.45, -12, 0.97);
        s.Seq("celesta", 72, melA, 0.5, 0.2, 0.55, 0, 1.0);         // A to finish: quiet bells, like light through leaves

        for (int bar = 0; bar < 32; bar++)
        {
            string c = ch[bar]; double b0 = bar * 3; int sec = bar / 8;
            int r2 = NdSynth.Root(c, 2), r3 = r2 + 12, t = NdSynth.Third(c);

            // harp rolling up and down the chord
            int[] arp = { 0, 7, 12, t + 12, 12, 7 };
            for (int k = 0; k < 6; k++) s.Note("harp", b0 + k * 0.5, 1.0, r3 + arp[k], k == 0 ? 0.45 : 0.32, -0.3 + 0.1 * k, 0.5);

            // low strings and a dark pad underneath
            s.Note("strings", b0, 3.0, r2, sec == 2 ? 0.55 : 0.4, -0.1, 0.5);
            if (sec == 2) foreach (int m in NdSynth.Voicing(c, 64)) s.Note("strings", b0, 3.0, m, 0.42, 0.2, 0.55);
            else foreach (int m in NdSynth.Voicing(c, 57)) s.Note("pad", b0, 3.0, m, 0.45, 0, 0.6);

            if (sec == 1 && bar % 2 == 0) { int[] v = NdSynth.Voicing(c, 76); s.Note("celesta", b0, 1, v[v.Length - 1], 0.3, 0.4, 0.6); }
            if (sec == 2) s.Note("timpani", b0, 1, r2, bar % 2 == 0 ? 0.35 : 0.25, 0, 0.4);
        }
        return s.Finish(path, 0.86, 0.3, 1.1);
    }

    // ============ MOUNTAIN + MINE SHAFT: "Beneath the Peaks". Grand, D minor, 88 BPM. Horns, brass, strings, timpani, taiko.
    public static string Mountain(string path)
    {
        NdSynth s = new NdSynth(88, 4, 32, 404);
        string[] A = { "Dm", "Bb", "F", "C", "Dm", "Bb", "Gm", "A" };
        string[] B = { "Bb", "F", "C", "Dm", "Gm", "Dm", "Bb", "A" };
        string[] ch = NdSynth.Cat(A, A, B, A);
        string melA =
            "D4/1.5 A4/0.5 A4/2 " +
            "Bb4/1 A4/0.5 G4/0.5 F4/2 " +
            "A4/1.5 C5/0.5 C5/1 Bb4/0.5 A4/0.5 " +
            "G4/3 r/1 " +
            "D4/1.5 A4/0.5 D5/2 " +
            "F5/1 E5/0.5 D5/0.5 D5/1 Bb4/1 " +
            "G4/1 Bb4/1 D5/1 G5/1 " +
            "E5/2 C#5/2";
        string melB =
            "F5/2 D5/1 F5/1 " +
            "C5/1.5 D5/0.5 C5/1 A4/1 " +
            "G4/1 C5/1 E5/1 G5/1 " +
            "F5/3 E5/0.5 D5/0.5 " +
            "D5/1.5 Bb4/0.5 G4/1 Bb4/1 " +
            "A4/1.5 D5/0.5 F5/1 A5/1 " +
            "Bb5/2 A5/1 G5/1 " +
            "A5/2 E5/1 C#5/1";

        s.Seq("horn", 0, melA, 0.75, 0.15, 0.4, 0, 0.95);            // A: a single horn call
        s.Seq("strings", 32, melA, 0.7, 0.25, 0.4, 12, 0.95);        // A again: strings on top, horn below
        s.Seq("horn", 32, melA, 0.45, 0.15, 0.4, 0, 0.95);
        s.Seq("brass", 64, melB, 0.8, 0.1, 0.4, 0, 0.95);            // B: full brass, strings an octave up
        s.Seq("strings", 64, melB, 0.6, 0.3, 0.4, 12, 0.95);
        s.Seq("horn", 96, melA, 0.8, 0.15, 0.4, 0, 0.95);            // A to finish: horns and strings together
        s.Seq("strings", 96, melA, 0.6, 0.25, 0.4, 12, 0.95);

        for (int bar = 0; bar < 32; bar++)
        {
            string c = ch[bar]; double b0 = bar * 4; int sec = bar / 8;
            int r2 = NdSynth.Root(c, 2), r3 = r2 + 12;

            // the driving string pattern (short bowed notes)
            int[] ost = { 0, 0, 7, 0, 0, 7, 12, 7 };
            for (int k = 0; k < 8; k++) s.Note("spicc", b0 + k * 0.5, 0.4, r3 + ost[k], k % 2 == 0 ? 0.7 : 0.5, -0.35, 0.35);

            // weight underneath: low strings, bass and a dark pad
            s.Note("strings", b0, 4.0, r2, 0.55, -0.1, 0.4);
            s.Note("bass", b0, 3.8, r2, 0.5, 0, 0.1);
            foreach (int m in NdSynth.Voicing(c, 52)) s.Note("pad", b0, 4.0, m, 0.4, 0, 0.5);

            // brass chords from the second section
            if (sec >= 1)
            {
                List<int> chord = new List<int>(NdSynth.Voicing(c, 57));
                if (!chord.Contains(r3)) chord.Add(r3);
                foreach (int m in chord)
                {
                    s.Note("brass", b0, 2.8, m, 0.45, -0.15, 0.35);
                    s.Note("brass", b0 + 3, 0.9, m, 0.35, -0.15, 0.35);
                }
            }

            // drums: timpani throughout, taiko in the B section, cymbals at the big moments
            s.Note("timpani", b0, 1, r2, 0.75, 0, 0.35);
            if (sec >= 1) { s.Note("timpani", b0 + 3, 0.5, r2, 0.45, 0, 0.35); s.Note("timpani", b0 + 3.5, 0.5, r2, 0.55, 0, 0.35); }
            if (sec == 2) { s.Note("taiko", b0, 1, 40, 0.6, 0, 0.3); s.Note("taiko", b0 + 2, 1, 40, 0.5, 0, 0.3); }
            if (bar == 8 || bar == 16 || bar == 24) s.Note("crash", b0, 1, 60, bar == 16 ? 0.7 : 0.55, 0.2, 0.4);
            if (bar == 15 || bar == 31)
            {
                s.Note("swell", b0 + 2, 2, 60, 0.8, 0, 0.3);
                for (int k = 0; k < 8; k++) s.Note("snare", b0 + 2 + k * 0.25, 0.25, 60, 0.15 + k * 0.06, 0.1, 0.3);
            }
        }
        return s.Finish(path, 0.88, 0.25, 1.1);
    }
}
'@

Add-Type -TypeDefinition $code -Language CSharp

$songs = [ordered]@{ farm = 'Farm'; city = 'City'; forest = 'Forest'; mountain = 'Mountain' }
foreach ($key in $songs.Keys) {
    if ($Only -and $Only -ne $key) { continue }
    $file = Join-Path $out ("music_" + $key + ".wav")
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $info = [NdSongs]::($songs[$key]).Invoke($file)
    Write-Host ("music_" + $key + ".wav  (" + [Math]::Round($timer.Elapsed.TotalSeconds, 1) + " s to make)  " + $info)
}
