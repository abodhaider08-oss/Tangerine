using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ImageMagick;
using FFMpegCore;
using Path = System.IO.Path;

namespace TangerineWin
{
    public partial class MainWindow : Window
    {
        private static readonly string[] ImageExts = { ".jpg", ".jpeg", ".png", ".webp", ".heic", ".bmp", ".tiff", ".gif", ".avif", ".ico" };
        private static readonly string[] WordExts  = { ".docx", ".doc", ".rtf", ".odt" };
        private static readonly string[] ExcelExts = { ".xlsx", ".xls", ".csv" };
        private static readonly string[] PptExts   = { ".pptx", ".ppt" };
        private static readonly string[] VideoExts = { ".mp4", ".mov", ".mkv", ".avi", ".webm" };
        private static readonly string[] AudioExts = { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a" };

        private string[] _activeFormats = { "JPG", "WEBP", "PNG", "PDF", "DOCX", "PPTX", "SHELF" };
        private string? _selectedFormat;
        private string _currentContextKey = "";

        private ThemePalette _palette;

        private readonly List<System.Windows.Shapes.Path> _petalPaths = new();
        private readonly Dictionary<System.Windows.Shapes.Path, TextBlock> _sliceLabels = new();
        private System.Windows.Shapes.Path? _currentHoveredSlice;
        private ProgressWindow? _progressWin;

        private int _spawnScreenX;
        private int _spawnScreenY;
        private long _openedTickMs = 0;
        private volatile bool _isWheelOpen = false;
        private volatile bool _isHiding = false;
        private volatile bool _isConverting = false;

        public MainWindow()
        {
            InitializeComponent();
            _palette = ThemeManager.GetPalette("AppleWhite");
            ApplyTheme("AppleWhite");

            KeyDown += (_, e) => { if (e.Key == Key.Escape) FastDismissWheel(); };
            MouseMove += Window_MouseMove;
            MouseLeftButtonUp += Window_MouseLeftButtonUp;
        }

        public void ApplyTheme(string themeName)
        {
            void DoApply()
            {
                _palette = ThemeManager.GetPalette(themeName);
                if (GlassDonutRing != null)
                {
                    GlassDonutRing.Fill = _palette.DonutRingFill;
                    GlassDonutRing.Stroke = _palette.DonutRingStroke;
                }
                if (InnerRimEllipse != null)
                {
                    InnerRimEllipse.Stroke = _palette.DonutRingStroke;
                }
                BuildRadialPetals(_activeFormats);
            }

            if (Dispatcher.CheckAccess()) DoApply();
            else Dispatcher.Invoke(DoApply);
        }

        public void ShowAtCursor(int rawScreenX, int rawScreenY)
        {
            _spawnScreenX = rawScreenX;
            _spawnScreenY = rawScreenY;
            _openedTickMs = Environment.TickCount64;

            Dispatcher.Invoke(async () =>
            {
                _isHiding = false;
                _isWheelOpen = true;

                BeginAnimation(OpacityProperty, null);
                Opacity = 1.0;

                var source = PresentationSource.FromVisual(this);
                double dpiX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
                double dpiY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

                Left = (rawScreenX * dpiX) - (Width / 2);
                Top = (rawScreenY * dpiY) - (Height / 2);

                Show();
                PlayFastSpringPopup();

                await Task.Delay(20);
                NativeHardwareMonitor.ForceOleDragEnterNudge();
            });
        }

        private void PlayFastSpringPopup()
        {
            var scaleAnim = new DoubleAnimation(0.45, 1.0, TimeSpan.FromMilliseconds(185))
            {
                EasingFunction = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut }
            };
            WheelScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            WheelScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        public void FastDismissWheel()
        {
            if (!_isWheelOpen || _isHiding) return;
            _isHiding = true;
            _isWheelOpen = false;

            Dispatcher.BeginInvoke(() =>
            {
                ResetHighlight();
                var scaleOut = new DoubleAnimation(0.35, TimeSpan.FromMilliseconds(95))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(95));
                fadeOut.Completed += (_, _) =>
                {
                    if (_isHiding)
                    {
                        Hide();
                        _isHiding = false;
                    }
                };

                WheelScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
                WheelScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOut);
                BeginAnimation(OpacityProperty, fadeOut);
            });
        }

