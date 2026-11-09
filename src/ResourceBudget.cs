using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace DobleCommander;

public sealed record MemoryReading(long Resident,long Committed,long Available,long Total,bool LimitActive);
public static class ResourceBudget {
 static readonly object sync=new();static readonly Process process=Process.GetCurrentProcess();static long nextSample;
 static MemoryReading reading=new(0,0,0,0,false);
 public static bool LimitActive=>false;
 public static void Initialize(){
  if(!OperatingSystem.IsWindows()||!Environment.Is64BitProcess)throw new PlatformNotSupportedException("Rcopies necesita Windows de 64 bits.");
  try{process.PriorityClass=ProcessPriorityClass.BelowNormal;}catch(System.ComponentModel.Win32Exception){}
 }
 public static MemoryReading Sample(){lock(sync){if(Environment.TickCount64<nextSample)return reading;nextSample=Environment.TickCount64+1000;process.Refresh();var state=new MemoryStatus{Length=(uint)Marshal.SizeOf<MemoryStatus>()};if(OperatingSystem.IsWindows())GlobalMemoryStatusEx(ref state);return reading=new(process.WorkingSet64,process.PrivateMemorySize64,(long)state.AvailablePhysical,(long)state.TotalPhysical,LimitActive);}}
 // Each destination gets its own streaming operation, without a semaphore or RAM ceiling.
 public static Task<IDisposable> EnterAsync(CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult<IDisposable>(new Transfer());}
 sealed class Transfer:IDisposable{public void Dispose(){}}
 public static ValueTask YieldUnderPressureAsync(CancellationToken token){token.ThrowIfCancellationRequested();return ValueTask.CompletedTask;}
 public sealed class BufferLease:IDisposable{public byte[] Bytes{get;}=ArrayPool<byte>.Shared.Rent(1024*1024);bool disposed;public void Dispose(){if(!disposed){disposed=true;ArrayPool<byte>.Shared.Return(Bytes);}}}
 public static BufferLease RentBuffer()=>new();
 public static FileStream Open(string path,FileMode mode,FileAccess access,FileShare share,FileOptions options=FileOptions.Asynchronous|FileOptions.SequentialScan){var stream=new FileStream(path,mode,access,share,64*1024,options);if(OperatingSystem.IsWindows()){int low=1;SetFileInformationByHandle(stream.SafeFileHandle,12,ref low,4);}return stream;}
 [StructLayout(LayoutKind.Sequential)]struct MemoryStatus{public uint Length,Load;public ulong TotalPhysical,AvailablePhysical,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,AvailableExtended;}
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetFileInformationByHandle(SafeFileHandle file,int kind,ref int info,uint size);
}
