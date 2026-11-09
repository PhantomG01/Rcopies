namespace DobleCommander;

internal static class LicensePolicy {
 internal static void Check(bool licensed,int destinations){if(!licensed&&destinations>1)throw new IOException("Sin licencia puedes copiar o hacer espejo a una sola unidad por operación. Selecciona un destino o importa una licencia para utilizar varios.");}
}

internal sealed record EjectOutcome(string Destination,bool Success,string Message);
internal sealed record EjectOption(TargetRow Row,EjectCandidate? Candidate,string? Error);

sealed partial class LiteWindow {
 readonly IUnitEjector ejector;
 readonly ContextMenuStrip destinationMenu=new(){ShowImageMargin=true,ImageScalingSize=new(20,20),BackColor=Theme.Surface,ForeColor=Theme.Text,Renderer=new ToolStripProfessionalRenderer(new DestinationMenuColors())};
 readonly Bitmap auxiliaryOnIcon=MenuArtwork.Auxiliary(true),auxiliaryOffIcon=MenuArtwork.Auxiliary(false),ejectIcon=MenuArtwork.Eject();
 readonly ToolStripMenuItem auxiliaryAction=new(),ejectAction=new("Desconectar unidad");
 TargetRow? contextTarget;TargetRow[] ejectSelection=[];bool ejecting;DateTime nextLicenseCheck;
 internal bool FullLicense=>fullLicense;
 void RefreshAccess(){fullLicense=licensed();heading.Text="Rcopies\n"+(fullLicense?"Copia · Multicopia · Espejo":"Modo libre · un destino por operación");detect.Enabled=fullLicense;UpdateSelection();}
 void SetupContextMenu(){
  auxiliaryAction.Image=auxiliaryOffIcon;ejectAction.Image=ejectIcon;destinationMenu.Items.AddRange([auxiliaryAction,ejectAction]);devices.ContextMenuStrip=destinationMenu;
  devices.MouseDown+=(_,e)=>{if(e.Button!=MouseButtons.Right)return;var hit=devices.HitTest(e.X,e.Y).Item;contextTarget=hit?.Tag as TargetRow;if(hit!=null&&!hit.Selected){foreach(ListViewItem item in devices.SelectedItems)item.Selected=false;hit.Selected=true;hit.Focused=true;}};
  devices.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Apps||e.KeyCode==Keys.F10&&e.Shift)contextTarget=devices.FocusedItem?.Tag as TargetRow;};
  destinationMenu.Opening+=async(_,e)=>{
   var row=contextTarget??devices.FocusedItem?.Tag as TargetRow;if(row==null||!Targets.Contains(row)){e.Cancel=true;return;}contextTarget=row;
   auxiliaryAction.Text=row.AllowAuxiliary?"No usar como origen auxiliar":"Permitir como origen auxiliar";auxiliaryAction.Image=row.AllowAuxiliary?auxiliaryOffIcon:auxiliaryOnIcon;auxiliaryAction.Enabled=!ejecting;
   var selected=devices.SelectedItems.Cast<ListViewItem>().Select(i=>(TargetRow)i.Tag!).ToArray();ejectSelection=selected.Contains(row)?selected:[row];var snapshot=ejectSelection;
   ejectAction.Text=snapshot.Length>1?$"Desconectar destinos seleccionados ({snapshot.Length})…":"Desconectar unidad (comprobando…)";ejectAction.Enabled=false;
   var options=await QueryEjectOptionsAsync(snapshot);if(destinationMenu.IsDisposed||!ReferenceEquals(ejectSelection,snapshot))return;
   ejectAction.Text=snapshot.Length>1?$"Desconectar destinos seleccionados ({snapshot.Length})":"Desconectar unidad";
   var available=options.Where(o=>o.Candidate!=null&&BlockingReason(o.Candidate)==null).ToArray();ejectAction.Enabled=!ejecting&&available.Length>0;
   ejectAction.ToolTipText=available.Length>0?"Se conservarán los destinos que estén en uso o que Windows no permita retirar.":"No hay destinos seleccionados que puedan desconectarse ahora.";
  };
  auxiliaryAction.Click+=async(_,_)=>{if(contextTarget is not {} row)return;try{await SetAuxiliaryAsync(row,!row.AllowAuxiliary);}catch(Exception e){MessageBox.Show(this,e.Message,"Origen auxiliar");}};
  ejectAction.Click+=async(_,_)=>{
   var rows=ejectSelection.ToArray();if(rows.Length==0)return;
   try{
    var options=await QueryEjectOptionsAsync(rows);bool grouped=options.Any(o=>o.Candidate is {} candidate&&(candidate.Disks.Length>1||Targets.Count(t=>ejector.DiskForPath(t.Path) is {} n&&candidate.Disks.Contains(n))>1));
    if((rows.Length>1||grouped)&&MessageBox.Show(this,$"Se solicitará la desconexión segura de {rows.Length} destino(s). Las unidades del mismo dispositivo pueden desconectarse juntas. Los destinos en uso se conservarán. ¿Continuar?","Desconectar destinos",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
    var report=await RequestEjectManyAsync(rows);string text=string.Join(Environment.NewLine,report.Select(r=>$"{r.Destination}: {r.Message}"));MessageBox.Show(this,text,"Resultado de la desconexión",MessageBoxButtons.OK,report.All(r=>r.Success)?MessageBoxIcon.Information:MessageBoxIcon.Warning);
   }catch(Exception e){MessageBox.Show(this,e.Message,"No se pudo desconectar");}
  };
 }
 internal async Task SetAuxiliaryAsync(TargetRow row,bool enabled){
  row.AllowAuxiliary=enabled;var current=batch;current?.Sources.SetEnabled(row.Path,enabled);
  if(enabled&&current?.IsOpen==true&&row.Copy is {} copy&&copy.Status.State=="Completado")await current.Sources.PromoteAsync(row.Path,copy.Token);
  RefreshProgress();
 }
 bool WritesActive(TargetRow row)=>row.Copy is {} copy&&!copy.CopyCompleted.IsCompleted||row.Mirror is {} mirror&&!mirror.Completion.IsCompleted;
 string? BlockingReason(EjectCandidate candidate){
  if(StorageUnits.SystemVolume(candidate.Root))return "No se puede desconectar la unidad de Windows.";
  var protectedPaths=new List<string>{Environment.SystemDirectory,AppContext.BaseDirectory,logs};if(!string.IsNullOrWhiteSpace(source.Text))protectedPaths.Add(source.Text);
  if(batch?.IsOpen==true)protectedPaths.AddRange(batch.Plan.Sources);
  foreach(var path in protectedPaths){var disk=ejector.DiskForPath(path);if(disk==null&&!path.StartsWith(@"\\",StringComparison.Ordinal))return "No se pudo comprobar qué disco contiene una ruta en uso.";if(disk is {} n&&candidate.Disks.Contains(n))return "El dispositivo contiene el origen, Rcopies, Windows o sus registros.";}
  foreach(var row in Targets.Where(WritesActive)){var disk=ejector.DiskForPath(row.Path);if(disk==null)return "No se pudo comprobar un destino activo.";if(candidate.Disks.Contains(disk.Value))return "Todavía hay una copia o espejo activo en este dispositivo. Espera o cancélalo antes de desconectar.";}
  return null;
 }
 async Task<EjectOption[]> QueryEjectOptionsAsync(TargetRow[] rows)=>await Task.WhenAll(rows.Select(async row=>{try{var candidate=await ejector.QueryAsync(row.Path);return new EjectOption(row,candidate,candidate==null?"La unidad no admite desconexión segura.":null);}catch(Exception e){return new EjectOption(row,null,e.Message);}}));
 void BeginEject(){if(ejecting)throw new IOException("Ya se está preparando otra desconexión.");ejecting=true;SetBusy(busy);}
 void EndEject(){ejecting=false;SetBusy(busy);OfferQueuedDevices();}
 internal async Task RequestEjectAsync(TargetRow row){BeginEject();try{var candidate=await ejector.QueryAsync(row.Path)??throw new IOException("Esta unidad no admite desconexión segura.");await EjectCoreAsync(row,candidate);}finally{EndEject();}}
 internal async Task<EjectOutcome[]> RequestEjectManyAsync(IEnumerable<TargetRow> selection){
  var rows=selection.DistinctBy(r=>r.Path,StringComparer.OrdinalIgnoreCase).ToArray();if(rows.Length==0)throw new IOException("Selecciona al menos un destino.");BeginEject();
  try{
   var options=await QueryEjectOptionsAsync(rows);var results=new Dictionary<string,EjectOutcome>(StringComparer.OrdinalIgnoreCase);var completedDisks=new HashSet<int>();
   foreach(var option in options.Where(o=>o.Candidate==null))results[option.Row.Path]=new(option.Row.Path,false,option.Error??"Desconexión no disponible.");
   var groups=options.Where(o=>o.Candidate!=null).GroupBy(o=>o.Candidate!.DeviceId+"|"+string.Join(",",o.Candidate.Disks.Order()),StringComparer.OrdinalIgnoreCase).OrderByDescending(g=>g.First().Candidate!.Disks.Length);
   foreach(var group in groups){var first=group.First();var candidate=first.Candidate!;try{
    if(candidate.Disks.Length==0)throw new IOException("No se pudo identificar el dispositivo.");
    if(!candidate.Disks.All(completedDisks.Contains)){await EjectCoreAsync(first.Row,candidate);completedDisks.UnionWith(candidate.Disks);}
    foreach(var option in group)results[option.Row.Path]=new(option.Row.Path,true,"Desconectada de forma segura.");
   }catch(Exception e){foreach(var option in group)results[option.Row.Path]=new(option.Row.Path,false,e.Message);}}
   return rows.Select(row=>results[row.Path]).ToArray();
  }finally{EndEject();}
 }
 async Task EjectCoreAsync(TargetRow row,EjectCandidate candidate){
  if(!Targets.Contains(row))throw new IOException("El destino ya no está en la lista.");var reason=BlockingReason(candidate);if(reason!=null)throw new IOException(reason);
  var affected=Targets.Where(t=>ejector.DiskForPath(t.Path) is {} disk&&candidate.Disks.Contains(disk)).ToArray();var current=batch;
  foreach(var target in affected)await SetAuxiliaryAsync(target,false);
  var end=DateTime.UtcNow.AddSeconds(20);
  while(current!=null&&affected.Any(t=>current.Sources.ActiveReaders(t.Path)>0||current.Sources.IsAuditing(t.Path))){if(DateTime.UtcNow>=end)throw new IOException("La unidad sigue siendo leída por otra copia. Reanuda o cancela esa copia y vuelve a intentarlo. No se forzó la desconexión.");await Task.Delay(75);}
  reason=BlockingReason(candidate);if(reason!=null)throw new IOException(reason);await ejector.EjectAsync(candidate);
  foreach(var item in devices.Items.Cast<ListViewItem>().ToArray())if(affected.Contains(item.Tag as TargetRow))devices.Items.Remove(item);
  DeviceEvent("safely-ejected",null,candidate.Root);RefreshProgress();
 }
}
