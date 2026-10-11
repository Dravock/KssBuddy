using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace KssBuddy.Rendering
{
    /// <summary>Ellipse (Augenlid) oder Linie (Wimpern) in Bildkoordinaten.</summary>
    internal readonly record struct LidShape(double X, double Y, double Rx, double Ry);
    internal readonly record struct LashLine(double X1, double Y1, double X2, double Y2, double Width);

    /// <summary>
    /// Alle figurenspezifischen Maße des Rigs. Neue Figur hinzufügen: drei Bilder in Assets/&lt;Ordner&gt;
    /// (body.png, arms_upper.png, arms_fore.png, alle gleich groß) ablegen und hier ein Profil ergänzen.
    /// Alle Koordinaten sind Bildpixel (y nach unten).
    /// </summary>
    internal sealed class CharacterProfile
    {
        public string Id { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Folder { get; init; } = "";

        // Bildgröße
        public double W { get; init; }
        public double H { get; init; }
        public double CX { get; init; }                     // Körpermitte

        // Arme
        public double ShoulderDx { get; init; }             // Schulter: Abstand zur Mitte
        public double ShoulderY { get; init; }
        public double ArmDirX { get; init; } = 170;         // Richtung der Armachse in der Ausgangspose (nach unten außen)
        public double ArmDirY { get; init; } = 175;
        public double ElbowS { get; init; }                 // Ellbogen: Abstand zur Schulter entlang der Armachse
        public double ArmBias { get; init; }                // Korrektur der Armwinkel, damit die Hände passend zusammenfinden
        public double ElbowBias { get; init; }
        public double ArmMeshDx { get; init; } = 60;        // Arm-Mesh nur für |x - CX| >= dieser Wert
        public double ArmMeshY0 { get; init; } = 120;
        public double ArmMeshY1 { get; init; } = 500;

        // Kopf / Körper
        public Point Neck { get; init; }
        public Point Hip { get; init; }
        public double HeadY0 { get; init; }                 // Kopf-Gewicht: 1 oberhalb von Y0, fällt bis Y1 auf 0
        public double HeadY1 { get; init; }
        public double TiltY0 { get; init; }                 // Oberkörper neigt sich oberhalb von Y0 voll, bis Y1 auslaufend
        public double TiltY1 { get; init; }
        public double HipShiftY0 { get; init; }
        public double HipShiftY1 { get; init; }

        // Haare (seitliche Strähnen): Zone in y (ein-/ausblenden) und in x-Abstand zur Mitte
        public double[] HairY { get; init; } = new double[4];    // y0..y3
        public double[] HairDx { get; init; } = new double[4];   // dx0..dx3
        public double HairK0 { get; init; }
        public double HairK1 { get; init; }

        // Brust (Atmen)
        public double ChestY { get; init; }
        public double ChestSigma { get; init; }
        public double ChestDx0 { get; init; }
        public double ChestDx1 { get; init; }

        // Blinzeln
        public LidShape[] Lids { get; init; } = new LidShape[0];
        public LashLine[] Lashes { get; init; } = new LashLine[0];
        public Color SkinColor { get; init; }
        public Color LashColor { get; init; }

        // ---- Vorhandene Figuren ---------------------------------------------------------------

        public static readonly CharacterProfile Slytherin = new()
        {
            Id = "slytherin", DisplayName = "Draconia Malfoid", Folder = "slytherin",
            W = 614, H = 939, CX = 312,
            ShoulderDx = 62, ShoulderY = 200, ElbowS = 165,
            Neck = new Point(312, 140), Hip = new Point(312, 440),
            HeadY0 = 125, HeadY1 = 170, TiltY0 = 430, TiltY1 = 560, HipShiftY0 = 450, HipShiftY1 = 700,
            HairY = new[] { 190.0, 260, 450, 500 }, HairDx = new[] { 55.0, 75, 135, 160 }, HairK0 = 200, HairK1 = 260,
            ChestY = 270, ChestSigma = 90, ChestDx0 = 60, ChestDx1 = 110,
            Lids = new[] { new LidShape(283, 86.5, 18, 7.5), new LidShape(340, 86.5, 17.5, 7.5) },
            Lashes = new[] { new LashLine(266, 85, 300, 87, 2.4), new LashLine(324, 86, 356, 85, 2.4) },
            SkinColor = Color.FromRgb(0xF3, 0xE2, 0xDA), LashColor = Color.FromRgb(0x2A, 0x20, 0x18),
        };

        public static readonly CharacterProfile Eris = new()
        {
            Id = "eris", DisplayName = "Eris", Folder = "eris",
            W = 630, H = 939, CX = 315,
            ShoulderDx = 65, ShoulderY = 216, ElbowS = 150, ArmBias = -6, ElbowBias = -10,
            Neck = new Point(315, 160), Hip = new Point(315, 418),
            HeadY0 = 130, HeadY1 = 175, TiltY0 = 410, TiltY1 = 540, HipShiftY0 = 430, HipShiftY1 = 680,
            HairY = new[] { 190.0, 260, 520, 580 }, HairDx = new[] { 50.0, 75, 150, 175 }, HairK0 = 200, HairK1 = 260,
            ChestY = 260, ChestSigma = 90, ChestDx0 = 60, ChestDx1 = 110,
            Lids = new[] { new LidShape(288, 119, 16, 8.5), new LidShape(341, 119, 16, 8.5) },
            Lashes = new[] { new LashLine(273, 115, 303, 121, 2.6), new LashLine(326, 121, 357, 115, 2.6) },
            SkinColor = Color.FromRgb(0xF5, 0xC8, 0xAF), LashColor = Color.FromRgb(0x3A, 0x1A, 0x14),
        };

        public static readonly IReadOnlyList<CharacterProfile> All = new[] { Slytherin, Eris };

        public static CharacterProfile ById(string? id)
        {
            foreach (var c in All) if (c.Id == id) return c;
            return Slytherin;
        }
    }
}
