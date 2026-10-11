using System;
using System.Windows.Media.Imaging;

namespace KssBuddy.Rendering
{
    /// <summary>
    /// Abspielbarer Video-Clip als PNG-Sequenz mit Alpha (Assets/video/{name}/fNNN.png).
    /// Die Bilder werden erst beim Anzeigen geladen, damit der Arbeitsspeicher klein bleibt.
    /// </summary>
    public sealed class VideoClip
    {
        public string Folder { get; }
        public int FirstFrame { get; }
        public int LastFrame { get; }
        public double Fps { get; }
        public double FadeIn { get; }
        public double FadeOut { get; }
        public double Duration => (LastFrame - FirstFrame + 1) / Fps;

        public VideoClip(string folder, int firstFrame, int lastFrame, double fps, double fadeIn = 0.3, double fadeOut = 0.5)
        {
            Folder = folder; FirstFrame = firstFrame; LastFrame = lastFrame; Fps = fps; FadeIn = fadeIn; FadeOut = fadeOut;
        }

        /// <summary>Bild zum Zeitpunkt t (Sekunden seit Start).</summary>
        public BitmapImage LoadFrame(double t)
        {
            int i = Math.Clamp(FirstFrame + (int)(t * Fps), FirstFrame, LastFrame);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri($"pack://application:,,,/Assets/video/{Folder}/f{i:000}.png");
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        /// <summary>Deckkraft des Video-Layers (0..1) mit Ein- und Ausblendung.</summary>
        public double Opacity(double t) => Math.Min(Math.Clamp(t / FadeIn, 0, 1), Math.Clamp((Duration - t) / FadeOut, 0, 1));
    }
}
