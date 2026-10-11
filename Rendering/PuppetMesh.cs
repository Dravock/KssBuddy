using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace KssBuddy.Rendering
{
    /// <summary>
    /// "Puppet-Rig": Der Charakter besteht aus drei Texturen mit identischem Bildformat:
    ///   body.png       = alles außer den Armen (Kopf, Haare, Rumpf, Rock/Hose, Beine)
    ///   arms_upper.png = Oberarme/Schulterteil der Ärmel  (wird HINTER dem Körper gezeichnet)
    ///   arms_fore.png  = Unterarme mit Händen              (wird VOR dem Körper gezeichnet)
    /// Alle liegen auf einem Gitter (Mesh). Pro Frame werden die Gitterpunkte nach der aktuellen Pose verschoben:
    /// Die Arme drehen starr um Schulter und Ellbogen, der Körper bekommt weiche Verformungen
    /// (Kopfneigung, Haarschwingen, Atmen, Hüftschwung, Hüpfen). Alle Maße stehen im CharacterProfile.
    /// </summary>
    internal sealed class PuppetMesh
    {
        private const int NX = 40, NY = 60;

        private readonly CharacterProfile _c;
        private readonly double _w, _h, _cx;
        private readonly double _ux, _uy;                       // Armachse (normiert)
        private readonly Point _neck, _hip;

        private readonly Point[] _base;
        private readonly Point3D[] _bodyPos, _upperPos, _forePos;
        private readonly MeshGeometry3D _bodyMesh = new(), _upperMesh = new(), _foreMesh = new();

        private readonly Patch[] _lids, _lashes;
        private readonly SolidColorBrush _lidBrush, _lashBrush;

        public Model3DGroup Model { get; } = new();
        public CharacterProfile Profile => _c;

        public PuppetMesh(CharacterProfile profile, ImageSource body, ImageSource upperArm, ImageSource foreArm)
        {
            _c = profile;
            _w = profile.W; _h = profile.H; _cx = profile.CX;
            double len = Math.Sqrt(profile.ArmDirX * profile.ArmDirX + profile.ArmDirY * profile.ArmDirY);
            _ux = profile.ArmDirX / len; _uy = profile.ArmDirY / len;
            _neck = profile.Neck; _hip = profile.Hip;

            // Gitter + Texturkoordinaten (für alle Texturen identisch)
            _base = new Point[(NX + 1) * (NY + 1)];
            var uv = new PointCollection();
            for (int j = 0; j <= NY; j++)
                for (int i = 0; i <= NX; i++)
                {
                    double x = _w * i / NX, y = _h * j / NY;
                    _base[j * (NX + 1) + i] = new Point(x, y);
                    uv.Add(new Point(x / _w, y / _h));
                }

            var bodyTri = new Int32Collection();
            var armTri = new Int32Collection();
            for (int j = 0; j < NY; j++)
                for (int i = 0; i < NX; i++)
                {
                    int a = j * (NX + 1) + i, b = a + 1, c = a + (NX + 1), d = c + 1;
                    foreach (var t in new[] { a, c, d, a, d, b }) bodyTri.Add(t);

                    // Arm-Mesh nur dort, wo Arme sein können (spart Rechenzeit)
                    double qx = (_base[a].X + _base[d].X) / 2, qy = (_base[a].Y + _base[d].Y) / 2;
                    if (Math.Abs(qx - _cx) >= profile.ArmMeshDx && qy >= profile.ArmMeshY0 && qy <= profile.ArmMeshY1)
                        foreach (var t in new[] { a, c, d, a, d, b }) armTri.Add(t);
                }

            _bodyPos = new Point3D[_base.Length];
            _upperPos = new Point3D[_base.Length];
            _forePos = new Point3D[_base.Length];
            _bodyMesh.TextureCoordinates = uv;  _bodyMesh.TriangleIndices = bodyTri;
            _upperMesh.TextureCoordinates = uv; _upperMesh.TriangleIndices = armTri;
            _foreMesh.TextureCoordinates = uv;  _foreMesh.TriangleIndices = armTri;

            // Zeichenreihenfolge = hinten nach vorn (z steigt): Oberarme, Körper, Unterarme, Lider, Wimpern.
            Model.Children.Add(new AmbientLight(Colors.White));
            Model.Children.Add(MakeModel(_upperMesh, new DiffuseMaterial(new ImageBrush(upperArm))));
            Model.Children.Add(MakeModel(_bodyMesh, new DiffuseMaterial(new ImageBrush(body))));
            Model.Children.Add(MakeModel(_foreMesh, new DiffuseMaterial(new ImageBrush(foreArm))));

            // Augenlider (Hautfarbe + Wimpernlinie) fürs Blinzeln; folgen der Kopfbewegung
            _lidBrush = new SolidColorBrush(profile.SkinColor);
            _lashBrush = new SolidColorBrush(profile.LashColor);
            _lids = new Patch[profile.Lids.Length];
            for (int i = 0; i < _lids.Length; i++)
            {
                var l = profile.Lids[i];
                _lids[i] = Patch.Ellipse(l.X, l.Y, l.Rx, l.Ry);
                Model.Children.Add(MakeModel(_lids[i].Mesh, new DiffuseMaterial(_lidBrush)));
            }
            _lashes = new Patch[profile.Lashes.Length];
            for (int i = 0; i < _lashes.Length; i++)
            {
                var l = profile.Lashes[i];
                _lashes[i] = Patch.Line(l.X1, l.Y1, l.X2, l.Y2, l.Width);
                Model.Children.Add(MakeModel(_lashes[i].Mesh, new DiffuseMaterial(_lashBrush)));
            }

            Apply(default, 0);
        }

        private static GeometryModel3D MakeModel(MeshGeometry3D mesh, Material m) =>
            new() { Geometry = mesh, Material = m, BackMaterial = m };

        // ---- Pro-Frame-Update ------------------------------------------------------------------

        public void Apply(in Pose p, double blink)
        {
            for (int i = 0; i < _base.Length; i++)
            {
                DeformBody(_base[i], p, out double bx, out double by);
                _bodyPos[i] = new Point3D(bx, _h - by, 1);
                DeformArm(_base[i], p, false, out double ux, out double uy);
                _upperPos[i] = new Point3D(ux, _h - uy, 0);
                DeformArm(_base[i], p, true, out double fx, out double fy);
                _forePos[i] = new Point3D(fx, _h - fy, 2);
            }
            _bodyMesh.Positions = new Point3DCollection(_bodyPos);
            _upperMesh.Positions = new Point3DCollection(_upperPos);
            _foreMesh.Positions = new Point3DCollection(_forePos);

            _lidBrush.Opacity = blink;
            _lashBrush.Opacity = blink;
            if (blink > 0.01)
            {
                foreach (var l in _lids) l.Update(this, p, 3);
                foreach (var l in _lashes) l.Update(this, p, 4);
            }
        }

        // ---- Verformung ------------------------------------------------------------------------

        private static double S(double e0, double e1, double x)
        {
            double t = Math.Clamp((x - e0) / (e1 - e0), 0, 1);
            return t * t * (3 - 2 * t);
        }

        private static Point Rot(Point v, Point pivot, double deg)
        {
            double r = deg * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
            double dx = v.X - pivot.X, dy = v.Y - pivot.Y;
            return new Point(pivot.X + dx * c - dy * s, pivot.Y + dx * s + dy * c);
        }

        /// <summary>Körper: Kopf neigen, Haare schwingen, Atmen. Danach gemeinsame Hüft-/Hüpf-Transformation.</summary>
        private void DeformBody(Point v0, in Pose p, out double ox, out double oy)
        {
            var c = _c;
            Point v = v0;
            double dx = Math.Abs(v0.X - _cx), y0 = v0.Y;

            double wHead = 1 - S(c.HeadY0, c.HeadY1, y0);
            if (wHead > 0 && p.HeadTilt != 0) v = Rot(v, _neck, p.HeadTilt * wHead);

            double wHair = S(c.HairY[0], c.HairY[1], y0) * (1 - S(c.HairY[2], c.HairY[3], y0))
                         * S(c.HairDx[0], c.HairDx[1], dx) * (1 - S(c.HairDx[2], c.HairDx[3], dx));
            if (wHair > 0)
            {
                double k = Math.Clamp((y0 - c.HairK0) / c.HairK1, 0, 1);
                v.X += (p.Hair * (1 - k) + p.Hair2 * k) * wHair;
            }

            double g = (y0 - c.ChestY) / c.ChestSigma;
            double wChest = Math.Exp(-g * g) * (1 - S(c.ChestDx0, c.ChestDx1, dx));
            if (wChest > 0)
            {
                v.X += (v0.X - _cx) * p.Breath * 0.012 * wChest;
                v.Y -= p.Breath * 2.0 * wChest;
            }

            Post(ref v, v0, p);
            ox = v.X; oy = v.Y;
        }

        /// <summary>Arme: starre Drehung um die Schulter; die Unterarm-Ebene (fore) dreht zusätzlich um den Ellbogen.</summary>
        private void DeformArm(Point v0, in Pose p, bool fore, out double ox, out double oy)
        {
            int side = v0.X < _cx ? -1 : 1;
            double sgn = side < 0 ? -1 : 1;                  // links dreht gegen den Uhrzeigersinn nach unten
            var sh = new Point(_cx + side * _c.ShoulderDx, _c.ShoulderY);
            double ux = side * _ux, uy = _uy;
            double ang = (side < 0 ? p.ArmL : p.ArmR) + _c.ArmBias;
            double elbow = (side < 0 ? p.ElbowL : p.ElbowR) + _c.ElbowBias;

            Point v = Rot(v0, sh, sgn * ang);
            if (fore)
            {
                var e0 = new Point(sh.X + ux * _c.ElbowS, sh.Y + uy * _c.ElbowS);
                Point e = Rot(e0, sh, sgn * ang);
                v = Rot(v, e, sgn * elbow);
            }

            Post(ref v, v0, p);
            ox = v.X; oy = v.Y;
        }

        /// <summary>Für Körper und Arme gleich: Oberkörper neigt sich um die Hüfte, Hüfte verschiebt sich, Hüpfen.</summary>
        private void Post(ref Point v, Point v0, in Pose p)
        {
            double y0 = v0.Y;
            double wBody = 1 - S(_c.TiltY0, _c.TiltY1, y0);
            if (wBody > 0 && p.BodyTilt != 0) v = Rot(v, _hip, p.BodyTilt * wBody);
            v.X += p.HipShift * (1 - S(_c.HipShiftY0, _c.HipShiftY1, y0));
            v.Y -= p.Bounce;
        }

        // ---- Kleine Zusatz-Meshes (Lider, Wimpern) -------------------------------------------

        private sealed class Patch
        {
            private readonly Point[] _pts;
            public MeshGeometry3D Mesh { get; } = new();

            private Patch(Point[] pts, Int32Collection tri)
            {
                _pts = pts;
                Mesh.TriangleIndices = tri;
            }

            public static Patch Ellipse(double cx, double cy, double rx, double ry, int n = 14)
            {
                var pts = new Point[n + 1];
                pts[0] = new Point(cx, cy);
                for (int i = 0; i < n; i++)
                {
                    double a = 2 * Math.PI * i / n;
                    pts[i + 1] = new Point(cx + rx * Math.Cos(a), cy + ry * Math.Sin(a));
                }
                var tri = new Int32Collection();
                for (int i = 1; i <= n; i++) { tri.Add(0); tri.Add(i); tri.Add(i % n + 1); }
                return new Patch(pts, tri);
            }

            public static Patch Line(double x1, double y1, double x2, double y2, double width)
            {
                double dx = x2 - x1, dy = y2 - y1, len = Math.Sqrt(dx * dx + dy * dy);
                double nx = -dy / len * width / 2, ny = dx / len * width / 2;
                var pts = new[]
                {
                    new Point(x1 + nx, y1 + ny), new Point(x1 - nx, y1 - ny),
                    new Point(x2 - nx, y2 - ny), new Point(x2 + nx, y2 + ny)
                };
                return new Patch(pts, new Int32Collection { 0, 1, 2, 0, 2, 3 });
            }

            public void Update(PuppetMesh owner, in Pose p, double z)
            {
                var res = new Point3D[_pts.Length];
                for (int i = 0; i < _pts.Length; i++)
                {
                    owner.DeformBody(_pts[i], p, out double x, out double y);
                    res[i] = new Point3D(x, owner._h - y, z);
                }
                Mesh.Positions = new Point3DCollection(res);
            }
        }
    }
}
