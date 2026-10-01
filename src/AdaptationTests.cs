using System;
using System.IO;
using System.Text;
public static class AdaptationTests {
 static void Assert(bool ok,string text){if(!ok)throw new Exception(text);}
 static byte[] Pe(ushort machine,string api){var b=new byte[512];b[0]=77;b[1]=90;BitConverter.GetBytes(128).CopyTo(b,60);b[128]=80;b[129]=69;BitConverter.GetBytes(machine).CopyTo(b,132);Encoding.ASCII.GetBytes(api).CopyTo(b,200);return b;}
 public static void Run(){string root=Path.Combine(Path.GetTempPath(),"helper-adapt-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var g=new Game{Name="fixture",Exe=Path.Combine(root,"game.exe"),Root=root};
 File.WriteAllText(g.Exe,"not a PE d3d11.dll");Assert(Adaptation.Bits(g.Exe)==0&&Adaptation.Routes(g,Adaptation.Detect(g)).Count==0,"Reject non-PE despite graphics string");
 File.WriteAllBytes(g.Exe,Pe(0x8664,"d3d11.dll d3d12.dll"));var d=Adaptation.Detect(g);Assert(d.Bits==64&&Adaptation.Routes(g,d).Count==2,"Ambiguous APIs kept separate");
 File.WriteAllBytes(g.Exe,Pe(0x8664,"vulkan-1.dll"));Assert(Adaptation.Detect(g).Apis.Contains("Vulkan")&&Adaptation.Routes(g,Adaptation.Detect(g)).Count==0,"Unsupported route not installable");
 File.WriteAllBytes(g.Exe,Pe(0x8664,""));File.WriteAllText(Path.Combine(root,"MonoGame.Framework.Windows.NetStandard.dll"),"SharpDX.Direct3D11");Assert(Adaptation.Routes(g,Adaptation.Detect(g)).Contains("DX11"),"Managed rendering module detected");
 File.WriteAllBytes(g.Exe,Pe(0x14c,"d3d9.dll"));Assert(Adaptation.Routes(g,Adaptation.Detect(g))[0]=="DX9","x86 route selected");
 Assert(Adaptation.InspectLogs(g,DateTime.MinValue).Contains("请先"),"No verification from old logs");
 bool rejected=false;try{Adaptation.Validate64(root);}catch{rejected=true;}Assert(rejected,"Incomplete package refused");
 File.WriteAllBytes(g.Exe,Pe(0xaa64,"d3d11.dll"));Assert(Adaptation.Routes(g,Adaptation.Detect(g)).Count==0,"ARM64 not misidentified as x64");
 Core.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"自动适配测试.json"),new{passed=true,checks=8});
 }
}
