using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace DobleCommander;

internal static class Branding {
 public const string Name="Rcopies";
 public const string Credit="";
 public const string Author="Programado y diseñado por Noel Rojas Morejon";
 public const string Phone="Telf: +17282401919";
 public const string Email="mail: noelrmorejon@gmail.com";
 static Image LoadImage(string resource){using var stream=typeof(Branding).Assembly.GetManifestResourceStream(resource)??throw new IOException("No se encontró un recurso visual de Rcopies.");using var source=Image.FromStream(stream);return new Bitmap(source);}
 static readonly Lazy<Image> logo=new(()=>LoadImage("Rcopies.Product.png")),watermark=new(()=>LoadImage("Rcopies.Watermark.png"));
 static readonly Lazy<Icon> appIcon=new(()=>{using var stream=typeof(Branding).Assembly.GetManifestResourceStream("Rcopies.App.ico")??throw new IOException("No se encontró el icono de Rcopies.");using var icon=new Icon(stream,256,256);return (Icon)icon.Clone();});
 public static Image Logo=>logo.Value;
 public static Image Watermark=>watermark.Value;
 public static Icon AppIcon=>appIcon.Value;
 public static void DrawWatermark(Graphics graphics,Rectangle bounds,float opacity=.13f){if(bounds.Width<48||bounds.Height<48)return;using var attributes=new ImageAttributes();attributes.SetColorMatrix(new ColorMatrix{Matrix33=opacity});graphics.DrawImage(Watermark,bounds,0,0,Watermark.Width,Watermark.Height,GraphicsUnit.Pixel,attributes);}
}

sealed class BrandHeader : Control {
 public BrandHeader(){Dock=DockStyle.Top;Height=86;DoubleBuffered=true;AccessibleName="Rcopies · "+Branding.Name+" · "+Branding.Credit;}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;using var gradient=new LinearGradientBrush(ClientRectangle,Color.FromArgb(15,13,25),Color.FromArgb(52,21,67),0f);g.FillRectangle(gradient,ClientRectangle);
  g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(Branding.Logo,new Rectangle(18,8,68,68));
  using var title=new Font("Segoe UI",20,FontStyle.Bold);using var subtitle=new Font("Segoe UI",10);using var small=new Font("Segoe UI",9);
  TextRenderer.DrawText(g,"Rcopies",title,new Rectangle(102,10,440,37),Theme.Text,TextFormatFlags.NoPadding|TextFormatFlags.VerticalCenter);
  TextRenderer.DrawText(g,Branding.Name+"   /   "+Branding.Credit,subtitle,new Rectangle(104,49,540,22),Theme.Muted,TextFormatFlags.NoPadding|TextFormatFlags.VerticalCenter);
  if(Width>1050)TextRenderer.DrawText(g,"ARCHIVOS  /  MULTICOPIA  /  ESPEJO",small,new Rectangle(Width-410,30,380,28),Color.FromArgb(203,170,235),TextFormatFlags.Right|TextFormatFlags.VerticalCenter);
  using var accent=new LinearGradientBrush(new Rectangle(0,Height-2,Math.Max(1,Width),2),Theme.Purple,Theme.Pink,0f);g.FillRectangle(accent,0,Height-2,Width,2);
 }
}
sealed class BrandFooter : Control {
 public BrandFooter(){Dock=DockStyle.Bottom;Height=30;DoubleBuffered=true;AccessibleName=Branding.Credit+" · "+Branding.Name;}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Theme.Background);using var pen=new Pen(Theme.Border);e.Graphics.DrawLine(pen,0,0,Width,0);using var font=new Font("Segoe UI",9);TextRenderer.DrawText(e.Graphics,"Rcopies  ·  Windows portable",font,new Rectangle(14,2,Width-28,26),Theme.Muted,TextFormatFlags.VerticalCenter);}
}

sealed class AboutDialog : Form {
 public AboutDialog(){
  Text="Acerca de · Rcopies";Icon=Branding.AppIcon;Size=new(760,550);MinimumSize=new(680,530);StartPosition=FormStartPosition.CenterParent;Font=new("Segoe UI",11);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
  var layout=new TableLayoutPanel(){Dock=DockStyle.Fill,Padding=new(24),ColumnCount=1,RowCount=6};foreach(var h in new[]{130,40,42,38,38,52})layout.RowStyles.Add(new(SizeType.Absolute,h));
  var hero=new Panel(){Dock=DockStyle.Fill};hero.Controls.Add(new PictureBox(){Image=Branding.Logo,SizeMode=PictureBoxSizeMode.Zoom,Dock=DockStyle.Left,Width=124});var text=new Label(){Text="Rcopies\nCopia · Multicopia · Espejo",Dock=DockStyle.Fill,Padding=new(18,16,0,0),Font=new Font("Segoe UI",14,FontStyle.Bold)};hero.Controls.Add(text);text.BringToFront();layout.Controls.Add(hero,0,0);
  layout.Controls.Add(new Label(){Text="Copia · Multicopia · Copia espejo",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,1);
  layout.Controls.Add(new Label(){Text=Branding.Author,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",12,FontStyle.Bold)},0,2);
  layout.Controls.Add(new Label(){Text=Branding.Phone,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,3);
  layout.Controls.Add(new Label(){Text=Branding.Email,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,4);
  var close=new Button(){Text="Cerrar",DialogResult=DialogResult.Cancel,Dock=DockStyle.Right,Width=120};var actions=new Panel(){Dock=DockStyle.Fill,Padding=new(0,8,0,0)};actions.Controls.Add(close);layout.Controls.Add(actions,0,5);Controls.Add(layout);CancelButton=close;AcceptButton=close;Theme.Apply(this);
 }
}
