namespace DobleCommander;

sealed class MirrorPlanningDialog : Form {
 readonly CancellationTokenSource cancel=new();readonly Label status=new(){Dock=DockStyle.Fill,Padding=new(18),AutoEllipsis=true};
 bool finished;public MirrorPreview[] Previews {get;private set;}=[];
 public MirrorPlanningDialog(string source,string[] destinations,string[] protectedPaths) {
  Text="Analizando copia espejo";Size=new(760,220);StartPosition=FormStartPosition.CenterParent;Font=new("Segoe UI",10);
  var stop=new Button(){Text="Cancelar análisis",Dock=DockStyle.Bottom,Height=40};stop.Click+=(_,_)=>{cancel.Cancel();status.Text="Cancelando análisis…";};Controls.Add(status);Controls.Add(new AccentProgressBar(){Dock=DockStyle.Bottom,Height=18,Style=ProgressBarStyle.Marquee});Controls.Add(stop);
  Theme.Apply(this);FormClosing+=(_,e)=>{if(!finished){cancel.Cancel();e.Cancel=true;}};
  Shown+=async(_,_)=>{
   try {
    IProgress<string> progress=new Progress<string>(text=>{if(!IsDisposed)status.Text=text;});
    Previews=await Task.Run(async()=>{
     MirrorPlanner.ValidateRoots(source,destinations,protectedPaths);
     foreach(var destination in destinations.Where(MirrorPlanner.VolumeRoot)){
      cancel.Token.ThrowIfCancellationRequested();progress.Report("Comprobando protección del volumen: "+destination);
      var volume=VolumeMonitor.Read(Path.GetPathRoot(destination)!)??throw new IOException("No puede comprobarse la identidad de este volumen para un espejo de raíz.");
      var safety=await VolumeFormatter.InspectAsync(volume,protectedPaths.Append(source).ToArray());FormattingPolicy.ValidateDevice(safety,volume.VolumePath);
     }
     var manifest=await MirrorPlanner.ReadSourceAsync(source,VolumeMonitor.IdentityFor,cancel.Token,progress);var previews=new List<MirrorPreview>();foreach(var destination in destinations){cancel.Token.ThrowIfCancellationRequested();previews.Add(await MirrorPlanner.PreviewAsync(manifest,destination,VolumeMonitor.IdentityFor,VolumeMonitor.FreeBytesFor,cancel.Token,progress));}return previews.ToArray();
    });
    finished=true;DialogResult=DialogResult.OK;
   }catch(OperationCanceledException){finished=true;DialogResult=DialogResult.Cancel;}
   catch(Exception e){MessageBox.Show(this,e.Message,"No se pudo preparar el espejo");finished=true;DialogResult=DialogResult.Cancel;}
   finally{finished=true;Close();}
  };
 }
 protected override void Dispose(bool disposing){if(disposing)cancel.Dispose();base.Dispose(disposing);}
}

