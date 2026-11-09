namespace DobleCommander;

// Keep source icons alive until the ImageList is disposed.
sealed class IconCatalog : IDisposable {
 public ImageList Images {get;}=new(){ColorDepth=ColorDepth.Depth32Bit,ImageSize=new Size(20,20)};
 readonly List<Icon> owned=[];
 public IconCatalog(){Add("folder",WindowsIcons.Stock(3));Add("file",(Icon)SystemIcons.Application.Clone());}
 void Add(string key,Icon icon){owned.Add(icon);Images.Images.Add(key,icon);}
 public string PathKey(string path,Icon icon){var key="path:"+path;if(!Images.Images.ContainsKey(key))Add(key,(Icon)icon.Clone());return key;}
 public string FileKey(string path) {
  var extension=Path.GetExtension(path).ToLowerInvariant();var key="ext:"+extension;
  if(!Images.Images.ContainsKey(key))Add(key,WindowsIcons.ForExtension(extension)??(Icon)SystemIcons.Application.Clone());
  return key;
 }
 public void Dispose(){Images.Dispose();foreach(var icon in owned)icon.Dispose();owned.Clear();}
}
