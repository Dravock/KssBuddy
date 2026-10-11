using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;

namespace KssBuddy.Rendering
{
    /// <summary>
    /// Spielt die Video-Clips (PNG-Sequenzen mit Alpha) ab und sorgt für weiche Übergänge:
    /// Idle läuft als Endlosschleife; Aktionen (Winken, Tanzen) starten und enden an den Frames, deren Pose
    /// dem jeweils anderen Clip am ähnlichsten ist (vorberechnet in Assets/video/transitions.json),
    /// und werden dort kurz überblendet.
    /// </summary>
    public sealed class VideoDirector
    {
        private sealed class ClipInfo { public int First, Last; public double Fps; public int Count => Last - First + 1; }
        private sealed class Edge { public int[] B = Array.Empty<int>(); public double[] C = Array.Empty<double>(); }
        private sealed record Plan(int A, string To, int B);

        private const double Fade = 0.3;                 // Dauer der Überblendung in Sekunden
        private readonly Dictionary<string, ClipInfo> _clips = new();
        private readonly Dictionary<string, Edge> _edges = new();
        private readonly Dictionary<string, BitmapSource> _cache = new();

        private string _cur = "idle";
        private double _pos;                              // Frame-Offset im aktuellen Clip
        private Plan? _plan;
        private string? _pending;                         // gewünschte Aktion

        private string? _to;                              // laufende Überblendung
        private double _toPos, _blendT;

        public bool Loaded { get; }
        public string Current => _cur;
        public bool InAction => !IsBase(_cur) || _to != null && !IsBase(_to);

        // Ruhe-Zustände, die als Endlosschleife laufen und überall betreten werden dürfen
        private static bool IsBase(string clip) => clip == "idle" || clip == "idle2";
        private static readonly Random Rng = new();

        public VideoDirector()
        {
            try
            {
                var res = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/video/transitions.json"));
                using var doc = JsonDocument.Parse(res.Stream);
                foreach (var c in doc.RootElement.GetProperty("clips").EnumerateObject())
                    _clips[c.Name] = new ClipInfo
                    {
                        First = c.Value.GetProperty("first").GetInt32(),
                        Last = c.Value.GetProperty("last").GetInt32(),
                        Fps = c.Value.GetProperty("fps").GetDouble()
                    };
                foreach (var e in doc.RootElement.GetProperty("edges").EnumerateObject())
                {
                    var edge = new Edge();
                    var b = new List<int>(); foreach (var x in e.Value.GetProperty("b").EnumerateArray()) b.Add(x.GetInt32());
                    var c = new List<double>(); foreach (var x in e.Value.GetProperty("c").EnumerateArray()) c.Add(x.GetDouble());
                    edge.B = b.ToArray(); edge.C = c.ToArray();
                    _edges[e.Name] = edge;
                }
                Loaded = _clips.ContainsKey("idle");
            }
            catch { Loaded = false; }
        }

        /// <summary>Aktion anfordern ("wave", "dance"/"tanz", "yawn"/"gaehnen"); "idle" bricht eine laufende Aktion nicht ab.</summary>
        public void Request(string clip)
        {
            if (clip == "dance") clip = "tanz";
            if (clip == "yawn") clip = "gaehnen";
            if (!_clips.ContainsKey(clip) || IsBase(clip)) return;
            if (_pending == clip || _cur == clip && _to == null) return;
            _pending = clip;
            if (IsBase(_cur) && _to == null) _plan = null;      // neu planen
        }

        /// <summary>Zeit voranschreiben. Gibt das aktuelle Bild, ggf. ein zweites Bild und dessen Deckkraft zurück.</summary>
        public (BitmapSource Frame, BitmapSource? Next, double Blend) Update(double dt)
        {
            var ci = _clips[_cur];
            _pos += dt * ci.Fps;

            if (_to != null)
            {
                _toPos += dt * _clips[_to].Fps;
                _blendT += dt;
                if (_blendT >= Fade) { _cur = _to; _pos = _toPos; _to = null; ci = _clips[_cur]; }
            }

            if (_to == null)
            {
                ci = _clips[_cur];
                if (_plan == null) MakePlan();
                if (_plan != null && _pos >= _plan.A)
                {
                    _to = _plan.To; _toPos = _plan.B; _blendT = 0;
                    if (_plan.To == _pending) _pending = null;
                    _plan = null;
                }
                else if (_pos >= ci.Count)                       // Sicherheitsnetz: nie über das Ende laufen
                {
                    if (IsBase(_cur)) _pos = 0; else { _pos = ci.Count - 1; MakePlan(true); }
                }
            }

            var frame = Get(_cur, _pos);
            if (_to == null) return (frame, null, 0);
            double t = Math.Clamp(_blendT / Fade, 0, 1);
            return (frame, Get(_to, _toPos), t * t * (3 - 2 * t));
        }

        private void MakePlan(bool force = false)
        {
            var ci = _clips[_cur];
            int cur = Math.Clamp((int)_pos, 0, ci.Count - 1);
            string target; int lo, hi;

            if (IsBase(_cur))
            {
                if (_pending != null) { target = _pending; lo = cur; hi = Math.Min(cur + 30, ci.Count - 1); }
                else
                {
                    // am Schleifenende: gleiche Schleife wiederholen oder zur anderen Ruhe-Variante wechseln
                    int win = Math.Min(60, ci.Count / 2);
                    string other = _cur == "idle" ? "idle2" : "idle";
                    double switchChance = _cur == "idle" ? 0.4 : 0.25;
                    target = _clips.ContainsKey(other) && Rng.NextDouble() < switchChance ? other : _cur;
                    lo = Math.Max(cur, ci.Count - win); hi = ci.Count - 1;
                }
            }
            else
            {
                target = _clips.ContainsKey("idle2") && Rng.NextDouble() < 0.4 ? "idle2" : "idle";
                lo = force ? ci.Count - 1 : Math.Max(cur, (int)(ci.Count * 0.6));
                hi = ci.Count - 1;
            }

            if (!_edges.TryGetValue($"{_cur}>{target}", out var e)) return;
            int bestA = -1; double bestScore = double.MaxValue;
            for (int a = lo; a <= hi && a < e.C.Length; a++)
            {
                double score = e.C[a] + 0.02 * Math.Max(a - cur, 0);   // möglichst ähnliche Pose, aber nicht ewig warten
                if (score < bestScore) { bestScore = score; bestA = a; }
            }
            if (bestA >= 0) _plan = new Plan(bestA, target, e.B[bestA]);
        }

        private BitmapSource Get(string clip, double pos)
        {
            var ci = _clips[clip];
            int i = ci.First + Math.Clamp((int)pos, 0, ci.Count - 1);
            string key = $"{clip}/{i}";
            if (_cache.TryGetValue(key, out var bmp)) return bmp;
            if (_cache.Count > 16) _cache.Clear();
            var b = new BitmapImage();
            b.BeginInit();
            b.UriSource = new Uri($"pack://application:,,,/Assets/video/{clip}/f{i:000}.png");
            b.CacheOption = BitmapCacheOption.OnLoad;
            b.EndInit();
            b.Freeze();
            _cache[key] = b;
            return b;
        }
    }
}
