using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace BfmeTestLab {
 public class UpdateFile { public string name; public string sha256; }
 public class UpdatePlan { public string target; public string stage; public int parent; public long parentStarted; public UpdateFile[] files; }
 public static class AppUpdate {
  public const string CurrentVersion="1.0.2";
  public static readonly string[] Names={"BFME2-Test-Lab.exe","Apply-Package.ps1","baseline.json","BFME2-TestPackage.zip","README.txt","Install.cmd","Install-App.ps1"};
  public static string Prepare(Release release,string destination) {
   Version version;
   if(release==null || release.tag_name==null || !Version.TryParse(release.tag_name.TrimStart('v'),out version))throw new Exception("Invalid application release version.");
   if(version<=new Version(CurrentVersion))return null;
   string name="BFME2-Test-Lab-"+version.ToString()+".zip";
   var assets=release.assets.Where(a=>a.name==name).ToArray();
   if(assets.Length!=1)throw new Exception("The release has no matching application ZIP.");
   var asset=assets[0];
   if(asset.size<1 || asset.size>150000000 || asset.digest==null || !System.Text.RegularExpressions.Regex.IsMatch(asset.digest,"^sha256:[0-9a-f]{64}$") || !asset.browser_download_url.StartsWith("https://github.com/"+Packages.Repository+"/releases/download/",StringComparison.Ordinal))throw new Exception("Unrecognized application download or missing checksum.");
   string stage=Path.Combine(Packages.Home,"app-updates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
   string zip=Path.Combine(stage,"download.zip");
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
   using(var w=new WebClient()) {w.Headers["User-Agent"]="BFME2-Test-Lab/"+CurrentVersion;w.DownloadFile(asset.browser_download_url,zip);}
   if(new FileInfo(zip).Length!=asset.size || "sha256:"+Packages.Hash(zip)!=asset.digest)throw new Exception("Application download checksum failed. Current app is unchanged.");
   Extract(zip,stage,version);
   var self=Process.GetCurrentProcess();
   var plan=new UpdatePlan {target=Path.GetFullPath(destination),stage=stage,parent=self.Id,parentStarted=self.StartTime.ToUniversalTime().Ticks,files=Names.Select(n=>new UpdateFile {name=n,sha256=Packages.Hash(Path.Combine(stage,n))}).ToArray()};
   string planPath=Path.Combine(stage,"update.json");File.WriteAllText(planPath,new JavaScriptSerializer().Serialize(plan));
   File.Copy(Assembly.GetExecutingAssembly().Location,Path.Combine(stage,"UpdateHelper.exe"));
   return planPath;
  }
  public static void Extract(string zip,string stage,Version expected) {
   Directory.CreateDirectory(stage);
   using(var z=ZipFile.OpenRead(zip))foreach(string name in Names) {
    var matches=z.Entries.Where(e=>e.FullName=="BFME2-Test-Lab/"+name).ToArray();
    if(matches.Length!=1 || matches[0].Length<1 || matches[0].Length>150000000)throw new Exception("Invalid application ZIP entry: "+name);
    matches[0].ExtractToFile(Path.Combine(stage,name));
   }
   var actual=AssemblyName.GetAssemblyName(Path.Combine(stage,Names[0])).Version;
   if(actual.Major!=expected.Major || actual.Minor!=expected.Minor || actual.Build!=expected.Build)throw new Exception("Application version does not match the release.");
  }
  public static void Start(string planPath) {
   var p=new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(planPath),"UpdateHelper.exe")) {UseShellExecute=false,CreateNoWindow=true,Arguments="--complete-update \""+planPath+"\""};
   Process.Start(p);
  }
  public static void Apply(UpdatePlan plan) {
   if(plan.files==null || plan.files.Length!=Names.Length || !Directory.Exists(plan.target) || !File.Exists(Path.Combine(plan.target,Names[0])) || Path.GetFullPath(plan.target).TrimEnd('\\').Equals(Path.GetFullPath(plan.stage).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))throw new Exception("Invalid update destination.");
   string backup=Path.Combine(plan.stage,"previous");Directory.CreateDirectory(backup);
   var changed=new List<string>();var existed=new HashSet<string>();var temporary=new List<string>();
   foreach(string name in Names) {
    var records=plan.files.Where(f=>f.name==name).ToArray();
    if(records.Length!=1 || Packages.Hash(Path.Combine(plan.stage,name))!=records[0].sha256)throw new Exception("Staged update checksum failed: "+name);
    string target=Path.Combine(plan.target,name);
    if(File.Exists(target)) {File.Copy(target,Path.Combine(backup,name),true);existed.Add(name);}
   }
   try {
    foreach(string name in Names) {
     string target=Path.Combine(plan.target,name),temp=target+".update-"+Guid.NewGuid().ToString("N");temporary.Add(temp);
     File.Copy(Path.Combine(plan.stage,name),temp);
     if(existed.Contains(name))File.Replace(temp,target,null);else File.Move(temp,target);
     changed.Add(name);
     if(Packages.Hash(target)!=plan.files.Single(f=>f.name==name).sha256)throw new Exception("Installed update checksum failed: "+name);
    }
   } catch(Exception original) {
    var failures=new List<string>();
    foreach(string name in changed.AsEnumerable().Reverse()) {
     try {string target=Path.Combine(plan.target,name);if(existed.Contains(name)) {string temp=target+".rollback-"+Guid.NewGuid().ToString("N");temporary.Add(temp);File.Copy(Path.Combine(backup,name),temp);File.Replace(temp,target,null);}else File.Delete(target);}
     catch {failures.Add(name);}
    }
    throw new Exception(original.Message+(failures.Count==0?" Previous app files restored.":" Could not restore: "+string.Join(", ",failures)+". Backups: "+backup));
   } finally {foreach(string p in temporary)try {if(File.Exists(p))File.Delete(p);}catch {}}
  }
  public static void Complete(string planPath, bool showErrors=true) {
   UpdatePlan plan=null;bool success=false;
   try {
    plan=Packages.Json<UpdatePlan>(File.ReadAllText(planPath));
    Process parent=null;try {parent=Process.GetProcessById(plan.parent);}catch(ArgumentException) {}
    if(parent!=null)using(parent) {if(parent.StartTime.ToUniversalTime().Ticks!=plan.parentStarted)throw new Exception("Original process identity changed.");if(!parent.WaitForExit(60000))throw new Exception("The application did not close. Update cancelled.");}
    Apply(plan);success=true;
    Directory.CreateDirectory(Packages.Home);File.WriteAllText(Path.Combine(Packages.Home,"last-app-update.txt"),"Application update installed successfully. Game files were not changed.");
   } catch(Exception e) {try {File.WriteAllText(Path.Combine(Path.GetDirectoryName(planPath),"update-error.txt"),e.ToString());}catch {} if(showErrors)MessageBox.Show("Application update failed: "+e.Message,"BFME2 Test Lab",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   if(plan!=null && File.Exists(Path.Combine(plan.target,Names[0]))) {
    try {Process.Start(new ProcessStartInfo(Path.Combine(plan.target,Names[0])) {UseShellExecute=true,WorkingDirectory=plan.target});}
    catch(Exception e) {if(showErrors)MessageBox.Show((success?"Update installed. ":"")+"Open Test Lab manually. "+e.Message);}
   }
  }
 }
}
