using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
[assembly: System.Reflection.AssemblyVersion("1.0.2.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.2.0")]

namespace BfmeTestLab {
 public class FileRecord { public string name; public long original_size; public string original_sha256; public long patched_size; public string patched_sha256; }
 public class Baseline { public string name; public string sha256; }
 public class Package { public int format; public string release; public FileRecord[] files; public Baseline[] baseline; }
 public class Asset { public string name; public string browser_download_url; public string digest; public long size; }
 public class Release { public string tag_name; public string html_url; public Asset[] assets; }
 public static class Packages {
  public const string Repository = "Joshua131313/bfme2-test-lab";
  public static string Home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BFME2TestLab");
  public static string Cache { get { return Path.Combine(Home, "packages"); } }
  public static readonly string[] Names = { "game.dat", "INI.big", "lotrbfme2.exe" };
  public static readonly string[] OriginalHashes = { "d79265812638d141c4f02e5924b03100d3f69eaca10a9379cfe14b074fbaf716", "5112ecb715ce3104dfa87f96c68567be57c1c8f43e8a6a2adf0b019281bb05e2", "dd4dc2ef6d51c8e23378864c570f183e78f7f908a2628c62ee366225e72e8412" };
  public static string Hash(string file) { using(var s=File.OpenRead(file)) using(var h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(s)).Replace("-", "").ToLowerInvariant(); }
  public static T Json<T>(string text) { return new JavaScriptSerializer { MaxJsonLength=1000000 }.Deserialize<T>(text); }
  public static Package Validate(string dir) {
   var p=Json<Package>(File.ReadAllText(Path.Combine(dir,"patch.json")));
   if(p==null || p.format!=2 || p.release==null || !p.release.StartsWith("BFME2 1.00 ") || p.release.Length>100 || p.files==null || p.files.Length!=3) throw new Exception("Unsupported test package.");
   for(int i=0;i<3;i++) {
    var f=p.files[i];
    if(f.name!=Names[i] || f.original_sha256!=OriginalHashes[i] || f.patched_size<1 || f.patched_size>100000000 || f.patched_sha256==null || !System.Text.RegularExpressions.Regex.IsMatch(f.patched_sha256,"^[0-9a-f]{64}$")) throw new Exception("Invalid package file record.");
    string path=Path.Combine(dir,"payload",f.name);
    if(new FileInfo(path).Length!=f.patched_size || Hash(path)!=f.patched_sha256) throw new Exception("Package checksum failed: "+f.name);
   }
   if(p.baseline==null || p.baseline.Length!=16) throw new Exception("Incomplete 1.00 baseline checks.");
   return p;
  }
  public static string Import(string zip) {
   Directory.CreateDirectory(Cache);
   string stage=Path.Combine(Cache,"staging-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(stage,"payload"));
   try {
    using(var z=ZipFile.OpenRead(zip)) {
     var manifest=z.Entries.Where(e=>e.FullName.Replace('\\','/').EndsWith("/patch.json",StringComparison.OrdinalIgnoreCase) || e.FullName=="patch.json").ToArray();
     if(manifest.Length!=1 || manifest[0].Length>1000000) throw new Exception("Package must contain one patch.json.");
     var entry=manifest[0]; string normalized=entry.FullName.Replace('\\','/');string prefix=normalized.Substring(0,normalized.Length-10);
     entry.ExtractToFile(Path.Combine(stage,"patch.json"));
     foreach(string name in Names) {
      var matches=z.Entries.Where(e=>e.FullName.Replace('\\','/')==prefix+"payload/"+name).ToArray();
      if(matches.Length!=1 || matches[0].Length>100000000) throw new Exception("Missing or oversized payload: "+name);
      matches[0].ExtractToFile(Path.Combine(stage,"payload",name));
     }
     // Remote scripts are never extracted or executed. Only manifest and fixed payload names.
    }
    Validate(stage);
    string destination=Path.Combine(Cache,Hash(Path.Combine(stage,"patch.json")));
    if(Directory.Exists(destination)) { Validate(destination); Directory.Delete(stage,true); }
    else Directory.Move(stage,destination);
    return destination;
   } catch { if(Directory.Exists(stage)) Directory.Delete(stage,true);throw; }
  }
  public static Release FetchRelease() {
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
   using(var w=new WebClient()) { w.Headers["User-Agent"]="BFME2-Test-Lab/1.0.1"; return Json<Release>(w.DownloadString("https://api.github.com/repos/"+Repository+"/releases/latest")); }
  }
  public static string DownloadLatest() {
   var r=FetchRelease();var assets=r.assets.Where(item=>item.name=="BFME2-TestPackage.zip").ToArray();
   if(assets.Length!=1) throw new Exception("The latest release has no test package.");
   var a=assets[0];
   if(a.size<1 || a.size>150000000 || !a.browser_download_url.StartsWith("https://github.com/"+Repository+"/releases/download/",StringComparison.Ordinal) || a.digest==null || !a.digest.StartsWith("sha256:")) throw new Exception("Invalid release asset or missing checksum.");
   Directory.CreateDirectory(Home);string temp=Path.Combine(Home,Guid.NewGuid().ToString("N")+".zip");
   try { using(var w=new WebClient()) { w.Headers["User-Agent"]="BFME2-Test-Lab/1.0.1"; w.DownloadFile(a.browser_download_url,temp); }
    if(new FileInfo(temp).Length!=a.size || "sha256:"+Hash(temp)!=a.digest) throw new Exception("Downloaded package checksum failed.");return Import(temp);
   } finally { if(File.Exists(temp)) File.Delete(temp); }
  }
 }
 public class MainWindow : Form {
  TextBox game=new TextBox(),log=new TextBox(); Label selected=new Label(),state=new Label();
  string packageDir; bool busy; List<Button> buttons=new List<Button>();
  public MainWindow() {
   Text="BFME2 Test Lab "+AppUpdate.CurrentVersion; ClientSize=new Size(660,530); MinimumSize=new Size(680,570); Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterScreen;
   BackColor=Color.FromArgb(245,247,250);
   var heading=new Label {Text="BFME2 · 1.00 Test Lab",Font=new Font("Segoe UI",18,FontStyle.Bold),AutoSize=true,Location=new Point(20,16)};Controls.Add(heading);
   Controls.Add(new Label {Text="Apply a test build, restore original files, or download the latest test.",AutoSize=true,Location=new Point(22,56)});
   game.SetBounds(22,93,510,28);game.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top;Controls.Add(game);
   AddButton("Browse…",542,91,96,delegate { using(var f=new FolderBrowserDialog {Description="Select the BFME2 folder containing game.dat and INI.big"}) if(f.ShowDialog()==DialogResult.OK) {game.Text=f.SelectedPath;Save();} });
   selected.SetBounds(22,134,615,28);Controls.Add(selected);
   AddButton("Test changes",22,171,142,delegate {RunAction("Install");});
   AddButton("Restore game",180,171,142,delegate {RunAction("Restore");});
   AddButton("Check files",338,171,142,delegate {RunAction("Validate");});
   AddButton("Update app",496,171,142,delegate {Work(async delegate {
    Write("Checking for an application update…");
    string destination=AppDomain.CurrentDomain.BaseDirectory;
    string plan=await Task.Run(()=>AppUpdate.Prepare(Packages.FetchRelease(),destination));
    if(plan==null) {Write("Test Lab "+AppUpdate.CurrentVersion+" is up to date.");return;}
    Save();Write("Update verified. Closing and reopening Test Lab…");AppUpdate.Start(plan);busy=false;Close();
   });});
   AddButton("Get latest files",22,216,142,delegate {Work(async delegate {packageDir=await Task.Run(()=>Packages.DownloadLatest());Save();RefreshPackage();Write("Latest package downloaded and verified. Click Test changes to install it.");});});
   AddButton("Load ZIP…",180,216,142,delegate {using(var d=new OpenFileDialog {Filter="Test package (*.zip)|*.zip"}) if(d.ShowDialog()==DialogResult.OK) {string path=d.FileName;Work(async delegate {packageDir=await Task.Run(()=>Packages.Import(path));Save();RefreshPackage();Write("Package imported and verified. Click Test changes to install it.");});}});
   AddButton("Release notes",338,216,142,delegate {Process.Start("https://github.com/"+Packages.Repository+"/releases/latest");});
   AddButton("Copy log",496,216,142,delegate {if(log.Text.Length>0) Clipboard.SetText(log.Text);});
   state.SetBounds(22,263,615,25);Controls.Add(state);
   log.SetBounds(22,294,616,208);log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.BackColor=Color.White;log.Font=new Font("Consolas",9);log.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(log);
   FormClosing+=delegate(object sender,FormClosingEventArgs e) {if(busy) {e.Cancel=true;MessageBox.Show("Wait for the current operation to finish.");}};
   Shown+=delegate { Work(async delegate {
    Directory.CreateDirectory(Packages.Home);string settings=Path.Combine(Packages.Home,"settings.json");
    if(File.Exists(settings)) {var cfg=Packages.Json<Dictionary<string,string>>(File.ReadAllText(settings));if(cfg.ContainsKey("game"))game.Text=cfg["game"];if(cfg.ContainsKey("package"))packageDir=cfg["package"];}
    if(string.IsNullOrWhiteSpace(game.Text)) foreach(string root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}) foreach(string name in new[]{"The Battle for Middle-earth (tm) II","The Battle for Middle-earth II"}) {string p=Path.Combine(root,"Electronic Arts",name);if(File.Exists(Path.Combine(p,"game.dat")))game.Text=p;}
    if(packageDir==null || !Directory.Exists(packageDir)) packageDir=await Task.Run(()=>Packages.Import(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"BFME2-TestPackage.zip")));
    RefreshPackage();Save();Write("Ready. Close BFME2 before installing or restoring. Check files identifies the installed build. Your map packs are preserved.");
    string updateResult=Path.Combine(Packages.Home,"last-app-update.txt");if(File.Exists(updateResult)) {Write(File.ReadAllText(updateResult));File.Delete(updateResult);}
   });};
  }
  void AddButton(string title,int x,int y,int width,Action action) {var b=new Button {Text=title,Location=new Point(x,y),Size=new Size(width,34),FlatStyle=FlatStyle.System};b.Click+=delegate {action();};Controls.Add(b);buttons.Add(b);}
  void Write(string text) {log.AppendText(text+Environment.NewLine);}
  void Save() {Directory.CreateDirectory(Packages.Home);File.WriteAllText(Path.Combine(Packages.Home,"settings.json"),new JavaScriptSerializer().Serialize(new Dictionary<string,string>{{"game",game.Text},{"package",packageDir??""}}));}
  void RefreshPackage() {var p=Packages.Validate(packageDir);selected.Text="Selected: "+p.release;}
  async void Work(Func<Task> work) {if(busy)return;busy=true;buttons.ForEach(b=>b.Enabled=false);game.Enabled=false;state.Text="Working…";UseWaitCursor=true;try {await work();state.Text="Ready";} catch(Exception e) {state.Text="Could not complete operation";Write("ERROR: "+e.Message);} finally {busy=false;buttons.ForEach(b=>b.Enabled=true);game.Enabled=true;UseWaitCursor=false;}}
  void RunAction(string action) {Work(async delegate {
   Packages.Validate(packageDir);string folder=game.Text.Trim().Trim('"');if(!Directory.Exists(folder) || !File.Exists(Path.Combine(folder,"game.dat")))throw new Exception("Choose the BFME2 installation folder first.");
   Save();string root=AppDomain.CurrentDomain.BaseDirectory;
   var info=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell\\v1.0\\powershell.exe")) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   info.Arguments="-NoProfile -ExecutionPolicy Bypass -File "+Quote(Path.Combine(root,"Apply-Package.ps1"))+" -Action "+action+" -GameDirectory "+Quote(folder)+" -PackageDirectory "+Quote(packageDir)+" -CacheDirectory "+Quote(Packages.Cache);
   Write(action+" — "+folder);
   var result=await Task.Run(()=>{using(var p=Process.Start(info)) {var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();p.WaitForExit();Task.WaitAll(stdout,stderr);return Tuple.Create(p.ExitCode,stdout.Result+stderr.Result);}});
   Write(result.Item2);if(result.Item1!=0)throw new Exception("Operation failed. See the log above.");
  });}
  static string Quote(string value) {if(value.Contains("\"") || value.Contains("\r") || value.Contains("\n"))throw new Exception("Invalid path.");return "\""+value.TrimEnd('\\')+"\"";}
 }
 static class Program {
  [STAThread] static void Main(string[] args) {
   if(args.Length==2 && args[0]=="--complete-update") {AppUpdate.Complete(args[1]);return;}
   if(args.Length==2 && args[0]=="--validate-package") {try {Console.WriteLine(Packages.Validate(args[1]).release);Environment.Exit(0);}catch {Environment.Exit(1);}return;}
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new MainWindow());
  }
 }
}

