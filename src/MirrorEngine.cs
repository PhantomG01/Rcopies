using System.Diagnostics;
using System.Text.Json;

namespace DobleCommander;

public sealed record MirrorNode(string Relative,bool Directory,long Size,DateTime Modified);
public sealed record MirrorChange(string Action,string Relative,long Bytes);
public sealed record MirrorSource(string Root,Plan Plan,IReadOnlyDictionary<string,MirrorNode> Nodes,string? Identity);
public sealed record MirrorPreview(MirrorSource Source,string Destination,IReadOnlyDictionary<string,MirrorNode> DestinationNodes,bool DestinationExisted,Entry[] CopyFiles,string[] RemoveRoots,string[] BlockerRoots,MirrorChange[] Changes,long PeakBytes,long ReclaimableBytes,long? FreeBytes,string? Identity,IReadOnlyDictionary<string,MirrorNode>? ExpectedNodes=null,string[]? SelectedDirectories=null) {
 public bool Fits(bool earlyPrune)=>FreeBytes==null||FreeBytes.Value+(earlyPrune?ReclaimableBytes:0)>=PeakBytes;
 public bool IsPartial=>ExpectedNodes!=null;
 public string[] DirectoriesToCreate=>SelectedDirectories??Source.Plan.Directories;
 public Entry[] FilesToFinalize=>IsPartial?CopyFiles:Source.Plan.Files;
}
public static partial class MirrorPlanner {
 public static readonly string[] ProtectedNames=["$RECYCLE.BIN","System Volume Information"];
 public static bool VolumeRoot(string path)=>Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)).Equals(Path.TrimEndingDirectorySeparator(Path.GetPathRoot(Path.GetFullPath(path))!),StringComparison.OrdinalIgnoreCase);
 static bool Protected(string root,string path)=>VolumeRoot(root)&&ProtectedNames.Contains(Path.GetRelativePath(root,path).Split(Path.DirectorySeparatorChar)[0],StringComparer.OrdinalIgnoreCase);
 public static void ValidateRoots(string source,IEnumerable<string> destinations,IEnumerable<string>? protectedPaths=null) {
  var src=Path.GetFullPath(source);if(!Directory.Exists(src))throw new IOException("La carpeta origen no existe.");SourcePool.NoLinks(src);
  var criticalPaths=(protectedPaths??[]).Append(AppContext.BaseDirectory).Append(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).Append(Environment.GetFolderPath(Environment.SpecialFolder.System)).Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  var targets=destinations.Select(Path.GetFullPath).ToArray();if(targets.Length==0)throw new IOException("Marca al menos un destino para el espejo.");
  foreach(var target in targets){if(File.Exists(target))throw new IOException("El destino es un archivo.");SourcePool.NoLinks(target);if(SourcePool.SameOrInside(target,src)||SourcePool.SameOrInside(src,target))throw new IOException("Origen y destino del espejo no pueden solaparse.");
   foreach(var critical in criticalPaths)if(SourcePool.SameOrInside(critical,target)||SourcePool.SameOrInside(target,critical))throw new IOException("Destino protegido: contiene o pertenece a Windows, la aplicación o sus logs.");
  }
  for(int a=0;a<targets.Length;a++)for(int b=a+1;b<targets.Length;b++)if(SourcePool.SameOrInside(targets[a],targets[b])||SourcePool.SameOrInside(targets[b],targets[a]))throw new IOException("Los destinos del espejo se solapan.");
 }
 public static Dictionary<string,MirrorNode> Scan(string root,CancellationToken token=default) {
  var result=new Dictionary<string,MirrorNode>(StringComparer.OrdinalIgnoreCase);if(!Directory.Exists(root))return result;SourcePool.NoLinks(root);
  void Visit(string directory){token.ThrowIfCancellationRequested();foreach(var path in Directory.EnumerateFileSystemEntries(directory)){
   token.ThrowIfCancellationRequested();if(Protected(root,path))continue;
   var attrs=File.GetAttributes(path);if((attrs&FileAttributes.ReparsePoint)!=0)throw new IOException("El espejo no admite enlaces o junctions: "+path);
   var relative=Path.GetRelativePath(root,path);bool folder=(attrs&FileAttributes.Directory)!=0;
   var info=new FileInfo(path);result.Add(relative,new(relative,folder,folder?0:info.Length,folder?DateTime.MinValue:info.LastWriteTimeUtc));if(folder)Visit(path);
  }}Visit(root);return result;
 }
 public static bool Same(IReadOnlyDictionary<string,MirrorNode> a,IReadOnlyDictionary<string,MirrorNode> b)=>a.Count==b.Count&&a.All(pair=>b.TryGetValue(pair.Key,out var other)&&pair.Value.Directory==other.Directory&&pair.Value.Size==other.Size&&pair.Value.Modified==other.Modified);
 static void NoOldRecovery(IReadOnlyDictionary<string,MirrorNode> nodes){if(nodes.Keys.Any(p=>p.Split(Path.DirectorySeparatorChar)[0].StartsWith(".DobleCommander-espejo-",StringComparison.OrdinalIgnoreCase)))throw new IOException("Hay una carpeta de recuperación de un espejo anterior. Revísala y retírala antes de iniciar otro espejo.");}
 public static Task<MirrorSource> ReadSourceAsync(string root,Func<string,string?>? identity=null,CancellationToken token=default,IProgress<string>? progress=null) {
  root=Path.GetFullPath(root);if(!Directory.Exists(root))throw new IOException("El origen no está disponible.");var id=identity?.Invoke(root);var nodes=Scan(root,token);NoOldRecovery(nodes);
  if(!Same(nodes,Scan(root,token))||identity!=null&&id!=identity(root))throw new IOException("El origen cambió durante el análisis.");
  var files=nodes.Values.Where(n=>!n.Directory).Select(n=>new Entry(Path.Combine(root,n.Relative),n.Relative,n.Size,n.Modified)).ToArray();var dirs=nodes.Values.Where(n=>n.Directory).Select(n=>n.Relative).Prepend("").ToArray();return Task.FromResult(new MirrorSource(root,new(files,dirs,[root]),nodes,id));
 }
 public static long? AvailableBytes(string destination){try{return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(destination))!).AvailableFreeSpace;}catch(IOException){return null;}catch(ArgumentException){return null;}catch(UnauthorizedAccessException){return null;}}
 static IEnumerable<MirrorPreview[]> SpaceGroups(IEnumerable<MirrorPreview> previews)=>previews.GroupBy(p=>p.Identity??"path:"+p.Destination,StringComparer.OrdinalIgnoreCase).Select(g=>g.ToArray());
 public static bool FitsTogether(IEnumerable<MirrorPreview> previews,bool allowEarlyPrune){foreach(var group in SpaceGroups(previews)){var known=group.Where(p=>p.FreeBytes!=null).Select(p=>p.FreeBytes!.Value).ToArray();if(known.Length==0)continue;if(known.Min()+(allowEarlyPrune?group.Sum(p=>p.ReclaimableBytes):0)<group.Sum(p=>p.PeakBytes))return false;}return true;}
 public static bool RequiresEarlyPrune(MirrorPreview preview,IEnumerable<MirrorPreview> previews)=>!FitsTogether(previews.Where(p=>preview.Identity==null?ReferenceEquals(p,preview):string.Equals(p.Identity,preview.Identity,StringComparison.OrdinalIgnoreCase)),false);
 static bool Under(string path,string root)=>path.Equals(root,StringComparison.OrdinalIgnoreCase)||path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
 static string[] MinimalRoots(IEnumerable<string> paths){var result=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var path in paths.OrderBy(p=>p.Length)){bool nested=false;for(string? parent=path;!string.IsNullOrEmpty(parent);parent=Path.GetDirectoryName(parent))if(result.Contains(parent)){nested=true;break;}if(!nested)result.Add(path);}return result.ToArray();}
 public static Task<MirrorPreview> PreviewAsync(MirrorSource source,string destination,Func<string,string?>? identity=null,Func<string,long?>? freeBytes=null,CancellationToken token=default,IProgress<string>? progress=null) {
  ValidateRoots(source.Root,[destination]);destination=Path.GetFullPath(destination);bool existed=Directory.Exists(destination);var id=identity?.Invoke(destination);var nodes=Scan(destination,token);NoOldRecovery(nodes);var copies=new List<Entry>();var changes=new List<MirrorChange>();
  if(VolumeRoot(destination)&&source.Nodes.Keys.Any(p=>ProtectedNames.Contains(p.Split(Path.DirectorySeparatorChar)[0],StringComparer.OrdinalIgnoreCase)))throw new IOException("El origen contiene un nombre reservado para la raíz de un volumen de Windows.");
  var blockers=MinimalRoots(source.Nodes.Values.Where(n=>nodes.TryGetValue(n.Relative,out var old)&&old.Directory!=n.Directory).Select(n=>n.Relative));
  var extras=MinimalRoots(nodes.Keys.Where(p=>!source.Nodes.ContainsKey(p)&&!blockers.Any(b=>Under(p,b))));
  foreach(var file in source.Plan.Files) {
   token.ThrowIfCancellationRequested();progress?.Report("Comparando "+destination+": "+file.Relative);
   bool same=nodes.TryGetValue(file.Relative,out var old)&&!old.Directory&&old.Size==file.Size&&old.Modified==file.Modified;
   if(!same){copies.Add(file);changes.Add(new(old==null?"Añadir":"Actualizar",file.Relative,file.Size));}
   else if(old!.Modified!=file.Modified)changes.Add(new("Ajustar fecha",file.Relative,0));
  }
  foreach(var dir in source.Nodes.Values.Where(n=>n.Directory))if(!nodes.TryGetValue(dir.Relative,out var old)||!old.Directory)changes.Add(new("Crear carpeta",dir.Relative,0));
  foreach(var b in blockers)changes.Add(new("Reemplazar tipo",b,0));foreach(var extra in extras)changes.Add(new("Eliminar",extra,nodes.Values.Where(n=>!n.Directory&&Under(n.Relative,extra)).Sum(n=>n.Size)));
  if(existed!=Directory.Exists(destination)||!Same(nodes,Scan(destination,token)))throw new IOException("El destino cambió durante el análisis.");
  long delta=0,peak=0;foreach(var file in copies){peak=Math.Max(peak,delta+file.Size);long old=nodes.TryGetValue(file.Relative,out var node)&&!node.Directory&&!blockers.Any(b=>Under(file.Relative,b))?node.Size:0;delta+=file.Size-old;}
  long reclaim=nodes.Values.Where(n=>!n.Directory&&(extras.Any(e=>Under(n.Relative,e))||blockers.Any(b=>Under(n.Relative,b)))).Sum(n=>n.Size);
  return Task.FromResult(new MirrorPreview(source,destination,nodes,existed,copies.ToArray(),extras,blockers,changes.ToArray(),peak,reclaim,(freeBytes??AvailableBytes)(destination),id));
 }
 internal static bool IsUnder(string path,string root)=>Under(path,root);
}

