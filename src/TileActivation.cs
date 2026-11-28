using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            if (!File.Exists(launcher))
            {
                File.Copy(editor, launcher);
                var attributes = File.GetAttributes(launcher);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(launcher, attributes & ~FileAttributes.ReadOnly);
            }
            new XElement("TileLaunch",
                new XElement("Editor", editor),
                project.DefaultBinding == null ? null : project.DefaultBinding.Xml("DefaultBinding"),
                new XElement("TileBindings", Enumerable.Range(0, project.TileCount).Select(index =>
                {
                    var binding = project.BindingOverride(project.TileKeyAt(index));
                    if (binding == null) return null;
                    var tile = binding.Xml("Tile");
                    tile.SetAttributeValue("Id", Project.PictureId(index + 1));
                    return tile;
                })),
                new XElement("RotationFamily", project.Frames.Count > 1 ? project.PackageName + "_xybhvjrwh6328" : ""))
                .Save(Path.Combine(directory, Configuration));
        }

        internal static int Open(string[] args)
        {
            try
            {
                var config = XElement.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Configuration));
                var overrides = config.Element("TileBindings") ?? config.Element("TileWebsites");
                var selected = overrides != null && args.Length > 0
                    ? overrides.Elements("Tile").FirstOrDefault(tile => (string)tile.Attribute("Id") == args[0]) : null;
                var binding = TileBinding.Load(selected ?? config.Element("DefaultBinding") ?? config.Element("Website"));
                if (binding != null && binding.Kind == TileBindingKind.Website)
                {
                    Uri uri;
                    if (!Project.TryWebsite(binding.Target, out uri)) return 1;
                    Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                }
                else
                {
                    string path;
                    if (!TileBinding.TryFilePath(binding == null ? (string)config.Element("Editor") : binding.Target, out path) ||
                        !File.Exists(path)) return 1;
                    if (binding == null && !string.Equals(Path.GetFileName(path), "TileGenie.exe", StringComparison.OrdinalIgnoreCase))
                        return 1;
                    using (Process.Start(new ProcessStartInfo(path) {
                        UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path)
                    })) { }
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
