namespace DobleCommander;
static class Program {
 [STAThread] static void Main(){ApplicationConfiguration.Initialize();try{ResourceBudget.Initialize();Application.Run(new LiteWindow());}catch(Exception e){MessageBox.Show(e.Message,"Rcopies");}}
}

sealed partial class LiteWindow : Form {
 readonly TextBox source=new(){Dock=DockStyle.Fill,PlaceholderText="Elige o arrastra la carpeta matriz o un archivo…",AllowDrop=true};
 readonly Label sourceInfo=new(){Dock=DockStyle.Fill,AutoEllipsis=true,Text="Una matriz → varios destinos en paralelo"};
 readonly DeviceList devices=new();readonly MemoryMeter memoryMeter=new();readonly Func<bool> licensed;bool fullLicense;readonly Label heading=new(){Dock=DockStyle.Fill,Padding=new(12,6,0,0),Font=new Font("Segoe UI",13,FontStyle.Bold)};
 readonly ComboBox policy=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=290};
 readonly CheckBox contents=new(){Text="Solo contenido",Checked=false,AutoSize=true};
 readonly CheckBox detect=new(){Text="Detectar unidades nuevas",Checked=true,AutoSize=true};
 readonly Label summary=new(){Dock=DockStyle.Fill,AutoEllipsis=true};
 readonly Label selection=new(){Dock=DockStyle.Top,Height=25,TextAlign=ContentAlignment.MiddleLeft};
 readonly AccentProgressBar global=new(){Dock=DockStyle.Bottom,Height=9};
 readonly System.Windows.Forms.Timer timer=new(){Interval=250};
 readonly List<Control> configuration=[];
 readonly Button copy=new LiteButton(){Text="▶  Copiar",AutoSize=true},join=new LiteButton(){Text="+ Al lote",AutoSize=true},cancel=new LiteButton(){Text="Cancelar todo",AutoSize=true};
 readonly VolumeMonitor monitor=new();
 readonly Queue<(DeviceVolume Volume,CopyBatch Batch,IDisposable Reservation)> arrivals=[];
 readonly HashSet<string> offered=new(StringComparer.OrdinalIgnoreCase);
 readonly bool discover;readonly string logs;readonly Func<string,StorageUnit?> storageReader;
 CopyBatch? batch;bool busy,scanning,offering;DateTime nextScan;CancellationTokenSource? planning;
 internal bool Busy=>busy;
 internal bool CopyPrepared=>planning==null&&batch?.IsOpen==true;
 internal string[] SelectedDestinations=>devices.SelectedItems.Cast<ListViewItem>().Select(i=>((TargetRow)i.Tag!).Path).Where(p=>!IsOrigin(p)).ToArray();
 internal IEnumerable<TargetRow> Targets=>devices.Items.Cast<ListViewItem>().Select(i=>(TargetRow)i.Tag!);
 internal string ProgressSummary=>summary.Text;
 internal int GlobalProgress=>global.Value;
 internal static string Bytes(long n)=>n>=1073741824?$"{n/1073741824d:F2} GB":n>=1048576?$"{n/1048576d:F1} MB":$"{n:N0} B";
 internal LiteWindow(bool discover=true,string? logDirectory=null,Func<string,StorageUnit?>? storageReader=null,Func<bool>? licensed=null,IUnitEjector? ejector=null){
  this.licensed=licensed??LicenseStore.IsLicensed;this.ejector=ejector??new WindowsUnitEjector();this.discover=discover;this.storageReader=storageReader??StorageUnits.Read;logs=logDirectory??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Rcopies","Logs");
  Text="Rcopies";Icon=Branding.AppIcon;Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterScreen;
  var area=Screen.PrimaryScreen?.WorkingArea??new Rectangle(0,0,1200,800);Size=new(Math.Min(1180,area.Width-32),Math.Min(760,area.Height-32));MinimumSize=new(900,590);
  var layout=new TableLayoutPanel(){Dock=DockStyle.Fill,Padding=new(12),ColumnCount=1,RowCount=7};
  foreach(var height in new[]{70,84,40,25,0,80,92})layout.RowStyles.Add(new(height==0?SizeType.Percent:SizeType.Absolute,height==0?100:height));
  var hero=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=3};hero.ColumnStyles.Add(new(SizeType.Absolute,64));hero.ColumnStyles.Add(new(SizeType.Percent,100));hero.ColumnStyles.Add(new(SizeType.Absolute,120));
  hero.Controls.Add(new PictureBox(){Image=Branding.Logo,SizeMode=PictureBoxSizeMode.Zoom,Dock=DockStyle.Fill},0,0);
  hero.Controls.Add(heading,1,0);
  var about=new LiteButton(){Text="Acerca de",Dock=DockStyle.Fill,Margin=new(4,12,0,12)};about.Click+=(_,_)=>{using var dialog=new AboutDialog();dialog.ShowDialog(this);};hero.Controls.Add(about,2,0);layout.Controls.Add(hero,0,0);
  var origin=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=4,RowCount=3,Padding=new(0,4,0,4)};origin.ColumnStyles.Add(new(SizeType.Percent,100));for(int i=0;i<3;i++)origin.ColumnStyles.Add(new(SizeType.Absolute,128));origin.RowStyles.Add(new(SizeType.Absolute,19));origin.RowStyles.Add(new(SizeType.Absolute,34));origin.RowStyles.Add(new(SizeType.Absolute,22));
  origin.Controls.Add(new Label(){Text="ORIGEN / MATRIZ",AutoSize=true,ForeColor=Theme.Muted},0,0);origin.SetColumnSpan(origin.GetControlFromPosition(0,0)!,4);origin.Controls.Add(source,0,1);
  OriginButton("Carpeta…",1,async()=>{using var dialog=new FolderBrowserDialog();if(dialog.ShowDialog(this)==DialogResult.OK)await SelectSourceFromUiAsync(dialog.SelectedPath);else ClearFinishedProgress();});
  OriginButton("Archivo…",2,async()=>{using var dialog=new OpenFileDialog();if(dialog.ShowDialog(this)==DialogResult.OK)await SelectSourceFromUiAsync(dialog.FileName);else ClearFinishedProgress();});
  OriginButton("Como matriz",3,async()=>{if(devices.SelectedItems.Count==1)await SelectSourceFromUiAsync(((TargetRow)devices.SelectedItems[0].Tag!).Path);});
  origin.Controls.Add(sourceInfo,0,2);origin.SetColumnSpan(sourceInfo,4);layout.Controls.Add(origin,0,1);
  void OriginButton(string label,int column,Func<Task> action){var button=new LiteButton(){Text=label,Dock=DockStyle.Fill,Margin=new(4,0,0,0)};button.Click+=async(_,_)=>await RunUiAsync(action);origin.Controls.Add(button,column,1);configuration.Add(button);}
  policy.Items.AddRange(["Si existe: omitir","Si existe: sobrescribir","Si existe: comparar tamaño y fecha"]);policy.SelectedIndex=2;policy.DrawMode=DrawMode.OwnerDrawFixed;policy.DrawItem+=(_,e)=>{using var fill=new SolidBrush(Theme.Raised);e.Graphics.FillRectangle(fill,e.Bounds);if(e.Index>=0)TextRenderer.DrawText(e.Graphics,policy.Items[e.Index]?.ToString()??string.Empty,Font,e.Bounds,Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};
  var options=new FlowLayoutPanel(){Dock=DockStyle.Fill,WrapContents=false,Padding=new(0,4,0,0)};options.Controls.AddRange([policy,contents,detect]);layout.Controls.Add(options,0,2);configuration.AddRange([source,policy,contents]);
  layout.Controls.Add(selection,0,3);layout.Controls.Add(devices,0,4);
  var progress=new Panel(){Dock=DockStyle.Fill,Padding=new(0,5,0,6)};progress.Controls.Add(summary);progress.Controls.Add(global);progress.Controls.Add(memoryMeter);layout.Controls.Add(progress,0,5);
  var actions=new TableLayoutPanel(){Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};actions.RowStyles.Add(new(SizeType.Absolute,40));actions.RowStyles.Add(new(SizeType.Absolute,44));
  var manage=new FlowLayoutPanel(){Dock=DockStyle.Fill,WrapContents=false};
  AddButton(manage,"+ Unidad",PromptDestination);AddButton(manage,"Unidades",async()=>await ReloadUnitsAsync());AddButton(manage,"Quitar",RemoveSelected);AddButton(manage,"Licencia",()=>{LicenseStore.Manage(this);RefreshAccess();});
  AddButton(manage,"Todas",()=>SelectAllTargets(true));AddButton(manage,"Ninguna",()=>SelectAllTargets(false));actions.Controls.Add(manage,0,0);
  var tools=new FlowLayoutPanel(){Dock=DockStyle.Fill,WrapContents=false};tools.Controls.AddRange([copy,join]);
  AddButton(tools,"Ⅱ  Pausar",()=>PauseSelected(false));AddButton(tools,"▶  Reanudar",()=>ResumeSelected(false));AddButton(tools,"Cancelar",async()=>await CancelAsync(false));tools.Controls.Add(cancel);
  actions.Controls.Add(tools,0,1);layout.Controls.Add(actions,0,6);Controls.Add(layout);Controls.Add(new BrandFooter());
  copy.Click+=async(_,_)=>await RunUiAsync(()=>StartCopyAsync());join.Click+=(_,_)=>{try{JoinSelectedTargets();}catch(Exception e){MessageBox.Show(this,e.Message,"Incorporar destino");}};
  cancel.Click+=async(_,_)=>await CancelAsync(true);
  devices.SelectedIndexChanged+=(_,_)=>UpdateSelection();
  ConfigureDrop(devices,false);ConfigureDrop(source,true);ConfigureDrop(origin,true);ConfigureDrop(this,true);
  source.Leave+=(_,_)=>UpdateSourceInfo();source.KeyDown+=async(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await RunUiAsync(()=>SelectSourceFromUiAsync(ReadSource()));}};
  Theme.Apply(this);copy.BackColor=Theme.Purple;copy.FlatAppearance.BorderColor=Theme.Purple;
  SetupContextMenu();RefreshAccess();
  timer.Tick+=(_,_)=>{if(DateTime.UtcNow>=nextLicenseCheck){RefreshAccess();nextLicenseCheck=DateTime.UtcNow.AddSeconds(20);}memoryMeter.Invalidate();RefreshProgress();if(discover)ScanDevices();};timer.Start();
  Shown+=async(_,_)=>{if(discover)await ReloadUnitsAsync();};
  FormClosing+=(_,e)=>{if(busy||offering||ejecting){e.Cancel=true;MessageBox.Show(this,"Cancela o espera las operaciones antes de cerrar.","Operación en curso");}};UpdateSelection();
 }
 void AddButton(Control parent,string text,Action action){var button=new LiteButton(){Text=text,AutoSize=true};button.Click+=(_,_)=>action();parent.Controls.Add(button);}
  void UpdateSelection(){int n=SelectedDestinations.Length;selection.Text=$"DESTINOS  ·  {n} seleccionados de {devices.Items.Count}  ·  Clic y arrastre: rango · Ctrl + clic: añadir o quitar";copy.Enabled=!busy&&!ejecting&&n>0;join.Enabled=fullLicense&&!ejecting&&busy&&!MirrorsRunning&&batch?.IsOpen==true;cancel.Enabled=busy||Targets.Any(t=>t.ShowProgress);}
 void SetBusy(bool value){busy=value;foreach(var control in configuration)control.Enabled=!value&&!ejecting;UpdateSelection();}
 bool IsOrigin(string destination){try{return !string.IsNullOrWhiteSpace(source.Text)&&(SourcePool.SameOrInside(destination,source.Text)||SourcePool.SameOrInside(source.Text,destination));}catch{return false;}}
 internal void SetSource(string path){if(busy||ejecting)throw new IOException("El origen está bloqueado durante la operación.");source.Text=Path.GetFullPath(path);UpdateSourceInfo();}
 void UpdateSourceInfo(){
  try{var path=Path.GetFullPath(source.Text);sourceInfo.Text=(Directory.Exists(path)?"Carpeta matriz: ":File.Exists(path)?"Archivo: ":"Ruta no disponible: ")+path;foreach(var item in devices.Items.Cast<ListViewItem>().ToArray())if(IsOrigin(((TargetRow)item.Tag!).Path)&&!IsRunning((TargetRow)item.Tag!))devices.Items.Remove(item);}
  catch{sourceInfo.Text="Elige una carpeta o archivo como origen.";}UpdateSelection();
 }
 internal void AddPaths(IEnumerable<string> paths,bool select=true,bool existingOnly=false){
  var errors=new List<string>();
  foreach(var value in paths)try{
   var unit=StorageUnits.Require(value,storageReader);var full=StorageUnits.Normalize(unit.Root);if(IsOrigin(full))continue;
   var found=devices.Items.Cast<ListViewItem>().FirstOrDefault(i=>((TargetRow)i.Tag!).Path.Equals(full,StringComparison.OrdinalIgnoreCase));
   if(found!=null){if(select)found.Selected=true;continue;}
   var row=new TargetRow(){Path=full,Name=unit.Label+" ("+full.TrimEnd('\\')+")",Details=Describe(unit)};
   var item=new ListViewItem(row.Name){Tag=row,ImageKey="folder",ToolTipText=full};for(int i=0;i<5;i++)item.SubItems.Add("");devices.Items.Add(item);
   _=devices.Handle;if(select)item.Selected=true;if(IsHandleCreated)_=LoadDetailsAsync(item);
  }catch(Exception e){errors.Add(e.Message);}
  UpdateSelection();if(errors.Count>0)throw new IOException(string.Join(Environment.NewLine,errors.Distinct()));
 }
 static string Describe(StorageUnit unit)=>$"{(unit.Type==DriveType.Fixed?"Disco local":"Extraíble")} · {unit.FileSystem} · Libre {Bytes(unit.Free)} / {Bytes(unit.Capacity)}";
 async Task LoadDetailsAsync(ListViewItem item){
  if(item.Tag is not TargetRow row)return;
  try{var data=await Task.Run(()=>{var unit=StorageUnits.Require(row.Path,storageReader);return(unit,icon:WindowsIcons.ForPath(row.Path));});
   using var icon=data.icon;if(IsDisposed||item.ListView!=devices)return;row.Name=data.unit.Label+" ("+row.Path.TrimEnd('\\')+")";row.Details=Describe(data.unit);item.Text=row.Name;if(icon!=null)item.ImageKey=devices.Icons.PathKey(row.Path,icon);item.ToolTipText=row.Path+"\n"+row.Details;devices.Invalidate();
  }catch(IOException){}catch(UnauthorizedAccessException){}catch(ObjectDisposedException){}
 }
 async Task ReloadUnitsAsync(){try{var units=await Task.Run(StorageUnits.Scan);var roots=units.Select(u=>u.Root).ToHashSet(StringComparer.OrdinalIgnoreCase);foreach(var item in devices.Items.Cast<ListViewItem>().ToArray())if(item.Tag is TargetRow row&&!IsRunning(row)&&!roots.Contains(row.Path))devices.Items.Remove(item);AddPaths(roots,false);foreach(ListViewItem item in devices.Items)_=LoadDetailsAsync(item);}catch(Exception e){MessageBox.Show(this,e.Message,"Unidades");}}
 void PromptDestination(){using var dialog=new Form(){Text="Añadir unidad de almacenamiento",Size=new(620,185),StartPosition=FormStartPosition.CenterParent,Font=Font,Padding=new(14),MinimizeBox=false,MaximizeBox=false};var path=new TextBox(){Dock=DockStyle.Top,PlaceholderText="Raíz de una unidad, por ejemplo E:\\"};var buttons=new FlowLayoutPanel(){Dock=DockStyle.Bottom,Height=42};var add=new LiteButton(){Text="Añadir",DialogResult=DialogResult.OK,AutoSize=true};var stop=new LiteButton(){Text="Cancelar",DialogResult=DialogResult.Cancel,AutoSize=true};buttons.Controls.AddRange([add,stop]);dialog.Controls.Add(path);dialog.Controls.Add(buttons);dialog.AcceptButton=add;dialog.CancelButton=stop;Theme.Apply(dialog);if(dialog.ShowDialog(this)==DialogResult.OK)try{AddPaths([path.Text]);}catch(Exception e){MessageBox.Show(this,e.Message,"Destino");}else ClearFinishedProgress();}
 void RemoveSelected(){foreach(var item in devices.SelectedItems.Cast<ListViewItem>().ToArray())if(item.Tag is TargetRow row&&!IsRunning(row))devices.Items.Remove(item);UpdateSelection();}
 void SelectAllTargets(bool value){_=devices.Handle;foreach(ListViewItem item in devices.Items)item.Selected=value&&!IsOrigin(((TargetRow)item.Tag!).Path);UpdateSelection();}
 internal void SelectTarget(string path,bool value){_=devices.Handle;var full=StorageUnits.Normalize(path);var item=devices.Items.Cast<ListViewItem>().Single(i=>((TargetRow)i.Tag!).Path.Equals(full,StringComparison.OrdinalIgnoreCase));item.Selected=value&&!IsOrigin(full);UpdateSelection();}
 internal void ConfigureCopy(bool contentsOnly,Conflict conflict,bool verification){if(busy)throw new IOException("Hay un lote activo.");contents.Checked=contentsOnly;policy.SelectedIndex=(int)conflict;}
 string ReadSource(){var text=source.Text.Trim().Trim('"');if(!Path.IsPathFullyQualified(text)||!Directory.Exists(text)&&!File.Exists(text))throw new IOException("Elige un origen existente con ruta completa.");return Path.GetFullPath(text);}
 void ValidateTargets(Plan plan,string[] paths){foreach(var path in paths)StorageUnits.Require(path,storageReader);if(paths.Length==0)throw new IOException("Selecciona al menos una unidad destino.");foreach(var p in paths)plan.Validate(p);foreach(var a in paths)foreach(var b in paths)if(a!=b&&(SourcePool.SameOrInside(a,b)||SourcePool.SameOrInside(b,a)))throw new IOException("Los destinos no pueden solaparse.");}
 void Register(CopyJob job){var row=Targets.Single(t=>t.Path.Equals(Path.TrimEndingDirectorySeparator(job.Destination),StringComparison.OrdinalIgnoreCase));row.Copy=job;row.Mirror=null;row.ShowProgress=true;}
 internal void JoinSelectedTargets(){RefreshAccess();if(MirrorsRunning)throw new IOException("Espera a que terminen los espejos para incorporar otra unidad.");if(ejecting)throw new IOException("Espera a que termine la desconexión.");if(batch?.IsOpen!=true)throw new IOException("No hay un lote de copia abierto.");var paths=SelectedDestinations.Where(p=>!batch.Jobs.Any(j=>Path.TrimEndingDirectorySeparator(j.Destination).Equals(p,StringComparison.OrdinalIgnoreCase))).ToArray();if(paths.Length==0)throw new IOException("Selecciona una unidad nueva para incorporarlo al lote.");LicensePolicy.Check(fullLicense,batch.Jobs.Length+paths.Length);ValidateTargets(batch.Plan,batch.Jobs.Select(j=>j.Destination).Concat(paths).ToArray());foreach(var path in paths)batch.Sources.SetEnabled(path,Targets.Single(t=>t.Path.Equals(path,StringComparison.OrdinalIgnoreCase)).AllowAuxiliary);foreach(var path in paths)Register(batch.AddDestination(path,true));RefreshProgress();}
 async Task RunUiAsync(Func<Task> action){try{await action();}catch(OperationCanceledException){ClearFinishedProgress();}catch(Exception e){MessageBox.Show(this,e.Message,"Rcopies");ClearFinishedProgress();}}
 internal async Task ExecuteMirrorAsync(MirrorPreview[] previews,bool earlyPrune){
  RefreshAccess();LicensePolicy.Check(fullLicense,previews.Length);if(ejecting)throw new IOException("Espera a que termine la desconexión.");
  // Only called after the production confirmation dialog, or by the isolated test harness.
  foreach(var preview in previews)StorageUnits.Require(MirrorRow(preview.Destination).Path,storageReader);
  ResetAllProgress();batch=null;var tasks=new List<Task>();
  try{foreach(var preview in previews){var row=MirrorRow(preview.Destination);row.Mirror=new MirrorJob(preview,earlyPrune&&MirrorPlanner.RequiresEarlyPrune(preview,previews),logs,VolumeMonitor.IdentityFor,VolumeMonitor.FreeBytesFor);row.ShowProgress=true;tasks.Add(row.Mirror.RunAsync());}await Task.WhenAll(tasks);}
  catch{foreach(var row in Targets)row.Mirror?.Cancel();await Task.WhenAll(tasks);throw;}finally{RefreshProgress();}
 }
 static Task Completion(TargetRow row)=>row.Copy is {} copy?copy.CopyCompleted:row.Mirror?.Completion??Task.CompletedTask;
 bool IsRunning(TargetRow row)=>row.Copy is {} copy?!(batch?.IsJobFinished(copy)??copy.CopyCompleted.IsCompleted):row.Mirror is {} mirror&&!mirror.Completion.IsCompleted;
 IEnumerable<TargetRow> Chosen(bool all)=>all?Targets:devices.SelectedItems.Cast<ListViewItem>().Select(i=>(TargetRow)i.Tag!);
 internal void PauseSelected(bool all){foreach(var row in Chosen(all||devices.SelectedItems.Count==0)){row.Copy?.Pause();row.Mirror?.Pause();}RefreshProgress();}
 internal void ResumeSelected(bool all){foreach(var row in Chosen(all||devices.SelectedItems.Count==0)){row.Copy?.Resume();row.Mirror?.Resume();}RefreshProgress();}
 internal async Task CancelAsync(bool all){
  if(all){planning?.Cancel();batch?.CancelAll();}var chosen=Chosen(all).ToArray();foreach(var row in chosen){row.Copy?.Cancel();row.Mirror?.Cancel();}
  await Task.WhenAll(chosen.Select(row=>row.Copy is {} copy?batch?.WaitForJobAsync(copy)??copy.CopyCompleted:Completion(row)));
  foreach(var row in chosen)ClearRow(row);ClearFinishedProgress();
 }
 void ClearRow(TargetRow row){row.ShowProgress=false;row.State=row.ProgressText=row.Speed=row.Eta=row.Current="";row.Remaining=null;row.Fraction=0;}
 internal void ClearFinishedProgress(){foreach(var row in Targets.Where(t=>!IsRunning(t)))ClearRow(row);RefreshProgress();}
 void ResetAllProgress(){foreach(var row in Targets){ClearRow(row);row.Copy=null;row.Mirror=null;}summary.Text="";global.Value=0;}
 internal void RefreshProgress(){
  if(IsDisposed)return;double done=0,total=0,speed=0;long written=0;int visible=0;var helpers=batch?.Sources.HelperRoots??[];var auditing=batch?.Sources.AuditingRoots??[];
  foreach(var row in Targets){
   if(!row.ShowProgress)continue;
   if(row.Copy is {} copy){var s=copy.Status;if(s.State=="Cancelado"&&!IsRunning(row)){ClearRow(row);continue;}row.State=helpers.Contains(copy.Destination,StringComparer.OrdinalIgnoreCase)?"Origen auxiliar":auditing.Contains(copy.Destination,StringComparer.OrdinalIgnoreCase)?"Preparando auxiliar":s.State;row.Fraction=s.Total==0?(s.State=="Completado"?1:0):Math.Clamp(s.Done/(double)s.Total,0,1);row.ProgressText=$"{row.Fraction*100:F1}% · {Bytes(s.Done)}";double rate=s.Seconds>0?s.Written/s.Seconds:0;row.Speed=s.State=="Copiando"?$"{Bytes((long)rate)}/s":"—";row.Current=s.Current+(copy.ReadSource.Length>0?" · Lee: "+copy.ReadSource:"");UpdateEta(row,s.Done,s.Total,s.Seconds,s.State,IsRunning(row));written+=s.Written;double weight=Math.Max(1,s.Total);total+=weight;done+=row.Fraction*weight;if(s.State=="Copiando")speed+=rate;
   }else if(row.Mirror is {} mirror){var s=mirror.Status;if(s.State=="Cancelado"&&mirror.Completion.IsCompleted){ClearRow(row);continue;}row.State=s.State;row.Fraction=mirror.Progress;row.ProgressText=$"{row.Fraction*100:F1}% · {Bytes(s.Written)}";double rate=s.Seconds>0?s.Written/s.Seconds:0;row.Speed=s.State=="Copiando"?$"{Bytes((long)rate)}/s":"—";row.Current=s.Current+(s.Recovery!=null&&s.State is "Error" or "Cancelado"?" · Recuperación: "+s.Recovery:"");UpdateEta(row,s.Done,s.Total,s.Seconds,s.State,IsRunning(row));written+=s.Written;double weight=Math.Max(1,s.Total);total+=weight;done+=row.Fraction*weight;if(s.State=="Copiando")speed+=rate;
   }visible++;
  }
  if(visible==0){global.Value=0;if(planning==null)summary.Text="";}else{global.Value=total==0?0:(int)Math.Clamp(done*100/total,0,100);summary.Text=$"Global {global.Value}%   ·   {visible} destino(s)   ·   Escritos {Bytes(written)}   ·   {Bytes((long)speed)}/s   ·   Auxiliares {helpers.Length}/4   ·   Restante {GlobalEta()}";}
  foreach(ListViewItem item in devices.Items)if(item.Tag is TargetRow row){item.SubItems[1].Text=row.State;item.SubItems[2].Text=row.ProgressText;item.SubItems[3].Text=row.Speed;item.SubItems[4].Text=row.Eta;item.SubItems[5].Text=row.ShowProgress?row.Current:row.Path;item.ToolTipText=row.Path+"\n"+row.Details+(row.ShowProgress?"\n"+row.State+"\n"+row.Current:"");}
  devices.Invalidate();UpdateSelection();
 }
 static void UpdateEta(TargetRow row,double done,double total,double seconds,string state,bool running){
  row.Remaining=null;
  if(state=="Pausado"){row.Eta="Pausado";return;}
  if(state is "Error" or "Cancelado"){row.Eta="—";return;}
  if(!running){row.Eta=RemainingTime.Format(0);row.Remaining=0;return;}
  if(row.State=="Preparando auxiliar"){row.Eta="Preparando…";return;}
  row.Remaining=RemainingTime.Estimate(done,total,seconds);row.Eta=row.Remaining is {} estimate?RemainingTime.Format(estimate):done>=total?"Finalizando…":"Calculando…";
 }
 string GlobalEta(){var rows=Targets.Where(t=>t.ShowProgress&&IsRunning(t)).ToArray();if(rows.Length==0)return RemainingTime.Format(0);if(rows.All(t=>t.Eta=="Pausado"))return "Pausado";if(rows.Any(t=>t.Remaining==null))return rows.Any(t=>t.Eta=="Pausado")?"Hay pausas":"Calculando…";return RemainingTime.Format(rows.Max(t=>t.Remaining??0));}
 async void ScanDevices(){
  var current=batch;if(!fullLicense||ejecting||MirrorsRunning||!detect.Checked||scanning||current?.IsOpen!=true||DateTime.UtcNow<nextScan)return;scanning=true;nextScan=DateTime.UtcNow.AddSeconds(2);
  try{var volumes=await Task.Run(VolumeMonitor.Scan);if(batch!=current||!current.IsOpen)return;foreach(var volume in monitor.Arrivals(volumes)){
   if(!StorageUnits.Eligible(volume.Type,volume.Size))continue;
   if(current.Plan.Sources.Concat(current.Jobs.Select(j=>j.Destination)).Append(AppContext.BaseDirectory).Append(Environment.SystemDirectory).Any(p=>SourcePool.SameOrInside(p,volume.Root))||!offered.Add(volume.VolumePath))continue;
   var reservation=current.ReserveAdmission();if(reservation!=null)arrivals.Enqueue((volume,current,reservation));
  }}catch(Exception e){DeviceEvent("scan-error",null,e.Message);}finally{scanning=false;OfferQueuedDevices();}
 }
 async void OfferQueuedDevices(){if(ejecting||offering||arrivals.Count==0)return;offering=true;try{while(arrivals.TryDequeue(out var pending)){using var admission=pending.Reservation;try{await OfferDeviceAsync(pending.Volume,pending.Batch);}catch(Exception e){DeviceEvent("device-error",pending.Volume,e.Message);if(e is System.ComponentModel.Win32Exception{NativeErrorCode:1223})ClearFinishedProgress();else MessageBox.Show(this,e.Message,"Nueva unidad");}}}finally{offering=false;}}
 async Task OfferDeviceAsync(DeviceVolume volume,CopyBatch current){
  RefreshAccess();if(!fullLicense||ejecting)return;LicensePolicy.Check(fullLicense,current.Jobs.Length+1);if(!current.IsOpen||StorageUnits.SystemVolume(volume.Root)||!StorageUnits.Eligible(volume.Type,volume.Size)||VolumeMonitor.Read(volume.Root)?.Identity!=volume.Identity)return;
  StorageUnits.Require(volume.Root,storageReader);
  using var dialog=new IncomingDeviceDialog(volume);if(dialog.ShowDialog(this)!=DialogResult.OK){ClearFinishedProgress();return;}current.Plan.Validate(volume.Root);
  if(dialog.Choice==DeviceChoice.Format){var protectedPaths=current.Plan.Sources.Concat(current.Jobs.Select(j=>j.Destination)).Append(AppContext.BaseDirectory).Append(Environment.SystemDirectory).ToArray();var safety=await VolumeFormatter.InspectAsync(volume,protectedPaths);FormattingPolicy.ValidateDevice(safety,volume.VolumePath);using var confirmation=new ConfirmFormatDialog(volume,safety);if(confirmation.ShowDialog(this)!=DialogResult.OK){ClearFinishedProgress();return;}DeviceEvent("format-confirmed",volume,confirmation.FileSystem);await VolumeFormatter.FormatAsync(volume,safety,confirmation.Typed,confirmation.FileSystem,protectedPaths,logs);}
  var ready=VolumeMonitor.Read(volume.Root);if(ready==null||!ready.VolumePath.Equals(volume.VolumePath,StringComparison.OrdinalIgnoreCase)||dialog.Choice!=DeviceChoice.Format&&ready.Identity!=volume.Identity)throw new IOException("La unidad se desconectó o cambió de identidad.");
  AddPaths([volume.Root]);current.Sources.SetEnabled(volume.Root,Targets.Single(t=>t.Path.Equals(volume.Root,StringComparison.OrdinalIgnoreCase)).AllowAuxiliary);Register(current.AddDestination(volume.Root,true));DeviceEvent("joined",ready);
 }
 void DeviceEvent(string action,DeviceVolume? volume,string? message=null){try{Directory.CreateDirectory(logs);File.AppendAllText(Path.Combine(logs,"devices-"+DateTime.Today.ToString("yyyyMMdd")+".jsonl"),System.Text.Json.JsonSerializer.Serialize(new{time=DateTimeOffset.Now,action,root=volume?.Root,volume=volume?.VolumePath,message})+Environment.NewLine);}catch(IOException){}catch(UnauthorizedAccessException){}}
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);BeginInvoke(new Action(()=>{if(IsDisposed)return;foreach(ListViewItem item in devices.Items)_=LoadDetailsAsync(item);}));}
 protected override void Dispose(bool disposing){if(disposing){destinationMenu.Dispose();auxiliaryOnIcon.Dispose();auxiliaryOffIcon.Dispose();ejectIcon.Dispose();timer.Stop();timer.Dispose();planning?.Cancel();planning?.Dispose();}base.Dispose(disposing);}
}
