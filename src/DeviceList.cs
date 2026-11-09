using System.Drawing.Drawing2D;
namespace DobleCommander;

sealed class TargetRow {
 public required string Path {get;init;}
 public string Name="",Details="",State="",ProgressText="",Speed="",Eta="",Current="";
 public double? Remaining;
 public double Fraction;
 public CopyJob? Copy;
 public MirrorJob? Mirror;
 public bool ShowProgress;public bool AllowAuxiliary=true;
}

// A single device list combines destination selection and live transfer information.
sealed class DeviceList : ListView {
 readonly ImageList spacing=new(){ImageSize=new Size(24,46),ColorDepth=ColorDepth.Depth32Bit};
 internal IconCatalog Icons {get;}=new();
 readonly System.Windows.Forms.Timer dragScroll=new(){Interval=75};
 bool selecting,addRange,removeRange;int rangeStart;ListViewItem? selectionAnchor;HashSet<ListViewItem> selectionBase=[];
 public DeviceList(){
  Dock=DockStyle.Fill;View=View.Details;CheckBoxes=false;FullRowSelect=true;MultiSelect=true;
  HideSelection=false;AllowDrop=true;ShowItemToolTips=true;OwnerDraw=true;DoubleBuffered=true;BorderStyle=BorderStyle.None;
  spacing.Images.Add(new Bitmap(24,46));SmallImageList=spacing;
  Columns.Add("UNIDAD / DESTINO",280);Columns.Add("ESTADO",145);Columns.Add("PROGRESO",180);Columns.Add("VELOCIDAD",110);Columns.Add("RESTANTE",110);Columns.Add("ARCHIVO / LECTURA",240);
  dragScroll.Tick+=(_,_)=>{if(selecting)SelectAtPoint(PointToClient(Cursor.Position));};
 }
 internal void BeginRangeSelection(int index,bool control=false,bool shift=false){
  if(index<0||index>=Items.Count)return;var item=Items[index];_=Handle;Focus();if(item.ListView!=this)return;index=item.Index;
  selectionBase=control?SelectedItems.Cast<ListViewItem>().ToHashSet():[];removeRange=control&&!shift&&selectionBase.Contains(item);addRange=control;
  if(!shift||selectionAnchor?.ListView!=this)selectionAnchor=shift&&FocusedItem!=null?FocusedItem:item;
  rangeStart=selectionAnchor!.Index;selecting=true;Capture=true;dragScroll.Start();MoveRangeSelection(index);
 }
 internal void MoveRangeSelection(int index){
  if(!selecting||Items.Count==0)return;index=Math.Clamp(index,0,Items.Count-1);int first=Math.Min(rangeStart,index),last=Math.Max(rangeStart,index);
  BeginUpdate();try{for(int i=0;i<Items.Count;i++){bool range=i>=first&&i<=last;Items[i].Selected=removeRange?selectionBase.Contains(Items[i])&&!range:range||addRange&&selectionBase.Contains(Items[i]);}Items[index].Focused=true;}finally{EndUpdate();}EnsureVisible(index);
 }
 internal void EndRangeSelection(){selecting=false;dragScroll.Stop();selectionBase.Clear();if(Capture)Capture=false;}
 void SelectAtPoint(Point point){
  if(Items.Count==0)return;var hit=HitTest(Math.Clamp(point.X,1,Math.Max(1,ClientSize.Width-2)),Math.Clamp(point.Y,0,Math.Max(0,ClientSize.Height-1))).Item;
  int top=TopItem?.Index??0,rowHeight=Math.Max(1,Items[top].Bounds.Height),header=Math.Max(18,Items[top].Bounds.Top);
  int index=hit?.Index??(point.Y<header?top:Math.Min(Items.Count-1,top+Math.Max(0,(ClientSize.Height-header)/rowHeight)));
  if(point.Y<header+8)index=Math.Max(0,top-1);else if(point.Y>ClientSize.Height-18)index=Math.Min(Items.Count-1,index+1);MoveRangeSelection(index);
 }
 protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture&&selecting){selecting=false;dragScroll.Stop();selectionBase.Clear();}base.OnMouseCaptureChanged(e);}
 protected override void OnResize(EventArgs e){base.OnResize(e);if(Columns.Count==6)Columns[5].Width=Math.Max(240,ClientSize.Width-Columns.Cast<ColumnHeader>().Take(5).Sum(c=>c.Width));}
 protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e){
  using var fill=new SolidBrush(Theme.Raised);e.Graphics.FillRectangle(fill,e.Bounds);
  TextRenderer.DrawText(e.Graphics,e.Header?.Text??"",Font,new Rectangle(e.Bounds.X+8,e.Bounds.Y,e.Bounds.Width-12,e.Bounds.Height),Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
 }
 protected override void OnDrawItem(DrawListViewItemEventArgs e){}
 protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e){
  if(e.Item?.Tag is not TargetRow row)return;
  using var fill=new SolidBrush(e.ItemIndex%2==0?Theme.Surface:Color.FromArgb(28,29,42));e.Graphics.FillRectangle(fill,e.Bounds);
  if(e.Item.Selected){using var overlay=new SolidBrush(Color.FromArgb(65,Theme.Purple));e.Graphics.FillRectangle(overlay,e.Bounds);}
  var rect=new Rectangle(e.Bounds.X+8,e.Bounds.Y+2,e.Bounds.Width-16,e.Bounds.Height-4);
  var foreground=e.Item.Selected?Color.White:Theme.Text;
  if(e.ColumnIndex==0){
   if(Icons.Images.Images[e.Item.ImageKey] is {} icon)e.Graphics.DrawImage(icon,new Rectangle(e.Bounds.X+8,e.Bounds.Y+12,22,22));
   rect.X=e.Bounds.X+40;rect.Width=Math.Max(1,e.Bounds.Right-rect.X-8);
   Draw(e.Graphics,row.Name,new Rectangle(rect.X,rect.Y,rect.Width,22),foreground);
   using var small=new Font(Font.FontFamily,8.5f);TextRenderer.DrawText(e.Graphics,row.Details,small,new Rectangle(rect.X,rect.Y+23,rect.Width,18),Theme.Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
  }else if(e.ColumnIndex==1)Draw(e.Graphics,row.State,rect,row.State=="Error"?Color.FromArgb(255,142,170):foreground);
  else if(e.ColumnIndex==2&&row.ShowProgress){
   var track=new Rectangle(rect.X,rect.Y+27,rect.Width,5);using var bg=new SolidBrush(Theme.Raised);e.Graphics.FillRectangle(bg,track);
   using var gradient=new LinearGradientBrush(track,Theme.Purple,Theme.Pink,0f);e.Graphics.FillRectangle(gradient,track.X,track.Y,(int)(track.Width*Math.Clamp(row.Fraction,0,1)),track.Height);
   Draw(e.Graphics,row.ProgressText,new Rectangle(rect.X,rect.Y,rect.Width,24),foreground);
  }else if(e.ColumnIndex==3)Draw(e.Graphics,row.Speed,rect,Theme.Muted);
  else if(e.ColumnIndex==4)Draw(e.Graphics,row.Eta,rect,Theme.Muted);
  else if(e.ColumnIndex==5)Draw(e.Graphics,row.ShowProgress?row.Current:row.Path,rect,Theme.Muted);
 }
 void Draw(Graphics g,string text,Rectangle rect,Color color)=>TextRenderer.DrawText(g,text,Font,rect,color,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
 protected override void WndProc(ref Message message){
  if(message.Msg==0x201){var point=new Point(unchecked((short)(message.LParam.ToInt64()&0xFFFF)),unchecked((short)((message.LParam.ToInt64()>>16)&0xFFFF)));var hit=HitTest(point).Item;if(hit!=null){BeginRangeSelection(hit.Index,(ModifierKeys&Keys.Control)!=0,(ModifierKeys&Keys.Shift)!=0);message.Result=IntPtr.Zero;return;}}
  if(selecting&&message.Msg==0x200){var point=new Point(unchecked((short)(message.LParam.ToInt64()&0xFFFF)),unchecked((short)((message.LParam.ToInt64()>>16)&0xFFFF)));SelectAtPoint(point);message.Result=IntPtr.Zero;return;}
  if(selecting&&message.Msg==0x202){EndRangeSelection();message.Result=IntPtr.Zero;return;}
  base.WndProc(ref message);if(!IsHandleCreated||IsDisposed||Disposing)return;
  if(message.Msg==0xF){using var graphics=Graphics.FromHwnd(Handle);PaintWatermark(graphics);}
  else if((message.Msg==0x317||message.Msg==0x318)&&message.WParam!=IntPtr.Zero){using var graphics=Graphics.FromHdc(message.WParam);PaintWatermark(graphics);}
 }
 void PaintWatermark(Graphics g){
  int top=32;if(Items.Count>0){int first=TopItem?.Index??0;for(int i=first;i<Math.Min(Items.Count,first+ClientSize.Height/40+3);i++){var bounds=Items[i].Bounds;if(bounds.Top>=ClientSize.Height)break;top=Math.Max(top,bounds.Bottom+10);}}
  int size=Math.Min(210,Math.Min(ClientSize.Width-30,ClientSize.Height-top-18));if(size<72)return;
  Branding.DrawWatermark(g,new Rectangle(ClientSize.Width-size-18,ClientSize.Height-size-12,size,size),.10f);
 }
 protected override void Dispose(bool disposing){if(disposing){dragScroll.Stop();dragScroll.Dispose();spacing.Dispose();Icons.Dispose();}base.Dispose(disposing);}
}
