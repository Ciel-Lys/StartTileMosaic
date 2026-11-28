using System;
using System.IO;
using System.Windows.Forms;

namespace TileMosaic
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (TileActivation.IsTileLauncher) return TileActivation.Open(args);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TileMosaic","Projects");
            string initial=null;
            for(int i=0;i<args.Length;i++)
            {
                if(args[i]=="--library" && i+1<args.Length)root=Path.GetFullPath(args[++i]);
                else if(args[i]=="--image" && i+1<args.Length)initial=args[++i];
            }
            UiText.Load(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TileMosaic", "language.txt"));
            Application.Run(new GenieForm(root,initial)); return 0;
        }
    }
}