public sealed record MirrorStatus(string State,long Done,long Total,long Written,long CopyBytes,string Current,double Seconds,string? Recovery);
public sealed class MirrorJob {
 readonly MirrorPreview preview;readonly bool earlyPrune;readonly string logs;readonly Func<string,string?>? identity;readonly Func<string,long?> freeBytes;
 readonly CancellationTokenSource cancellation=new();readonly ManualResetEventSlim gate=new(true);readonly object sync=new();readonly Stopwatch watch=new();
 readonly TaskCompletionSource finished=new(TaskCreationOptions.RunContinuationsAsynchronously);
 internal Task Completion=>finished.Task;
 string state="Pendiente",current="";long done,written;double fraction;string? recovery;readonly long total;string resumeState="Pendiente";bool entered;
 public string Destination=>preview.Destination;public string Source=>preview.Source.Root;public string LogPath {get;}
 public MirrorStatus Status{get{lock(sync)return new(state,done,total,written,preview.CopyFiles.Sum(f=>f.Size),current,watch.Elapsed.TotalSeconds,recovery);}}
 public double Progress{get{lock(sync)return Math.Clamp((done+fraction)/Math.Max(1,total),0,1);}}
 public MirrorJob(MirrorPreview preview,bool earlyPrune,string logs,Func<string,string?>? identity=null,Func<string,long?>? freeBytes=null){this.preview=preview;this.earlyPrune=earlyPrune;this.logs=logs;this.identity=identity;this.freeBytes=freeBytes??MirrorPlanner.AvailableBytes;total=preview.BlockerRoots.Length+preview.RemoveRoots.Length+preview.CopyFiles.Length+preview.FilesToFinalize.Length+preview.DirectoriesToCreate.Length+1;Directory.CreateDirectory(logs);LogPath=Path.Combine(logs,"mirror-"+Guid.NewGuid().ToString("N")+".jsonl");}
 public void Pause(){lock(sync){if(state is "Completado" or "Error" or "Cancelado" or "Pausado")return;resumeState=state;state="Pausado";watch.Stop();gate.Reset();}}
 public void Resume(){lock(sync){if(state!="Pausado")return;state=resumeState;if(entered)watch.Start();gate.Set();}}
 public void Cancel(){cancellation.Cancel();gate.Set();}
 void Check(){gate.Wait(cancellation.Token);cancellation.Token.ThrowIfCancellationRequested();if(identity!=null){if(preview.Source.Identity!=null&&identity(preview.Source.Root)!=preview.Source.Identity)throw new IOException("La unidad origen cambió de identidad.");if(preview.Identity!=null&&identity(Destination)!=preview.Identity)throw new IOException("La unidad destino cambió de identidad.");}}
 void Phase(string text){Check();lock(sync){if(state=="Pausado")resumeState=text;else state=text;}}
 void Current(string text){lock(sync)current=text;}
 void CompleteStep(){lock(sync){done++;fraction=0;}}
 void Log(string action,string path,string? message=null)=>File.AppendAllText(LogPath,JsonSerializer.Serialize(new{time=DateTimeOffset.Now,action,path,message})+Environment.NewLine);
 void ValidateSource(){Check();if(!Directory.Exists(preview.Source.Root)||!MirrorPlanner.Same(preview.Source.Nodes,MirrorPlanner.Scan(preview.Source.Root,cancellation.Token)))throw new IOException("El origen cambió o desapareció después de previsualizar. Vuelve a analizar el espejo.");}
 public Task RunAsync()=>Task.Run(async()=>{
  try {
   Check();
   using var resources=await ResourceBudget.EnterAsync(cancellation.Token);
   lock(sync){entered=true;resumeState="Validando";if(state!="Pausado"){state="Validando";watch.Start();}}
   Check();MirrorPlanner.ValidateRoots(preview.Source.Root,[Destination],[logs]);ValidateSource();if(preview.DestinationExisted!=Directory.Exists(Destination)||!MirrorPlanner.Same(preview.DestinationNodes,MirrorPlanner.Scan(Destination,cancellation.Token)))throw new IOException("El destino cambió después de previsualizar. Vuelve a analizar.");
   var free=freeBytes(Destination);if(free!=null&&free.Value+(earlyPrune?preview.ReclaimableBytes:0)<preview.PeakBytes)throw new IOException("Espacio insuficiente para este plan de espejo.");
   Log("mirror-start",Destination,$"Origen={preview.Source.Root}; borrado previo={earlyPrune}");Directory.CreateDirectory(Destination);
   var staged=new List<(string Original,string Backup)>();
   if(earlyPrune) {
    Phase("Borrado previo");ValidateSource();foreach(var path in preview.RemoveRoots.Concat(preview.BlockerRoots)){Check();DeleteApproved(path,preview.DestinationNodes);CompleteStep();}
   } else {
    recovery=Path.Combine(Destination,".DobleCommander-espejo-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(recovery);
    foreach(var path in preview.BlockerRoots){Check();Stage(path,"conflictos",staged);CompleteStep();}
   }
   Phase("Copiando");foreach(var dir in preview.DirectoriesToCreate.OrderBy(p=>p.Length)){Check();var full=Path.Combine(Destination,dir);SourcePool.NoLinks(full);Directory.CreateDirectory(full);CompleteStep();}
   foreach(var file in preview.CopyFiles){Check();Current(file.Relative);await CopyFile(file);CompleteStep();}
   Phase("Finalizando");ValidateSource();
   foreach(var file in preview.FilesToFinalize){Check();Current(file.Relative);var full=Path.Combine(Destination,file.Relative);SourcePool.NoLinks(full);if(!File.Exists(full)||new FileInfo(full).Length!=file.Size)throw new IOException("Archivo incompleto en el destino: "+file.Relative);File.SetLastWriteTimeUtc(full,file.Modified);CompleteStep();}
   ValidateSource();
   if(!earlyPrune){Phase("Retirando sobrantes");foreach(var path in preview.RemoveRoots){Check();Stage(path,"sobrantes",staged);CompleteStep();}}
   Phase("Validando estructura");var final=MirrorPlanner.Scan(Destination,cancellation.Token);if(recovery!=null){var relative=Path.GetFileName(recovery);final=final.Where(p=>!MirrorPlanner.IsUnder(p.Key,relative)).ToDictionary(p=>p.Key,p=>p.Value,StringComparer.OrdinalIgnoreCase);}
   var expected=preview.ExpectedNodes??preview.Source.Nodes;
   if(final.Count!=expected.Count||expected.Any(p=>!final.TryGetValue(p.Key,out var other)||p.Value.Directory!=other.Directory||p.Value.Size!=other.Size))throw new IOException("La estructura del destino no coincide. Se conserva la carpeta de recuperación.");
   ValidateSource();
   if(recovery!=null){Phase("Eliminando recuperación");PurgeRecovery(staged);recovery=null;}
   CompleteStep();lock(sync){state="Completado";current=preview.IsPartial?"Sincronización parcial completada":"Espejo completado";}Log("mirror-complete",Destination);
  }catch(OperationCanceledException){lock(sync)state="Cancelado";SafeLog("mirror-cancel",recovery);}
  catch(Exception e){lock(sync){state="Error";current=e.Message;}SafeLog("mirror-error",e+Environment.NewLine+"Recuperación: "+recovery);}
  finally{watch.Stop();finished.TrySetResult();}
 });
 async Task CopyFile(Entry file) {
  SourcePool.ValidatePrimary(file);var target=Path.Combine(Destination,file.Relative);SourcePool.NoLinks(target);var temp=target+".mirror-"+Guid.NewGuid().ToString("N")+".partial";
  try {
   await using(var input=ResourceBudget.Open(file.Source,FileMode.Open,FileAccess.Read,FileShare.Read))
   await using(var output=ResourceBudget.Open(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
    using var bufferLease=ResourceBudget.RentBuffer();var buffer=bufferLease.Bytes;long offset=0;int count;
    while(true){Check();await ResourceBudget.YieldUnderPressureAsync(cancellation.Token);count=await input.ReadAsync(buffer,cancellation.Token);if(count==0)break;await output.WriteAsync(buffer.AsMemory(0,count),cancellation.Token);offset+=count;lock(sync){written+=count;fraction=file.Size==0?0:Math.Min(1,offset/(double)file.Size);}}
    if(offset!=file.Size)throw new IOException("El contenido del origen cambió: "+file.Relative);
    await output.FlushAsync(cancellation.Token);output.Flush(true);
   }
   Check();SourcePool.ValidatePrimary(file);if(new FileInfo(temp).Length!=file.Size)throw new IOException("La copia temporal está incompleta.");File.SetLastWriteTimeUtc(temp,file.Modified);SourcePool.NoLinks(target);File.Move(temp,target,true);Log("mirror-copy",target);
  }finally{if(File.Exists(temp))File.Delete(temp);}
 }
 void ValidateApproved(string relative,IReadOnlyDictionary<string,MirrorNode> expected) {
  var full=Path.Combine(Destination,relative);SourcePool.NoLinks(full);if(!expected.TryGetValue(relative,out var root))throw new IOException("Ruta no incluida en la previsualización.");
  var info=new FileInfo(full);bool directory=Directory.Exists(full);if(!directory&&!File.Exists(full)||directory!=root.Directory||!directory&&(info.Length!=root.Size||info.LastWriteTimeUtc!=root.Modified))throw new IOException("Cambió un elemento pendiente de eliminación: "+full);
  if(directory){var children=MirrorPlanner.Scan(full,cancellation.Token);var approved=expected.Where(p=>!p.Key.Equals(relative,StringComparison.OrdinalIgnoreCase)&&MirrorPlanner.IsUnder(p.Key,relative)).ToDictionary(p=>Path.GetRelativePath(full,Path.Combine(Destination,p.Key)),p=>p.Value with{Relative=Path.GetRelativePath(full,Path.Combine(Destination,p.Key))},StringComparer.OrdinalIgnoreCase);if(!MirrorPlanner.Same(children,approved))throw new IOException("Cambió una carpeta pendiente de eliminación: "+full);}
 }
 void Stage(string relative,string category,List<(string Original,string Backup)> staged) {
  ValidateApproved(relative,preview.DestinationNodes);var original=Path.Combine(Destination,relative);var backup=Path.Combine(recovery!,category,relative);Directory.CreateDirectory(Path.GetDirectoryName(backup)!);if(Directory.Exists(original))Directory.Move(original,backup);else File.Move(original,backup);staged.Add((relative,backup));Log("mirror-staged",original,backup);
 }
 void DeleteApproved(string relative,IReadOnlyDictionary<string,MirrorNode> expected) {
  ValidateApproved(relative,expected);var full=Path.Combine(Destination,relative);
  foreach(var file in expected.Values.Where(n=>!n.Directory&&MirrorPlanner.IsUnder(n.Relative,relative))){Check();var path=Path.Combine(Destination,file.Relative);SourcePool.NoLinks(path);File.Delete(path);Log("mirror-delete",path);}
  foreach(var dir in expected.Values.Where(n=>n.Directory&&MirrorPlanner.IsUnder(n.Relative,relative)).OrderByDescending(n=>n.Relative.Length)){Check();var path=Path.Combine(Destination,dir.Relative);SourcePool.NoLinks(path);Directory.Delete(path,false);Log("mirror-delete-dir",path);}
 }
 void PurgeRecovery(List<(string Original,string Backup)> staged) {
  SourcePool.NoLinks(recovery!);var actual=MirrorPlanner.Scan(recovery!,cancellation.Token);var expected=new Dictionary<string,MirrorNode>(StringComparer.OrdinalIgnoreCase);
  foreach(var pair in staged)foreach(var node in preview.DestinationNodes.Values.Where(n=>MirrorPlanner.IsUnder(n.Relative,pair.Original))){var full=node.Relative.Equals(pair.Original,StringComparison.OrdinalIgnoreCase)?pair.Backup:Path.Combine(pair.Backup,Path.GetRelativePath(Path.Combine(Destination,pair.Original),Path.Combine(Destination,node.Relative)));var relative=Path.GetRelativePath(recovery!,full);expected[relative]=node with{Relative=relative};}
  foreach(var key in expected.Keys.ToArray()){string? parent=Path.GetDirectoryName(key);while(!string.IsNullOrEmpty(parent)){expected.TryAdd(parent,new(parent,true,0,DateTime.MinValue));parent=Path.GetDirectoryName(parent);}}
  if(!MirrorPlanner.Same(expected,actual))throw new IOException("La recuperación cambió; se conserva sin purgar.");
  foreach(var node in actual.Values.Where(n=>!n.Directory)){Check();var path=Path.Combine(recovery!,node.Relative);SourcePool.NoLinks(path);File.Delete(path);Log("mirror-purge",path);}
  foreach(var node in actual.Values.Where(n=>n.Directory).OrderByDescending(n=>n.Relative.Length)){Check();var path=Path.Combine(recovery!,node.Relative);SourcePool.NoLinks(path);Directory.Delete(path,false);}
  Directory.Delete(recovery!,false);
 }
 void SafeLog(string action,string? message){try{Log(action,Destination,message);}catch{}}
}
