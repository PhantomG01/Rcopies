using DobleCommander;
using RcopiesLicensing;
using System.Security.Cryptography;

static class LiteTests {
 static int passed;
 static void Assert(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);passed++;Console.WriteLine("PASS: "+name);}
 static IEnumerable<Control> Children(Control c){yield return c;foreach(Control child in c.Controls)foreach(var nested in Children(child))yield return nested;}
 static void Pump(Task task){var deadline=DateTime.UtcNow.AddSeconds(45);while(!task.IsCompleted){if(DateTime.UtcNow>deadline)throw new Exception("Task timeout");Application.DoEvents();Thread.Sleep(1);}task.GetAwaiter().GetResult();}
 static T Pump<T>(Task<T> task){Pump((Task)task);return task.GetAwaiter().GetResult();}
 static void Until(Func<bool> condition){var end=DateTime.UtcNow.AddSeconds(35);while(!condition()){if(DateTime.UtcNow>end)throw new Exception("Progress timeout");Application.DoEvents();Thread.Sleep(1);}}
 static byte[] Hash(string path)=>SHA256.HashData(File.ReadAllBytes(path));
 [STAThread] static int Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());var root=Path.Combine(Path.GetTempPath(),"Rcopies-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   ResourceBudget.Initialize();
   StorageUnit? FakeUnit(string path)=>path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)?new StorageUnit(path,Path.GetFileName(path),DriveType.Fixed,500_000_000_000,400_000_000_000,"NTFS"):null;
   using var testKey=System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
   var testId=LicenseCore.Fingerprint("isolated-test-machine");var testLicense=LicenseCore.Sign(new(1,LicenseCore.Product,Guid.NewGuid().ToString(),"Test",testId,DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1)),testKey);
   bool Authorize(){LicenseCore.Verify(testLicense,testKey.ExportSubjectPublicKeyInfoPem(),testId,DateTimeOffset.UtcNow);return true;}
   string Dir(string name){var path=Path.Combine(root,name);Directory.CreateDirectory(path);return path;}
   var source=Dir("MATRIZ");Directory.CreateDirectory(Path.Combine(source,"VacÃ­a"));File.WriteAllText(Path.Combine(source,"episodio.txt"),"Contenido original Ã±");
   var a=Path.Combine(root,"COPIA-A");var b=Path.Combine(root,"COPIA-B");var excluded=Path.Combine(root,"NO-SELECCIONADO");
   using var form=new LiteWindow(false,Dir("logs"),FakeUnit,Authorize);
   var controls=Children(form).ToArray();Assert(controls.OfType<ListView>().Count()==1&&!controls.OfType<TreeView>().Any(),"LITE has one device list and no explorer panels");
   Assert(!controls.OfType<Button>().Any(x=>x.Text.Contains("Renombrar")||x.Text=="Logs"),"LITE exposes only copy workflow controls");
   var deviceList=controls.OfType<ListView>().Single();Assert(!deviceList.CheckBoxes&&deviceList.MultiSelect&&deviceList.FullRowSelect,"native multiselection supports Shift and Ctrl without checkboxes");
   Assert(StorageUnits.Eligible(DriveType.Fixed,1_000_000_000)&&StorageUnits.Eligible(DriveType.Removable,32_000_000_000),"small local and USB storage are eligible without a 120 GB minimum");
   Assert(!StorageUnits.Eligible(DriveType.Fixed,0)&&!StorageUnits.Eligible(DriveType.Removable,-1),"invalid or empty capacity is still rejected");
   Assert(!StorageUnits.Eligible(DriveType.CDRom,500_000_000_000)&&!StorageUnits.Eligible(DriveType.Network,500_000_000_000)&&!StorageUnits.Eligible(DriveType.Ram,500_000_000_000),"optical drives including DVD and Blu-ray and nonlocal device types are excluded");
   Assert(StorageUnits.Read(root)==null,"production storage reader rejects an ordinary folder");
   var windowsRoot=Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!;Assert(StorageUnits.SystemVolume(windowsRoot)&&StorageUnits.Read(windowsRoot)==null,"Windows volume is excluded from production destination detection");
   using(var protectedVolume=new LiteWindow(false,Dir("system-logs"),path=>new StorageUnit(path,"Windows",DriveType.Fixed,500_000_000_000,400_000_000_000,"NTFS"),Authorize)){bool rejected=false;try{protectedVolume.AddPaths([windowsRoot]);}catch(IOException){rejected=true;}Assert(rejected&&!protectedVolume.Targets.Any(),"manual insertion cannot bypass Windows-volume protection");}
   Assert(controls.OfType<MemoryMeter>().Any()&&controls.OfType<Button>().Any(b=>b.Text=="Licencia"),"Rcopies exposes RAM meter and license management");
   using(var unlicensed=new LiteWindow(false,Dir("unlicensed-logs"),FakeUnit,()=>false)){var destination=Path.Combine(root,"free-copy");unlicensed.ConfigureCopy(true,Conflict.Compare,false);unlicensed.SetSource(source);unlicensed.AddPaths([destination]);Pump(unlicensed.StartCopyAsync());Assert(File.Exists(Path.Combine(destination,"episodio.txt")),"free mode opens and copies to one destination");unlicensed.AddPaths([Path.Combine(root,"must-not-copy")]);bool rejected=false;try{Pump(unlicensed.StartCopyAsync());}catch(IOException){rejected=true;}Assert(rejected&&!Directory.Exists(Path.Combine(root,"must-not-copy")),"free mode blocks multiple destinations before writing");}

   using(var small=new LiteWindow(false,Dir("filter-logs"),path=>new StorageUnit(path,"Small",DriveType.Removable,64_000_000_000,32_000_000_000,"exFAT"))){small.AddPaths([Path.Combine(root,"small")]);Assert(small.Targets.Count()==1,"manual admission accepts a ready storage unit below the old capacity threshold");}
   using(var filtered=new LiteWindow(false,Dir("optical-logs"),path=>new StorageUnit(path,"Blu-ray",DriveType.CDRom,500_000_000_000,0,"UDF"))){bool rejected=false;try{filtered.AddPaths([Path.Combine(root,"optical")]);}catch(IOException){rejected=true;}Assert(rejected&&!filtered.Targets.Any(),"manual admission cannot bypass the optical-drive filter");}
   Assert(RemainingTime.Estimate(50,100,10)==10&&RemainingTime.Format(10)=="00:00:10","ETA estimates remaining work and formats hours minutes seconds");
   Assert(RemainingTime.Estimate(0,100,10)==null&&RemainingTime.Format(90061)=="1 d 01:01:01","ETA handles unknown rates and durations longer than a day");
   Assert(Branding.Logo.Width==1024&&Branding.Logo.Height==1024,"Rcopies icon is rendered correctly");
   using(var about=new AboutDialog()){var text=string.Join("\n",Children(about).OfType<Label>().Select(l=>l.Text));Assert(text.Contains(Branding.Author)&&text.Contains(Branding.Phone)&&text.Contains(Branding.Email),"about preserves author and contact");}
   form.SetSource(source);form.AddPaths([a,b,excluded]);form.SelectTarget(excluded,false);form.AddPaths([a]);
   Assert(form.Targets.Count()==3&&form.SelectedDestinations.Length==2,"destinations deduplicate and retain explicit selection");
   form.AddPaths([source]);Assert(!form.SelectedDestinations.Contains(source),"matrix cannot also be selected as a destination");
   form.ConfigureCopy(true,Conflict.Compare,true);Pump(form.StartCopyAsync());
   Assert(File.Exists(Path.Combine(a,"episodio.txt"))&&File.Exists(Path.Combine(b,"episodio.txt")),"LITE copies to two selected destinations");
   Assert(Hash(Path.Combine(a,"episodio.txt")).SequenceEqual(Hash(Path.Combine(source,"episodio.txt")))&&Hash(Path.Combine(b,"episodio.txt")).SequenceEqual(Hash(Path.Combine(source,"episodio.txt"))),"both destination hashes match source");
   Assert(Directory.Exists(Path.Combine(a,"VacÃ­a")),"contents mode preserves empty directories without wrapper");
   Assert(!Directory.Exists(excluded),"unselected target is never written");
   form.RefreshProgress();Assert(form.GlobalProgress==100&&form.ProgressSummary.Contains("100%")&&form.ProgressSummary.Contains("Restante 00:00:00")&&form.Targets.Where(t=>t.ShowProgress).All(t=>t.Eta=="00:00:00"),"global progress reaches completion");
   form.ClearFinishedProgress();Assert(form.GlobalProgress==0&&form.ProgressSummary==""&&form.Targets.All(t=>!t.ShowProgress&&t.State==""),"clearing completed progress empties the information area");
   File.WriteAllText(Path.Combine(source,"episodio.txt"),"Contenido actualizado");Pump(form.StartCopyAsync());Assert(Hash(Path.Combine(a,"episodio.txt")).SequenceEqual(Hash(Path.Combine(source,"episodio.txt"))),"compare policy updates modified file");
   File.WriteAllText(Path.Combine(a,"sobrante.txt"),"Debe retirarse");var manifest=Pump(MirrorPlanner.ReadSourceAsync(source));var preview=Pump(MirrorPlanner.PreviewAsync(manifest,a));
   using(var popup=new MirrorConfirmationDialog([preview])){var buttons=Children(popup).OfType<Button>().Select(x=>x.Text).Order().ToArray();Assert(buttons.SequenceEqual(new[]{"Cancelar","Proceder"})&&Children(popup).OfType<DataGridView>().Any(),"mirror comparison has aligned rows and exactly Proceder and Cancelar as final actions");}
   Assert(File.Exists(Path.Combine(a,"sobrante.txt")),"mirror preview makes no destination changes");
   Pump(form.ExecuteMirrorAsync([preview],false));Assert(!File.Exists(Path.Combine(a,"sobrante.txt"))&&Hash(Path.Combine(a,"episodio.txt")).SequenceEqual(Hash(Path.Combine(source,"episodio.txt"))),"LITE mirror removes extras and matches matrix");
   Pump(form.CancelAsync(true));form.RefreshProgress();Assert(form.ProgressSummary==""&&form.GlobalProgress==0&&form.Targets.All(t=>!t.ShowProgress),"cancel clears completed mirror presentation");
   bool cancellation=false;using(var stop=new CancellationTokenSource()){stop.Cancel();try{Plan.Create([source],true,stop.Token);}catch(OperationCanceledException){cancellation=true;}}Assert(cancellation,"source planning responds to cancellation");
   var large=Dir("MATRIZ-GRANDE");using(var f=File.Create(Path.Combine(large,"large.bin")))f.SetLength(128L*1024*1024);
   using(var running=new LiteWindow(false,Dir("active-logs"),FakeUnit,Authorize)){var x=Path.Combine(root,"ACTIVE-A");var y=Path.Combine(root,"ACTIVE-B");running.ConfigureCopy(true,Conflict.Compare,false);running.SetSource(large);running.AddPaths([x,y]);var task=running.StartCopyAsync();Until(()=>running.Targets.Any(t=>t.Copy?.Status.Done>0)||task.IsCompleted);
    Assert(running.Busy,"copy controller locks configuration during transfer");bool locked=false;try{running.SetSource(source);}catch(IOException){locked=true;}Assert(locked,"matrix cannot be changed during active copy");
    running.PauseSelected(true);running.RefreshProgress();Assert(running.Targets.Any(t=>t.State=="Pausado"&&t.Eta=="Pausado")&&running.ProgressSummary.Contains("Restante Pausado"),"pause updates device state and freezes ETA");
    var late=Path.Combine(root,"ACTIVE-LATE");running.AddPaths([late]);running.JoinSelectedTargets();Assert(running.Targets.Single(t=>t.Path==late).Copy!=null,"new selected destination joins the active batch");
    running.ResumeSelected(true);Pump(task);Assert(File.Exists(Path.Combine(late,"large.bin"))&&Hash(Path.Combine(late,"large.bin")).SequenceEqual(Hash(Path.Combine(large,"large.bin"))),"resumed batch finishes the late destination with matching content");
    running.ConfigureCopy(true,Conflict.Overwrite,true);var cancelTask=running.StartCopyAsync();Until(()=>running.CopyPrepared&&running.Targets.Any(t=>t.Copy?.Status.State=="Copiando"&&t.Copy.Status.Done>0)||cancelTask.IsCompleted);
    Pump(running.CancelAsync(true));Pump(cancelTask);running.RefreshProgress();Assert(running.ProgressSummary==""&&running.GlobalProgress==0&&running.Targets.All(t=>!t.ShowProgress),"active cancellation clears rows and global progress");Assert(!Directory.EnumerateFiles(root,"*.partial",SearchOption.AllDirectories).Any(),"cancellation leaves no partial transfer files");
   }
   using(var invalid=new LiteWindow(false,Dir("invalid-logs"),FakeUnit,Authorize)){var good=Path.Combine(root,"should-not-start");invalid.SetSource(source);invalid.AddPaths([good,Path.Combine(source,"nested")]);bool rejected=false;try{invalid.ConfigureCopy(true,Conflict.Compare,true);invalid.AddPaths([Path.Combine(good,"nested")]);Pump(invalid.StartCopyAsync());}catch(IOException){rejected=true;}Assert(rejected&&!Directory.Exists(good),"all destination overlaps are validated before any copy starts");}
   if(args.Length==2&&args[0]=="--render"){
    form.SetSource(source);form.Show();Application.DoEvents();Pump(form.StartCopyAsync());Application.DoEvents();Thread.Sleep(150);Application.DoEvents();using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);bitmap.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);form.Close();using var activation=new ActivationDialog("Se requiere una licencia vÃ¡lida para usar Rcopies.");activation.Show();Application.DoEvents();using var activationImage=new Bitmap(activation.Width,activation.Height);activation.DrawToBitmap(activationImage,new Rectangle(Point.Empty,activationImage.Size));activationImage.Save(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!,"Rcopies-Activacion.png"),System.Drawing.Imaging.ImageFormat.Png);activation.Close();Console.WriteLine("UI preview rendered: "+args[1]);
   }
   using(var process=System.Diagnostics.Process.GetCurrentProcess()){process.Refresh();Assert(!ResourceBudget.LimitActive&&process.PeakWorkingSet64>0,"copy workflows measure RAM without an application ceiling");Console.WriteLine($"Measured peak resident RAM: {process.PeakWorkingSet64/1048576d:F1} MiB; private commit: {process.PrivateMemorySize64/1048576d:F1} MiB");}
   Console.WriteLine($"{passed} Rcopies UI assertions passed. No user files or disks were modified.");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
}
