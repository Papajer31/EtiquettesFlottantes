using System;
using System.Windows;
using System.Linq;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Étiquettes_Flottantes
{
    public partial class MainWindow : Window
    {
        private FloatingLabelsWindow? _labelsWindow;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnGo_Click(object sender, RoutedEventArgs e)
        {
            var lines = (InputTextBox.Text ?? string.Empty)
                .Split(new[] { "", "" }, StringSplitOptions.None)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();

            if (_labelsWindow == null || !_labelsWindow.IsVisible)
            {
                _labelsWindow = new FloatingLabelsWindow();
                _labelsWindow.Closed += (_, __) => _labelsWindow = null;
                _labelsWindow.Show();
            }

            _labelsWindow.SetLabels(lines);
            _labelsWindow.Activate();
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
        private void BtnExemple_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Exemple");
        #endregion
        //===========================================//
    }
}