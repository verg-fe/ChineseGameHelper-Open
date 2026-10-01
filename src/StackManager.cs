using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Net;

public class StackProfile {
 public string Name="我的组合",Package="",Enabler="",Weights="",Proxy="dxgi.dll",Input="dlssg",Output="off",Upscale="fsr4";
 public bool Neural; public int Multiplier=2;
}
public class StackState {public List<StackProfile> Profiles=new List<StackProfile>();}
public class StackRecord {
 public string Folder,Backup,Game,InstalledAt; public StackProfile Profile;
 public Dictionary<string,string> Before=new Dictionary<string,string>(),After=new Dictionary<string,string>();
}
public class BenchEntry {
 public string Time,Game,Profile,Scene,BaseFPS,DisplayFPS,Latency,Quality,Stability,Notes;
}
public static class StackEngine {
 public const string StableUrl="https://github.com/optiscaler/OptiScaler/releases/download/v0.9.4/Optiscaler_0.9.4-final.20260718._MM.7z";
 public const string StableHash="575cb4df866116093df75af607e37fd70e10f5163e0f23fd5c804142e80ef0ad";
 public static string Home=Path.Combine(Core.Data,"combinations");
 public static string StatePath=Path.Combine(Home,"profiles.json");
 public static string StableFolder=Path.Combine(Home,"packages","optiscaler-0.9.4");
 static readonly string[] ProxyNames={"dxgi.dll","winmm.dll","d3d12.dll","version.dll","dbghelp.dll","wininet.dll","winhttp.dll"};
 public static string Safe(string root,string relative){
  if(String.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative))throw new Exception("无效的包内路径。");
  string full=Path.GetFullPath(Path.Combine(root,relative));string prefix=Path.GetFullPath(root).TrimEnd('\\','/')+Path.DirectorySeparatorChar;
  if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new Exception("包内路径越过目标目录。");
  string cur=Path.GetDirectoryName(full);while(cur!=null&&cur.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)){if(Directory.Exists(cur)&&(File.GetAttributes(cur)&FileAttributes.ReparsePoint)!=0)throw new Exception("目录链接不能作为安装目标。");cur=Path.GetDirectoryName(cur);}
  if(File.Exists(full)&&(File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new Exception("不能覆盖链接文件。");return full;
 }
 public static string ReadIni(string text,string section,string key){bool active=false;foreach(var line in text.Replace("\r\n","\n").Split('\n')){string s=line.Trim();if(s.StartsWith("[")){active=s.Equals("["+section+"]",StringComparison.OrdinalIgnoreCase);continue;}if(active){var m=Regex.Match(s,"^"+Regex.Escape(key)+"\\s*=\\s*([^;#]*)",RegexOptions.IgnoreCase);if(m.Success)return m.Groups[1].Value.Trim();}}return null;}
 public static string Patch(string text,string section,string key,string value){
  var lines=text.Replace("\r\n","\n").Split('\n');bool active=false,found=false;
  for(int i=0;i<lines.Length;i++){string s=lines[i].Trim();if(s.StartsWith("[")){active=s.Equals("["+section+"]",StringComparison.OrdinalIgnoreCase);continue;}if(active&&Regex.IsMatch(s,"^"+Regex.Escape(key)+"\\s*=",RegexOptions.IgnoreCase)){lines[i]=key+"="+value;found=true;}}
  if(!found)throw new Exception("安装包不支持配置项 ["+section+"] "+key+"。请换用匹配版本，不能强行写入未知设置。");return String.Join("\r\n",lines);
 }
 static string Optional(string text,string section,string key,string value){return ReadIni(text,section,key)==null?text:Patch(text,section,key,value);}
 public static StackProfile Clone(StackProfile p){return Core.Json.Deserialize<StackProfile>(Core.Json.Serialize(p));}
 static bool HasMarker(string file,string marker){using(var f=File.OpenRead(file)){var buf=new byte[1024*1024];string carry="";int n;while((n=f.Read(buf,0,buf.Length))>0){string text=carry+Encoding.ASCII.GetString(buf,0,n);if(text.IndexOf(marker,StringComparison.OrdinalIgnoreCase)>=0)return true;carry=text.Substring(Math.Max(0,text.Length-128));}}return false;}
 public static string BuildConfig(string ini,StackProfile p){
  bool modern=ReadIni(ini,"FrameGen","FGNvngxReplacement")!=null;
  // This schema was verified against stable 0.9.4 and the modern upstream INI.
  bool ffx=Regex.IsMatch(ini,@"(?im)^;[^\r\n]*\bffx\b[^\r\n]*FSR")||Regex.IsMatch(ini,@"(?im)^Dx12Upscaler\s*=\s*ffx\s*$");
  ini=Patch(ini,"Upscalers","Dx12Upscaler",ffx?"ffx":"fsr31");
  string fsrSection=ReadIni(ini,"FSR","UpscalerIndex")!=null?"FSR":"FSR3";
  ini=Patch(ini,fsrSection,"UpscalerIndex",p.Upscale=="fsr4"?"0":"1");
  ini=Patch(ini,"Libraries","OptiDllPath",@".\OptiScaler");
  ini=Patch(ini,"FrameGen","Enabled",p.Output=="off"?"false":"true");
  string input=p.Input,output="fsrfg";
  if(p.Output=="off"){input="nofg";output="nofg";}
  else if(p.Output=="nukems"){input=modern?"nvngxfg":"nukems";output=modern?"auto":"nukems";if(modern)ini=Patch(ini,"FrameGen","FGNvngxReplacement","Nukems");}
  else if(p.Output=="mfg"||p.Output=="combo"){
   if(!modern||ReadIni(ini,"DLSSG","InterpolationCount")==null)throw new Exception("所选稳定包不支持多帧生成接口。请导入支持 FGNvngxReplacement / DLSSG 的实验包，再导入 DLSS Enabler headless DLL。");
   input="nvngxfg";output="auto";ini=Patch(ini,"FrameGen","FGNvngxReplacement",p.Output=="mfg"?"Arturs":"Combo");
   ini=Patch(ini,"DLSSG","InterpolationCount",(p.Multiplier-1).ToString());
  }else if(p.Output=="fsr3"||p.Output=="fsr4"){ini=Patch(ini,fsrSection,"FGIndex",p.Output=="fsr4"?"0":"1");}
  else throw new Exception("未知帧生成方案。");
  ini=Patch(ini,"FrameGen","FGInput",input);ini=Patch(ini,"FrameGen","FGOutput",output);
  ini=Optional(ini,"Inputs","EnableDlssInputs","true");
  ini=Optional(ini,"Spoofing","Dxgi",input=="dlssg"||input=="nvngxfg"||input=="nukems"?"true":"false");
  ini=Optional(ini,"Menu","FGShortcutKey","-1"); // Avoid competing with the NR End key.
  ini=Optional(ini,"Log","LogToFile","true");
  if(p.Neural){
   foreach(var pair in new Dictionary<string,string>{{"Enabled","true"},{"RunBeforeSR","true"},{"Passes","1"},{"LocalTone","0"},{"LocalStructure","1"},{"SkinStructure","1"}})ini=Patch(ini,"DlssNr",pair.Key,pair.Value);
   ini=Regex.Replace(ini,@"(?m)^;NrBackend=daniel\r?$","NrBackend=daniel");
   ini=Optional(ini,"DlssNr","NrBackend","daniel");
  }else ini=Optional(ini,"DlssNr","Enabled","false");
  return ini;
 }
 public static void FetchStable(Action<string> report){throw new Exception("开源版不自动下载组件。请打开官方来源，按其许可自行取得，并用选择目录导入。");}
 public static Dictionary<string,string> Plan(StackProfile p){
  string root=Path.GetFullPath(p.Package);if(!ProxyNames.Contains(p.Proxy))throw new Exception("不支持的加载名称。");
  string main=Safe(root,"OptiScaler.dll"),ini=Safe(root,"OptiScaler.ini");if(!Core.X64(main)||!File.Exists(ini))throw new Exception("选择的目录必须直接包含 x64 OptiScaler.dll 和 OptiScaler.ini。");
  if(root.Equals(Path.GetFullPath(StableFolder),StringComparison.OrdinalIgnoreCase)||File.Exists(Path.Combine(root,"verified.json"))){
   var hashes=Core.Json.Deserialize<Dictionary<string,string>>(Core.Read(Path.Combine(root,"verified.json")));if(hashes==null)throw new Exception("稳定版缓存没有校验记录，请重新准备。");foreach(var h in hashes){var f=Safe(root,h.Key);if(!File.Exists(f)||Core.Digest(f)!=h.Value)throw new Exception("稳定版缓存被修改，请重新准备："+h.Key);}
  }
  var files=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);files[p.Proxy]=main;
  // Place vendor libraries in OptiScaler's private search directory, not over native game DLLs.
  foreach(var f in Core.Files(root,12,CancellationToken.None)){
   string rel=f.Substring(root.TrimEnd('\\').Length+1),name=Path.GetFileName(f);
   if(name.Equals("OptiScaler.dll",StringComparison.OrdinalIgnoreCase)||name.Equals("OptiScaler.ini",StringComparison.OrdinalIgnoreCase))continue;
   if(rel.StartsWith("Runtime\\",StringComparison.OrdinalIgnoreCase)||name.StartsWith("dlssnr_",StringComparison.OrdinalIgnoreCase)||name.StartsWith("nvngx_dlssnr",StringComparison.OrdinalIgnoreCase)||name.StartsWith("Lmxxf",StringComparison.OrdinalIgnoreCase))continue;
   if(!(name.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)||name.Equals("fakenvapi.ini",StringComparison.OrdinalIgnoreCase)||rel.IndexOf("license",StringComparison.OrdinalIgnoreCase)>=0))continue;
   string target=rel.StartsWith("OptiScaler\\",StringComparison.OrdinalIgnoreCase)?rel:Path.Combine("OptiScaler",rel);
   // Agility SDK is explicitly required beside the game EXE by stable 0.9.4.
   if(rel.StartsWith("D3D12_Optiscaler\\",StringComparison.OrdinalIgnoreCase))target=rel;
   files[target]=f;
  }
  if(p.Output=="nukems"&&!files.Keys.Any(k=>Path.GetFileName(k).Equals("dlssg_to_fsr3_amd_is_better.dll",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(k).Equals("amdnr_dlssg_fsr3.dll",StringComparison.OrdinalIgnoreCase)))throw new Exception("包内缺少 Nukem 帧生成组件。");
  if(p.Output=="mfg"||p.Output=="combo"){if(!Core.X64(p.Enabler))throw new Exception("请选择 x64 的 DLSS Enabler headless DLL。它不包含在稳定版中。");files[@"OptiScaler\dlss-enabler-headless.dll"]=p.Enabler;}
  if(p.Neural){
   var version=FileVersionInfo.GetVersionInfo(main).ProductVersion??"";
   if(version.IndexOf("amd-presr",StringComparison.OrdinalIgnoreCase)<0&&!HasMarker(main,"AMD-NR"))throw new Exception("这个包未识别为 AMD Pre-SR / AMD-NR 集成版。不能把普通 OptiScaler 与独立 DLSS 5 代理直接叠加。");
   for(int i=1;i<=3;i++){string name="dlssnr_amd_pass"+i+".dll",path=Path.Combine(root,name);if(!File.Exists(path))path=Path.Combine(root,"Runtime",name);if(!Core.X64(path)||!HasMarker(path,"dlssnr_amd"))throw new Exception("缺少匹配的 AMD NR runtime："+name);files[name]=path;}
   string lm=Path.Combine(root,"LmxxfNrRuntime.dll"),pak=Path.Combine(root,"LmxxfNrRuntime.pak");
   if(File.Exists(lm)||File.Exists(pak)){if(!Core.X64(lm)||!File.Exists(pak)||new FileInfo(pak).Length<1024*1024)throw new Exception("lmxxf 运行组件不完整，请重新准备集成包。");files["LmxxfNrRuntime.dll"]=lm;files["LmxxfNrRuntime.pak"]=pak;}
   if(!File.Exists(p.Weights)||new FileInfo(p.Weights).Length<1024*1024)throw new Exception("请选择自己生成的 dlssnr_on_amd_weights.bin，不能使用 DLL 或 Git LFS 指针。");files["dlssnr_on_amd_weights.bin"]=p.Weights;
   string sums=Core.Read(Path.Combine(root,"SHA256SUMS.txt"));foreach(var kv in files.Where(kv=>kv.Key.StartsWith("dlssnr_"))){var match=Regex.Match(sums,@"(?im)^([0-9a-f]{64})\s+\*?(?:Runtime[/\\])?"+Regex.Escape(kv.Key)+@"\s*$");if(match.Success&&Core.Digest(kv.Value)!=match.Groups[1].Value.ToLowerInvariant())throw new Exception("NR runtime 与包内校验记录不匹配："+kv.Key);}
  }
  string config=BuildConfig(Core.Read(ini),p);string prepared=Path.Combine(Home,"prepared",Guid.NewGuid().ToString("N"),"OptiScaler.ini");Directory.CreateDirectory(Path.GetDirectoryName(prepared));File.WriteAllText(prepared,config,new UTF8Encoding(false));files["OptiScaler.ini"]=prepared;
  return files;
 }
 public static string RecordPath(Game g){return Path.Combine(Path.GetDirectoryName(Core.RecordPath(g)),"stack.json");}
 public static StackRecord Record(Game g){string path=RecordPath(g);if(!File.Exists(path))return null;var r=Core.Json.Deserialize<StackRecord>(Core.Read(path));if(!Path.GetFullPath(r.Folder).Equals(Path.GetFullPath(Path.GetDirectoryName(g.Exe)),StringComparison.OrdinalIgnoreCase))throw new Exception("组合记录目录不匹配。");return r;}
 public static string Evidence(Game g){
  if(!Core.X64(g.Exe))throw new Exception("需要 x64 的游戏主程序。");bool input=false,dx12=false,anti=false;
  foreach(var f in Core.Files(g.Root,16,CancellationToken.None)){string n=Path.GetFileName(f).ToLowerInvariant();if(n=="nvngx_dlss.dll"||n=="libxess.dll"||n.Contains("fidelityfx")||n.Contains("fsr2")||n.Contains("fsr3"))input=true;if(n.Contains("d3d12")||n.Contains("dx12"))dx12=true;if(Regex.IsMatch(f,"easyanticheat|battleye|beservice|anticheatexpert|eaanticheat|vgk\\.|faceit",RegexOptions.IgnoreCase))anti=true;}
  if(anti&&!g.OfflineOnly)throw new Exception("发现反作弊组件。只有官方支持的离线模式才能使用，请先在主界面设置游戏模式。");
  if(!input)throw new Exception("未找到 DLSS / FSR2+ / XeSS 输入线索，不能承诺 OptiScaler 可用。需要逐游戏核实接口。");
  return dx12?"发现升频输入和 DX12 线索；待进游戏验证。":"发现升频输入；未确认 DX12，请先核对游戏渲染模式。";
 }
 static void WriteCopy(string source,string target){Directory.CreateDirectory(Path.GetDirectoryName(target));string tmp=target+".dlss-helper-tmp";if(File.Exists(tmp))throw new Exception("发现未完成的临时文件："+tmp);File.Copy(source,tmp);if(File.Exists(target))File.Replace(tmp,target,null);else File.Move(tmp,target);}
 public static void Apply(Game g,StackProfile p,Action<string> report){
  Core.EnsureStopped(g);Evidence(g);if(Core.GetRecord(g)!=null)throw new Exception("此游戏已安装独立 DLSS 5。请先在主界面恢复其文件，然后用 AMD-NR 集成包安装组合；不覆盖已有加载器。");
  ApplyPrepared(g,p,Plan(p),report);
 }
 public static void ApplyPrepared(Game g,StackProfile p,Dictionary<string,string> files,Action<string> report,Dictionary<string,string> preserved=null){
  Core.EnsureStopped(g);if(Core.GetRecord(g)!=null)throw new Exception("请先恢复独立版插件。");
  files=ResolutionPresets.Prepare(g,p,files);
  string folder=Path.GetDirectoryName(g.Exe),rp=RecordPath(g);if(File.Exists(rp+".pending"))throw new Exception("检测到未完成的组合事务，请先恢复已安装文件。");
  var previous=Record(g);if(previous!=null){VerifyCurrent(previous);if(!previous.After.Keys.OrderBy(x=>x).SequenceEqual(files.Keys.OrderBy(x=>x)))throw new Exception("更换安装包／加载名称会改变文件集合，请先恢复旧组合再安装。");}
  foreach(var name in ProxyNames){string f=Path.Combine(folder,name);bool keep=preserved!=null&&preserved.ContainsKey(name)&&!files.ContainsKey(name)&&File.Exists(f)&&Core.Digest(f)==preserved[name];if(File.Exists(f)&&(previous==null||!previous.After.ContainsKey(name))&&!keep&&!Core.IsMicrosoftSystemDll(f))throw new Exception("发现其他加载器："+name+"。请先处理旧模组，不会覆盖。");}
  string tx=Path.Combine(Path.GetDirectoryName(rp),"stack-backup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(tx);
  var record=new StackRecord{Folder=folder,Backup=tx,Game=g.Name,Profile=Clone(p),InstalledAt=DateTime.UtcNow.ToString("o")};
  foreach(var kv in files){string dest=Safe(folder,kv.Key);record.After[kv.Key]=Core.Digest(kv.Value);if(File.Exists(dest)){string hash=Core.Digest(dest);if(previous==null&&hash!=record.After[kv.Key])throw new Exception("目标已有不同文件："+kv.Key+"。已保留原文件。");record.Before[kv.Key]=hash;WriteCopy(dest,Safe(tx,kv.Key));}}
  Core.Save(rp+".pending",record);var written=new List<string>();
  try{foreach(var kv in files){report("写入组合文件："+kv.Key);WriteCopy(kv.Value,Safe(folder,kv.Key));written.Add(kv.Key);if(Core.Digest(Safe(folder,kv.Key))!=record.After[kv.Key])throw new Exception("写入后校验失败："+kv.Key);}
   if(previous!=null){record.Before=previous.Before;record.Backup=previous.Backup;}
   Core.Save(rp,record);File.Delete(rp+".pending");report("组合文件已部署并校验，尚未证明游戏内效果生效。请重启游戏设置输入。");
  }catch{foreach(var key in written){string dest=Safe(folder,key);string backup=Safe(tx,key);if(File.Exists(backup))WriteCopy(backup,dest);else if(File.Exists(dest)&&Core.Digest(dest)==record.After[key])File.Delete(dest);}throw;}
 }
 static void VerifyCurrent(StackRecord r){foreach(var kv in r.After){string path=Safe(r.Folder,kv.Key);if(!File.Exists(path))throw new Exception("已安装文件丢失："+kv.Key);if(!kv.Key.EndsWith(".ini",StringComparison.OrdinalIgnoreCase)&&Core.Digest(path)!=kv.Value)throw new Exception("安装文件已被其他工具修改："+kv.Key+"。已停止自动覆盖／删除。");}}
 public static void Restore(Game g){
  Core.EnsureStopped(g);string rp=RecordPath(g);bool pending=File.Exists(rp+".pending");var r=pending?Core.Json.Deserialize<StackRecord>(Core.Read(rp+".pending")):Record(g);if(r==null)throw new Exception("没有本助手的组合安装记录。");
  if(!Path.GetFullPath(r.Folder).Equals(Path.GetFullPath(Path.GetDirectoryName(g.Exe)),StringComparison.OrdinalIgnoreCase))throw new Exception("恢复目录不匹配。");
  foreach(var kv in r.Before){string backup=Safe(r.Backup,kv.Key);if(!File.Exists(backup)||Core.Digest(backup)!=kv.Value)throw new Exception("备份不完整，停止恢复："+kv.Key);}
  if(!pending)VerifyCurrent(r);
  else foreach(var kv in r.After){string path=Safe(r.Folder,kv.Key);if(File.Exists(path)){string hash=Core.Digest(path);if(hash!=kv.Value&&(!r.Before.ContainsKey(kv.Key)||hash!=r.Before[kv.Key]))throw new Exception("中断期间文件发生变化，需人工检查："+kv.Key);}}
  string archive=Path.Combine(Home,"restored-configs",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));
  foreach(var kv in r.After){string path=Safe(r.Folder,kv.Key);if(File.Exists(path)&&kv.Key.EndsWith(".ini",StringComparison.OrdinalIgnoreCase))WriteCopy(path,Safe(archive,kv.Key));if(r.Before.ContainsKey(kv.Key))WriteCopy(Safe(r.Backup,kv.Key),path);else if(File.Exists(path))File.Delete(path);}
  if(pending)File.Delete(rp+".pending");else File.Delete(rp);
 }
 public static string Describe(StackProfile p){string output=p.Output=="off"?"关闭":p.Output=="fsr3"?"FSR3":p.Output=="fsr4"?"FSR4":p.Output=="nukems"?"Nukem FSR3":p.Output=="mfg"?"Enabler 多帧":"混合多帧";return "神经渲染："+(p.Neural?"AMD NR Pre-SR":"关闭")+" → 升频："+(p.Upscale=="fsr4"?"FSR4":"FSR3.1")+" → 帧生成："+output+(p.Output=="mfg"||p.Output=="combo"?" / "+p.Multiplier+"×":"")+"\r\n\r\n"+Guidance(p);}
 public static string Guidance(StackProfile p){string s="需有 DLSS2+ / FSR2+ / XeSS 输入的 DX12 游戏。游戏内选择受支持升频，Insert 打开 OptiScaler。设置保存后重启，文件部署不等于效果生效。";
  if(p.Output=="off")return s+"\r\n基线方案：帧生成关闭，适合测基础帧率。";
  if(p.Output=="nukems"||p.Output=="mfg"||p.Output=="combo"||p.Input=="dlssg")s+="\r\n此方案要求游戏原生 DLSS 帧生成输入。AMD 上若该开关不可见，需要逐游戏适配，不能保证自动解锁。";
  else if(p.Input=="fsrfg"||p.Input=="fsrfg30")s+="\r\n请在游戏中打开原生 FSR 帧生成。";
  else s+="\r\nOptiFG 用于没有原生帧生成的游戏；缺少独立 HUD 数据，可能出现 UI 残影。";
  if(p.Neural)s+="\r\nAMD NR 集成包必须匹配 runtime；在游戏里验证神经渲染和帧生成均工作。";return s;
 }
}

