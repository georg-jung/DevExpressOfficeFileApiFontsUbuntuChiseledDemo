using System.Runtime.InteropServices;
using DevExpress.Drawing;
using DevExpress.XtraRichEdit;

internal static class Program
{
    // Contract:
    // - Logs to STDERR
    // - Writes the produced PDF bytes to STDOUT (and nothing else)
    // - Returns non-zero on failure
    public static int Main(string[] args)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;

            var docxPath = args.Length >= 1 ? args[0] : Path.Combine(baseDir, "demo.docx");
            
            string[] fontFiles = ["Inter-Regular.ttf", "JetBrainsMono-Regular.ttf"];
            string[] fontPaths = [.. fontFiles.Select(f => Path.Combine(baseDir, f))];

            Log($"OS: {RuntimeInformation.OSDescription}");
            Log($".NET: {RuntimeInformation.FrameworkDescription}");
            Log($"BaseDir: {baseDir}");
            Log($"DOCX: {docxPath}");
            foreach (var fp in fontPaths)
                Log($"Font: {fp}");

            if (!File.Exists(docxPath))
            {
                Log($"ERROR: DOCX not found: {docxPath}");
                return 2;
            }

            foreach (var fp in fontPaths)
            {
                if (!File.Exists(fp))
                {
                    Log($"ERROR: Font not found: {fp}");
                    return 3;
                }
            }

            // Be explicit for the test: force Skia so you don't accidentally pass due to GDI+ fallback.
            Settings.DrawingEngine = DrawingEngine.Skia;
            Log($"DevExpress.Drawing.Settings.DrawingEngine = {Settings.DrawingEngine}");

            string fontsPath = "/usr/share/fonts";
            try
            {
                Log($"Listing files in {fontsPath}:");

                foreach (string fullPath in Directory.EnumerateFiles(fontsPath, "*", SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(fontsPath, fullPath);
                    Log(relativePath);
                }
            }
            catch (Exception ex)
            {
                Log($"Error listing files in {fontsPath}\n\n{ex}\n\n");
            }

            // Useful signal: if DevExpress cannot find a font used by the doc, it may raise this.
            DXFontRepository.QueryNotFoundFont += (_, e) =>
            {
                Log($"[QueryNotFoundFont] requested='{e.RequestedFont}', actual='{e.ActualFont}'");

                // Optional "sledgehammer" to keep the test moving:
                // if your DOCX references a font and the repo doesn't have it,
                // supply the bytes from the first loaded font.
                e.FontFileData ??= File.ReadAllBytes(fontPaths[0]);
            };

            // Register the fonts BEFORE loading the document (avoids extra layout recalcs).
            foreach (var fp in fontPaths)
            {
                DXFontRepository.Instance.AddFont(fp);
            }

            // Log what the repository sees (helps verify your font actually registered)
            try
            {
                var fonts = DXFontRepository.Instance.GetFonts();
                var names = fonts.Select(f => f.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
                Log($"DXFontRepository fonts: {names.Length}");
                // Keep output short-ish; print first 30
                foreach (var n in names.Take(30))
                    Log($"  - {n}");
                if (names.Length > 30) Log("  - ...");
            }
            catch (Exception ex)
            {
                // Don't fail the run if introspection changes between versions.
                Log($"DXFontRepository.GetFonts() failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
            }

            using var word = new RichEditDocumentServer();

            Log("Loading DOCX...");
            word.LoadDocument(docxPath, DocumentFormat.Docx);

            Log("Exporting to PDF (stream)...");
            using var pdf = new MemoryStream();
            word.ExportToPdf(pdf);

            Log($"PDF bytes: {pdf.Length}");

            // Write PDF to stdout (binary)
            pdf.Position = 0;
            using var stdout = Console.OpenStandardOutput();
            pdf.CopyTo(stdout);
            stdout.Flush();

            Log("Done.");
            return 0;
        }
        catch (Exception ex)
        {
            Log("FATAL:");
            Log(ex.ToString());
            return 1;
        }
    }

    private static void Log(string message) =>
        Console.Error.WriteLine($"[{DateTimeOffset.UtcNow:O}] {message}");
}
