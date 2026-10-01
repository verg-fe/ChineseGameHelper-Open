using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using Microsoft.Win32;

public class Game {
 public string Name, Exe, Root, Source, SteamId;
 public bool OfflineOnly;
 public override string ToString(){return Name;}
}
public class Settings { public List<Game> Games=new List<Game>(); public string Model=""; }
public class Record { public string Folder; public Dictionary<string,string> Before=new Dictionary<string,string>(); public Dictionary<string,string> After=new Dictionary<string,string>(); public string Backup; }
public static class Core {
 public static readonly string Data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ChineseGameHelper.Open");
 public const string Hash="2d37453e918a1c5487314b3ae7c088b624ac0be1b82469427ec04b481a845ded";
 public const string Ini="dlssnr_on_amd.ini";
 public static readonly string[] Proxies={"version.dll","winmm.dll","dbghelp.dll","wininet.dll","winhttp.dll","dxgi.dll"};
 public static readonly string[] Managed=Proxies.Concat(new[]{Ini,"dlssnr_on_amd_weights.bin","dlssnr_on_amd.log","nvngx_dlssnr.dll"}).ToArray();
 public static JavaScriptSerializer Json=new JavaScriptSerializer();
 public static string Digest(string p){using(var s=File.OpenRead(p))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 public static void Save(string p,object o){Directory.CreateDirectory(Path.GetDirectoryName(p)); var t=p+".tmp"; File.WriteAllText(t,Json.Serialize(o),Encoding.UTF8); if(File.Exists(p))File.Replace(t,p,null);else File.Move(t,p);}
 public static string Read(string p){try{return File.ReadAllText(p);}catch{return "";}}
 public static bool X64(string p){try{using(var b=new BinaryReader(File.OpenRead(p))){if(b.ReadUInt16()!=0x5a4d)return false;b.BaseStream.Position=0x3c;int off=b.ReadInt32();if(off<64||off>b.BaseStream.Length-6)return false;b.BaseStream.Position=off;return b.ReadUInt32()==0x4550&&b.ReadUInt16()==0x8664;}}catch{return false;}}
 public static IEnumerable<string> Files(string root,int depth,CancellationToken token){
  var q=new Queue<Tuple<string,int>>();q.Enqueue(Tuple.Create(root,0));int count=0;
  while(q.Count>0){token.ThrowIfCancellationRequested();var n=q.Dequeue(); if(++count>180000)yield break;
   string[] fs=new string[0],ds=new string[0];try{fs=Directory.GetFiles(n.Item1);if(n.Item2<depth)ds=Directory.GetDirectories(n.Item1);}catch{}
   foreach(var f in fs)yield return f;
   foreach(var d in ds){var name=Path.GetFileName(d).ToLowerInvariant();if(new[]{"windows","$recycle.bin","system volume information","node_modules",".git","appdata","winsxs","windowsapps"}.Contains(name))continue;try{if((File.GetAttributes(d)&FileAttributes.ReparsePoint)!=0)continue;}catch{continue;}q.Enqueue(Tuple.Create(d,n.Item2+1));}
  }
 }
 public static bool Candidate(string f){return f.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)&&!Regex.IsMatch(Path.GetFileName(f),"setup|install|unins|crash|report|redist|helper|benchmark|server|launcher|vc_red|dxsetup|dlssnr|notification|anticheat|overlay|vconsole|worldbuilder|runme|_code",RegexOptions.IgnoreCase);}
 static string Vdf(string s,string key){var m=Regex.Match(s,"\""+Regex.Escape(key)+"\"\\s*\"([^\"]*)\"");return m.Success?m.Groups[1].Value.Replace("\\\\","\\"):"";}
 public static List<Game> Scan(bool full,string extra,Action<string> progress,CancellationToken token){
  var games=new List<Game>();var roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));
  foreach(var key in new[]{@"HKEY_CURRENT_USER\Software\Valve\Steam",@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam"}){var v=Registry.GetValue(key,"SteamPath",null)??Registry.GetValue(key,"InstallPath",null);if(v!=null)roots.Add(v.ToString());}
  foreach(var drive in DriveInfo.GetDrives().Where(d=>d.IsReady&&d.DriveType==DriveType.Fixed))roots.Add(Path.Combine(drive.RootDirectory.FullName,"SteamLibrary"));
  foreach(var root in roots.ToArray()){string lib=Read(Path.Combine(root,"steamapps","libraryfolders.vdf"));foreach(Match m in Regex.Matches(lib,"\"path\"\\s*\"([^\"]+)\""))roots.Add(m.Groups[1].Value.Replace("\\\\","\\"));}
  foreach(var root in roots){string apps=Path.Combine(Path.GetFullPath(root),"steamapps");if(!Directory.Exists(apps))continue;foreach(var acf in Directory.GetFiles(apps,"appmanifest_*.acf")){token.ThrowIfCancellationRequested();var txt=Read(acf);var name=Vdf(txt,"name");var dir=Path.Combine(apps,"common",Vdf(txt,"installdir"));if(!Directory.Exists(dir)||name.Contains("Redistributables"))continue;progress("定位 Steam 游戏："+name);AddGame(games,name,dir,"Steam",Vdf(txt,"appid"),token);}}
  string epic=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),@"Epic\EpicGamesLauncher\Data\Manifests");
  if(Directory.Exists(epic))foreach(var f in Directory.GetFiles(epic,"*.item")){try{var d=Json.Deserialize<Dictionary<string,object>>(Read(f));string root=d["InstallLocation"].ToString();string exe=Path.Combine(root,d["LaunchExecutable"].ToString());if(File.Exists(exe))games.Add(new Game{Name=d["DisplayName"].ToString(),Exe=exe,Root=root,Source="Epic"});}catch{}}
  var loose=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  if(!String.IsNullOrEmpty(extra))loose.Add(extra);
  else foreach(var drive in DriveInfo.GetDrives().Where(d=>d.IsReady&&d.DriveType==DriveType.Fixed)){
   if(full)loose.Add(drive.RootDirectory.FullName);
   else {foreach(var n in new[]{"Games","游戏","WeGameApps","XboxGames","GOG Games","Epic Games","Ubisoft Game Launcher\\games","EA Games","Program Files\\EA Games","Program Files\\Epic Games","Program Files (x86)\\GOG Galaxy\\Games"}){var p=Path.Combine(drive.RootDirectory.FullName,n);if(Directory.Exists(p))loose.Add(p);}}
  }
  foreach(var root in loose){progress("扫描目录："+root);foreach(var f in Files(root,full?30:12,token)){if(!Candidate(f))continue;if(games.Any(g=>f.StartsWith(g.Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))continue;string dir=Path.GetDirectoryName(f);bool evidence=File.Exists(Path.Combine(dir,"steam_api64.dll"))||File.Exists(Path.Combine(dir,"UnityPlayer.dll"))||Path.GetFileName(f).IndexOf("-Win64-Shipping",StringComparison.OrdinalIgnoreCase)>=0||(!full&&extra!=null);
    if(!evidence)continue;games.Add(new Game{Name=Path.GetFileNameWithoutExtension(f).Replace("-Win64-Shipping",""),Exe=f,Root=dir,Source=full?"磁盘识别 · 待核对":"目录识别 · 待核对"});
  }}
  return games.GroupBy(g=>g.Exe,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).OrderBy(g=>g.Name).ToList();
 }
 static void AddGame(List<Game> games,string name,string root,string source,string id,CancellationToken token){
  var exes=Files(root,7,token).Where(Candidate).ToList();if(exes.Count==0)return;
  string exe=exes.OrderByDescending(p=>Score(p,name) - (p.Substring(root.Length).Count(c=>c==Path.DirectorySeparatorChar)*50000000L)).First();games.Add(new Game{Name=name,Exe=exe,Root=root,Source=source,SteamId=id});
 }
 static long Score(string p,string name){long s=0;string n=Path.GetFileNameWithoutExtension(p);if(n.IndexOf("shipping",StringComparison.OrdinalIgnoreCase)>=0)s+=3000000000L;if(X64(p))s+=100000000;if(n.Equals(name,StringComparison.OrdinalIgnoreCase))s+=1000000000;try{s+=Math.Min(new FileInfo(p).Length,90000000);}catch{}return s;}
 public static string Check(Game g){
  if(!File.Exists(g.Exe))return "游戏文件不存在";if(!X64(g.Exe))return "不支持：非 x64 游戏";
  bool fsr=false,anti=false;foreach(var f in Files(g.Root,16,CancellationToken.None)){var n=Path.GetFileName(f).ToLowerInvariant();if(Regex.IsMatch(f,"easyanticheat|battleye|beservice|anticheatexpert|eaanticheat|vgk\\.|vgc\\.|faceit|ace-base|ace-guard",RegexOptions.IgnoreCase))anti=true;if(n.Contains("fsr3")||n.Contains("fidelityfx")&&n.Contains("dx12"))fsr=true;}
  if(anti&&!g.OfflineOnly)return "需要选择游戏模式：发现反作弊组件；如只玩官方支持的离线／故事模式，请在“游戏模式”中选择仅离线。";
  if(!fsr)return "待核对：未识别到 FSR / DX12 组件线索；不等于游戏不支持 FSR，内置或其他集成方式可能漏检。";
  return g.OfflineOnly?"可尝试：仅离线／故事模式。FSR / DX12 线索已找到；本选项不自动关闭反作弊，也不阻止进入在线。":"可尝试：发现 FSR / DX12 线索";
 }
 public static string RecordPath(Game g){using(var h=SHA256.Create()){var id=BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(Path.GetDirectoryName(g.Exe).ToLowerInvariant()))).Replace("-","");return Path.Combine(Data,"installs",id,"record.json");}}
 public static Record GetRecord(Game g){string p=RecordPath(g);return File.Exists(p)?Json.Deserialize<Record>(File.ReadAllText(p)):null;}
 public static string GetIni(string text,string key){bool section=false;foreach(var line in text.Split('\n')){string s=line.Trim();if(s.StartsWith("[")){section=s.Equals("[DlssNrOnAmd]",StringComparison.OrdinalIgnoreCase);continue;}if(section){var m=Regex.Match(s,"^"+Regex.Escape(key)+"\\s*=\\s*([^;#]*)",RegexOptions.IgnoreCase);if(m.Success)return m.Groups[1].Value.Trim();}}return null;}
 public static string SetIni(string text,string key,string val){var lines=text.Replace("\r\n","\n").Split('\n');bool section=false,found=false;for(int i=0;i<lines.Length;i++){var s=lines[i].Trim();if(s.StartsWith("[")){section=s.Equals("[DlssNrOnAmd]",StringComparison.OrdinalIgnoreCase);continue;}if(section&&Regex.IsMatch(s,"^"+Regex.Escape(key)+"\\s*=",RegexOptions.IgnoreCase)){lines[i]=key+"="+val;found=true;}}
  if(!found)throw new Exception("当前插件配置不含受支持的选项："+key+"。请保留原配置。");return String.Join("\r\n",lines);
 }
 public static void EnsureStopped(Game g){foreach(var p in Process.GetProcesses()){bool running=false;try{running=String.Equals(p.MainModule.FileName,g.Exe,StringComparison.OrdinalIgnoreCase);}catch{}finally{p.Dispose();}if(running)throw new InvalidOperationException("请先退出游戏，再修改安装或设置。");}}
 public static void Preset(Game g,string tone,string structure,string enabled){EnsureStopped(g);var r=GetRecord(g);if(r==null)throw new Exception("请先通过本助手完成安装。");string p=Path.Combine(r.Folder,Ini);var text=Read(p);text=SetIni(text,"Enabled",enabled);if(tone!=null){text=SetIni(text,"LocalTone",tone);text=SetIni(text,"LocalStructure",structure);text=SetIni(text,"SkinStructure","-1");}File.WriteAllText(p,text,new UTF8Encoding(false));r.After[Ini]=Digest(p);Save(RecordPath(g),r);}
 public static void ValidateModel(string p){if(!File.Exists(p)||!Path.GetFileName(p).Equals("nvngx_dlssnr.dll",StringComparison.OrdinalIgnoreCase)||!X64(p))throw new Exception("请选择 x64 的 nvngx_dlssnr.dll。");}
 public static bool IsMicrosoftSystemDll(string path){
  if(!File.Exists(path))return false;
  if(!String.Equals(FileVersionInfo.GetVersionInfo(path).CompanyName,"Microsoft Corporation",StringComparison.OrdinalIgnoreCase))return false;
  // File metadata alone is not a signature check. Verify trust through Windows.
  string script="$s=Get-AuthenticodeSignature -LiteralPath $env:DLSSHELPER_VERIFY_FILE; if($s.Status -eq 'Valid' -and $s.SignerCertificate.Subject -match '(^|,\\s*)O=Microsoft Corporation(,|$)'){exit 0}else{exit 1}";
  string shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");
  var psi=new ProcessStartInfo(shell,"-NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(script))){UseShellExecute=false,CreateNoWindow=true};psi.EnvironmentVariables["DLSSHELPER_VERIFY_FILE"]=path;
  using(var process=Process.Start(psi)){if(!process.WaitForExit(15000)){process.Kill();process.WaitForExit();return false;}return process.ExitCode==0;}
 }
 public static string EnsureInstaller(){return ExternalComponents.Installer();}
  public static void Install(Game g,string model,Action<string> progress){
   if(StackEngine.Record(g)!=null)throw new Exception("此游戏已由“组合 / 帧生成”管理。请进入组合窗口切换方案；更换为 AMD-NR 集成包时，先点“恢复组合文件”，再应用新组合。无需手动删除 dxgi.dll，也不要再安装独立神经渲染代理。");
  EnsureStopped(g);string check=Check(g);if(!check.StartsWith("可尝试"))throw new Exception(check);ValidateModel(model);
  string folder=Path.GetDirectoryName(g.Exe);string probe=Path.Combine(folder,".dlss-write-test-"+Guid.NewGuid().ToString("N"));try{using(File.Create(probe)){} File.Delete(probe);}catch{throw new Exception("无法写入游戏目录。请将游戏放在可写目录，或用管理员身份运行助手。");}if(GetRecord(g)!=null)throw new Exception("此游戏已由助手管理。可直接开启、关闭或恢复后重装。");
  foreach(var n in Proxies.Concat(new[]{Ini,"dlssnr_on_amd_weights.bin"})){string p=Path.Combine(folder,n);if(File.Exists(p)&&!(Proxies.Contains(n)&&IsMicrosoftSystemDll(p)))throw new Exception("发现已有文件 "+n+"，无法确认可安全共存。文件已保留；请检查原模组或使用原工具移除。");}
  string modelTarget=Path.Combine(folder,"nvngx_dlssnr.dll");if(File.Exists(modelTarget)&&Digest(modelTarget)!=Digest(model))throw new Exception("游戏目录已有不同的模型 DLL，停止安装以保留原文件。");
  Directory.CreateDirectory(Data);var rec=new Record{Folder=folder,Backup=Path.Combine(Path.GetDirectoryName(RecordPath(g)),"backup")};Directory.CreateDirectory(rec.Backup);
  foreach(var n in Managed){string p=Path.Combine(folder,n);if(File.Exists(p)){rec.Before[n]=Digest(p);File.Copy(p,Path.Combine(rec.Backup,n),true);}}
  // A durable journal permits recovery even if this process is interrupted.
  Save(RecordPath(g)+".pending",rec);
  string stage=Path.Combine(Data,"staging",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);string setup=Path.Combine(stage,"dlssnr_on_amd_setup.exe");
  try{
   File.Copy(EnsureInstaller(),setup);if(Digest(setup)!=Hash)throw new Exception("原版安装器校验失败。");
   File.WriteAllText(Path.Combine(stage,"SpecialK.deny.dlssnr_on_amd_setup"),"");if(!File.Exists(modelTarget))File.Copy(model,modelTarget);
   progress("请在原版安装窗口完成安装，随后关闭该窗口，助手再验证结果。不要选择覆盖已有的微软 DLL。");
   var psi=new ProcessStartInfo(setup,"\""+folder+"\""){WorkingDirectory=stage,UseShellExecute=false};
   using(var process=Process.Start(psi)){process.WaitForExit();if(process.ExitCode!=0)throw new Exception("原版安装器未成功退出，正在恢复安装前文件。");}
   foreach(var n in Proxies.Where(n=>rec.Before.ContainsKey(n))){var p=Path.Combine(folder,n);if(!File.Exists(p)||Digest(p)!=rec.Before[n])throw new Exception("原版安装器修改了原有 "+n+"；已停止并恢复原文件，请改选其他加载名称。");}
   if(!Proxies.Any(n=>!rec.Before.ContainsKey(n)&&File.Exists(Path.Combine(folder,n)))||!File.Exists(Path.Combine(folder,"dlssnr_on_amd_weights.bin")))throw new Exception("未生成完整插件和模型。可能取消了安装，或模型转换失败；正在恢复安装前文件。");
   string cfg=Read(Path.Combine(folder,Ini));foreach(var k in new[]{"Enabled","UseFsrInputs","UseDepth","Temporal","Interop","PreUpscale"})if(GetIni(cfg,k)!="1")throw new Exception("配置校验失败："+k);if(GetIni(cfg,"Async")!="0")throw new Exception("配置未采用游戏内联模式。");
   foreach(var n in Managed){string p=Path.Combine(folder,n);if(File.Exists(p))rec.After[n]=Digest(p);}Save(RecordPath(g),rec);File.Delete(RecordPath(g)+".pending");progress("安装文件已验证。实际生效仍需进游戏开启 FSR 并检查画面。");
  }catch{foreach(var n in Managed){string p=Path.Combine(folder,n);if(rec.Before.ContainsKey(n))File.Copy(Path.Combine(rec.Backup,n),p,true);else if(File.Exists(p))File.Delete(p);}if(File.Exists(RecordPath(g)+".pending"))File.Delete(RecordPath(g)+".pending");throw;}
 }
 public static void Restore(Game g){EnsureStopped(g);var r=GetRecord(g);if(r==null)throw new Exception("本助手没有该游戏的安装记录，不能自动删除其他工具的文件。");
  foreach(var pair in r.After){if(!Managed.Contains(pair.Key))throw new Exception("安装记录含未知文件。");string p=Path.Combine(r.Folder,pair.Key);if(File.Exists(p)&&pair.Key!="dlssnr_on_amd.log"&&Digest(p)!=pair.Value)throw new Exception(pair.Key+" 在安装后被其他程序修改，已停止恢复以保护文件。");}
  foreach(var pair in r.Before){if(!Managed.Contains(pair.Key)||!File.Exists(Path.Combine(r.Backup,pair.Key))||Digest(Path.Combine(r.Backup,pair.Key))!=pair.Value)throw new Exception("原文件备份不完整，已停止恢复。");}
  foreach(var pair in r.After){string p=Path.Combine(r.Folder,pair.Key);if(r.Before.ContainsKey(pair.Key))File.Copy(Path.Combine(r.Backup,pair.Key),p,true);else if(File.Exists(p))File.Delete(p);}File.Delete(RecordPath(g));
 }
}

