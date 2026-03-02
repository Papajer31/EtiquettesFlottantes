using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Étiquettes_Flottantes
{
    public partial class FloatingLabelsWindow : Window
    {
        private bool _isMaximizedCustom;
        private Rect _restoreBounds;

        private Border? _draggingLabel;
        private Point _labelDragOffset;

        private int _zCounter = 1;
        private double _labelFontSize = 30;

        // Synchronisation texte <-> étiquette (clé normalisée)
        private readonly Dictionary<string, Border> _labelsByKey = new(StringComparer.CurrentCulture);

        // Placement des nouveaux labels : on empile en bas, sans bouger les existants
        private const double StartX = 20;
        private const double StartY = 20;
        private const double GapY = 12;

        public FloatingLabelsWindow()
        {
            InitializeComponent();
            Loaded += (_, __) => _restoreBounds = new Rect(Left, Top, ActualWidth, ActualHeight);
        }

        /// <summary>
        /// Synchronise F2 avec les lignes de F1 :
        /// - supprime les étiquettes dont le texte n'existe plus
        /// - ajoute uniquement les nouvelles
        /// - ne déplace jamais les étiquettes existantes (priorité au placement)
        /// </summary>
        public void SetLabels(string[] lines)
        {
            var desiredKeys = lines
                .Select(NormalizeKey)
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct()
                .ToList();

            var desiredSet = new HashSet<string>(desiredKeys, StringComparer.CurrentCulture);

            // 1) Suppressions
            var toRemove = _labelsByKey.Keys.Where(k => !desiredSet.Contains(k)).ToList();
            foreach (var key in toRemove)
            {
                if (_labelsByKey.TryGetValue(key, out var b))
                {
                    CanvasArea.Children.Remove(b);
                }
                _labelsByKey.Remove(key);
            }

            // 2) Ajouts (sans bouger l'existant)
            var added = new List<Border>();
            foreach (var key in desiredKeys)
            {
                if (_labelsByKey.ContainsKey(key)) continue;

                var label = CreateLabel(key);
                CanvasArea.Children.Add(label);

                _labelsByKey[key] = label;
                added.Add(label);
            }

            if (added.Count > 0)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    CanvasArea.UpdateLayout();

                    // Mesurer les nouvelles (important avant de calculer le "shift")
                    foreach (var b in added) b.UpdateLayout();

                    double shift = added.Sum(b => b.ActualHeight + GapY);

                    // 1) Décaler vers le bas uniquement les étiquettes "auto" non déplacées
                    const double eps = 0.5;
                    var autoExisting = CanvasArea.Children
                        .OfType<Border>()
                        .Where(b => !added.Contains(b))
                        .Where(b => (b.Tag as string) != "pinned")
                        .Where(b =>
                        {
                            var left = Canvas.GetLeft(b);
                            if (double.IsNaN(left)) left = StartX;
                            return Math.Abs(left - StartX) < eps;
                        })
                        .OrderByDescending(b =>
                        {
                            var top = Canvas.GetTop(b);
                            if (double.IsNaN(top)) top = StartY;
                            return top;
                        })
                        .ToList();

                    foreach (var b in autoExisting)
                    {
                        var top = Canvas.GetTop(b);
                        if (double.IsNaN(top)) top = StartY;
                        Canvas.SetTop(b, top + shift);
                    }

                    // 2) Placer les nouvelles en haut
                    double y = StartY;
                    foreach (var b in added)
                    {
                        Canvas.SetLeft(b, StartX);
                        Canvas.SetTop(b, y);
                        y += b.ActualHeight + GapY;
                    }

                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private static string NormalizeKey(string s) => (s ?? string.Empty).Trim();

        private Border CreateLabel(string text)
        {
            var border = new Border
            {
                Background = Brushes.LightYellow,
                BorderBrush = Brushes.DarkGoldenrod,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    ShadowDepth = 2,
                    BlurRadius = 4,
                    Opacity = 0.5
                },
                Padding = new Thickness(10, 6, 10, 6),
                SnapsToDevicePixels = true,
                MinWidth = 100
            };

            var tb = new TextBlock
            {
                Text = text,
                FontSize = _labelFontSize,
                Foreground = Brushes.Black,
                Margin = new Thickness(6),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            if (Application.Current.TryFindResource("AndikaFont") is FontFamily ff)
                tb.FontFamily = ff;

            border.Child = tb;

            border.MouseLeftButtonDown += Label_MouseLeftButtonDown;
            border.MouseMove += Label_MouseMove;
            border.MouseLeftButtonUp += Label_MouseLeftButtonUp;

            return border;
        }

        public void SetLabelFontSize(double fontSize)
        {
            _labelFontSize = Math.Clamp(fontSize, 10, 80);

            foreach (var child in CanvasArea.Children)
            {
                if (child is Border b && b.Child is TextBlock tb)
                    tb.FontSize = _labelFontSize;
            }
        }

        // Définit une image de fond en mode Stretch = Fill derrière le Canvas.
        public void SetBackgroundImage(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) { BackgroundImage.Source = null; return; }

            try
            {
                // CacheOption.OnLoad évite de locker le fichier sur disque.
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(imagePath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();

                BackgroundImage.Source = bmp;
            }
            catch { BackgroundImage.Source = null; }
        }

        public void ShuffleAndWrapAllLabels()
        {
            const double startX = StartX;
            const double startY = StartY;
            const double gapX = 12;
            const double gapY = GapY;

            // 1) Récupérer toutes les étiquettes (même déplacées / pinned)
            var labels = CanvasArea.Children
                .OfType<Border>()
                .Where(b => b.Visibility == Visibility.Visible)
                .ToList();

            if (labels.Count == 0)
                return;

            // 2) Mélange complet (Fisher–Yates)
            var rng = new Random();
            for (int i = labels.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (labels[i], labels[j]) = (labels[j], labels[i]);
            }

            // 3) Layout à jour pour mesurer correctement
            CanvasArea.UpdateLayout();
            foreach (var b in labels) b.UpdateLayout();

            // 4) Largeur dispo
            double availableWidth = CanvasArea.ActualWidth;
            if (availableWidth <= 0) availableWidth = ActualWidth;

            // Limite droite avec une petite marge
            double rightLimit = Math.Max(startX + 50, availableWidth - startX);

            // 5) Placement "wrap" en haut à gauche
            double x = startX;
            double y = startY;
            double rowMaxHeight = 0;

            foreach (var b in labels)
            {
                double w = b.ActualWidth;
                double h = b.ActualHeight;

                if (w <= 0) w = 100;
                if (h <= 0) h = 30;

                if (x != startX && (x + w) > rightLimit)
                {
                    x = startX;
                    y += rowMaxHeight + gapY;
                    rowMaxHeight = 0;
                }

                Canvas.SetLeft(b, x);
                Canvas.SetTop(b, y);

                // Puisqu'on “aligne avant tri”, on considère que tout redevient "auto"
                b.Tag = null;

                x += w + gapX;
                rowMaxHeight = Math.Max(rowMaxHeight, h);
            }
        }

        //===========================================//
        #region GESTION DES CLICS DE SOURIS

        private void Root_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Clic milieu = fermer la fenêtre (même si on clique sur une étiquette)
            if (e.ChangedButton == MouseButton.Middle)
            {
                Close();
                e.Handled = true;
            }
        }

        private void Label_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border b) return;

            _draggingLabel = b;
            _labelDragOffset = e.GetPosition(b);
            b.CaptureMouse();

            Panel.SetZIndex(b, _zCounter++);
            e.Handled = true;
        }

        private void Label_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingLabel == null) return;
            if (!_draggingLabel.IsMouseCaptured) return;

            var pos = e.GetPosition(CanvasArea);
            var x = pos.X - _labelDragOffset.X;
            var y = pos.Y - _labelDragOffset.Y;

            Canvas.SetLeft(_draggingLabel, x);
            Canvas.SetTop(_draggingLabel, y);

            e.Handled = true;
        }

        private void Label_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggingLabel == null) return;

            _draggingLabel.ReleaseMouseCapture();
            _draggingLabel = null;
            e.Handled = true;
        }

        private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_draggingLabel != null) return;
            if (_isMaximizedCustom) return;
            try { DragMove(); } catch { }
        }

        private void Root_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            ToggleMaximizeOnCurrentScreen();
            e.Handled = true;
        }

        private void ToggleMaximizeOnCurrentScreen()
        {
            // Sécurité : si une étiquette était en cours de drag, on stoppe.
            if (_draggingLabel != null)
            {
                try { _draggingLabel.ReleaseMouseCapture(); } catch { }
                _draggingLabel = null;
            }

            if (!_isMaximizedCustom)
            {
                _restoreBounds = new Rect(Left, Top, ActualWidth, ActualHeight);
                var wa = GetCurrentMonitorWorkingArea();

                Left = wa.Left;
                Top = wa.Top;
                Width = wa.Width;
                Height = wa.Height;

                _isMaximizedCustom = true;
            }
            else
            {
                Left = _restoreBounds.Left;
                Top = _restoreBounds.Top;
                Width = _restoreBounds.Width;
                Height = _restoreBounds.Height;

                _isMaximizedCustom = false;
            }
        }

        #endregion
        //===========================================//

        //===========================================//
        #region GESTION MULTIÉCRANS

        private Rect GetCurrentMonitorWorkingArea()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var hmon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            var mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

            if (!GetMonitorInfo(hmon, ref mi))
            {
                return new Rect(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);
            }

            var w = mi.rcWork.Right - mi.rcWork.Left;
            var h = mi.rcWork.Bottom - mi.rcWork.Top;
            return new Rect(mi.rcWork.Left, mi.rcWork.Top, w, h);
        }

        private const int MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public int dwFlags; }

        #endregion
        //===========================================//
    }
}
