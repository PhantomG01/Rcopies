using System.Drawing.Drawing2D;
namespace DobleCommander;

internal static class MenuArtwork {
 internal static Bitmap Auxiliary(bool enabled){
  var image=new Bitmap(24,24);using var g=Graphics.FromImage(image);g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);using var link=new Pen(Color.FromArgb(172,135,255),2.1f);g.DrawLine(link,6,6,17,12);g.DrawLine(link,6,18,17,12);using var node=new SolidBrush(Color.FromArgb(194,170,255));g.FillEllipse(node,2,2,7,7);g.FillEllipse(node,2,15,7,7);g.FillEllipse(node,14,9,7,7);
  using var state=new Pen(enabled?Color.FromArgb(44,217,189):Color.FromArgb(255,102,150),2.4f){StartCap=LineCap.Round,EndCap=LineCap.Round};if(enabled){g.DrawLine(state,14,21,22,21);g.DrawLine(state,18,17,18,23);}else g.DrawLine(state,2,22,22,2);return image;
 }
 internal static Bitmap Eject(){var image=new Bitmap(24,24);using var g=Graphics.FromImage(image);g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);using var fill=new SolidBrush(Color.FromArgb(54,220,211));g.FillPolygon(fill,[new PointF(12,4),new(21,15),new(3,15)]);using var line=new Pen(Color.FromArgb(189,158,255),3){StartCap=LineCap.Round,EndCap=LineCap.Round};g.DrawLine(line,4,20,20,20);return image;}
}
sealed class DestinationMenuColors:ProfessionalColorTable {
 public override Color ToolStripDropDownBackground=>Theme.Surface;
 public override Color ImageMarginGradientBegin=>Theme.Surface;public override Color ImageMarginGradientMiddle=>Theme.Surface;public override Color ImageMarginGradientEnd=>Theme.Surface;
 public override Color MenuBorder=>Theme.Border;public override Color MenuItemBorder=>Theme.Purple;public override Color MenuItemSelected=>Theme.Selection;
 public override Color MenuItemSelectedGradientBegin=>Theme.Selection;public override Color MenuItemSelectedGradientEnd=>Theme.Selection;
 public override Color MenuItemPressedGradientBegin=>Theme.Selection;public override Color MenuItemPressedGradientMiddle=>Theme.Selection;public override Color MenuItemPressedGradientEnd=>Theme.Selection;
}
