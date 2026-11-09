using DobleCommander;
using System.Security.Cryptography;

var root=Path.Combine(Path.GetTempPath(),"Rcopies-parallel-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
CopyJob[] jobs=[];Task[] tasks=[];int passed=0;
void Assert(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS: "+name);passed++;}
try{
 var source=Path.Combine(root,"source");Directory.CreateDirectory(source);
 File.WriteAllBytes(Path.Combine(source,"first.bin"),RandomNumberGenerator.GetBytes(1024*1024));
 File.WriteAllBytes(Path.Combine(source,"second.bin"),RandomNumberGenerator.GetBytes(1024*1024));
 var plan=Plan.Create([source],true);var pool=new SourcePool(plan);
 jobs=Enumerable.Range(0,40).Select(i=>new CopyJob(plan,Path.Combine(root,"target-"+i),Conflict.Overwrite,true,Path.Combine(root,"logs"),pool,1,token=>release.Task.WaitAsync(token))).ToArray();
 tasks=jobs.Select(j=>j.RunAsync()).ToArray();
 var end=DateTime.UtcNow.AddSeconds(30);
 while(jobs.Any(j=>j.Status.Done==0)&&DateTime.UtcNow<end)await Task.Delay(5);
 Assert(jobs.All(j=>j.Status.Done>0&&!j.CopyCompleted.IsCompleted),"40 real destinations advance before any is allowed to finish");
 Assert(pool.HelperCount==0,"unfinished destinations are never auxiliary sources");
 release.SetResult();await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(40));
 Assert(jobs.All(j=>j.Status.State=="Completado"&&j.Status.Done==j.Status.Total),"all 40 destinations finish at 100 percent");
 bool Matches(CopyJob j)=>plan.Files.All(f=>SHA256.HashData(File.ReadAllBytes(f.Source)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(Path.Combine(j.Destination,f.Relative)))));
 Assert(jobs.All(Matches),"all destination bytes match both original files");
 var batch=new CopyBatch(plan,Conflict.Overwrite,true,Path.Combine(root,"batch-logs"));
 for(int i=0;i<10;i++)batch.AddDestination(Path.Combine(root,"batch-"+i));batch.SealInitial();await batch.WaitAsync().WaitAsync(TimeSpan.FromSeconds(40));
 Assert(batch.Jobs.All(j=>j.Status.State=="Completado")&&batch.Sources.HelperCount==4&&batch.Sources.HelperRoots.All(r=>batch.Jobs.Any(j=>j.Destination==r&&j.Status.Done==j.Status.Total)),"only four fully completed destinations become auxiliary sources without hashing");
 using(var lockedSource=File.Open(plan.Files[0].Source,FileMode.Open,FileAccess.Read,FileShare.None))
 using(var lockedCopy=File.Open(Path.Combine(jobs[0].Destination,plan.Files[0].Relative),FileMode.Open,FileAccess.Read,FileShare.None)){
  Assert(await new SourcePool(plan).PromoteAsync(jobs[0].Destination),"promotion succeeds while source and destination contents are locked against reads");
  var metadata=await MirrorPlanner.ReadSourceAsync(source);var preview=await MirrorPlanner.PreviewAsync(metadata,jobs[0].Destination);
  Assert(preview.CopyFiles.Length==0,"mirror planning compares metadata without reading locked file contents");
 }
 var different=Path.Combine(root,"same-metadata");Directory.CreateDirectory(different);File.WriteAllBytes(Path.Combine(different,plan.Files[0].Relative),new byte[plan.Files[0].Size]);File.SetLastWriteTimeUtc(Path.Combine(different,plan.Files[0].Relative),plan.Files[0].Modified);
 var metadataOnly=await MirrorPlanner.PreviewAsync(await MirrorPlanner.ReadSourceAsync(source),different);
 Assert(!metadataOnly.CopyFiles.Any(f=>f.Relative==plan.Files[0].Relative),"same size and timestamp are intentionally treated as unchanged without content verification");
 var bigSource=Path.Combine(root,"incremental-source");Directory.CreateDirectory(bigSource);using(var file=File.Create(Path.Combine(bigSource,"large.bin")))file.SetLength(128L*1024*1024);
 var incremental=new CopyBatch(Plan.Create([bigSource],true),Conflict.Overwrite,false,Path.Combine(root,"incremental-logs"));
 try{
  for(int i=0;i<3;i++)incremental.AddDestination(Path.Combine(root,"paused-"+i)).Pause();
  var fast=incremental.AddDestination(Path.Combine(root,"first-finished"));incremental.SealInitial();
  await incremental.WaitForJobAsync(fast).WaitAsync(TimeSpan.FromSeconds(30));
  Assert(incremental.Sources.HelperCount==1&&incremental.Sources.HelperRoots.Contains(fast.Destination)&&incremental.Jobs.Where(j=>j!=fast).All(j=>j.Status.State=="Pausado"),"first completed copy becomes an auxiliary while the other three destinations are still unfinished");
  foreach(var job in incremental.Jobs)job.Resume();await incremental.WaitAsync().WaitAsync(TimeSpan.FromSeconds(40));
  Assert(incremental.Sources.HelperCount==4&&incremental.Jobs.All(j=>j.Status.State=="Completado"),"other completed copies join independently up to four auxiliaries");
 }finally{incremental.CancelAll();incremental.SealInitial();await incremental.WaitAsync();}
 Console.WriteLine($"{passed} parallel engine assertions passed.");return 0;
}catch(Exception e){Console.Error.WriteLine(e);return 1;}
finally{release.TrySetResult();foreach(var j in jobs)j.Cancel();try{await Task.WhenAll(tasks);}catch{}Directory.Delete(root,true);}
