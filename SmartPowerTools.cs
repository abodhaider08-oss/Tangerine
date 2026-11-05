using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using ImageMagick;

namespace TangerineWin
{
    public static class SmartPowerTools
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(45) };

        // 1. LOCAL INSTANT OCR (IMAGE / PDF -> COPY TEXT TO CLIPBOARD & TXT)
        public static async Task<string> ExtractTextOcrAsync(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".pdf")
            {
                string pdfText = PdfToolkit.ExtractTextFromPdf(filePath);
                if (!string.IsNullOrWhiteSpace(pdfText))
                    return pdfText;
            }

            if (ext is ".txt" or ".csv" or ".md" or ".json" or ".xml" or ".log")
            {
                return await File.ReadAllTextAsync(filePath);
            }

            string safePath = filePath.Replace("'", "''");
            string[] psLines =
            {
                "$ErrorActionPreference = 'Stop'",
                "Add-Type -AssemblyName System.Runtime.WindowsRuntime",
                "$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]",
                "$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime]",
                "$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Foundation, ContentType = WindowsRuntime]",
                "function Await($WinRtTask, $ResultType) {",
                "    $asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]",
                "    $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)",
                "    $netTask = $asTask.Invoke($null, @($WinRtTask))",
                "    $netTask.Wait(-1) | Out-Null",
                "    return $netTask.Result",
                "}",
                "$file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync('" + safePath + "')) ([Windows.Storage.StorageFile])",
                "$stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])",
                "$decoder = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])",
                "$bitmap = Await ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])",
                "$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()",
                "if ($null -eq $engine) { $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage((New-Object Windows.Globalization.Language('en-US'))) }",
                "$result = Await ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])",
                "Write-Output $result.Text"
            };

            string psScript = string.Join("\r\n", psLines);
            try
            {
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(psScript));
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -NonInteractive -EncodedCommand " + encoded,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = await proc.StandardOutput.ReadToEndAsync();
                    await proc.WaitForExitAsync();
                    if (!string.IsNullOrWhiteSpace(output))
                        return output.Trim();
                }
            }
            catch { }

            return "[OCR] No readable text found in image.";
        }

        // 2. SMART OFFLINE BACKGROUND REMOVER (NO BG -> TRANSPARENT PNG)
        public static string RemoveImageBackground(string inputPath)
        {
            string dir = Path.GetDirectoryName(inputPath)!;
            string name = Path.GetFileNameWithoutExtension(inputPath);
            string outPath = Path.Combine(dir, $"{name}_nobg.png");

            using var image = new MagickImage(inputPath);
            image.Alpha(AlphaOption.Set);
            image.ColorFuzz = new Percentage(14);

            var bgPixel = image.GetPixels().GetPixel(0, 0).ToColor();
            if (bgPixel != null)
            {
                image.Transparent(bgPixel);
            }

            var trPixel = image.GetPixels().GetPixel((int)image.Width - 1, 0).ToColor();
            if (trPixel != null)
            {
                image.Transparent(trPixel);
            }

            image.Format = MagickFormat.Png32;
            image.Write(outPath);
            return outPath;
        }

        // 3. LIGHTNING VIDEO STREAM-COPY TOOLS (MUTE VIDEO & TRIM 10s IN 0.2s)
        public static string MuteVideoStreamCopy(string inputPath)
        {
            string dir = Path.GetDirectoryName(inputPath)!;
            string name = Path.GetFileNameWithoutExtension(inputPath);
            string ext = Path.GetExtension(inputPath);
            string outPath = Path.Combine(dir, $"{name}_muted{ext}");

            RunFfmpegRaw("-y -i \"" + inputPath + "\" -c:v copy -an \"" + outPath + "\"");
            return outPath;
        }

        public static string TrimVideoPreview10s(string inputPath)
        {
            string dir = Path.GetDirectoryName(inputPath)!;
            string name = Path.GetFileNameWithoutExtension(inputPath);
            string ext = Path.GetExtension(inputPath);
            string outPath = Path.Combine(dir, $"{name}_10s{ext}");

            RunFfmpegRaw("-y -ss 00:00:00 -t 10 -i \"" + inputPath + "\" -c copy \"" + outPath + "\"");
            return outPath;
        }

        private static void RunFfmpegRaw(string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(30000);
        }

        // 4. SMART BATCH AUTO-SORT & ORGANIZER (AUTO SORT)
        public static string AutoSortFilesIntoFolders(string[] files)
        {
            if (files.Length == 0) return "";
            string baseDir = Path.GetDirectoryName(files[0])!;
            int movedCount = 0;

            foreach (var file in files)
            {
                if (!File.Exists(file)) continue;
                string ext = Path.GetExtension(file).ToLowerInvariant();

                string category = ext switch
                {
                    ".jpg" or ".jpeg" or ".png" or ".webp" or ".heic" or ".gif" or ".bmp" or ".tiff" or ".avif" or ".ico" => "Sorted_Images",
                    ".pdf" or ".docx" or ".doc" or ".xlsx" or ".xls" or ".pptx" or ".ppt" or ".txt" or ".csv" or ".rtf"   => "Sorted_Documents",
                    ".mp4" or ".mkv" or ".mov" or ".avi" or ".webm"                                                       => "Sorted_Videos",
                    ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a"                                             => "Sorted_Audio",
                    ".zip" or ".rar" or ".7z" or ".tar" or ".gz"                                                          => "Sorted_Archives",
                    _ => "Sorted_Other"
                };

                string targetFolder = Path.Combine(baseDir, category);
                Directory.CreateDirectory(targetFolder);

                string destFile = Path.Combine(targetFolder, Path.GetFileName(file));
                if (File.Exists(destFile))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    destFile = Path.Combine(targetFolder, $"{name}_{DateTime.Now:HHmmss}{ext}");
                }

                File.Move(file, destFile);
                movedCount++;
            }

            return $"Sorted {movedCount} files into categorized folders!";
        }

        // 5. LOCAL OLLAMA AI DOCUMENT SUMMARIZER (AI SUMMARY - SAVES _Summary.txt & COPIES TO CLIPBOARD)
        public static async Task<string> SummarizeDocumentWithOllamaAsync(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string rawText = "";

            if (ext == ".pdf")
            {
                rawText = PdfToolkit.ExtractTextFromPdf(filePath);
            }
            else if (ext is ".txt" or ".md" or ".csv" or ".json" or ".log")
            {
                rawText = await File.ReadAllTextAsync(filePath);
            }
            else if (ext == ".xlsx")
            {
                using var wb = new XLWorkbook(filePath);
                var ws = wb.Worksheets.First();
                var sb = new StringBuilder();
                foreach (var r in ws.RowsUsed().Take(80))
                    sb.AppendLine(string.Join(" | ", r.CellsUsed().Select(c => c.GetValue<string>())));
                rawText = sb.ToString();
            }
            else
            {
                string tempTxt = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
                try
                {
                    OfficeConverter.ConvertWordOrPdfToWord(filePath, tempTxt, "txt");
                    if (File.Exists(tempTxt))
                    {
                        rawText = await File.ReadAllTextAsync(tempTxt);
                        File.Delete(tempTxt);
                    }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(rawText))
                return "Could not extract text from document.";

            string clipped = rawText.Length > 7500 ? rawText.Substring(0, 7500) : rawText;
            string dir = Path.GetDirectoryName(filePath)!;
            string name = Path.GetFileNameWithoutExtension(filePath);
            string summaryOutPath = Path.Combine(dir, $"{name}_Summary.txt");

            try
            {
                string tagsJson = await Http.GetStringAsync("http://localhost:11434/api/tags");
                using var doc = JsonDocument.Parse(tagsJson);
                var models = doc.RootElement.GetProperty("models").EnumerateArray()
                                .Select(m => m.GetProperty("name").GetString() ?? "")
                                .Where(n => !string.IsNullOrEmpty(n))
                                .ToList();

                string chosenModel = models.FirstOrDefault(m => m.Contains("qwen", StringComparison.OrdinalIgnoreCase))
                                  ?? models.FirstOrDefault(m => m.Contains("llama", StringComparison.OrdinalIgnoreCase))
                                  ?? models.FirstOrDefault(m => m.Contains("deepseek", StringComparison.OrdinalIgnoreCase))
                                  ?? models.FirstOrDefault()
                                  ?? "qwen2.5:7b";

                var payload = new
                {
                    model = chosenModel,
                    prompt = "Summarize the following document concisely in clear bullet points:\n\n" + clipped,
                    stream = false
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var resp = await Http.PostAsync("http://localhost:11434/api/generate", content);
                if (resp.IsSuccessStatusCode)
                {
                    string respJson = await resp.Content.ReadAsStringAsync();
                    using var respDoc = JsonDocument.Parse(respJson);
                    string aiText = respDoc.RootElement.GetProperty("response").GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(aiText))
                    {
                        File.WriteAllText(summaryOutPath, aiText.Trim(), Encoding.UTF8);
                        return aiText.Trim();
                    }
                }
            }
            catch { }

            var lines = rawText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(l => l.Trim())
                               .Where(l => l.Length > 25)
                               .Take(12)
                               .ToList();

            var fallbackSb = new StringBuilder();
            fallbackSb.AppendLine($"[Document Summary - {Path.GetFileName(filePath)}]");
            fallbackSb.AppendLine($"Total Words: {rawText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length:N0}");
            fallbackSb.AppendLine("--------------------------------------------------");
            foreach (var l in lines)
                fallbackSb.AppendLine("- " + l);

            string finalFallback = fallbackSb.ToString();
            File.WriteAllText(summaryOutPath, finalFallback, Encoding.UTF8);
            return finalFallback;
        }
    }
}
