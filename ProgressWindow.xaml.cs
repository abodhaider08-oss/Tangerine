using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace TangerineWin
{
    public partial class ProgressWindow : Window
    {
        private const double MaxBarWidth = 260.0;

        public ProgressWindow()
        {
            InitializeComponent();
        }

        public void ShowNear(double left, double top)
        {
            Left = left + 25;
            Top = top + 140;
            Opacity = 1;
            Show();
        }

        public void UpdateProgress(string statusText, double fraction)
        {
            Dispatcher.Invoke(() =>
            {
                FileNameText.Text = statusText;
                int pct = (int)Math.Round(Math.Clamp(fraction, 0.0, 1.0) * 100);
                PercentText.Text = $"{pct}%";

                double targetWidth = Math.Clamp(fraction, 0.0, 1.0) * MaxBarWidth;
                var anim = new DoubleAnimation(targetWidth, TimeSpan.FromMilliseconds(110))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                ProgressFill.BeginAnimation(FrameworkElement.WidthProperty, anim);
            });
        }
    }
}
