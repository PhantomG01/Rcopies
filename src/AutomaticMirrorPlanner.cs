namespace DobleCommander;

public sealed record AutomaticCopyPlan(Plan NormalPlan,string[] NewDestinations,MirrorPreview[] ChangedMirrors,string[] UnchangedFolders);
public sealed record AutomaticMirrorApproval(MirrorPreview[] Previews,bool EarlyPrune);

public static class AutomaticMirrorPlanner {
 public static async Task<AutomaticCopyPlan> AnalyzeAsync(string origin,string[] destinationUnits,bool contentsOnly,Func<string,string?>? identity=null,Func<string,long?>? freeBytes=null,IEnumerable<string>? protectedPaths=null,CancellationToken token=default){
  origin=Path.GetFullPath(origin);token.ThrowIfCancellationRequested();var normal=Plan.Create([origin],contentsOnly||MirrorPlanner.VolumeRoot(origin),token);
  var roots=destinationUnits.Select(Path.GetFullPath).ToArray();foreach(var root in roots)normal.Validate(root);
  for(int a=0;a<roots.Length;a++)for(int b=a+1;b<roots.Length;b++)if(SourcePool.SameOrInside(roots[a],roots[b])||SourcePool.SameOrInside(roots[b],roots[a]))throw new IOException("Los destinos no pueden solaparse.");
  if(!Directory.Exists(origin)||MirrorPlanner.VolumeRoot(origin))return new(normal,roots,[],[]);
  var name=Path.GetFileName(Path.TrimEndingDirectorySeparator(origin));var matched=new List<string>();var fresh=new List<string>();
  foreach(var root in roots){token.ThrowIfCancellationRequested();var candidate=Path.Combine(root,name);SourcePool.NoLinks(candidate);
   if(Directory.Exists(candidate))matched.Add(candidate);else if(File.Exists(candidate))throw new IOException("Existe un archivo con el nombre de la carpeta origen: "+candidate);else fresh.Add(root);
  }
  if(matched.Count==0)return new(normal,fresh.ToArray(),[],[]);
  MirrorPlanner.ValidateRoots(origin,matched,protectedPaths);var manifest=await MirrorPlanner.ReadSourceAsync(origin,identity,token);var changed=new List<MirrorPreview>();var unchanged=new List<string>();
  foreach(var folder in matched){token.ThrowIfCancellationRequested();var preview=await MirrorPlanner.PreviewAsync(manifest,folder,identity,freeBytes,token);if(preview.Changes.Length>0)changed.Add(preview);else unchanged.Add(folder);}
  return new(normal,fresh.ToArray(),changed.ToArray(),unchanged.ToArray());
 }
}
