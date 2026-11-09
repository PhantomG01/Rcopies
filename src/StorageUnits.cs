namespace DobleCommander;

internal sealed record StorageUnit(string Root,string Label,DriveType Type,long Capacity,long Free,string FileSystem);
internal static class StorageUnits {
 internal static bool Eligible(DriveType type,long capacity)=>type is DriveType.Fixed or DriveType.Removable&&capacity>0;
 internal static string Normalize(string path)=>Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim().Trim('"')));
 internal static bool SystemVolume(string path){var windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);if(string.IsNullOrWhiteSpace(windows))windows=Environment.SystemDirectory;var root=Path.GetPathRoot(windows);return root!=null&&SourcePool.SameOrInside(windows,path)||root!=null&&Normalize(path).Equals(Normalize(root),StringComparison.OrdinalIgnoreCase);}
 internal static StorageUnit? Read(string path){
  try{
   if(!Path.IsPathFullyQualified(path)||SystemVolume(path))return null;var full=Normalize(path);var root=Path.GetPathRoot(full);
   if(root==null||!full.Equals(Normalize(root),StringComparison.OrdinalIgnoreCase))return null;
   var drive=new DriveInfo(root);
   if(drive.DriveType is not (DriveType.Fixed or DriveType.Removable)||!drive.IsReady||!Eligible(drive.DriveType,drive.TotalSize))return null;
   return new(full,string.IsNullOrWhiteSpace(drive.VolumeLabel)?"Sin etiqueta":drive.VolumeLabel,drive.DriveType,drive.TotalSize,drive.AvailableFreeSpace,drive.DriveFormat);
  }catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}catch(ArgumentException){return null;}
 }
 internal static StorageUnit Require(string path,Func<string,StorageUnit?> read){
  if(!Path.IsPathFullyQualified(path))throw new IOException("Selecciona la raíz de una unidad de almacenamiento.");
  var full=Normalize(path);var unit=read(full);
  if(SystemVolume(full)||unit==null||!full.Equals(Normalize(unit.Root),StringComparison.OrdinalIgnoreCase)||!Eligible(unit.Type,unit.Capacity))throw new IOException("Selecciona una unidad local o extraíble lista. No se admiten carpetas como destinos, unidades ópticas ni la unidad de Windows.");
  return unit;
 }
 internal static StorageUnit[] Scan()=>DriveInfo.GetDrives().Select(d=>Read(d.Name)).OfType<StorageUnit>().ToArray();
}

internal static class RemainingTime {
 internal static double? Estimate(double done,double total,double seconds){
  if(done<=0||total<=done||seconds<=0)return null;double remaining=(total-done)*seconds/done;return double.IsFinite(remaining)?Math.Max(0,remaining):null;
 }
 internal static string Format(double seconds){var span=TimeSpan.FromSeconds(Math.Clamp(seconds,0,315360000));return span.Days>0?$"{span.Days} d {span:hh\\:mm\\:ss}":span.ToString(@"hh\:mm\:ss");}
}
