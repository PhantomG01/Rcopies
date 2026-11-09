using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace DobleCommander;

internal sealed record EjectCandidate(string Root,string VolumeIdentity,string DeviceId,uint Node,int[] Disks);
internal interface IUnitEjector {
 Task<EjectCandidate?> QueryAsync(string root);
 int? DiskForPath(string path);
 Task EjectAsync(EjectCandidate candidate);
}

// Requests removal through PnP; never forces a dismount or disables a device.
internal sealed class WindowsUnitEjector:IUnitEjector {
 static readonly Guid diskInterface=new("53f56307-b6bf-11d0-94f2-00a0c91efb8b");
 sealed record Disk(uint Node,int Number);
 public Task<EjectCandidate?> QueryAsync(string root)=>Task.Run(()=>Query(root));
 EjectCandidate? Query(string root){
  if(StorageUnits.Read(root)==null)return null;
  var number=DiskForPath(root);if(number==null)return null;
  var windowsDisk=DiskForPath(Environment.SystemDirectory);if(windowsDisk==null||number==windowsDisk)return null;
  var volume=VolumeMonitor.Read(root);if(volume==null)return null;
  var disks=Disks();var disk=disks.FirstOrDefault(d=>d.Number==number);if(disk==null)return null;
  uint node=disk.Node;string id="";bool removable=false;
  for(int level=0;level<4;level++){
   id=DeviceId(node);if(id.Length==0||id.Contains("ROOT_HUB",StringComparison.OrdinalIgnoreCase))break;
   // Never walk past the disk/USB-storage chain into a hub or PCI controller.
   if(level>0&&!id.StartsWith("USB\\VID_",StringComparison.OrdinalIgnoreCase)&&!id.StartsWith("USBSTOR\\",StringComparison.OrdinalIgnoreCase)&&!id.StartsWith("SCSI\\",StringComparison.OrdinalIgnoreCase))break;
   var caps=Capabilities(node);if((caps&6)!=0){removable=true;break;}
   if(!CanAscendPast(id))break;
   if(CM_Get_Parent(out var parent,node,0)!=0)break;node=parent;
  }
  if(!removable)return null;
  var affected=disks.Where(d=>Descendant(d.Node,node)).Select(d=>d.Number).Distinct().ToArray();
  if(affected.Length==0||affected.Any(n=>n<0)||affected.Contains(windowsDisk.Value))return null;
  return new(root,volume.Identity,id,node,affected);
 }
 internal static bool CanAscendPast(string deviceId)=>!deviceId.StartsWith("USB\\VID_",StringComparison.OrdinalIgnoreCase)||deviceId.Split('\\')[1].Contains("&MI_",StringComparison.OrdinalIgnoreCase);
 public async Task EjectAsync(EjectCandidate candidate){
  var current=await QueryAsync(candidate.Root);if(current==null||current.VolumeIdentity!=candidate.VolumeIdentity||current.DeviceId!=candidate.DeviceId||!current.Disks.Order().SequenceEqual(candidate.Disks.Order()))throw new IOException("La unidad cambió o dejó de admitir desconexión. No se desconectó ningún dispositivo.");
  await Task.Run(()=>{var veto=new StringBuilder(1024);var result=CM_Request_Device_Eject(current.Node,out var type,veto,(uint)veto.Capacity,0);if(result!=0)throw new IOException($"Windows no permitió desconectar la unidad (código {result}, veto {type}). Cierra los archivos o programas que la estén usando e inténtalo de nuevo. {veto}");});
 }
 public int? DiskForPath(string path){
  try{
   string? existing=Path.GetFullPath(path);while(existing!=null&&!Directory.Exists(existing)&&!File.Exists(existing))existing=Path.GetDirectoryName(existing);if(existing==null)return null;
   var mount=new StringBuilder(1024);if(!GetVolumePathName(existing,mount,(uint)mount.Capacity))return null;
   var volume=new StringBuilder(1024);if(!GetVolumeNameForVolumeMountPoint(mount.ToString(),volume,(uint)volume.Capacity))return null;
   return Number(volume.ToString().TrimEnd('\\'));
  }catch(ArgumentException){return null;}catch(IOException){return null;}
 }
 static int? Number(string device){using var handle=CreateFile(device,0,3,IntPtr.Zero,3,0,IntPtr.Zero);if(handle.IsInvalid)return null;return DeviceIoControl(handle,0x2D1080,IntPtr.Zero,0,out var number,(uint)Marshal.SizeOf<DeviceNumber>(),out _,IntPtr.Zero)?checked((int)number.Number):null;}
 static List<Disk> Disks(){
  var guid=diskInterface;var set=SetupDiGetClassDevs(ref guid,null,IntPtr.Zero,0x12);if(set==new IntPtr(-1))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());var result=new List<Disk>();
  try{for(uint index=0;;index++){
   var entry=new InterfaceData{Size=(uint)Marshal.SizeOf<InterfaceData>()};if(!SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref guid,index,ref entry)){if(Marshal.GetLastWin32Error()!=259)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());break;}
   SetupDiGetDeviceInterfaceDetail(set,ref entry,IntPtr.Zero,0,out uint size,IntPtr.Zero);if(size<8||size>1024*1024)throw new IOException("No se pudo identificar el dispositivo de almacenamiento.");
   var buffer=Marshal.AllocHGlobal((int)size);try{Marshal.WriteInt32(buffer,IntPtr.Size==8?8:6);var info=new DeviceInfo{Size=(uint)Marshal.SizeOf<DeviceInfo>()};if(!SetupDiGetDeviceInterfaceDetailInfo(set,ref entry,buffer,size,out _,ref info))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());var path=Marshal.PtrToStringUni(buffer+4)??"";result.Add(new(info.Node,Number(path)??-1));}finally{Marshal.FreeHGlobal(buffer);}
  }}finally{SetupDiDestroyDeviceInfoList(set);}return result;
 }
 static bool Descendant(uint node,uint parent){for(int depth=0;depth<16;depth++){if(node==parent)return true;if(CM_Get_Parent(out var next,node,0)!=0)break;node=next;}return false;}
 static string DeviceId(uint node){var id=new StringBuilder(1024);return CM_Get_Device_ID(node,id,(uint)id.Capacity,0)==0?id.ToString():"";}
 static uint Capabilities(uint node){uint size=4;return CM_Get_DevNode_Registry_Property(node,0x10,out _,out uint flags,ref size,0)==0?flags:0;}
 [StructLayout(LayoutKind.Sequential)]struct InterfaceData{public uint Size;public Guid Guid;public uint Flags;public nuint Reserved;}
 [StructLayout(LayoutKind.Sequential)]struct DeviceInfo{public uint Size;public Guid ClassGuid;public uint Node;public nuint Reserved;}
 [StructLayout(LayoutKind.Sequential)]struct DeviceNumber{public uint Type,Number,Partition;}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool DeviceIoControl(SafeFileHandle file,uint code,IntPtr input,uint inputLength,out DeviceNumber output,uint length,out uint returned,IntPtr overlapped);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetVolumePathName(string path,StringBuilder volume,uint length);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetVolumeNameForVolumeMountPoint(string path,StringBuilder volume,uint length);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr SetupDiGetClassDevs(ref Guid guid,string? enumerator,IntPtr parent,uint flags);
 [DllImport("setupapi.dll",SetLastError=true)]static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr device,ref Guid guid,uint index,ref InterfaceData data);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set,ref InterfaceData data,IntPtr detail,uint size,out uint required,IntPtr info);
 [DllImport("setupapi.dll",EntryPoint="SetupDiGetDeviceInterfaceDetailW",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiGetDeviceInterfaceDetailInfo(IntPtr set,ref InterfaceData data,IntPtr detail,uint size,out uint required,ref DeviceInfo info);
 [DllImport("setupapi.dll",SetLastError=true)]static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
 [DllImport("cfgmgr32.dll")]static extern uint CM_Get_Parent(out uint parent,uint node,uint flags);
 [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Get_Device_ID(uint node,StringBuilder buffer,uint length,uint flags);
 [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Get_DevNode_Registry_Property(uint node,uint property,out uint type,out uint buffer,ref uint length,uint flags);
 [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Request_Device_Eject(uint node,out uint veto,StringBuilder name,uint length,uint flags);
}