        public void OnGlobalCursorMoved(int rawX, int rawY)
        {
            if (!_isWheelOpen || _isHiding) return;

            if (Environment.TickCount64 - _openedTickMs < 900) return;

            double dx = rawX - _spawnScreenX;
            double dy = rawY - _spawnScreenY;
            double distSq = (dx * dx) + (dy * dy);

            if (distSq > (235 * 235))
            {
                FastDismissWheel();
            }
        }

        public void OnGlobalLeftMouseUp()
        {
            if (!_isWheelOpen || _isConverting) return;

            Task.Delay(140).ContinueWith(_ =>
            {
                if (_isWheelOpen && !_isConverting)
                {
                    FastDismissWheel();
                }
            });
        }

        private void UpdateWheelForFiles(string[] files)
        {
            if (files.Length == 0) return;
            string firstPath = files[0];

            if (Directory.Exists(firstPath))
            {
                ApplySliceTargets(new[] { "ZIP", "SHELF" });
                return;
            }

            string ext = Path.GetExtension(firstPath).ToLowerInvariant();
            string extNoDot = ext.TrimStart('.').ToUpperInvariant();
            string[] targetList;

            bool allPdfs = files.All(f => Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase));

            if (files.Length >= 3 && !allPdfs)
            {
                targetList = new[] { "AUTO SORT", "ZIP", "PDF", "PNG", "JPG", "SHELF" };
            }
            else if (allPdfs && files.Length > 1)
            {
                targetList = new[] { "MERGE PDF", "COMPRESS", "DOCX", "PPTX", "XLSX", "PNG", "JPG", "SHELF" };
            }
            else if (ext == ".pdf")
            {
                targetList = new[] { "DOCX", "PPTX", "XLSX", "COMPRESS", "COPY TEXT", "AI SUMMARY", "PNG", "JPG", "SHELF" };
            }
            else if (WordExts.Contains(ext))
            {
                var allWord = new[] { "PDF", "PPTX", "XLSX", "TXT", "HTML", "AI SUMMARY", "PNG", "SHELF" };
                targetList = allWord.Where(f => !f.Equals(extNoDot, StringComparison.OrdinalIgnoreCase)).ToArray();
            }
            else if (ExcelExts.Contains(ext))
            {
                var allExcel = new[] { "PDF", "CSV", "XLSX", "DOCX", "HTML", "TXT", "SHELF" };
                targetList = allExcel.Where(f => !f.Equals(extNoDot, StringComparison.OrdinalIgnoreCase)).ToArray();
            }
            else if (PptExts.Contains(ext))
            {
                targetList = new[] { "PDF", "DOCX", "PNG", "JPG", "ZIP", "SHELF" };
            }
            else if (ImageExts.Contains(ext))
            {
                var baseImg = new[] { "JPG", "PNG", "WEBP", "PDF", "HEIC", "AVIF", "ICO", "BMP", "TIFF" }
                    .Where(f => !f.Equals(extNoDot, StringComparison.OrdinalIgnoreCase) && !(extNoDot == "JPEG" && f == "JPG"))
                    .Take(5)
                    .ToList();
                baseImg.AddRange(new[] { "NO BG", "COPY TEXT", "SHELF" });
                targetList = baseImg.ToArray();
            }
            else if (VideoExts.Contains(ext))
            {
                var vids = new[] { "MP4", "MP3", "GIF", "WEBM", "WAV", "MUTE VIDEO", "TRIM 10S", "SHELF" }
                    .Where(f => !f.Equals(extNoDot, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                targetList = vids;
            }
            else if (AudioExts.Contains(ext))
            {
                var auds = new[] { "MP3", "WAV", "FLAC", "AAC", "OGG", "SHELF" }
                    .Where(f => !f.Equals(extNoDot, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                targetList = auds;
            }
            else
            {
                targetList = new[] { "PDF", "DOCX", "PPTX", "XLSX", "PNG", "JPG", "ZIP", "SHELF" };
            }

            ApplySliceTargets(targetList);
        }

        private void ApplySliceTargets(string[] targetList)
        {
            string newKey = string.Join(",", targetList) + "_" + _palette.Name;
            if (newKey != _currentContextKey)
            {
                _currentContextKey = newKey;
                _activeFormats = targetList;
                BuildRadialPetals(_activeFormats);
            }
        }

        private void BuildRadialPetals(string[] formats)
        {
            WheelCanvas.Children.Clear();
            _petalPaths.Clear();
            _sliceLabels.Clear();
            _currentHoveredSlice = null;

            double cx = 180, cy = 180;
            double outerR = 150;
            double innerR = 58;
            double gapPx = formats.Length > 8 ? 4.8 : 6.2;
            double cornerR = formats.Length > 8 ? 10.0 : 13.5;

            double stepDeg = 360.0 / formats.Length;

            for (int i = 0; i < formats.Length; i++)
            {
                double centerDeg = (i * stepDeg) - 90.0;
                double startDeg = centerDeg - (stepDeg / 2.0);
                double endDeg = centerDeg + (stepDeg / 2.0);
                double midRad = centerDeg * Math.PI / 180.0;

                double midR = (innerR + outerR) / 2.0;
                double px = cx + midR * Math.Cos(midRad);
                double py = cy + midR * Math.Sin(midRad);

                var geom = CreateRoundedPetalGeometry(cx, cy, innerR, outerR, startDeg, endDeg, gapPx, cornerR);
                geom.Freeze();

                var petalPath = new System.Windows.Shapes.Path
                {
                    Data = geom,
                    Fill = _palette.PetalDefaultFill,
                    Stroke = _palette.PetalStroke,
                    StrokeThickness = 1.0,
                    Tag = formats[i],
                    RenderTransform = new ScaleTransform(1.0, 1.0, px, py)
                };

                WheelCanvas.Children.Add(petalPath);
                _petalPaths.Add(petalPath);

                string rawTag = formats[i];
                string labelText = rawTag switch
                {
                    "SHELF"      => "📌 SHELF",
                    "MERGE PDF"  => "📑 MERGE",
                    "COMPRESS"   => "📉 COMP",
                    "COPY TEXT"  => "📝 OCR",
                    "NO BG"      => "✂️ NO BG",
                    "MUTE VIDEO" => "🔇 MUTE",
                    "TRIM 10S"   => "⏱️ 10s",
                    "AUTO SORT"  => "📂 SORT",
                    "AI SUMMARY" => "🧠 AI SUM",
                    _            => rawTag
                };

                var label = new TextBlock
                {
                    Text = labelText,
                    Foreground = _palette.TextDefaultBrush,
                    FontWeight = FontWeights.Bold,
                    FontSize = formats.Length > 8 ? 11.0 : 12.5,
                    IsHitTestVisible = false
                };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, px - label.DesiredSize.Width / 2);
                Canvas.SetTop(label, py - label.DesiredSize.Height / 2);
                WheelCanvas.Children.Add(label);

                _sliceLabels[petalPath] = label;
            }
        }

        private static Geometry CreateRoundedPetalGeometry(
            double cx, double cy, double rIn, double rOut,
            double startDeg, double endDeg, double gapPx, double cornerRadius)
        {
            double sRad = startDeg * Math.PI / 180.0;
            double eRad = endDeg * Math.PI / 180.0;

            double cr = Math.Min(cornerRadius, (rOut - rIn) * 0.32);

            double dOutGap = Math.Asin(Math.Clamp((gapPx / 2.0) / rOut, 0, 0.45));
            double dOutCorner = Math.Asin(Math.Clamp((gapPx / 2.0 + cr) / rOut, 0, 0.65));

            double dInGap = Math.Asin(Math.Clamp((gapPx / 2.0) / rIn, 0, 0.45));
            double dInCorner = Math.Asin(Math.Clamp((gapPx / 2.0 + cr) / rIn, 0, 0.65));

            Point pOutStartArc = Polar(cx, cy, rOut, sRad + dOutCorner);
            Point pOutEndArc   = Polar(cx, cy, rOut, eRad - dOutCorner);

            Point pEdgeEndOut  = Polar(cx, cy, rOut - cr, eRad - dOutGap);
            Point pEdgeEndIn   = Polar(cx, cy, rIn + cr,  eRad - (gapPx / 2.0) / (rIn + cr));

            Point pInEndArc    = Polar(cx, cy, rIn, eRad - dInCorner);
            Point pInStartArc  = Polar(cx, cy, rIn, sRad + dInCorner);

            Point pEdgeStartIn  = Polar(cx, cy, rIn + cr,  sRad + (gapPx / 2.0) / (rIn + cr));
            Point pEdgeStartOut = Polar(cx, cy, rOut - cr, sRad + dOutGap);

            Point qOutEnd   = Polar(cx, cy, rOut, eRad - dOutGap);
            Point qInEnd    = Polar(cx, cy, rIn,  eRad - dInGap);
            Point qInStart  = Polar(cx, cy, rIn,  sRad + dInGap);
            Point qOutStart = Polar(cx, cy, rOut, sRad + dOutGap);

            var fig = new PathFigure { StartPoint = pOutStartArc, IsClosed = true };
            fig.Segments.Add(new ArcSegment(pOutEndArc, new Size(rOut, rOut), 0, false, SweepDirection.Clockwise, true));
            fig.Segments.Add(new QuadraticBezierSegment(qOutEnd, pEdgeEndOut, true));
            fig.Segments.Add(new LineSegment(pEdgeEndIn, true));
            fig.Segments.Add(new QuadraticBezierSegment(qInEnd, pInEndArc, true));
            fig.Segments.Add(new ArcSegment(pInStartArc, new Size(rIn, rIn), 0, false, SweepDirection.Counterclockwise, true));
            fig.Segments.Add(new QuadraticBezierSegment(qInStart, pEdgeStartIn, true));
            fig.Segments.Add(new LineSegment(pEdgeStartOut, true));
            fig.Segments.Add(new QuadraticBezierSegment(qOutStart, pOutStartArc, true));

            var geom = new PathGeometry();
            geom.Figures.Add(fig);
            return geom;
        }

        private static Point Polar(double cx, double cy, double r, double rad)
            => new(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));

        private void AnimatePetalHover(System.Windows.Shapes.Path petal, bool isHovered)
        {
            petal.Fill = isHovered ? _palette.PetalHoverFill : _palette.PetalDefaultFill;
            if (_sliceLabels.TryGetValue(petal, out var label))
            {
                label.Foreground = isHovered ? _palette.TextHoverBrush : _palette.TextDefaultBrush;
            }

            if (petal.RenderTransform is ScaleTransform st)
            {
                double target = isHovered ? 1.07 : 1.0;
                var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(90))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            }
        }

