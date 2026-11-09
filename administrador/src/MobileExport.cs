namespace RcopiesAdmin;
sealed partial class AdminWindow {
 void ExportMobile(){try{
  using var key=ReadKey();
  var password=Password("Contraseña para el iPhone (mínimo 12 caracteres)")??throw new OperationCanceledException();
  if(Password("Repite la contraseña para el iPhone")!=password)throw new IOException("Las contraseñas no coinciden.");
  var document=MobileVault.Export(key,password);
  using var save=new SaveFileDialog(){Filter="Autoridad cifrada para iPhone|*.rcopies-vault",FileName="Noel-iPhone.rcopies-vault"};
  if(save.ShowDialog(this)!=DialogResult.OK)return;
  File.WriteAllText(save.FileName,document);
  result.Text="Exportación cifrada lista. Impórtala desde Archivos en el iPhone. No la subas al alojamiento web.";
 }catch(OperationCanceledException){}catch(Exception e){MessageBox.Show(this,e.Message,"Exportar para iPhone");}}
}
