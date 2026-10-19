using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using BfmeTestLab;
class UpdateTests {
 [STAThread] static int Main(string[] args) {
  if(args[0]=="--helper") {Packages.Home=Path.Combine(Path.GetDirectoryName(args[1]),"home");AppUpdate.Complete(args[1],false);return 0;}
  try {
   string root=Path.Combine(Path.GetFullPath(args[0]),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   string app=Path.GetFullPath(args[1]),stage=Path.Combine(root,"stage"),target=Path.Combine(root,"target");Directory.CreateDirectory(stage);Directory.CreateDirectory(target);
   foreach(string n in AppUpdate.Names) {File.Copy(Path.Combine(app,n),Path.Combine(stage,n));File.WriteAllText(Path.Combine(target,n),"previous-"+n);}
   File.WriteAllText(Path.Combine(target,"keep-settings.txt"),"unchanged");
   var before=AppUpdate.Names.ToDictionary(n=>n,n=>Packages.Hash(Path.Combine(target,n)));
   var plan=new UpdatePlan {stage=stage,target=target,files=AppUpdate.Names.Select(n=>new UpdateFile {name=n,sha256=Packages.Hash(Path.Combine(stage,n))}).ToArray()};
   bool failed=false;
   using(var locked=File.Open(Path.Combine(target,"Apply-Package.ps1"),FileMode.Open,FileAccess.Read,FileShare.Read)) {try {AppUpdate.Apply(plan);}catch {failed=true;}}
   if(!failed || AppUpdate.Names.Any(n=>Packages.Hash(Path.Combine(target,n))!=before[n]))throw new Exception("Rollback failed");
   AppUpdate.Apply(plan);
   if(AppUpdate.Names.Any(n=>Packages.Hash(Path.Combine(target,n))!=Packages.Hash(Path.Combine(stage,n))) || File.ReadAllText(Path.Combine(target,"keep-settings.txt"))!="unchanged")throw new Exception("Apply/preservation failed");
   File.AppendAllText(Path.Combine(stage,"README.txt"),"tampered");failed=false;try {AppUpdate.Apply(plan);}catch {failed=true;}if(!failed)throw new Exception("Tamper accepted");
   if(AppUpdate.Prepare(new Release {tag_name="v"+AppUpdate.CurrentVersion},target)!=null)throw new Exception("Same-version update offered");
   Console.WriteLine("PASS: app replace, rollback after locked second file, extra-file preservation, tamper refusal, up-to-date response.");
   // Real helper-process handoff: wait for a parent, replace, and restart a harmless probe.
   File.Copy(Path.Combine(app,"README.txt"),Path.Combine(stage,"README.txt"),true);
   File.Copy(Path.GetFullPath(args[2]),Path.Combine(stage,"BFME2-Test-Lab.exe"),true);
   plan.files=AppUpdate.Names.Select(n=>new UpdateFile {name=n,sha256=Packages.Hash(Path.Combine(stage,n))}).ToArray();
   using(var parent=Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -Command Start-Sleep -Seconds 2") {UseShellExecute=false,CreateNoWindow=true})) {
    plan.parent=parent.Id;plan.parentStarted=parent.StartTime.ToUniversalTime().Ticks;
    string planPath=Path.Combine(root,"plan.json");File.WriteAllText(planPath,new JavaScriptSerializer().Serialize(plan));
    using(var helper=Process.Start(new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--helper \""+planPath+"\"") {UseShellExecute=false,CreateNoWindow=true})) {
     if(!helper.WaitForExit(20000)) {helper.Kill();throw new Exception("Helper timeout");}
     if(File.Exists(Path.Combine(root,"update-error.txt")))throw new Exception(File.ReadAllText(Path.Combine(root,"update-error.txt")));
     if(!parent.HasExited)throw new Exception("Helper did not wait for parent");
    }
   }
   for(int i=0;i<100 && !File.Exists(Path.Combine(target,"restarted.txt"));i++)System.Threading.Thread.Sleep(50);
   if(!File.Exists(Path.Combine(target,"restarted.txt")))throw new Exception("Updated app was not restarted");
   Console.WriteLine("PASS: actual helper waits for parent, replaces files, restarts updated executable.");return 0;
  }catch(Exception e) {Console.WriteLine(e);return 1;}
 }
}
