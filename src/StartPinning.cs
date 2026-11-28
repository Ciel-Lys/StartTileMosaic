using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.UI.StartScreen;

namespace TileMosaic
{
    // ABI contracts published by ExplorerPatcher/ep_starttiledata (UnifiedTile.idl).
    // No registry changes, injection or replacement of the user's layout.
    public static class StartPinning
    {
        const string ManagerName="WindowsInternal.Shell.UnifiedTile.CuratedTileCollections.CuratedTileCollectionManager";
        const string IdentifierName="WindowsInternal.Shell.UnifiedTile.PackagedUnifiedTileIdentifier";
        static readonly Guid ManagerId=new Guid("899ee71b-5c01-438f-b12e-61d49f6b4083");
        static readonly Guid CollectionId=new Guid("51a07090-3a1f-49ef-9932-a971b8154790");
        static readonly Guid StartId=new Guid("adbf8965-6056-4126-ab26-6660af4661ce");
        static readonly Guid IdentifierFactoryId=new Guid("ec3e7864-aaab-4367-9c63-94d289545500");
        [DllImport("combase.dll")] static extern int RoInitialize(int type);
        [DllImport("combase.dll")] static extern void RoUninitialize();
        [DllImport("combase.dll",CharSet=CharSet.Unicode)] static extern int WindowsCreateString(string s,int n,out IntPtr p);
        [DllImport("combase.dll")] static extern int WindowsDeleteString(IntPtr p);
        [DllImport("combase.dll")] static extern int RoActivateInstance(IntPtr name,out IntPtr p);
        [DllImport("combase.dll")] static extern int RoGetActivationFactory(IntPtr name,ref Guid iid,out IntPtr p);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int StringObject(IntPtr self,IntPtr text,out IntPtr obj);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Contains(IntPtr self,IntPtr id,out IntPtr tile,out byte present);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Pin(IntPtr self,IntPtr id,int size);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int PinAtLocation(IntPtr self, IntPtr id, IntPtr group, TilePosition position, TileSize size);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Unpin(IntPtr self,IntPtr id);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetInt(IntPtr self,out int value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int NoArgs(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int ObjectOut(IntPtr self,out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetSize(IntPtr self,out TileSize value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetPosition(IntPtr self, out TilePosition value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetPosition(IntPtr self, TilePosition value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetGuid(IntPtr self, out Guid value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int DeleteGroup(IntPtr self, Guid value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int TwoStrings(IntPtr self,IntPtr app,IntPtr tileId,out IntPtr value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetOwner(IntPtr self,IntPtr app);
        [StructLayout(LayoutKind.Sequential)] public struct TileSize { public float Width,Height; }
        [StructLayout(LayoutKind.Sequential)] struct TilePosition { public float X,Y; }
        static T Method<T>(IntPtr obj,int slot) where T:class { return Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj),slot*IntPtr.Size),typeof(T)) as T; }
        static void Check(int hr) { if(hr<0)Marshal.ThrowExceptionForHR(hr); }
        static IntPtr HString(string text) { IntPtr result;Check(WindowsCreateString(text,text.Length,out result));return result; }
        static IntPtr Query(IntPtr source,Guid iid) { IntPtr p;Check(Marshal.QueryInterface(source,ref iid,out p));return p; }
        static void Release(IntPtr p) { if(p!=IntPtr.Zero)Marshal.Release(p); }
        static int InitializeApartment()
        {
            int result = RoInitialize(0);
            if (result != unchecked((int)0x80010106)) Check(result);
            return result;
        }

        static IntPtr OpenCollection()
        {
            IntPtr instance = IntPtr.Zero, manager = IntPtr.Zero, collection = IntPtr.Zero;
            IntPtr text = HString(ManagerName);
            try
            {
                Check(RoActivateInstance(text, out instance));
                manager = Query(instance, ManagerId);
                IntPtr name = HString("Start.TileGrid");
                try { Check(Method<StringObject>(manager, 7)(manager, name, out collection)); }
                finally { WindowsDeleteString(name); }
                return Query(collection, CollectionId);
            }
            finally
            {
                WindowsDeleteString(text);
                Release(collection); Release(manager); Release(instance);
            }
        }

        static int GroupCellWidth(IntPtr start)
        {
            int cells;
            Check(Method<GetInt>(start, 14)(start, out cells));
            return cells > 0 ? cells : 6;
        }

        sealed class PictureGroup
        {
            public IntPtr Handle;
            public Guid Id;
        }

        static IntPtr GetPictureGroup(IntPtr collection, Dictionary<int, PictureGroup> groups, int index)
        {
            PictureGroup group;
            if (!groups.TryGetValue(index, out group))
            {
                group = new PictureGroup();
                groups.Add(index, group);
                Check(Method<ObjectOut>(collection, 17)(collection, out group.Handle));
                Check(Method<GetGuid>(group.Handle, 6)(group.Handle, out group.Id));
                Check(Method<SetPosition>(group.Handle, 12)(group.Handle, new TilePosition { X = -1, Y = -1 }));
            }
            return group.Handle;
        }

        struct FreePlacement
        {
            public int Group;
            public int Column;
        }

        static FreePlacement[] PlanFreeLayout(FreeTileLayout layout, int groupWidth, out int groupCount)
        {
            var placements = new FreePlacement[layout.Count];
            var remaining = Enumerable.Range(0, layout.Count).OrderBy(index => layout[index].Position.X).ToList();
            int origin = 0;
            groupCount = 0;
            while (remaining.Count != 0)
            {
                int firstColumn = layout[remaining[0]].Position.X;
                if (groupCount > 0 || layout[remaining[0]].Bounds.Right > groupWidth)
                    origin = firstColumn;
                int right = origin + groupWidth;
                // A tile that spans a group edge moves intact with the next group.
                var batch = remaining.Where(index => layout[index].Bounds.Right <= right).ToArray();
                if (batch.Length == 0)
                    throw new InvalidOperationException(UiText.Get("开始菜单的磁贴组太窄，无法放入所选尺寸的磁贴。"));
                foreach (int index in batch)
                    placements[index] = new FreePlacement { Group = groupCount, Column = layout[index].Position.X - origin };
                remaining.RemoveAll(index => layout[index].Bounds.Right <= right);
                groupCount++;
            }
            return placements;
        }

        static void CommitAndWait(IntPtr collection)
        {
            IntPtr action=IntPtr.Zero,info=IntPtr.Zero;
            try
            {
                Check(Method<ObjectOut>(collection,29)(collection,out action));
                info=Query(action,new Guid("00000036-0000-0000-c000-000000000046"));
                var until=DateTime.UtcNow.AddSeconds(30);int state;
                do { Check(Method<GetInt>(info,7)(info,out state));if(state!=0)break;if(DateTime.UtcNow>until)throw new TimeoutException(UiText.Get("开始菜单保存超时，请稍后再试。"));System.Threading.Thread.Sleep(20); }while(true);
                Check(Method<NoArgs>(action,8)(action));
            }
            finally{Release(info);Release(action);}
        }

        public sealed class Result { public int Added,Present,FreeLayoutGroups; public string Details=""; }
        enum Operation { Inspect, Pin, Unpin }

        public static Result Run(Project p,string family,bool pinMissing)
        {
            var result = Edit(p, family, pinMissing ? Operation.Pin : Operation.Inspect, true);
            if (pinMissing) Edit(p, family, Operation.Unpin, false);
            return result;
        }

        public static void RemovePins(Project p, string family)
        {
            Edit(p, family, Operation.Unpin, true);
            Edit(p, family, Operation.Unpin, false);
        }

        static Uri Asset(string stem,string size)
        {
            return new Uri("ms-appx:///Assets/" + stem + "-" + size + ".png");
        }

        // A classic Start pin needs both a tile-store record (artwork and caption)
        // and a matching secondary identifier in the Start.TileGrid collection.
        static void RegisterPicture(IntPtr owner, string tileId, string stem, bool wide, bool caption)
        {
            var picture = new SecondaryTile();
            IntPtr unknown = Marshal.GetIUnknownForObject(picture), metadata = IntPtr.Zero;
            try
            {
                metadata = Query(unknown, new Guid("2d7f0d3b-ec36-463b-9f69-d7238d77c122"));
                Check(Method<SetOwner>(metadata,6)(metadata,owner));
                picture.TileId = tileId;
                picture.DisplayName = Project.TileDisplayName;
                picture.Arguments = tileId;
                var visual = picture.VisualElements;
                visual.Square150x150Logo = Asset(stem,"150");
                visual.Square71x71Logo = Asset(stem,"71");
                visual.ShowNameOnSquare150x150Logo = caption;
                if (wide)
                {
                    visual.Wide310x150Logo = Asset(stem,"Wide");
                    visual.Square310x310Logo = Asset(stem,"310");
                    visual.ShowNameOnWide310x150Logo = caption;
                    visual.ShowNameOnSquare310x310Logo = caption;
                }
                Check(Method<NoArgs>(metadata,12)(metadata));
                int hr = Method<NoArgs>(metadata,13)(metadata);
                // The immutable project's metadata may already exist after an unpin.
                if (hr != unchecked((int)0x800700B7)) Check(hr);
            }
            finally { Release(metadata); Release(unknown); }
        }

        static Result Edit(Project p, string family, Operation operation, bool secondary)
        {
            p.Validate();
            if(string.IsNullOrEmpty(family) || !family.StartsWith(p.PackageName+"_",StringComparison.Ordinal))throw new InvalidOperationException(UiText.Get("作品身份与已安装应用不匹配。"));
            int init = InitializeApartment();
            IntPtr collection=IntPtr.Zero,start=IntPtr.Zero,factory=IntPtr.Zero;
            var groups = new Dictionary<int, PictureGroup>();
            var created=new List<IntPtr>(); var result=new Result();
            try
            {
                collection = OpenCollection();
                start=Query(collection,StartId); int restriction;Check(Method<GetInt>(start,12)(start,out restriction));
                if(operation==Operation.Pin && restriction!=0)throw new InvalidOperationException(UiText.Get("开始菜单布局受系统策略限制，未更改任何磁贴。"));
                IntPtr text=HString(IdentifierName);Guid iid=IdentifierFactoryId;try{Check(RoGetActivationFactory(text,ref iid,out factory));}finally{WindowsDeleteString(text);}
                var numbers = Enumerable.Range(1, p.TileCount);
                FreePlacement[] placements = null;
                int largeColumns = 0;
                if (operation == Operation.Pin)
                {
                    if (p.FreeLayout != null)
                    {
                        placements = PlanFreeLayout(p.FreeLayout, Math.Max(1, GroupCellWidth(start) / 2), out result.FreeLayoutGroups);
                        numbers = numbers.OrderBy(n => placements[n - 1].Group);
                    }
                    else if (p.LargeTiles) largeColumns = Math.Min(p.Columns, Math.Max(1, GroupCellWidth(start) / 4));
                }
                try
                {
                    foreach(int n in numbers)
                    {
                        string stem = "Tile"+n.ToString("D3"), key = Project.PictureId(n);
                        IntPtr id=IntPtr.Zero,tile=IntPtr.Zero; text=HString(family+"!"+(secondary ? "Tile001" : stem));
                        try
                        {
                            if (secondary)
                            {
                                IntPtr tileKey = HString(key);
                                try { Check(Method<TwoStrings>(factory,7)(factory,text,tileKey,out id)); }
                                finally { WindowsDeleteString(tileKey); }
                            }
                            else Check(Method<StringObject>(factory,6)(factory,text,out id));
                            byte exists;
                            Check(Method<Contains>(collection,14)(collection,id,out tile,out exists));
                            if(exists!=0)
                            {
                                result.Present++;
                                if(operation==Operation.Unpin)
                                {
                                    if(restriction!=0)throw new InvalidOperationException(UiText.Get("开始菜单布局受系统策略限制，未更改任何磁贴。"));
                                    Check(Method<Unpin>(start,8)(start,id));
                                    continue;
                                }
                            }
                            else if(operation==Operation.Pin)
                            {
                                var shape = p.ShapeAt(p.TileKeyAt(n - 1));
                                RegisterPicture(text, key, stem, shape != TileShape.Medium, p.ShowNames);
                                Marshal.AddRef(id);
                                created.Add(id);
                                if (placements != null || shape == TileShape.Large)
                                {
                                    int groupIndex = 0;
                                    TilePosition position;
                                    if (placements != null)
                                    {
                                        var placement = placements[n - 1];
                                        var cell = p.FreeLayout[n - 1].Position;
                                        groupIndex = placement.Group;
                                        position = new TilePosition { X = placement.Column * 2, Y = cell.Y * 2 };
                                    }
                                    else position = new TilePosition { X = (result.Added % largeColumns) * 4, Y = (result.Added / largeColumns) * 4 };
                                    IntPtr pictureGroup = GetPictureGroup(collection, groups, groupIndex);
                                    var size = FreeTileLayout.Cells(shape);
                                    Check(Method<PinAtLocation>(start, 7)(start, id, pictureGroup, position,
                                        new TileSize { Width = size.Width * 2, Height = size.Height * 2 }));
                                }
                                else Check(Method<Pin>(start,6)(start,id,shape == TileShape.Wide ? 1 : 0));
                                result.Added++;
                                Release(tile);tile=IntPtr.Zero;Check(Method<Contains>(collection,14)(collection,id,out tile,out exists));
                                if(exists==0)throw new InvalidOperationException(UiText.Format("开始菜单未接受磁贴 {0}。", n));
                            }
                            if (tile != IntPtr.Zero)
                            {
                                TileSize size;
                                Check(Method<GetSize>(tile, 10)(tile, out size));
                                result.Details += n + ": " + size.Width + "x" + size.Height;
                                if (p.FreeLayout != null)
                                {
                                    TilePosition position;
                                    Check(Method<GetPosition>(tile, 8)(tile, out position));
                                    result.Details += " @ " + position.X + "," + position.Y;
                                }
                                result.Details += Environment.NewLine;
                            }
                        }
                        finally{WindowsDeleteString(text);Release(tile);Release(id);}
                    }
                    if(created.Count>0 || (operation==Operation.Unpin && result.Present>0))CommitAndWait(collection);
                }
                catch
                {
                    // Roll back only the identifiers newly added by this invocation.
                    if (created.Count > 0)
                    {
                        foreach(IntPtr id in created)Method<Unpin>(start,8)(start,id);
                        foreach (var group in groups.Values)
                            if (group.Id != Guid.Empty) Method<DeleteGroup>(collection, 19)(collection, group.Id);
                        try { CommitAndWait(collection); } catch { }
                    }
                    throw;
                }
                return result;
            }
            finally
            {
                foreach(IntPtr id in created)Release(id);
                foreach (var group in groups.Values) Release(group.Handle);
                Release(factory);Release(start);Release(collection);
                if(init>=0)RoUninitialize();
            }
        }
    }
}
