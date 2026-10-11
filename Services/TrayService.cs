using System;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
using KssBuddy.Rendering;

namespace KssBuddy.Services
{
    /// <summary>Tray-Icon mit Kontextmenü. Kennt die Fenster-Logik nicht, meldet nur Ereignisse.</summary>
    internal sealed class TrayService : IDisposable
    {
        private readonly NotifyIcon _icon;
        private readonly ToolStripMenuItem _visibleItem;
        private readonly ToolStripMenuItem _clickThroughItem;
        private readonly ToolStripMenuItem _topmostItem;

        public event Action? ToggleVisibilityRequested;
        public event Action? ToggleClickThroughRequested;
        public event Action? ToggleTopmostRequested;
        public event Action<string>? AnimationRequested;   // "idle" | "wave" | "dance" | "uwu" | "cheer" | "stretch" | "shy" | "sleepy" | "scold" | "hips"
        public event Action<string>? CharacterRequested;   // Id aus CharacterProfile
        public event Action? SettingsRequested;
        private readonly Dictionary<string, ToolStripMenuItem> _characterItems = new();
        public event Action? ExitRequested;

        public TrayService()
        {
            _visibleItem = new ToolStripMenuItem("Sichtbar") { Checked = true, CheckOnClick = false };
            _visibleItem.Click += (_, _) => ToggleVisibilityRequested?.Invoke();

            _clickThroughItem = new ToolStripMenuItem("Click-Through") { CheckOnClick = false };
            _clickThroughItem.Click += (_, _) => ToggleClickThroughRequested?.Invoke();

            _topmostItem = new ToolStripMenuItem("Immer im Vordergrund") { Checked = true, CheckOnClick = false };
            _topmostItem.Click += (_, _) => ToggleTopmostRequested?.Invoke();

            var anim = new ToolStripMenuItem("Animation");
            var idleItem = new ToolStripMenuItem("Idle");
            idleItem.Click += (_, _) => AnimationRequested?.Invoke("idle");
            var waveItem = new ToolStripMenuItem("Winken");
            waveItem.Click += (_, _) => AnimationRequested?.Invoke("wave");
            var danceItem = new ToolStripMenuItem("Tanzen (an/aus)");
            danceItem.Click += (_, _) => AnimationRequested?.Invoke("dance");
            var uwuItem = new ToolStripMenuItem("UwU-Tanz (an/aus)");
            uwuItem.Click += (_, _) => AnimationRequested?.Invoke("uwu");
            ToolStripMenuItem Item(string text, string key)
            {
                var it = new ToolStripMenuItem(text);
                it.Click += (_, _) => AnimationRequested?.Invoke(key);
                return it;
            }
            anim.DropDownItems.AddRange(new ToolStripItem[]
            {
                idleItem, waveItem, danceItem, uwuItem, new ToolStripSeparator(),
                Item("Gähnen", "yawn"), Item("Jubeln", "cheer"), Item("Strecken", "stretch"),
                Item("Ermahnen", "scold"), Item("Hüftschwung (an/aus)", "hips"), Item("Schüchtern (an/aus)", "shy"), Item("Einnicken (an/aus)", "sleepy")
            });

            var characters = new ToolStripMenuItem("Charakter");
            foreach (var profile in CharacterProfile.All)
            {
                string id = profile.Id;
                var ci = new ToolStripMenuItem(profile.DisplayName);
                ci.Click += (_, _) => CharacterRequested?.Invoke(id);
                _characterItems[id] = ci;
                characters.DropDownItems.Add(ci);
            }

            var settings = new ToolStripMenuItem("Einstellungen…");
            settings.Click += (_, _) => SettingsRequested?.Invoke();

            var exit = new ToolStripMenuItem("Beenden");
            exit.Click += (_, _) => ExitRequested?.Invoke();

            var menu = new ContextMenuStrip();
            menu.Items.AddRange(new ToolStripItem[]
            {
                _visibleItem, _clickThroughItem, _topmostItem,
                new ToolStripSeparator(), characters, anim, settings, new ToolStripSeparator(), exit
            });

            _icon = new NotifyIcon
            {
                Icon = CreateIcon(),
                Text = "KSS Buddy",
                Visible = true,
                ContextMenuStrip = menu
            };
            _icon.DoubleClick += (_, _) => ToggleVisibilityRequested?.Invoke();
        }

        public void SetCharacterChecked(string id)
        {
            foreach (var kv in _characterItems) kv.Value.Checked = kv.Key == id;
        }

        public void SetVisibleChecked(bool value) => _visibleItem.Checked = value;
        public void SetClickThroughChecked(bool value) => _clickThroughItem.Checked = value;
        public void SetTopmostChecked(bool value) => _topmostItem.Checked = value;

        /// <summary>Zeichnet ein kleines Platzhalter-Icon, damit keine .ico-Datei nötig ist.</summary>
        private static Icon CreateIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using var body = new SolidBrush(Color.FromArgb(47, 111, 222));
                g.FillEllipse(body, 2, 2, 28, 28);
                g.FillEllipse(Brushes.White, 8, 9, 6, 9);
                g.FillEllipse(Brushes.White, 18, 9, 6, 9);
            }
            IntPtr h = bmp.GetHicon();
            return Icon.FromHandle(h);
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.Dispose();
        }
    }
}
