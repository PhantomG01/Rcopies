using System.Diagnostics;
using System.Text.Json;

namespace DobleCommander;
public enum Conflict { Skip, Overwrite, Compare }
public record Entry(string Source, string Relative, long Size, DateTime Modified);
public sealed record Plan(Entry[] Files, string[] Directories, string[] Sources) {
 public long Bytes => Files.Sum(x => x.Size);
 public static Plan Create(IEnumerable<string> sources, bool contents, CancellationToken cancellation=default) {
  cancellation.ThrowIfCancellationRequested();
  var files = new List<Entry>(); var dirs = new List<string>();
  var roots = sources.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  if (roots.Length == 0) throw new IOException("Selecciona un origen.");
  void Visit(string path, string relative) {
   cancellation.ThrowIfCancellationRequested();
   if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Enlace o junction no admitido: " + path);
   if (Directory.Exists(path)) {
    dirs.Add(relative);
    foreach (var child in Directory.EnumerateFileSystemEntries(path)) Visit(child, Path.Combine(relative, Path.GetFileName(child)));
   } else { var f = new FileInfo(path); files.Add(new(path, relative, f.Length, f.LastWriteTimeUtc)); }
  }
  foreach (var root in roots) Visit(root, contents && roots.Length == 1 && Directory.Exists(root) ? "" : Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar)));
  if (files.Select(x=>x.Relative).Concat(dirs).GroupBy(x=>x,StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1)) throw new IOException("Los orígenes generan rutas duplicadas.");
  return new(files.ToArray(), dirs.ToArray(), roots);
 }
 public void Validate(string destination) {
  var dest = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar);
  foreach(var s in Sources) {
   var root = s.TrimEnd(Path.DirectorySeparatorChar);
   if (dest.Equals(root,StringComparison.OrdinalIgnoreCase) || dest.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || root.StartsWith(dest+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new IOException("Origen y destino no pueden solaparse.");
  }
  var current = dest;
  while (!string.IsNullOrEmpty(current)) {
   if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new IOException("Destino con enlace o junction: " + current);
   current=Path.GetDirectoryName(current);
  }
 }
}
public sealed record Snapshot(string State, long Done, long Total, long Written, int Copied, int Skipped, string Current, double Seconds);
public sealed class CopyJob {
 readonly Plan plan; readonly Conflict conflict;
 readonly SourcePool? sourcePool;readonly int startIndex;readonly Func<CancellationToken,Task>? beforeCatchUp;readonly Func<bool>? originalsActive;readonly Func<bool>? stillSameDevice;
 readonly CancellationTokenSource cancellation = new(); readonly ManualResetEventSlim gate = new(true);
 readonly Stopwatch watch = new(); readonly object sync = new();
 readonly TaskCompletionSource copyFinished=new(TaskCreationOptions.RunContinuationsAsynchronously);
 internal Task CopyCompleted=>copyFinished.Task;
 long done, written; int copied, skipped, fileIndex; string state="Pendiente", current="", readSource="";bool entered,auxiliaryEligible=true;
 internal bool CanAssist {get{lock(sync)return state=="Completado"&&auxiliaryEligible;}}
 public int CurrentFileIndex {get{lock(sync)return fileIndex;}}
 public string ReadSource {get{lock(sync)return readSource;}}
 internal CancellationToken Token=>cancellation.Token;
 public string Destination {get;} public string LogPath {get;}
 public CopyJob(Plan plan, string destination, Conflict conflict, bool verify, string logDirectory,SourcePool? sourcePool=null,int startIndex=0,Func<CancellationToken,Task>? beforeCatchUp=null,Func<bool>? originalsActive=null,Func<bool>? stillSameDevice=null) {
  plan.Validate(destination); this.plan=plan; Destination=Path.GetFullPath(destination); this.conflict=conflict;
  this.sourcePool=sourcePool;this.startIndex=Math.Clamp(startIndex,0,plan.Files.Length);this.beforeCatchUp=beforeCatchUp;this.originalsActive=originalsActive;this.stillSameDevice=stillSameDevice;
  Directory.CreateDirectory(logDirectory); LogPath=Path.Combine(logDirectory,DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".jsonl");
 }
 public Snapshot Status { get {lock(sync) return new(state,done,plan.Bytes,written,copied,skipped,current,watch.Elapsed.TotalSeconds);} }
 public void Pause() {lock(sync) {if(state is not ("Copiando" or "Pendiente"))return; gate.Reset(); watch.Stop();state="Pausado";}}
 public void Resume() {lock(sync) {if(state!="Pausado")return;if(entered)watch.Start();state=entered?"Copiando":"Pendiente";gate.Set();}}
 public void Cancel() {cancellation.Cancel();gate.Set();}
 void Check() {gate.Wait(cancellation.Token);cancellation.Token.ThrowIfCancellationRequested();if(stillSameDevice!=null&&!stillSameDevice())throw new IOException("La identidad del dispositivo cambió o la unidad se desconectó.");}
 void Log(string action,string path,string? message=null) => File.AppendAllText(LogPath,JsonSerializer.Serialize(new {time=DateTimeOffset.Now,action,path,message})+Environment.NewLine);
 static void NoLink(string path) {if((File.Exists(path)||Directory.Exists(path)) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0) throw new IOException("Enlace en destino: "+path);}
 public void Note(string action,string message)=>SafeLog(action,message);
 public Task RunAsync() => Task.Run(async () => {
  try {
   Check();
   using var resources=await ResourceBudget.EnterAsync(cancellation.Token);
   lock(sync){entered=true;if(state!="Pausado"){state="Copiando";watch.Start();}}
   Check(); plan.Validate(Destination); Directory.CreateDirectory(Destination); Log("start",Destination);
   foreach(var d in plan.Directories.OrderBy(x=>x.Length)) {Check(); var target=Path.Combine(Destination,d); NoLink(target); Directory.CreateDirectory(target);}
   if(beforeCatchUp!=null)Log("late-join",Destination,$"Inicio en archivo {startIndex}; archivos anteriores al finalizar el grupo inicial.");
   var order=Enumerable.Range(startIndex,plan.Files.Length-startIndex).Concat(Enumerable.Range(0,startIndex));
   foreach(var index in order) {
    if(index==0&&startIndex>0&&beforeCatchUp!=null){lock(sync)current="Esperando recuperación de archivos anteriores";await beforeCatchUp(cancellation.Token);Check();Log("catch-up",Destination);}
    var file=plan.Files[index];lock(sync)fileIndex=index;
    Check(); lock(sync)current=file.Relative;
    var target=Path.Combine(Destination,file.Relative); NoLink(target);
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    if(Directory.Exists(target)) throw new IOException("Carpeta en lugar de archivo: "+target);
    if(File.Exists(target) && (conflict==Conflict.Skip || (conflict==Conflict.Compare && await Equal(file.Source,target)))) {
     lock(sync){done+=file.Size; skipped++;if(conflict==Conflict.Skip)auxiliaryEligible=false;}Log("skip",target);continue;
    }
    var temp=target+".dc-"+Guid.NewGuid().ToString("N")+".partial";
    try {
     string lastLoggedSource="";
     await using(var output=ResourceBudget.Open(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
      using var bufferLease=ResourceBudget.RentBuffer();var buffer=bufferLease.Bytes;long offset=0;
      if(file.Size==0)SourcePool.ValidatePrimary(file);
      while(offset<file.Size) {
       Check();long segmentStart=offset;using var lease=sourcePool?.Acquire(file,Destination);var path=lease?.Path??file.Source;bool helper=lease?.HelperRoot!=null;
       try {
        await using var input=ResourceBudget.Open(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(input.Length!=file.Size)throw new IOException("Tamaño de origen diferente al manifiesto.");if(!helper)SourcePool.ValidatePrimary(file);
        if(helper)SourcePool.ValidateHelper(file,path);input.Position=offset;lock(sync)readSource=path;
        if(offset==0||!string.Equals(lastLoggedSource,path,StringComparison.OrdinalIgnoreCase)){Log("read-source",path,helper?"Auxiliar completado":"Origen principal");lastLoggedSource=path;}
        long segmentEnd=Math.Min(file.Size,offset+8L*1024*1024);
        while(offset<segmentEnd){Check();await ResourceBudget.YieldUnderPressureAsync(cancellation.Token);int n=await input.ReadAsync(buffer.AsMemory(0,(int)Math.Min(buffer.Length,segmentEnd-offset)),cancellation.Token);if(n==0)throw new EndOfStreamException("El origen terminó antes de lo esperado.");
         await output.WriteAsync(buffer.AsMemory(0,n),cancellation.Token);offset+=n;lock(sync){done+=n;written+=n;}
         if(originalsActive?.Invoke()==true)await Task.Delay(10,cancellation.Token);
         if(helper&&!sourcePool!.IsEnabled(lease!.HelperRoot!)){Log("auxiliary-handoff",path,"Auxiliar deshabilitado; continúa desde el mismo byte con otra fuente.");break;}
        }
        if(!helper)SourcePool.ValidatePrimary(file);else SourcePool.ValidateHelper(file,path);
       }catch(IOException e)when(helper){output.Position=segmentStart;output.SetLength(segmentStart);lock(sync)done-=offset-segmentStart;offset=segmentStart;sourcePool!.Reject(lease!.HelperRoot!);Log("auxiliary-fallback",path,e.Message);}
       catch(UnauthorizedAccessException e)when(helper){output.Position=segmentStart;output.SetLength(segmentStart);lock(sync)done-=offset-segmentStart;offset=segmentStart;sourcePool!.Reject(lease!.HelperRoot!);Log("auxiliary-fallback",path,e.Message);}
      }
      await output.FlushAsync(cancellation.Token); output.Flush(true);
     }
     if(new FileInfo(temp).Length!=file.Size)throw new IOException("Longitud de copia incorrecta.");
     Check(); File.SetLastWriteTimeUtc(temp,file.Modified); NoLink(target);
     File.Move(temp,target,conflict!=Conflict.Skip); lock(sync)copied++;Log("copied",target);
    } finally {if(File.Exists(temp))File.Delete(temp);}
   }
   lock(sync){state="Completado";fileIndex=plan.Files.Length;}Log("complete",Destination);
  } catch(OperationCanceledException) {lock(sync)state="Cancelado";SafeLog("cancel",null);}
  catch(Exception e) {lock(sync){state="Error";current=e.Message;}SafeLog("error",e.ToString());}
  finally {watch.Stop();copyFinished.TrySetResult();}
 });
 void SafeLog(string action,string? message) {try{Log(action,Destination,message);}catch{}}
 Task<bool> Equal(string a,string b) {
  Check();var first=new FileInfo(a);var second=new FileInfo(b);
  return Task.FromResult(first.Length==second.Length&&first.LastWriteTimeUtc==second.LastWriteTimeUtc);
 }
}
