using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace RcopiesAdmin;

// Portable encrypted authority; the plaintext private key is never written to the export.
public static class MobileVault {
 public const int Iterations=600_000;
 public static string Export(ECDsa authority,string password){
  if(password.Length<12||password.Length>1024)throw new IOException("Usa una contraseña de entre 12 y 1024 caracteres.");
  var p=authority.ExportParameters(true);
  if(p.Curve.Oid.Value!="1.2.840.10045.3.1.7")throw new IOException("La autoridad debe ser ECDSA P-256.");
  static string Url(byte[] data)=>Convert.ToBase64String(data).TrimEnd('=').Replace('+','-').Replace('/','_');
  byte[]? clear=null,derived=null;
  try{
   clear=JsonSerializer.SerializeToUtf8Bytes(new{kty="EC",crv="P-256",x=Url(p.Q.X!),y=Url(p.Q.Y!),d=Url(p.D!),ext=true,key_ops=new[]{"sign"}});
   var salt=RandomNumberGenerator.GetBytes(16);var iv=RandomNumberGenerator.GetBytes(12);
   derived=Rfc2898DeriveBytes.Pbkdf2(password,salt,Iterations,HashAlgorithmName.SHA256,32);
   var ciphertext=new byte[clear.Length];var tag=new byte[16];
   using(var aes=new AesGcm(derived,16))aes.Encrypt(iv,clear,ciphertext,tag,Encoding.UTF8.GetBytes("Rcopies-iPhone-v1"));
   return JsonSerializer.Serialize(new{format="Rcopies-iPhone",version=1,kdf="PBKDF2-SHA256",iterations=Iterations,cipher="AES-256-GCM",salt=Convert.ToBase64String(salt),iv=Convert.ToBase64String(iv),data=Convert.ToBase64String(ciphertext.Concat(tag).ToArray())},new JsonSerializerOptions{WriteIndented=true});
  }finally{if(p.D!=null)CryptographicOperations.ZeroMemory(p.D);if(clear!=null)CryptographicOperations.ZeroMemory(clear);if(derived!=null)CryptographicOperations.ZeroMemory(derived);}
 }
}
