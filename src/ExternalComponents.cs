using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

// This file manages user-selected paths; it contains no third-party executable payload.
public class ComponentPaths { public string Installer="", Legacy030="", Legacy040="", Magpie=""; }
public static class ExternalComponents {
 public static string PathOverride;
 public static string Config {get{return PathOverride??Path.Combine(Core.Data,"components.json");}}
 public static ComponentPaths Load(){if(!File.Exists(Config))return new ComponentPaths();return Core.Json.Deserialize<ComponentPaths>(File.ReadAllText(Config))??new ComponentPaths();}
 public static void Save(ComponentPaths p){Core.Save(Config,p);}
 public static string Legacy(bool modern){var p=Load();return modern?p.Legacy040:p.Legacy030;}
 public static string Installer(){string p=Load().Installer;if(String.IsNullOrWhiteSpace(p)||!File.Exists(p))throw new Exception("请先在外部组件中选择自己从官方取得的原版 v0.4.0 安装器。");if(Core.Digest(p)!=Core.Hash)throw new Exception("安装器与受支持的原版 v0.4.0 SHA256不匹配，未运行。新版本需要先适配。");return Path.GetFullPath(p);}
 public static string Magpie(){string p=Load().Magpie;if(String.IsNullOrWhiteSpace(p)||!File.Exists(p)||!Path.GetFileName(p).Equals("Magpie.exe",StringComparison.OrdinalIgnoreCase)||!Core.X64(p))throw new Exception("请先在外部组件中选择你信任且合法取得的 x64 Magpie.exe。");return Path.GetFullPath(p);}
}
public class ComponentsForm:Form {
 readonly ComponentPaths paths=ExternalComponents.Load();
 public ComponentsForm(){
  Text="外部组件 / 许可 · 开源版";ClientSize=new Size(860,475);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",10);
  var note=new Label{Text="本助手源码使用MIT许可；第三方组件不随源码或便携包提供。\r\n选择本地路径只保存引用，不复制、下载或修改组件；只有点击安装／启动时才使用。\r\n请核对组件许可。桥接目录必须与已适配版本校验值匹配，本助手不提供补丁。",Location=new Point(20,16),Size=new Size(820,80)};Controls.Add(note);
  Row(112,"原版安装器",paths.Installer,false,v=>paths.Installer=v);
  Row(164,"桥接目录 0.3",paths.Legacy030,true,v=>paths.Legacy030=v);
  Row(216,"桥接目录 0.4",paths.Legacy040,true,v=>paths.Legacy040=v);
  Row(268,"外部 Magpie",paths.Magpie,false,v=>paths.Magpie=v);
  var links=new FlowLayoutPanel{Location=new Point(20,327),Size=new Size(820,52)};Controls.Add(links);
  Link(links,"Daniel 官方来源","https://github.com/danielblnc/DLSS-NR-on-AMD");Link(links,"ReShade 桥接来源","https://github.com/zmodelerlover/dlss5-neural-amd");Link(links,"Magpie 官方来源","https://github.com/Blinue/Magpie");
  Controls.Add(new Label{Text="普通Magpie并不自带神经处理。自选扩展版的效果、授权与兼容性需单独核对。\r\n开源版与原个人试验版配置隔离；旧版安装记录不会自动迁移。",Location=new Point(20,391),Size=new Size(820,62)});
 }
 void Row(int y,string title,string value,bool folder,Action<string> set){Controls.Add(new Label{Text=title,Location=new Point(20,y+7),Size=new Size(120,28)});var box=new TextBox{Text=value,ReadOnly=true,Location=new Point(145,y),Size=new Size(560,30)};Controls.Add(box);var b=new Button{Text="选择",Location=new Point(719,y-2),Size=new Size(110,34)};Controls.Add(b);b.Click+=delegate{string selected=null;if(folder){using(var d=new FolderBrowserDialog()){if(d.ShowDialog(this)==DialogResult.OK)selected=d.SelectedPath;}}else{using(var d=new OpenFileDialog{Filter="程序|*.exe",CheckFileExists=true}){if(d.ShowDialog(this)==DialogResult.OK)selected=d.FileName;}}if(selected==null)return;try{selected=Path.GetFullPath(selected);set(selected);ExternalComponents.Save(paths);box.Text=selected;}catch(Exception ex){MessageBox.Show(this,ex.Message,"路径未保存");}};}
 void Link(Control parent,string title,string url){var b=new Button{Text=title,AutoSize=true,Height=34};b.Click+=delegate{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});};parent.Controls.Add(b);}
}
public static class ComponentTests {
 static int checks;
 static void Assert(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Reject(Action action,string message){bool rejected=false;try{action();}catch{rejected=true;}Assert(rejected,message);}
 public static void Run(){
  string root=Path.Combine(Path.GetTempPath(),"OpenHelperTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);ExternalComponents.PathOverride=Path.Combine(root,"components.json");
  Reject(()=>ExternalComponents.Installer(),"Missing installer rejected without download");Reject(()=>LegacyBridge.Prepare(true),"Missing local bridge rejected");Reject(()=>ExternalComponents.Magpie(),"Missing external enhancer rejected");
  string fake=Path.Combine(root,"installer.exe");File.WriteAllText(fake,"inert test file");ExternalComponents.Save(new ComponentPaths{Installer=fake,Legacy040=root});Reject(()=>ExternalComponents.Installer(),"Wrong installer hash rejected");Reject(()=>LegacyBridge.Prepare(true),"Incomplete bridge rejected");
  Assert(ExternalComponents.Load().Installer==fake,"User path persisted");Assert(File.ReadAllText(fake)=="inert test file","Original untouched");
  Assert(typeof(Program).Assembly.GetManifestResourceNames().Length==0,"No embedded binary packages");Assert(Core.Data.EndsWith("ChineseGameHelper.Open"),"Personal data store isolated");
  Core.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"外部组件测试.json"),new{passed=true,checks=checks});
 }
}
