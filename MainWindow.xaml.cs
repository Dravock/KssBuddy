using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KssBuddy.Platform;
using KssBuddy.Rendering;
using KssBuddy.Services;

namespace KssBuddy
{
    /// <summary>
    /// Overlay-Fenster des Companions. Verantwortlich für:
    /// Dragging, Click-Through, Always-on-Top, Tray-Anbindung, Render-Schleife und Pausieren bei Vollbild.
    /// Die Verformung des Bildes steckt in Rendering/PuppetMesh, die Bewegungen in Rendering/AnimationController.
    /// </summary>
    public partial class MainWindow : Window
    {
        private const double BaseWidth = 420, BaseHeight = 552;   // Fenstergröße bei 100 %

        private readonly AppSettings _settings = SettingsService.Load();
        private SettingsWindow? _settingsWindow;
        private bool _autostartApplied;

        private readonly TrayService _tray = new();
        private readonly DispatcherTimer _fullscreenTimer;
        private readonly AnimationController _controller = new();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private PuppetMesh? _puppet;
        private CharacterProfile _profile = CharacterProfile.Slytherin;
        private double _lastFrame;
        private bool _rendering;

        // Draconia läuft komplett über Video-Clips (PNG-Sequenzen mit Alpha); andere Figuren nutzen das Rig
        private readonly VideoDirector _director = new();
        private double _nextVideoAuto = double.MaxValue;
        private static readonly Random Rng = new();
        private bool UsesVideo => _profile.Id == "slytherin" && _director.Loaded;

        private bool _userVisible = true;
        private bool _pausedByFullscreen;

        public MainWindow()
        {
            InitializeComponent();

            _tray.ToggleVisibilityRequested += () => Dispatcher.Invoke(ToggleVisibility);
            _tray.ToggleClickThroughRequested += () => Dispatcher.Invoke(() => { _settings.ClickThrough = !_settings.ClickThrough; Commit(); });
            _tray.ToggleTopmostRequested += () => Dispatcher.Invoke(() => { _settings.Topmost = !_settings.Topmost; Commit(); });
            _tray.CharacterRequested += id => Dispatcher.Invoke(() => { SwitchCharacter(CharacterProfile.ById(id)); _settingsWindow?.Reload(); });
            _tray.AnimationRequested += name => Dispatcher.Invoke(() => PlayAnimation(name));
            _tray.SettingsRequested += () => Dispatcher.Invoke(OpenSettings);
            _tray.ExitRequested += () => Dispatcher.Invoke(Close);

            // Vollbild-Prüfung: 1x pro Sekunde ist günstig und reicht völlig aus.
            _fullscreenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _fullscreenTimer.Tick += (_, _) => CheckFullscreen();

            Loaded += OnLoaded;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            Closed += (_, _) => { StopRendering(); _fullscreenTimer.Stop(); _tray.Dispose(); };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WindowStyleHelper.HideFromAltTab(this);

            RestorePosition();
            SwitchCharacter(CharacterProfile.ById(_settings.CharacterId));
            ApplySettings();

            StartRendering();
            _fullscreenTimer.Start();
        }

        // ---- Figur wechseln --------------------------------------------------------------------

        internal void SwitchCharacter(CharacterProfile profile)
        {
            _profile = profile;
            _settings.CharacterId = profile.Id;
            string root = $"pack://application:,,,/Assets/{profile.Folder}/";
            var body = new BitmapImage(new Uri(root + "body.png"));
            var upper = new BitmapImage(new Uri(root + "arms_upper.png"));
            var fore = new BitmapImage(new Uri(root + "arms_fore.png"));

            if (_puppet != null) Scene.Children.Remove(_puppet.Model);
            _puppet = new PuppetMesh(profile, body, upper, fore);
            Scene.Children.Add(_puppet.Model);

            // Kamera mittig auf die Figur; Szenenbreite lässt Platz für erhobene Arme
            Stage.Camera = new OrthographicCamera(new Point3D(profile.CX, profile.H * 0.6, 10),
                                                  new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 950);
            ShowVideoMode(UsesVideo);
            _controller.AutoActions = _settings.AutoActions && !UsesVideo;
            _tray.SetCharacterChecked(profile.Id);
            SettingsService.Save(_settings);
        }

        // ---- Einstellungen ---------------------------------------------------------------------

        /// <summary>Speichert und wendet die Einstellungen an (z. B. nach Änderung über das Tray-Menü) und aktualisiert das Einstellungsfenster.</summary>
        private void Commit()
        {
            SettingsService.Save(_settings);
            ApplySettings();
            _settingsWindow?.Reload();
        }

        /// <summary>Überträgt die Werte aus AppSettings auf Fenster, Animation und Tray-Menü.</summary>
        internal void ApplySettings()
        {
            Width = BaseWidth * _settings.Scale;
            Height = BaseHeight * _settings.Scale;
            Opacity = _settings.Opacity;
            Topmost = _settings.Topmost;
            // Draconia: Auto-Aktionen übernimmt der Video-Director (siehe UpdateVideo), sonst die Rig-Aktionen
            _controller.AutoActions = _settings.AutoActions && !UsesVideo;
            if (_nextVideoAuto == double.MaxValue) ScheduleVideoAuto();
            WindowStyleHelper.SetClickThrough(this, _settings.ClickThrough);

            _tray.SetTopmostChecked(_settings.Topmost);
            _tray.SetClickThroughChecked(_settings.ClickThrough);

            if (_settings.Autostart != _autostartApplied)
            {
                AutostartService.Set(_settings.Autostart);
                _autostartApplied = _settings.Autostart;
            }
        }

