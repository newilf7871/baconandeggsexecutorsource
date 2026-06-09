// © BNE Softworks (github.com/newilf7871)
// chrome webview tabs from https://github.com/adamschwartz/chrome-tabs credit to him
// licensed under GNU GPLv3 (see LICENSE)
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace v2bootstrapper
{
    public static class Bootstrapper
    {
        private const string PastebinUrl = "https://pastebin.com/raw/y7W99xPr";
        private static readonly string AppFolderName = "v2executor";
        private static readonly string ProgramFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppFolderName);
        private static readonly string LinkFilePath = Path.Combine(ProgramFilesPath, "update_link.txt");
        private static readonly HttpClient client;

        static Bootstrapper()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        }

        public static void Initialize()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;
            System.Net.ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

            _ = Task.Run(async () =>
            {
                try
                {
                    await CheckForUpdates(isInitial: true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Bootstrapper] Initial check failed: {ex.Message}");
                }

                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30));
                    try
                    {
                        await CheckForUpdates(isInitial: false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Bootstrapper] Background check failed: {ex.Message}");
                    }
                }
            });
        }

        private static bool HasExistingInstall() =>
            Directory.Exists(ProgramFilesPath) &&
            Directory.GetFiles(ProgramFilesPath, "*.exe", SearchOption.AllDirectories)
                .Any(f => Path.GetFileNameWithoutExtension(f).Equals("v2executor", StringComparison.OrdinalIgnoreCase));

        private static async Task CheckForUpdates(bool isInitial)
        {
            UpdateForm? progressForm = null;
            bool cleanupForm = false;

            try
            {
                if (isInitial)
                {
                    var tcs = new TaskCompletionSource<UpdateForm>();
                    var thread = new Thread(() =>
                    {
                        var form = new UpdateForm();
                        form.Load += (s, e) => tcs.SetResult(form);
                        Application.Run(form);
                    });
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();

                    progressForm = await tcs.Task;
                    progressForm.UpdateProgress(0, "Checking for updates...");
                    cleanupForm = true;
                }

                string? directLink = await GetDirectLinkFromPastebin();

                if (string.IsNullOrWhiteSpace(directLink) || !directLink.StartsWith("http"))
                {
                        if (HasExistingInstall())
                    {
                        if (progressForm != null) progressForm.UpdateProgress(0, "Launching...");
                        LaunchFromProgramFiles();
                    }
                    else if (progressForm != null)
                    {
                        progressForm.UpdateProgress(0, "Pastebin unreachable, no local install.");
                        await Task.Delay(2000);
                    }
                    goto Cleanup;
                }

                bool updateNeeded = false;
                if (!Directory.Exists(ProgramFilesPath) || !File.Exists(LinkFilePath))
                {
                    updateNeeded = true;
                }
                else
                {
                    try
                    {
                        string savedLink = File.ReadAllText(LinkFilePath).Trim();
                        if (directLink != savedLink) updateNeeded = true;
                    }
                    catch { updateNeeded = true; }
                }

                if (updateNeeded)
                {
                    await DownloadAndApplyUpdate(directLink, progressForm);
                    cleanupForm = false;
                }
                else if (VerifyInstallIntegrity())
                {
                    if (progressForm != null) progressForm.UpdateProgress(100, "Up to date. Launching...");
                    await Task.Delay(300);
                    LaunchFromProgramFiles();
                }
                else
                {
                        if (progressForm != null) progressForm.UpdateProgress(0, "Install corrupted. Re-downloading...");
                    try { File.Delete(LinkFilePath); } catch { }
                    await Task.Delay(300);
                    await DownloadAndApplyUpdate(directLink, progressForm);
                    cleanupForm = false;
                }

            Cleanup:
                if (cleanupForm && progressForm != null)
                {
                    if (progressForm.InvokeRequired) progressForm.Invoke(new Action(progressForm.Close));
                    else progressForm.Close();
                }
            }
            catch (Exception ex)
            {
                if (cleanupForm && progressForm != null)
                {
                    try { progressForm.Invoke(new Action(progressForm.Close)); } catch { }
                }
                MessageBox.Show($"Bootstrapper error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void LaunchFromProgramFiles()
        {
            try
            {
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";

                string? exeToRun = FindInstalledExe();
                if (exeToRun == null)
                {
                    MessageBox.Show($"No v2executor.exe found in {ProgramFilesPath}. The downloaded archive may be empty or corrupted.", "Launch Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                if (string.Equals(currentExe, exeToRun, StringComparison.OrdinalIgnoreCase)) return;

                var psi = new ProcessStartInfo
                {
                    FileName = exeToRun,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exeToRun) ?? ProgramFilesPath
                };
                Process.Start(psi);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch v2executor: {ex.Message}", "Launch Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool IsValidZipFile(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var header = new byte[4];
                if (fs.Read(header, 0, 4) != 4) return false;
                return header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;
            }
            catch { return false; }
        }

        private static string? FindInstalledExe()
        {
            if (!Directory.Exists(ProgramFilesPath)) return null;
            var files = Directory.GetFiles(ProgramFilesPath, "*.exe", SearchOption.AllDirectories);
            return files.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f).Equals("v2executor", StringComparison.OrdinalIgnoreCase));
        }

        private static bool VerifyInstallIntegrity()
        {
            var exePath = FindInstalledExe();
            if (exePath == null || !File.Exists(exePath)) return false;

            try
            {
                var info = new FileInfo(exePath);
                if (info.Length < 1024) return false;

                using var fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var header = new byte[2];
                if (fs.Read(header, 0, 2) != 2) return false;
                return header[0] == 0x4D && header[1] == 0x5A;
            }
            catch { return false; }
        }

        private static async Task<string> GetDirectLinkFromPastebin()
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, PastebinUrl))
                {
                    request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                    var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        return content.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Bootstrapper] Fetch failed: {ex.Message}");
            }
            return string.Empty;
        }

        private static async Task DownloadAndApplyUpdate(string url, UpdateForm? progressForm)
        {
            try
            {
                if (progressForm == null)
                {
                    var tcs = new TaskCompletionSource<UpdateForm>();
                    var thread = new Thread(() =>
                    {
                        var form = new UpdateForm();
                        form.Load += (s, e) => tcs.SetResult(form);
                        Application.Run(form);
                    });
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    progressForm = await tcs.Task;
                }

                string tempZip = Path.Combine(Path.GetTempPath(), "v2executor_download.zip");
                try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                bool downloadSuccess = false;

                try
                {
                    progressForm.UpdateProgress(0, "Connecting (M1)...");
                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                        using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                        {
                            response.EnsureSuccessStatusCode();
                            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                            using (var contentStream = await response.Content.ReadAsStreamAsync())
                            using (var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                            {
                                var buffer = new byte[8192];
                                var totalRead = 0L;
                                int bytesRead;
                                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                                    totalRead += bytesRead;
                                    if (totalBytes != -1)
                                        progressForm.UpdateProgress((int)((double)totalRead / totalBytes * 100), "Downloading...");
                                }
                            }
                            downloadSuccess = true;
                        }
                    }
                }
                catch { }

                if (!downloadSuccess)
                {
                    try
                    {
                        progressForm.UpdateProgress(0, "Connecting (M2)...");
                        var psi = new ProcessStartInfo
                        {
                            FileName = "curl.exe",
                            Arguments = $"-L -k -o \"{tempZip}\" \"{url}\" -A \"Mozilla/5.0\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using (var proc = Process.Start(psi))
                        {
                            if (proc != null)
                            {
                                await proc.WaitForExitAsync();
                                if (File.Exists(tempZip) && new FileInfo(tempZip).Length > 1000) downloadSuccess = true;
                            }
                        }
                    }
                    catch { }
                }

                if (!downloadSuccess)
                {
                    try
                    {
                        progressForm.UpdateProgress(0, "Connecting (M3)...");
                        var bitsCmd = $"Import-Module BitsTransfer; Start-BitsTransfer -Source '{url}' -Destination '{tempZip}' -UserAgent 'Mozilla/5.0'";
                        var psi = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{bitsCmd}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using (var proc = Process.Start(psi))
                        {
                            if (proc != null)
                            {
                                await proc.WaitForExitAsync();
                                if (File.Exists(tempZip) && new FileInfo(tempZip).Length > 1000) downloadSuccess = true;
                            }
                        }
                    }
                    catch { }
                }

                if (!downloadSuccess)
                {
                    try
                    {
                        progressForm.UpdateProgress(0, "Connecting (M4)...");
                        var psCmd = $"[Net.ServicePointManager]::ServerCertificateValidationCallback = {{$true}}; $wc = New-Object System.Net.WebClient; $wc.Headers.Add('User-Agent','Mozilla/5.0'); $wc.DownloadFile('{url}', '{tempZip}')";
                        var psi = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCmd}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using (var proc = Process.Start(psi))
                        {
                            if (proc != null)
                            {
                                await proc.WaitForExitAsync();
                                if (File.Exists(tempZip) && new FileInfo(tempZip).Length > 1000) downloadSuccess = true;
                            }
                        }
                    }
                    catch { }
                }

                if (!downloadSuccess)
                {
                    try
                    {
                        progressForm.UpdateProgress(0, "Connecting (M5)...");
                            var clearPsi = new ProcessStartInfo
                        {
                            FileName = "certutil.exe",
                            Arguments = $"-urlcache -f \"{url}\" delete",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        try { using var clearProc = Process.Start(clearPsi); clearProc?.WaitForExit(); } catch { }

                        var psi = new ProcessStartInfo
                        {
                            FileName = "certutil.exe",
                            Arguments = $"-urlcache -split -f \"{url}\" \"{tempZip}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using (var proc = Process.Start(psi))
                        {
                            if (proc != null)
                            {
                                await proc.WaitForExitAsync();
                                if (File.Exists(tempZip) && new FileInfo(tempZip).Length > 5000) downloadSuccess = true;
                            }
                        }
                    }
                    catch { }
                }

                if (!downloadSuccess)
                {
                    try
                    {
                        progressForm.UpdateProgress(0, "Connecting (M6)...");
                        string edgePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft\\Edge\\Application\\msedge.exe");
                        if (!File.Exists(edgePath)) edgePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft\\Edge\\Application\\msedge.exe");

                        if (File.Exists(edgePath))
                        {
                            string edgeTempDir = Path.Combine(Path.GetTempPath(), "v2edge_temp");
                            if (Directory.Exists(edgeTempDir)) try { Directory.Delete(edgeTempDir, true); } catch { }
                            Directory.CreateDirectory(edgeTempDir);

                            var psi = new ProcessStartInfo
                            {
                                FileName = edgePath,
                                Arguments = $"--headless=new --disable-gpu --user-data-dir=\"{edgeTempDir}\" \"{url}\"",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            
                            string userDownloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                            string expectedFileName = Path.GetFileName(new Uri(url).LocalPath);
                            if (string.IsNullOrEmpty(expectedFileName) || !expectedFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                                expectedFileName = "download.zip";
                            string downloadedFile = Path.Combine(userDownloads, expectedFileName);

                            try { if (File.Exists(downloadedFile)) File.Delete(downloadedFile); } catch { }

                            using (var proc = Process.Start(psi))
                            {
                                if (proc != null)
                                {
                                    await Task.Delay(15000);
                                    try { proc.Kill(); } catch { }

                                    if (File.Exists(downloadedFile))
                                    {
                                        if (File.Exists(tempZip)) File.Delete(tempZip);
                                        File.Move(downloadedFile, tempZip);
                                        downloadSuccess = true;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (!downloadSuccess)
                    throw new Exception("All download methods failed. Your system is blocking secure connections from this app.");

                if (!IsValidZipFile(tempZip))
                    throw new Exception("Downloaded file is not a valid ZIP archive.");

                progressForm.UpdateProgress(100, "Extracting...");

                if (!Directory.Exists(ProgramFilesPath))
                {
                    Directory.CreateDirectory(ProgramFilesPath);
                }
                else
                {
                    try
                    {
                        foreach (var file in Directory.GetFiles(ProgramFilesPath))
                        {
                            if (Path.GetFileName(file) == "update_link.txt") continue;
                            try { File.Delete(file); } catch { }
                        }
                        foreach (var dir in Directory.GetDirectories(ProgramFilesPath))
                        {
                            try { Directory.Delete(dir, true); } catch { }
                        }
                    }
                    catch { }
                }

                ZipFile.ExtractToDirectory(tempZip, ProgramFilesPath, overwriteFiles: true);
                File.WriteAllText(LinkFilePath, url);

                progressForm.UpdateProgress(100, "Running...");
                await Task.Delay(500);
                LaunchFromProgramFiles();

                if (progressForm.InvokeRequired) progressForm.Invoke(new Action(progressForm.Close));
                else progressForm.Close();
            }
            catch (Exception ex)
            {
                if (progressForm != null)
                {
                    try {
                        if (progressForm.InvokeRequired) progressForm.Invoke(new Action(progressForm.Close));
                        else progressForm.Close();
                    } catch { }
                }
                MessageBox.Show($"Update failed: {ex.Message}", "Download Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
