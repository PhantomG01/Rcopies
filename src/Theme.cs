using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms.VisualStyles;
namespace DobleCommander;

internal static class Theme {
 public static readonly Color Background=Color.FromArgb(14,15,23),Surface=Color.FromArgb(23,24,36),Raised=Color.FromArgb(34,35,50),Border=Color.FromArgb(58,51,78),Text=Color.FromArgb(237,235,247),Muted=Color.FromArgb(163,158,183),Purple=Color.FromArgb(130,77,235),Pink=Color.FromArgb(238,49,143),Selection=Color.FromArgb(72,43,108);
 static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control,object> styled=new();
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
 static void DarkCaption(Form form){int enabled=1;DwmSetWindowAttribute(form.Handle,20,ref enabled,sizeof(int));}
 public static void Apply(Control root){Style(root);foreach(Control child in root.Controls)Apply(child);}
 static void Style(Control control){
  if(styled.TryGetValue(control,out _))return;styled.Add(control,new object());
  control.BackColor=Surface;control.ForeColor=Text;
  switch(control){
   case Form form:form.BackColor=Background;form.HandleCreated+=(_,_)=>DarkCaption(form);if(form.IsHandleCreated)DarkCaption(form);break;
   case Button button:
    button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=1;button.FlatAppearance.BorderColor=Border;button.FlatAppearance.MouseOverBackColor=Selection;button.FlatAppearance.MouseDownBackColor=Color.FromArgb(89,47,137);button.BackColor=Raised;button.ForeColor=Text;button.Padding=new Padding(9,3,9,3);button.MinimumSize=new Size(0,30);button.UseVisualStyleBackColor=false;
    if(button.Text.StartsWith("F5",StringComparison.Ordinal)||button.Text=="Proceder"){button.BackColor=Purple;button.FlatAppearance.BorderColor=Purple;button.ForeColor=Color.White;}
    if(button.Text.Contains("espejo",StringComparison.OrdinalIgnoreCase)||button.Text=="Espejo"){button.ForeColor=Color.FromArgb(255,142,196);button.FlatAppearance.BorderColor=Color.FromArgb(128,51,103);}
    if(button.Text.StartsWith("Cancelar",StringComparison.Ordinal)){button.ForeColor=Color.FromArgb(255,150,171);}
    break;
   case TextBoxBase input:input.BackColor=Raised;input.ForeColor=Text;input.BorderStyle=BorderStyle.FixedSingle;break;
   case ComboBox combo:combo.BackColor=Raised;combo.ForeColor=Text;combo.FlatStyle=FlatStyle.Flat;break;
   case TreeView tree:
    tree.BackColor=Surface;tree.ForeColor=Text;tree.LineColor=Border;tree.BorderStyle=BorderStyle.None;tree.DrawMode=TreeViewDrawMode.OwnerDrawText;
    tree.DrawNode+=(_,e)=>{if(e.Node==null)return;bool selected=(e.State&TreeNodeStates.Selected)!=0;var bounds=new Rectangle(e.Bounds.X,e.Bounds.Y,Math.Max(1,tree.ClientSize.Width-e.Bounds.X),e.Bounds.Height);using var brush=new SolidBrush(selected?Selection:Surface);e.Graphics.FillRectangle(brush,bounds);TextRenderer.DrawText(e.Graphics,e.Node.Text,tree.Font,bounds,selected?Color.White:e.Node.ForeColor.IsEmpty?Text:e.Node.ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);};break;
   case ListView list:if(list is not DeviceList)StyleList(list);break;
   case DataGridView grid:
    grid.BackgroundColor=Surface;grid.GridColor=Border;grid.BorderStyle=BorderStyle.None;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.Single;grid.ColumnHeadersHeight=32;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
    grid.ColumnHeadersDefaultCellStyle.BackColor=Raised;grid.ColumnHeadersDefaultCellStyle.ForeColor=Muted;grid.DefaultCellStyle.BackColor=Surface;grid.DefaultCellStyle.ForeColor=Text;grid.DefaultCellStyle.SelectionBackColor=Selection;grid.DefaultCellStyle.SelectionForeColor=Color.White;grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(27,28,41);grid.RowTemplate.Height=29;break;
   case SplitContainer split:split.BackColor=Background;break;
   case Label label:label.BackColor=Surface;label.ForeColor=label.Text.Contains("ATENCIÓN",StringComparison.Ordinal)||label.Text.Contains("SE BORRARÁ",StringComparison.Ordinal)?Color.FromArgb(255,142,170):Text;break;
  }
 }
 static void StyleList(ListView list){
  list.BackColor=Surface;list.ForeColor=Text;list.BorderStyle=BorderStyle.None;list.OwnerDraw=true;
  list.DrawColumnHeader+=(_,e)=>{using var brush=new SolidBrush(Raised);e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,e.Header?.Text??"",list.Font,new Rectangle(e.Bounds.X+8,e.Bounds.Y,e.Bounds.Width-12,e.Bounds.Height),Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);};
  list.DrawItem+=(_,e)=>{if(list.View!=View.Details)e.DrawDefault=true;};
  list.DrawSubItem+=(_,e)=>{
   if(e.Item==null||e.SubItem==null||list.IsDisposed||list.Disposing)return;
   bool selected=e.Item.Selected;using var brush=new SolidBrush(selected?Selection:e.ItemIndex%2==0?Surface:Color.FromArgb(26,27,40));e.Graphics.FillRectangle(brush,e.Bounds);
   int x=e.Bounds.X+8;if(e.ColumnIndex==0){if(list.CheckBoxes){var glyph=CheckBoxRenderer.GetGlyphSize(e.Graphics,CheckBoxState.UncheckedNormal);CheckBoxRenderer.DrawCheckBox(e.Graphics,new Point(x,e.Bounds.Y+(e.Bounds.Height-glyph.Height)/2),e.Item.Checked?CheckBoxState.CheckedNormal:CheckBoxState.UncheckedNormal);x+=glyph.Width+7;}
    if(list.SmallImageList is {} images){Image? icon=e.Item.ImageKey.Length>0?images.Images[e.Item.ImageKey]:e.Item.ImageIndex>=0&&e.Item.ImageIndex<images.Images.Count?images.Images[e.Item.ImageIndex]:null;if(icon!=null){e.Graphics.DrawImage(icon,new Rectangle(x,e.Bounds.Y+(e.Bounds.Height-20)/2,20,20));x+=26;}}
   }
   TextRenderer.DrawText(e.Graphics,e.SubItem.Text,list.Font,new Rectangle(x,e.Bounds.Y,Math.Max(1,e.Bounds.Right-x-6),e.Bounds.Height),selected?Color.White:e.ColumnIndex==0?Text:Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
  };
 }
}

