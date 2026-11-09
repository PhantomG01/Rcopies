using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

static class Solicitud {
 public static string Fingerprint(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("Clonova-PC-v1|"+value.Trim().ToLowerInvariant()))).Replace("-","");}
 public static string MachineId(){using(var registry=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))using(var key=registry.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography")){var value=key==null?null:key.GetValue("MachineGuid") as string;if(String.IsNullOrWhiteSpace(value))throw new IOException("No se pudo obtener el identificador de Windows.");return Fingerprint(value);}}
 public static string Message(string name,string id){return "Hola Noel, quiero solicitar una licencia de Rcopies.\n\nNombre: "+name.Trim()+"\nIdentificador del PC: "+id+"\n\nPor favor, indícame las condiciones y el precio.";}
 public static string Url(string name,string id){return "https://wa.me/17282401919?text="+Uri.EscapeDataString(Message(name,id));}
 [STAThread] static void Main(string[] args){
  if(args.Length==2&&args[0]=="--self-test"){
   // Expected fixture supplied independently by the build verification script.
   string expected=File.ReadAllText(args[1]+".expected").Trim();
   if(Fingerprint(" Example-Windows-ID ")!=expected)throw new Exception("Fingerprint mismatch.");
   string id=MachineId();if(id.Length!=64)throw new Exception("Invalid ID length.");
   string url=Url("María & Luis",id);if(Uri.UnescapeDataString(url.Substring(url.IndexOf("?text=")+6))!=Message("María & Luis",id))throw new Exception("Encoding mismatch.");
   File.WriteAllText(args[1],"PASS: identical Rcopies fingerprint algorithm; 64-character Windows ID; Unicode WhatsApp message; no messages sent.");return;
  }
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new RequestWindow());
 }
}
sealed class RequestWindow:Form {
 TextBox name=new TextBox(),identifier=new TextBox(),message=new TextBox();Label status=new Label();Button whatsapp=new Button(),copy=new Button();
 public RequestWindow(){
  Text="Rcopies · Solicitar licencia";ClientSize=new Size(700,660);MinimumSize=new Size(700,690);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(22,19,34);ForeColor=Color.FromArgb(239,233,250);AutoScaleMode=AutoScaleMode.Dpi;
  try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
  var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=10};
  int[] heights={56,62,28,38,28,38,30,126,52,52};foreach(int h in heights)layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
  layout.Controls.Add(new Label{Text="Solicita tu licencia de Rcopies",Font=new Font("Segoe UI",20,FontStyle.Bold),Dock=DockStyle.Fill},0,0);
  layout.Controls.Add(new Label{Text="Sin instalar Rcopies. El identificador de este PC se obtiene automáticamente. Nada se envía hasta que tú lo confirmes en WhatsApp.",Dock=DockStyle.Fill},0,1);
  layout.Controls.Add(new Label{Text="Tu nombre o empresa",Dock=DockStyle.Fill},0,2);layout.Controls.Add(name,0,3);
  layout.Controls.Add(new Label{Text="Identificador de este PC",Dock=DockStyle.Fill},0,4);layout.Controls.Add(identifier,0,5);
  layout.Controls.Add(new Label{Text="Tu solicitud",Dock=DockStyle.Fill},0,6);layout.Controls.Add(message,0,7);
  foreach(var box in new[]{name,identifier,message}){box.Dock=DockStyle.Fill;box.BackColor=Color.FromArgb(39,33,53);box.ForeColor=ForeColor;box.BorderStyle=BorderStyle.FixedSingle;}
  name.MaxLength=120;identifier.ReadOnly=true;message.ReadOnly=true;message.Multiline=true;message.ScrollBars=ScrollBars.Vertical;
  var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};whatsapp.Text="Continuar en WhatsApp";whatsapp.AutoSize=true;whatsapp.Height=40;whatsapp.BackColor=Color.FromArgb(173,133,245);whatsapp.ForeColor=Color.FromArgb(24,15,39);whatsapp.FlatStyle=FlatStyle.Flat;
  copy.Text="Copiar solicitud";copy.AutoSize=true;copy.Height=40;actions.Controls.Add(whatsapp);actions.Controls.Add(copy);layout.Controls.Add(actions,0,8);status.Dock=DockStyle.Fill;layout.Controls.Add(status,0,9);Controls.Add(layout);
  try{identifier.Text=Solicitud.MachineId();status.Text="Escribe tu nombre y continúa en WhatsApp para revisar y enviar a Noel.";}catch{status.Text="No se pudo obtener el identificador de Windows. Cierra y vuelve a intentar.";whatsapp.Enabled=false;copy.Enabled=false;}
  name.TextChanged+=delegate{UpdateMessage();};UpdateMessage();
  whatsapp.Click+=delegate{if(!Ready())return;try{Process.Start(new ProcessStartInfo(Solicitud.Url(name.Text,identifier.Text)){UseShellExecute=true});status.Text="Revisa y envía el mensaje en WhatsApp. Si no se abre, usa Copiar solicitud.";}catch{status.Text="No se pudo abrir WhatsApp. Copia la solicitud y envíala al +1 728 240 1919.";}};
  copy.Click+=delegate{if(!Ready())return;try{Clipboard.SetText(message.Text);status.Text="Solicitud copiada. Pégala en WhatsApp y envíala al +1 728 240 1919.";}catch{status.Text="Selecciona el mensaje y pulsa Ctrl+C para copiarlo.";}};
 }
 void UpdateMessage(){message.Text=Solicitud.Message(name.Text,identifier.Text).Replace("\n",Environment.NewLine);}
 bool Ready(){if(String.IsNullOrWhiteSpace(name.Text)){status.Text="Escribe tu nombre antes de continuar.";name.Focus();return false;}return identifier.Text.Length==64;}
}
