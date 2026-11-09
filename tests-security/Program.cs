using RcopiesLicensing;
using DobleCommander;
using System.Security.Cryptography;
using System.Text.Json;

static class SecurityTests {
 static int passed;
 static void Assert(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);passed++;Console.WriteLine("PASS: "+name);}
 static bool Reject(Action action){try{action();return false;}catch(IOException){return true;}}
 static async Task<int> Main(string[] args){
  try{
   if(args.Length==3&&args[0]=="--verify-license"){LicenseCore.Verify(LicenseCore.ReadSmall(args[1]),File.ReadAllText(args[2]),LicenseCore.MachineId(),DateTimeOffset.UtcNow);Console.WriteLine("Owner PC license signature, identity and validity confirmed.");return 0;}
   if(args.Contains("--probe-budget")){ResourceBudget.Initialize();Assert(!ResourceBudget.LimitActive,"no native application RAM ceiling");Assert(AppContext.GetData("System.GC.HeapHardLimit")==null,"no configured CLR heap ceiling");Assert(ResourceBudget.Sample().Available>0,"real free RAM is measured");Console.WriteLine($"{passed} memory checks passed.");return 0;}
   using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);var publicKey=key.ExportSubjectPublicKeyInfoPem();var now=DateTimeOffset.UtcNow;var id=LicenseCore.Fingerprint("test-machine");
   var data=new LicenseData(1,LicenseCore.Product,Guid.NewGuid().ToString(),"Test customer",id,now.AddMinutes(-1),now.AddDays(1));var signed=LicenseCore.Sign(data,key);
   Assert(LicenseCore.Verify(signed,publicKey,id,now).Customer==data.Customer,"valid signed license activates the intended PC");
   Assert(Reject(()=>LicenseCore.Verify(signed,publicKey,LicenseCore.Fingerprint("other"),now)),"license cannot activate a different PC");
   Assert(Reject(()=>LicenseCore.Verify(signed,publicKey,id,now.AddDays(2))),"expired license is rejected");
   Assert(Reject(()=>LicenseCore.Verify(signed,publicKey,id,now.AddDays(-1))),"license cannot be used before issuance");
   using var other=ECDsa.Create(ECCurve.NamedCurves.nistP256);Assert(Reject(()=>LicenseCore.Verify(signed,other.ExportSubjectPublicKeyInfoPem(),id,now)),"an untrusted signing key is rejected");
   var envelope=JsonSerializer.Deserialize<LicenseEnvelope>(signed)!;var tampered=JsonSerializer.Serialize(envelope with{Payload=Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(data with{Customer="Modified"}))});
   Assert(Reject(()=>LicenseCore.Verify(tampered,publicKey,id,now)),"modifying signed customer or expiry data invalidates signature");
   Assert(Reject(()=>LicenseCore.Verify("not-json",publicKey,id,now)),"malformed license is rejected");
   Assert(Reject(()=>LicenseCore.Verify(new string('x',32769),publicKey,id,now)),"oversized license is rejected before parsing");
   var forever=LicenseCore.Sign(data with{ExpiresUtc=null},key);Assert(LicenseCore.Verify(forever,publicKey,id,now.AddYears(30)).ExpiresUtc==null,"owner can issue a perpetual PC-bound license");
   Assert(Reject(()=>LicenseCore.Sign(data with{Product="other-product"},key)),"license for a different product cannot be issued");
   Assert(Reject(()=>LicenseCore.Sign(data with{MachineId=""},key)),"missing PC identity is rejected");
   Assert(Reject(()=>LicenseCore.Sign(data with{ExpiresUtc=now.AddDays(-2)},key)),"expiry before issuance is rejected");
   Assert(LicenseCore.Fingerprint(" ABC ")==LicenseCore.Fingerprint("abc")&&LicenseCore.ValidId(LicenseCore.MachineId()),"stable normalized fingerprint and actual Windows PC identifier");
   var encrypted=key.ExportEncryptedPkcs8PrivateKeyPem("test-only-passphrase",new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc,HashAlgorithmName.SHA256,10000));using var recovered=ECDsa.Create();recovered.ImportFromEncryptedPem(encrypted,"test-only-passphrase");Assert(LicenseCore.Verify(LicenseCore.Sign(data,recovered),publicKey,id,now).LicenseId==data.LicenseId,"encrypted administrator key preserves signing authority");
   bool wrongPassword=false;try{using var bad=ECDsa.Create();bad.ImportFromEncryptedPem(encrypted,"wrong-password");}catch(CryptographicException){wrongPassword=true;}Assert(wrongPassword,"encrypted administrator key rejects wrong password");
   Assert(!ResourceBudget.LimitActive,"RAM ceiling is disabled");
   Assert(AppContext.GetData("System.GC.HeapHardLimit")==null,"CLR memory ceiling is absent");
   var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int active=0;
   var tasks=Enumerable.Range(0,40).Select(async _=>{using var transfer=await ResourceBudget.EnterAsync(CancellationToken.None);Interlocked.Increment(ref active);await release.Task;}).ToArray();
   bool allStarted=active==40;release.SetResult();await Task.WhenAll(tasks);Assert(allStarted,"all 40 transfers enter before any finishes, without a destination queue");
   using(var buffer=ResourceBudget.RentBuffer())Assert(buffer.Bytes.Length>=1024*1024,"copy buffers are pooled and bounded");
   var sample=ResourceBudget.Sample();Assert(sample.Resident>0&&sample.Committed>0&&sample.Total>0,"RAM meter obtains real process and system measurements");
   Console.WriteLine($"{passed} licensing/resource assertions passed.");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
}
