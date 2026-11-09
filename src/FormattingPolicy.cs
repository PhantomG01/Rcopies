namespace DobleCommander;

public sealed record FormatSafety(string VolumePath,string DiskId,bool IsBoot,bool IsSystem,bool IsReadOnly,bool IsOffline,bool HasPageFile,bool ProtectedDisk,bool DataPartition);
public static class FormattingPolicy {
 public static void ValidateDevice(FormatSafety safety,string expectedVolume) {
  if(!safety.VolumePath.Equals(expectedVolume,StringComparison.OrdinalIgnoreCase)||string.IsNullOrWhiteSpace(safety.DiskId))throw new IOException("La identidad del volumen cambió o no pudo comprobarse.");
  if(safety.IsBoot||safety.IsSystem||safety.IsReadOnly||safety.IsOffline||safety.HasPageFile||safety.ProtectedDisk||!safety.DataPartition)throw new IOException("Se bloqueó el formateo: disco de sistema, protegido, ocupado, sin acceso o partición no admitida.");
 }
 public static void Validate(FormatSafety safety,string expectedVolume,string typed,string drive,string fileSystem) {
  if(!drive.EndsWith(':')||drive.Length!=2||drive[0]<'A'||drive[0]>'Z')throw new IOException("Letra de unidad no válida.");
  if(typed!="BORRAR "+drive)throw new IOException("La confirmación escrita no coincide con la unidad.");
  if(fileSystem is not ("NTFS" or "exFAT"))throw new IOException("Sistema de archivos no admitido.");
  ValidateDevice(safety,expectedVolume);
 }
}
