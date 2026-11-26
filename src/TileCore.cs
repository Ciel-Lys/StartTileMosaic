using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace TileMosaic
{
    public sealed class FrameCrop
    {
        public string Name="图片"; public float Zoom=1,PanX=.5f,PanY=.5f;
        public XElement Xml() { return new XElement("Frame",new XAttribute("Name",Name),new XAttribute("Zoom",Zoom),new XAttribute("PanX",PanX),new XAttribute("PanY",PanY)); }
    }
    public sealed class Project
    {
        public const string TileDisplayName = "z Custom Tile by TileGenie";
        internal const int MinimumCycleMinutes = 1, MaximumCycleMinutes = 24 * 60;
        public string Id = "";
        public string Name = "我的图片磁贴";
        public int Columns = 2, Rows = 3, Gap = 6;
        public bool Wide = true, ShowNames = false;
        public int[] ColumnWidths;
        public List<FrameCrop> Frames=new List<FrameCrop>();
        public int CycleMinutes=15;
        public string Website="";
        public bool IsWide(int column) { return ColumnWidths == null ? Wide : ColumnWidths[column] == 310; }
        public int WidthAt(int column) { return IsWide(column) ? 310 : 150; }
        public int LeftAt(int column) { int x=0; for(int i=0;i<column;i++)x+=WidthAt(i)+Gap; return x; }
        public float Zoom = 1, PanX = .5f, PanY = .5f;
        public int Revision = 0;
        public string DirectoryPath = "";
        public string PackageName { get { return "TileMosaic.Art." + Id; } }
        public int TileWidth { get { return Wide ? 310 : 150; } }
        public int TileHeight { get { return 150; } }
        public int Width { get { return LeftAt(Columns)-Gap; } }
        public int Height { get { return Rows * TileHeight + (Rows - 1) * Gap; } }
        public override string ToString() { return Name + "   ·   " + Columns + " × " + Rows + UiText.Get(Wide ? " 宽磁贴" : " 方形磁贴"); }
        public Project Copy() { var p=(Project)MemberwiseClone(); if(ColumnWidths!=null)p.ColumnWidths=(int[])ColumnWidths.Clone(); p.Frames=Frames.Select(f=>new FrameCrop{Name=f.Name,Zoom=f.Zoom,PanX=f.PanX,PanY=f.PanY}).ToList(); return p; }
        public void Validate()
        {
            if (Columns < 1 || Columns > 6 || Rows < 1 || Rows > 8 || Gap < 0 || Gap > 24)
                throw new InvalidDataException(UiText.Get("行列或间距超出支持范围。"));
            if(ColumnWidths!=null && (ColumnWidths.Length!=Columns || ColumnWidths.Any(w=>w!=150 && w!=310)))throw new InvalidDataException(UiText.Get("列宽无效。"));
            Uri url;
            if(Website.Length>0 && (!Uri.TryCreate(Website,UriKind.Absolute,out url) || (url.Scheme!="http" && url.Scheme!="https")))throw new InvalidDataException(UiText.Get("网址必须以 http:// 或 https:// 开头。"));
            if(CycleMinutes<MinimumCycleMinutes || CycleMinutes>MaximumCycleMinutes || Frames.Count>20)
                throw new InvalidDataException(UiText.Format("支持最多 20 张轮换图片，间隔为 {0} 至 {1} 分钟。", MinimumCycleMinutes, MaximumCycleMinutes));
            if(Frames.Any(f=>float.IsNaN(f.Zoom)||f.Zoom<1||f.Zoom>4||float.IsNaN(f.PanX)||float.IsNaN(f.PanY)||f.PanX<0||f.PanX>1||f.PanY<0||f.PanY>1))throw new InvalidDataException(UiText.Get("图片裁剪参数无效。"));
            if (Name.Trim().Length == 0 || Name.Length > 32) throw new InvalidDataException(UiText.Get("作品名称需要 1–32 个字符。"));
            if (float.IsNaN(Zoom) || float.IsInfinity(Zoom) || Zoom < 1 || Zoom > 4 ||
                float.IsNaN(PanX) || float.IsNaN(PanY) || PanX < 0 || PanX > 1 || PanY < 0 || PanY > 1)
                throw new InvalidDataException(UiText.Get("裁剪参数无效。"));
            Guid parsed;
            if (!Guid.TryParseExact(Id, "N", out parsed)) throw new InvalidDataException(UiText.Get("作品 ID 无效。"));
        }
        public void Save(string path)
        {
            new XDocument(new XElement("TileMosaicProject", new XAttribute("format", 1),
                new XElement("Id", Id), new XElement("Name", Name), new XElement("Columns", Columns),
                new XElement("Rows", Rows), new XElement("Wide", Wide), new XElement("Gap", Gap),
                new XElement("ColumnWidths",ColumnWidths==null?"":string.Join(",",ColumnWidths)),
                new XElement("Website",Website),new XElement("CycleMinutes",CycleMinutes),new XElement("Frames",Frames.Select(f=>f.Xml())),
                new XElement("ShowNames", ShowNames), new XElement("Zoom", Zoom),
                new XElement("PanX", PanX), new XElement("PanY", PanY), new XElement("Revision", Revision))).Save(path);
        }
        public static Project Load(string path)
        {
            XElement x = XDocument.Load(path).Root;
            if (x == null || x.Name != "TileMosaicProject") throw new InvalidDataException(UiText.Get("不是磁贴工坊作品。"));
            var p = new Project { Id = (string)x.Element("Id"), Name = (string)x.Element("Name"),
                Columns = (int)x.Element("Columns"), Rows = (int)x.Element("Rows"), Wide = (bool)x.Element("Wide"),
                Gap = (int)x.Element("Gap"), ShowNames = (bool)x.Element("ShowNames"), Zoom = (float)x.Element("Zoom"),
                PanX = (float)x.Element("PanX"), PanY = (float)x.Element("PanY"), Revision = (int)x.Element("Revision"),
                DirectoryPath = Path.GetDirectoryName(Path.GetFullPath(path)) };
            string widths=(string)x.Element("ColumnWidths");
            if(!string.IsNullOrWhiteSpace(widths))p.ColumnWidths=widths.Split(',').Select(int.Parse).ToArray();
            p.Website=(string)x.Element("Website")??"";p.CycleMinutes=(int?)x.Element("CycleMinutes")??15;
            if(x.Element("Frames")!=null)p.Frames=x.Element("Frames").Elements("Frame").Select(f=>new FrameCrop{Name=(string)f.Attribute("Name"),Zoom=(float)f.Attribute("Zoom"),PanX=(float)f.Attribute("PanX"),PanY=(float)f.Attribute("PanY")}).ToList();
            p.Validate(); return p;
        }
    }

    public static class Images
    {
        public static Bitmap Load(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var image = Image.FromStream(stream, true, true))
            {
                if ((long)image.Width * image.Height > 120000000) throw new InvalidDataException(UiText.Get("图片超过 1.2 亿像素，请先缩小图片。"));
                if (image.PropertyIdList.Contains(0x112))
                {
                    int orientation = BitConverter.ToUInt16(image.GetPropertyItem(0x112).Value, 0);
                    RotateFlipType[] transforms = { RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipNone,
                        RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate180FlipX,
                        RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate270FlipNone };
                    if (orientation > 0 && orientation < transforms.Length) image.RotateFlip(transforms[orientation]);
                }
                double factor = Math.Min(1.0, 8192.0 / Math.Max(image.Width, image.Height));
                var result = new Bitmap(Math.Max(1, (int)(image.Width * factor)), Math.Max(1, (int)(image.Height * factor)), PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(result)) { Quality(g); g.DrawImage(image, new Rectangle(0, 0, result.Width, result.Height)); }
                return result;
            }
        }
        public static void Quality(Graphics g)
        {
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }
        public static RectangleF Crop(Size image, Project p)
        {
            double scale = Math.Max((double)p.Width / image.Width, (double)p.Height / image.Height) * p.Zoom;
            float w = (float)(p.Width / scale), h = (float)(p.Height / scale);
            return new RectangleF(Math.Max(0, image.Width - w) * p.PanX, Math.Max(0, image.Height - h) * p.PanY, w, h);
        }
        public static RectangleF TileCrop(Size image, Project p, int row, int column)
        {
            RectangleF total = Crop(image, p);
            return new RectangleF(total.X + p.LeftAt(column) * total.Width / p.Width,
                total.Y + (row * (p.TileHeight + p.Gap)) * total.Height / p.Height,
                p.WidthAt(column) * total.Width / p.Width, p.TileHeight * total.Height / p.Height);
        }
        public static Bitmap Render(Image image, RectangleF source, int w, int h, bool cover)
        {
            if (cover)
            {
                float aspect = (float)w / h;
                if (source.Width / source.Height > aspect) { float width = source.Height * aspect; source.X += (source.Width - width) / 2; source.Width = width; }
                else { float height = source.Width / aspect; source.Y += (source.Height - height) / 2; source.Height = height; }
            }
            var result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result))
            using (var attributes = new ImageAttributes())
            {
                Quality(g); attributes.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(image, new Rectangle(0, 0, w, h), source.X, source.Y, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
            }
            return result;
        }
        public static void Save(Image source, RectangleF crop, int w, int h, string path, bool cover)
        { using (var b = Render(source, crop, w, h, cover)) b.Save(path, ImageFormat.Png); }
    }

    public static class PackageBuilder
    {
        public const string Publisher = "CN=TileMosaic.Local";
        static readonly XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        static readonly XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";

        static readonly XNamespace uap10 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/10";
        static readonly XNamespace rescap = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";

        public static string PrepareRegistration(Project project)
        {
            string directory = Path.Combine(project.DirectoryPath, "Package");
            string path = Path.Combine(directory, "AppxManifest.xml");
            var manifest = XDocument.Load(path);
            var identity = manifest.Root.Element(ns + "Identity");
            if (identity == null || (string)identity.Attribute("Name") != project.PackageName ||
                (string)identity.Attribute("Publisher") != Publisher)
                throw new InvalidDataException(UiText.Get("作品与磁贴包的标识不一致。"));

            if (!manifest.Descendants(ns + "Application").Any(app => app.Attribute("StartPage") != null))
                return path;

            ConfigureActivation(manifest.Root, project, directory);
            var version = Version.Parse((string)identity.Attribute("Version"));
            identity.SetAttributeValue("Version", new Version(version.Major, version.Minor,
                version.Build, version.Revision + 1).ToString());
            string staged = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                manifest.Save(staged);
                File.Replace(staged, path, null);
            }
            finally { if (File.Exists(staged)) File.Delete(staged); }
            return path;
        }

        static void ConfigureActivation(XElement package, Project project, string directory)
        {
            var applications = package.Element(ns + "Applications").Elements(ns + "Application").ToList();
            foreach (var app in applications)
            {
                // Keep the application IDs, so existing secondary tiles keep their placement.
                app.SetAttributeValue("StartPage", null);
                app.SetAttributeValue("Executable", TileActivation.Executable);
                app.SetAttributeValue("EntryPoint", "Windows.FullTrustApplication");
                var visual = app.Element(uap + "VisualElements");
                var tile = visual == null ? null : visual.Element(uap + "DefaultTile");
                if (tile == null) throw new InvalidDataException(UiText.Get("磁贴缺少显示配置。"));
                visual.SetAttributeValue("DisplayName", Project.TileDisplayName);
                visual.SetAttributeValue("Description", Project.TileDisplayName);
                visual.SetAttributeValue("AppListEntry", "none");
                tile.SetAttributeValue("ShortName", Project.TileDisplayName);
            }

            if (project.Frames.Count > 1)
            {
                var owner = applications.First(app => (string)app.Attribute("Id") == "Tile001");
                var extensions = owner.Element(ns + "Extensions");
                if (extensions == null)
                {
                    extensions = new XElement(ns + "Extensions");
                    owner.Add(extensions);
                }
                extensions.Elements().Where(extension => (string)extension.Attribute("Category") == "windows.backgroundTasks" ||
                    (string)extension.Attribute("Category") == "windows.appService").Remove();
                // JS rotation still runs under the original owner, in a separate background host.
                extensions.Add(
                    new XElement(ns + "Extension", new XAttribute("Category", "windows.backgroundTasks"),
                        new XAttribute("StartPage", "background.js"),
                        new XAttribute(uap10 + "RuntimeBehavior", "windowsApp"),
                        new XAttribute(uap10 + "TrustLevel", "appContainer"),
                        new XElement(ns + "BackgroundTasks", new XElement(ns + "Task", new XAttribute("Type", "timer")))),
                    new XElement(uap + "Extension", new XAttribute("Category", "windows.appService"),
                        new XAttribute("StartPage", "rotation-service.js"),
                        new XAttribute(uap10 + "RuntimeBehavior", "windowsApp"),
                        new XAttribute(uap10 + "TrustLevel", "appContainer"),
                        new XElement(uap + "AppService", new XAttribute("Name", TileRuntime.RotationServiceName))));
                TileRuntime.WriteBackgroundFiles(directory, project);
            }

            package.SetAttributeValue(XNamespace.Xmlns + "uap10", uap10);
            package.SetAttributeValue(XNamespace.Xmlns + "rescap", rescap);
            package.SetAttributeValue("IgnorableNamespaces", "uap uap10 rescap");
            var capabilities = package.Element(ns + "Capabilities");
            if (capabilities == null)
            {
                capabilities = new XElement(ns + "Capabilities");
                package.Add(capabilities);
            }
            if (!capabilities.Elements(rescap + "Capability").Any(cap => (string)cap.Attribute("Name") == "runFullTrust"))
                capabilities.Add(new XElement(rescap + "Capability", new XAttribute("Name", "runFullTrust")));
            TileActivation.Write(directory, project);
        }

        // Build into a new staging directory. Existing installed assets are untouched until an explicit update.
        public static Project Generate(Project settings, Bitmap source, string root)
        { return Generate(settings,new List<Bitmap>{source},root); }
        public static Project Generate(Project settings, IList<Bitmap> sources, string root)
        {
            if(sources==null || sources.Count==0)throw new InvalidDataException(UiText.Get("请先添加图片。"));
            Bitmap source=sources[0];
            Project p = settings.Copy();
            if(p.Frames.Count==0)p.Frames.Add(new FrameCrop{Name=p.Name,Zoom=p.Zoom,PanX=p.PanX,PanY=p.PanY});
            if(p.Frames.Count!=sources.Count)throw new InvalidDataException(UiText.Get("图片与裁剪参数数量不一致。"));
            p.Id = Guid.NewGuid().ToString("N"); p.Revision = 1; p.Validate();
            Directory.CreateDirectory(root);
            string final = Path.Combine(Path.GetFullPath(root), p.Id);
            string stage = final + ".building";
            Directory.CreateDirectory(stage);
            try
            {
                p.DirectoryPath = final;
                source.Save(Path.Combine(stage, "source.png"), ImageFormat.Png);
                for(int frame=1;frame<sources.Count;frame++)sources[frame].Save(Path.Combine(stage,"source"+frame.ToString("D3")+".png"),ImageFormat.Png);
                string payload = Path.Combine(stage, "Package");
                string assets = Path.Combine(payload, "Assets"); Directory.CreateDirectory(assets);
                p.Zoom=p.Frames[0].Zoom;p.PanX=p.Frames[0].PanX;p.PanY=p.Frames[0].PanY;
                RectangleF total = Images.Crop(source.Size, p);
                Images.Save(source, total, 50, 50, Path.Combine(assets, "Store.png"), true);
                var applications = new XElement(ns + "Applications");
                for (int row = 0; row < p.Rows; row++) for (int col = 0; col < p.Columns; col++)
                {
                    string id = "Tile" + (row * p.Columns + col + 1).ToString("D3");
                    RectangleF crop = Images.TileCrop(source.Size, p, row, col);
                    foreach (int size in new[] { 44, 71, 150, 310 })
                        Images.Save(source, crop, size, size, Path.Combine(assets, id + "-" + size + ".png"), true);
                    if (p.IsWide(col)) Images.Save(source, crop, 310, 150, Path.Combine(assets, id + "-Wide.png"), false);
                    var tile = new XElement(uap + "DefaultTile", new XAttribute("ShortName", Project.TileDisplayName), new XAttribute("Square71x71Logo", "Assets\\" + id + "-71.png"));
                    if (p.IsWide(col)) tile.Add(new XAttribute("Wide310x150Logo", "Assets\\" + id + "-Wide.png"),new XAttribute("Square310x310Logo", "Assets\\" + id + "-310.png"));
                    if(p.ShowNames)
                    {
                        var show=new XElement(uap+"ShowNameOnTiles",new XElement(uap+"ShowOn",new XAttribute("Tile","square150x150Logo")));
                        if(p.IsWide(col))show.Add(new XElement(uap+"ShowOn",new XAttribute("Tile","wide310x150Logo")),new XElement(uap+"ShowOn",new XAttribute("Tile","square310x310Logo")));
                        tile.Add(show);
                    }
                    applications.Add(new XElement(ns + "Application", new XAttribute("Id", id),
                        new XElement(uap + "VisualElements", new XAttribute("DisplayName", Project.TileDisplayName), new XAttribute("Description", Project.TileDisplayName), new XAttribute("AppListEntry", "none"),
                            new XAttribute("BackgroundColor", "transparent"), new XAttribute("Square150x150Logo", "Assets\\" + id + "-150.png"),
                            new XAttribute("Square44x44Logo", "Assets\\" + id + "-44.png"), tile)));
                }
                for(int frame=0;frame<sources.Count;frame++)
                {
                    Project fp=p.Copy();fp.Zoom=p.Frames[frame].Zoom;fp.PanX=p.Frames[frame].PanX;fp.PanY=p.Frames[frame].PanY;
                    for(int row=0;row<p.Rows;row++)for(int col=0;col<p.Columns;col++)
                    {
                        string id="F"+frame.ToString("D3")+"-Tile"+(row*p.Columns+col+1).ToString("D3");
                        var crop=Images.TileCrop(sources[frame].Size,fp,row,col);
                        foreach(int size in new[]{150,310})Images.Save(sources[frame],crop,size,size,Path.Combine(assets,id+"-"+size+".png"),true);
                        if(p.IsWide(col))Images.Save(sources[frame],crop,310,150,Path.Combine(assets,id+"-Wide.png"),false);
                    }
                }
                var manifest = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(ns + "Package", new XAttribute(XNamespace.Xmlns + "uap", uap),
                    new XAttribute("IgnorableNamespaces", "uap"), new XElement(ns + "Identity", new XAttribute("Name", p.PackageName),
                        new XAttribute("Publisher", Publisher), new XAttribute("Version", "1.0.0." + p.Revision), new XAttribute("ProcessorArchitecture", "neutral")),
                    new XElement(ns + "Properties", new XElement(ns + "DisplayName", p.Name), new XElement(ns + "PublisherDisplayName", "磁贴工坊（本地作品）"), new XElement(ns + "Logo", "Assets\\Store.png")),
                    new XElement(ns + "Dependencies", new XElement(ns + "TargetDeviceFamily", new XAttribute("Name", "Windows.Universal"), new XAttribute("MinVersion", "10.0.19041.0"), new XAttribute("MaxVersionTested", "10.0.26100.0"))),
                    new XElement(ns + "Resources", new XElement(ns + "Resource", new XAttribute("Language", UiText.English ? "en-US" : "zh-CN"))), applications));
                ConfigureActivation(manifest.Root, p, payload);
                manifest.Save(Path.Combine(payload, "AppxManifest.xml"));
                p.Save(Path.Combine(stage, "project.xml"));
                File.WriteAllText(Path.Combine(stage, "使用说明.txt"),
                    "这些文件由 TileGenie 生成。Package 文件夹供 Windows 显示磁贴，请保留其位置。\r\n如需取消固定，请在开始菜单中右键点击对应磁贴。", System.Text.Encoding.UTF8);
                Directory.Move(stage, final); return p;
            }
            catch
            {
                // Only the unique directory created by this invocation is eligible for rollback.
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
                throw;
            }
        }
    }
}
