using System.Runtime.InteropServices;
using System.Text;
namespace DobleCommander;

internal sealed record DeviceVolume(string Root,string VolumePath,uint Serial,string Label,long Size,string FileSystem,DriveType Type) {
 public string Identity=>VolumePath+"|"+Serial.ToString("X8");
 public string Drive=>Root.TrimEnd('\\');
}
internal sealed class VolumeMonitor {
 HashSet<string> previous=new(StringComparer.OrdinalIgnoreCase);
 public void Baseline(DeviceVolume[] volumes)=>previous=volumes.Select(v=>v.Identity).ToHashSet(StringComparer.OrdinalIgnoreCase);
 public DeviceVolume[] Arrivals(DeviceVolume[] volumes){var result=volumes.Where(v=>!previous.Contains(v.Identity)).ToArray();Baseline(volumes);return result;}
 public static DeviceVolume[] Scan(){var result=new List<DeviceVolume>();foreach(var drive in DriveInfo.GetDrives())try{if(drive.DriveType is DriveType.Fixed or DriveType.Removable && Read(drive.Name) is {} v&&StorageUnits.Eligible(v.Type,v.Size)&&!StorageUnits.SystemVolume(v.Root))result.Add(v);}catch(IOException){}catch(UnauthorizedAccessException){}return result.ToArray();}
 public static string? IdentityFor(string path) {
  try {
   var full=Path.GetFullPath(path);var root=new StringBuilder(1024);
   if(!GetVolumePathName(full,root,root.Capacity)) {
    string? parent=full;while(parent!=null&&!Directory.Exists(parent)&&!File.Exists(parent))parent=Path.GetDirectoryName(parent);
    if(parent==null||!GetVolumePathName(parent,root,root.Capacity))return null;
   }
   var guid=new StringBuilder(1024);if(!GetVolumeNameForVolumeMountPoint(root.ToString(),guid,guid.Capacity))return null;
   var label=new StringBuilder(261);var fs=new StringBuilder(261);if(!GetVolumeInformation(root.ToString(),label,label.Capacity,out uint serial,out _,out _,fs,fs.Capacity))return null;
   return guid+"|"+serial.ToString("X8");
  }catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}
 }
 public static DeviceVolume? Read(string root) {
  var guid=new StringBuilder(1024);if(!GetVolumeNameForVolumeMountPoint(root,guid,guid.Capacity))return null;
  var label=new StringBuilder(261);var fs=new StringBuilder(261);if(!GetVolumeInformation(root,label,label.Capacity,out uint serial,out _,out _,fs,fs.Capacity))return null;
  var d=new DriveInfo(root);return new(root,guid.ToString(),serial,label.ToString(),d.TotalSize,fs.ToString(),d.DriveType);
 }
 public static long? FreeBytesFor(string path){try{string? parent=Path.GetFullPath(path);while(parent!=null&&!Directory.Exists(parent))parent=Path.GetDirectoryName(parent);if(parent==null||!GetDiskFreeSpaceEx(parent,out ulong free,out _,out _))return null;return free>long.MaxValue?long.MaxValue:(long)free;}catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetVolumeNameForVolumeMountPoint(string root,StringBuilder volume,int size);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetVolumePathName(string path,StringBuilder root,int size);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetVolumeInformation(string root,StringBuilder label,int length,out uint serial,out uint component,out uint flags,StringBuilder fs,int fsLength);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetDiskFreeSpaceEx(string directory,out ulong available,out ulong total,out ulong free);
}
