using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

public class ResolutionChoice {
 public string Mode,DisplayDevice;
}
public class PhysicalDisplay {
 public string Device; public int Width,Height; public bool Primary;
 public override string ToString(){return Device+" · "+Width+"×"+Height+(Primary?" · 主屏幕":"");}
}
public static class ResolutionPresets {
 public static string StorePath=Path.Combine(Core.Data,"resolution-presets.json");
 public static readonly string[] Modes={"质量","平衡","性能","超级性能"};
 static readonly object Gate=new object();
 public static double Ratio(string mode){switch(mode){case "质量":return 1.5;case "平衡":return 1.7;case "性能":return 2.0;case "超级性能":return 3.0;default:throw new Exception("请选择质量、平衡、性能或超级性能。");}}
 public static Size InputSize(int width,int height,string mode){if(width<64||height<64||width>32768||height>32768)throw new Exception("输出分辨率无效。");double ratio=Ratio(mode);return new Size(Math.Max(64,(int)Math.Floor(width/ratio+0.5)),Math.Max(64,(int)Math.Floor(height/ratio+0.5)));}
 public static string Key(Game g){return Path.GetFullPath(Path.GetDirectoryName(LegacyBridge.Target(g).Exe)).TrimEnd('\\').ToLowerInvariant();}
 static Dictionary<string,ResolutionChoice> Load(){if(!File.Exists(StorePath))return new Dictionary<string,ResolutionChoice>();return Core.Json.Deserialize<Dictionary<string,ResolutionChoice>>(File.ReadAllText(StorePath))??new Dictionary<string,ResolutionChoice>();}
 public static ResolutionChoice Saved(Game g){lock(Gate){ResolutionChoice value;return Load().TryGetValue(Key(g),out value)?value:null;}}
 public static string BridgeConfig(string ini,string mode){return StackEngine.Patch(ini,"amd-nr","Scale",(1.0/Ratio(mode)).ToString("0.########",CultureInfo.InvariantCulture));}
 public static string UpscaleConfig(string ini,string mode){
  ini=StackEngine.Patch(ini,"UpscaleRatio","UpscaleRatioOverrideEnabled","true");
  ini=StackEngine.Patch(ini,"UpscaleRatio","UpscaleRatioOverrideValue",Ratio(mode).ToString("0.0",CultureInfo.InvariantCulture));
  // OptiScaler's own menu makes its global and per-quality overrides mutually exclusive.
  if(StackEngine.ReadIni(ini,"QualityOverrides","QualityRatioOverrideEnabled")!=null)ini=StackEngine.Patch(ini,"QualityOverrides","QualityRatioOverrideEnabled","false");
  return ini;
 }
 public static Dictionary<string,string> Prepare(Game g,StackProfile profile,Dictionary<string,string> files){
  var saved=Saved(g);if(saved==null)return files;
  bool bridge=profile.Input=="reshade32"||profile.Input=="reshade64";string name=bridge?"amd-nr.ini":"OptiScaler.ini",source;
  if(!files.TryGetValue(name,out source))return files;
  string text=File.ReadAllText(source),changed=bridge?BridgeConfig(text,saved.Mode):UpscaleConfig(text,saved.Mode);
  string stage=Path.Combine(Core.Data,"resolution-prepared",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
  string prepared=Path.Combine(stage,name);File.WriteAllText(prepared,changed,new UTF8Encoding(false));
  var copy=new Dictionary<string,string>(files,StringComparer.OrdinalIgnoreCase);copy[name]=prepared;return copy;
 }
 static void WriteText(string path,string text){string tmp=path+".resolution-tmp";if(File.Exists(tmp))throw new Exception("发现未完成的分辨率设置临时文件，请保留现场："+tmp);Directory.CreateDirectory(Path.GetDirectoryName(path));try{File.WriteAllText(tmp,text,new UTF8Encoding(false));if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}finally{if(File.Exists(tmp))File.Delete(tmp);}}
 public static string Set(Game game,string mode,string device){lock(Gate){
  Ratio(mode);var target=LegacyBridge.Target(game);
  foreach(var g in new[]{game,target})if(File.Exists(Core.RecordPath(g)+".pending")||File.Exists(StackEngine.RecordPath(g)+".pending"))throw new Exception("安装事务尚未完成，请先恢复文件再保存档位。");
  var prefs=Load();prefs[Key(game)]=new ResolutionChoice{Mode=mode,DisplayDevice=device};
  var record=StackEngine.Record(target);if(record==null&&!target.Exe.Equals(game.Exe,StringComparison.OrdinalIgnoreCase)){target=game;record=StackEngine.Record(target);}
  if(record==null){Core.Save(StorePath,prefs);return Core.GetRecord(game)!=null?"档位已保存。Daniel 独立版没有已验证的外部输入分辨率接口，请在游戏中选择对应 FSR 档位。":"档位已保存，未安装插件。以后安装受支持的桥接或升频组合时自动使用；当前游戏文件未修改。";}
  Core.EnsureStopped(game);Core.EnsureStopped(target);
  bool bridge=record.Profile!=null&&(record.Profile.Input=="reshade32"||record.Profile.Input=="reshade64");
  string name=bridge?"amd-nr.ini":"OptiScaler.ini",path=StackEngine.Safe(record.Folder,name);
  if(!record.After.ContainsKey(name)||!File.Exists(path))throw new Exception("没有本助手管理的输入分辨率配置，已保留原文件。");
  foreach(var kv in record.After.Where(k=>!k.Key.EndsWith(".ini",StringComparison.OrdinalIgnoreCase))){string current=StackEngine.Safe(record.Folder,kv.Key);if(!File.Exists(current)||Core.Digest(current)!=kv.Value)throw new Exception("安装组件已经变化，请先检查："+kv.Key);}
  string original=File.ReadAllText(path),changed=bridge?BridgeConfig(original,mode):UpscaleConfig(original,mode),rp=StackEngine.RecordPath(target);
  string oldRecord=File.ReadAllText(rp),oldPrefs=File.Exists(StorePath)?File.ReadAllText(StorePath):null;
  string snapshot=Path.Combine(Core.Data,"settings-backups","resolution-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(snapshot);File.Copy(path,Path.Combine(snapshot,name));File.WriteAllText(Path.Combine(snapshot,"stack-before.json"),oldRecord);
  try{WriteText(path,changed);record.After[name]=Core.Digest(path);Core.Save(rp,record);Core.Save(StorePath,prefs);}
  catch{WriteText(path,original);WriteText(rp,oldRecord);if(oldPrefs!=null)WriteText(StorePath,oldPrefs);else if(File.Exists(StorePath))File.Delete(StorePath);throw;}
  return "已保存并应用“"+mode+"”，下次启动生效。"+(bridge?"只调整神经处理输入，不改变游戏本身的渲染分辨率。":"已设置升频输入比例；游戏需启用受支持的 DLSS / FSR / XeSS 输入。");
 }}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct DisplayMode {
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Device;
  public ushort Spec,Driver,Size,Extra;public uint Fields;public int X,Y;public uint Orientation,FixedOutput;
  public short Colour,Duplex,YResolution,TT,Collate;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Form;
  public ushort Pixels;public uint Bits,Width,Height,Flags,Frequency,ICMMethod,ICMIntent,Media,Dither,Reserved1,Reserved2,PanningWidth,PanningHeight;
 }
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool EnumDisplaySettings(string device,int mode,ref DisplayMode settings);
 public static List<PhysicalDisplay> Displays(){var result=new List<PhysicalDisplay>();foreach(var screen in Screen.AllScreens){var mode=new DisplayMode{Size=(ushort)Marshal.SizeOf(typeof(DisplayMode))};if(EnumDisplaySettings(screen.DeviceName,-1,ref mode)&&mode.Width>=64&&mode.Height>=64)result.Add(new PhysicalDisplay{Device=screen.DeviceName,Width=(int)mode.Width,Height=(int)mode.Height,Primary=screen.Primary});}if(result.Count==0)throw new Exception("未读到屏幕物理分辨率，请检查显示器连接。");return result;}
 public static string Backend(Game g){var r=StackEngine.Record(LegacyBridge.Target(g))??StackEngine.Record(g);if(r!=null&&r.Profile!=null&&(r.Profile.Input=="reshade32"||r.Profile.Input=="reshade64"))return "ReShade 神经桥接：档位控制神经网络输入尺寸，游戏原始渲染尺寸保持原设置。";if(r!=null)return "OptiScaler：档位控制升频输入比例，神经处理在该输入基础上运行；其独立 NR 缩放设置保留。";return Core.GetRecord(g)!=null?"Daniel 独立版：先保存档位，再在游戏内选择对应 FSR 档位；这里不能强制修改原生菜单。":"尚未安装受支持的组件：可先保存档位；选择档位不会安装或开启插件。";}
}

public class ResolutionEvidence {
 public string Source,Written,Summary;public int OutputWidth,OutputHeight,InputWidth,InputHeight,RequestedPasses,AcceptedPasses,FrameSamples,FailedFrameSamples;
}
public static class ResolutionLogs {
 public static ResolutionEvidence Parse(string text){
  var evidence=new ResolutionEvidence();var rasters=Regex.Matches(text,@"raster: back buffer (\d+)x(\d+)[^\r\n]*network at (\d+)x(\d+)",RegexOptions.IgnoreCase);
  if(rasters.Count>0){var match=rasters[rasters.Count-1];evidence.OutputWidth=Int32.Parse(match.Groups[1].Value);evidence.OutputHeight=Int32.Parse(match.Groups[2].Value);evidence.InputWidth=Int32.Parse(match.Groups[3].Value);evidence.InputHeight=Int32.Parse(match.Groups[4].Value);}
  var passes=Regex.Matches(text,@"(\d+) asked(?: for)?, (\d+) accepted");if(passes.Count>0){var match=passes[passes.Count-1];evidence.RequestedPasses=Int32.Parse(match.Groups[1].Value);evidence.AcceptedPasses=Int32.Parse(match.Groups[2].Value);}
  var frames=Regex.Matches(text,@"engine_frame=\d+ result=(\d+)");evidence.FrameSamples=frames.Count;evidence.FailedFrameSamples=frames.Cast<Match>().Count(m=>m.Groups[1].Value!="1");
  bool error=Regex.IsMatch(text,@"DXGI_ERROR_DEVICE_(?:HUNG|REMOVED)|serial chain incomplete|final neural pass did not complete|network skipped",RegexOptions.IgnoreCase);
  evidence.Summary=error?"日志含设备异常、未完成处理或跳过，请检查原始日志。":evidence.FrameSamples>0?"记录到 "+evidence.FrameSamples+" 个抽样处理结果，非成功 "+evidence.FailedFrameSamples+" 个；抽样不能证明所有帧稳定。":"未读到可识别的处理帧；不能据此确认已开启。";
  return evidence;
 }
 static string Tail(string path){using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){if(f.Length>2*1024*1024)f.Seek(-2*1024*1024,SeekOrigin.End);using(var reader=new StreamReader(f))return reader.ReadToEnd();}}
 public static ResolutionEvidence Read(Game g){
  var folders=new[]{Path.GetDirectoryName(LegacyBridge.Target(g).Exe),Path.GetDirectoryName(g.Exe)}.Distinct(StringComparer.OrdinalIgnoreCase);
  var candidates=new List<ResolutionEvidence>();foreach(var folder in folders)foreach(string name in new[]{"amd-nr-x86-host.log","amd-nr.log","amd_presr.log","OptiScaler.log"}){string path=Path.Combine(folder,name);if(!File.Exists(path))continue;try{var item=Parse(Tail(path));item.Source=path;item.Written=File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss");candidates.Add(item);}catch(IOException){}}
  if(candidates.Count==0)return new ResolutionEvidence{Summary="未找到当前游戏目录的日志。请运行游戏并进入场景后刷新；不会启动或重新安装游戏。"};
  // Prefer the latest session; never use a stale resolution merely because it is parseable.
  var latest=candidates.OrderByDescending(c=>c.Written,StringComparer.Ordinal).ThenByDescending(c=>c.OutputWidth>0).First();return latest;
 }
 public static string Format(ResolutionEvidence evidence,PhysicalDisplay screen,string mode){
  if(evidence.Source==null)return evidence.Summary;
  var text=new StringBuilder("最近保存的日志（不代表此刻正在运行）\r\n");text.AppendLine(evidence.Source);text.AppendLine("更新时间："+evidence.Written);
  if(evidence.OutputWidth>0){text.AppendLine("实际输出："+evidence.OutputWidth+"×"+evidence.OutputHeight+"；实际神经输入："+evidence.InputWidth+"×"+evidence.InputHeight);
   if(screen!=null&&(screen.Width!=evidence.OutputWidth||screen.Height!=evidence.OutputHeight))text.AppendLine("游戏输出与所选屏幕不同：窗口或游戏内分辨率会改变实际输入尺寸。");
   if(mode!=null){var expected=ResolutionPresets.InputSize(evidence.OutputWidth,evidence.OutputHeight,mode);text.AppendLine("“"+mode+"”在该游戏输出下预计输入："+expected.Width+"×"+expected.Height+"（以重启后的日志为准）。");}
  }else text.AppendLine("此日志没有可识别的输入 / 输出尺寸，尺寸尚未验证。");
  if(evidence.RequestedPasses>0)text.AppendLine("神经处理次数：请求 "+evidence.RequestedPasses+"，接受 "+evidence.AcceptedPasses);
  text.Append(evidence.Summary);return text.ToString();
 }
}

public class ResolutionForm:Form {
 readonly Game game;readonly ComboBox screens=new ComboBox();readonly Label intro=new Label(),state=new Label();readonly RadioButton[] modes=new RadioButton[4];readonly TextBox logs=new TextBox();readonly Button apply=new Button();bool busy;
 public ResolutionForm(Game g){
  game=g;Text="输入分辨率 · "+g.Name;ClientSize=new Size(820,650);MinimumSize=new Size(836,689);StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",10);BackColor=Color.FromArgb(17,23,35);ForeColor=Color.White;AutoScaleMode=AutoScaleMode.Dpi;
  intro.SetBounds(20,16,780,66);intro.Text=ResolutionPresets.Backend(g)+"\r\n按屏幕物理像素预览；游戏输出需在游戏中设置，窗口模式以实际输出为准。";Controls.Add(intro);
  screens.SetBounds(20,90,580,30);screens.DropDownStyle=ComboBoxStyle.DropDownList;screens.BackColor=Color.FromArgb(26,35,50);screens.ForeColor=Color.White;screens.Name="ResolutionDisplay";Controls.Add(screens);
  screens.DrawMode=DrawMode.OwnerDrawFixed;screens.ItemHeight=24;screens.DrawItem+=delegate(object sender,DrawItemEventArgs e){e.DrawBackground();if(e.Index>=0)TextRenderer.DrawText(e.Graphics,screens.Items[e.Index].ToString(),Font,e.Bounds,Color.White,Color.FromArgb(26,35,50),TextFormatFlags.VerticalCenter|TextFormatFlags.Left);e.DrawFocusRectangle();};
  var refresh=new Button{Text="刷新屏幕 / 日志",Location=new Point(615,87),Size=new Size(185,36)};refresh.Click+=delegate{if(!busy)RefreshScreens();};Controls.Add(refresh);
  var options=new Panel{Location=new Point(20,137),Size=new Size(780,164)};Controls.Add(options);
  for(int i=0;i<4;i++){int index=i;var radio=new RadioButton{Location=new Point(10,i*40),Size=new Size(760,36),Name="ResolutionMode"+i};modes[i]=radio;options.Controls.Add(radio);radio.CheckedChanged+=delegate{if(modes[index].Checked){state.Text="已选择“"+ResolutionPresets.Modes[index]+"”。点击保存后应用，下次启动游戏核对实际尺寸。";RefreshEvidence();}};}
  apply.Text="保存档位 / 应用到所选游戏";apply.Name="ResolutionApply";apply.SetBounds(20,307,350,38);apply.Click+=delegate{if(busy||Mode()==null)return;busy=true;apply.Enabled=false;screens.Enabled=false;foreach(var radio in modes)radio.Enabled=false;try{var display=Display();state.Text=ResolutionPresets.Set(game,Mode(),display.Device);}catch(Exception ex){state.Text="未应用："+ex.Message;}finally{busy=false;apply.Enabled=true;screens.Enabled=true;foreach(var radio in modes)radio.Enabled=true;RefreshEvidence();}};Controls.Add(apply);
  state.SetBounds(20,357,780,64);Controls.Add(state);
  logs.SetBounds(20,426,780,202);logs.Multiline=true;logs.ReadOnly=true;logs.ScrollBars=ScrollBars.Vertical;logs.BackColor=Color.FromArgb(26,35,50);logs.ForeColor=Color.White;logs.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top|AnchorStyles.Bottom;logs.Name="ResolutionLogSummary";Controls.Add(logs);
  screens.SelectedIndexChanged+=delegate{UpdateSizes();};RefreshScreens();var saved=ResolutionPresets.Saved(game);if(saved!=null){for(int i=0;i<4;i++)modes[i].Checked=ResolutionPresets.Modes[i]==saved.Mode;state.Text="此游戏已保存：“"+saved.Mode+"”。更改后请重新启动游戏核对日志。";}else{apply.Enabled=false;state.Text="请选一个档位。四档是助手的输入比例预设，不代表每个游戏都已支持自动修改。";}RefreshEvidence();
 }
 string Mode(){for(int i=0;i<4;i++)if(modes[i].Checked)return ResolutionPresets.Modes[i];return null;}
 PhysicalDisplay Display(){return (PhysicalDisplay)screens.SelectedItem;}
 void RefreshScreens(){var selected=screens.SelectedItem as PhysicalDisplay;var saved=ResolutionPresets.Saved(game);string device=selected!=null?selected.Device:saved==null?null:saved.DisplayDevice;var displays=ResolutionPresets.Displays();screens.Items.Clear();foreach(var screen in displays)screens.Items.Add(screen);var pick=displays.FirstOrDefault(s=>s.Device==device)??displays.FirstOrDefault(s=>s.Primary)??displays[0];screens.SelectedItem=pick;UpdateSizes();}
 void UpdateSizes(){var screen=Display();if(screen==null)return;intro.Text=ResolutionPresets.Backend(game)+"\r\n当前屏幕："+screen+"（物理像素）；窗口模式以游戏实际输出为准。";for(int i=0;i<4;i++){var size=ResolutionPresets.InputSize(screen.Width,screen.Height,ResolutionPresets.Modes[i]);modes[i].Text=ResolutionPresets.Modes[i]+"   ·   "+size.Width+"×"+size.Height+" → "+screen.Width+"×"+screen.Height+"   ·   宽高约 "+(100/ResolutionPresets.Ratio(ResolutionPresets.Modes[i])).ToString("0.#",CultureInfo.InvariantCulture)+"%";}RefreshEvidence();}
 void RefreshEvidence(){logs.Text=ResolutionLogs.Format(ResolutionLogs.Read(game),Display(),Mode());if(!busy)apply.Enabled=Mode()!=null;}
}