public class MainForm:Form {
 Settings settings;ListView list=new ListView();TextBox search=new TextBox();Label status=new Label(),detail=new Label(),count=new Label();FlowLayoutPanel actions=new FlowLayoutPanel();CancellationTokenSource scanCancel;bool busy;List<Game> games;Button scanButton;string statePath=Path.Combine(Core.Data,"settings.json");
 Color bg=Color.FromArgb(17,23,35),panel=Color.FromArgb(26,35,50),muted=Color.FromArgb(160,179,203),accent=Color.FromArgb(63,219,173);
 public MainForm(){
  Text="中文游戏助手 · 开源版";ClientSize=new Size(1160,750);MinimumSize=new Size(1020,700);StartPosition=FormStartPosition.CenterScreen;BackColor=bg;ForeColor=Color.White;Font=new Font("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.Dpi;
  try{settings=Core.Json.Deserialize<Settings>(Core.Read(statePath))??new Settings();}catch{settings=new Settings();}games=settings.Games??new List<Game>();
  var title=new Label{Text="中文游戏助手  /  开源版",Font=new Font(Font.FontFamily,24,FontStyle.Bold),AutoSize=true,Location=new Point(28,24),ForeColor=accent};Controls.Add(title);
  var sub=new Label{Text="MIT 助手源码  ·  第三方组件独立获取  ·  不含运行库或模型",AutoSize=true,Location=new Point(30,78),ForeColor=muted};Controls.Add(sub);
  var stack=Button("组合 / 帧生成",delegate{if(busy){Message("请先完成当前操作。");return;}using(var f=new StackForm(games,list.SelectedItems.Count>0?Selected():null)){f.ShowDialog(this);}RefreshList();ShowDetail();},195);stack.Location=new Point(935,44);stack.Anchor=AnchorStyles.Top|AnchorStyles.Right;Controls.Add(stack);
  var browser=Button("网页 / 视频浏览器",delegate{try{BrowserSupport.Open(this);}catch(Exception ex){Message(ex.Message);}},195);browser.Location=new Point(728,44);browser.Anchor=AnchorStyles.Top|AnchorStyles.Right;Controls.Add(browser);
  var toolbar=new FlowLayoutPanel{Location=new Point(26,116),Size=new Size(1108,48),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};Controls.Add(toolbar);
  scanButton=Button("扫描游戏",async delegate{await Scan(false,null);});toolbar.Controls.Add(scanButton);
  toolbar.Controls.Add(Button("全盘深度扫描",async delegate{await Scan(true,null);}));toolbar.Controls.Add(Button("扫描指定目录",async delegate{using(var d=new FolderBrowserDialog()){d.Description="选择游戏库目录";if(d.ShowDialog()==DialogResult.OK)await Scan(false,d.SelectedPath);}}));
  toolbar.Controls.Add(Button("手动添加 EXE",delegate{Add();}));toolbar.Controls.Add(Button("导入模型 DLL",delegate{ChooseModel();}));toolbar.Controls.Add(Button("停止扫描",delegate{if(scanCancel!=null)scanCancel.Cancel();}));
  search.SetBounds(30,180,720,30);search.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;search.BackColor=panel;search.ForeColor=Color.White;search.TextChanged+=delegate{RefreshList();};Controls.Add(search);
  count.SetBounds(770,184,340,28);count.Anchor=AnchorStyles.Top|AnchorStyles.Right;count.ForeColor=muted;Controls.Add(count);
  list.SetBounds(30,224,720,374);list.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;list.View=View.Details;list.FullRowSelect=true;list.MultiSelect=false;list.HideSelection=false;list.BackColor=panel;list.ForeColor=Color.White;list.BorderStyle=BorderStyle.None;list.Columns.Add("游戏 / 搜索名称",270);list.Columns.Add("来源",160);list.Columns.Add("安装状态",250);list.SelectedIndexChanged+=delegate{ShowDetail();};Controls.Add(list);
  var side=new Panel{Location=new Point(774,224),Size=new Size(356,374),Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Right,BackColor=panel};Controls.Add(side);
  detail.SetBounds(18,16,320,110);detail.ForeColor=Color.White;side.Controls.Add(detail);
  actions.SetBounds(12,130,336,235);actions.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Bottom;side.Controls.Add(actions);
  actions.Controls.Add(Button("检测方案 / 安装",async delegate{await Install();},155));actions.Controls.Add(Button("启动游戏",delegate{Launch();},155));
  actions.Controls.Add(Button("中文画面设置",delegate{Configure();},155));actions.Controls.Add(Button("关闭效果",async delegate{var g=Selected(); await Work(()=>{if(LegacyBridge.Installed(g))LegacyBridge.SetEnabled(g,false);else Core.Preset(g,null,null,"0");});},155));
  actions.Controls.Add(Button("在线前恢复文件",async delegate{await RestoreForOnline();},155));actions.Controls.Add(Button("打开游戏目录",delegate{var g=Selected();Process.Start("explorer.exe",Path.GetDirectoryName(g.Exe));},155));
  var resolution=Button("输入分辨率 / 日志",delegate{if(busy)return;using(var f=new ResolutionForm(Selected())){f.ShowDialog(this);}RefreshList();},155);resolution.Name="ResolutionPresetButton";actions.Controls.Add(resolution);actions.Controls.Add(Button("重新定位 EXE",delegate{Relocate();},155));
  var note=new Label{Text="首次准备：外部组件中选择原版安装器；自行导入模型。第三方版本兼容性需要核对。\n游戏内：选择 DirectX 12，并开启 FSR 3 / 4 / FSRAA。End 仍为原版英文菜单。中文设置在下次启动生效。",Location=new Point(30,617),Size=new Size(1100,52),Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right,ForeColor=muted};Controls.Add(note);
  status.SetBounds(30,680,1100,50);status.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;status.ForeColor=accent;Controls.Add(status);actions.Controls.Add(Button("游戏模式",delegate{ChooseMode();},155));actions.Controls.Add(Button("自动适配向导",delegate{Adapt();},155));var components=Button("外部组件 / 许可",delegate{using(var f=new ComponentsForm())f.ShowDialog(this);},195);components.SetBounds(935,83,195,28);components.Anchor=AnchorStyles.Top|AnchorStyles.Right;Controls.Add(components);Status("就绪。扫描只定位游戏，不代表该游戏兼容 DLSS 5。");RefreshList();ShowDetail();
  FormClosing+=delegate(object s,FormClosingEventArgs e){if(busy){e.Cancel=true;Status("正在执行操作，请等待完成。扫描可先点击停止扫描。");}else Save();};
  Shown+=async delegate{if(!Program.Testing && games.Count==0)await Scan(false,null);};
 }
 Button Button(string text,EventHandler click,int width=164){var b=new Button{Text=text,Size=new Size(width,36),Margin=new Padding(3,3,3,6),FlatStyle=FlatStyle.Flat,BackColor=panel,ForeColor=Color.White,Cursor=Cursors.Hand};b.FlatAppearance.BorderColor=Color.FromArgb(63,85,111);b.Click+=delegate(object s,EventArgs e){try{click(s,e);}catch(Exception ex){Message(ex.Message);}};return b;}
 void Save(){settings.Games=games;Core.Save(statePath,settings);}
 void Status(string s){if(InvokeRequired){BeginInvoke((Action)(()=>Status(s)));return;}status.Text=s;}
 void Message(string s){if(InvokeRequired){Invoke((Action)(()=>Message(s)));return;}MessageBox.Show(this,s,"中文游戏助手",MessageBoxButtons.OK,MessageBoxIcon.Information);}
 Game Selected(){if(list.SelectedItems.Count==0)throw new Exception("请先在左侧选择一个游戏。");return (Game)list.SelectedItems[0].Tag;}
 void RefreshList(){string selected=list.SelectedItems.Count>0?((Game)list.SelectedItems[0].Tag).Exe:null;list.BeginUpdate();list.Items.Clear();foreach(var g in games.Where(g=>(g.Name+(g.SteamId=="602960"?" 潜渊症":"")).IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0)){string s="未安装 · 兼容性待检查";try{var r=Core.GetRecord(g);if(LegacyBridge.Installed(g))s=LegacyBridge.DisplayStatus(g);else if(LegacyBridge.Pending(g))s="桥接安装中断 · 需要恢复";else if(r!=null)s=Core.GetIni(Core.Read(Path.Combine(r.Folder,Core.Ini)),"Enabled")=="1"?"已配置开启 · 待游戏内验证":"已配置关闭";else if(StackEngine.Record(g)!=null)s="组合已部署 · 待游戏内验证";else if(File.Exists(Core.RecordPath(g)+".pending"))s="上次安装中断 · 需要检查";}catch{s="记录异常 · 需要检查";}var item=new ListViewItem(new[]{g.Name,g.Source,s}){Tag=g};list.Items.Add(item);if(g.Exe==selected)item.Selected=true;}count.Text=games.Count+" 个游戏  |  输入名称筛选";list.EndUpdate();}
 void ShowDetail(){actions.Enabled=!busy && list.SelectedItems.Count>0;if(list.SelectedItems.Count==0){detail.Text="选择一个游戏\n\n自动定位可能选中不同启动程序，可用“重新定位 EXE”修正。\n\n支持升频组合；32/64位神经桥接请使用“自动适配向导”。";return;}var g=Selected();detail.Text=g.Name+"\n\n"+g.Exe+"\n\n模式："+(g.OfflineOnly?"仅离线／故事":"常规（不跳过反作弊检查）");}
 async Task Work(Action action){if(busy){Message("请等待当前操作完成。");return;}busy=true;actions.Enabled=false;try{await Task.Run(action);Status("操作完成。设置保存后，请重新启动游戏使其生效。");}catch(Exception e){Message(e.Message);Status("操作未完成："+e.Message);}finally{busy=false;actions.Enabled=list.SelectedItems.Count>0;RefreshList();}}
 async Task Scan(bool full,string extra){if(busy)return;busy=true;actions.Enabled=false;scanCancel=new CancellationTokenSource();try{var found=await Task.Run(()=>Core.Scan(full,extra,Status,scanCancel.Token));games=games.Concat(found).Where(g=>File.Exists(g.Exe)).GroupBy(g=>g.Exe,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).OrderBy(g=>g.Name).ToList();Save();RefreshList();Status("扫描完成：共 "+games.Count+" 个游戏。受保护目录、非常规安装可能漏检，可手动添加。");}catch(OperationCanceledException){Status("扫描已停止，原游戏库保留。");}catch(Exception e){Message(e.Message);}finally{busy=false;actions.Enabled=list.SelectedItems.Count>0;scanCancel.Dispose();scanCancel=null;}}
 string Pick(string filter){using(var d=new OpenFileDialog{Filter=filter,CheckFileExists=true})return d.ShowDialog(this)==DialogResult.OK?d.FileName:null;}
 void Add(){if(busy)return;var p=Pick("游戏程序 (*.exe)|*.exe");if(p==null)return;games.Add(new Game{Name=Path.GetFileNameWithoutExtension(p),Exe=p,Root=Path.GetDirectoryName(p),Source="手动添加"});games=games.GroupBy(g=>g.Exe,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).ToList();Save();RefreshList();}
 void Relocate(){if(busy)return;var g=Selected();var p=Pick("游戏程序 (*.exe)|*.exe");if(p==null)return;if(Core.GetRecord(g)!=null||StackEngine.Record(g)!=null||LegacyBridge.Installed(g)||LegacyBridge.Pending(g))throw new Exception("请先恢复原文件，再修改程序位置。");g.Exe=p;if(!p.StartsWith(g.Root+"\\",StringComparison.OrdinalIgnoreCase))g.Root=Path.GetDirectoryName(p);Save();RefreshList();ShowDetail();}
 bool ChooseModel(){if(busy)return false;var p=Pick("DLSS 5 模型 (nvngx_dlssnr.dll)|nvngx_dlssnr.dll");if(p==null)return false;try{Core.ValidateModel(p);settings.Model=p;Save();Status("模型已选择，不限制版本；安装时只检查文件名与 x64 格式。");return true;}catch(Exception e){Message(e.Message);return false;}}
 async Task Install(){if(busy)return;Game g=Selected();if(LegacyBridge.Installed(g)||Adaptation.Routes(g,Adaptation.Detect(g)).Count>0){Adapt();return;}if(LegacyBridge.Installed(g)){await Work(()=>LegacyBridge.SetEnabled(g,true));return;}if(LegacyBridge.Api(g)!=null){await Work(()=>LegacyBridge.Install(g,Status));return;}if(File.Exists(Core.RecordPath(g)+".pending")){Message("发现中断的安装记录。请查看 "+Core.RecordPath(g)+".pending 及其 backup 目录，暂不覆盖安装。");return;}if(Core.GetRecord(g)!=null){await Work(()=>Core.Preset(g,null,null,"1"));return;}
  string local=Path.Combine(Path.GetDirectoryName(g.Exe),"nvngx_dlssnr.dll");if(File.Exists(local)){try{Core.ValidateModel(local);settings.Model=local;}catch{}}
  if(!File.Exists(settings.Model)&&!ChooseModel())return;var model=settings.Model;await Work(()=>Core.Install(g,model,Status));}
 void ChooseMode(){
  if(busy)return;var g=Selected();
  using(var f=new Form{Text="游戏模式 · "+g.Name,ClientSize=new Size(540,290),StartPosition=FormStartPosition.CenterParent,Font=Font,BackColor=bg,ForeColor=Color.White,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
   var info=new Label{Location=new Point(20,20),Size=new Size(500,155),Text=LegacyBridge.IsL4D2(g)?"L4D2 神经桥接仅限单机实验。\n\n选择仅离线后，从助手启动会自动添加 -insecure，\n该次不能加入 VAC 保护服务器。\n\n通过“老游戏 / ReShade”安装和恢复桥接。\n回联机前先恢复桥接，再从 Steam 正常启动。\n从 Steam 直接启动不会自动添加助手的离线参数。":"离线与在线共用目录的游戏，可以选择仅离线安装。\n\nGTA：先在 Rockstar Games Launcher 设置中关闭\nBattlEye，只进入故事模式。回在线前恢复插件文件，\n并重新启用 BattlEye。\n\n这个选项只改变助手的安装检查，不会关闭反作弊，\n也不能锁定游戏模式。其他游戏需确认官方支持离线模组。"};f.Controls.Add(info);
   var offline=Button("仅离线／故事模式",delegate{g.OfflineOnly=true;Save();ShowDetail();f.Close();Status("已设为仅离线／故事模式。现在可以重新点击一键安装。");},230);offline.Location=new Point(20,195);f.Controls.Add(offline);
   var normal=Button("常规／准备玩在线",delegate{if(Core.GetRecord(g)!=null||StackEngine.Record(g)!=null||LegacyBridge.Installed(g)||LegacyBridge.Pending(g)){Message("请先点击“在线前恢复文件”。关闭画面效果不会卸载插件，不能据此切回在线。");return;}g.OfflineOnly=false;Save();ShowDetail();f.Close();Status("已恢复常规检查。请在官方启动器重新启用反作弊。");},230);normal.Location=new Point(275,195);f.Controls.Add(normal);
   var link=new LinkLabel{Text="查看 Rockstar 官方故事模式说明",Location=new Point(20,250),AutoSize=true,LinkColor=accent};link.LinkClicked+=delegate{Process.Start(new ProcessStartInfo("https://support.rockstargames.com/articles/1nenwhZlVrJY6CTFeSS2Fx/grand-theft-auto-online-battleye-faq"){UseShellExecute=true});};f.Controls.Add(link);f.ShowDialog(this);
  }
 }
 async Task RestoreForOnline(){if(busy)return;var g=Selected();bool restored=false;await Work(()=>{if(LegacyBridge.Installed(g)||LegacyBridge.Pending(g))LegacyBridge.Restore(g);else if(StackEngine.Record(g)!=null||File.Exists(StackEngine.RecordPath(g)+".pending"))StackEngine.Restore(g);else Core.Restore(g);restored=true;});if(restored){g.OfflineOnly=false;Save();ShowDetail();Status("本助手安装的文件已恢复。请重新启用官方反作弊，再从游戏平台启动；其他模组需另外清理。");Message("本助手安装的文件已恢复，游戏没有自动启动。\n\nGTA：在 Rockstar Games Launcher 中重新启用 BattlEye，并移除你设置的禁用反作弊启动参数。\n\n助手只恢复自己的文件，不能保证其他模组已清理。");}}
 void Launch(){if(busy)return;var g=Selected();if(!File.Exists(g.Exe))throw new Exception("游戏程序不存在，请重新定位。");if(LegacyBridge.IsL4D2(g)&&(g.OfflineOnly||LegacyBridge.Installed(g)||LegacyBridge.Pending(g))){if(LegacyBridge.Pending(g))throw new Exception("桥接安装中断，请先恢复文件。");Process.Start(new ProcessStartInfo(g.Exe,LegacyBridge.OfflineArguments(g)){WorkingDirectory=Path.GetDirectoryName(g.Exe),UseShellExecute=true});Status("L4D2 已按 -insecure 单机测试模式启动；Home 菜单，Ctrl+End 切换。联机前先恢复桥接文件。");return;}if(g.OfflineOnly&&MessageBox.Show(this,"此次只进入离线／故事模式。\n\nGTA 请先在 Rockstar Games Launcher 设置里关闭 BattlEye。助手不会自动关闭它，也不能锁定游戏模式。\n\n已准备好，仅启动离线／故事模式？","离线启动",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;if(!String.IsNullOrEmpty(g.SteamId))Process.Start(new ProcessStartInfo("steam://rungameid/"+g.SteamId){UseShellExecute=true});else Process.Start(new ProcessStartInfo(g.Exe){WorkingDirectory=Path.GetDirectoryName(g.Exe),UseShellExecute=true});Status(LegacyBridge.Installed(g)?"已启动老游戏。Home 查看 AMD Neural Rendering；Ctrl+End 开关，不需要 FSR 选项。":"已请求启动游戏。请在画面设置中开启 FSR；本助手不能确认实际渲染效果。");}
 void Adapt(){if(busy)return;using(var f=new AdaptationForm(Selected())){f.ShowDialog(this);}RefreshList();ShowDetail();}
 void Legacy(){if(busy)return;var g=Selected();using(var f=new LegacyBridgeForm(g)){f.ShowDialog(this);}RefreshList();}
 void Configure(){if(busy)return;var g=Selected();if(LegacyBridge.Installed(g)){Legacy();return;}if(Core.GetRecord(g)==null)throw new Exception("请先安装，再使用中文设置。");using(var f=new Form{Text="中文画面设置 · 下次启动生效",ClientSize=new Size(440,290),StartPosition=FormStartPosition.CenterParent,BackColor=bg,ForeColor=Color.White,Font=Font,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
  var l=new Label{Text="无需操作英文菜单，保存预设后重新启动游戏。\n这些预设调整外观，不保证提高帧率。",Location=new Point(20,20),Size=new Size(400,50)};f.Controls.Add(l);
  var choices=new ComboBox{Location=new Point(20,90),Width=390,DropDownStyle=ComboBoxStyle.DropDownList};choices.Items.AddRange(new object[]{"自然：结构 0.5，色调 0","原版默认：结构 1，色调 0","增强：结构 1，色调 0.3"});choices.SelectedIndex=1;f.Controls.Add(choices);
  var save=Button("保存并开启",delegate{try{string[] structure={"0.5","1","1"};Core.Preset(g,choices.SelectedIndex==2?"0.3":"0",structure[choices.SelectedIndex],"1");f.Close();RefreshList();Status("已保存中文预设，下次启动生效。");}catch(Exception e){Message(e.Message);}},180);save.Location=new Point(20,150);f.Controls.Add(save);
  var hint=new Label{Text="End 叠加层仍由原版插件提供，无法在这里汉化。\nFSR 3 / 4 / FSRAA 需要在游戏自身设置中开启。",Location=new Point(20,214),Size=new Size(400,55),ForeColor=muted};f.Controls.Add(hint);f.ShowDialog(this);}}
}
public static class Program { public static bool Testing;
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool EnumWindows(WindowVisitor visitor,IntPtr data);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
 [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder text,int capacity);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetLastActivePopup(IntPtr window);
 delegate bool WindowVisitor(IntPtr window,IntPtr data);
 public static bool ActivateExisting(){
  var ids=new HashSet<int>(Process.GetProcessesByName("中文游戏助手-开源版").Where(p=>p.Id!=Process.GetCurrentProcess().Id).Select(p=>p.Id));bool found=false;
  EnumWindows(delegate(IntPtr window,IntPtr data){uint id;GetWindowThreadProcessId(window,out id);if(!ids.Contains((int)id))return true;var text=new StringBuilder(256);GetWindowText(window,text,text.Capacity);if(text.ToString()!="中文游戏助手 · 开源版")return true;
   ShowWindow(window,9);IntPtr popup=GetLastActivePopup(window);if(popup!=window){ShowWindow(popup,5);SetForegroundWindow(popup);}else SetForegroundWindow(window);found=true;return false;
  },IntPtr.Zero);return found;
 }
 [STAThread] public static int Main(string[] args){
  BrowserSupport.Register();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  if(args.Contains("--browser")){try{BrowserSupport.Open(null);return 0;}catch(Exception ex){File.WriteAllText("browser-error.txt",ex.ToString());return 1;}}
  if(args.Contains("--components-self-test")){try{ComponentTests.Run();return 0;}catch(Exception ex){File.WriteAllText("components-test-error.txt",ex.ToString());return 1;}}
  if(args.Contains("--resolution-self-test")){try{ResolutionPresetTests.Run();return 0;}catch(Exception e){File.WriteAllText("resolution-test-error.txt",e.ToString());return 1;}}
  if(args.Contains("--adapt-self-test")){try{AdaptationTests.Run();return 0;}catch(Exception e){File.WriteAllText("adapt-test-error.txt",e.ToString());return 1;}}
  if(args.Contains("--legacy-self-test")){try{LegacyBridgeTests.Run();return 0;}catch(Exception e){File.WriteAllText("legacy-test-error.txt",e.ToString());return 1;}}
  if(args.Contains("--stack-self-test")){try{StackTests.Run();return 0;}catch(Exception e){File.WriteAllText("stack-test-error.txt",e.ToString());return 1;}}
  if(args.Contains("--self-test")){try{Tests.Run();return 0;}catch(Exception e){File.WriteAllText("self-test-error.txt",e.ToString());return 1;}}
  if(args.Contains("--scan-report")){var games=Core.Scan(false,null,s=>{},CancellationToken.None);Core.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"scan-report.json"),games);return 0;}
   using(var mutex=new Mutex(false,"Local\\ChineseGameHelper.Open")){bool owned;try{owned=mutex.WaitOne(0);}catch(AbandonedMutexException){owned=true;}if(!owned){for(int i=0;i<20;i++){if(ActivateExisting())return 0;Thread.Sleep(100);}MessageBox.Show("助手正在启动或处理操作，请稍后重新打开。");return 0;}try{Application.Run(new MainForm());}finally{mutex.ReleaseMutex();}}return 0;
 }
}
public static class Tests {
 static void Assert(bool b,string message){if(!b)throw new Exception(message);}
 public static void Run(){ Program.Testing=true;
  string ini="[Other]\r\nEnabled=9\r\n[DlssNrOnAmd]\r\nEnabled=1\r\nLocalTone=0\r\nLocalStructure=1\r\nSkinStructure=-1\r\n";
  var updated=Core.SetIni(ini,"Enabled","0");Assert(Core.GetIni(updated,"Enabled")=="0","INI toggle");Assert(updated.Contains("Enabled=9"),"Unrelated section preserved");bool rejected=false;try{Core.SetIni(ini,"Unknown","1");}catch{rejected=true;}Assert(rejected,"Unknown schema blocked");
  string root=Path.Combine(Path.GetTempPath(),"DlssHelperTest-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"game.exe"),"not PE");Assert(!Core.X64(Path.Combine(root,"game.exe")),"Invalid executable rejected");Assert(!Core.Candidate("CrashReport.exe"),"Helper rejected");Assert(Core.Candidate("Game-Win64-Shipping.exe"),"Game accepted");
  string exe=Path.Combine(root,"fake.exe");var bytes=new byte[256];bytes[0]=0x4d;bytes[1]=0x5a;bytes[0x3c]=0x80;bytes[0x80]=0x50;bytes[0x81]=0x45;bytes[0x84]=0x64;bytes[0x85]=0x86;File.WriteAllBytes(exe,bytes);Assert(Core.X64(exe),"PE x64");
  var g=new Game{Name="Fixture",Exe=exe,Root=root,Source="test"};Assert(Core.Check(g).StartsWith("待核对"),"Unknown compatibility");File.WriteAllText(Path.Combine(root,"amd_fidelityfx_dx12.dll"),"");Assert(Core.Check(g).StartsWith("可尝试"),"FSR evidence");File.WriteAllText(Path.Combine(root,"EasyAntiCheat.exe"),"");Assert(Core.Check(g).StartsWith("需要选择"),"Anti-cheat requires mode selection");g.OfflineOnly=true;Assert(Core.Check(g).StartsWith("可尝试"),"Offline mode allows FSR game");File.Delete(Path.Combine(root,"amd_fidelityfx_dx12.dll"));Assert(Core.Check(g).StartsWith("待核对"),"Offline mode does not bypass FSR requirements");g.OfflineOnly=false;
  string deep=Path.Combine(root,@"Game\Plugins\FSR v4.1.1\Source\fidelityfx-sdk\Kits\FidelityFX\signedbin");Directory.CreateDirectory(deep);File.WriteAllText(Path.Combine(deep,"amd_fidelityfx_upscaler_dx12.dll"),"");g.OfflineOnly=true;Assert(Core.Check(g).StartsWith("可尝试"),"Deep Unreal FSR plugin detected");g.OfflineOnly=false;
  string modelFixture=Path.Combine(root,"nvngx_dlssnr.dll");File.Copy(exe,modelFixture);Core.ValidateModel(modelFixture);Assert(true,"Model accepted without version resource");rejected=false;try{Core.ValidateModel(exe);}catch{rejected=true;}Assert(rejected,"Model filename check retained");
  ExternalComponents.PathOverride=Path.Combine(root,"missing-components.json");rejected=false;try{Core.EnsureInstaller();}catch{rejected=true;}Assert(rejected,"No implicit installer download");
  var record=new Record{Folder=root,Backup=Path.Combine(root,"backup")};Directory.CreateDirectory(record.Backup);File.WriteAllText(Path.Combine(root,Core.Ini),ini);record.After[Core.Ini]=Core.Digest(Path.Combine(root,Core.Ini));Core.Save(Core.RecordPath(g),record);Core.Preset(g,"0.3","0.5","1");Assert(Core.GetIni(Core.Read(Path.Combine(root,Core.Ini)),"LocalTone")=="0.3","Preset saved");File.AppendAllText(Path.Combine(root,Core.Ini),";modified");rejected=false;try{Core.Restore(g);}catch{rejected=true;}Assert(rejected&&File.Exists(Path.Combine(root,Core.Ini)),"Changed files retained");var r=Core.GetRecord(g);r.After[Core.Ini]=Core.Digest(Path.Combine(root,Core.Ini));Core.Save(Core.RecordPath(g),r);Core.Restore(g);Assert(!File.Exists(Path.Combine(root,Core.Ini)),"Managed new file removed");
  using(var form=new MainForm()){form.Show();Application.DoEvents();using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"界面预览.png"));}form.Hide();}
  Core.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.json"),new{passed=true,checks=20,scope="Model version restriction removed, filename check retained, INI, PE, compatibility markers, checksum, preset, restore protection; no real game installation"});
 }
}







