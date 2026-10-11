using System.Windows;
using System.Windows.Controls;
using KssBuddy.Rendering;
using KssBuddy.Services;

namespace KssBuddy
{
    /// <summary>
    /// Einstellungsfenster. Ändert die Werte direkt im AppSettings-Objekt und meldet jede Änderung
    /// an das Hauptfenster (Apply), damit sie sofort sichtbar wird. Kein "Übernehmen"-Knopf nötig.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly MainWindow _main;
        private readonly AppSettings _s;
        private bool _loading = true;

        internal SettingsWindow(MainWindow main, AppSettings settings)
        {
            InitializeComponent();
            _main = main;
            _s = settings;

            CharacterBox.ItemsSource = CharacterProfile.All;
            Reload();
        }

        /// <summary>Zeigt die aktuellen Werte an (auch aufrufbar, wenn sie sich außerhalb geändert haben, z. B. über das Tray-Menü).</summary>
        internal void Reload()
        {
            _loading = true;
            CharacterBox.SelectedItem = CharacterProfile.ById(_s.CharacterId);
            ScaleSlider.Value = _s.Scale * 100;
            OpacitySlider.Value = _s.Opacity * 100;
            ScaleText.Text = $"{(int)(_s.Scale * 100)} %";
            OpacityText.Text = $"{(int)(_s.Opacity * 100)} %";
            TopmostBox.IsChecked = _s.Topmost;
            ClickThroughBox.IsChecked = _s.ClickThrough;
            AutoBox.IsChecked = _s.AutoActions;
            FullscreenBox.IsChecked = _s.PauseOnFullscreen;
            AutostartBox.IsChecked = _s.Autostart;
            foreach (ComboBoxItem item in FpsBox.Items)
                if ((string)item.Tag == _s.Fps.ToString()) FpsBox.SelectedItem = item;
            _loading = false;
        }

        private void Changed()
        {
            SettingsService.Save(_s);
            _main.ApplySettings();
        }

        private void OnCharacterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || CharacterBox.SelectedItem is not CharacterProfile p) return;
            _s.CharacterId = p.Id;
            _main.SwitchCharacter(p);
            SettingsService.Save(_s);
        }

        private void OnScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            _s.Scale = e.NewValue / 100;
            ScaleText.Text = $"{(int)e.NewValue} %";
            Changed();
        }

        private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            _s.Opacity = e.NewValue / 100;
            OpacityText.Text = $"{(int)e.NewValue} %";
            Changed();
        }

        private void OnTopmostClicked(object sender, RoutedEventArgs e)
        {
            _s.Topmost = TopmostBox.IsChecked == true; Changed();
        }

        private void OnClickThroughClicked(object sender, RoutedEventArgs e)
        {
            _s.ClickThrough = ClickThroughBox.IsChecked == true; Changed();
        }

        private void OnAutoClicked(object sender, RoutedEventArgs e)
        {
            _s.AutoActions = AutoBox.IsChecked == true; Changed();
        }

        private void OnFullscreenClicked(object sender, RoutedEventArgs e)
        {
            _s.PauseOnFullscreen = FullscreenBox.IsChecked == true; Changed();
        }

        private void OnAutostartClicked(object sender, RoutedEventArgs e)
        {
            _s.Autostart = AutostartBox.IsChecked == true; Changed();
        }

        private void OnFpsChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || FpsBox.SelectedItem is not ComboBoxItem item) return;
            _s.Fps = int.Parse((string)item.Tag); Changed();
        }

        private void OnAnimClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string name) _main.PlayAnimation(name);
        }

        private void OnResetPosition(object sender, RoutedEventArgs e) => _main.ResetPosition();

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
