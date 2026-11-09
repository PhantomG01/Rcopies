namespace DobleCommander;

// Aligned source/destination rows share one viewport. Filtering only changes
// presentation; selections are retained separately for each destination.
sealed class MirrorConfirmationDialog:Form {
 readonly MirrorPreview[] previews;
 readonly int pendingCopies;
 readonly Dictionary<int,HashSet<string>> selections=[];
 readonly ComboBox destination=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
 readonly ComboBox filter=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=155};
 readonly TextBox search=new(){Width=260,PlaceholderText="Buscar archivo o carpeta…"};
 readonly CheckBox all=new(){Text="Incluir todos los cambios de este destino",AutoSize=true};
 readonly DataGridView grid=new(){Dock=DockStyle.Fill,VirtualMode=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,AutoGenerateColumns=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,BackgroundColor=Theme.Background,BorderStyle=BorderStyle.None,EnableHeadersVisualStyles=false};
 readonly Label summary=new(){Dock=DockStyle.Fill,Padding=new(10,7,10,0)};
 readonly Label paths=new(){Dock=DockStyle.Fill,AutoEllipsis=true};
 readonly Button proceed=new(){Text="Proceder",AutoSize=true,DialogResult=DialogResult.OK};
 readonly Bitmap folderIcon=Stock(3),fileIcon=Stock(0);
 List<ComparisonRow> rows=[];List<int> visible=[];bool updating;
 public bool EarlyPrune {get;private set;}
 public MirrorPreview[] SelectedPreviews {get;private set;}=[];
 internal DataGridView ComparisonGrid=>grid;
 internal int ComparisonRows=>rows.Count;
 sealed record ComparisonRow(string Path,MirrorNode? Source,MirrorNode? Target,string Action){public bool Folder=>Source?.Directory==true||Target?.Directory==true;public bool Change=>Action!="Igual";}
 static Bitmap Stock(uint number){using var icon=WindowsIcons.Stock(number);return icon.ToBitmap();}
 public MirrorConfirmationDialog(MirrorPreview[] previews,bool automatic=false,int newDestinationCount=0){
  if(previews.Length==0)throw new ArgumentException("No hay destinos para confirmar.");this.previews=previews;pendingCopies=newDestinationCount;
  Text=automatic?"Por favor confirme el proceso de espejo":"Comparación de carpetas · Copia espejo";Icon=Branding.AppIcon;Size=new(1280,780);MinimumSize=new(960,620);StartPosition=FormStartPosition.CenterParent;Font=new("Segoe UI",10);MinimizeBox=false;ShowInTaskbar=false;ControlBox=false;
  var layout=new TableLayoutPanel(){Dock=DockStyle.Fill,Padding=new(16),ColumnCount=1,RowCount=7};
  foreach(var height in new[]{40,38,30,40})layout.RowStyles.Add(new(SizeType.Absolute,height));layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.Absolute,125));layout.RowStyles.Add(new(SizeType.Absolute,52));
  layout.Controls.Add(new Label(){Text=automatic?"Por favor confirme el proceso de espejo":"Comparar origen y destino",Dock=DockStyle.Fill,Font=new("Segoe UI",18,FontStyle.Bold)},0,0);
  foreach(var p in previews)destination.Items.Add(p.Destination);layout.Controls.Add(destination,0,1);layout.Controls.Add(paths,0,2);
  filter.Items.AddRange(["Diferencias","Todos","Seleccionados"]);filter.SelectedIndex=0;
  var tools=new FlowLayoutPanel(){Dock=DockStyle.Fill,WrapContents=false,Padding=new(0,4,0,0)};tools.Controls.AddRange([filter,search,all]);layout.Controls.Add(tools,0,3);
  grid.Columns.Add(new DataGridViewCheckBoxColumn(){Name="Include",HeaderText="✓",Width=38,SortMode=DataGridViewColumnSortMode.NotSortable});
  void ImageColumn(string name){grid.Columns.Add(new DataGridViewImageColumn(){Name=name,HeaderText="",Width=25,ImageLayout=DataGridViewImageCellLayout.Zoom,ReadOnly=true,DefaultCellStyle=new(){NullValue=null}});}
  void TextColumn(string name,string title,int width){grid.Columns.Add(new DataGridViewTextBoxColumn(){Name=name,HeaderText=title,Width=width,ReadOnly=true,SortMode=DataGridViewColumnSortMode.NotSortable});}
  ImageColumn("SourceIcon");TextColumn("SourcePath","ORIGEN / RUTA",225);TextColumn("SourceSize","TAMAÑO",85);TextColumn("SourceDate","MODIFICADO",130);TextColumn("Action","ACCIÓN →",160);ImageColumn("TargetIcon");TextColumn("TargetPath","DESTINO / RUTA",225);TextColumn("TargetSize","TAMAÑO",85);TextColumn("TargetDate","MODIFICADO",130);
  grid.RowTemplate.Height=30;grid.DefaultCellStyle.BackColor=Theme.Background;grid.DefaultCellStyle.ForeColor=Theme.Text;grid.DefaultCellStyle.SelectionBackColor=Theme.Purple;grid.DefaultCellStyle.SelectionForeColor=Color.White;grid.ColumnHeadersDefaultCellStyle.BackColor=Theme.Raised;grid.ColumnHeadersDefaultCellStyle.ForeColor=Theme.Text;grid.GridColor=Theme.Raised;
  grid.CellValueNeeded+=(_,e)=>{if(e.RowIndex<0||e.RowIndex>=visible.Count)return;var row=rows[visible[e.RowIndex]];var selected=Selection();
   e.Value=e.ColumnIndex switch{0=>selected.Contains(row.Path),1=>row.Source==null?null:row.Source.Directory?folderIcon:fileIcon,2=>row.Source==null?"—":row.Path,3=>SizeOf(row.Source),4=>DateOf(row.Source),5=>row.Action,6=>row.Target==null?null:row.Target.Directory?folderIcon:fileIcon,7=>row.Target==null?"—":row.Path,8=>SizeOf(row.Target),9=>DateOf(row.Target),_=>null};};
  grid.CellFormatting+=(_,e)=>{if(e.RowIndex<0||e.RowIndex>=visible.Count)return;var action=rows[visible[e.RowIndex]].Action;e.CellStyle!.ForeColor=action switch{"Eliminar"=>Color.FromArgb(255,128,153),"Actualizar"=>Color.FromArgb(255,208,118),"Añadir"=>Color.FromArgb(124,229,179),"Crear carpeta"=>Color.FromArgb(124,194,255),"Reemplazar tipo"=>Color.FromArgb(206,166,255),_=>Color.FromArgb(165,166,181)};};
  grid.CellValuePushed+=(_,e)=>{if(e.ColumnIndex!=0||e.RowIndex<0||e.RowIndex>=visible.Count)return;SetSelected(rows[visible[e.RowIndex]].Path,e.Value is true);if(IsHandleCreated)BeginInvoke(new Action(RefreshSelection));};
  grid.CurrentCellDirtyStateChanged+=(_,_)=>{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
  layout.Controls.Add(grid,0,4);layout.Controls.Add(summary,0,5);
  var buttons=new FlowLayoutPanel(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new(0,8,0,0)};
  var cancel=new Button(){Text="Cancelar",AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.Add(cancel);buttons.Controls.Add(proceed);layout.Controls.Add(buttons,0,6);Controls.Add(layout);AcceptButton=cancel;CancelButton=cancel;
  destination.SelectedIndexChanged+=(_,_)=>LoadDestination();filter.SelectedIndexChanged+=(_,_)=>FilterRows();search.TextChanged+=(_,_)=>FilterRows();all.CheckedChanged+=(_,_)=>{if(updating||destination.SelectedIndex<0)return;var selected=Selection();selected.Clear();if(all.Checked)foreach(var row in rows.Where(r=>r.Change||r.Folder))selected.Add(row.Path);RefreshSelection();};
  proceed.Click+=(_,_)=>{grid.EndEdit();RefreshSelection();if(!proceed.Enabled)DialogResult=DialogResult.None;};
  Theme.Apply(this);destination.SelectedIndex=0;
  if(automatic)paths.Text="Los destinos tienen datos que han sido actualizados en el origen. "+paths.Text;
 }
 static string SizeOf(MirrorNode? node)=>node==null?"—":node.Directory?"Carpeta":LiteWindow.Bytes(node.Size);
 static string DateOf(MirrorNode? node)=>node==null||node.Directory?"—":node.Modified.ToLocalTime().ToString("g");
 HashSet<string> Selection()=>selections[destination.SelectedIndex];
 void LoadDestination(){
  var p=previews[destination.SelectedIndex];paths.Text="ORIGEN: "+p.Source.Root+"     →     DESTINO: "+p.Destination;
  rows=p.Source.Nodes.Keys.Union(p.DestinationNodes.Keys,StringComparer.OrdinalIgnoreCase).OrderBy(path=>path,StringComparer.OrdinalIgnoreCase).Select(path=>{
   p.Source.Nodes.TryGetValue(path,out var source);p.DestinationNodes.TryGetValue(path,out var target);
   string action=source==null?"Eliminar":target==null?source.Directory?"Crear carpeta":"Añadir":source.Directory!=target.Directory?"Reemplazar tipo":!source.Directory&&(source.Size!=target.Size||source.Modified!=target.Modified)?"Actualizar":"Igual";
   return new ComparisonRow(path,source,target,action);
  }).ToList();
  if(!selections.ContainsKey(destination.SelectedIndex))selections[destination.SelectedIndex]=rows.Where(r=>r.Change||r.Folder).Select(r=>r.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
  RefreshSelection();
 }
 internal void SelectDestination(int index)=>destination.SelectedIndex=index;
 internal void SelectPath(string path,bool include){SetSelected(path,include);RefreshSelection();}
 internal void SelectAllChanges(bool include){var selected=Selection();selected.Clear();if(include)foreach(var row in rows.Where(r=>r.Change||r.Folder))selected.Add(row.Path);RefreshSelection();}
 void SetSelected(string path,bool include){
  var p=previews[destination.SelectedIndex];var root=p.BlockerRoots.FirstOrDefault(b=>MirrorPlanner.IsUnder(path,b))??path;
  var row=rows.FirstOrDefault(r=>r.Path.Equals(root,StringComparison.OrdinalIgnoreCase));if(row==null||!row.Change&&!row.Folder)return;
  var selected=Selection();foreach(var item in rows.Where(r=>MirrorPlanner.IsUnder(r.Path,root)&&(r.Change||r.Folder)))if(include)selected.Add(item.Path);else selected.Remove(item.Path);
  if(!include)foreach(var parent in selected.Where(p=>MirrorPlanner.IsUnder(root,p)).ToArray())selected.Remove(parent);
 }
 void FilterRows(){
  if(destination.SelectedIndex<0)return;var selected=Selection();string term=search.Text.Trim();
  visible=Enumerable.Range(0,rows.Count).Where(i=>(filter.SelectedIndex!=0||rows[i].Change)&&(filter.SelectedIndex!=2||selected.Contains(rows[i].Path))&&(term.Length==0||rows[i].Path.Contains(term,StringComparison.OrdinalIgnoreCase))).ToList();
  grid.RowCount=visible.Count;grid.Invalidate();
 }
 void RefreshSelection(){
  if(destination.SelectedIndex<0)return;var plans=previews.Select((p,i)=>MirrorPlanner.SelectChanges(p,selections.TryGetValue(i,out var chosen)?chosen:null)).ToArray();SelectedPreviews=plans.Where(p=>p.Changes.Length>0).ToArray();
  EarlyPrune=!MirrorPlanner.FitsTogether(SelectedPreviews,false)&&MirrorPlanner.FitsTogether(SelectedPreviews,true);bool fits=MirrorPlanner.FitsTogether(SelectedPreviews,EarlyPrune);
  proceed.Enabled=SelectedPreviews.Length>0&&fits;
  var selected=Selection();updating=true;all.Checked=rows.Where(r=>r.Change).All(r=>selected.Contains(r.Path));updating=false;FilterRows();
  int files=SelectedPreviews.Sum(p=>p.CopyFiles.Length),removed=SelectedPreviews.Sum(p=>p.DestinationNodes.Values.Count(n=>!n.Directory&&p.RemoveRoots.Concat(p.BlockerRoots).Any(r=>MirrorPlanner.IsUnder(n.Relative,r))));
  bool partial=plans.Any(p=>p.IsPartial);
  summary.Text=$"{(partial?"SELECCIÓN PARCIAL: se conservan los cambios excluidos; no será un espejo completo.":"ESPEJO COMPLETO: el origen manda sobre cada destino.")}\nCopiar: {files} archivos · {LiteWindow.Bytes(SelectedPreviews.Sum(p=>p.CopyFiles.Sum(f=>f.Size)))} · Retirar: {removed} archivos · Destinos: {SelectedPreviews.Length}.\nComparación por tamaño y fecha, sin SHA-256. Verde: añadir · Amarillo: actualizar · Rosa: eliminar. Cambios de tipo incluyen su subárbol.\n"+(EarlyPrune?"FALTA ESPACIO: Proceder autoriza borrar sobrantes antes de copiar. Ese borrado no se puede deshacer.":!fits?"ESPACIO INSUFICIENTE: este plan no puede ejecutarse.":"Los borrados no pasan por la papelera. Revisa la selección antes de proceder.")+(pendingCopies>0?$"\nTambién se copiará la carpeta en {pendingCopies} unidad(es) donde todavía no existe.":"");
  if(previews[0].Source.Plan.Files.Length==0)summary.Text+="\nATENCIÓN: el origen no contiene archivos; Proceder autoriza retirar los archivos seleccionados del destino.";
 }
 protected override void Dispose(bool disposing){if(disposing){folderIcon.Dispose();fileIcon.Dispose();}base.Dispose(disposing);}
}
