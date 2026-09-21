using Microsoft.Win32;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SuperhumanCopilot;
internal static class Program {
 [STAThread] static void Main(){ ApplicationConfiguration.Initialize(); Application.Run(new AppContext()); }
}
internal sealed class AppContext : ApplicationContext {
 const int Port=8765; readonly NotifyIcon tray; readonly TcpListener server; readonly CancellationTokenSource stop=new();
 readonly string token; readonly string dir; readonly System.Threading.Timer timer;
 public AppContext(){
  dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ProjectSuperhuman","Copilot"); Directory.CreateDirectory(dir);
  token=LoadToken();
  var menu=new ContextMenuStrip();
  menu.Items.Add("Status",null,(_,_)=>ShowStatus()); menu.Items.Add("Copy pairing info",null,(_,_)=>CopyPairing()); menu.Items.Add("Exit",null,(_,_)=>ExitThread());
  tray=new NotifyIcon{Icon=SystemIcons.Information,Text="Project Superhuman Copilot",ContextMenuStrip=menu,Visible=true};
  tray.DoubleClick+=(_,_)=>ShowStatus(); EnableStartup();
  server=new TcpListener(IPAddress.Any,Port); StartServer();
  timer=new System.Threading.Timer(_=>Save(),null,TimeSpan.Zero,TimeSpan.FromMinutes(1));
 }
 string LoadToken(){var p=Path.Combine(dir,"pairing-token.txt"); if(File.Exists(p)) return File.ReadAllText(p).Trim(); var v=Convert.ToHexString(RandomNumberGenerator.GetBytes(24)); File.WriteAllText(p,v); return v;}
 static long Uptime()=>Environment.TickCount64/1000;
 object Status()=>new{schema=1,deviceType="windows_pc",hostname=Environment.MachineName,observedAtUtc=DateTime.UtcNow,bootUtc=DateTime.UtcNow.AddSeconds(-Uptime()),uptimeSeconds=Uptime(),uptimeHours=Math.Round(Uptime()/3600d,2),source="windows.Environment.TickCount64"};
 void Save(){try{File.WriteAllText(Path.Combine(dir,"status.json"),JsonSerializer.Serialize(Status(),new JsonSerializerOptions{WriteIndented=true}));}catch{}}
 void StartServer(){try{server.Start();_=Task.Run(Loop);}catch(Exception e){MessageBox.Show("LAN server could not start: "+e.Message);}}
 async Task Loop(){while(!stop.IsCancellationRequested){try{var client=await server.AcceptTcpClientAsync(stop.Token);_=Task.Run(()=>Handle(client));}catch(OperationCanceledException){break;}catch when(stop.IsCancellationRequested){break;}catch{await Task.Delay(500);}}}
 async Task Handle(TcpClient client){
  using(client) using var stream=client.GetStream();
  try{
   using var reader=new StreamReader(stream,Encoding.ASCII,false,4096,true);
   var request=await reader.ReadLineAsync(); if(string.IsNullOrWhiteSpace(request))return;
   string? authorization=null; string? line;
   while(!string.IsNullOrEmpty(line=await reader.ReadLineAsync())) if(line.StartsWith("Authorization:",StringComparison.OrdinalIgnoreCase)) authorization=line["Authorization:".Length..].Trim();
   var parts=request.Split(' '); var path=parts.Length>1?parts[1]:"/";
   int code=200; string statusText="OK"; object body;
   if(path=="/health") body=new{ok=true};
   else if(path=="/status"){
    if(authorization!="Bearer "+token){code=401;statusText="Unauthorized";body=new{error="unauthorized"};} else body=Status();
   } else {code=404;statusText="Not Found";body=new{error="not_found"};}
   var payload=JsonSerializer.SerializeToUtf8Bytes(body);
   var headers=Encoding.ASCII.GetBytes($"HTTP/1.1 {code} {statusText}\r\nContent-Type: application/json\r\nContent-Length: {payload.Length}\r\nConnection: close\r\nAccess-Control-Allow-Origin: *\r\n\r\n");
   await stream.WriteAsync(headers); await stream.WriteAsync(payload);
  }catch{}
 }
 static string Ip(){
  foreach(var n in NetworkInterface.GetAllNetworkInterfaces()){
   if(n.OperationalStatus!=OperationalStatus.Up||n.NetworkInterfaceType==NetworkInterfaceType.Loopback)continue;
   var p=n.GetIPProperties(); if(!p.GatewayAddresses.Any(g=>g.Address.AddressFamily==AddressFamily.InterNetwork&&!g.Address.Equals(IPAddress.Any)))continue;
   foreach(var a in p.UnicastAddresses) if(a.Address.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(a.Address)) return a.Address.ToString();
  }
  foreach(var n in NetworkInterface.GetAllNetworkInterfaces()) if(n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback) foreach(var a in n.GetIPProperties().UnicastAddresses) if(a.Address.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(a.Address)) return a.Address.ToString();
  return"127.0.0.1";
 }
 string Pair()=> $"Endpoint: http://{Ip()}:{Port}/status\r\nToken: {token}";
 void CopyPairing(){Clipboard.SetText(Pair());}
 void ShowStatus()=>MessageBox.Show($"Superhuman Copilot is running.\n\nPC uptime: {TimeSpan.FromSeconds(Uptime())}\nEndpoint: http://{Ip()}:{Port}/status","Superhuman Copilot");
 static void EnableStartup(){using var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true);k?.SetValue("ProjectSuperhumanCopilot",$"\"{Environment.ProcessPath}\"");}
 protected override void ExitThreadCore(){stop.Cancel();timer.Dispose();try{server.Stop();}catch{}tray.Visible=false;tray.Dispose();base.ExitThreadCore();}
}