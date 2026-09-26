using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;

public static class LegacyBridge {
 public static string Package=Path.Combine(Core.Data,"combinations","packages","reshade-amd-nr-0.6.6-x86");
 public static readonly Dictionary<string,string> Hashes=new Dictionary<string,string>{
  {"ReShade32.dll","da430e0a9c6eecefa0d1b27d05e16c426fb5d04e808b194d914eaac4b31bc0f8"},
  {"amd-nr.addon32","281bd02493c160041149d600d027e1097f42f0fc411c5cd8fdd6722c6aea81b3"},
  {"amd-nr-host64.exe","c1cbd5764f4d34850654b3d339065d53fc2009a87723fd3b50e7baae6facf855"},
  {"dlssnr_amd_pass1.dll","70af3fb757f83f71ec947ce461970fdecc9636864bc01d952abffb36ae310be6"},
  {"dlssnr_on_amd_weights.bin","6bf8dc931ef3ccffe18c82de26ab374156e7f19539ffcf8eabaa25dca5cf15ab"},
  {"payload.sha256","a971fe2c1ad573bf7d039d4a0e783ed9f6e8f3e15bfc7ad993f9aa5ad06bdb8b"}};
 public static readonly Dictionary<string,string> ModernHashes=MakeModernHashes();
 static Dictionary<string,string> MakeModernHashes(){var h=new Dictionary<string,string>(Hashes);
 h["amd-nr.addon32"]="04d1bfe02c8f7ed1314df1c022606c518b84f450762046c1c5ef503290a5c753";
 h["amd-nr-host64.exe"]="41c4e63f74556e8cbab9534573d0382ef08d2f67fb80b7d3ab3a8fc39b76c959";
 h["dlssnr_amd_pass1.dll"]="ff6feffa41abccce98ddf0cb37ce5cafd525c1a5dc8c59d2434cb3da8b7a16a8";
 h["payload.sha256"]="7ec123f3672e1f47036ad8fb348b807c1d97b55d84538fc8d6fe4a9fcb39b87b";return h;}
 public static string PackageFor(bool modern){return ExternalComponents.Legacy(modern);}
 public static string Version(Game g){var r=StackEngine.Record(Target(g));string hash;return r!=null&&r.After.TryGetValue("dlssnr_amd_pass1.dll",out hash)?(hash==ModernHashes["dlssnr_amd_pass1.dll"]?"0.4.0":hash==Hashes["dlssnr_amd_pass1.dll"]?"0.3.0":"未知版本"):"未安装";}
 public static void Prepare(bool modern){
  string dest=PackageFor(modern);if(String.IsNullOrWhiteSpace(dest)||!Directory.Exists(dest))throw new Exception("请先在外部组件中选择合法取得的本地桥接目录。开源版不提供、下载或修补神经运行库和模型。");
  foreach(var kv in modern?ModernHashes:Hashes)if(!File.Exists(Path.Combine(dest,kv.Key))||Core.Digest(Path.Combine(dest,kv.Key))!=kv.Value)throw new Exception("本地桥接包与已适配版本不匹配："+kv.Key+"。开源版不会自动替换或修补。");
  if(!Directory.Exists(Path.Combine(dest,"Licenses")))throw new Exception("桥接包缺少 Licenses 声明目录。");
 }
 public static bool X86(string path){try{using(var b=new BinaryReader(File.OpenRead(path))){if(b.ReadUInt16()!=0x5a4d)return false;b.BaseStream.Position=0x3c;int off=b.ReadInt32();if(off<64||off>b.BaseStream.Length-6)return false;b.BaseStream.Position=off;return b.ReadUInt32()==0x4550&&b.ReadUInt16()==0x14c;}}catch{return false;}}
 public static Game Target(Game g){string exe=g.Exe;
  if(Path.GetFileName(exe).Equals("RA3.exe",StringComparison.OrdinalIgnoreCase)){
   string actual=Path.Combine(Path.GetDirectoryName(exe),"Data","ra3_1.12.game");if(File.Exists(actual))exe=actual;
  }
  return new Game{Name=g.Name,Exe=exe,Root=g.Root,Source=g.Source,OfflineOnly=g.OfflineOnly};
 }
 public static string Api(Game g){var t=Target(g);if(!X86(t.Exe))return null;string s=Encoding.ASCII.GetString(File.ReadAllBytes(t.Exe));if(s.IndexOf("d3d9.dll",StringComparison.OrdinalIgnoreCase)>=0)return "d3d9";if(s.IndexOf("d3d11.dll",StringComparison.OrdinalIgnoreCase)>=0)return "d3d11";return null;}
 public static bool Installed(Game g){var r=StackEngine.Record(Target(g));return r!=null&&r.Profile!=null&&r.Profile.Input=="reshade32";}
 public static bool Pending(Game g){return File.Exists(StackEngine.RecordPath(Target(g))+".pending");}
 public static string Check(Game g){string api=Api(g);return api==null?"老游戏桥接：仅支持已识别的32位 DX9 / DX11 程序；64位游戏请使用其他方案。":"可尝试 ReShade 32位 "+api.ToUpperInvariant()+" 神经渲染桥接（实验性）。\r\n渲染程序："+Target(g).Exe+"\r\n不需要游戏内 FSR 开关；本路线不包含帧生成。";}
 public static Dictionary<string,string> Plan(Game g){return Plan(g,true);}
 public static Dictionary<string,string> Plan(Game g,bool modern){
  Prepare(modern);string Package=PackageFor(modern);var Hashes=modern?ModernHashes:LegacyBridge.Hashes;
  string api=Api(g);if(api==null)throw new Exception(Check(g));
  foreach(var pair in Hashes){string f=Path.Combine(Package,pair.Key);if(!File.Exists(f)||Core.Digest(f)!=pair.Value)throw new Exception("桥接组件缺失或校验失败："+pair.Key+"。组件目录："+Package);}
  var files=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  foreach(var pair in Hashes)files[pair.Key=="ReShade32.dll"?(api=="d3d9"?"d3d9.dll":"dxgi.dll"):pair.Key]=Path.Combine(Package,pair.Key);
  foreach(var f in Directory.GetFiles(Path.Combine(Package,"Licenses")))files[Path.Combine("AMDNR-Licenses",Path.GetFileName(f))]=f;
  string stage=Path.Combine(StackEngine.Home,"prepared",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
  string ini=Path.Combine(stage,"amd-nr.ini");File.WriteAllText(ini,"[amd-nr]\r\nStartOn=0\r\nToggleKey=35\r\nToggleMods=1\r\nDisableOnAltTab=1\r\nScale=0.5\r\nPasses=1\r\nEncoding=0\r\nLanguage=0\r\nColourStrength=0.25\r\nStructure=1\r\nSkin=-1\r\nInline=1\r\nAsync=1\r\n",new UTF8Encoding(false));files["amd-nr.ini"]=ini;
  string reshade=Path.Combine(stage,"ReShade.ini");File.WriteAllText(reshade,"[ADDON]\r\nAddonPath=.\r\nDisabledAddons=\r\n[GENERAL]\r\nPerformanceMode=1\r\n[OVERLAY]\r\nTutorialProgress=4\r\n",new UTF8Encoding(false));files["ReShade.ini"]=reshade;return files;
 }
 public static void Install(Game g,Action<string> report){Install(g,Version(g)!="0.3.0",report,true);}
 public static void Install(Game g,bool modern,Action<string> report,bool enable){
  Core.EnsureStopped(g);var t=Target(g);Core.EnsureStopped(t);
  foreach(var f in Core.Files(g.Root,12,CancellationToken.None))if(Regex.IsMatch(f,"easyanticheat|battleye|beservice|anticheatexpert|eaanticheat|vgk\\.|faceit",RegexOptions.IgnoreCase)&&!g.OfflineOnly)throw new Exception("发现反作弊组件，请先核对游戏支持的离线模式。");
  if(Core.GetRecord(g)!=null)throw new Exception("请先恢复独立版插件。");
  var existing=StackEngine.Record(t);if(existing!=null&&!Installed(g))throw new Exception("渲染目录已有其他组合，请先恢复。");
  var files=Plan(g,modern);
  if(existing!=null){foreach(var name in new[]{"amd-nr.ini","ReShade.ini"}){string current=Path.Combine(Path.GetDirectoryName(t.Exe),name);if(File.Exists(current))files[name]=current;}}
  // RA3's old image-helper library has no signature. Preserve only the inspected exact binary;
  // never copy over it or relax collision checks for unknown dbghelp variants.
  var preserved=new Dictionary<string,string>();if(Path.GetFileName(t.Exe).Equals("ra3_1.12.game",StringComparison.OrdinalIgnoreCase))preserved["dbghelp.dll"]="f089f6b1aa2a324603728c0453568201cb0ab6b8d3e8d6dcc2b000ad5cdfaba4";
  StackEngine.ApplyPrepared(t,new StackProfile{Name="ReShade AMD NR 32位桥接 / Daniel "+(modern?"0.4.0 本地试验":"0.3.0 原版"),Package=PackageFor(modern),Input="reshade32",Output="off",Neural=true,Proxy=Api(g)=="d3d9"?"d3d9.dll":"dxgi.dll"},files,report,preserved);
  if(enable)SetEnabled(g,true);
 }
 public static void SetEnabled(Game g,bool enabled){Core.EnsureStopped(g);var t=Target(g);Core.EnsureStopped(t);if(!Installed(g))throw new Exception("尚未安装老游戏桥接。");string p=Path.Combine(Path.GetDirectoryName(t.Exe),"amd-nr.ini");string ini=StackEngine.Patch(Core.Read(p),"amd-nr","StartOn",enabled?"1":"0");if(enabled)ini=StackEngine.Patch(ini,"amd-nr","DisableOnAltTab","0");File.WriteAllText(p,ini,new UTF8Encoding(false));}
 public static void Restore(Game g){Core.EnsureStopped(g);if(!Installed(g)&&!Pending(g))throw new Exception("没有桥接安装记录。");StackEngine.Restore(Target(g));}
}

public class LegacyBridgeForm:Form {
 Game game;ComboBox version=new ComboBox();Label status=new Label();FlowLayoutPanel buttons=new FlowLayoutPanel();bool busy;
 public LegacyBridgeForm(Game g){game=g;Text="老游戏神经渲染 · "+g.Name;ClientSize=new Size(720,490);Font=new Font("Microsoft YaHei UI",10);StartPosition=FormStartPosition.CenterParent;
  Controls.Add(new Label{Text=LegacyBridge.Check(g)+"\r\n\r\n0.4.0：本地试验适配，已通过红警3菜单短测；4K长时间稳定性待验证。\r\n0.3.0：保留原版，选中后可切回。两版均不内置，需自行取得并导入匹配目录。\r\n新安装默认关闭，50%分辨率、1次处理。版本切换保留画面设置。\r\n“下次启动开启”会关闭切出自动停用，避免启动时被误关。\r\nHome 菜单；Ctrl+End 切换。不包含升频或插帧。",Location=new Point(18,18),Size=new Size(684,200)});
  Controls.Add(new Label{Text="选择运行库",Location=new Point(18,225),Size=new Size(115,30)});
  version.DropDownStyle=ComboBoxStyle.DropDownList;version.Items.AddRange(new object[]{"Daniel 0.4.0（本地试验版）","Daniel 0.3.0（原版／回退）"});version.SelectedIndex=LegacyBridge.Version(g)=="0.3.0"?1:0;version.SetBounds(135,221,410,32);Controls.Add(version);
  buttons.SetBounds(18,267,684,140);Controls.Add(buttons);
  Add("检查本地组件",()=>{var plan=LegacyBridge.Plan(game,selectedModern);Report("组件校验通过："+plan.Count+" 个文件，未修改游戏。");});
  Add("安装／切换版本",()=>LegacyBridge.Install(game,selectedModern,Report,false));Add("下次启动开启",()=>LegacyBridge.SetEnabled(game,true));Add("下次启动关闭",()=>LegacyBridge.SetEnabled(game,false));Add("卸载并恢复文件",()=>LegacyBridge.Restore(game));
  var folder=new Button{Text="打开渲染目录",Width=145,Height=36};folder.Click+=delegate{Process.Start("explorer.exe",Path.GetDirectoryName(LegacyBridge.Target(game).Exe));};buttons.Controls.Add(folder);
  status.SetBounds(18,415,684,65);Controls.Add(status);status.Text="当前运行库："+LegacyBridge.Version(g)+"。安装状态不代表游戏内效果已生效。";
  FormClosing+=delegate(object s,FormClosingEventArgs e){if(busy)e.Cancel=true;};
 }
 bool selectedModern;
 void Report(string s){if(InvokeRequired){BeginInvoke((Action)(()=>Report(s)));return;}status.Text=s;}
 void Add(string text,Action a){var b=new Button{Text=text,Width=145,Height=36};b.Click+=async delegate{if(busy)return;busy=true;selectedModern=version.SelectedIndex==0;buttons.Enabled=false;version.Enabled=false;try{await Task.Run(a);Report("操作完成。当前运行库："+LegacyBridge.Version(game)+"。设置下次启动生效。");}catch(Exception e){Report(e.Message);MessageBox.Show(this,e.Message,"老游戏桥接");}finally{busy=false;buttons.Enabled=true;version.Enabled=true;}};buttons.Controls.Add(b);}
}

public static class LegacyBridgeTests {
 static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
 public static void Run(){
  string root=Path.Combine(Path.GetTempPath(),"LegacyBridgeTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"Data"));ExternalComponents.PathOverride=Path.Combine(root,"components.json");
  var componentSettings=new ComponentPaths{Legacy030=Path.Combine(root,"legacy030"),Legacy040=Path.Combine(root,"legacy040")};ExternalComponents.Save(componentSettings);
  foreach(bool modern in new[]{false,true}){string folder=LegacyBridge.PackageFor(modern);Directory.CreateDirectory(Path.Combine(folder,"Licenses"));var hashes=modern?LegacyBridge.ModernHashes:LegacyBridge.Hashes;foreach(var key in hashes.Keys.ToArray()){string file=Path.Combine(folder,key);File.WriteAllText(file,"inert fixture "+key+(modern?"040":"030"));hashes[key]=Core.Digest(file);}File.WriteAllText(Path.Combine(folder,"Licenses","TEST.txt"),"Synthetic fixture. No real GPU runtime.");}
  byte[] pe=new byte[512];pe[0]=77;pe[1]=90;pe[60]=128;pe[128]=80;pe[129]=69;pe[132]=0x4c;pe[133]=1;Encoding.ASCII.GetBytes("d3d9.dll").CopyTo(pe,300);
  File.WriteAllBytes(Path.Combine(root,"RA3.exe"),pe);File.WriteAllBytes(Path.Combine(root,"Data","ra3_1.12.game"),pe);
  var game=new Game{Name="RA3 fixture",Exe=Path.Combine(root,"RA3.exe"),Root=root};var target=LegacyBridge.Target(game);
  Assert(target.Exe.EndsWith("ra3_1.12.game"),"Real render target");Assert(LegacyBridge.Api(game)=="d3d9","DX9 x86 identified");
  string original=Core.Digest(target.Exe);LegacyBridge.Install(game,s=>{});Assert(LegacyBridge.Installed(game),"Managed record created");
  Assert(!File.Exists(Path.Combine(root,"d3d9.dll"))&&File.Exists(Path.Combine(root,"Data","d3d9.dll")),"Render directory used");
  LegacyBridge.SetEnabled(game,false);Assert(StackEngine.ReadIni(Core.Read(Path.Combine(root,"Data","amd-nr.ini")),"amd-nr","StartOn")=="0","Disable preset");
  Assert(LegacyBridge.Version(game)=="0.4.0","Modern default");
  string settings=Core.Read(Path.Combine(root,"Data","amd-nr.ini"));
  LegacyBridge.Install(game,false,s=>{},false);Assert(LegacyBridge.Version(game)=="0.3.0","Rollback version");
  Assert(Core.Read(Path.Combine(root,"Data","amd-nr.ini"))==settings,"Switch preserves settings");
  LegacyBridge.Install(game,true,s=>{},false);Assert(LegacyBridge.Version(game)=="0.4.0","Upgrade version");
  string host=Path.Combine(root,"Data","amd-nr-host64.exe");byte[] hostBytes=File.ReadAllBytes(host);File.AppendAllText(host,"tampered");
  bool changedBlocked=false;try{LegacyBridge.Install(game,false,s=>{},false);}catch{changedBlocked=true;}
  Assert(changedBlocked&&Core.Digest(Path.Combine(root,"Data","dlssnr_amd_pass1.dll"))==LegacyBridge.ModernHashes["dlssnr_amd_pass1.dll"],"Tampering blocks swap before writes");File.WriteAllBytes(host,hostBytes);
  LegacyBridge.Restore(game);Assert(!File.Exists(Path.Combine(root,"Data","d3d9.dll"))&&Core.Digest(target.Exe)==original,"Restore keeps game binary");
  File.WriteAllText(Path.Combine(root,"Data","d3d9.dll"),"old-mod");bool blocked=false;try{LegacyBridge.Install(game,s=>{});}catch{blocked=true;}
  Assert(blocked&&File.ReadAllText(Path.Combine(root,"Data","d3d9.dll"))=="old-mod","Existing proxy protected");
  Assert(!File.Exists(Path.Combine(root,"Data","amd-nr-host64.exe")),"No partial install on conflict");
  Core.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"老游戏桥接测试.json"),new{passed=true,checks=13,scope="Synthetic x86 DX9 RA3 layout; inert synthetic package hashes; install, toggle, restore, proxy conflict. No GPU rendering test."});
 }
}
