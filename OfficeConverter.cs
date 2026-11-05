using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using ImageMagick;

namespace TangerineWin
{
    public static class OfficeConverter
    {
        public static bool ConvertWordOrPdfToWord(string inputPath, string outPath, string targetFmt)
        {
            if (TryWordComConvert(inputPath, outPath, targetFmt))
                return true;

            string dir = Path.GetDirectoryName(outPath)!;
            if (TryLibreOfficeConvert(inputPath, dir, targetFmt))
                return true;

            throw new Exception($"Could not convert {Path.GetFileName(inputPath)} to {targetFmt}. Ensure Microsoft Word or LibreOffice is installed.");
        }

        public static bool ConvertPdfToPowerPoint(string inputPath, string outPath)
        {
            if (TryPdfToPptxViaCom(inputPath, outPath))
                return true;

            string dir = Path.GetDirectoryName(outPath)!;
            if (TryLibreOfficePdfToPptx(inputPath, dir))
                return true;

            throw new Exception("PDF to PPTX requires Microsoft PowerPoint or LibreOffice.");
        }

        public static bool ConvertPdfToExcel(string inputPath, string outPath)
        {
            if (TryPdfToXlsxViaOfficeCom(inputPath, outPath))
                return true;

            string dir = Path.GetDirectoryName(outPath)!;
            if (TryLibreOfficeConvert(inputPath, dir, "xlsx"))
                return true;

            throw new Exception("PDF to XLSX requires Microsoft Office or LibreOffice.");
        }

        public static bool ConvertSpreadsheet(string inputPath, string outPath, string targetFmt)
        {
            string inExt = Path.GetExtension(inputPath).ToLowerInvariant();

            if (inExt == ".xlsx" && targetFmt == "csv")
            {
                using var wb = new XLWorkbook(inputPath);
                var ws = wb.Worksheets.First();
                var sb = new StringBuilder();
                foreach (var row in ws.RowsUsed())
                {
                    var cells = row.CellsUsed().Select(c => "\"" + c.GetValue<string>().Replace("\"", "\"\"") + "\"");
                    sb.AppendLine(string.Join(",", cells));
                }
                File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
                return true;
            }

            if (inExt == ".csv" && targetFmt == "xlsx")
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Sheet1");
                var lines = File.ReadAllLines(inputPath);
                for (int r = 0; r < lines.Length; r++)
                {
                    var cols = lines[r].Split(',');
                    for (int c = 0; c < cols.Length; c++)
                    {
                        ws.Cell(r + 1, c + 1).Value = cols[c].Trim('\"');
                    }
                }
                wb.SaveAs(outPath);
                return true;
            }

            if (inExt == ".xlsx" && targetFmt == "html")
            {
                using var wb = new XLWorkbook(inputPath);
                var ws = wb.Worksheets.First();
                var sb = new StringBuilder("<html><head><meta charset='utf-8'><style>table{border-collapse:collapse;font-family:sans-serif;}td,th{border:1px solid #ccc;padding:6px 10px;}</style></head><body><table>");
                foreach (var row in ws.RowsUsed())
                {
                    sb.Append("<tr>");
                    foreach (var cell in row.CellsUsed())
                        sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(cell.GetValue<string>())}</td>");
                    sb.Append("</tr>");
                }
                sb.Append("</table></body></html>");
                File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
                return true;
            }

            if (inExt == ".xlsx" && targetFmt == "txt")
            {
                using var wb = new XLWorkbook(inputPath);
                var ws = wb.Worksheets.First();
                var sb = new StringBuilder();
                foreach (var row in ws.RowsUsed())
                {
                    var cells = row.CellsUsed().Select(c => c.GetValue<string>());
                    sb.AppendLine(string.Join("\t", cells));
                }
                File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
                return true;
            }

            if (targetFmt == "docx")
            {
                string tempHtml = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.html");
                ConvertSpreadsheet(inputPath, tempHtml, "html");
                bool ok = TryWordComConvert(tempHtml, outPath, "docx");
                if (File.Exists(tempHtml)) File.Delete(tempHtml);
                if (ok) return true;
            }

