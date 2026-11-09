using RcopiesLicensing;
namespace DobleCommander;
internal static class LicenseStore {
 static readonly string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Rcopies");
 internal static string PublicKey{get{using var stream=typeof(LicenseStore).Assembly.GetManifestResourceStream("Rcopies.LicensePublic.pem")??throw new IOException("No se encontró la clave pública de Rcopies.");using var reader=new StreamReader(stream);return reader.ReadToEnd();}}
 internal static string FilePath=>Path.Combine(directory,"license.rcopies-license");
 internal static LicenseData Validate(){var now=DateTimeOffset.UtcNow;var legacy=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Clonova","license.clonova-license");var selected=File.Exists(FilePath)?FilePath:legacy;var data=LicenseCore.Verify(LicenseCore.ReadSmall(selected),PublicKey,LicenseCore.MachineId(),now);var stamp=Path.Combine(directory,"last-seen.utc");if(File.Exists(stamp)&&DateTimeOffset.TryParse(File.ReadAllText(stamp),out var last)&&now<last.AddMinutes(-5))throw new IOException("La fecha del equipo retrocedió. Corrige la fecha de Windows para validar la licencia.");Directory.CreateDirectory(directory);File.WriteAllText(stamp,now.ToString("O"));return data;}
 internal static LicenseData Install(string path){var document=LicenseCore.ReadSmall(path);var data=LicenseCore.Verify(document,PublicKey,LicenseCore.MachineId(),DateTimeOffset.UtcNow);Directory.CreateDirectory(directory);var temp=Path.Combine(directory,Guid.NewGuid().ToString("N")+".tmp");try{File.WriteAllText(temp,document);File.Move(temp,FilePath,true);}finally{if(File.Exists(temp))File.Delete(temp);}return Validate();}
 internal static bool IsLicensed(){try{Validate();return true;}catch(Exception e)when(e is IOException or UnauthorizedAccessException){return false;}}
 internal static bool Ensure(IWin32Window? owner=null){try{Validate();return true;}catch(Exception e)when(e is IOException or UnauthorizedAccessException){using var activation=new ActivationDialog(e.Message);return owner==null?activation.ShowDialog()==DialogResult.OK:activation.ShowDialog(owner)==DialogResult.OK;}}
 internal static void Demand(){try{Validate();}catch(Exception e)when(e is IOException or UnauthorizedAccessException){throw new IOException("Rcopies necesita una licencia válida. Abre «Licencia» para activarlo. "+e.Message,e);}}
 internal static void Manage(IWin32Window owner){string message;try{var data=Validate();message=$"Licencia activa para {data.Customer}.\nVence: {(data.ExpiresUtc is {} end?end.ToLocalTime().ToString("g"):"Sin vencimiento")}. Puedes importar una renovación.";}catch(Exception e)when(e is IOException or UnauthorizedAccessException){message="Modo libre: una copia o espejo a un destino por operación. Importa una licencia para usar varios destinos.";}using var dialog=new ActivationDialog(message);dialog.ShowDialog(owner);}
}

sealed class ActivationDialog:Form {
 public ActivationDialog(string? reason=null){
  Text="Rcopies · Activación";Icon=Branding.AppIcon;Size=new(740,440);MinimumSize=Size;StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10);MaximizeBox=false;MinimizeBox=false;
  var panel=new TableLayoutPanel(){Dock=DockStyle.Fill,Padding=new(22),ColumnCount=1,RowCount=6};foreach(int height in new[]{64,58,28,40,44,60})panel.RowStyles.Add(new(SizeType.Absolute,height));
  panel.Controls.Add(new Label(){Text="Rcopies\nLicencia",Font=new Font("Segoe UI",15,FontStyle.Bold),Dock=DockStyle.Fill},0,0);
  panel.Controls.Add(new Label(){Text=reason??"Envía el identificador a Noel para recibir tu archivo de licencia.",Dock=DockStyle.Fill,AutoEllipsis=true},0,1);
  panel.Controls.Add(new Label(){Text="Identificador completo de este PC (64 caracteres)",Dock=DockStyle.Fill},0,2);
  string id;try{id=LicenseCore.MachineId();}catch(Exception e){id="No disponible: "+e.Message;}
  var identifier=new TextBox(){Text=id,ReadOnly=true,Dock=DockStyle.Fill};panel.Controls.Add(identifier,0,3);
  var copy=new LiteButton(){Text="Copiar identificador",AutoSize=true};copy.Click+=(_,_)=>{if(LicenseCore.ValidId(id)){Clipboard.SetText(id);copy.Text=$"Copiado completo ({id.Length} caracteres)";}};panel.Controls.Add(copy,0,4);
  var actions=new FlowLayoutPanel(){Dock=DockStyle.Fill};var activate=new LiteButton(){Text="Importar licencia…",AutoSize=true};var close=new LiteButton(){Text="Cerrar",DialogResult=DialogResult.Cancel,AutoSize=true};
  activate.Click+=(_,_)=>{using var file=new OpenFileDialog(){Filter="Licencia Rcopies|*.rcopies-license;*.clonova-license|Todos los archivos|*.*"};if(file.ShowDialog(this)!=DialogResult.OK)return;try{LicenseStore.Install(file.FileName);DialogResult=DialogResult.OK;Close();}catch(Exception e){MessageBox.Show(this,e.Message,"No se pudo activar");}};
  actions.Controls.AddRange([activate,close]);panel.Controls.Add(actions,0,5);Controls.Add(panel);CancelButton=close;Theme.Apply(this);
 }
}
