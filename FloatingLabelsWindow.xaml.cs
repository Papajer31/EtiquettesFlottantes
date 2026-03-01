using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Étiquettes_Flottantes
{
    public partial class FloatingLabelsWindow : Window
    {
        private bool _isMaximizedCustom;
        private Rect _restoreBounds;

        private Border? _draggingLabel;
        private Point _labelDragOffset;
        private int _zCounter = 1;

        public FloatingLabelsWindow()
        {
            InitializeComponent();
            Loaded += (_, __) => _restoreBounds = new Rect(Left, Top, ActualWidth, ActualHeight);
        }

        public void SetLabels(string[] lines)
        {
            CanvasArea.Children.Clear();

            const double startX = 20;
            const double startY = 20;
            const double gapY = 12;

            double y = startY;

            foreach (var text in lines)
            {
                var label = CreateLabel(text);

                Canvas.SetLeft(label, startX);
                Canvas.SetTop(label, y);
                CanvasArea.Children.Add(label);

                y += label.Height + gapY;
            }
        }

        private Border CreateLabel(string text)
        {
            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6),
                SnapsToDevicePixels = true,
                Height = 44,
                MinWidth = 120
            };

            var tb = new TextBlock
            {
                Text = text,
                FontSize = 22,
                Foreground = Brushes.Black,
                VerticalAlignment = VerticalAlignment.Center
            };

            border.Child = tb;

            border.MouseLeftButtonDown += Label_MouseLeftButtonDown;
            border.MouseMove += Label_MouseMove;
            border.MouseLeftButtonUp += Label_MouseLeftButtonUp;

            // Empêche le drag de la fenêtre quand on clique sur une étiquette
            border.PreviewMouseLeftButtonDown += (_, e) => e.Handled = true;

            return border;
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
            // Drag fenêtre (clic gauche) – seulement si on ne drague pas déjà une étiquette
            if (_draggingLabel != null) return;

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

        private Rect GetCurrentMonitorWorkingArea()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var hmon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            var mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

            if (!GetMonitorInfo(hmon, ref mi))
            {
                // Fallback : écran principal WPF
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
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }
    }
}
