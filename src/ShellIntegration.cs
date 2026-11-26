using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TileMosaic
{
    public static class ShellIntegration
    {
        public static Task<bool> InitializeRotation(string family) { return InitializeRotation(family, false); }

        internal static async Task<bool> InitializeRotation(string family, bool renewOnly)
        {
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
            using (var connection = new AppServiceConnection())
            {
                connection.PackageFamilyName = family;
                connection.AppServiceName = TileRuntime.RotationServiceName;
                var status = await WaitForOperation(connection.OpenAsync(), timeout.Token);
                if (status != AppServiceConnectionStatus.Success)
                    throw new InvalidOperationException("Rotation service: " + status);
                var request = new ValueSet();
                if (renewOnly) request.Add("renewOnly", true);
                var response = await WaitForOperation(connection.SendMessageAsync(request), timeout.Token);
                if (response.Status != AppServiceResponseStatus.Success)
                    throw new InvalidOperationException("Rotation service: " + response.Status);
                var message = response.Message;
                object state, error;
                if (message.TryGetValue("state", out state))
                {
                    if (Equals(state, "ready")) return true;
                    if (Equals(state, "scheduled")) return false;
                }
                throw new InvalidOperationException(message.TryGetValue("error", out error)
                    ? Convert.ToString(error) : "Invalid rotation service response.");
            }
        }
        static async Task<T> WaitForOperation<T>(IAsyncOperation<T> operation, CancellationToken timeout)
        {
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (timeout.Register(delegate { completion.TrySetCanceled(); }))
            {
                operation.Completed = delegate(IAsyncOperation<T> finished, AsyncStatus status) {
                    if (status == AsyncStatus.Canceled) { completion.TrySetCanceled(); return; }
                    try { completion.TrySetResult(finished.GetResults()); }
                    catch (Exception ex) { completion.TrySetException(ex); }
                };
                return await completion.Task;
            }
        }
        static string Quote(string s) { return "'" + s.Replace("'", "''") + "'"; }
        internal static string PowerShellPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"); }
        }
        public static async Task<string> PowerShell(string script)
        {
            string wrapped = "$ErrorActionPreference='Stop'; [Console]::OutputEncoding=New-Object System.Text.UTF8Encoding; try { " + script + " } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }";
            var info = new ProcessStartInfo(PowerShellPath, "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(wrapped))) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            using (var process = new Process { StartInfo = info })
            {
                process.Start(); Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());
                string result = await output, errors = await error;
                if (process.ExitCode != 0) throw new InvalidOperationException(errors.Trim().Length > 0 ? errors.Trim() : result.Trim());
                return result.Trim();
            }
        }
        public static async Task<string> Install(Project p, X509Certificate2 signer)
        {
            p.Validate();
            if (!LocalSigning.IsTrusted(signer))
                throw new InvalidOperationException(UiText.Get("无法信任本机签名证书。请检查管理员权限。"));
            string manifest = PackageBuilder.PrepareRegistration(p);
            string signedPackage = Path.Combine(p.DirectoryPath, Guid.NewGuid().ToString("N") + ".msix");
            try
            {
                await Task.Run(delegate {
                    MsixPackage.Pack(manifest, signedPackage);
                    MsixPackage.Sign(signedPackage, signer);
                });
                return await PowerShell("Add-AppxPackage -Path " + Quote(signedPackage) +
                    " -ErrorAction Stop; $p=Get-AppxPackage -Name " + Quote(p.PackageName) +
                    "; if (!$p) { throw " + Quote(UiText.Get("安装后的包不存在。")) + " }; $p.PackageFamilyName");
            }
            finally
            {
                try { File.Delete(signedPackage); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        public static Task<string> Family(Project p)
        { p.Validate(); return PowerShell("(Get-AppxPackage -Name " + Quote(p.PackageName) + ").PackageFamilyName"); }
        public static Task<string> Uninstall(Project p)
        {
            p.Validate();
            // All generated packages use the same manifest publisher (CN=TileMosaic.Local).
            // Its publisher ID is stable even if Windows already removed the package.
            StartPinning.RemovePins(p, p.PackageName + "_xybhvjrwh6328");
            return PowerShell("Get-AppxPackage -Name " + Quote(p.PackageName) + " | ForEach-Object { Remove-AppxPackage -Package $_.PackageFullName -ErrorAction Stop }");
        }
        public static void Open(string path) { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        public static void ShowApp(string family)
        {
            // AppsFolder contains the real app entries and exposes ExplorerPatcher's pin command.
            Process.Start(new ProcessStartInfo("explorer.exe", "shell:AppsFolder") { UseShellExecute = true });
        }
        public static int RequestPin(Project p, string family)
        {
            if (string.IsNullOrWhiteSpace(family)) throw new InvalidOperationException("请先安装此作品。");
            object shell = null, folder = null; int submitted = 0;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                dynamic sh = shell; folder = sh.NameSpace("shell:AppsFolder"); dynamic f = folder;
                for (int i = 1; i <= p.Rows * p.Columns; i++)
                {
                    object item = null, verbs = null;
                    try
                    {
                        item = f.ParseName(family + "!Tile" + i.ToString("D3")); if (item == null) continue;
                        dynamic it = item; verbs = it.Verbs(); dynamic list = verbs;
                        for (int j = 0; j < list.Count; j++)
                        {
                            object verb = list.Item(j);
                            try
                            {
                                dynamic v = verb; string name = ((string)v.Name).Replace("&", "").Trim();
                                bool isPin = (name.Contains("固定到") && (name.Contains("开始") || name.Contains("Start"))) ||
                                    name.Equals("Pin to Start", StringComparison.OrdinalIgnoreCase) || name.Equals("Pin to Start Menu", StringComparison.OrdinalIgnoreCase);
                                if (isPin && !name.Contains("取消") && !name.StartsWith("Unpin", StringComparison.OrdinalIgnoreCase)) { v.DoIt(); submitted++; break; }
                            }
                            finally { if (verb != null) Marshal.FinalReleaseComObject(verb); }
                        }
                    }
                    finally { if (verbs != null) Marshal.FinalReleaseComObject(verbs); if (item != null) Marshal.FinalReleaseComObject(item); }
                }
            }
            finally { if (folder != null) Marshal.FinalReleaseComObject(folder); if (shell != null) Marshal.FinalReleaseComObject(shell); }
            return submitted;
        }
    }
}
