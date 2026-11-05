using System;
using System.IO;
using System.Linq;
using System.Text;
using ImageMagick;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using UglyToad.PdfPig;

namespace TangerineWin
{
    public static class PdfToolkit
    {
        // 1. Merge Multiple PDFs into a single combined PDF in 0.1s
        public static string MergePdfs(string[] pdfFiles)
        {
            var validPdfs = pdfFiles.Where(f => File.Exists(f) && Path.GetExtension(f).Equals(".pdf", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (validPdfs.Length == 0)
                throw new Exception("No valid PDF files selected to merge.");

            string firstDir = Path.GetDirectoryName(validPdfs[0])!;
            string firstName = Path.GetFileNameWithoutExtension(validPdfs[0]);
            string outPath = Path.Combine(firstDir, $"{firstName}_Merged_{validPdfs.Length}Files.pdf");

            using var outPdf = new PdfSharpCore.Pdf.PdfDocument();
            foreach (var file in validPdfs)
            {
                using var inputPdf = PdfReader.Open(file, PdfDocumentOpenMode.Import);
                for (int idx = 0; idx < inputPdf.PageCount; idx++)
                {
                    outPdf.AddPage(inputPdf.Pages[idx]);
                }
            }
            outPdf.Save(outPath);
            return outPath;
        }

        // 2. Compress PDF file size
        public static string CompressPdf(string inputPath)
        {
            string dir = Path.GetDirectoryName(inputPath)!;
            string name = Path.GetFileNameWithoutExtension(inputPath);
            string outPath = Path.Combine(dir, $"{name}_compressed.pdf");

            using (var doc = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify))
            {
                doc.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
                doc.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Automatic;
                doc.Options.NoCompression = false;
                doc.Options.CompressContentStreams = true;
                doc.Save(outPath);
            }

            try
            {
                var origLen = new FileInfo(inputPath).Length;
                var newLen = new FileInfo(outPath).Length;
                if (newLen >= origLen * 0.92)
                {
                    var settings = new MagickReadSettings { Density = new Density(130, 130) };
                    using var pages = new MagickImageCollection();
                    pages.Read(inputPath, settings);
                    foreach (var p in pages)
                    {
                        p.Format = MagickFormat.Pdf;
                        p.Quality = 62;
                        p.Settings.Compression = CompressionMethod.JPEG;
                    }
                    string tempCompressed = Path.Combine(dir, $"{name}_opt_temp.pdf");
                    pages.Write(tempCompressed);
                    if (File.Exists(tempCompressed) && new FileInfo(tempCompressed).Length < newLen)
                    {
                        File.Copy(tempCompressed, outPath, true);
                    }
                    if (File.Exists(tempCompressed)) File.Delete(tempCompressed);
                }
            }
            catch { }

            return outPath;
        }

        // 3. Standalone Offline PDF Text Extraction via PdfPig (Works without MS Office!)
        public static string ExtractTextFromPdf(string pdfPath)
        {
            var sb = new StringBuilder();
            using var document = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
            foreach (var page in document.GetPages())
            {
                sb.AppendLine($"--- [Page {page.Number}] ---");
                sb.AppendLine(page.Text);
                sb.AppendLine();
            }
            return sb.ToString().Trim();
        }
    }
}
