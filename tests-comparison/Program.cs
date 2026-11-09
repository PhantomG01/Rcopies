using DobleCommander;

static class ComparisonTests {
 static int passed;
 static void Assert(bool value,string name){if(!value)throw new Exception("FAIL: "+name);passed++;Console.WriteLine("PASS: "+name);}
 static T Pump<T>(Task<T> task){Pump((Task)task);return task.GetAwaiter().GetResult();}
 static void Pump(Task task){var end=DateTime.UtcNow.AddSeconds(40);while(!task.IsCompleted){if(DateTime.UtcNow>end)throw new Exception("Timeout");Application.DoEvents();Thread.Sleep(1);}task.GetAwaiter().GetResult();}
 static IEnumerable<Control> Children(Control c){yield return c;foreach(Control child in c.Controls)foreach(var nested in Children(child))yield return nested;}
 [STAThread] static int Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
  var root=Path.Combine(Path.GetTempPath(),"Rcopies-comparison-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   string Dir(string name){var p=Path.Combine(root,name);Directory.CreateDirectory(p);return p;}
   void Write(string folder,string path,string value){var full=Path.Combine(folder,path);Directory.CreateDirectory(Path.GetDirectoryName(full)!);File.WriteAllText(full,value);}
   string source=Dir("MATRIZ"),target=Dir("DESTINO-A"),other=Dir("DESTINO-B"),logs=Dir("logs");
   Write(source,"Nuevo.txt","New source");Write(source,"Actualizado.txt","Current source content");Write(source,"Igual.txt","Identical");Write(target,"Igual.txt","Identical");File.SetLastWriteTimeUtc(Path.Combine(target,"Igual.txt"),File.GetLastWriteTimeUtc(Path.Combine(source,"Igual.txt")));
   Write(target,"Actualizado.txt","Old");Write(target,"Extras/uno.txt","Remove one");Write(target,"Extras/dos.txt","Keep two");Write(source,"Subcarpeta/Seleccionado.txt","Selected child");Write(source,"CambioTipo/uno.txt","One");Write(source,"CambioTipo/dos.txt","Two");Write(target,"CambioTipo","Was a file");Directory.CreateDirectory(Path.Combine(source,"Vacia"));Write(source,"NoCambiarTipo","Source file");Write(target,"NoCambiarTipo/keep.txt","Keep existing tree");
   var manifest=Pump(MirrorPlanner.ReadSourceAsync(source));var full=Pump(MirrorPlanner.PreviewAsync(manifest,target));var second=Pump(MirrorPlanner.PreviewAsync(manifest,other));
   using(var dialog=new MirrorConfirmationDialog([full,second])){
    Assert(dialog.ComparisonGrid.Columns.Count==10&&dialog.ComparisonGrid.VirtualMode,"source and destination have aligned icon/path/size/date columns and virtual rows");
    Assert(Children(dialog).OfType<Button>().Select(b=>b.Text).Order().SequenceEqual(new[]{"Cancelar","Proceder"}),"only Proceder and Cancelar execute or dismiss the comparison");
    Assert(dialog.SelectedPreviews.Length==2&&dialog.SelectedPreviews.All(p=>!p.IsPartial),"default selection preserves full mirrors for every destination");
    if(args.Length==2&&args[0]=="--render"){dialog.Show();Application.DoEvents();using var image=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);}
    dialog.SelectAllChanges(false);Assert(dialog.SelectedPreviews.Length==1&&dialog.SelectedPreviews[0].Destination==other,"deselecting a destination leaves another destination selected");
    dialog.SelectDestination(1);dialog.SelectAllChanges(false);Assert(dialog.SelectedPreviews.Length==0&&!Children(dialog).OfType<Button>().Single(b=>b.Text=="Proceder").Enabled,"no selected changes disables Proceder");
    dialog.SelectDestination(0);dialog.SelectPath("Nuevo.txt",true);Assert(dialog.SelectedPreviews.Length==1&&dialog.SelectedPreviews[0].IsPartial&&dialog.SelectedPreviews[0].CopyFiles.Single().Relative=="Nuevo.txt","individual source files create an actual partial execution plan");
    var search=Children(dialog).OfType<TextBox>().Single();search.Text="not-present";Assert(dialog.ComparisonGrid.RowCount==0&&dialog.SelectedPreviews.Length==1,"search only filters presentation and preserves selections");search.Text="";
    dialog.SelectPath(Path.Combine("CambioTipo","uno.txt"),true);Assert(dialog.SelectedPreviews[0].CopyFiles.Count(f=>f.Relative.StartsWith("CambioTipo"))==2,"selecting a child of a type conflict includes the atomic replacement subtree");
    dialog.SelectPath(Path.Combine("CambioTipo","uno.txt"),false);Assert(!dialog.SelectedPreviews[0].CopyFiles.Any(f=>f.Relative.StartsWith("CambioTipo")),"deselecting a type-conflict child removes its whole replacement group");
    dialog.SelectDestination(1);Assert(dialog.SelectedPreviews.Length==1&&dialog.SelectedPreviews[0].Destination==target,"switching destinations does not lose independent selections");
    if(dialog.Visible)dialog.Close();
   }
   var selected=new HashSet<string>(StringComparer.OrdinalIgnoreCase){Path.Combine("Subcarpeta","Seleccionado.txt"),Path.Combine("Extras","uno.txt")};
   var partial=MirrorPlanner.SelectChanges(full,selected);Assert(partial.IsPartial&&partial.CopyFiles.Length==1&&partial.RemoveRoots.Length==1,"partial planner limits actual copies and deletes to selected items");
   var partialJob=new MirrorJob(partial,false,logs);Pump(partialJob.RunAsync());Assert(partialJob.Status.State=="Completado"&&File.ReadAllText(Path.Combine(target,"Subcarpeta","Seleccionado.txt"))=="Selected child","selected child copies with implicit parent directory");
   Assert(File.ReadAllText(Path.Combine(target,"Actualizado.txt"))=="Old"&&!File.Exists(Path.Combine(target,"Nuevo.txt"))&&!Directory.Exists(Path.Combine(target,"Vacia")),"unselected updates, new files and empty directories remain untouched");
   Assert(!File.Exists(Path.Combine(target,"Extras","uno.txt"))&&File.Exists(Path.Combine(target,"Extras","dos.txt")),"selected deletion does not delete an unselected sibling");
   Assert(File.Exists(Path.Combine(target,"CambioTipo"))&&File.Exists(Path.Combine(target,"NoCambiarTipo","keep.txt")),"unselected type conflicts preserve existing target topology");
   var refreshed=Pump(MirrorPlanner.PreviewAsync(manifest,target));var group=MirrorPlanner.SelectChanges(refreshed,new HashSet<string>{Path.Combine("CambioTipo","uno.txt"),"Vacia"});var groupJob=new MirrorJob(group,false,logs);Pump(groupJob.RunAsync());
   Assert(groupJob.Status.State=="Completado"&&File.Exists(Path.Combine(target,"CambioTipo","uno.txt"))&&File.Exists(Path.Combine(target,"CambioTipo","dos.txt"))&&Directory.Exists(Path.Combine(target,"Vacia")),"selected type replacement and empty directory execute correctly");
   var before=MirrorPlanner.Scan(target);var nothing=MirrorPlanner.SelectChanges(Pump(MirrorPlanner.PreviewAsync(manifest,target)),new HashSet<string>());var noJob=new MirrorJob(nothing,false,logs);Pump(noJob.RunAsync());Assert(noJob.Status.State=="Completado"&&MirrorPlanner.Same(before,MirrorPlanner.Scan(target)),"empty partial selection is a metadata-preserving no-op");
   bool rejected=false;try{MirrorPlanner.SelectChanges(full,new HashSet<string>{"../outside"});}catch(IOException){rejected=true;}Assert(rejected,"selection cannot inject paths outside the analyzed manifest");
   var remaining=Pump(MirrorPlanner.PreviewAsync(manifest,target));var complete=new MirrorJob(remaining,false,logs);Pump(complete.RunAsync());Assert(complete.Status.State=="Completado"&&!File.Exists(Path.Combine(target,"Extras","dos.txt"))&&File.Exists(Path.Combine(target,"Nuevo.txt")),"full mirror still converges after a partial synchronization");
   var low=Dir("LOW-SPACE");Write(low,"remove.txt",new string('x',512));Write(low,"keep.txt",new string('y',512));var lowFull=Pump(MirrorPlanner.PreviewAsync(manifest,low,freeBytes:_=>0));
   var noPrune=MirrorPlanner.SelectChanges(lowFull,new HashSet<string>{"Nuevo.txt"});var denied=new MirrorJob(noPrune,true,logs,freeBytes:_=>0);Pump(denied.RunAsync());Assert(denied.Status.State=="Error"&&File.Exists(Path.Combine(low,"remove.txt"))&&File.Exists(Path.Combine(low,"keep.txt")),"unselected deletions cannot be used to claim free space or removed early");
   var selectedPrune=MirrorPlanner.SelectChanges(lowFull,new HashSet<string>{"Nuevo.txt","remove.txt"});Assert(selectedPrune.ReclaimableBytes==512&&selectedPrune.Fits(true),"partial space budget counts only approved selected deletions");var early=new MirrorJob(selectedPrune,true,logs,freeBytes:_=>0);Pump(early.RunAsync());Assert(early.Status.State=="Completado"&&!File.Exists(Path.Combine(low,"remove.txt"))&&File.Exists(Path.Combine(low,"keep.txt"))&&File.Exists(Path.Combine(low,"Nuevo.txt")),"partial early pruning preserves unselected files and copies only the selection");
   Console.WriteLine($"{passed} comparison and partial mirror assertions passed. Temporary folders only.");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{Directory.Delete(root,true);}
 }
}
