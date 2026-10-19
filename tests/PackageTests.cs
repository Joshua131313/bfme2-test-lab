using System;
using System.IO;
using System.IO.Compression;
using System.Drawing;
using System.Windows.Forms;
using BfmeTestLab;
class PackageTests {
 [STAThread] static int Main(string[] args) {
  try {
   Packages.Home=Path.Combine(Path.GetFullPath(args[0]),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Packages.Home);
   string zip=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"BFME2-TestPackage.zip");
   string folder=Packages.Import(zip);Packages.Validate(folder);
   if(Packages.Import(zip)!=folder)throw new Exception("Repeat import failed");
   string bad=Path.Combine(Packages.Home,"bad.zip");File.Copy(zip,bad);
   using(var z=ZipFile.Open(bad,ZipArchiveMode.Update)) {var entry=z.CreateEntry("BFME2-1.00-Hybrid-Test04/../../escape.txt");using(var w=new StreamWriter(entry.Open()))w.Write("not extracted");}
   Packages.Import(bad);if(File.Exists(Path.Combine(Packages.Home,"escape.txt")))throw new Exception("Traversal extracted");
   using(var z=ZipFile.Open(bad,ZipArchiveMode.Update)) {z.GetEntry("BFME2-1.00-Hybrid-Test04/payload/game.dat").Delete();var e=z.CreateEntry("BFME2-1.00-Hybrid-Test04/payload/game.dat");using(var w=new StreamWriter(e.Open()))w.Write("corrupt");}
   bool refused=false;try {Packages.Import(bad);}catch {refused=true;}if(!refused)throw new Exception("Corrupt payload accepted");
   string dupe=Path.Combine(Packages.Home,"dupe.zip");File.Copy(zip,dupe);using(var z=ZipFile.Open(dupe,ZipArchiveMode.Update)) {var e=z.CreateEntry("other/patch.json");using(var w=new StreamWriter(e.Open()))w.Write("{}");}
   refused=false;try {Packages.Import(dupe);}catch {refused=true;}if(!refused)throw new Exception("Duplicate manifest accepted");
   Application.EnableVisualStyles();using(var form=new MainWindow()) {form.ShowInTaskbar=false;form.Opacity=0;form.Show();var deadline=DateTime.UtcNow.AddSeconds(15);while(DateTime.UtcNow<deadline && (form.UseWaitCursor || !File.Exists(Path.Combine(Packages.Home,"settings.json")))) {Application.DoEvents();System.Threading.Thread.Sleep(20);}Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)) {form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(Path.Combine(Path.GetFullPath(args[0]),"window.png"));}}
   Console.WriteLine("PASS: import, repeat import, traversal ignored, corrupt payload refused, duplicate manifest refused, WinForms construction/render.");return 0;
  } catch(Exception e) {Console.WriteLine(e);return 1;}
 }
}
