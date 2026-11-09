using DobleCommander;

static class InputTests {
 static int passed;
 static void Assert(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);passed++;}
 static IEnumerable<Control> Children(Control c){yield return c;foreach(Control child in c.Controls)foreach(var nested in Children(child))yield return nested;}
 static void Pump(Task task){var end=DateTime.UtcNow.AddSeconds(40);while(!task.IsCompleted){if(DateTime.UtcNow>end)throw new Exception("Timeout");Application.DoEvents();Thread.Sleep(1);}task.GetAwaiter().GetResult();}
 static void Until(Func<bool> predicate){var end=DateTime.UtcNow.AddSeconds(20);while(!predicate()){if(DateTime.UtcNow>end)throw new Exception("Timeout");Application.DoEvents();Thread.Sleep(1);}}
 static bool Reject(Action action){try{action();return false;}catch(IOException){return true;}}
 static int[] Selected(DeviceList list)=>list.SelectedIndices.Cast<int>().Order().ToArray();
 static void Drop(Control target,string path){var data=new DataObject(DataFormats.FileDrop,new[]{path});var args=new DragEventArgs(data,0,0,0,DragDropEffects.Copy,DragDropEffects.Copy);typeof(Control).GetMethod("OnDragDrop",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(target,new object[]{args});}
 [STAThread] static int Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
  var root=Path.Combine(Path.GetTempPath(),"Rcopies-input-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  try{
   string Dir(string name){var p=Path.GetFullPath(Path.Combine(root,name));Directory.CreateDirectory(p);return p;}
   StorageUnit? Unit(string p)=>p.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)?new(p,Path.GetFileName(p),DriveType.Removable,16_000_000_000,8_000_000_000,"exFAT"):null;
   using(var list=new DeviceList()){for(int i=0;i<8;i++)list.Items.Add(new ListViewItem("Unit "+i));_=list.Handle;
    list.BeginRangeSelection(1);list.MoveRangeSelection(4);list.EndRangeSelection();Assert(Selected(list).SequenceEqual(new[]{1,2,3,4}),"left drag selects a contiguous range without keyboard modifiers");
    list.BeginRangeSelection(4);list.MoveRangeSelection(1);list.EndRangeSelection();Assert(Selected(list).SequenceEqual(new[]{1,2,3,4}),"drag selection works upwards as well as downwards");
    list.BeginRangeSelection(6,true);list.EndRangeSelection();Assert(Selected(list).SequenceEqual(new[]{1,2,3,4,6}),"Ctrl click adds a separate destination without losing the dragged range");
    list.BeginRangeSelection(2,true);list.EndRangeSelection();Assert(Selected(list).SequenceEqual(new[]{1,3,4,6}),"Ctrl click removes an already selected destination");
    list.BeginRangeSelection(0);list.MoveRangeSelection(5);list.MoveRangeSelection(2);list.EndRangeSelection();Assert(Selected(list).SequenceEqual(new[]{0,1,2}),"dragging back shrinks the range instead of leaving unwanted destinations selected");
    list.BeginRangeSelection(4);list.EndRangeSelection();Assert(Selected(list).SequenceEqual(new[]{4}),"plain click begins a new selection");
    list.BeginRangeSelection(7,false,true);list.EndRangeSelection();Assert(Selected(list).SequenceEqual(new[]{4,5,6,7}),"Shift selection remains available alongside mouse dragging");
   }
   Assert(StorageUnits.Eligible(DriveType.Removable,1_000_000_000)&&StorageUnits.Eligible(DriveType.Fixed,64_000_000_000),"small ready storage units are accepted without the old 120 GB limit");
   Assert(!StorageUnits.Eligible(DriveType.CDRom,500_000_000_000)&&!StorageUnits.Eligible(DriveType.Removable,0),"optical and invalid-capacity devices remain excluded");
   var originVolume=Dir("origin-volume");var folder=Dir("origin-volume/payload");var file=Path.Combine(folder,"sample.txt");File.WriteAllText(file,"Dropped source data Ã±");var a=Path.Combine(root,"unit-a");var b=Path.Combine(root,"unit-b");
   using(var form=new LiteWindow(false,Dir("logs"),Unit,()=>true)){
    form.AddPaths([originVolume,a,b]);var before=form.SelectedDestinations.Where(p=>p!=originVolume).Order().ToArray();Drop(Children(form).OfType<DeviceList>().Single(),folder);Pump(form.LastDropOperation);
    Assert(form.CurrentSource==folder,"dropping a folder over the destination list automatically chooses it as source");
    Assert(form.SelectedDestinations.Order().SequenceEqual(before)&&form.Targets.All(t=>t.Path!=originVolume),"source drop preserves destination selection and removes the source unit from destinations");
    Assert(File.ReadAllText(Path.Combine(a,"payload","sample.txt"))==File.ReadAllText(file)&&File.ReadAllText(Path.Combine(b,"payload","sample.txt"))==File.ReadAllText(file),"source drop automatically starts and completes copying to the existing selected destinations");
    form.HandleDrop([file],true);Assert(form.CurrentSource==file&&form.SelectedDestinations.Order().SequenceEqual(before),"file drop into the source area preserves selected destinations");
    string previous=form.CurrentSource;Assert(Reject(()=>form.HandleDrop([Path.Combine(root,"missing")]))&&form.CurrentSource==previous,"unavailable drops produce a controlled error without changing source");
    Assert(Reject(()=>form.HandleDrop([folder,file]))&&form.CurrentSource==previous,"multiple source items are rejected atomically instead of treated as destination folders");
    var list=Children(form).OfType<DeviceList>().Single();var menu=list.ContextMenuStrip!;Assert(menu.ShowImageMargin&&menu.Items.Cast<ToolStripItem>().All(item=>item.Image!=null),"right-click actions have their own icons and visible icon margin");
    Assert(Children(form).All(c=>!c.Text.Contains("120 GB",StringComparison.OrdinalIgnoreCase)),"old minimum-capacity text is absent from the interface");
   }
   using(var form=new LiteWindow(false,Dir("root-logs"),Unit,()=>true)){var systemRoot=Path.GetPathRoot(Environment.SystemDirectory)!;Pump(form.HandleDropAsync([systemRoot]));Assert(StorageUnits.Normalize(form.CurrentSource)==StorageUnits.Normalize(systemRoot)&&!form.Busy&&!form.CopyPrepared,"a drop without selected destinations only establishes the source");}
   using(var form=new LiteWindow(false,Dir("file-drop-logs"),Unit,()=>true)){var target=Path.Combine(root,"file-drop-target");form.AddPaths([target]);Drop(Children(form).OfType<DeviceList>().Single(),file);Pump(form.LastDropOperation);Assert(File.ReadAllText(Path.Combine(target,"sample.txt"))==File.ReadAllText(file),"dropping a file also starts copying automatically");}
   using(var form=new LiteWindow(false,Dir("focus-logs"),Unit,()=>true)){form.AddPaths([originVolume,a,b]);form.Show();Application.DoEvents();var input=Children(form).OfType<TextBox>().Single();input.Focus();input.Text=folder;var list=Children(form).OfType<DeviceList>().Single();list.BeginRangeSelection(2);list.EndRangeSelection();Assert(form.SelectedDestinations.SequenceEqual(new[]{b}),"selection stays attached to the clicked row when source validation removes another row on focus change");form.Close();}
   var large=Dir("large-source");using(var stream=File.Create(Path.Combine(large,"large.bin")))stream.SetLength(128L*1024*1024);
   using(var form=new LiteWindow(false,Dir("active-logs"),Unit,()=>true)){form.SetSource(large);form.AddPaths([Path.Combine(root,"active-target")]);var task=form.StartCopyAsync();Until(()=>form.CopyPrepared&&form.Targets.Any(t=>t.Copy?.Status.Done>0)||task.IsCompleted);form.PauseSelected(true);Assert(form.Busy&&Reject(()=>form.HandleDrop([folder]))&&form.CurrentSource==large,"source drop cannot alter the origin of an active copy");Pump(form.CancelAsync(true));Pump(task);}
   if(args.Length==2&&args[0]=="--render"){
    using var form=new LiteWindow(false,Dir("preview-logs"),Unit,()=>true);form.HandleDrop([folder]);form.AddPaths([a,b,Path.Combine(root,"unit-c")]);form.Show();Application.DoEvents();var list=Children(form).OfType<DeviceList>().Single();list.BeginRangeSelection(0);list.MoveRangeSelection(1);list.EndRangeSelection();using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);list.Items[0].Focused=true;var menu=list.ContextMenuStrip!;menu.Show(list,new Point(65,35));Application.DoEvents();using var menuImage=new Bitmap(menu.Width,menu.Height);menu.DrawToBitmap(menuImage,new Rectangle(Point.Empty,menuImage.Size));menuImage.Save(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!,"Rcopies-Menu.png"),System.Drawing.Imaging.ImageFormat.Png);menu.Close();form.Close();
   }
   Console.WriteLine($"{passed} input workflow assertions passed. Only temporary test folders were written.");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{if(Directory.Exists(root))Directory.Delete(root,true);}
 }
}
