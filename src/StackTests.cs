using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public static class StackTests {
 static int count;
 static void Assert(bool x,string text){if(!x)throw new Exception(text);count++;}
 static void Rejected(Action action,string name){bool failed=false;try{action();}catch{failed=true;}Assert(failed,name);}
 public static void Run(){
  Program.Testing=true;count=0;
  string root=Path.Combine(Path.GetTempPath(),"StackTests-"+Guid.NewGuid().ToString("N")),pkg=Path.Combine(root,"package"),game=Path.Combine(root,"game");Directory.CreateDirectory(pkg);Directory.CreateDirectory(game);
  byte[] bytes=new byte[256];bytes[0]=0x4d;bytes[1]=0x5a;bytes[0x3c]=0x80;bytes[0x80]=0x50;bytes[0x81]=0x45;bytes[0x84]=0x64;bytes[0x85]=0x86;
  File.WriteAllBytes(Path.Combine(pkg,"OptiScaler.dll"),bytes);File.WriteAllBytes(Path.Combine(pkg,"amd_fidelityfx_dx12.dll"),bytes);File.WriteAllBytes(Path.Combine(pkg,"dlssg_to_fsr3_amd_is_better.dll"),bytes);File.WriteAllBytes(Path.Combine(game,"Game.exe"),bytes);File.WriteAllBytes(Path.Combine(game,"nvngx_dlss.dll"),bytes);File.WriteAllText(Path.Combine(game,"d3d12.dll.marker"),"");
  string ini="[Upscalers]\r\nDx12Upscaler=auto\r\n[FrameGen]\r\nEnabled=auto\r\nFGInput=auto\r\nFGOutput=auto\r\n[FSR]\r\nUpscalerIndex=auto\r\nFGIndex=auto\r\n[Libraries]\r\nOptiDllPath=auto\r\n[Menu]\r\nFGShortcutKey=auto\r\n[Inputs]\r\nEnableDlssInputs=auto\r\n[Log]\r\nLogToFile=auto\r\n[Spoofing]\r\nDxgi=auto\r\n";
  File.WriteAllText(Path.Combine(pkg,"OptiScaler.ini"),ini);var p=new StackProfile{Package=pkg};
  string cfg=StackEngine.BuildConfig(ini,p);Assert(StackEngine.ReadIni(cfg,"Upscalers","Dx12Upscaler")=="fsr31","Stable schema uses fsr31");Assert(StackEngine.ReadIni(cfg,"FSR","UpscalerIndex")=="0","FSR4 selected");Assert(StackEngine.ReadIni(cfg,"FrameGen","Enabled")=="false","Baseline FG disabled");Assert(StackEngine.ReadIni(cfg,"Menu","FGShortcutKey")=="-1","No End hotkey conflict");
  p.Output="fsr3";cfg=StackEngine.BuildConfig(ini,p);Assert(StackEngine.ReadIni(cfg,"FSR","FGIndex")=="1","FSR3 FG pinned");p.Output="fsr4";cfg=StackEngine.BuildConfig(ini,p);Assert(StackEngine.ReadIni(cfg,"FSR","FGIndex")=="0","FSR4 FG pinned");p.Output="nukems";cfg=StackEngine.BuildConfig(ini,p);Assert(StackEngine.ReadIni(cfg,"FrameGen","FGInput")=="nukems"&&StackEngine.ReadIni(cfg,"FrameGen","FGOutput")=="nukems","Nukem stable pair");p.Output="mfg";Rejected(()=>StackEngine.BuildConfig(ini,p),"Stable rejects unknown MFG schema");
  string modern=ini.Replace("FGOutput=auto","FGOutput=auto\r\nFGNvngxReplacement=auto")+"\r\n[DLSSG]\r\nInterpolationCount=auto\r\n";p.Multiplier=4;cfg=StackEngine.BuildConfig(modern,p);Assert(StackEngine.ReadIni(cfg,"FrameGen","FGNvngxReplacement")=="Arturs"&&StackEngine.ReadIni(cfg,"DLSSG","InterpolationCount")=="3","Modern Enabler MFG");
  Rejected(()=>StackEngine.Safe(root,"..\\outside.dll"),"Traversal blocked");Rejected(()=>StackEngine.Safe(root,"C:\\outside.dll"),"Absolute destination blocked");
  p.Output="off";p.Neural=true;Rejected(()=>StackEngine.Plan(p),"Ordinary OptiScaler rejected for NR");p.Neural=false;
  var files=StackEngine.Plan(p);Assert(files.ContainsKey(@"OptiScaler\amd_fidelityfx_dx12.dll")&&!files.ContainsKey("amd_fidelityfx_dx12.dll"),"Dependencies isolated");
  var g=new Game{Name="Test game",Exe=Path.Combine(game,"Game.exe"),Root=game,Source="test"};string native=Path.Combine(game,"amd_fidelityfx_dx12.dll");File.WriteAllText(native,"native-game-library");string nativeHash=Core.Digest(native);
  StackEngine.Apply(g,p,s=>{});Assert(StackEngine.Record(g)!=null,"Install manifest created");Assert(Core.Digest(native)==nativeHash,"Native library preserved");Assert(File.Exists(Path.Combine(game,"dxgi.dll")),"Proxy deployed");
  p.Output="nukems";StackEngine.Apply(g,p,s=>{});Assert(StackEngine.ReadIni(Core.Read(Path.Combine(game,"OptiScaler.ini")),"FrameGen","FGInput")=="nukems","Switch existing managed profile");
  File.AppendAllText(Path.Combine(game,"OptiScaler.ini"),"\r\n; game overlay changed setting");StackEngine.Restore(g);Assert(!File.Exists(Path.Combine(game,"dxgi.dll"))&&Core.Digest(native)==nativeHash,"Restore after overlay INI edits");
  StackEngine.Apply(g,p,s=>{});File.AppendAllText(Path.Combine(game,"dxgi.dll"),"changed");Rejected(()=>StackEngine.Restore(g),"Modified binary protected");File.WriteAllBytes(Path.Combine(game,"dxgi.dll"),bytes);StackEngine.Restore(g);
  File.WriteAllText(Path.Combine(game,"dxgi.dll"),"existing-mod");Rejected(()=>StackEngine.Apply(g,p,s=>{}),"Existing mod not overwritten");Assert(File.ReadAllText(Path.Combine(game,"dxgi.dll"))=="existing-mod","Existing mod retained");
  Core.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"组合测试结果.json"),new{passed=true,checks=count,scope="Fixture files only; profile config, isolated dependencies, transaction update/restore, conflict protection; no real game installation."});
  using(var f=new StackForm(new List<Game>(),null)){f.Show();Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"组合管理器预览.png"));}f.Hide();}
 }
}
