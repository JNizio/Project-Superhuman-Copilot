using Microsoft.Win32;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SuperhumanCopilot;
internal static class Program { [STAThread] static void Main(){ApplicationConfiguration.Initialize();Application.Run(new AppContext());} }
internal sealed class AppContext:ApplicationContext{
 const int Port=8765; const string Repo="JNizio/Project-Superhuman-Copilot"; readonly NotifyIcon tray; readonly TcpListener server; readonly CancellationTokenSource stop=new(); readonly string token,dir; readonly System.Threading.Timer timer;
 public AppContext(){
  dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ProjectSuperhuman","Copilot");Directory.CreateDirectory(dir);token=LoadToken();
  var menu=new ContextMenuStrip();menu.Items.Add("Status",null,(_,_)=>ShowStatus());menu.Items.Add("Copy pairing info",null,(_,_)=>CopyPairing());menu.Items.Add("Settings",null,(_,_)=>ShowSettings());menu.Items.Add("Exit",null,(_,_)=>ExitThread());
  tray=new NotifyIcon{Icon=SystemIcons.Information,Text="Project Superhuman Copilot",ContextMenuStrip=menu,Visible=true};tray.DoubleClick+=(_,_)=>ShowStatus();EnableStartup();
  server=new TcpListener(IPAddress.Any,Port);StartServer();timer=new System.Threading.Timer(_=>Save(),null,TimeSpan.Zero,TimeSpan.FromMinutes(1));
 }
 string LoadToken(){var p=Path.Combine(dir,"pairing-token.txt");if(File.Exists(p))return File.ReadAllText(p).Trim();var v=Convert.ToHexString(RandomNumberGenerator.GetBytes(24));File.WriteAllText(p,v);return v;}
 static long Uptime()=>Environment.TickCount64/1000;
 object Status()=>new{schema=1,deviceType="windows_pc",hostname=Environment.MachineName,observedAtUtc=DateTime.UtcNow,bootUtc=DateTime.UtcNow.AddSeconds(-Uptime()),uptimeSeconds=Uptime(),uptimeHours=Math.Round(Uptime()/3600d,2),source="windows.Environment.TickCount64"};
 void Save(){try{File.WriteAllText(Path.Combine(dir,"status.json"),JsonSerializer.Serialize(Status(),new JsonSerializerOptions{WriteIndented=true}));}catch{}}
 void StartServer(){try{server.Start();_=Task.Run(Loop);}catch(Exception e){MessageBox.Show("LAN server could not start: "+e.Message);}}
 async Task Loop(){while(!stop.IsCancellationRequested){try{var c=await server.AcceptTcpClientAsync(stop.Token);_=Task.Run(()=>Handle(c));}catch(OperationCanceledException){break;}catch when(stop.IsCancellationRequested){break;}catch{await Task.Delay(500);}}}
 async Task Handle(TcpClient client){using(client)using var stream=client.GetStream();try{using var reader=new StreamReader(stream,Encoding.ASCII,false,4096,true);var request=await reader.ReadLineAsync();if(string.IsNullOrWhiteSpace(request))return;string? auth=null,line;while(!string.IsNullOrEmpty(line=await reader.ReadLineAsync()))if(line.StartsWith("Authorization:",StringComparison.OrdinalIgnoreCase))auth=line["Authorization:".Length..].Trim();var parts=request.Split(' ');var path=parts.Length>1?parts[1]:"/";int code=200;string st="OK";object body;if(path=="/health")body=new{ok=true};else if(path=="/status"){if(auth!="Bearer "+token){code=401;st="Unauthorized";body=new{error="unauthorized"};}else body=Status();}else{code=404;st="Not Found";body=new{error="not_found"};}var payload=JsonSerializer.SerializeToUtf8Bytes(body);var headers=Encoding.ASCII.GetBytes($"HTTP/1.1 {code} {st}\r\nContent-Type: application/json\r\nContent-Length: {payload.Length}\r\nConnection: close\r\nAccess-Control-Allow-Origin: *\r\n\r\n");await stream.WriteAsync(headers);await stream.WriteAsync(payload);}catch{}}
 static string Ip(){foreach(var n in NetworkInterface.GetAllNetworkInterfaces()){if(n.OperationalStatus!=OperationalStatus.Up||n.NetworkInterfaceType==NetworkInterfaceType.Loopback)continue;var p=n.GetIPProperties();if(!p.GatewayAddresses.Any(g=>g.Address.AddressFamily==AddressFamily.InterNetwork&&!g.Address.Equals(IPAddress.Any)))continue;foreach(var a in p.UnicastAddresses)if(a.Address.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(a.Address))return a.Address.ToString();}return"127.0.0.1";}
 string Pair()=>$"Endpoint: http://{Ip()}:{Port}/status\r\nToken: {token}";void CopyPairing()=>Clipboard.SetText(Pair());
 void ShowStatus()=>MessageBox.Show($"Superhuman Copilot is running.\n\nPC uptime: {TimeSpan.FromSeconds(Uptime())}\nEndpoint: http://{Ip()}:{Port}/status","Superhuman Copilot");
 void ShowSettings(){var f=new Form{Text="Superhuman Copilot — Settings",Width=430,Height=210,StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false};var title=new Label{Text="Project Superhuman Copilot",Left=22,Top=20,AutoSize=true,Font=new Font(SystemFonts.DefaultFont.FontFamily,12,FontStyle.Bold)};var info=new Label{Text="Updates are downloaded from the official GitHub repository.",Left=22,Top=55,Width=370,AutoSize=true};var b=new Button{Text="Check for updates",Left=22,Top=95,Width=160,Height=34};b.Click+=async(_,_)=>{b.Enabled=false;b.Text="Checking…";await UpdateAsync(f);b.Enabled=true;b.Text="Check for updates";};f.Controls.AddRange([title,info,b]);f.ShowDialog();}
 async Task UpdateAsync(IWin32Window owner){try{
  using var http=new HttpClient();http.DefaultRequestHeaders.UserAgent.ParseAdd("ProjectSuperhumanCopilot/1");
  var json=await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");using var doc=JsonDocument.Parse(json);var root=doc.RootElement;var tag=root.GetProperty("tag_name").GetString()??"unknown";
  string? url=null;foreach(var a in root.GetProperty("assets").EnumerateArray()){var n=a.GetProperty("name").GetString()??"";if(n.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)){url=a.GetProperty("browser_download_url").GetString();break;}}
  if(url is null){MessageBox.Show(owner,"No Windows EXE is attached to the latest GitHub release yet.","Update");return;}
  var current=Application.ProductVersion;var answer=MessageBox.Show(owner,$"Latest release: {tag}\nInstalled build: {current}\n\nDownload and install the latest release?","Update",MessageBoxButtons.YesNo);if(answer!=DialogResult.Yes)return;
  var exe=Environment.ProcessPath??throw new InvalidOperationException("Could not locate the running EXE.");var next=Path.Combine(dir,"SuperhumanCopilot.update.exe");await File.WriteAllBytesAsync(next,await http.GetByteArrayAsync(url));
  var script=Path.Combine(dir,"apply-update.cmd");var q='"';File.WriteAllText(script,$"@echo off\r\ntimeout /t 2 /nobreak >nul\r\ncopy /y {q}{next}{q} {q}{exe}{q} >nul\r\nstart {q}{q} {q}{exe}{q}\r\ndel {q}{next}{q}\r\ndel %~f0\r\n");
  Process.Start(new ProcessStartInfo("cmd.exe",$"/c {q}{script}{q}"){CreateNoWindow=true,UseShellExecute=false});ExitThread();
 }catch(HttpRequestException e)when(e.StatusCode==HttpStatusCode.NotFound){MessageBox.Show(owner,"No GitHub release has been published yet.","Update");}catch(Exception e){MessageBox.Show(owner,"Update failed: "+e.Message,"Update");}}
 static void EnableStartup(){using var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true);k?.SetValue("ProjectSuperhumanCopilot",$"\"{Environment.ProcessPath}\"");}
 protected override void ExitThreadCore(){stop.Cancel();timer.Dispose();try{server.Stop();}catch{}tray.Visible=false;tray.Dispose();base.ExitThreadCore();}
}