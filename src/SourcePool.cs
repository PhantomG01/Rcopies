
namespace DobleCommander;

// Completed copies are promoted independently; metadata checks never reread contents.
public sealed class SourcePool {
 readonly Plan plan;readonly object sync=new();
 readonly List<Helper> helpers=[];readonly Dictionary<string,int> readers=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,long> assignments=new(StringComparer.OrdinalIgnoreCase);int acceptedCount;
 readonly HashSet<string> auditing=new(StringComparer.OrdinalIgnoreCase);
 readonly HashSet<string> disabled=new(StringComparer.OrdinalIgnoreCase),acceptedRoots=new(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,CancellationTokenSource> auditStops=new(StringComparer.OrdinalIgnoreCase);
 public sealed record Helper(string Root,string? Identity);
 readonly Func<string,string?>? identity;
 public int HelperCount {get{lock(sync)return helpers.Count;}}
 public string[] HelperRoots {get{lock(sync)return helpers.Select(h=>h.Root).ToArray();}}
 public string[] AuditingRoots {get{lock(sync)return auditing.ToArray();}}
 public SourcePool(Plan plan,Func<string,string?>? identity=null){this.plan=plan;this.identity=identity;}
 static string Key(string root)=>System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar);
 public int ActiveReaders(string root){lock(sync)return readers.GetValueOrDefault(Key(root));}
 public bool IsAuditing(string root){lock(sync)return auditStops.ContainsKey(Key(root));}
 public bool IsEnabled(string root){lock(sync)return !disabled.Contains(Key(root));}
 public void SetEnabled(string root,bool enabled){CancellationTokenSource? stop=null;var key=Key(root);lock(sync){if(enabled)disabled.Remove(key);else{disabled.Add(key);helpers.RemoveAll(h=>Key(h.Root).Equals(key,StringComparison.OrdinalIgnoreCase));auditStops.TryGetValue(key,out stop);}}if(stop!=null)try{stop.Cancel();}catch(ObjectDisposedException){}}
 public sealed class Lease : IDisposable {
  readonly SourcePool owner;readonly string key;int disposed;
  public string Path {get;}public string? HelperRoot {get;}
  internal Lease(SourcePool owner,string path,string key,string? helper){this.owner=owner;Path=path;this.key=key;HelperRoot=helper;}
  public void Dispose(){if(Interlocked.Exchange(ref disposed,1)==0)lock(owner.sync){owner.readers[key]--;}}
 }
 public Lease Acquire(Entry file,string destination) {
  lock(sync) {
   var available=helpers.Where(h=>!disabled.Contains(Key(h.Root))&&!SameOrInside(destination,h.Root)&&!SameOrInside(h.Root,destination)).OrderBy(h=>readers.GetValueOrDefault(Key(h.Root))).ThenBy(h=>assignments.GetValueOrDefault(h.Root)).ToArray();
   foreach(var helper in available) {
    if(identity!=null&&!string.Equals(identity(helper.Root),helper.Identity,StringComparison.Ordinal)){helpers.Remove(helper);continue;}
    var path=System.IO.Path.Combine(helper.Root,file.Relative);
    readers[Key(helper.Root)]=readers.GetValueOrDefault(Key(helper.Root))+1;
    assignments[helper.Root]=assignments.GetValueOrDefault(helper.Root)+1;
    return new(this,path,Key(helper.Root),helper.Root);
   }
   readers["primary"]=readers.GetValueOrDefault("primary")+1;return new(this,file.Source,"primary",null);
  }
 }
 public void Reject(string root){lock(sync)helpers.RemoveAll(h=>h.Root.Equals(root,StringComparison.OrdinalIgnoreCase));}
 // Only CopyBatch calls this after a successful completed job. There is no scan
 // or content reread between completion and the first auxiliary read.
 internal Task<bool> PromoteCompletedAsync(string destination,CancellationToken token){
  token.ThrowIfCancellationRequested();var key=Key(destination);var volume=identity?.Invoke(destination);if(identity!=null&&volume==null)return Task.FromResult(false);
  lock(sync){if(disabled.Contains(key)||acceptedCount>=4&&!acceptedRoots.Contains(key)||helpers.Any(h=>Key(h.Root).Equals(key,StringComparison.OrdinalIgnoreCase)))return Task.FromResult(false);helpers.Add(new(destination,volume));if(acceptedRoots.Add(key))acceptedCount++;return Task.FromResult(true);}
 }
 public Task<bool> PromoteAsync(string destination,CancellationToken token=default)=>Task.Run(()=>{
  token.ThrowIfCancellationRequested();var key=Key(destination);using var stop=CancellationTokenSource.CreateLinkedTokenSource(token);var externalToken=token;token=stop.Token;
  lock(sync){if(disabled.Contains(key)||acceptedCount>=4&&!acceptedRoots.Contains(key)||helpers.Any(h=>Key(h.Root).Equals(key,StringComparison.OrdinalIgnoreCase))||auditStops.ContainsKey(key))return false;auditStops[key]=stop;auditing.Add(destination);}
  try {
   var volume=identity?.Invoke(destination);if(identity!=null&&volume==null)return false;
   foreach(var dir in plan.Directories){token.ThrowIfCancellationRequested();NoLinks(System.IO.Path.Combine(destination,dir));if(!Directory.Exists(System.IO.Path.Combine(destination,dir)))return false;}
   foreach(var file in plan.Files){token.ThrowIfCancellationRequested();ValidateHelper(file,System.IO.Path.Combine(destination,file.Relative));}
   if(identity!=null&&!string.Equals(volume,identity(destination),StringComparison.Ordinal))return false;
   lock(sync){if(disabled.Contains(key)||acceptedCount>=4&&!acceptedRoots.Contains(key))return false;helpers.Add(new(destination,volume));if(acceptedRoots.Add(key))acceptedCount++;}return true;
  }catch(OperationCanceledException)when(!externalToken.IsCancellationRequested){return false;}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
  finally{lock(sync){auditing.Remove(destination);auditStops.Remove(key);}}
 },token);
 public static void ValidateHelper(Entry file,string path){NoLinks(path);var info=new FileInfo(path);if(!info.Exists||info.Length!=file.Size||info.LastWriteTimeUtc!=file.Modified)throw new IOException("El archivo auxiliar cambió o está incompleto: "+file.Relative);}
 public static bool SameOrInside(string path,string root){var p=System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar);var r=System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar);return p.Equals(r,StringComparison.OrdinalIgnoreCase)||p.StartsWith(r+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
 public static void NoLinks(string path){for(string? p=System.IO.Path.GetFullPath(path);!string.IsNullOrEmpty(p);p=System.IO.Path.GetDirectoryName(p))if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Enlace no permitido: "+p);}
 public static void ValidatePrimary(Entry file){var info=new FileInfo(file.Source);if(info.Length!=file.Size||info.LastWriteTimeUtc!=file.Modified)throw new IOException("El origen cambió desde la planificación: "+file.Source);}
}