        private void UpdateHoverByPoint(Point pos)
        {
            double dx = pos.X - 180.0;
            double dy = pos.Y - 180.0;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            System.Windows.Shapes.Path? hit = null;

            if (dist >= 42.0 && dist <= 165.0 && _petalPaths.Count > 0)
            {
                double deg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                double step = 360.0 / _petalPaths.Count;
                double normalized = (deg + 90.0 + (step / 2.0) + 360.0) % 360.0;
                int index = (int)(normalized / step) % _petalPaths.Count;
                hit = _petalPaths[index];
            }

            if (hit != _currentHoveredSlice)
            {
                if (_currentHoveredSlice != null)
                    AnimatePetalHover(_currentHoveredSlice, false);

                _currentHoveredSlice = hit;

                if (_currentHoveredSlice != null)
                    AnimatePetalHover(_currentHoveredSlice, true);
            }

            _selectedFormat = hit?.Tag as string;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            UpdateHoverByPoint(e.GetPosition(WheelCanvas));
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_selectedFormat == null)
                FastDismissWheel();
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                UpdateWheelForFiles(files);
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            UpdateHoverByPoint(e.GetPosition(WheelCanvas));
            e.Handled = true;
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            UpdateHoverByPoint(e.GetPosition(WheelCanvas));

