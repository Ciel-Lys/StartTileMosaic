using System;
using System.IO;
using System.Xml.Linq;

namespace TileMosaic
{
    public enum TileBindingKind { Website, File }

    public sealed class TileBinding
    {
        public TileBinding(TileBindingKind kind, string target) { Kind = kind; Target = target; }
        public TileBindingKind Kind { get; private set; }
        public string Target { get; private set; }

        internal static TileBinding Website(string value)
        {
            return string.IsNullOrEmpty(value) ? null : new TileBinding(TileBindingKind.Website, value);
        }

        internal static bool TryFilePath(string value, out string path)
        {
            path = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            try
            {
                // A saved binding must resolve to the same path from any working directory.
                var root = Path.GetPathRoot(value);
                if (string.IsNullOrEmpty(root) || root.Length <= 2) return false;
                path = Path.GetFullPath(value);
                return true;
            }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
            catch (PathTooLongException) { return false; }
        }

        internal void Validate(string websiteError)
        {
            if (Kind == TileBindingKind.Website)
            {
                Uri uri;
                if (!Project.TryWebsite(Target, out uri)) throw new InvalidDataException(websiteError);
            }
            else
            {
                string path;
                if (Kind != TileBindingKind.File || !TryFilePath(Target, out path))
                    throw new InvalidDataException(UiText.Get("请选择有效的文件。"));
            }
        }

        internal XElement Xml(string name)
        {
            return new XElement(name, new XAttribute("Kind", Kind), Target);
        }

        internal static TileBinding Load(XElement element)
        {
            if (element == null) return null;
            string kind = (string)element.Attribute("Kind");
            if (kind == null) return Website(element.Value);
            if (kind == "Executable") return new TileBinding(TileBindingKind.File, element.Value);
            TileBindingKind parsed;
            if (!Enum.TryParse(kind, out parsed) || !Enum.IsDefined(typeof(TileBindingKind), parsed))
                throw new InvalidDataException(UiText.Get("磁贴绑定类型无效。"));
            return new TileBinding(parsed, element.Value);
        }
    }
}
