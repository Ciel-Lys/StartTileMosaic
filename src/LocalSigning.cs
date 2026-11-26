using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace TileMosaic
{
    internal static class LocalSigning
    {
        const string FriendlyName = "StartTileMosaic local package signing";

        internal static async Task<X509Certificate2> GetOrCreate()
        {
            string result = await ShellIntegration.PowerShell(
                "$cert=Get-ChildItem Cert:\\CurrentUser\\My | Where-Object {" +
                " $_.Subject -eq '" + PackageBuilder.Publisher + "' -and $_.Issuer -eq $_.Subject" +
                " -and $_.HasPrivateKey -and $_.FriendlyName -eq '" + FriendlyName + "'" +
                " -and $_.NotAfter -gt (Get-Date).AddDays(7) -and $_.NotBefore -le (Get-Date)" +
                " -and $_.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3' }" +
                " | Sort-Object NotAfter -Descending | Select-Object -First 1;" +
                " if (!$cert) { $cert=New-SelfSignedCertificate -Type Custom -Subject '" + PackageBuilder.Publisher + "'" +
                " -FriendlyName '" + FriendlyName + "' -CertStoreLocation 'Cert:\\CurrentUser\\My'" +
                " -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -KeyExportPolicy NonExportable" +
                " -KeyUsage DigitalSignature -NotAfter (Get-Date).AddYears(3)" +
                " -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3','2.5.29.19={critical}{text}ca=false') };" +
                " $cert.Thumbprint");
            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                var matches = store.Certificates.Find(X509FindType.FindByThumbprint, result, false);
                if (matches.Count == 1 && matches[0].HasPrivateKey) return matches[0];
                throw new InvalidOperationException(UiText.Get("无法读取本机签名证书。"));
            }
        }

        internal static bool IsTrusted(X509Certificate2 certificate)
        {
            using (var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadOnly);
                return store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false).Count > 0;
            }
        }

        // Called only after the editor's explicit confirmation and the Windows UAC prompt.
        internal static async Task<bool> Trust(X509Certificate2 certificate)
        {
            // Send just the public certificate to the elevated process. It reads no user-writable script.
            string data = Convert.ToBase64String(certificate.Export(X509ContentType.Cert));
            string script =
                "$ErrorActionPreference='Stop'; $cert=New-Object System.Security.Cryptography.X509Certificates.X509Certificate2;" +
                " $cert.Import([Convert]::FromBase64String('" + data + "'));" +
                " $store=New-Object System.Security.Cryptography.X509Certificates.X509Store('TrustedPeople','LocalMachine');" +
                " try { $store.Open('ReadWrite'); $store.Add($cert); } finally { $store.Close(); $cert.Dispose(); }";
            var info = new ProcessStartInfo(ShellIntegration.PowerShellPath,
                "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script))) {
                UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System)
            };
            try
            {
                using (var process = Process.Start(info))
                {
                    await Task.Run(() => process.WaitForExit());
                    if (process.ExitCode != 0 || !IsTrusted(certificate))
                        throw new InvalidOperationException(UiText.Get("无法信任本机签名证书。请检查管理员权限。"));
                    return true;
                }
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223) return false;
                throw;
            }
        }
    }
}
