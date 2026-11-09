using RcopiesLicensing;
using System.Security.Cryptography;
using System.Text.Json;
namespace RcopiesAdmin;
static class Program {
 [STAThread] static int Main(string[] args){
  if(args.Length==1&&args[0]=="--check-authority"){using var key=ECDsa.Create();key.ImportFromPem(File.ReadAllText(AdminWindow.KeyPath));var publicPath=Path.Combine(Path.GetDirectoryName(AdminWindow.KeyPath)!,"Rcopies-public.pem");if(key.ExportSubjectPublicKeyInfoPem().Trim()!=File.ReadAllText(publicPath).Trim())throw new IOException("El archivo de emisión no corresponde a esta autoridad.");Console.WriteLine("Signing authority loaded automatically; public key matches.");return 0;}
  if(args.Length==2&&args[0]=="--provision"){
   string directory=Path.GetFullPath(args[1]);Directory.CreateDirectory(directory);var privatePath=Path.Combine(directory,"Noel-Rcopies-private.pem");
   using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
   if(File.Exists(privatePath))key.ImportFromPem(File.ReadAllText(privatePath));else File.WriteAllText(privatePath,key.ExportECPrivateKeyPem());
   File.WriteAllText(Path.Combine(directory,"Rcopies-public.pem"),key.ExportSubjectPublicKeyInfoPem());
   var license=new LicenseData(1,LicenseCore.Product,Guid.NewGuid().ToString(),"Noel Rojas Morejon",LicenseCore.MachineId(),DateTimeOffset.UtcNow,null);
   File.WriteAllText(Path.Combine(directory,"Noel-PC.clonova-license"),LicenseCore.Sign(license,key));return 0;
  }
  ApplicationConfiguration.Initialize();
  if(args.Length==2&&args[0]=="--render"){using var window=new AdminWindow();window.Show();Application.DoEvents();using var image=new Bitmap(window.Width,window.Height);window.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);window.Close();return 0;}
  Application.Run(new AdminWindow());return 0;
 }
}
sealed partial class AdminWindow:Form {
 readonly TextBox machine=new(){Dock=DockStyle.Fill},customer=new(){Dock=DockStyle.Fill};
 readonly DateTimePicker expires=new(){Dock=DockStyle.Fill,Format=DateTimePickerFormat.Short,Value=DateTime.Today.AddYears(1)};
 readonly CheckBox perpetual=new(){Text="Sin vencimiento",AutoSize=true};readonly Label result=new(){Dock=DockStyle.Fill};
 internal static string KeyPath=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","Claves-Noel","Noel-Rcopies-private.pem"));
 public AdminWindow(){
  Text="Rcopies · Administrador de licencias";using(var stream=typeof(AdminWindow).Assembly.GetManifestResourceStream("RcopiesAdmin.App.ico")){if(stream!=null){using var icon=new Icon(stream,256,256);Icon=(Icon)icon.Clone();}}Size=new(850,550);MinimumSize=Size;StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",11);
  var layout=new TableLayoutPanel(){Dock=DockStyle.Fill,Padding=new(24),ColumnCount=2,RowCount=8};layout.ColumnStyles.Add(new(SizeType.Absolute,180));layout.ColumnStyles.Add(new(SizeType.Percent,100));foreach(int height in new[]{70,45,45,45,40,50,55,70})layout.RowStyles.Add(new(SizeType.Absolute,height));
  var title=new Label(){Text="Rcopies\nAdministrador de licencias",Font=new Font("Segoe UI",15,FontStyle.Bold),Dock=DockStyle.Fill};layout.Controls.Add(title,0,0);layout.SetColumnSpan(title,2);
  Add("Cliente / titular",customer,1);Add("ID del PC (63 o 64)",machine,2);Add("Válida hasta",expires,3);layout.Controls.Add(perpetual,1,4);perpetual.CheckedChanged+=(_,_)=>expires.Enabled=!perpetual.Checked;
  var generate=new Button(){Text="Generar licencia…",AutoSize=true};generate.Click+=(_,_)=>Generate();layout.Controls.Add(generate,1,5);
  var protect=new Button(){Text="Proteger administrador…",AutoSize=true};protect.Click+=(_,_)=>Protect();var history=new Button(){Text="Historial de licencias",AutoSize=true};history.Click+=(_,_)=>History();var actions=new FlowLayoutPanel(){Dock=DockStyle.Fill};var mobile=new Button(){Text="Exportar para iPhone…",AutoSize=true};mobile.Click+=(_,_)=>ExportMobile();actions.Controls.AddRange([protect,history,mobile]);layout.Controls.Add(actions,0,6);layout.SetColumnSpan(actions,2);layout.Controls.Add(result,0,7);layout.SetColumnSpan(result,2);Controls.Add(layout);
  void Add(string label,Control control,int row){layout.Controls.Add(new Label(){Text=label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,row);layout.Controls.Add(control,1,row);}
  Style(this);
 }
 static void Style(Control c){var background=Color.FromArgb(23,24,36);c.BackColor=background;c.ForeColor=Color.FromArgb(237,235,247);if(c is Button b){b.FlatStyle=FlatStyle.Flat;b.Padding=new(8,4,8,4);}if(c is TextBoxBase input){input.BackColor=Color.FromArgb(34,35,50);input.BorderStyle=BorderStyle.FixedSingle;}foreach(Control child in c.Controls)Style(child);}
 string? Password(string title){using var dialog=new Form(){Text=title,Size=new(530,180),StartPosition=FormStartPosition.CenterParent,Font=Font,Padding=new(16),MinimizeBox=false,MaximizeBox=false};var input=new TextBox(){Dock=DockStyle.Top,UseSystemPasswordChar=true};var actions=new FlowLayoutPanel(){Dock=DockStyle.Bottom,Height=43};var ok=new Button(){Text="Aceptar",AutoSize=true,DialogResult=DialogResult.OK};var cancel=new Button(){Text="Cancelar",AutoSize=true,DialogResult=DialogResult.Cancel};actions.Controls.AddRange([ok,cancel]);dialog.Controls.Add(input);dialog.Controls.Add(actions);dialog.AcceptButton=ok;dialog.CancelButton=cancel;Style(dialog);return dialog.ShowDialog(this)==DialogResult.OK?input.Text:null;}
 ECDsa ReadKey(){if(!File.Exists(KeyPath))throw new IOException("Falta el archivo de emisión. Restaura la carpeta Claves-Noel junto a portable.");var pem=File.ReadAllText(KeyPath);var key=ECDsa.Create();try{if(pem.Contains("ENCRYPTED PRIVATE KEY")){var password=Password("Contraseña del administrador")??throw new OperationCanceledException();key.ImportFromEncryptedPem(pem,password);}else key.ImportFromPem(pem);return key;}catch{key.Dispose();throw;}}
 void Generate(){try{
  string id=LicenseCore.NormalizeId(machine.Text);if(!LicenseCore.ValidId(id))throw new IOException("Pega el identificador completo del PC: 63 o 64 caracteres hexadecimales.");if(string.IsNullOrWhiteSpace(customer.Text)||customer.Text.Trim().Length>200)throw new IOException("Escribe el nombre del titular (máximo 200 caracteres).");
  var now=DateTimeOffset.UtcNow;DateTimeOffset? end=perpetual.Checked?null:new DateTimeOffset(expires.Value.Date.AddDays(1)).ToUniversalTime();if(end<=now)throw new IOException("El vencimiento debe ser posterior a hoy.");
  using var key=ReadKey();var data=new LicenseData(1,LicenseCore.Product,Guid.NewGuid().ToString(),customer.Text.Trim(),id,now,end);var document=LicenseCore.Sign(data,key);
  using var save=new SaveFileDialog(){Filter="Licencia Rcopies|*.rcopies-license",FileName="Rcopies-"+id[..10]+".rcopies-license"};if(save.ShowDialog(this)!=DialogResult.OK)return;File.WriteAllText(save.FileName,document);
  var log=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RcopiesAdmin");Directory.CreateDirectory(log);File.AppendAllText(Path.Combine(log,"licencias-emitidas.jsonl"),JsonSerializer.Serialize(data)+Environment.NewLine);result.Text="Licencia generada. Entrega solo el archivo de licencia al cliente.";
 }catch(OperationCanceledException){}catch(Exception e){MessageBox.Show(this,e.Message,"Licencia no generada");}}
 void Protect(){try{using var key=ReadKey();var password=Password("Nueva contraseña del administrador")??throw new OperationCanceledException();if(password.Length<12)throw new IOException("Usa al menos 12 caracteres.");if(Password("Repite la nueva contraseña")!=password)throw new IOException("Las contraseñas no coinciden.");var pem=key.ExportEncryptedPkcs8PrivateKeyPem(password,new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc,HashAlgorithmName.SHA256,200_000));var temp=KeyPath+".tmp";File.WriteAllText(temp,pem);File.Move(temp,KeyPath,true);result.Text="Administrador protegido. Conserva la contraseña y tu copia de seguridad privada.";}catch(OperationCanceledException){}catch(Exception e){MessageBox.Show(this,e.Message,"Proteger administrador");}}
 void History(){try{var file=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RcopiesAdmin","licencias-emitidas.jsonl");var rows=File.Exists(file)?File.ReadLines(file).Where(l=>!string.IsNullOrWhiteSpace(l)).Select(l=>JsonSerializer.Deserialize<LicenseData>(l)).OfType<LicenseData>().Select(l=>new{Cliente=l.Customer,PC=l.MachineId,Emisión=l.IssuedUtc.ToLocalTime().ToString("g"),Vence=l.ExpiresUtc?.ToLocalTime().ToString("g")??"Sin vencimiento",Licencia=l.LicenseId}).ToArray():[];using var window=new Form(){Text="Rcopies · Historial de emisiones",Size=new(1050,530),StartPosition=FormStartPosition.CenterParent};window.Controls.Add(new DataGridView(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,DataSource=rows});window.ShowDialog(this);}catch(Exception e){MessageBox.Show(this,e.Message,"Historial");}}
}
