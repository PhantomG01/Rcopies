using System.Diagnostics;
using System.Text;
using System.Text.Json;
namespace DobleCommander;

internal static class VolumeFormatter {
 static string Quote(string value)=>"'"+value.Replace("'","''")+"'";
 static string PowerShell=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
 internal static string InspectionScript(DeviceVolume volume,IEnumerable<string> protectedPaths) {
  var roots=protectedPaths.Select(Path.GetPathRoot).Where(r=>r!=null&&r.Length>=2&&r[1]==':'&&char.IsAsciiLetter(r[0])).Select(r=>char.ToUpperInvariant(r![0]).ToString()).Distinct();
  var letters="@("+string.Join(",",roots.Select(Quote))+")";
  return "$ErrorActionPreference='Stop'; "+
   "$v=Get-Volume -DriveLetter "+Quote(volume.Drive[..1])+"; if($v.Path -ne "+Quote(volume.VolumePath)+"){throw 'La identidad del volumen cambió'}; "+
   "$p=Get-Partition -DriveLetter "+Quote(volume.Drive[..1])+"; $d=$p|Get-Disk; if(@($p).Count -ne 1 -or @($d).Count -ne 1){throw 'Disco ambiguo'}; "+
   "$blocked=$false;foreach($letter in "+letters+"){$other=Get-Partition -DriveLetter $letter|Get-Disk;if(@($other|Where-Object {$_.UniqueId -eq $d.UniqueId}).Count -gt 0){$blocked=$true}}; "+
   "$page=@(Get-CimInstance Win32_PageFileUsage|Where-Object {$_.Name.StartsWith("+Quote(volume.Drive)+",[StringComparison]::OrdinalIgnoreCase)}).Count -gt 0; "+
   "$gptType=([string]$p.GptType).Trim('{}'); $data=(!$p.IsHidden -and (($d.PartitionStyle -eq 'GPT' -and $gptType -eq 'ebd0a0a2-b9e5-4433-87c0-68b6b72699c7') -or ($d.PartitionStyle -eq 'MBR' -and $p.MbrType -in 6,7,11,12,14))); "+
   "$s=[ordered]@{VolumePath=[string]$v.Path;DiskId=[string]$d.UniqueId;IsBoot=[bool]($d.IsBoot -or $p.IsBoot);IsSystem=[bool]($d.IsSystem -or $p.IsSystem);IsReadOnly=[bool]($d.IsReadOnly -or $p.IsReadOnly);IsOffline=[bool]$d.IsOffline;HasPageFile=[bool]$page;ProtectedDisk=[bool]$blocked;DataPartition=[bool]$data}; ";
 }
 static string Encode(string script)=>Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
 public static async Task<FormatSafety> InspectAsync(DeviceVolume volume,string[] protectedPaths) {
  var start=new ProcessStartInfo(PowerShell){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8};
  foreach(var arg in new[]{"-NoProfile","-NonInteractive","-EncodedCommand",Encode("[Console]::OutputEncoding=[Text.Encoding]::UTF8; "+InspectionScript(volume,protectedPaths)+"$s|ConvertTo-Json -Compress")})start.ArgumentList.Add(arg);
  using var process=Process.Start(start)??throw new IOException("No se pudo consultar el disco.");
  var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
  try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);throw new IOException("La consulta de seguridad del disco agotó el tiempo.");}
  if(process.ExitCode!=0)throw new IOException("No se pudo comprobar la seguridad del disco. "+await error);
  return JsonSerializer.Deserialize<FormatSafety>(await output,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new IOException("Respuesta de disco no válida.");
 }
 public static async Task FormatAsync(DeviceVolume volume,FormatSafety safety,string typed,string fs,string[] protectedPaths,string logDirectory) {
  FormattingPolicy.Validate(safety,volume.VolumePath,typed,volume.Drive,fs);
  if(VolumeMonitor.Read(volume.Root)?.Identity!=volume.Identity)throw new IOException("El dispositivo cambió antes del formateo.");
  Directory.CreateDirectory(logDirectory);var result=Path.Combine(logDirectory,"format-"+Guid.NewGuid().ToString("N")+".json");
  // Recheck the exact volume and physical disk inside the elevated helper, immediately before formatting.
  var script=BuildFormatScript(volume,safety,fs,protectedPaths,result);
  var start=new ProcessStartInfo(PowerShell){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
  foreach(var arg in new[]{"-NoProfile","-NonInteractive","-EncodedCommand",Encode(script)})start.ArgumentList.Add(arg);
  using var process=Process.Start(start)??throw new IOException("No se pudo iniciar el formateo.");await process.WaitForExitAsync();
  if(!File.Exists(result))throw new IOException("El formateo no produjo un resultado verificable. No se iniciará ninguna copia.");
  using var json=JsonDocument.Parse(await File.ReadAllTextAsync(result));if(process.ExitCode!=0||!json.RootElement.GetProperty("Success").GetBoolean())throw new IOException(json.RootElement.TryGetProperty("Error",out var message)?message.GetString():"El formateo falló.");
 }
 internal static string BuildFormatScript(DeviceVolume volume,FormatSafety safety,string fs,string[] protectedPaths,string result) {
  return "try {"+InspectionScript(volume,protectedPaths)+
   "if($s.DiskId -ne "+Quote(safety.DiskId)+" -or $s.IsBoot -or $s.IsSystem -or $s.IsReadOnly -or $s.IsOffline -or $s.HasPageFile -or $s.ProtectedDisk -or !$s.DataPartition){throw 'Se bloqueó el formateo por seguridad'}; "+
   "Format-Volume -Path "+Quote(volume.VolumePath)+" -FileSystem "+Quote(fs)+" -NewFileSystemLabel 'COPIA' -Force -Confirm:$false -ErrorAction Stop|Out-Null; "+
   "@{Success=$true;Volume="+Quote(volume.VolumePath)+"}|ConvertTo-Json|Set-Content -LiteralPath "+Quote(result)+" -Encoding UTF8; exit 0"+
   "} catch {@{Success=$false;Error=$_.Exception.Message}|ConvertTo-Json|Set-Content -LiteralPath "+Quote(result)+" -Encoding UTF8;exit 1}";
 }
}
