using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.IO.Compression;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;

public class AdaptDetection {
 public int Bits; public string Folder,Detail; public List<string> Apis=new List<string>();
}
public class AdaptEvidence {public string Fingerprint,Result,Scene,Time;}
public static class Adaptation {
 public static int Bits(string path){try{using(var r=new BinaryReader(File.OpenRead(path))){if(r.ReadUInt16()!=0x5a4d)return 0;r.BaseStream.Position=60;int p=r.ReadInt32();if(p<64||p>r.BaseStream.Length-6)return 0;r.BaseStream.Position=p;if(r.ReadUInt32()!=0x4550)return 0;int m=r.ReadUInt16();return m==0x8664?64:m==0x14c?32:0;}}catch{return 0;}}
 public static AdaptDetection Detect(Game g){
  var t=LegacyBridge.Target(g);var d=new AdaptDetection{Bits=Bits(t.Exe),Folder=Path.GetDirectoryName(t.Exe)};var evidence=new List<string>();
  var paths=new List<string>{t.Exe};foreach(string name in new[]{"UnityPlayer.dll","MonoGame.Framework.Windows.NetStandard.dll","Barotrauma.dll"}){string p=Path.Combine(d.Folder,name);if(File.Exists(p))paths.Add(p);}
  foreach(string p in paths){if(new FileInfo(p).Length>134217728)continue;string s=Encoding.ASCII.GetString(File.ReadAllBytes(p));foreach(var kv in new Dictionary<string,string>{{"d3d9.dll","DX9"},{"d3d11.dll","DX11"},{"d3d12.dll","DX12"},{"SharpDX.Direct3D11","DX11"},{"vulkan-1.dll","Vulkan"},{"opengl32.dll","OpenGL"}})if(s.IndexOf(kv.Key,StringComparison.OrdinalIgnoreCase)>=0){if(!d.Apis.Contains(kv.Value))d.Apis.Add(kv.Value);evidence.Add(Path.GetFileName(p)+" → "+kv.Value);}}
  d.Detail="程序："+t.Exe+"\r\n位数："+(d.Bits==0?"无法识别":d.Bits+"位")+"\r\n接口线索："+(d.Apis.Count==0?"未找到":String.Join(" / ",d.Apis))+"\r\n"+String.Join("；",evidence.Distinct())+"\r\n静态线索不等于正在使用的接口。多接口游戏需在游戏中选择相同接口。";
  return d;
 }
 public static List<string> Routes(Game g,AdaptDetection d){var result=new List<string>();if(d.Bits==32){string api=LegacyBridge.Api(g);if(api!=null)result.Add(api=="d3d9"?"DX9":"DX11");}if(d.Bits==64)foreach(string api in d.Apis)if(api=="DX11"||api=="DX12")result.Add(api);return result;}
 public static string EvidencePath(Game g){return Path.Combine(Path.GetDirectoryName(StackEngine.RecordPath(LegacyBridge.Target(g))),"verification.json");}
 public static string Fingerprint(Game g){var r=StackEngine.Record(LegacyBridge.Target(g));if(r==null)return "none";var parts=new List<string>{Core.Digest(g.Exe)};foreach(var kv in r.After.OrderBy(x=>x.Key)){string p=Path.Combine(r.Folder,kv.Key);parts.Add(kv.Key+":"+(File.Exists(p)?Core.Digest(p):"missing"));}return String.Join("|",parts);}
 public static string Verification(Game g){try{string p=EvidencePath(g);if(!File.Exists(p))return "待游戏内验证";var e=Core.Json.Deserialize<AdaptEvidence>(Core.Read(p));return e.Fingerprint==Fingerprint(g)?e.Result+"（用户记录）":"配置已改变，需重新验证";}catch{return "验证记录不可用";}}
 public static void SaveResult(Game g,string result,string scene){if(!LegacyBridge.Installed(g))throw new Exception("请先安装桥接。");if(String.IsNullOrWhiteSpace(scene))throw new Exception("请填写实际测试场景、分辨率和时长。");Core.Save(EvidencePath(g),new AdaptEvidence{Fingerprint=Fingerprint(g),Result=result,Scene=scene,Time=DateTime.UtcNow.ToString("o")});}
 public static readonly Dictionary<string,string> Hash64=new Dictionary<string,string>{{"dxgi.dll","0cee63f9c9f13f3ac909c5b4903f4dbb4b719a7ab3b4f13b0deaf83c814b94f7"},{"amd-nr.addon64","db7cfd85789bc75dd2bde7d7036688d0776d86078ca71ba82aee40919aaa9d59"},{"dlssnr_amd_pass1.dll","ff6feffa41abccce98ddf0cb37ce5cafd525c1a5dc8c59d2434cb3da8b7a16a8"},{"dlssnr_on_amd_weights.bin","6bf8dc931ef3ccffe18c82de26ab374156e7f19539ffcf8eabaa25dca5cf15ab"},{"payload.sha256","7ec123f3672e1f47036ad8fb348b807c1d97b55d84538fc8d6fe4a9fcb39b87b"}};
 public static string Prepare64(string selected){
  if(!String.IsNullOrWhiteSpace(selected)){Validate64(selected);return selected;}
  string dest=Path.Combine(Core.Data,"combinations","packages","reshade64-runtime040");
  using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Bridge64")){
   if(stream==null)throw new Exception("请选取合法取得的64位组件目录；开源版不内置神经运行库和权重。");
   LegacyBridge.Prepare(true);Directory.CreateDirectory(dest);
   using(var zip=new ZipArchive(stream,ZipArchiveMode.Read))foreach(var entry in zip.Entries){if(entry.FullName.EndsWith("/"))continue;string path=Path.GetFullPath(Path.Combine(dest,entry.FullName));if(!path.StartsWith(Path.GetFullPath(dest)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("组件路径错误");Directory.CreateDirectory(Path.GetDirectoryName(path));if(!File.Exists(path))using(var input=entry.Open())using(var output=File.Create(path))input.CopyTo(output);}
   foreach(string name in new[]{"dlssnr_amd_pass1.dll","dlssnr_on_amd_weights.bin","payload.sha256"})if(!File.Exists(Path.Combine(dest,name)))File.Copy(Path.Combine(LegacyBridge.PackageFor(true),name),Path.Combine(dest,name));
  }Validate64(dest);return dest;
 }
 public static void Validate64(string folder){foreach(var kv in Hash64)if(!File.Exists(Path.Combine(folder,kv.Key))||!Core.Digest(Path.Combine(folder,kv.Key)).Equals(kv.Value,StringComparison.OrdinalIgnoreCase))throw new Exception("64位组件缺失或版本不匹配："+kv.Key);if(!Directory.Exists(Path.Combine(folder,"Licenses")))throw new Exception("组件包缺少 Licenses 声明目录。");}
 public static void Install(Game g,string route,string package,Action<string> report){
  Core.EnsureStopped(g);var d=Detect(g);if(!Routes(g,d).Contains(route))throw new Exception("未找到该路线的检测依据，停止安装。");
  if(d.Bits==32){LegacyBridge.Install(g,true,report,false);return;}
  if(LegacyBridge.Installed(g))throw new Exception("已安装，请先恢复再更换方案；现有开关可直接使用。");
  string scanRoot=Directory.Exists(g.Root)?g.Root:Path.GetDirectoryName(g.Exe);
  foreach(var f in Core.Files(scanRoot,12,CancellationToken.None))if(Regex.IsMatch(f,"easyanticheat|battleye|beservice|anticheatexpert|eaanticheat|vgk\\.|faceit",RegexOptions.IgnoreCase)&&!g.OfflineOnly)throw new Exception("发现反作弊组件。仅可在游戏官方允许的离线模组模式下测试；先通过游戏模式设置确认。");
  string source=Prepare64(package);var files=new Dictionary<string,string>();foreach(var kv in Hash64)files[kv.Key]=Path.Combine(source,kv.Key);
  foreach(string f in Directory.GetFiles(Path.Combine(source,"Licenses")))files[Path.Combine("AMDNR-Licenses",Path.GetFileName(f))]=f;
  string stage=Path.Combine(Core.Data,"adaptation",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
  string ini=Path.Combine(stage,"amd-nr.ini");File.WriteAllText(ini,"[amd-nr]\r\nStartOn=0\r\nDisableOnAltTab=1\r\nToggleKey=35\r\nToggleMods=1\r\nScale=0.25\r\nPasses=1\r\nInline=1\r\nAsync=1\r\n",new UTF8Encoding(false));files["amd-nr.ini"]=ini;
  string ri=Path.Combine(stage,"ReShade.ini");File.WriteAllText(ri,"[ADDON]\r\nAddonPath=.\r\n[OVERLAY]\r\nTutorialProgress=4\r\n",new UTF8Encoding(false));files["ReShade.ini"]=ri;
  StackEngine.ApplyPrepared(LegacyBridge.Target(g),new StackProfile{Name="ReShade 64位 / "+route+" / 0.4.0 试验",Input="reshade64",Output="off",Neural=true,Package=source,Proxy="dxgi.dll"},files,report);
 }
 public static DateTime BeginTest(Game g){Core.EnsureStopped(g);if(!LegacyBridge.Installed(g))throw new Exception("请先备份安装桥接。");string folder=Path.GetDirectoryName(LegacyBridge.Target(g).Exe);string dest=Path.Combine(Core.Data,"adaptation","logs-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dest);
  foreach(string name in new[]{"amd-nr.log","amd-nr-x86-host.log","amd-nr-x86.log","dlssnr_on_amd.log","ReShade.log"}){string path=Path.Combine(folder,name);if(File.Exists(path))File.Move(path,Path.Combine(dest,name));}
  DateTime started=DateTime.UtcNow;Process.Start(new ProcessStartInfo(g.Exe,LegacyBridge.IsL4D2(g)?LegacyBridge.OfflineArguments(g):""){WorkingDirectory=Path.GetDirectoryName(g.Exe),UseShellExecute=true});return started;
 }
 public static string InspectLogs(Game g,DateTime started){if(started==DateTime.MinValue)return "请先点“启动游戏验证”，清理旧日志后再检查本次结果。";string folder=Path.GetDirectoryName(LegacyBridge.Target(g).Exe);string all="";foreach(string n in new[]{"amd-nr.log","amd-nr-x86-host.log","amd-nr-x86.log"}){string p=Path.Combine(folder,n);if(File.Exists(p)&&File.GetLastWriteTimeUtc(p)>=started)using(var f=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var r=new StreamReader(f)){all+=r.ReadToEnd();}}
  if(all.Length==0)return "尚无本次桥接日志。请检查加载位置、游戏接口和插件菜单。";
  if(Regex.IsMatch(all,"failed|device.removed|device.hung|network took",RegexOptions.IgnoreCase))return "本次日志出现错误或长耗时，请关闭效果并查看日志；必要时一键恢复。";
  if(Regex.IsMatch(all,@"frame \d+ processed|engine_frame=\d+ result=1"))return "本次日志检测到神经处理帧；这不代表画质、战斗场景或长期稳定性通过。请记录实测结果。";
  return "已有日志，但尚未确认神经处理帧。进入场景后用 Home 检查，Ctrl+End 开启效果。";
 }
}

public class AdaptationForm:Form {
 Game game;AdaptDetection detection;ComboBox routes=new ComboBox();TextBox info=new TextBox(),scene=new TextBox();Label status=new Label();string package="";DateTime started=DateTime.MinValue;
 public AdaptationForm(Game g){game=g;Text="自动适配 · "+g.Name;ClientSize=new Size(850,660);Font=new Font("Microsoft YaHei UI",10);StartPosition=FormStartPosition.CenterParent;
  info.SetBounds(18,18,814,225);info.Multiline=true;info.ReadOnly=true;info.ScrollBars=ScrollBars.Vertical;Controls.Add(info);
  routes.SetBounds(18,255,215,30);routes.DropDownStyle=ComboBoxStyle.DropDownList;Controls.Add(routes);
  var buttons=new FlowLayoutPanel{Left=18,Top=298,Width=814,Height=125};Controls.Add(buttons);
  Add(buttons,"1 重新检测",RefreshDetection);Add(buttons,"选择64位组件",()=>{using(var f=new FolderBrowserDialog())if(f.ShowDialog(this)==DialogResult.OK){Adaptation.Validate64(f.SelectedPath);package=f.SelectedPath;status.Text="64位组件校验通过";}});
  Add(buttons,"2 备份并安装",()=>{Adaptation.Install(game,Convert.ToString(routes.SelectedItem),package,s=>status.Text=s);RefreshDetection();status.Text="已部署，默认关闭；安装不等于生效。接下来启动游戏内验证。";});
  Add(buttons,"3 启动游戏验证",()=>{started=Adaptation.BeginTest(game);status.Text="请选择单机和低分辨率。Home 查看插件、Ctrl+End 开启；测试后读取日志。";});
  Add(buttons,"读取本次日志",()=>status.Text=Adaptation.InspectLogs(game,started));
  Add(buttons,"关闭下次启动效果",()=>{LegacyBridge.SetEnabled(game,false);RefreshDetection();});
  Add(buttons,"4 一键恢复",()=>{if(LegacyBridge.Installed(game)||LegacyBridge.Pending(game))LegacyBridge.Restore(game);else throw new Exception("没有本向导的桥接安装记录。");started=DateTime.MinValue;RefreshDetection();status.Text="已恢复本助手管理的文件；其他模组不在恢复范围内。";});
  var label=new Label{Left=18,Top=430,Width=814,Height=28,Text="游戏内验证记录：填写场景、分辨率、持续时间及问题（不会自动判为全部兼容）"};Controls.Add(label);
  scene.SetBounds(18,463,814,55);scene.Multiline=true;Controls.Add(scene);
  var results=new FlowLayoutPanel{Left=18,Top=527,Width=814,Height=45};Controls.Add(results);
  Add(results,"记录：仅菜单通过",()=>SaveResult("菜单短测通过"));Add(results,"记录：场景通过",()=>SaveResult("指定场景通过"));Add(results,"记录：存在问题",()=>SaveResult("实测存在问题"));
  status.SetBounds(18,580,814,66);Controls.Add(status);RefreshDetection();
 }
 void Add(FlowLayoutPanel panel,string text,Action action){var b=new Button{Text=text,Width=190,Height=36};panel.Controls.Add(b);b.Click+=delegate{try{UseWaitCursor=true;action();}catch(Exception ex){status.Text=ex.Message;}finally{UseWaitCursor=false;}};}
 void SaveResult(string result){if(started==DateTime.MinValue)throw new Exception("请先启动本次游戏测试再记录。");Adaptation.SaveResult(game,result,scene.Text);RefreshDetection();status.Text="已保存用户实测记录。修改组件或配置后需重新验证。";}
 void RefreshDetection(){detection=Adaptation.Detect(game);routes.Items.Clear();foreach(string route in Adaptation.Routes(game,detection))routes.Items.Add(route);if(routes.Items.Count>0)routes.SelectedIndex=0;
  info.Text="自动检测 → 推荐方案 → 备份安装 → 游戏内验证 → 一键恢复\r\n\r\n"+detection.Detail+"\r\n\r\n推荐："+(routes.Items.Count==0?"暂无已接通的安装路线；不强行写入插件。":"ReShade "+detection.Bits+"位＋0.4.0 神经处理（试验，不包含插帧）。")+"\r\n状态："+(LegacyBridge.Installed(game)?LegacyBridge.DisplayStatus(game)+"；"+Adaptation.Verification(game):"未安装桥接")+"\r\n安装会校验组件、保存事务和原文件；发现其他加载器时停止覆盖。";
 }
}
