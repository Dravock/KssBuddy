using System;

namespace KssBuddy.Rendering
{
    /// <summary>
    /// Beschreibt eine Körperhaltung als wenige Zahlen. Die Animationen liefern pro Frame eine Pose,
    /// das Mesh (PuppetMesh) verformt daraus das Bild. Winkel in Grad, Strecken in Bildpixeln (718x934).
    /// </summary>
    internal struct Pose
    {
        public double Breath;       // -1..1  Atmung (Brustkorb dehnt sich)
        public double BodyTilt;     // Neigung des Oberkörpers um die Hüfte (+ = nach rechts)
        public double HipShift;     // seitliche Hüftverschiebung in px
        public double Bounce;       // Hüpfen nach oben in px
        public double HeadTilt;     // Kopfneigung um den Hals
        public double ArmL, ArmR;   // Schulterwinkel: 0 = T-Pose, 90 = Arm hängt senkrecht, negativ = Arm oben
        public double ElbowL, ElbowR; // zusätzlicher Ellbogenwinkel (+ = Unterarm nach unten)
        public double Hair, Hair2;  // seitliches Haarschwingen (Hair2 = verzögert, für die Spitzen)

        public static Pose Lerp(in Pose a, in Pose b, double t)
        {
            static double L(double x, double y, double k) => x + (y - x) * k;
            return new Pose
            {
                Breath = L(a.Breath, b.Breath, t),
                BodyTilt = L(a.BodyTilt, b.BodyTilt, t),
                HipShift = L(a.HipShift, b.HipShift, t),
                Bounce = L(a.Bounce, b.Bounce, t),
                HeadTilt = L(a.HeadTilt, b.HeadTilt, t),
                ArmL = L(a.ArmL, b.ArmL, t),
                ArmR = L(a.ArmR, b.ArmR, t),
                ElbowL = L(a.ElbowL, b.ElbowL, t),
                ElbowR = L(a.ElbowR, b.ElbowR, t),
                Hair = L(a.Hair, b.Hair, t),
                Hair2 = L(a.Hair2, b.Hair2, t),
            };
        }
    }
}
