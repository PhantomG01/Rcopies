namespace DobleCommander;

sealed partial class LiteWindow {
 internal string CurrentSource=>source.Text;
 static bool IsUnitRoot(string path){try{return Path.IsPathFullyQualified(path)&&StorageUnits.Normalize(path).Equals(StorageUnits.Normalize(Path.GetPathRoot(Path.GetFullPath(path))!),StringComparison.OrdinalIgnoreCase);}catch(ArgumentException){return false;}}
 internal void HandleDrop(string[] paths,bool sourceArea=false){
  if(paths.Length==0)return;if(ejecting)throw new IOException("Espera a que termine la desconexiÃ³n.");
  if(!sourceArea&&paths.Length>1&&paths.All(IsUnitRoot)){AddPaths(paths);return;}
  if(busy)throw new IOException("El origen estÃ¡ bloqueado mientras se realiza la copia.");
  if(paths.Length!=1)throw new IOException("Arrastra un solo archivo o carpeta como origen. Puedes copiar todo su contenido a los destinos seleccionados.");
  var path=paths[0];if(!Path.IsPathFullyQualified(path)||!Directory.Exists(path)&&!File.Exists(path))throw new IOException("El archivo o carpeta de origen ya no estÃ¡ disponible.");
  SetSource(path);
 }
 internal Task LastDropOperation {get;private set;}=Task.CompletedTask;
 internal async Task HandleDropAsync(string[] paths,bool sourceArea=false){
  HandleDrop(paths,sourceArea);
  if(paths.Length==1&&SelectedDestinations.Length>0){if(sourceArea&&Directory.Exists(paths[0]))await StartSmartOperationAsync(false);else if(!sourceArea)await StartCopyAsync();}
 }
 void ConfigureDrop(Control target,bool sourceArea){
  target.AllowDrop=true;
  target.DragEnter+=(_,e)=>UpdateDropEffect(e,sourceArea);target.DragOver+=(_,e)=>UpdateDropEffect(e,sourceArea);
  target.DragDrop+=async (_,e)=>{if(e.Data?.GetData(DataFormats.FileDrop) is not string[] paths)return;try{LastDropOperation=HandleDropAsync(paths,sourceArea);await LastDropOperation;}catch(Exception error){MessageBox.Show(this,error.Message,"Origen y destinos");}};
 }
 void UpdateDropEffect(DragEventArgs e,bool sourceArea){
  e.Effect=DragDropEffects.None;if(ejecting||e.Data?.GetData(DataFormats.FileDrop) is not string[] paths||paths.Length==0)return;
  bool units=!sourceArea&&paths.Length>1&&paths.All(IsUnitRoot);if(units||!busy&&paths.Length==1)e.Effect=DragDropEffects.Copy;
 }
}
