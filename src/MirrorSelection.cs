namespace DobleCommander;

public static partial class MirrorPlanner {
 // Derive an immutable plan, including its expected final tree. Unselected data
 // remains in that tree and therefore must not be pruned by the executor.
 public static MirrorPreview SelectChanges(MirrorPreview preview,IReadOnlySet<string>? selected){
  if(selected==null)return preview;
  foreach(var path in selected)if(!preview.Source.Nodes.ContainsKey(path)&&!preview.DestinationNodes.ContainsKey(path))throw new IOException("Selección ajena al análisis: "+path);
  var requested=selected.ToHashSet(StringComparer.OrdinalIgnoreCase);
  static bool UnderRoots(string path,IReadOnlySet<string> roots){for(string? current=path;!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current))if(roots.Contains(current))return true;return false;}
  bool Chosen(string path)=>UnderRoots(path,requested);
  if(preview.Changes.All(c=>Chosen(c.Relative)))return preview;
  var selectedParents=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var path in requested)for(string? current=path;!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current))selectedParents.Add(current);
  var blockers=preview.BlockerRoots.Where(b=>Chosen(b)||selectedParents.Contains(b)).ToArray();var blockerSet=blockers.ToHashSet(StringComparer.OrdinalIgnoreCase);
  bool InBlocker(string path)=>UnderRoots(path,blockerSet);
  var files=preview.CopyFiles.Where(f=>Chosen(f.Relative)||InBlocker(f.Relative)).ToArray();
  var removes=MinimalRoots(preview.DestinationNodes.Keys.Where(path=>!preview.Source.Nodes.ContainsKey(path)&&Chosen(path)&&!InBlocker(path)));
  var fileParents=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var f in files)for(var parent=Path.GetDirectoryName(f.Relative);!string.IsNullOrEmpty(parent);parent=Path.GetDirectoryName(parent))fileParents.Add(parent);
  var dirs=preview.Source.Plan.Directories.Where(d=>d.Length>0&&(Chosen(d)||InBlocker(d)||fileParents.Contains(d))).Prepend("").ToArray();
  var expected=preview.DestinationNodes.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.OrdinalIgnoreCase);
  var removedRoots=removes.Concat(blockers).ToHashSet(StringComparer.OrdinalIgnoreCase);foreach(var path in expected.Keys.Where(path=>UnderRoots(path,removedRoots)).ToArray())expected.Remove(path);
  foreach(var d in dirs.Where(d=>d.Length>0))expected[d]=preview.Source.Nodes[d];
  foreach(var f in files)expected[f.Relative]=preview.Source.Nodes[f.Relative];
  var changes=new List<MirrorChange>();
  foreach(var f in files)changes.Add(new(preview.DestinationNodes.ContainsKey(f.Relative)?"Actualizar":"Añadir",f.Relative,f.Size));
  foreach(var d in dirs.Where(d=>d.Length>0))if(!preview.DestinationNodes.TryGetValue(d,out var old)||!old.Directory)changes.Add(new("Crear carpeta",d,0));
  changes.AddRange(blockers.Select(b=>new MirrorChange("Reemplazar tipo",b,0)));
  foreach(var d in removes)changes.Add(new("Eliminar",d,preview.DestinationNodes.Values.Where(n=>!n.Directory&&IsUnder(n.Relative,d)).Sum(n=>n.Size)));
  long delta=0,peak=0;
  foreach(var f in files){peak=Math.Max(peak,delta+f.Size);long old=preview.DestinationNodes.TryGetValue(f.Relative,out var node)&&!node.Directory&&!InBlocker(f.Relative)?node.Size:0;delta+=f.Size-old;}
  long reclaim=preview.DestinationNodes.Values.Where(n=>!n.Directory&&UnderRoots(n.Relative,removedRoots)).Sum(n=>n.Size);
  return preview with{CopyFiles=files,RemoveRoots=removes,BlockerRoots=blockers,Changes=changes.ToArray(),PeakBytes=peak,ReclaimableBytes=reclaim,ExpectedNodes=expected,SelectedDirectories=dirs};
 }
}
