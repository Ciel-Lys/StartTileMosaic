using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography.X509Certificates;

namespace TileMosaic
{
    // Windows Packaging API and SignerSignEx2. No SDK tools are needed at runtime.
    internal static class MsixPackage
    {
        [StructLayout(LayoutKind.Sequential)]
        struct PackageSettings
        {
            [MarshalAs(UnmanagedType.Bool)] public bool ForceZip32;
            public IntPtr HashMethod;
        }

        [ComImport, Guid("beb94909-e451-438b-b5a7-d79e767b75d8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAppxFactory
        {
            IAppxPackageWriter CreatePackageWriter(IStream output, ref PackageSettings settings);
        }

        [ComImport, Guid("9099e33b-246f-41e4-881a-008eb613f858"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAppxPackageWriter
        {
            void AddPayloadFile([MarshalAs(UnmanagedType.LPWStr)] string name,
                [MarshalAs(UnmanagedType.LPWStr)] string contentType, int compression, IStream stream);
            void Close(IStream manifest);
        }

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int SHCreateStreamOnFileEx(string path, uint mode, uint attributes,
            [MarshalAs(UnmanagedType.Bool)] bool create, IStream template, out IStream stream);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("urlmon.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int CreateUri(string uri, uint flags, IntPtr reserved, out IntPtr result);

        internal static void Pack(string manifest, string output)
        {
            string directory = Path.GetDirectoryName(manifest);
            IntPtr hashMethod = IntPtr.Zero;
            IAppxFactory factory = null;
            IAppxPackageWriter writer = null;
            IStream destination = null;
            try
            {
                Marshal.ThrowExceptionForHR(CreateUri("http://www.w3.org/2001/04/xmlenc#sha256", 0, IntPtr.Zero, out hashMethod));
                var settings = new PackageSettings { HashMethod = hashMethod, ForceZip32 = true };
                var factoryClass = Type.GetTypeFromCLSID(new Guid("5842a140-ff9f-4166-8f5c-62f5b7b0c781"), true);
                factory = (IAppxFactory)Activator.CreateInstance(factoryClass);
                destination = OpenFile(output, 0x1011, true); // STGM_CREATE | STGM_WRITE | STGM_SHARE_EXCLUSIVE
                writer = factory.CreatePackageWriter(destination, ref settings);
                foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    if (string.Equals(file, manifest, StringComparison.OrdinalIgnoreCase)) continue;
                    string name = file.Substring(directory.Length + 1);
                    IStream input = OpenFile(file, 0x20, false); // STGM_READ | STGM_SHARE_DENY_WRITE
                    try { writer.AddPayloadFile(name, ContentType(file), 1, input); }
                    finally { Release(input); }
                }
                IStream manifestStream = OpenFile(manifest, 0x20, false);
                try { writer.Close(manifestStream); }
                finally { Release(manifestStream); }
            }
            finally
            {
                Release(writer);
                Release(destination);
                Release(factory);
                if (hashMethod != IntPtr.Zero) Marshal.Release(hashMethod);
            }
        }

        static IStream OpenFile(string path, uint mode, bool create)
        {
            IStream stream;
            Marshal.ThrowExceptionForHR(SHCreateStreamOnFileEx(path, mode, 0, create, null, out stream));
            return stream;
        }

        static void Release(object value) { if (value != null) Marshal.ReleaseComObject(value); }

        static string ContentType(string file)
        {
            switch (Path.GetExtension(file).ToLowerInvariant())
            {
                case ".png": return "image/png";
                case ".js": return "application/javascript";
                case ".xml": return "application/xml";
                default: return "application/octet-stream";
            }
        }

        // ABI layouts from Microsoft's "How to programmatically sign an app package".
        [StructLayout(LayoutKind.Sequential)]
        struct SignerFile { public int Size; public IntPtr FileName, Handle; }
        [StructLayout(LayoutKind.Sequential)]
        struct SignerSubject { public int Size; public IntPtr Index; public int Choice; public IntPtr File; }
        [StructLayout(LayoutKind.Sequential)]
        struct SignerStore { public int Size; public IntPtr Certificate; public int Policy; public IntPtr Store; }
        [StructLayout(LayoutKind.Sequential)]
        struct SignerCertificate { public int Size, Choice; public IntPtr Store, Window; }
        [StructLayout(LayoutKind.Sequential)]
        struct SignerSignature
        {
            public int Size, HashAlgorithm, AttributeChoice;
            public IntPtr Authenticode, Authenticated, Unauthenticated;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct SignerParameters
        {
            public int Flags;
            public IntPtr Subject, Certificate, Signature, Provider;
            public int TimestampFlags;
            public IntPtr TimestampAlgorithm, TimestampUri, Attributes, SipData, Context, CryptoPolicy, Reserved;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct PackageSipData { public IntPtr Parameters, State; }

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("mssign32.dll", ExactSpelling = true)]
        static extern int SignerSignEx2(int flags, IntPtr subject, IntPtr certificate, IntPtr signature,
            IntPtr provider, int timestampFlags, IntPtr timestampAlgorithm, IntPtr timestampUri,
            IntPtr attributes, IntPtr sipData, IntPtr context, IntPtr cryptoPolicy, IntPtr reserved);

        internal static void Sign(string path, X509Certificate2 certificate)
        {
            int result;
            using (var memory = new NativeMemory())
            {
                var file = new SignerFile { Size = Marshal.SizeOf(typeof(SignerFile)), FileName = memory.String(path) };
                var subject = new SignerSubject {
                    Size = Marshal.SizeOf(typeof(SignerSubject)), Index = memory.Value(0), Choice = 1,
                    File = memory.Value(file)
                };
                var store = new SignerStore {
                    Size = Marshal.SizeOf(typeof(SignerStore)), Certificate = certificate.Handle, Policy = 8
                };
                var signingCertificate = new SignerCertificate {
                    Size = Marshal.SizeOf(typeof(SignerCertificate)), Choice = 2, Store = memory.Value(store)
                };
                var signature = new SignerSignature {
                    Size = Marshal.SizeOf(typeof(SignerSignature)), HashAlgorithm = 0x800c // CALG_SHA_256
                };
                var args = new SignerParameters {
                    Subject = memory.Value(subject), Certificate = memory.Value(signingCertificate),
                    Signature = memory.Value(signature), SipData = memory.Value(new PackageSipData())
                };
                var sipData = new PackageSipData { Parameters = memory.Value(args) };
                Marshal.StructureToPtr(sipData, args.SipData, false);

                result = SignerSignEx2(args.Flags, args.Subject, args.Certificate, args.Signature, args.Provider,
                    args.TimestampFlags, args.TimestampAlgorithm, args.TimestampUri, args.Attributes, args.SipData,
                    args.Context, args.CryptoPolicy, args.Reserved);
                sipData = (PackageSipData)Marshal.PtrToStructure(args.SipData, typeof(PackageSipData));
                if (sipData.State != IntPtr.Zero) Marshal.Release(sipData.State);
                GC.KeepAlive(certificate);
            }
            Marshal.ThrowExceptionForHR(result);
        }

        sealed class NativeMemory : IDisposable
        {
            readonly List<IntPtr> blocks = new List<IntPtr>();

            public IntPtr Value<T>(T value) where T : struct
            {
                var address = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(T)));
                blocks.Add(address);
                Marshal.StructureToPtr(value, address, false);
                return address;
            }

            public IntPtr String(string value)
            {
                var address = Marshal.StringToHGlobalUni(value);
                blocks.Add(address);
                return address;
            }

            public void Dispose() { foreach (var address in blocks) Marshal.FreeHGlobal(address); }
        }
    }
}