            if (_selectedFormat == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                FastDismissWheel();
                return;
            }

            string actionFmt = _selectedFormat.ToUpperInvariant();
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (actionFmt == "SHELF")
            {
                _isWheelOpen = false;
                ResetHighlight();
                Hide();
                App.ShelfWin?.AddFiles(files);
                SystemSounds.Asterisk.Play();
                return;
            }

            if (actionFmt == "AUTO SORT")
            {
                _isWheelOpen = false;
                ResetHighlight();
                Hide();

                string msg = await Task.Run(() => SmartPowerTools.AutoSortFilesIntoFolders(files));
                SystemSounds.Asterisk.Play();
                MessageBox.Show(msg, "Tangerine Auto-Sort", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _isConverting = true;
            double winLeft = Left;
            double winTop = Top;

            _isWheelOpen = false;
            ResetHighlight();
            Hide();

            _progressWin ??= new ProgressWindow();
            _progressWin.ShowNear(winLeft, winTop);

            bool anyError = false;
            string lastErr = "";

            if (actionFmt == "MERGE PDF")
            {
                _progressWin.UpdateProgress($"Merging {files.Length} PDF files...", 0.45);
                await Task.Run(() =>
                {
                    try { PdfToolkit.MergePdfs(files); }
                    catch (Exception ex) { anyError = true; lastErr = ex.Message; }
                });
                _progressWin.UpdateProgress("Merged PDF saved!", 1.0);
            }
            else if (actionFmt == "COPY TEXT")
            {
                _progressWin.UpdateProgress("Extracting text (OCR)...", 0.5);
                string extracted = await SmartPowerTools.ExtractTextOcrAsync(files[0]);
                Clipboard.SetText(extracted);
                string outTxt = Path.Combine(Path.GetDirectoryName(files[0])!, Path.GetFileNameWithoutExtension(files[0]) + "_OCR.txt");
                File.WriteAllText(outTxt, extracted, System.Text.Encoding.UTF8);
                _progressWin.UpdateProgress("Text copied to Clipboard & saved!", 1.0);
            }
            else if (actionFmt == "AI SUMMARY")
            {
                _progressWin.UpdateProgress("Summarizing document...", 0.5);
                string summary = await SmartPowerTools.SummarizeDocumentWithOllamaAsync(files[0]);
                Clipboard.SetText(summary);
                _progressWin.UpdateProgress("Summary copied & saved to _Summary.txt!", 1.0);
            }
            else
            {
                string targetFmt = actionFmt.ToLowerInvariant();
                for (int i = 0; i < files.Length; i++)
                {
                    string file = files[i];
                    string shortName = Path.GetFileName(file);
                    _progressWin.UpdateProgress($"Processing {shortName} -> {actionFmt}...", (double)i / files.Length + 0.15);

                    await Task.Run(() =>
                    {
                        try
                        {
                            if (actionFmt == "COMPRESS")
                                PdfToolkit.CompressPdf(file);
                            else if (actionFmt == "NO BG")
                                SmartPowerTools.RemoveImageBackground(file);
                            else if (actionFmt == "MUTE VIDEO")
                                SmartPowerTools.MuteVideoStreamCopy(file);
                            else if (actionFmt == "TRIM 10S")
                                SmartPowerTools.TrimVideoPreview10s(file);
                            else
                                ConvertSingleFile(file, targetFmt);
                        }
                        catch (Exception ex)
                        {
                            anyError = true;
                            lastErr = ex.Message;
                            File.AppendAllText("conversion_errors.log", $"[{DateTime.Now}] {file}: {ex.Message}\r\n");
                        }
                    });

                    _progressWin.UpdateProgress($"Completed {shortName}", (double)(i + 1) / files.Length);
                }
            }

            if (anyError)
            {
                _progressWin.UpdateProgress($"Error: {lastErr}", 1.0);
                SystemSounds.Hand.Play();
                await Task.Delay(2400);
            }
            else
            {
                SystemSounds.Asterisk.Play();
                await Task.Delay(650);
            }

            _progressWin.Hide();
            _isConverting = false;
        }

        private static void ConvertSingleFile(string inputPath, string fmt)
        {
            if (Directory.Exists(inputPath) && fmt == "zip")
            {
                string zipOut = inputPath.TrimEnd('\\', '/') + ".zip";
                if (File.Exists(zipOut)) File.Delete(zipOut);
                ZipFile.CreateFromDirectory(inputPath, zipOut);
                return;
            }

            string dir = Path.GetDirectoryName(inputPath)!;
            string name = Path.GetFileNameWithoutExtension(inputPath);
            string inExt = Path.GetExtension(inputPath).ToLowerInvariant();
            string outPath = Path.Combine(dir, $"{name}_converted.{fmt}");

            if (inExt == ".pdf" && fmt == "pptx")
            {
                OfficeConverter.ConvertPdfToPowerPoint(inputPath, outPath);
                return;
            }

            if (inExt == ".pdf" && fmt == "xlsx")
            {
                OfficeConverter.ConvertPdfToExcel(inputPath, outPath);
                return;
            }

            if (WordExts.Contains(inExt) && fmt == "pptx")
            {
                string tempPdf = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
                OfficeConverter.ConvertWordOrPdfToWord(inputPath, tempPdf, "pdf");
                OfficeConverter.ConvertPdfToPowerPoint(tempPdf, outPath);
                if (File.Exists(tempPdf)) File.Delete(tempPdf);
                return;
            }

            if (WordExts.Contains(inExt) && fmt == "xlsx")
            {
                string tempPdf = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
                OfficeConverter.ConvertWordOrPdfToWord(inputPath, tempPdf, "pdf");
                OfficeConverter.ConvertPdfToExcel(tempPdf, outPath);
                if (File.Exists(tempPdf)) File.Delete(tempPdf);
                return;
            }

            if (WordExts.Contains(inExt) || (inExt == ".pdf" && fmt is "docx" or "txt" or "rtf" or "html") || (inExt == ".txt" && fmt is "docx" or "pdf" or "html"))
            {
                if (WordExts.Contains(inExt) && fmt is "png" or "jpg")
                {
                    string tempPdf = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
                    OfficeConverter.ConvertWordOrPdfToWord(inputPath, tempPdf, "pdf");
                    RenderPdfToImages(tempPdf, dir, name, fmt);
                    if (File.Exists(tempPdf)) File.Delete(tempPdf);
                    return;
                }

                OfficeConverter.ConvertWordOrPdfToWord(inputPath, outPath, fmt);
                return;
            }

            if (ExcelExts.Contains(inExt))
            {
                OfficeConverter.ConvertSpreadsheet(inputPath, outPath, fmt);
                return;
            }

            if (PptExts.Contains(inExt))
            {
                if (fmt is "png" or "jpg")
                {
                    string tempPdf = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
                    OfficeConverter.ConvertPowerPoint(inputPath, tempPdf, "pdf");
                    RenderPdfToImages(tempPdf, dir, name, fmt);
                    if (File.Exists(tempPdf)) File.Delete(tempPdf);
                    return;
                }
                OfficeConverter.ConvertPowerPoint(inputPath, outPath, fmt);
                return;
            }

            if (inExt == ".pdf" && fmt is "png" or "jpg" or "webp" or "tiff")
            {
                RenderPdfToImages(inputPath, dir, name, fmt);
                return;
            }

            if (fmt == "zip")
            {
                if (File.Exists(outPath)) File.Delete(outPath);
                using var archive = ZipFile.Open(outPath, ZipArchiveMode.Create);
                archive.CreateEntryFromFile(inputPath, Path.GetFileName(inputPath));
                return;
            }

            if (fmt is "jpg" or "png" or "webp" or "heic" or "pdf" or "ico" or "bmp" or "tiff" or "avif")
            {
                using var image = new MagickImage(inputPath);
                if (fmt == "ico") image.Resize(256, 256);
                image.Quality = 90;
                image.Format = fmt switch
                {
                    "jpg"  => MagickFormat.Jpg,
                    "png"  => MagickFormat.Png,
                    "webp" => MagickFormat.WebP,
                    "heic" => MagickFormat.Heic,
                    "pdf"  => MagickFormat.Pdf,
                    "ico"  => MagickFormat.Ico,
                    "bmp"  => MagickFormat.Bmp,
                    "tiff" => MagickFormat.Tiff,
                    "avif" => MagickFormat.Avif,
                    _      => MagickFormat.Png
                };
                image.Write(outPath);
            }
            else if (fmt is "mp4" or "mp3" or "wav" or "flac" or "aac" or "ogg" or "webm" or "gif")
            {
                FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToFile(outPath, overwrite: true)
                    .ProcessSynchronously();
            }
        }

        private static void RenderPdfToImages(string pdfPath, string outDir, string baseName, string fmt)
        {
            var settings = new MagickReadSettings { Density = new Density(200, 200) };
            using var images = new MagickImageCollection();
            images.Read(pdfPath, settings);
            int page = 1;
            foreach (var img in images)
            {
                string pageOut = Path.Combine(outDir, $"{baseName}_p{page}.{fmt}");
                img.Write(pageOut);
                page++;
            }
        }

        private void ResetHighlight()
        {
            if (_currentHoveredSlice != null)
            {
                AnimatePetalHover(_currentHoveredSlice, false);
                _currentHoveredSlice = null;
            }
            _selectedFormat = null;
        }
    }
}
