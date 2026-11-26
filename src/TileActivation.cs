using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace TileMosaic
{
    internal static class TileActivation
    {
        internal const string Executable = "PictureTile.exe";
        const string Configuration = "tile-launch.xml";

        internal static bool IsTileLauncher
        {
            get { return string.Equals(Path.GetFileName(Assembly.GetExecutingAssembly().Location),
                Executable, StringComparison.OrdinalIgnoreCase); }
        }

        internal static void Write(string directory, Project project)
        {
            var editor = Assembly.GetExecutingAssembly().Location;
            var launcher = Path.Combine(directory, Executable);
            if (!File.Exists(launcher)) File.Copy(editor, launcher);
            new XElement("TileLaunch",
                new XElement("Editor", editor),
                new XElement("Website", project.Website),
                new XElement("RotationFamily", project.Frames.Count > 1 ? project.PackageName + "_xybhvjrwh6328" : ""))
                .Save(Path.Combine(directory, Configuration));
        }

        internal static int Open()
        {
            try
            {
                var config = XElement.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Configuration));
                string website = (string)config.Element("Website");
                string editor = (string)config.Element("Editor");
                if (!string.IsNullOrEmpty(website))
                {
                    Uri uri;
                    if (!Uri.TryCreate(website, UriKind.Absolute, out uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return 1;
                    Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                }
                else
                {
                    if (string.IsNullOrEmpty(editor) || !Path.IsPathRooted(editor) ||
                        !string.Equals(Path.GetFileName(editor), "TileGenie.exe", StringComparison.OrdinalIgnoreCase)) return 1;
                    Process.Start(new ProcessStartInfo(editor) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(editor) });
                }

                // The normal timer runs on its own. A click can renew a queue
                // if the system previously denied background execution.
                string family = (string)config.Element("RotationFamily");
                if (!string.IsNullOrEmpty(family))
                {
                    try { ShellIntegration.InitializeRotation(family, true).GetAwaiter().GetResult(); }
                    catch (Exception) { }
                }
                return 0;
            }
            catch (Exception) { return 1; }
        }
    }
}
