using System.Runtime.InteropServices;

namespace DobleCommander;

// Shell icon handles belong to the caller. Clone before releasing the native handle.
internal static class WindowsIcons {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
 struct StockInfo {
  public uint Size;public IntPtr Icon;public int SystemIndex;public int IconIndex;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Path;
 }
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
 struct FileInfo {
  public IntPtr Icon;public int Index;public uint Attributes;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string DisplayName;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]public string TypeName;
 }
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SHGetStockIconInfo(uint id,uint flags,ref StockInfo info);
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SHGetFileInfo(string path,uint attributes,out FileInfo info,uint size,uint flags);
 [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
 static Icon? CloneAndRelease(IntPtr handle) {
  if(handle==IntPtr.Zero)return null;
  try{return (Icon)Icon.FromHandle(handle).Clone();}finally{DestroyIcon(handle);}
 }
 public static Icon Stock(uint id) {
  var info=new StockInfo(){Size=(uint)Marshal.SizeOf<StockInfo>(),Path=""};
  return SHGetStockIconInfo(id,0x100|0x1,ref info)==0 ? CloneAndRelease(info.Icon)??(Icon)SystemIcons.Application.Clone() : (Icon)SystemIcons.Application.Clone();
 }
 public static Icon? ForPath(string path) {
  var result=SHGetFileInfo(path,0,out var info,(uint)Marshal.SizeOf<FileInfo>(),0x100|0x1);
  return result==IntPtr.Zero?null:CloneAndRelease(info.Icon);
 }
 public static Icon? ForExtension(string extension) {
  var result=SHGetFileInfo("file"+extension,0x80,out var info,(uint)Marshal.SizeOf<FileInfo>(),0x100|0x1|0x10);
  return result==IntPtr.Zero?null:CloneAndRelease(info.Icon);
 }
}
