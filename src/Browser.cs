using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

public static class BrowserSupport {
 public static string Home=Path.Combine(Core.Data,"browser");
 public static string Engine {get{return Path.GetDirectoryName(ExternalComponents.Magpie());}}
 public static string Sdk=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"browser","sdk");
 delegate bool WindowVisitor(IntPtr window,IntPtr data);
 [DllImport("user32")]static extern bool EnumWindows(WindowVisitor visitor,IntPtr data);
 [DllImport("user32")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("user32",CharSet=CharSet.Unicode)]static extern uint RegisterWindowMessage(string name);
 [DllImport("user32")]static extern bool PostMessage(IntPtr window,uint msg,IntPtr w,IntPtr l);
 public static void Stop(Process process){if(process==null)return;try{if(process.HasExited)return;uint quit=RegisterWindowMessage("WM_MAGPIE_QUIT");EnumWindows(delegate(IntPtr window,IntPtr data){uint pid;GetWindowThreadProcessId(window,out pid);if(pid==(uint)process.Id)PostMessage(window,quit,IntPtr.Zero,IntPtr.Zero);return true;},IntPtr.Zero);}catch(InvalidOperationException){}}
 [DllImport("kernel32",CharSet=CharSet.Unicode)]static extern IntPtr LoadLibrary(string path);
 public static void Register(){AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){string name=new AssemblyName(e.Name).Name;if(name!="Microsoft.Web.WebView2.Core"&&name!="Microsoft.Web.WebView2.WinForms")return null;PrepareSdk();return Assembly.LoadFrom(Path.Combine(Sdk,name+".dll"));};}
 public static void PrepareSdk(){if(!File.Exists(Path.Combine(Sdk,"Microsoft.Web.WebView2.Core.dll")))throw new Exception("缺少WebView2 SDK文件，请使用完整开源版便携目录或运行build.ps1构建。");if(LoadLibrary(Path.Combine(Sdk,Environment.Is64BitProcess?"x64":"x86","WebView2Loader.dll"))==IntPtr.Zero)throw new Exception("浏览器加载器无法启动。");}

 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 public static void Open(Form owner){PrepareSdk();using(var form=new HelperBrowser()){form.ShowDialog(owner);}}
 public static void PrepareEnhancement(){
  if(Process.GetProcessesByName("Magpie").Length>0)throw new Exception("Magpie已在运行，请使用它现有的配置与快捷键，或退出后再从浏览器启动。");
  ExternalComponents.Magpie();
 }
 public static string Address(string text){Uri uri;text=text.Trim();if(Uri.TryCreate(text,UriKind.Absolute,out uri)&&(uri.Scheme=="https"||uri.Scheme=="http"))return uri.AbsoluteUri;if(text.IndexOf(' ')<0&&text.Contains(".")&&Uri.TryCreate("https://"+text,UriKind.Absolute,out uri))return uri.AbsoluteUri;return "https://www.bing.com/search?q="+Uri.EscapeDataString(text);}
}