public class StackForm:Form {
 Color bg=Color.FromArgb(17,23,35),panel=Color.FromArgb(26,35,50);ComboBox gameBox=new ComboBox(),profileBox=new ComboBox(),upscale=new ComboBox(),fg=new ComboBox(),input=new ComboBox(),mult=new ComboBox(),proxy=new ComboBox();TextBox package=new TextBox(),weights=new TextBox(),enabler=new TextBox(),profileName=new TextBox();CheckBox neural=new CheckBox();Label status=new Label(),summary=new Label();StackState state;bool busy,loading;List<Game> games;DataGridView bench=new DataGridView();FlowLayoutPanel buttons=new FlowLayoutPanel();
 public StackForm(List<Game> available,Game selected){
  games=available;Text="组合实验室 · OptiScaler / 神经渲染 / 帧生成";ClientSize=new Size(1060,780);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",10);BackColor=bg;ForeColor=Color.White;
  try{state=Core.Json.Deserialize<StackState>(Core.Read(StackEngine.StatePath))??new StackState();}catch{state=new StackState();}
  var tabs=new TabControl{Dock=DockStyle.Fill};Controls.Add(tabs);var config=new TabPage("安装与组合"){BackColor=bg,ForeColor=Color.White};var compare=new TabPage("方案对比"){BackColor=bg};var results=new TabPage("实测记录"){BackColor=bg};tabs.TabPages.AddRange(new[]{config,compare,results});
  LabelAt(config,"目标游戏",20,22);gameBox.SetBounds(125,18,880,30);gameBox.DropDownStyle=ComboBoxStyle.DropDownList;StyleCombo(gameBox);gameBox.Items.Add("暂不选游戏（只管理组合）");foreach(var g in games)gameBox.Items.Add(g);gameBox.SelectedIndex=selected==null?0:games.IndexOf(selected)+1;config.Controls.Add(gameBox);
  LabelAt(config,"已存组合",20,66);profileBox.SetBounds(125,62,420,30);profileBox.DropDownStyle=ComboBoxStyle.DropDownList;StyleCombo(profileBox);config.Controls.Add(profileBox);var save=Btn("保存为组合",delegate{SaveProfile();});save.SetBounds(820,62,185,32);config.Controls.Add(save);profileName.SetBounds(560,62,245,30);config.Controls.Add(profileName);
  LabelAt(config,"OptiScaler 包",20,112);package.SetBounds(125,108,590,30);config.Controls.Add(package);var choose=Btn("选择目录",delegate{using(var d=new FolderBrowserDialog()){if(d.ShowDialog(this)==DialogResult.OK){package.Text=d.SelectedPath;RefreshSummary();}}});choose.SetBounds(725,106,125,34);config.Controls.Add(choose);var fetch=Btn("官方来源",delegate{Open("https://github.com/optiscaler/OptiScaler/releases");});fetch.SetBounds(860,106,145,34);config.Controls.Add(fetch);
  LabelAt(config,"升频输出",20,160);Combo(config,upscale,125,156,290,new[]{"FSR4（RX 9070 优先测试）","FSR3.1（对照／兼容）"});LabelAt(config,"加载名称",455,160);Combo(config,proxy,560,156,220,new[]{"dxgi.dll","winmm.dll","version.dll","d3d12.dll"});
  neural.Text="启用 DLSS 5 神经渲染（需 AMD-NR / AMD Pre-SR 集成包）";neural.SetBounds(125,200,820,30);config.Controls.Add(neural);
  LabelAt(config,"模型权重",20,244);weights.SetBounds(125,240,720,30);config.Controls.Add(weights);var w=Btn("选择 BIN",delegate{weights.Text=Pick("模型权重|*.bin",weights.Text);});w.SetBounds(860,239,145,34);config.Controls.Add(w);
  LabelAt(config,"帧生成输出",20,289);Combo(config,fg,125,285,430,new[]{"关闭（先测基线）","FSR3 帧生成","FSR4 帧生成（RDNA4）","Nukem：DLSSG → FSR3","DLSS Enabler：FSR 多帧（实验）","FFX + Enabler 混合多帧（实验）"});LabelAt(config,"输出倍率",590,289);Combo(config,mult,695,285,310,new[]{"2×","3×","4×","5×","6×"});
  LabelAt(config,"游戏输入",20,334);Combo(config,input,125,330,880,new[]{"原生 DLSS 帧生成 / Streamline 2+","原生 FSR3.1 帧生成","原生 FSR3.0 帧生成","没有原生帧生成：OptiFG（实验）"});
  LabelAt(config,"多帧组件",20,379);enabler.SetBounds(125,375,720,30);config.Controls.Add(enabler);var e=Btn("选择 DLL",delegate{enabler.Text=Pick("DLSS Enabler headless|*.dll",enabler.Text);});e.SetBounds(860,374,145,34);config.Controls.Add(e);
  summary.SetBounds(20,425,985,135);summary.ForeColor=Color.FromArgb(176,202,224);config.Controls.Add(summary);
  buttons.SetBounds(16,572,1000,94);config.Controls.Add(buttons);
  buttons.Controls.Add(Btn("只检查／预览",async delegate{var p=Current();await Run(()=>{var files=StackEngine.Plan(p);Status("方案检查通过："+files.Count+" 个部署文件。未修改游戏；兼容性仍需实测。");});}));
  buttons.Controls.Add(Btn("应用到所选游戏",async delegate{Game g=Game();if(g==null)return;var p=Current();await Run(()=>StackEngine.Apply(g,p,Status));}));
  buttons.Controls.Add(Btn("恢复组合文件",async delegate{Game g=Game();if(g==null)return;await Run(()=>{StackEngine.Restore(g);Status("组合文件已恢复；其他工具的模组不会删除。在线前请恢复官方反作弊设置。");});}));
  buttons.Controls.Add(Btn("打开配置目录",delegate{Directory.CreateDirectory(StackEngine.Home);Process.Start("explorer.exe",StackEngine.Home);}));
  buttons.Controls.Add(Btn("AMD-NR 包来源",delegate{Open("https://github.com/3zwr1/AMD-NR---OptiScaler/releases");}));
  buttons.Controls.Add(Btn("多帧组件来源",delegate{Open("https://github.com/artur-graniszewski/DLSS-Enabler/releases");}));
  status.SetBounds(20,678,985,58);status.ForeColor=Color.FromArgb(63,219,173);config.Controls.Add(status);Status("先保存、检查组合；只有“应用到所选游戏”才会写入游戏。配置更改在重启游戏后生效。");
  var explanation=new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,BackColor=panel,ForeColor=Color.White,Font=new Font(Font.FontFamily,11),Text=Comparison};compare.Controls.Add(explanation);
  bench.Dock=DockStyle.Fill;bench.ReadOnly=true;bench.AllowUserToAddRows=false;bench.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells;bench.BackgroundColor=panel;bench.DefaultCellStyle.ForeColor=Color.Black;results.Controls.Add(bench);var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=52};results.Controls.Add(bar);bar.Controls.Add(Btn("记录一次实测",delegate{AddResult();}));bar.Controls.Add(Btn("导出记录 JSON",delegate{ExportResults();}));bar.Controls.Add(new Label{Text="手动记录，不伪造帧率；相同场景、分辨率、画质和 NR 设置才可比较。",ForeColor=Color.White,AutoSize=true,Margin=new Padding(8,14,0,0)});
  foreach(var c in new[]{upscale,fg,input,mult,proxy})c.SelectedIndexChanged+=delegate{RefreshSummary();};neural.CheckedChanged+=delegate{RefreshSummary();};profileBox.SelectedIndexChanged+=delegate{if(!loading&&profileBox.SelectedIndex>=0)LoadProfile(state.Profiles[profileBox.SelectedIndex]);};
  if(state.Profiles.Count==0){state.Profiles.Add(new StackProfile{Name="A · FSR4 升频基线",Package=StackEngine.StableFolder});state.Profiles.Add(new StackProfile{Name="B · FSR4 + FSR3 FG",Package=StackEngine.StableFolder,Output="fsr3"});state.Profiles.Add(new StackProfile{Name="C · FSR4 + FSR4 FG",Package=StackEngine.StableFolder,Output="fsr4"});state.Profiles.Add(new StackProfile{Name="D · Nukem FSR3 对照",Package=StackEngine.StableFolder,Output="nukems"});}
  FillProfiles();profileBox.SelectedIndex=0;LoadResults();FormClosing+=delegate(object sender,FormClosingEventArgs ev){if(busy){ev.Cancel=true;Status("正在处理，请等待操作完成后关闭。");}};
 }
 void LabelAt(Control p,string t,int x,int y){p.Controls.Add(new Label{Text=t,Location=new Point(x,y),AutoSize=true,ForeColor=Color.White});}
 void StyleCombo(ComboBox c){c.DrawMode=DrawMode.OwnerDrawFixed;c.ItemHeight=24;c.BackColor=panel;c.ForeColor=Color.White;c.DrawItem+=delegate(object sender,DrawItemEventArgs e){e.DrawBackground();string text=e.Index>=0&&e.Index<c.Items.Count?c.GetItemText(c.Items[e.Index]):c.Text;TextRenderer.DrawText(e.Graphics,text,c.Font,e.Bounds,Color.White,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);e.DrawFocusRectangle();};}
 void Combo(Control parent,ComboBox c,int x,int y,int w,string[] items){c.SetBounds(x,y,w,32);StyleCombo(c);c.DropDownStyle=ComboBoxStyle.DropDownList;c.Items.AddRange(items);c.SelectedIndex=0;parent.Controls.Add(c);}
 Button Btn(string t,EventHandler h){var b=new Button{Text=t,Size=new Size(190,36),FlatStyle=FlatStyle.Flat,BackColor=panel,ForeColor=Color.White,Margin=new Padding(4)};b.Click+=delegate(object s,EventArgs e){try{h(s,e);}catch(Exception ex){MessageBox.Show(this,ex.Message,"组合实验室");}};return b;}
 string Pick(string filter,string current){using(var f=new OpenFileDialog{Filter=filter})return f.ShowDialog(this)==DialogResult.OK?f.FileName:current;}
 void Open(string url){Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
 Game Game(){var g=gameBox.SelectedItem as Game;if(g==null)MessageBox.Show(this,"先选择游戏。当前可以保存组合和准备组件，不会修改游戏。");return g;}
 void Status(string message){if(InvokeRequired){BeginInvoke((Action)(()=>Status(message)));return;}status.Text=message;}
 async Task Run(Action a){if(busy)return;busy=true;buttons.Enabled=false;try{await Task.Run(a);}catch(Exception ex){Status("未完成："+ex.Message);MessageBox.Show(this,ex.Message,"组合实验室");}finally{busy=false;buttons.Enabled=true;}}
 StackProfile Current(){return new StackProfile{Name=String.IsNullOrWhiteSpace(profileName.Text)?"我的组合":profileName.Text.Trim(),Package=package.Text.Trim(),Weights=weights.Text.Trim(),Enabler=enabler.Text.Trim(),Neural=neural.Checked,Proxy=proxy.Text,Upscale=upscale.SelectedIndex==0?"fsr4":"fsr3",Output=new[]{"off","fsr3","fsr4","nukems","mfg","combo"}[fg.SelectedIndex],Input=new[]{"dlssg","fsrfg","fsrfg30","upscaler"}[input.SelectedIndex],Multiplier=mult.SelectedIndex+2};}
 void RefreshSummary(){if(loading)return;summary.Text=StackEngine.Describe(Current());}
 void FillProfiles(){loading=true;profileBox.Items.Clear();foreach(var p in state.Profiles)profileBox.Items.Add(p.Name);loading=false;}
 void LoadProfile(StackProfile p){loading=true;profileName.Text=p.Name;package.Text=p.Package;weights.Text=p.Weights;enabler.Text=p.Enabler;neural.Checked=p.Neural;proxy.SelectedItem=p.Proxy;upscale.SelectedIndex=p.Upscale=="fsr4"?0:1;fg.SelectedIndex=Math.Max(0,Array.IndexOf(new[]{"off","fsr3","fsr4","nukems","mfg","combo"},p.Output));input.SelectedIndex=Math.Max(0,Array.IndexOf(new[]{"dlssg","fsrfg","fsrfg30","upscaler"},p.Input));mult.SelectedIndex=Math.Max(0,Math.Min(4,p.Multiplier-2));loading=false;RefreshSummary();}
 void SaveProfile(){var p=Current();int i=state.Profiles.FindIndex(x=>x.Name==p.Name);if(i<0){state.Profiles.Add(p);i=state.Profiles.Count-1;}else state.Profiles[i]=p;Core.Save(StackEngine.StatePath,state);FillProfiles();profileBox.SelectedIndex=i;Status("组合已保存，未写入游戏。");}
 string ResultsPath{get{return Path.Combine(StackEngine.Home,"benchmarks.json");}}
 List<BenchEntry> Results(){try{return Core.Json.Deserialize<List<BenchEntry>>(Core.Read(ResultsPath))??new List<BenchEntry>();}catch{return new List<BenchEntry>();}}
 void LoadResults(){bench.DataSource=null;var table=new System.Data.DataTable();foreach(var n in new[]{"时间","游戏","组合","场景／设置","基础 FPS","显示 FPS","延迟 ms","画质／残影","稳定性","备注"})table.Columns.Add(n);foreach(var r in Results())table.Rows.Add(r.Time,r.Game,r.Profile,r.Scene,r.BaseFPS,r.DisplayFPS,r.Latency,r.Quality,r.Stability,r.Notes);bench.DataSource=table;}
 void AddResult(){Game g=Game();if(g==null)return;using(var f=new Form{Text="手动录入同场景测试（未知数值留空）",ClientSize=new Size(680,510),StartPosition=FormStartPosition.CenterParent,Font=Font}){string[] labels={"组合名","场景／分辨率／画质／NR设置","基础 FPS（关闭 FG 测得）","显示 FPS（开启 FG 测得）","延迟 ms（仅填写实际测量）","画质／拖影／HUD","稳定性／崩溃","备注／采集工具"};var boxes=new List<TextBox>();for(int i=0;i<labels.Length;i++){f.Controls.Add(new Label{Text=labels[i],Location=new Point(15,18+i*52),Size=new Size(270,25)});var b=new TextBox{Location=new Point(285,15+i*52),Width=375};if(i==0)b.Text=profileName.Text;boxes.Add(b);f.Controls.Add(b);}var save=new Button{Text="保存实测记录",Location=new Point(285,445),Size=new Size(200,38)};save.Click+=delegate{for(int i=2;i<=4;i++){double value;if(boxes[i].Text.Length>0&&(!Double.TryParse(boxes[i].Text,out value)||value<=0||Double.IsInfinity(value)||Double.IsNaN(value))){MessageBox.Show(f,"FPS 和延迟请填正数，未知留空。");return;}}if(String.IsNullOrWhiteSpace(boxes[1].Text)){MessageBox.Show(f,"请写明测试场景与画质，避免不可比较的数据。");return;}var all=Results();all.Add(new BenchEntry{Time=DateTime.Now.ToString("s"),Game=g.Name,Profile=boxes[0].Text,Scene=boxes[1].Text,BaseFPS=boxes[2].Text,DisplayFPS=boxes[3].Text,Latency=boxes[4].Text,Quality=boxes[5].Text,Stability=boxes[6].Text,Notes=boxes[7].Text});Core.Save(ResultsPath,all);f.Close();LoadResults();};f.Controls.Add(save);f.ShowDialog(this);}}
 void ExportResults(){using(var d=new SaveFileDialog{Filter="JSON|*.json",FileName="帧生成实测记录.json"})if(d.ShowDialog(this)==DialogResult.OK)Core.Save(d.FileName,Results());}
 const string Comparison="先明确：以下是方案特性，不是你这台电脑的实测排名。\r\n\r\n"+
 "1. FSR3 / FSR4 帧生成\r\n游戏原生帧生成接口优先，能提供更完整的运动和 HUD 数据。RX 9070 可测试 FSR4 FG，和 FSR3 FG 做同场景对照。画质、延迟、兼容性仍取决于游戏。\r\n\r\n"+
 "2. Nukem：DLSSG → FSR3\r\n借用游戏的 DLSS 帧生成入口，实际执行 FSR3 插帧。这不是 NVIDIA 原生 DLSS 多帧生成。适合有 DLSSG 输入、希望在 AMD 上启用帧生成的游戏。\r\n\r\n"+
 "3. DLSS Enabler 多帧 / FFX + Enabler\r\n实验组合可配置更多生成帧。需要支持该接口的新版 OptiScaler 或 AMD-NR 包，以及匹配的 headless DLL。稳定 0.9.4 不支持这个接口，助手会明确阻止错误配置。更高显示 FPS 不能说明操作延迟更低。\r\n\r\n"+
 "4. OptiFG\r\n没有原生帧生成时从升频接口取数据。仍需游戏有受支持升频接口；缺少 HUDless 数据时容易影响文字和准星。它不是任意游戏万能补丁。\r\n\r\n"+
 "5. DLSS 5 神经渲染 + OptiScaler\r\n使用 AMD-NR / AMD Pre-SR 专用集成包，先在低分辨率上进行神经渲染，再升频、帧生成。独立 DLSS 5 安装必须先恢复，不能直接用两个 dxgi/version 代理互相覆盖。\r\n\r\n"+
 "建议测试顺序\r\nA：FSR4 升频，FG 关闭、NR 关闭。\r\nB：同设置 + FSR3 FG。\r\nC：同设置 + FSR4 FG。\r\nD：同设置 + Nukem FSR3；再测实验多帧。\r\n最后在胜出方案上加入 NR，重新记录基础帧率和延迟。\r\n每次使用同一存档、路线、分辨率和画质，暖机后至少跑三次。基础 FPS 要关闭 FG 实测，不能拿显示 FPS 除以倍率当作测量值。鼠标延迟、准星/HUD 残影和崩溃分别记录。\r\n\r\n"+
 "来源\r\nhttps://github.com/optiscaler/OptiScaler/wiki/Frame-Generation-Options\r\nhttps://github.com/optiscaler/OptiScaler/releases/tag/v0.9.4\r\nhttps://github.com/3zwr1/AMD-NR---OptiScaler\r\nhttps://github.com/artur-graniszewski/DLSS-Enabler\r\n\r\n本助手是自建工具，不是 OptiScaler 官方管理器。";
}

