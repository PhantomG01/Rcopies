namespace DobleCommander;

sealed partial class LiteWindow {
 [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
 internal Func<MirrorPreview[],Task<AutomaticMirrorApproval?>>? AutomaticConfirmation {get;set;}
 internal async Task SelectSourceFromUiAsync(string path){SetSource(path);if(SelectedDestinations.Length>0&&Directory.Exists(path))await StartSmartOperationAsync(false);}
 internal Task StartCopyAsync()=>StartSmartOperationAsync(true);
 async Task StartSmartOperationAsync(bool copyNew){
  RefreshAccess();if(ejecting)throw new IOException("Espera a que termine la desconexión.");if(busy)throw new IOException("Ya hay un lote en curso.");
  var origin=ReadSource();var roots=SelectedDestinations;LicensePolicy.Check(fullLicense,roots.Length);foreach(var root in roots)StorageUnits.Require(root,storageReader);if(roots.Length==0)throw new IOException("Selecciona al menos una unidad destino.");
  bool contentsOnly=contents.Checked;var conflict=(Conflict)policy.SelectedIndex;SetBusy(true);planning=new();var tasks=new List<Task>();
  try{
   var token=planning.Token;summary.Text="Comparando carpetas coincidentes…";
   var analysis=await Task.Run(()=>AutomaticMirrorPlanner.AnalyzeAsync(origin,roots,contentsOnly,VolumeMonitor.IdentityFor,VolumeMonitor.FreeBytesFor,[AppContext.BaseDirectory,Environment.SystemDirectory,logs],token));token.ThrowIfCancellationRequested();
   MirrorPreview[] mirrors=[];bool earlyPrune=false;
   if(analysis.ChangedMirrors.Length>0){
    AutomaticMirrorApproval? approval;
    if(AutomaticConfirmation!=null)approval=await AutomaticConfirmation(analysis.ChangedMirrors);
    else{using var dialog=new MirrorConfirmationDialog(analysis.ChangedMirrors,true,analysis.NewDestinations.Length);approval=dialog.ShowDialog(this)==DialogResult.OK?new(dialog.SelectedPreviews,dialog.EarlyPrune):null;}
    token.ThrowIfCancellationRequested();if(approval==null){ClearFinishedProgress();return;}
    mirrors=approval.Previews;earlyPrune=approval.EarlyPrune;
    foreach(var selected in mirrors)if(!analysis.ChangedMirrors.Any(p=>p.Destination.Equals(selected.Destination,StringComparison.OrdinalIgnoreCase)&&ReferenceEquals(p.Source,selected.Source)))throw new IOException("La confirmación no corresponde a las carpetas analizadas.");
   }else if(!copyNew){ClearFinishedProgress();return;}
   if(analysis.NewDestinations.Length==0&&mirrors.Length==0){ClearFinishedProgress();sourceInfo.Text="Sin cambios: las carpetas coincidentes tienen el mismo tamaño y fecha.";return;}
   if(discover){var volumes=await Task.Run(VolumeMonitor.Scan);token.ThrowIfCancellationRequested();monitor.Baseline(volumes);offered.Clear();}
   ResetAllProgress();batch=null;
   if(analysis.NewDestinations.Length>0){
    batch=new CopyBatch(analysis.NormalPlan,conflict,false,logs,VolumeMonitor.IdentityFor);foreach(var row in Targets)batch.Sources.SetEnabled(row.Path,row.AllowAuxiliary);
    foreach(var root in analysis.NewDestinations){token.ThrowIfCancellationRequested();Register(batch.AddDestination(root));}batch.SealInitial();tasks.Add(batch.WaitAsync());
   }
   foreach(var preview in mirrors){token.ThrowIfCancellationRequested();var row=MirrorRow(preview.Destination);StorageUnits.Require(row.Path,storageReader);row.Copy=null;row.Mirror=new MirrorJob(preview,earlyPrune&&MirrorPlanner.RequiresEarlyPrune(preview,mirrors),logs,VolumeMonitor.IdentityFor,VolumeMonitor.FreeBytesFor);row.ShowProgress=true;tasks.Add(row.Mirror.RunAsync());}
   planning.Dispose();planning=null;await Task.WhenAll(tasks);RefreshProgress();
  }catch(OperationCanceledException){await StopPrepared();ClearFinishedProgress();}
  catch{await StopPrepared();throw;}
  finally{planning?.Dispose();planning=null;SetBusy(false);RefreshProgress();}
  async Task StopPrepared(){foreach(var row in Targets)row.Mirror?.Cancel();if(batch?.IsOpen==true){batch.CancelAll();batch.SealInitial();await batch.WaitAsync();}await Task.WhenAll(tasks);}
 }
 TargetRow MirrorRow(string destination)=>Targets.SingleOrDefault(row=>SourcePool.SameOrInside(destination,row.Path))??throw new IOException("La carpeta de espejo no pertenece a una unidad destino seleccionada.");
 bool MirrorsRunning=>Targets.Any(row=>row.Mirror is {} mirror&&!mirror.Completion.IsCompleted);
}