sealed class AccentProgressBar : Control {
 readonly System.Windows.Forms.Timer animation=new(){Interval=40};int current;ProgressBarStyle style;
 [System.ComponentModel.DefaultValue(0)]public int Minimum{get;set;}
 [System.ComponentModel.DefaultValue(100)]public int Maximum{get;set;}=100;
 [System.ComponentModel.DefaultValue(0)]public int Value{get=>current;set{current=Math.Clamp(value,Minimum,Maximum);Invalidate();}}
 [System.ComponentModel.DefaultValue(ProgressBarStyle.Blocks)]public ProgressBarStyle Style{get=>style;set{style=value;animation.Enabled=value==ProgressBarStyle.Marquee;Invalidate();}}
 public AccentProgressBar(){DoubleBuffered=true;animation.Tick+=(_,_)=>Invalidate();}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Theme.Raised);if(ClientSize.Width<1||ClientSize.Height<1)return;int width=Style==ProgressBarStyle.Marquee?Math.Max(24,ClientSize.Width/4):(int)(ClientSize.Width*Math.Clamp((Value-Minimum)/(double)Math.Max(1,Maximum-Minimum),0,1));if(width<=0)return;int x=Style==ProgressBarStyle.Marquee?(int)((Environment.TickCount64/12)%(Math.Max(1,ClientSize.Width+width)))-width:0;using var brush=new LinearGradientBrush(ClientRectangle,Theme.Purple,Theme.Pink,0f);e.Graphics.FillRectangle(brush,x,0,width,ClientSize.Height);}
 protected override void Dispose(bool disposing){if(disposing)animation.Dispose();base.Dispose(disposing);}
}

sealed class WatermarkListView : ListView {
 public WatermarkListView(){DoubleBuffered=true;}
 protected override void WndProc(ref Message message){base.WndProc(ref message);if(message.Msg!=0xF||!IsHandleCreated||IsDisposed||Disposing||ClientSize.Height<80)return;
  int top=34;if(Items.Count>0){int first=TopItem?.Index??0;for(int i=first;i<Math.Min(Items.Count,first+ClientSize.Height/16+3);i++){var box=Items[i].Bounds;if(box.Top>=ClientSize.Height)break;if(box.Bottom>0)top=Math.Max(top,box.Bottom+8);}}
  int size=Math.Min(240,Math.Min(ClientSize.Width-28,ClientSize.Height-top-18));if(size<72)return;using var graphics=Graphics.FromHwnd(Handle);Branding.DrawWatermark(graphics,new Rectangle(ClientSize.Width-size-18,ClientSize.Height-size-12,size,size));
 }
}
