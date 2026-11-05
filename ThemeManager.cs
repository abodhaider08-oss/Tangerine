using System.Windows;
using System.Windows.Media;

namespace TangerineWin
{
    public class ThemePalette
    {
        public string Name { get; init; } = "";
        public Brush DonutRingFill { get; init; } = Brushes.Transparent;
        public Brush DonutRingStroke { get; init; } = Brushes.White;
        public Brush PetalDefaultFill { get; init; } = Brushes.White;
        public Brush PetalHoverFill { get; init; } = Brushes.Orange;
        public Brush PetalStroke { get; init; } = Brushes.White;
        public Brush TextDefaultBrush { get; init; } = Brushes.Black;
        public Brush TextHoverBrush { get; init; } = Brushes.White;
    }

    public static class ThemeManager
    {
        public static ThemePalette GetPalette(string themeName)
        {
            return themeName switch
            {
                "DarkObsidian" => CreateDarkObsidian(),
                "CyberpunkOrange" => CreateCyberpunkOrange(),
                _ => CreateAppleWhite()
            };
        }

        private static ThemePalette CreateAppleWhite()
        {
            var ringFill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            ringFill.GradientStops.Add(new GradientStop(Color.FromArgb(72, 255, 255, 255), 0.0));
            ringFill.GradientStops.Add(new GradientStop(Color.FromArgb(36, 216, 230, 242), 0.5));
            ringFill.GradientStops.Add(new GradientStop(Color.FromArgb(60, 255, 255, 255), 1.0));
            ringFill.Freeze();

            var ringStroke = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            ringStroke.GradientStops.Add(new GradientStop(Color.FromArgb(220, 255, 255, 255), 0.0));
            ringStroke.GradientStops.Add(new GradientStop(Color.FromArgb(100, 255, 255, 255), 0.5));
            ringStroke.GradientStops.Add(new GradientStop(Color.FromArgb(170, 255, 255, 255), 1.0));
            ringStroke.Freeze();

            var petalDef = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            petalDef.GradientStops.Add(new GradientStop(Color.FromArgb(238, 248, 246, 240), 0.0));
            petalDef.GradientStops.Add(new GradientStop(Color.FromArgb(225, 236, 233, 225), 1.0));
            petalDef.Freeze();

            var petalHov = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            petalHov.GradientStops.Add(new GradientStop(Color.FromArgb(250, 255, 94, 20), 0.0));
            petalHov.GradientStops.Add(new GradientStop(Color.FromArgb(250, 242, 68, 8), 1.0));
            petalHov.Freeze();

            var stroke = new SolidColorBrush(Color.FromArgb(165, 255, 255, 255));
            stroke.Freeze();
            var txtDef = new SolidColorBrush(Color.FromRgb(28, 28, 32));
            txtDef.Freeze();

            return new ThemePalette
            {
                Name = "AppleWhite",
                DonutRingFill = ringFill,
                DonutRingStroke = ringStroke,
                PetalDefaultFill = petalDef,
                PetalHoverFill = petalHov,
                PetalStroke = stroke,
                TextDefaultBrush = txtDef,
                TextHoverBrush = Brushes.White
            };
        }

        private static ThemePalette CreateDarkObsidian()
        {
            var ringFill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            ringFill.GradientStops.Add(new GradientStop(Color.FromArgb(110, 20, 20, 26), 0.0));
            ringFill.GradientStops.Add(new GradientStop(Color.FromArgb(90, 10, 10, 14), 1.0));
            ringFill.Freeze();

            var ringStroke = new SolidColorBrush(Color.FromArgb(110, 255, 255, 255));
            ringStroke.Freeze();

            var petalDef = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            petalDef.GradientStops.Add(new GradientStop(Color.FromArgb(230, 36, 36, 44), 0.0));
            petalDef.GradientStops.Add(new GradientStop(Color.FromArgb(235, 22, 22, 28), 1.0));
            petalDef.Freeze();

            var petalHov = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            petalHov.GradientStops.Add(new GradientStop(Color.FromArgb(250, 99, 102, 241), 0.0));
            petalHov.GradientStops.Add(new GradientStop(Color.FromArgb(250, 79, 70, 229), 1.0));
            petalHov.Freeze();

            var stroke = new SolidColorBrush(Color.FromArgb(75, 255, 255, 255));
            stroke.Freeze();
            var txtDef = new SolidColorBrush(Color.FromRgb(238, 238, 244));
            txtDef.Freeze();

            return new ThemePalette
            {
                Name = "DarkObsidian",
                DonutRingFill = ringFill,
                DonutRingStroke = ringStroke,
                PetalDefaultFill = petalDef,
                PetalHoverFill = petalHov,
                PetalStroke = stroke,
                TextDefaultBrush = txtDef,
                TextHoverBrush = Brushes.White
            };
        }

        private static ThemePalette CreateCyberpunkOrange()
        {
            var ringFill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            ringFill.GradientStops.Add(new GradientStop(Color.FromArgb(100, 35, 18, 8), 0.0));
            ringFill.GradientStops.Add(new GradientStop(Color.FromArgb(90, 18, 12, 10), 1.0));
            ringFill.Freeze();

            var ringStroke = new SolidColorBrush(Color.FromArgb(180, 255, 138, 0));
            ringStroke.Freeze();

            var petalDef = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            petalDef.GradientStops.Add(new GradientStop(Color.FromArgb(235, 32, 26, 24), 0.0));
            petalDef.GradientStops.Add(new GradientStop(Color.FromArgb(240, 20, 16, 15), 1.0));
            petalDef.Freeze();

            var petalHov = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            petalHov.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 150, 0), 0.0));
            petalHov.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 75, 0), 1.0));
            petalHov.Freeze();

            var stroke = new SolidColorBrush(Color.FromArgb(130, 255, 138, 0));
            stroke.Freeze();
            var txtDef = new SolidColorBrush(Color.FromRgb(255, 195, 120));
            txtDef.Freeze();
            var txtHov = new SolidColorBrush(Color.FromRgb(18, 14, 12));
            txtHov.Freeze();

            return new ThemePalette
            {
                Name = "CyberpunkOrange",
                DonutRingFill = ringFill,
                DonutRingStroke = ringStroke,
                PetalDefaultFill = petalDef,
                PetalHoverFill = petalHov,
                PetalStroke = stroke,
                TextDefaultBrush = txtDef,
                TextHoverBrush = txtHov
            };
        }
    }
}