        private void OpenSettings()
        {
            if (_settingsWindow != null) { _settingsWindow.Activate(); return; }
            _settingsWindow = new SettingsWindow(this, _settings);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }

        private void RestorePosition()
        {
            if (_settings.Left is double l && _settings.Top is double t &&
                l > SystemParameters.VirtualScreenLeft - 100 && l < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 50 &&
                t > SystemParameters.VirtualScreenTop - 100 && t < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 50)
            {
                Left = l; Top = t;
            }
        }

        internal void ResetPosition()
        {
            Left = SystemParameters.WorkArea.Right - Width - 40;
            Top = SystemParameters.WorkArea.Bottom - Height - 20;
            _settings.Left = Left; _settings.Top = Top;
            SettingsService.Save(_settings);
        }

        // ---- Render-Schleife -------------------------------------------------------------------

        private void StartRendering()
        {
            if (_rendering) return;
            _rendering = true;
            _lastFrame = _clock.Elapsed.TotalSeconds;
            CompositionTarget.Rendering += OnRendering;
        }

        private void StopRendering()
        {
            if (!_rendering) return;
            _rendering = false;
            CompositionTarget.Rendering -= OnRendering;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            double now = _clock.Elapsed.TotalSeconds;
            if (now - _lastFrame < 1.0 / _settings.Fps) return;
            double dt = Math.Min(now - _lastFrame, 0.1);
            _lastFrame = now;

            Pose pose = _controller.Update(dt);
            if (!UsesVideo) _puppet?.Apply(pose, _controller.Blink);
            UpdateVideo(now, dt);
        }

        private void ShowVideoMode(bool video)
        {
            Stage.Visibility = video ? Visibility.Hidden : Visibility.Visible;
            VideoLayer.Visibility = VideoLayerNext.Visibility = video ? Visibility.Visible : Visibility.Hidden;
            if (!video) { VideoLayer.Source = null; VideoLayerNext.Source = null; }
        }

        private void RequestVideo(string clip) => _director.Request(clip);

        private void ScheduleVideoAuto() => _nextVideoAuto = _clock.Elapsed.TotalSeconds + 60 + Rng.NextDouble() * 120;

        private void UpdateVideo(double now, double dt)
        {
            if (!UsesVideo) return;

            // Zufällige Aktion alle 1-3 Minuten
            if (_settings.AutoActions && !_director.InAction && now >= _nextVideoAuto)
            {
                ScheduleVideoAuto();
                _director.Request(Rng.Next(5) switch { 0 or 1 => "wave", 2 or 3 => "dance", _ => "yawn" });
            }

            var (frame, next, blend) = _director.Update(dt);
            VideoLayer.Source = frame;
            VideoLayerNext.Source = next;
            VideoLayerNext.Opacity = blend;
        }

        // ---- Eingaben --------------------------------------------------------------------------

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            double l = Left, t = Top;
            try { DragMove(); } catch (InvalidOperationException) { /* Maus schon losgelassen */ }

            // Wurde das Fenster nicht bewegt, war es ein Klick -> Winken; sonst neue Position merken
            if (Math.Abs(Left - l) < 2 && Math.Abs(Top - t) < 2)
            {
                if (UsesVideo) RequestVideo("wave"); else _controller.Play(AnimationClip.Wave);
            }
            else
            {
                _settings.Left = Left; _settings.Top = Top;
                SettingsService.Save(_settings);
            }
        }

        internal void PlayAnimation(string name)
        {
            switch (name)
            {
                // Draconia nutzt für Idle/Winken/Tanzen die Video-Clips, andere Figuren das Rig
                case "yawn": if (UsesVideo) RequestVideo("yawn"); else _controller.Play(AnimationClip.Stretch); break;
                case "idle": if (!UsesVideo) _controller.Play(AnimationClip.Idle); break;
                case "wave": if (UsesVideo) RequestVideo("wave"); else _controller.Play(AnimationClip.Wave); break;
                case "dance": if (UsesVideo) RequestVideo("dance"); else _controller.Toggle(AnimationClip.Dance); break;
                case "uwu": _controller.Toggle(AnimationClip.UwU); break;
                case "cheer": _controller.Play(AnimationClip.Cheer); break;
                case "stretch": _controller.Play(AnimationClip.Stretch); break;
                case "scold": _controller.Play(AnimationClip.Scold); break;
                case "hips": _controller.Toggle(AnimationClip.HipShake); break;
                case "shy": _controller.Toggle(AnimationClip.Shy); break;
                case "sleepy": _controller.Toggle(AnimationClip.Sleepy); break;
            }
        }

        // ---- Fenster-Modi ----------------------------------------------------------------------

        private void ToggleVisibility()
        {
            _userVisible = !_userVisible;
            Visibility = _userVisible ? Visibility.Visible : Visibility.Hidden;
            _tray.SetVisibleChecked(_userVisible);
            if (_userVisible && !_pausedByFullscreen) StartRendering(); else StopRendering();
        }

        // ---- Ressourcenschonung ----------------------------------------------------------------

        private void CheckFullscreen()
        {
            bool fullscreen = _settings.PauseOnFullscreen && FullscreenDetector.IsForegroundFullscreen();
            if (fullscreen == _pausedByFullscreen) return;

            _pausedByFullscreen = fullscreen;
            if (fullscreen)
            {
                StopRendering();                  // keine Render-Schleife, keine CPU/GPU-Last
                Visibility = Visibility.Hidden;
            }
            else if (_userVisible)
            {
                Visibility = Visibility.Visible;
                StartRendering();
            }
        }
    }
}
