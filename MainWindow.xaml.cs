using Microsoft.Win32;
using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;

namespace Étiquettes_Flottantes
{
    public partial class MainWindow : Window
    {
        private FloatingLabelsWindow? _labelsWindow;
        private double _labelsFontSize = 30;
        private string? _backgroundImagePath;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, __) => { InputTextBox.Focus(); InputTextBox.CaretIndex = InputTextBox.Text?.Length ?? 0; };
        }

        private void BtnGo_Click(object sender, RoutedEventArgs e)
        {
            var lines = (InputTextBox.Text ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();

            if (_labelsWindow == null || !_labelsWindow.IsVisible)
            {
                _labelsWindow = new FloatingLabelsWindow { Owner = this };
                _labelsWindow.Closed += (_, __) => _labelsWindow = null;
                _labelsWindow.Show();
            }

            if (!string.IsNullOrEmpty(_backgroundImagePath)) _labelsWindow.SetBackgroundImage(_backgroundImagePath);

            _labelsWindow.SetLabelFontSize(_labelsFontSize);
            _labelsWindow.SetLabels(lines);
            _labelsWindow.Activate();

        }

        private void BtnFontMinus_Click(object sender, RoutedEventArgs e) { ChangeLabelsFontSize(-2); }
        private void BtnFontPlus_Click(object sender, RoutedEventArgs e) { ChangeLabelsFontSize(+2); }
        private void ChangeLabelsFontSize(double delta)
        {
            _labelsFontSize = Math.Clamp(_labelsFontSize + delta, 10, 80);
            if (_labelsWindow != null && _labelsWindow.IsVisible) _labelsWindow.SetLabelFontSize(_labelsFontSize);
        }

        private void BtnLoadImage_Click(object sender, RoutedEventArgs e)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var fondsDir = Path.Combine(baseDir, "Fonds");
            if (!Directory.Exists(fondsDir)) Directory.CreateDirectory(fondsDir);
            var dlg = new OpenFileDialog
            {
                Title = "Choisir une image de fond",
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|Tous les fichiers|*.*",
                InitialDirectory = fondsDir,
                CheckFileExists = true,
                Multiselect = false
            };

            if (dlg.ShowDialog(this) != true) return;

            _backgroundImagePath = dlg.FileName;
            TxtImageName.Text = $"Image chargée : {System.IO.Path.GetFileName(_backgroundImagePath)}";
        }

        //===========================================//
        #region BARRE DE TITRE CUSTOM (WindowChrome)

        // Gestion du clic sur la TopBar :
        // - Double-clic : maximise/restaure (comportement Windows standard)
        // - Clic simple : permet de déplacer la fenêtre (DragMove)
        // Note : on évite de lancer un DragMove si l'utilisateur clique sur un Button.
        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ClickedInsideButton(e.OriginalSource as DependencyObject)) return;
            if (e.ClickCount == 2) { ToggleMaxRestore(); return; }
            try { DragMove(); } catch { }
        }
        private static bool ClickedInsideButton(DependencyObject? origin)
        {
            var d = origin;
            while (d != null)
            {
                if (d is ButtonBase) return true;
                d = VisualTreeHelper.GetParent(d);
            }
            return false;
        }
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnMaxRestore_Click(object sender, RoutedEventArgs e) => ToggleMaxRestore();
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
        private void ToggleMaxRestore() { WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized; }
        private void BtnExemple_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Charger un texte",
                Filter = "Fichiers texte (*.txt)|*.txt|Tous les fichiers (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() != true)
                return;

            // Lecture robuste : essaye UTF-8, sinon ANSI (Windows-1252)
            string content;
            try
            {
                content = File.ReadAllText(dlg.FileName, Encoding.UTF8);
            }
            catch
            {
                content = File.ReadAllText(dlg.FileName, Encoding.Default);
            }

            InputTextBox.Text = content;
            InputTextBox.Focus();
            InputTextBox.CaretIndex = InputTextBox.Text.Length;
        }
        private void BtnListeH_Click(object sender, RoutedEventArgs e)
        {
            // On ne crée pas F2 juste pour ça : on aligne seulement si F2 est ouverte.
            if (_labelsWindow == null || !_labelsWindow.IsVisible)
                return;

            _labelsWindow.ShuffleAndWrapAllLabels();
            _labelsWindow.Activate();
        }
        #endregion
        //===========================================//
    }
}