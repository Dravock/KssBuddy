using System;

namespace KssBuddy.Rendering
{
    internal enum AnimationClip { Idle, Wave, Dance, UwU, Cheer, Stretch, Shy, Sleepy, Scold, HipShake }

    /// <summary>
    /// Zustandsmaschine der Animationen. Idle läuft immer im Hintergrund; Clips (Winken, Tanzen)
    /// werden weich ein- und ausgeblendet. Neue Clips: Pose-Funktion schreiben und in Update() eintragen.
    /// </summary>
    internal sealed class AnimationController
    {
        private const double FadeSeconds = 0.4;
        private const double WaveDuration = 3.4;
        private const double StretchDuration = 4.5;
        private const double CheerDuration = 4.0;
        private const double ScoldDuration = 5.2;

        /// <summary>Gelegentliche Zufalls-Aktion im Leerlauf (Strecken oder Winken). Zum Abschalten auf false setzen.</summary>
        public bool AutoActions { get; set; } = true;
        private double _nextAuto = 60;
        private readonly Random _rng = new();

        private double _time;
        private AnimationClip _clip = AnimationClip.Idle;
        private double _clipTime;
        private double _weight;
        private bool _fadingOut;

        private double _nextBlink = 2.0;
        private double _blinkT = -1;
        private bool _doubleBlink;

        /// <summary>0 = Augen offen, 1 = zu.</summary>
        public double Blink { get; private set; }
        public AnimationClip Current => _clip;

        public void Play(AnimationClip clip)
        {
            if (clip == AnimationClip.Idle) { _fadingOut = true; return; }
            _clip = clip;
            _clipTime = 0;
            _fadingOut = false;
        }

        /// <summary>Endlos-Tänze ein-/ausschalten: läuft der Clip schon, geht es zurück zu Idle.</summary>
        public void Toggle(AnimationClip clip) => Play(_clip == clip && !_fadingOut ? AnimationClip.Idle : clip);

        private double _eyesClosed;   // 0..1, wird bei UwU hochgezogen

        public Pose Update(double dt)
        {
            _time += dt;
            UpdateBlink(dt);

            Pose idle = IdlePose(_time);
            if (_clip == AnimationClip.Idle)
            {
                _eyesClosed = 0;
                if (AutoActions && _time >= _nextAuto)
                {
                    Play(_rng.NextDouble() < 0.5 ? AnimationClip.Stretch : AnimationClip.Wave);
                    _nextAuto = _time + 60 + _rng.NextDouble() * 90;
                }
                return idle;
            }

            _clipTime += dt;
            // Einmal-Clips beenden sich selbst
            if ((_clip == AnimationClip.Wave && _clipTime > WaveDuration) ||
                (_clip == AnimationClip.Stretch && _clipTime > StretchDuration) ||
                (_clip == AnimationClip.Cheer && _clipTime > CheerDuration) ||
                (_clip == AnimationClip.Scold && _clipTime > ScoldDuration)) _fadingOut = true;

            _weight = _fadingOut ? Math.Max(0, _weight - dt / FadeSeconds)
                                 : Math.Min(1, _weight + dt / FadeSeconds);
            if (_fadingOut && _weight <= 0) { _clip = AnimationClip.Idle; _fadingOut = false; return idle; }

            Pose clip = _clip switch
            {
                AnimationClip.Wave => WavePose(idle, _clipTime),
                AnimationClip.UwU => UwUPose(idle, _clipTime),
                AnimationClip.Cheer => CheerPose(idle, _clipTime),
                AnimationClip.Stretch => StretchPose(idle, _clipTime),
                AnimationClip.Shy => ShyPose(idle, _clipTime),
                AnimationClip.Sleepy => SleepyPose(idle, _clipTime),
                AnimationClip.Scold => ScoldPose(idle, _clipTime),
                AnimationClip.HipShake => HipShakePose(idle, _clipTime),
                _ => DancePose(idle, _clipTime),
            };
            double k = _weight * _weight * (3 - 2 * _weight); // smoothstep

            // Augen-Zielwert je Clip: UwU/Schüchtern/Strecken = zu, Einnicken = schwere Lider mit Auf und Ab
            double eyes = _clip switch
            {
                AnimationClip.UwU => 1,
                AnimationClip.Shy => 1,
                AnimationClip.Stretch => 1,
                AnimationClip.Sleepy => 0.65 + 0.35 * Math.Sin(Math.PI * _clipTime / 6.0),
                _ => 0,
            };
            _eyesClosed = eyes * k;
            Blink = Math.Max(Blink, _eyesClosed);

            return Pose.Lerp(idle, clip, k);
        }

        // ---- Clips -------------------------------------------------------------------------

