namespace DobleCommander;
internal enum DeviceChoice { Ignore, Copy, Format }
internal sealed class IncomingDeviceDialog : Form {
 public DeviceChoice Choice {get;private set;}
 public IncomingDeviceDialog(DeviceVolume volume) {
  Text="Nueva unidad detectada · Incorporar al lote";Size=new(760,420);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;Font=new("Segoe UI",10);
  var description=new Label(){Dock=DockStyle.Fill,Padding=new(20),Text=$"Nueva unidad: {volume.Label} ({volume.Drive})\nCapacidad: {LiteWindow.Bytes(volume.Size)} · {volume.FileSystem}\nIdentidad: {volume.VolumePath}\n\nLas copias actuales seguirán trabajando. La nueva unidad empezará por el tramo de archivos en curso y recuperará los anteriores al terminar el grupo inicial.\n\nSe conserva la política de conflictos del lote: sin formatear, los archivos existentes podrían omitirse.\n\nFormatear borra todo el contenido del volumen y requiere una confirmación adicional."};
  var buttons=new FlowLayoutPanel(){Dock=DockStyle.Bottom,Height=70,Padding=new(12),FlowDirection=FlowDirection.RightToLeft};
  void Button(string text,DeviceChoice choice){var button=new Button(){Text=text,AutoSize=true,Height=38};button.Click+=(_,_)=>{Choice=choice;DialogResult=choice==DeviceChoice.Ignore?DialogResult.Cancel:DialogResult.OK;Close();};buttons.Controls.Add(button);}
  Button("Ignorar",DeviceChoice.Ignore);Button("Incorporar sin formatear",DeviceChoice.Copy);Button("Formatear y copiar…",DeviceChoice.Format);Controls.Add(description);Controls.Add(buttons);Theme.Apply(this);
 }
}
internal sealed class ConfirmFormatDialog : Form {
 readonly TextBox typed=new(){Dock=DockStyle.Fill};readonly ComboBox fs=new(){Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
 public string Typed=>typed.Text;public string FileSystem=>(string)fs.SelectedItem!;
 public ConfirmFormatDialog(DeviceVolume volume,FormatSafety safety) {
  Text="Confirmar borrado del volumen";Size=new(760,560);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;Font=new("Segoe UI",10);
  var layout=new TableLayoutPanel(){Dock=DockStyle.Fill,Padding=new(20),ColumnCount=1,RowCount=6};foreach(var height in new[]{0,28,36,28,36,50})layout.RowStyles.Add(new(height==0?SizeType.Percent:SizeType.Absolute,height==0?100:height));
  layout.Controls.Add(new Label(){Dock=DockStyle.Fill,ForeColor=Color.Firebrick,Text=$"SE BORRARÁ TODO EL CONTENIDO DE {volume.Drive}\n\nEtiqueta: {volume.Label}\nCapacidad: {LiteWindow.Bytes(volume.Size)}\nVolumen: {volume.VolumePath}\nDisco físico: {safety.DiskId}\n\nEsta operación no se puede deshacer. Después se incorporará esta unidad al lote de copia."},0,0);
  layout.Controls.Add(new Label(){Text="Sistema de archivos (formateo rápido):",Dock=DockStyle.Fill},0,1);fs.Items.AddRange(["NTFS","exFAT"]);fs.SelectedIndex=0;layout.Controls.Add(fs,0,2);
  layout.Controls.Add(new Label(){Text=$"Escribe exactamente BORRAR {volume.Drive} para confirmar:",Dock=DockStyle.Fill},0,3);layout.Controls.Add(typed,0,4);
  var actions=new FlowLayoutPanel(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var confirm=new Button(){Text="Borrar, formatear y copiar",AutoSize=true,Enabled=false,DialogResult=DialogResult.OK};actions.Controls.Add(confirm);var cancel=new Button(){Text="Cancelar",DialogResult=DialogResult.Cancel,AutoSize=true};actions.Controls.Add(cancel);layout.Controls.Add(actions,0,5);typed.TextChanged+=(_,_)=>confirm.Enabled=typed.Text=="BORRAR "+volume.Drive;Controls.Add(layout);CancelButton=cancel;Theme.Apply(this);
 }
}
