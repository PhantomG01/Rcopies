namespace DobleCommander;

// Admission reservations keep the batch open while a newly detected device is reviewed.
public sealed class CopyBatch {
 readonly object sync=new();readonly List<(CopyJob Job,Task Task,bool Initial)> work=[];
 readonly TaskCompletionSource initialDone=new(TaskCreationOptions.RunContinuationsAsynchronously);
 bool sealedInitial,closed,accepting=true;int reservations;TaskCompletionSource changed=new(TaskCreationOptions.RunContinuationsAsynchronously);
 readonly Conflict conflict;readonly bool verify;readonly string logs;readonly Func<string,string?>? identity;
 public Plan Plan {get;}public SourcePool Sources {get;}
 public bool IsOpen {get{lock(sync)return !closed&&accepting;}}
 public CopyJob[] Jobs {get{lock(sync)return work.Select(w=>w.Job).ToArray();}}
 public Task WaitForJobAsync(CopyJob job){lock(sync)return work.FirstOrDefault(w=>ReferenceEquals(w.Job,job)).Task??Task.CompletedTask;}
 public bool IsJobFinished(CopyJob job){lock(sync)return work.FirstOrDefault(w=>ReferenceEquals(w.Job,job)).Task?.IsCompleted??job.CopyCompleted.IsCompleted;}
 public CopyBatch(Plan plan,Conflict conflict,bool verify,string logs,Func<string,string?>? identity=null){Plan=plan;this.conflict=conflict;this.verify=verify;this.logs=logs;this.identity=identity;Sources=new(plan,identity);}
 void Signal(){var old=changed;changed=new(TaskCreationOptions.RunContinuationsAsynchronously);old.TrySetResult();}
 public CopyJob AddDestination(string destination,bool late=false) {
  lock(sync) {
   if(closed||!accepting)throw new IOException("El lote ya terminó o fue cancelado.");if(!late&&sealedInitial)throw new IOException("El grupo inicial ya está cerrado.");
   var full=Path.GetFullPath(destination);Plan.Validate(full);
   foreach(var w in work)if(SourcePool.SameOrInside(full,w.Job.Destination)||SourcePool.SameOrInside(w.Job.Destination,full))throw new IOException("El nuevo destino se solapa con otro destino del lote.");
   int frontier=late?work.Where(w=>w.Initial&&w.Job.Status.State is "Copiando" or "Pausado").Select(w=>w.Job.CurrentFileIndex).DefaultIfEmpty(0).Min():0;
   var volume=identity?.Invoke(full);
   Func<bool>? stillSame=identity==null||volume==null?null:()=>string.Equals(identity(full),volume,StringComparison.Ordinal);
   var job=new CopyJob(Plan,full,conflict,verify,logs,Sources,frontier,late?token=>initialDone.Task.WaitAsync(token):null,late?()=>!initialDone.Task.IsCompleted:null,stillSame);
   var task=Run(job);work.Add((job,task,!late));Signal();return job;
  }
 }
 async Task Run(CopyJob job){await job.RunAsync();if(job.CanAssist)try{bool promoted=await Sources.PromoteCompletedAsync(job.Destination,job.Token);job.Note(promoted?"auxiliary-enabled":"auxiliary-not-enabled",promoted?"Copia completada y habilitada como fuente auxiliar.":"Sin plaza auxiliar o destino deshabilitado.");}catch(Exception e){job.Note("auxiliary-error",e.Message);}}
 public void SealInitial() {
  lock(sync){if(sealedInitial)return;sealedInitial=true;var tasks=work.Where(w=>w.Initial).Select(w=>w.Job.CopyCompleted).ToArray();_=Finish();async Task Finish(){try{await Task.WhenAll(tasks);initialDone.TrySetResult();}catch(Exception e){initialDone.TrySetException(e);}}Signal();}
 }
 public IDisposable? ReserveAdmission(){lock(sync){if(closed||!accepting)return null;reservations++;Signal();return new Reservation(this);}}
 public void CancelAll(){lock(sync){accepting=false;foreach(var w in work)w.Job.Cancel();Signal();}}
 sealed class Reservation(CopyBatch batch):IDisposable {bool released;public void Dispose(){lock(batch.sync){if(released)return;released=true;batch.reservations--;batch.Signal();}}}
 public async Task WaitAsync() {
  while(true){Task[] tasks;Task wake;lock(sync){if(!sealedInitial)throw new InvalidOperationException("Falta cerrar el grupo inicial.");if(reservations==0&&work.All(w=>w.Task.IsCompleted)){closed=true;return;}tasks=work.Where(w=>!w.Task.IsCompleted).Select(w=>w.Task).ToArray();wake=changed.Task;}
   if(tasks.Length==0)await wake;else await Task.WhenAny(Task.WhenAll(tasks),wake);
  }
 }
}