        private static Pose IdlePose(double t)
        {
            double w = 2 * Math.PI;
            return new Pose
            {
                Breath = Math.Sin(w * t / 3.8),
                BodyTilt = 0.7 * Math.Sin(w * t / 7.0),
                HipShift = 2.5 * Math.Sin(w * t / 7.0 + Math.PI),
                HeadTilt = 1.6 * Math.Sin(w * t / 5.3 + 1.0),
                ArmL = 30 + 0.8 * Math.Sin(w * t / 3.8 + 0.6),
                ArmR = 30 + 0.8 * Math.Sin(w * t / 3.8 + 0.6),
                ElbowL = 60 + 1.5 * Math.Sin(w * t / 3.8 + 1.2),
                ElbowR = 60 + 1.5 * Math.Sin(w * t / 3.8 + 1.2),
                Hair = 5 * Math.Sin(w * t / 4.2),
                Hair2 = 5 * Math.Sin(w * t / 4.2 - 0.9),
            };
        }

        private static Pose WavePose(Pose p, double t)
        {
            double w = 2 * Math.PI;
            p.ArmR = -114;                             // rechter Arm hoch (Idle: 30 = Arm hängt, Hände im Schoß)
            p.ElbowR = 25 * Math.Sin(w * 2.6 * t);     // Unterarm wedelt
            p.HeadTilt = 5;
            p.BodyTilt = -1.5;
            return p;
        }

        private static Pose DancePose(Pose p, double t)
        {
            double s = Math.Sin(Math.PI * t);          // 2-Sekunden-Zyklus
            p.Bounce = 10 * Math.Abs(Math.Sin(Math.PI * t));
            p.HipShift = 22 * s;
            p.BodyTilt = -5 * s;
            p.HeadTilt = 7 * Math.Sin(Math.PI * t + 0.5);
            p.ArmL = -35 + 55 * s;                     // Arme pumpen im Wechsel (negativ = nach oben)
            p.ArmR = -35 - 55 * s;
            p.ElbowL = 5 + 10 * Math.Sin(2 * Math.PI * t);
            p.ElbowR = 5 + 10 * Math.Sin(2 * Math.PI * t + Math.PI);
            p.Hair = 10 * Math.Sin(Math.PI * t - 0.3);
            p.Hair2 = 10 * Math.Sin(Math.PI * t - 1.2);
            return p;
        }

        /// <summary>
        /// UwU-Tanz (eigene Interpretation): Hände als "Pfötchen" an den Wangen, Ellbogen seitlich,
        /// Hüfte schwingt von links nach rechts, Kopf neigt sich gegenläufig, kleines Wippen im Takt. Zyklus 2 s.
        /// </summary>
        private static Pose UwUPose(Pose p, double t)
        {
            double s = Math.Sin(Math.PI * t), c = Math.Cos(Math.PI * t);
            p.ArmL = -62 + 8 * s;
            p.ArmR = -62 - 8 * s;
            p.ElbowL = -135 + 6 * c;
            p.ElbowR = -135 - 6 * c;
            p.HipShift = 20 * s;
            p.BodyTilt = -5 * s;
            p.HeadTilt = -8 * s;
            p.Bounce = 6 * Math.Abs(Math.Sin(Math.PI * t));
            p.Hair = 8 * Math.Sin(Math.PI * t - 0.3);
            p.Hair2 = 8 * Math.Sin(Math.PI * t - 1.2);
            return p;
        }

        /// <summary>
        /// Ermahnen: eine Hand (rechte der Figur = linke Bildseite) stemmt sich in die Hüfte, die andere hebt sich
        /// neben den Kopf und wackelt im Takt "tz-tz-tz" mit Pausen. Dazu Kopf leicht schräg, Gewicht auf ein Bein.
        /// Einmal-Clip, ca. 5 s.
        /// </summary>
        private static Pose ScoldPose(Pose p, double t)
        {
            // Wackeln in Schüben: ca. 1,3 s aktiv (3-4 Wackler), dann 0,6 s Pause
            double cyc = t % 1.9;
            double env = cyc < 1.3 ? Math.Sin(Math.PI * cyc / 1.3) : 0;
            double wag = Math.Sin(2 * Math.PI * 3.4 * t) * env;

            p.ArmL = 15; p.ElbowL = 85;                       // Hand in der Hüfte, Ellbogen steht seitlich ab
            p.ArmR = -12 + 3 * wag;                           // Hand neben dem Kopf
            p.ElbowR = -115 + 13 * wag;                       // Unterarm wackelt
            p.HeadTilt = -4 + 1.5 * env;
            p.BodyTilt = 1.0;
            p.HipShift = -6;
            p.Breath = 0.4;
            return p;
        }

