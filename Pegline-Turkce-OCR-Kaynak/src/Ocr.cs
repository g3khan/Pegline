using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Pegline
{
    // Offline OCR via the open-source Tesseract executable.
    // Installation: Tesseract in PATH, or tesseract.exe beside Pegline.exe.
    // For Turkish recognition install both tur.traineddata and eng.traineddata.
    static class Ocr
    {
        static string FindEngine()
        {
            string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tesseract.exe");
            if (File.Exists(local)) return local;
            string pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in pathVar.Split(Path.PathSeparator))
            {
                if (String.IsNullOrWhiteSpace(dir)) continue;
                try
                {
                    string file = Path.Combine(dir.Trim().Trim('"'), "tesseract.exe");
                    if (File.Exists(file)) return file;
                }
                catch (Exception) { }
            }
            foreach (string dir in new[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tesseract-OCR") })
            {
                string file = Path.Combine(dir, "tesseract.exe");
                if (File.Exists(file)) return file;
            }
            throw new InvalidOperationException("OCR için Tesseract kurulu değil. Tesseract OCR yükleyip Türkçe (tur) ve İngilizce (eng) dil paketlerini seçin. Ardından Pegline'ı yeniden başlatın.");
        }

        public static string Extract(string imagePath)
        {
            string exe = FindEngine();
            string result = Run(exe, imagePath, "tur+eng");
            return result.Trim();
        }

        static string Run(string exe, string imagePath, string languages)
        {
            // stdout is UTF-8; text is never sent to a cloud service.
            var psi = new ProcessStartInfo(exe)
            {
                Arguments = "\"" + imagePath.Replace("\"", "") + "\" stdout -l " + languages,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using (var process = Process.Start(psi))
            {
                // Reading both streams concurrently avoids a pipe deadlock.
                var errorTask = System.Threading.Tasks.Task.Run(() => process.StandardError.ReadToEnd());
                string output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(30000))
                {
                    try { process.Kill(); } catch (Exception) { }
                    throw new TimeoutException("OCR işlemi 30 saniyede tamamlanamadı.");
                }
                string errors = errorTask.Result;
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("OCR başarısız. Tesseract Türkçe ve İngilizce dil dosyalarını (tur.traineddata, eng.traineddata) kontrol edin.\n" + errors);
                return output;
            }
        }
    }
}
