using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using System.Globalization;
namespace RcopiesLicensing;

public sealed record LicenseData(int Version,string Product,string LicenseId,string Customer,string MachineId,DateTimeOffset IssuedUtc,DateTimeOffset? ExpiresUtc);
public sealed record LicenseEnvelope(string Payload,string Signature);
public static class LicenseCore {
 public const string Product="clonova-1";
 public static string MachineId(){
  using var registry=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);using var key=registry.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");var guid=key?.GetValue("MachineGuid") as string;
  if(string.IsNullOrWhiteSpace(guid))throw new IOException("No se pudo leer el identificador de este Windows.");return Fingerprint(guid);
 }
 public static string Fingerprint(string windowsId)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Clonova-PC-v1|"+windowsId.Trim().ToLowerInvariant())));
 public static string NormalizeId(string id)=>new string(id.Where(c=>c!='-'&&!char.IsWhiteSpace(c)&&CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.Format).ToArray()).ToUpperInvariant();
 public static bool ValidId(string? id)=>id is {Length:63 or 64}&&id.All(Uri.IsHexDigit);
 public static bool MatchesMachine(string licensedId,string machine){
  var expected=NormalizeId(licensedId);var actual=NormalizeId(machine);
  if(!ValidId(expected)||!ValidId(actual))return false;
  if(expected.Equals(actual,StringComparison.Ordinal))return true;
  // Compatibility for a signed 63-character ID: require exactly one omitted hex
  // character from this PC's complete fingerprint, never an arbitrary prefix.
  if(expected.Length==63&&actual.Length==64)for(int i=0;i<actual.Length;i++)if(actual.Remove(i,1).Equals(expected,StringComparison.Ordinal))return true;
  return false;
 }
 public static string Sign(LicenseData data,ECDsa privateKey){ValidateFields(data);var payload=JsonSerializer.SerializeToUtf8Bytes(data);var signature=privateKey.SignData(payload,HashAlgorithmName.SHA256);return JsonSerializer.Serialize(new LicenseEnvelope(Convert.ToBase64String(payload),Convert.ToBase64String(signature)),new JsonSerializerOptions{WriteIndented=true});}
 public static LicenseData Verify(string document,string publicPem,string machine,DateTimeOffset now){
  if(document.Length>32768)throw new IOException("Archivo de licencia demasiado grande.");
  try{var envelope=JsonSerializer.Deserialize<LicenseEnvelope>(document)??throw new IOException("Licencia vacía.");var payload=Convert.FromBase64String(envelope.Payload);var signature=Convert.FromBase64String(envelope.Signature);using var key=ECDsa.Create();key.ImportFromPem(publicPem);
   if(!key.VerifyData(payload,signature,HashAlgorithmName.SHA256))throw new IOException("La firma de la licencia no es válida.");var data=JsonSerializer.Deserialize<LicenseData>(payload)??throw new IOException("Contenido de licencia no válido.");ValidateFields(data);
   if(!MatchesMachine(data.MachineId,machine))throw new IOException("Esta licencia pertenece a otro PC.");
   if(data.IssuedUtc>now.AddMinutes(5))throw new IOException("La fecha del equipo es anterior a la emisión de la licencia.");
   if(data.ExpiresUtc is {} end&&now>=end)throw new IOException("La licencia ha vencido. Solicita una renovación a Noel R.");return data;
  }catch(Exception e)when(e is JsonException or FormatException or CryptographicException or ArgumentException){throw new IOException("El archivo de licencia no es válido.",e);}
 }
 static void ValidateFields(LicenseData data){if(data.Version!=1||data.Product!=Product||!ValidId(data.MachineId)||string.IsNullOrWhiteSpace(data.Customer)||data.Customer.Length>200||!Guid.TryParse(data.LicenseId,out _)||data.ExpiresUtc<=data.IssuedUtc)throw new IOException("Los datos de licencia no son válidos para Rcopies.");}
 public static string ReadSmall(string path){var info=new FileInfo(path);if(info.Length>32768)throw new IOException("Archivo de licencia demasiado grande.");return File.ReadAllText(path);}
}