public class HelperBrowser:Form {
 WebView2 view=new WebView2();TextBox address=new TextBox();Label status=new Label();Button enhance=new Button();bool ready,working;Process magpie;
 public HelperBrowser(){
  Text="网页视频浏览器 · 中文游戏助手";ClientSize=new Size(1100,710);MinimumSize=new Size(760,500);StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",10);BackColor=Color.FromArgb(18,24,34);
  var toolbar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=45,Padding=new Padding(6),WrapContents=false};Controls.Add(view);view.Dock=DockStyle.Fill;Controls.Add(toolbar);
  Action<string,Action> add=(text,action)=>{var b=new Button{Text=text,Width=62,Height=30};b.Click+=delegate{try{action();}catch(Exception ex){Status(ex.Message);}};toolbar.Controls.Add(b);};
  add("后退",()=>{if(ready&&view.CanGoBack)view.GoBack();});add("前进",()=>{if(ready&&view.CanGoForward)view.GoForward();});add("刷新",()=>{if(ready)view.Reload();});add("主页",()=>ShowHome());
  address.Width=390;address.Height=30;toolbar.Controls.Add(address);address.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;Navigate();}};add("前往",Navigate);
  var local=new Button{Text="本地视频",Width=90,Height=30};toolbar.Controls.Add(local);local.Click+=delegate{using(var d=new OpenFileDialog{Filter="视频文件|*.mp4;*.webm;*.m4v;*.mov|所有文件|*.*"})if(d.ShowDialog(this)==DialogResult.OK&&ready)view.CoreWebView2.Navigate(new Uri(d.FileName).AbsoluteUri);};
  enhance.Text="外部增强";enhance.Width=90;enhance.Height=30;toolbar.Controls.Add(enhance);enhance.Click+=async delegate{if(working)return;working=true;enhance.Enabled=false;try{await Task.Run(()=>BrowserSupport.PrepareEnhancement());magpie=Process.Start(new ProcessStartInfo(Path.Combine(BrowserSupport.Engine,"Magpie.exe"),"-t"){WorkingDirectory=BrowserSupport.Engine,UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden});Status("已启动你选择的Magpie。请按该程序自己的配置与快捷键开启效果；本助手不改变其配置，也不确认效果生效。");Activate();view.Focus();}catch(Exception ex){Status(ex.Message);MessageBox.Show(this,ex.Message,"视频外部增强");}finally{working=false;enhance.Enabled=true;}};
  status.Dock=DockStyle.Bottom;status.Height=58;status.Padding=new Padding(8);status.ForeColor=Color.Gainsboro;status.Text="普通播放。外部增强需先在主界面“外部组件 / 许可”中选择自己的Magpie；不附带运行库或模型。";Controls.Add(status);
  Shown+=async delegate{try{var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(BrowserSupport.Home,"profile"));await view.EnsureCoreWebView2Async(env);ready=true;view.CoreWebView2.Settings.AreHostObjectsAllowed=false;view.CoreWebView2.SourceChanged+=delegate{address.Text=view.Source==null?"":view.Source.ToString();};view.CoreWebView2.NewWindowRequested+=delegate(object s,CoreWebView2NewWindowRequestedEventArgs e){e.Handled=true;Uri u;if(Uri.TryCreate(e.Uri,UriKind.Absolute,out u)&&(u.Scheme=="http"||u.Scheme=="https"))view.CoreWebView2.Navigate(e.Uri);};view.CoreWebView2.NavigationCompleted+=delegate(object s,CoreWebView2NavigationCompletedEventArgs e){if(!e.IsSuccess && e.WebErrorStatus!=CoreWebView2WebErrorStatus.ConnectionAborted)Status("网页加载失败："+e.WebErrorStatus);};ShowHome();}catch(Exception ex){Status("浏览器启动失败："+ex.Message);}};
  var stop=new Button{Text="退出外部增强",Width=90,Height=30};toolbar.Controls.Add(stop);stop.Click+=delegate{BrowserSupport.Stop(magpie);Status("已请求退出本浏览器启动的增强组件。普通网页播放保留。");};
  foreach(Control control in toolbar.Controls){var button=control as Button;if(button!=null){button.BackColor=Color.FromArgb(42,57,76);button.ForeColor=Color.White;button.FlatStyle=FlatStyle.Flat;}}
  FormClosing+=delegate(object s,FormClosingEventArgs e){if(working){e.Cancel=true;return;}BrowserSupport.Stop(magpie);};
  FormClosed+=delegate{view.Dispose();if(magpie!=null)magpie.Dispose();};
 }
 void Status(string s){status.Text=s;}
 void Navigate(){if(ready&&!String.IsNullOrWhiteSpace(address.Text))view.CoreWebView2.Navigate(BrowserSupport.Address(address.Text));}
 void ShowHome(){if(!ready)return;view.NavigateToString("<!doctype html><meta charset='utf-8'><style>body{background:#101722;color:#e9edf4;font:20px system-ui;margin:7% 10%;line-height:1.8}h1{color:#38d5b3}a{color:#6ecfff}</style><h1>网页视频浏览器 · 开源版</h1><p>输入网址或搜索词，也可以打开本地MP4。</p><p>外部增强：先在助手的“外部组件 / 许可”中选择自己取得的Magpie，再点击上方“外部增强”。效果与快捷键由该程序自己的配置决定。</p><p>本版不附带神经运行库、模型或插帧插件，不修改Magpie配置，也不向网页注入游戏DLL。启动程序不代表增强已生效。</p><p><a href='https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4'>公开测试视频</a></p>");}
}