        /// <summary>
        /// Hüftschwung: beide Hände in den Hüften, die Hüfte schwingt schnell von links nach rechts, der Oberkörper
        /// gleicht gegenläufig aus, Kopf und Haare gehen mit. Endlos (Zyklus 0,9 s), nochmal klicken beendet.
        /// </summary>
        private static Pose HipShakePose(Pose p, double t)
        {
            double s = Math.Sin(2 * Math.PI * t / 0.9);
            p.ArmL = 15; p.ElbowL = 85;                       // beide Hände in der Hüfte
            p.ArmR = 15; p.ElbowR = 85;
            p.HipShift = 26 * s;                              // Hüfte (und Beinansatz) nach links/rechts
            p.BodyTilt = -6 * s;                              // Oberkörper dagegen
            p.HeadTilt = 5 * s;
            p.Bounce = 3 * Math.Abs(Math.Cos(Math.PI * t / 0.9));   // kleines Federn in den Knien
            p.Hair = -8 * s;
            p.Hair2 = -8 * Math.Sin(2 * Math.PI * t / 0.9 - 0.8);
            return p;
        }

        /// <summary>Jubeln: Arme in V-Form hoch, hüpft, Haare fliegen. Einmal-Clip, ca. 4 s.</summary>
        private static Pose CheerPose(Pose p, double t)
        {
            double j = Math.Abs(Math.Sin(2 * Math.PI * t * 0.9));   // ca. 1,8 Sprünge pro Sekunde
            double w = Math.Sin(2 * Math.PI * t * 0.9);
            p.ArmL = -88 + 6 * w;
            p.ArmR = -88 - 6 * w;
            p.ElbowL = 0; p.ElbowR = 0;
            p.Bounce = 24 * j;
            p.HeadTilt = 4 * w;
            p.BodyTilt = 2 * w;
            p.Hair = 9 * Math.Sin(2 * Math.PI * t * 0.9 - 0.4);
            p.Hair2 = 9 * Math.Sin(2 * Math.PI * t * 0.9 - 1.3);
            return p;
        }

        /// <summary>Strecken: Arme langsam über den Kopf, leichte Rückneigung, tiefes Atmen. Einmal-Clip, ca. 4,5 s.</summary>
        private static Pose StretchPose(Pose p, double t)
        {
            double up = Math.Sin(Math.PI * Math.Clamp(t / StretchDuration, 0, 1));   // 0 -> 1 -> 0
            double e = up * up * (3 - 2 * up);
            p.ArmL = 30 + (-125 - 30) * e;
            p.ArmR = 30 + (-125 - 30) * e;
            p.ElbowL = 60 * (1 - e);
            p.ElbowR = 60 * (1 - e);
            p.BodyTilt = -3 * e * Math.Sin(2 * t);
            p.HeadTilt = -3 * e;
            p.Breath = 1.5 * e;
            return p;
        }

        /// <summary>Schüchtern: Hände an den Wangen, Kopf zur Seite, sanftes Schwanken, Augen zu.</summary>
        private static Pose ShyPose(Pose p, double t)
        {
            double s = Math.Sin(2 * Math.PI * t / 3.2);
            p.ArmL = -40; p.ArmR = -40;
            p.ElbowL = -150 + 3 * s; p.ElbowR = -150 - 3 * s;
            p.HeadTilt = 8 + 2 * s;
            p.BodyTilt = -2 + 1.5 * s;
            p.HipShift = 5 * s;
            p.Hair = 4 * s;
            p.Hair2 = 4 * Math.Sin(2 * Math.PI * t / 3.2 - 0.8);
            return p;
        }

        /// <summary>Einnicken: Kopf sackt langsam zur Seite und ruckt wieder hoch, schwere Lider, tiefes Atmen. Endlos.</summary>
        private static Pose SleepyPose(Pose p, double t)
        {
            double cyc = t % 6.0;                                       // 6-Sekunden-Zyklus
            double drop = cyc < 4.5 ? (cyc / 4.5) * (cyc / 4.5)             // Kopf sackt langsam ab
                                    : Math.Max(0, 1 - (cyc - 4.5) / 0.4);   // ruckt schnell wieder hoch
            p.HeadTilt = 13 * drop;
            p.BodyTilt = 1.5 * drop;
            p.Breath = 1.4 * Math.Sin(2 * Math.PI * t / 5.0);
            p.Bounce = -3 * drop;
            return p;
        }

        // ---- Blinzeln ---------------------------------------------------------------------

        private void UpdateBlink(double dt)
        {
            if (_blinkT < 0 && _time >= _nextBlink)
            {
                _blinkT = 0;
                _doubleBlink = _rng.NextDouble() < 0.2;
            }
            if (_blinkT < 0) { Blink = 0; return; }

            _blinkT += dt;
            const double d = 0.18;
            double x = _blinkT;
            Blink = x < 0.06 ? x / 0.06 : x < 0.10 ? 1 : x < d ? 1 - (x - 0.10) / (d - 0.10) : 0;

            if (_blinkT >= d)
            {
                _blinkT = -1;
                _nextBlink = _time + (_doubleBlink ? 0.2 : 2.5 + _rng.NextDouble() * 3.5);
            }
        }
    }
}
