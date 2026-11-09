namespace DobleCommander;
sealed class MemoryMeter:Control {
 public MemoryMeter(){DoubleBuffered=true;Dock=DockStyle.Top;Height=28;AccessibleName="Consumo de RAM de Rcopies";}
 protected override void OnPaint(PaintEventArgs e){
  var memory=ResourceBudget.Sample();e.Graphics.Clear(Theme.Surface);
  string text=DisplayText(memory);
  TextRenderer.DrawText(e.Graphics,text,Font,new Rectangle(0,0,Width,Height-5),Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);AccessibleDescription=text;
 }
 internal static string DisplayText(MemoryReading memory)=>$"RAM usada: {memory.Resident/1048576d:F0} MB  ·  RAM libre: {memory.Available/1073741824d:F1} GiB";
}
