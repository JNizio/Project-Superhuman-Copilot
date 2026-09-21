using Microsoft.Win32;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace SuperhumanCopilot;
internal static class Program {
 [STAThread] static void Main(){ ApplicationConfiguration.Initialize(); Application.Run(new AppContext()); }
}
internal sealed class AppContext : ApplicationContext {
 const int Port=8765; readonly NotifyIcon tray; readonly HttpListener server=new(); readonly CancellationTokenSource stop=new();
 readonly string token; readonly string dir; readonly System.Threading.Timer timer;
 public AppContext(){
  dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ProjectSuperhuman","Copilot"); Directory.CreateDirectory(dir);
  token=LoadToken();
  var menu=new ContextMenuStrip();
  menu.Items.Add("Status",null,(_,_)=>ShowStatus()); menu.Items.Add("Copy pairing info",null,(_,_)=>CopyPairing()); menu.Items.Add("Exit",null,(_,_)=>ExitThread());
  tray=new NotifyIcon{Icon=SystemIcons.Information,Text="Project Superhuman Copilot",ContextMenuStrip=menu,Visible=true};
  tray.DoubleClick+=(_,_)=>ShowStatus(); EnableStartup(); StartServer();
  timer=new System.Threading.Timer(_=>Save(),null,TimeSpan.Zero,TimeSpan.FromMinutes(1));
 }
 string LoadToken(){var p=Path.Combine(dir,"pairing-token.txt"); if(File.Exists(p)) return File.ReadAllText(p).Trim(); var v=Convert.ToHexString(RandomNumberGenerator.GetBytes(24)); File.WriteAllText(p,v); return v;}
 static long Uptime()=>Environment.TickCount64/1000;
 object Status()=>new{schema=1,deviceType="windows_pc",hostname=Environment.MachineName,observedAtUtc=DateTime.UtcNow,bootUtc=DateTime.UtcNow.AddSeconds(-Uptime()),uptimeSeconds=Uptime(),uptimeHours=Math.Round(Uptime()/3600d,2),source="windows.Environment.TickCount64"};
 void Save(){try{File.WriteAllText(Path.Combine(dir,"status.json"),JsonSerializer.Serialize(Status(),new JsonSerializerOptions{WriteIndented=true}));}catch{}}
 void StartServer(){try{server.Prefixes.Add($"http://+:{Port}/");server.Start();_=Task.Run(Loop);}catch(Exception e){MessageBox.Show("LAN server could not start: "+e.Message);}}
 async Task Loop(){while(!stop.IsCancellationRequested&&server.IsListening){try{var x=await server.GetContextAsync();_=Task.Run(()=>Handle(x));}catch when(stop.IsCancellationRequested){break;}catch{await Task.Delay(500);}}}
 async Task Handle(HttpListenerContext x){
  x.Response.ContentType="application/json"; object body;
  if(x.Request.Url?.AbsolutePath=="/health") body=new{ok=true};
  else if(x.Request.Url?.AbsolutePath=="/status"){
   if(x.Request.Headers["Authorization"]!="Bearer "+token){x.Response.StatusCode=401;body=new{error="unauthorized"};} else body=Status();
  } else{x.Response.StatusCode=404;body=new{error="not_found"};}
  var b=JsonSerializer.SerializeToUtf8Bytes(body);x.Response.ContentLength64=b.Length;await x.Response.OutputStream.WriteAsync(b);x.Response.Close();
 }
 static string Ip(){foreach(var n in NetworkInterface.GetAllNetworkInterfaces()){if(n.OperationalStatus!=OperationalStatus.Up||n.NetworkInterfaceType==NetworkInterfaceType.Loopback)continue;foreach(var a in n.GetIPProperties().UnicastAddresses)if(a.Address.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(a.Address))return a.Address.ToString();}return"127.0.0.1";}
 string Pair()=> $"Endpoint: http://{Ip()}:{Port}/status\r\nToken: {token}";
 void CopyPairing(){Clipboard.SetText(Pair());}
 void ShowStatus()=>MessageBox.Show($"Superhuman Copilot is running.\n\nPC uptime: {TimeSpan.FromSeconds(Uptime())}\nEndpoint: http://{Ip()}:{Port}/status","Superhuman Copilot");
 static void EnableStartup(){using var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true);k?.SetValue("ProjectSuperhumanCopilot",$"\"{Environment.ProcessPath}\"");}
 protected override void ExitThreadCore(){stop.Cancel();timer.Dispose();try{server.Stop();server.Close();}catch{}tray.Visible=false;tray.Dispose();base.ExitThreadCore();}
}