            if (TryExcelComConvert(inputPath, outPath, targetFmt))
                return true;

            string dir = Path.GetDirectoryName(outPath)!;
            if (TryLibreOfficeConvert(inputPath, dir, targetFmt))
                return true;

            throw new Exception($"Spreadsheet conversion to {targetFmt} requires Excel or LibreOffice.");
        }

        public static bool ConvertPowerPoint(string inputPath, string outPath, string targetFmt)
        {
            if (targetFmt == "docx")
            {
                string tempPdf = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
                if (TryPowerPointComConvert(inputPath, tempPdf, "pdf"))
                {
                    bool ok = TryWordComConvert(tempPdf, outPath, "docx");
                    if (File.Exists(tempPdf)) File.Delete(tempPdf);
                    if (ok) return true;
                }
            }

            if (TryPowerPointComConvert(inputPath, outPath, targetFmt))
                return true;

            string dir = Path.GetDirectoryName(outPath)!;
            if (TryLibreOfficeConvert(inputPath, dir, targetFmt))
                return true;

            throw new Exception("PowerPoint conversion requires MS PowerPoint or LibreOffice.");
        }

        private static bool TryPdfToPptxViaCom(string pdfPath, string outPptxPath)
        {
            dynamic? pptApp = null;
            dynamic? pres = null;
            try
            {
                Type? pptType = Type.GetTypeFromProgID("PowerPoint.Application");
                if (pptType == null) return false;

                pptApp = Activator.CreateInstance(pptType);
                if (pptApp == null) return false;

                pres = pptApp.Presentations.Add(0);

                try
                {
                    var settings = new MagickReadSettings { Density = new Density(180, 180) };
                    using var images = new MagickImageCollection();
                    images.Read(pdfPath, settings);
                    int slideIdx = 1;
                    foreach (var img in images)
                    {
                        string tempPng = Path.Combine(Path.GetTempPath(), $"slide_{Guid.NewGuid()}.png");
                        img.Write(tempPng);
                        dynamic slide = pres.Slides.Add(slideIdx, 12);
                        float slideW = (float)pres.PageSetup.SlideWidth;
                        float slideH = (float)pres.PageSetup.SlideHeight;
                        slide.Shapes.AddPicture(tempPng, 0, -1, 0, 0, slideW, slideH);
                        if (File.Exists(tempPng)) File.Delete(tempPng);
                        slideIdx++;
                    }
                }
                catch
                {
                    string extractedText = PdfToolkit.ExtractTextFromPdf(pdfPath);
                    var chunks = extractedText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                              .Select(s => s.Trim())
                                              .Where(s => s.Length > 2)
                                              .Chunk(8)
                                              .ToList();

                    int sIdx = 1;
                    foreach (var chunk in chunks)
                    {
                        dynamic slide = pres.Slides.Add(sIdx, 2);
                        slide.Shapes[1].TextFrame.TextRange.Text = $"Slide {sIdx}";
                        slide.Shapes[2].TextFrame.TextRange.Text = string.Join("\r\n", chunk);
                        sIdx++;
                    }
                }

                pres.SaveAs(outPptxPath, 24);
                pres.Close();
                pptApp.Quit();
                return File.Exists(outPptxPath);
            }
            catch
            {
                try { pres?.Close(); } catch { }
                try { pptApp?.Quit(); } catch { }
                return false;
            }
        }

        private static bool TryPdfToXlsxViaOfficeCom(string pdfPath, string outXlsxPath)
        {
            try
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Extracted_PDF");

                string text = PdfToolkit.ExtractTextFromPdf(pdfPath);
                int currentRow = 1;
                foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string clean = line.Trim();
                    if (!string.IsNullOrEmpty(clean))
                    {
                        ws.Cell(currentRow, 1).Value = clean;
                        currentRow++;
                    }
                }

                wb.SaveAs(outXlsxPath);
                return File.Exists(outXlsxPath);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryWordComConvert(string inputPath, string outPath, string targetFmt)
        {
            dynamic? wordApp = null;
            dynamic? doc = null;
            try
            {
                Type? wordType = Type.GetTypeFromProgID("Word.Application");
                if (wordType == null) return false;

                wordApp = Activator.CreateInstance(wordType);
                if (wordApp == null) return false;
                wordApp.Visible = false;
                wordApp.DisplayAlerts = 0;

                doc = wordApp.Documents.Open(inputPath, false, true);

                int wdFormat = targetFmt switch
                {
                    "docx" => 16,
                    "pdf"  => 17,
                    "txt"  => 2,
                    "rtf"  => 6,
                    "html" => 8,
                    _      => 17
                };

                doc.SaveAs2(outPath, wdFormat);
                doc.Close(false);
                wordApp.Quit(false);
                return File.Exists(outPath);
            }
            catch
            {
                try { doc?.Close(false); } catch { }
                try { wordApp?.Quit(false); } catch { }
                return false;
            }
        }

        private static bool TryExcelComConvert(string inputPath, string outPath, string targetFmt)
        {
            dynamic? excelApp = null;
            dynamic? wb = null;
            try
            {
                Type? excelType = Type.GetTypeFromProgID("Excel.Application");
                if (excelType == null) return false;

                excelApp = Activator.CreateInstance(excelType);
                if (excelApp == null) return false;
                excelApp.Visible = false;
                excelApp.DisplayAlerts = false;

                wb = excelApp.Workbooks.Open(inputPath, ReadOnly: true);

                if (targetFmt == "pdf")
                {
                    wb.ExportAsFixedFormat(0, outPath);
                }
                else
                {
                    int xlFormat = targetFmt switch
                    {
                        "xlsx" => 51,
                        "csv"  => 6,
                        "html" => 44,
                        _      => 51
                    };
                    wb.SaveAs(outPath, xlFormat);
                }

                wb.Close(false);
                excelApp.Quit();
                return File.Exists(outPath);
            }
            catch
            {
                try { wb?.Close(false); } catch { }
                try { excelApp?.Quit(); } catch { }
                return false;
            }
        }

        private static bool TryPowerPointComConvert(string inputPath, string outPath, string targetFmt)
        {
            dynamic? pptApp = null;
            dynamic? pres = null;
            try
            {
                Type? pptType = Type.GetTypeFromProgID("PowerPoint.Application");
                if (pptType == null) return false;

                pptApp = Activator.CreateInstance(pptType);
                if (pptApp == null) return false;

                pres = pptApp.Presentations.Open(inputPath, -1, 0, 0);
                if (targetFmt == "pdf")
                {
                    pres.SaveAs(outPath, 32);
                }
                else if (targetFmt == "pptx")
                {
                    pres.SaveAs(outPath, 24);
                }
                pres.Close();
                pptApp.Quit();
                return File.Exists(outPath);
            }
            catch
            {
                try { pres?.Close(); } catch { }
                try { pptApp?.Quit(); } catch { }
                return false;
            }
        }

        private static bool TryLibreOfficePdfToPptx(string inputPath, string outDir)
        {
            string[] possiblePaths =
            {
                @"C:\Program Files\LibreOffice\program\soffice.exe",
                @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
            };

            string? soffice = possiblePaths.FirstOrDefault(File.Exists);
            if (soffice == null) return false;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = soffice,
                    Arguments = "--headless --infilter=\"impress_pdf_import\" --convert-to pptx --outdir \"" + outDir + "\" \"" + inputPath + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(30000);
                return proc?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryLibreOfficeConvert(string inputPath, string outDir, string targetFmt)
        {
            string[] possiblePaths =
            {
                @"C:\Program Files\LibreOffice\program\soffice.exe",
                @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
            };

            string? soffice = possiblePaths.FirstOrDefault(File.Exists);
            if (soffice == null) return false;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = soffice,
                    Arguments = "--headless --convert-to " + targetFmt + " --outdir \"" + outDir + "\" \"" + inputPath + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(25000);
                return proc?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
