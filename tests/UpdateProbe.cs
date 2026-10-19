using System;
using System.IO;
[assembly: System.Reflection.AssemblyVersion("1.0.2.0")]
class UpdateProbe { static void Main() {File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"restarted.txt"),"Update restart succeeded");} }
