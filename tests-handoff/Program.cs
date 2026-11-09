using DobleCommander;

static class HandoffTests {
 static int passed;
 static void Assert(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);passed++;}
 static async Task Until(Func<bool> condition){var end=DateTime.UtcNow.AddSeconds(20);while(!condition()){if(DateTime.UtcNow>end)throw new Exception("Timeout waiting for handoff");await Task.Delay(1);}}
 static async Task<int> Main(){
  var root=Path.Combine(Path.GetTempPath(),"Rcopies-handoff-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);CopyJob? first=null,second=null;Task? t1=null,t2=null;
  try{
   string Dir(string name){var path=Path.Combine(root,name);Directory.CreateDirectory(path);return path;}
   var source=Dir("source");var sourceFile=Path.Combine(source,"data.bin");using(var file=File.Create(sourceFile))file.SetLength(64L*1024*1024);var plan=Plan.Create([source],true);
   var helperA=Dir("helper-a");var helperB=Dir("helper-b");File.Copy(sourceFile,Path.Combine(helperA,"data.bin"));File.Copy(sourceFile,Path.Combine(helperB,"data.bin"));var pool=new SourcePool(plan);
   Assert(await pool.PromoteAsync(helperA)&&await pool.PromoteAsync(helperB),"two complete destinations pass audit and become helpers");
   var logs=Dir("logs");var targetA=Dir("target-a");var targetB=Dir("target-b");
   first=new CopyJob(plan,targetA,Conflict.Overwrite,true,logs,pool,originalsActive:()=>true);t1=first.RunAsync();await Until(()=>first.Status.Done>0&&first.ReadSource.StartsWith(helperA,StringComparison.OrdinalIgnoreCase));
   second=new CopyJob(plan,targetB,Conflict.Overwrite,true,logs,pool,originalsActive:()=>true);t2=second.RunAsync();await Until(()=>second.Status.Done>0&&second.ReadSource.StartsWith(helperB,StringComparison.OrdinalIgnoreCase));
   long before=first.Status.Done,otherBefore=second.Status.Done;pool.SetEnabled(helperA,false);
   await Until(()=>first.ReadSource.StartsWith(helperB,StringComparison.OrdinalIgnoreCase)&&pool.ActiveReaders(helperA)==0);
   Assert(first.Status.Done>=before,"handoff preserves the current byte offset and progress");
   Assert(!first.Token.IsCancellationRequested&&!second.Token.IsCancellationRequested,"disabling helper cancels neither beneficiary copy");
   Assert(first.Status.State!="Pausado"&&second.Status.State!="Pausado","handoff does not pause active copies");
   Assert(first.ReadSource.StartsWith(helperB,StringComparison.OrdinalIgnoreCase)&&pool.HelperCount==1,"beneficiary switches to the next enabled helper");
   await Task.WhenAll(t1,t2);
   Assert(first.Status.State=="Completado"&&second.Status.State=="Completado","both copies finish successfully after helper opt-out");
   Assert(first.Status.Written==plan.Bytes&&second.Status.Written==plan.Bytes&&second.Status.Done>=otherBefore,"no destination is restarted or recopied during handoff");
   var expected=await TestFiles.Hash(sourceFile,CancellationToken.None);
   Assert((await TestFiles.Hash(Path.Combine(targetA,"data.bin"),CancellationToken.None)).SequenceEqual(expected)&&(await TestFiles.Hash(Path.Combine(targetB,"data.bin"),CancellationToken.None)).SequenceEqual(expected),"both final SHA-256 hashes match the original");
   Assert(File.ReadLines(first.LogPath).Any(l=>l.Contains("auxiliary-handoff",StringComparison.Ordinal)),"live helper transfer is recorded without an error or cancellation");
   Console.WriteLine($"{passed} live handoff assertions passed.");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{first?.Cancel();second?.Cancel();if(t1!=null)await t1;if(t2!=null)await t2;if(Directory.Exists(root))Directory.Delete(root,true);}
 }
}
