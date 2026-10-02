using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public class Skill {
 public string Name, Display, Description, Path, Source, Evidence, Raw, Body, Prompt, Policy, Plugin;
 public bool Registered, Seen, Enabled=true;
 public string Notice { get { var x=new List<string>(); if(Description.Length>350)x.Add("简介较长，建议检查"); if(!Registered&&!Seen)x.Add("仅发现文件，未确认可用"); if(!Enabled)x.Add("已禁用"); return String.Join("；",x); } }
}
public class Inventory {
 public List<Skill> Skills=new List<Skill>(); public List<string> Notes=new List<string>();
 public string Session="", SessionDate="", SessionCwd=""; public bool Live;
 public Dictionary<string,Skill> ByPath=new Dictionary<string,Skill>(StringComparer.OrdinalIgnoreCase);
}
public static class Scan {
 public static string User=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
 public static string Home=Environment.GetEnvironmentVariable("CODEX_HOME")??System.IO.Path.Combine(User,".codex");
 public static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=48*1024*1024,RecursionLimit=160};}
 public static Dictionary<string,object> Obj(object o){return o as Dictionary<string,object>??new Dictionary<string,object>();}
 public static string S(Dictionary<string,object> d,string k){return d.ContainsKey(k)&&d[k]!=null?Convert.ToString(d[k]):"";}
 public static IEnumerable<object> A(object o){return (o as IEnumerable??new object[0]).Cast<object>();}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateFile(string name,uint access,uint share,IntPtr sec,uint disposition,uint flags,IntPtr template);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern uint GetFinalPathNameByHandle(IntPtr h,StringBuilder b,uint n,uint flags);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 public static string Canon(string p){
  p=System.IO.Path.GetFullPath(p);IntPtr h=CreateFile(p,0,7,IntPtr.Zero,3,0x02000000,IntPtr.Zero);
  if(h!=new IntPtr(-1)){try{var b=new StringBuilder(32768);if(GetFinalPathNameByHandle(h,b,(uint)b.Capacity,0)>0){var s=b.ToString();return s.StartsWith(@"\\?\UNC\")?@"\\"+s.Substring(8):s.StartsWith(@"\\?\")?s.Substring(4):s;}}finally{CloseHandle(h);}}
  return p;
 }
 public static Dictionary<string,string> Front(string text){
  var d=new Dictionary<string,string>();var lines=text.TrimStart('\uFEFF').Replace("\r","").Split('\n');if(lines.Length==0||lines[0].Trim()!="---")return d;
  for(int i=1;i<lines.Length&&lines[i].Trim()!="---";i++){
   var m=Regex.Match(lines[i],@"^([\w-]+):\s*(.*)$");if(!m.Success)continue;
   var k=m.Groups[1].Value;var v=m.Groups[2].Value.Trim();
   if(v==">"||v=="|"||v==">-"||v=="|-"||v==""){
    var a=new List<string>();while(i+1<lines.Length&&lines[i+1].Length>0&&Char.IsWhiteSpace(lines[i+1][0]))a.Add(lines[++i].Trim());
    v=String.Join(v.StartsWith("|")?"\r\n":" ",a);
   }else if(v.Length>=2&&v[0]=='"'&&v[v.Length-1]=='"'){try{v=Json().Deserialize<string>(v);}catch{v=v.Substring(1,v.Length-2);}}
   else if(v.Length>=2&&v[0]=='\''&&v[v.Length-1]=='\'')v=v.Substring(1,v.Length-2).Replace("''","'");
   d[k]=v;
  }return d;
 }
 public static Skill Read(string path,string source){
  var raw=File.ReadAllText(path,Encoding.UTF8);var d=Front(raw);string name=d.ContainsKey("name")?d["name"]:new DirectoryInfo(System.IO.Path.GetDirectoryName(path)).Name;
  string body=Regex.Replace(raw.TrimStart('\uFEFF'),@"\A---\s*\r?\n[\s\S]*?\r?\n---\s*\r?\n","");
  var title=Regex.Match(body,@"(?m)^#\s+(.+)");var s=new Skill{Name=name,Display=title.Success?title.Groups[1].Value.Trim():name,Description=d.ContainsKey("description")?d["description"]:"（文件没有 description，请查看原文）",Path=System.IO.Path.GetFullPath(path),Source=source,Raw=raw,Body=body,Prompt="",Policy="允许自动匹配（默认；最终取决于 Codex 和本轮指令）",Plugin=""};
  string y=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path),"agents","openai.yaml");
  if(File.Exists(y)){
   string t=File.ReadAllText(y,Encoding.UTF8);
   if(Regex.IsMatch(t,@"(?m)^\s*allow_implicit_invocation:\s*false\b"))s.Policy="仅手动点名（allow_implicit_invocation: false）";
   var n=Regex.Match(t,@"(?m)^\s*display_name:\s*(.+)$");if(n.Success)s.Display=n.Groups[1].Value.Trim().Trim('"','\'');
   var pr=Regex.Match(t,@"(?m)^\s*default_prompt:\s*(.+)$");if(pr.Success)s.Prompt=pr.Groups[1].Value.Trim().Trim('"','\'');
  }return s;
 }
 public static Skill Add(Inventory r,string path,string source){
  if(!File.Exists(path))return null;string key=Canon(path);Skill s;
  if(r.ByPath.TryGetValue(key,out s))return s;
  try{s=Read(path,source);r.ByPath[key]=s;r.Skills.Add(s);return s;}catch(Exception e){r.Notes.Add(path+"："+e.Message);return null;}
 }
 static void Walk(Inventory r,string root,string source){
  if(!Directory.Exists(root))return;var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var stack=new Stack<string>();stack.Push(root);
  while(stack.Count>0){var dir=stack.Pop();try{
   if(!seen.Add(Canon(dir)))continue;if(seen.Count>12000){r.Notes.Add("目录过大，扫描已限流："+root);break;}
   string file=System.IO.Path.Combine(dir,"SKILL.md");if(File.Exists(file))Add(r,file,source);
   foreach(var sub in Directory.GetDirectories(dir)){
    var n=System.IO.Path.GetFileName(sub);if(new[]{".git","node_modules","references","scripts","assets",".venv","venv","__pycache__"}.Contains(n))continue;
    stack.Push(sub);
   }
  }catch(Exception e){r.Notes.Add(dir+"："+e.Message);}}
 }
 static string Source(string p){
  if(p.IndexOf(@"\plugins\",StringComparison.OrdinalIgnoreCase)>=0)return "插件";
  if(p.IndexOf(@"\skills\.system\",StringComparison.OrdinalIgnoreCase)>=0)return "系统";
  if(p.IndexOf(User,StringComparison.OrdinalIgnoreCase)==0)return p.IndexOf(@"\.agents\",StringComparison.OrdinalIgnoreCase)>=0?"通用 Agents":"我的 Skills";
  return "项目";
 }
 static Dictionary<string,object> Request(Process p,int id,string method,object param){
  p.StandardInput.WriteLine(Json().Serialize(new{id=id,method=method,@params=param}));p.StandardInput.Flush();
  for(int i=0;i<100;i++){
   string line=p.StandardOutput.ReadLine();if(line==null)throw new Exception("Codex 本地服务已退出");
   var d=Obj(Json().DeserializeObject(line));if(d.ContainsKey("id")&&Convert.ToInt32(d["id"])==id){if(d.ContainsKey("error"))throw new Exception(Json().Serialize(d["error"]));return Obj(d["result"]);}
  }throw new Exception("无法读取技能目录响应");
 }
 static void Registered(Inventory r,string cwd){
  var candidates=new[]{System.IO.Path.Combine(Home,@"plugins\.plugin-appserver\codex.exe"),System.IO.Path.Combine(Home,@"bin\codex.exe")};var exe=candidates.FirstOrDefault(File.Exists);
  if(exe==null){r.Notes.Add("未找到 Codex 本地服务：使用磁盘和最近聊天目录。");return;}
  using(var p=new Process()){
   p.StartInfo=new ProcessStartInfo(exe,"app-server --stdio"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=cwd};
   try{p.Start();p.ErrorDataReceived+=(o,e)=>{};p.BeginErrorReadLine();
    var t=Task.Run(()=>{Request(p,1,"initialize",new{clientInfo=new{name="skill_viewer",version="1.0"},capabilities=new{experimentalApi=true}});p.StandardInput.WriteLine("{\"method\":\"initialized\"}");p.StandardInput.Flush();return Request(p,2,"skills/list",new{cwds=new[]{cwd},forceReload=true});});
    if(!t.Wait(18000))throw new Exception("读取 Codex 本地目录超时（18 秒）");
    var result=t.Result;if(!result.ContainsKey("data"))throw new Exception("目录响应没有 data");
    foreach(var entry in A(result["data"])){
     var en=Obj(entry);if(en.ContainsKey("errors"))foreach(var err in A(en["errors"]))r.Notes.Add("Codex 目录错误："+Json().Serialize(err));
     if(!en.ContainsKey("skills"))continue;
     foreach(var item in A(en["skills"])){
      var m=Obj(item);string path=S(m,"path");var s=Add(r,path,Source(path));if(s==null)continue;
      s.Name=S(m,"name");s.Registered=true;s.Enabled=!m.ContainsKey("enabled")||Convert.ToBoolean(m["enabled"]);s.Plugin=S(m,"pluginId");
      if(m.ContainsKey("interface")){var f=Obj(m["interface"]);if(S(f,"displayName")!="")s.Display=S(f,"displayName");if(S(f,"defaultPrompt")!="")s.Prompt=S(f,"defaultPrompt");}
     }
    }r.Live=true;
   }catch(Exception e){r.Notes.Add("本地可用目录读取失败："+(e.InnerException??e).Message);}
   finally{try{if(!p.HasExited){p.Kill();p.WaitForExit(2000);}}catch{}}
  }
 }
 static void Session(Inventory r,string cwd){
  string root=System.IO.Path.Combine(Home,"sessions");if(!Directory.Exists(root))return;
  var files=Directory.EnumerateFiles(root,"*.jsonl",SearchOption.AllDirectories).Select(p=>new FileInfo(p)).OrderByDescending(p=>p.LastWriteTimeUtc).Take(24).ToList();
  string fallback="",fallbackFile="",fallbackCwd="",fallbackTime="";
  foreach(var file in files){try{
   string cat="",scwd="",stamp="";using(var sr=new StreamReader(new FileStream(file.FullName,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete),Encoding.UTF8)){
    for(int i=0;i<30&&!sr.EndOfStream;i++){
     string line=sr.ReadLine();if(line.Length>12*1024*1024)continue;
     var d=Obj(Json().DeserializeObject(line));var v=Obj(d.ContainsKey("payload")?d["payload"]:null);
     if(S(d,"type")=="session_meta"){if(S(v,"thread_source")=="subagent")break;scwd=S(v,"cwd");stamp=S(v,"timestamp");}
     if(S(d,"type")=="turn_context")break;
     if(S(d,"type")!="response_item"||S(v,"role")!="developer"||!v.ContainsKey("content"))continue;
     string text=String.Join("\n",A(v["content"]).Select(x=>S(Obj(x),"text")));
     if(text.Contains("### Available skills"))cat=text;
    }
   }if(cat=="")continue;
   if(fallback==""){fallback=cat;fallbackFile=file.FullName;fallbackCwd=scwd;fallbackTime=stamp;}
   if(String.Equals(scwd.TrimEnd('\\'),cwd.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)){fallback=cat;fallbackFile=file.FullName;fallbackCwd=scwd;fallbackTime=stamp;break;}
  }catch{}}
  if(fallback=="")return;
  r.Session=fallbackFile;r.SessionDate=fallbackTime;r.SessionCwd=fallbackCwd;
  var roots=new Dictionary<string,string>();foreach(Match m in Regex.Matches(fallback,@"- `(r\d+)` = `([^`]+)`"))roots[m.Groups[1].Value]=m.Groups[2].Value;
  foreach(Match m in Regex.Matches(fallback,@"(?m)^- (.+?): (.*?) \(file: ([^)]+)\)\s*$")){
   string path=m.Groups[3].Value;foreach(var kv in roots)if(path.StartsWith(kv.Key+"/")){path=kv.Value+path.Substring(kv.Key.Length);break;}
   var s=Add(r,path,Source(path.Replace('/','\\')));if(s==null)continue;s.Seen=true;if(!s.Registered)s.Name=m.Groups[1].Value;
  }
 }
 public static Inventory Run(string cwd,IEnumerable<string> extras){
  if(!Directory.Exists(cwd))throw new Exception("项目目录不存在："+cwd);
  var r=new Inventory();Registered(r,cwd);Session(r,cwd);
  Walk(r,System.IO.Path.Combine(Home,"skills"),"我的 Skills");Walk(r,System.IO.Path.Combine(User,@".agents\skills"),"通用 Agents");
  var current=new DirectoryInfo(cwd);while(current!=null){Walk(r,System.IO.Path.Combine(current.FullName,@".agents\skills"),"项目");Walk(r,System.IO.Path.Combine(current.FullName,@".codex\skills"),"项目（兼容目录）");if(Directory.Exists(System.IO.Path.Combine(current.FullName,".git"))||File.Exists(System.IO.Path.Combine(current.FullName,".git")))break;current=current.Parent;}
  string cache=System.IO.Path.Combine(Home,@"plugins\cache");if(Directory.Exists(cache))foreach(var market in Directory.GetDirectories(cache))foreach(var plugin in Directory.GetDirectories(market))foreach(var ver in Directory.GetDirectories(plugin))Walk(r,System.IO.Path.Combine(ver,"skills"),"插件");
  foreach(var e in extras)Walk(r,e,"自选目录");
  var disabled=new HashSet<string>(StringComparer.OrdinalIgnoreCase);string cfg=System.IO.Path.Combine(Home,"config.toml");
  if(File.Exists(cfg)){string txt=File.ReadAllText(cfg);foreach(Match m in Regex.Matches(txt,@"(?ms)^\[\[skills\.config\]\]\s*\n(.*?)(?=^\[|\z)")){if(!Regex.IsMatch(m.Groups[1].Value,@"(?m)^enabled\s*=\s*false"))continue;var p=Regex.Match(m.Groups[1].Value,@"(?m)^path\s*=\s*['"" ](.+?)['""]\s*$");if(p.Success){try{disabled.Add(Canon(p.Groups[1].Value.Replace("\\\\","\\")));}catch{}}}}
  foreach(var s in r.Skills){if(s.Path.IndexOf(@"\skills\.system\",StringComparison.OrdinalIgnoreCase)>=0)s.Source="系统";if(disabled.Contains(Canon(s.Path)))s.Enabled=false;s.Evidence=s.Registered?"本地服务已注册":s.Seen?"最近聊天可见":"仅磁盘发现";}
  r.Skills=r.Skills.OrderBy(s=>!s.Registered&&!s.Seen).ThenBy(s=>s.Name,StringComparer.OrdinalIgnoreCase).ToList();return r;
 }
}
public class Viewer:Form {
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,string l);
 TextBox query=new TextBox(), project=new TextBox(); ComboBox source=new ComboBox(), availability=new ComboBox(); DataGridView grid=new DataGridView();
 Label count=new Label(),name=new Label(),meta=new Label(),desc=new Label(); TextBox command=new TextBox(); RichTextBox body=new RichTextBox(),raw=new RichTextBox(),diagnostics=new RichTextBox();
 Button refresh,copy,folder,open;Inventory inventory;List<string> extras=new List<string>();TabControl detail=new TabControl();
 Color teal=Color.FromArgb(13,108,104),ink=Color.FromArgb(34,48,59);string settings;
 public Viewer(){
  Text="Codex 技能查看器";Size=new Size(Math.Min(1440,Screen.PrimaryScreen.WorkingArea.Width-60),Math.Min(900,Screen.PrimaryScreen.WorkingArea.Height-60));MinimumSize=new Size(1050,650);StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.None;Font=new Font("Microsoft YaHei UI",10);BackColor=Color.FromArgb(245,247,250);ForeColor=ink;
  settings=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexSkillViewer","settings.json");
  var outer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(22,16,22,12)};outer.RowStyles.Add(new RowStyle(SizeType.Absolute,65));outer.RowStyles.Add(new RowStyle(SizeType.Absolute,43));outer.RowStyles.Add(new RowStyle(SizeType.Absolute,45));outer.RowStyles.Add(new RowStyle(SizeType.Percent,100));outer.RowStyles.Add(new RowStyle(SizeType.Absolute,30));Controls.Add(outer);
  var head=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,280));
  var title=new Label{Text="我的 Codex Skills",Font=new Font(Font.FontFamily,22,FontStyle.Bold),Dock=DockStyle.Fill,ForeColor=teal};head.Controls.Add(title,0,0);
  var subtitle=new Label{Text="查看 · 搜索 · 复制调用方式\n每次打开自动重新扫描",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,ForeColor=Color.DimGray};head.Controls.Add(subtitle,1,0);outer.Controls.Add(head,0,0);
  var row=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=5};row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,84));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,106));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,116));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,114));
  row.Controls.Add(new Label{Text="项目目录",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);project.Dock=DockStyle.Fill;project.Margin=new Padding(0,6,10,0);project.Text=Directory.GetCurrentDirectory();row.Controls.Add(project,1,0);
  row.Controls.Add(Btn("选择项目",()=>{using(var d=new FolderBrowserDialog()){d.Description="选择要查询技能的项目目录";if(d.ShowDialog()==DialogResult.OK){project.Text=d.SelectedPath;Rescan();}}}),2,0);
  row.Controls.Add(Btn("添加目录",()=>{using(var d=new FolderBrowserDialog()){d.Description="添加其他技能来源目录（只读取）";if(d.ShowDialog()==DialogResult.OK){if(!extras.Contains(d.SelectedPath))extras.Add(d.SelectedPath);Rescan();}}}),3,0);
  refresh=Btn("重新扫描",Rescan);row.Controls.Add(refresh,4,0);outer.Controls.Add(row,0,1);
  var filters=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,200));filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,215));query.Dock=DockStyle.Fill;query.Margin=new Padding(0,7,12,0);query.AccessibleName="搜索技能";query.TextChanged+=(o,e)=>Filter();filters.Controls.Add(query,0,0);
  source.Items.AddRange(new object[]{"全部来源","我的 Skills","通用 Agents","系统","插件","项目","自选目录"});source.DropDownStyle=ComboBoxStyle.DropDownList;source.Dock=DockStyle.Fill;source.Margin=new Padding(0,7,12,0);source.SelectedIndex=0;source.SelectedIndexChanged+=(o,e)=>Filter();filters.Controls.Add(source,1,0);
  availability.Items.AddRange(new object[]{"已注册或聊天可见","本地服务已注册","最近聊天可见","仅磁盘发现","显示全部文件"});availability.DropDownStyle=ComboBoxStyle.DropDownList;availability.Dock=DockStyle.Fill;availability.Margin=new Padding(0,7,0,0);availability.SelectedIndex=0;availability.SelectedIndexChanged+=(o,e)=>Filter();filters.Controls.Add(availability,2,0);outer.Controls.Add(filters,0,2);
  var split=new SplitContainer{Size=new Size(1300,650),Dock=DockStyle.Fill,Panel1MinSize=420,Panel2MinSize=360,SplitterWidth=10,BackColor=BackColor};split.SplitterDistance=730;outer.Controls.Add(split,0,3);
  grid.Dock=DockStyle.Fill;grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.ReadOnly=true;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.RowHeadersVisible=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.MultiSelect=false;grid.AutoGenerateColumns=false;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=teal;grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;grid.ColumnHeadersDefaultCellStyle.Font=new Font(Font,FontStyle.Bold);grid.ColumnHeadersHeight=40;grid.RowTemplate.Height=64;grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(218,239,237);grid.DefaultCellStyle.SelectionForeColor=ink;grid.DefaultCellStyle.Padding=new Padding(7);grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(248,250,252);grid.GridColor=Color.FromArgb(235,239,243);
  grid.Columns.Add(new DataGridViewTextBoxColumn{Name="skill",HeaderText="技能 / 调用名",FillWeight=33});grid.Columns.Add(new DataGridViewTextBoxColumn{Name="description",HeaderText="简介 / 适用场景",FillWeight=49});grid.Columns.Add(new DataGridViewTextBoxColumn{Name="source",HeaderText="来源",FillWeight=18});grid.SelectionChanged+=(o,e)=>SelectSkill();split.Panel1.Controls.Add(grid);
  var right=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=7,BackColor=Color.White,Padding=new Padding(16,12,16,8)};right.RowStyles.Add(new RowStyle(SizeType.Absolute,44));right.RowStyles.Add(new RowStyle(SizeType.Absolute,62));right.RowStyles.Add(new RowStyle(SizeType.Absolute,108));right.RowStyles.Add(new RowStyle(SizeType.Absolute,37));right.RowStyles.Add(new RowStyle(SizeType.Absolute,43));right.RowStyles.Add(new RowStyle(SizeType.Percent,100));right.RowStyles.Add(new RowStyle(SizeType.Absolute,24));split.Panel2.Controls.Add(right);
  name.Dock=DockStyle.Fill;name.Font=new Font(Font.FontFamily,15,FontStyle.Bold);name.ForeColor=teal;name.AutoEllipsis=true;right.Controls.Add(name,0,0);meta.Dock=DockStyle.Fill;meta.ForeColor=Color.DimGray;meta.AutoEllipsis=true;right.Controls.Add(meta,0,1);desc.Dock=DockStyle.Fill;desc.AutoEllipsis=true;right.Controls.Add(desc,0,2);command.Dock=DockStyle.Fill;command.ReadOnly=true;command.BackColor=Color.FromArgb(240,248,247);command.Font=new Font("Consolas",11);right.Controls.Add(command,0,3);
  var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};copy=Btn("复制调用名",()=>Copy(false));open=Btn("打开原文件",()=>Launch(false));folder=Btn("打开文件夹",()=>Launch(true));foreach(var b in new[]{copy,open,folder}){b.Dock=DockStyle.None;b.Width=110;b.Height=31;b.Margin=new Padding(0,4,7,4);actions.Controls.Add(b);}right.Controls.Add(actions,0,4);
  detail.Dock=DockStyle.Fill;var a=new TabPage("使用说明");var braw=new TabPage("SKILL.md 原文");var diag=new TabPage("来源与检查");detail.TabPages.AddRange(new[]{a,braw,diag});foreach(var tb in new[]{body,raw,diagnostics}){tb.Dock=DockStyle.Fill;tb.ReadOnly=true;tb.BorderStyle=BorderStyle.None;tb.BackColor=Color.White;tb.Font=new Font(Font.FontFamily,10);tb.DetectUrls=false;}a.Controls.Add(body);braw.Controls.Add(raw);diag.Controls.Add(diagnostics);right.Controls.Add(detail,0,5);right.Controls.Add(new Label{Text="只读取技能文件；复制后在 Codex 中粘贴使用。",Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,8),ForeColor=Color.DimGray},0,6);
  count.Dock=DockStyle.Fill;count.TextAlign=ContentAlignment.MiddleLeft;count.ForeColor=Color.DimGray;outer.Controls.Add(count,0,4);
  try{if(File.Exists(settings)){var d=Scan.Obj(Scan.Json().DeserializeObject(File.ReadAllText(settings)));if(Directory.Exists(Scan.S(d,"project")))project.Text=Scan.S(d,"project");if(d.ContainsKey("extras"))extras=Scan.A(d["extras"]).Select(Convert.ToString).Where(Directory.Exists).ToList();}}catch{}
  Shown+=(o,e)=>{SendMessage(query.Handle,0x1501,new IntPtr(1),"搜索名称、用途或说明，例如：改稿、论文、PDF");Rescan();};
 }
 Button Btn(string text,Action action){var b=new Button{Text=text,Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=teal,Height=33,Margin=new Padding(3,4,3,4),Cursor=Cursors.Hand};b.FlatAppearance.BorderColor=Color.FromArgb(201,221,219);b.Click+=(o,e)=>{try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"操作未完成",MessageBoxButtons.OK,MessageBoxIcon.Information);}};return b;}
 async void Rescan(){
  if(!refresh.Enabled)return;refresh.Enabled=false;count.Text="正在读取本地可用目录与磁盘文件…";string cwd=project.Text.Trim();var dirs=extras.ToArray();
  try{inventory=await Task.Run(()=>Scan.Run(cwd,dirs));if(!inventory.Skills.Any(s=>s.Registered||s.Seen))availability.SelectedIndex=4;Filter();Directory.CreateDirectory(System.IO.Path.GetDirectoryName(settings));File.WriteAllText(settings,Scan.Json().Serialize(new{project=cwd,extras=extras}),Encoding.UTF8);
  }catch(Exception e){count.Text="扫描失败："+e.Message;}finally{refresh.Enabled=true;}
 }
 void Filter(){
  if(inventory==null)return;string term=query.Text.Trim();string src=Convert.ToString(source.SelectedItem);int mode=availability.SelectedIndex;
  var list=inventory.Skills.Where(s=>(src=="全部来源"||s.Source.StartsWith(src))&&(mode==4||mode==0&&(s.Registered||s.Seen)||mode==1&&s.Registered||mode==2&&s.Seen||mode==3&&!s.Registered&&!s.Seen)&&(term==""||(s.Name+" "+s.Display+" "+s.Description+" "+s.Path+" "+s.Body).IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0)).ToList();
  grid.Rows.Clear();foreach(var s in list){int i=grid.Rows.Add(s.Name,s.Description,s.Source+"\n"+s.Evidence);grid.Rows[i].Tag=s;}
  count.Text=String.Format("显示 {0} 项  ·  已注册 {1}  ·  最近聊天目录 {2}  ·  唯一文件 {3}  ·  扫描 {4:HH:mm:ss}{5}",list.Count,inventory.Skills.Count(s=>s.Registered),inventory.Skills.Count(s=>s.Seen),inventory.Skills.Count,DateTime.Now,inventory.Notes.Count>0?"  ·  "+inventory.Notes.Count+" 条提示（来源与检查）":"");
  if(grid.Rows.Count>0){grid.Rows[0].Selected=true;grid.CurrentCell=grid.Rows[0].Cells[0];}SelectSkill();
 }
 Skill Current(){return grid.CurrentRow==null?null:grid.CurrentRow.Tag as Skill;}
 void SelectSkill(){var s=Current();copy.Enabled=open.Enabled=folder.Enabled=s!=null;
  if(s==null){name.Text="搜索技能名称或用途";meta.Text="";desc.Text="输入关键词，例如：改稿、论文、抖音、PDF。";command.Text="";body.Text=raw.Text=diagnostics.Text="";return;}
  name.Text=s.Display;meta.Text=s.Name+"\n"+s.Source+" · "+s.Evidence+(s.Enabled?"":" · 已禁用");desc.Text=s.Description;command.Text="$"+s.Name;raw.Text=s.Raw;
  body.Text="怎样调用\n"+(s.Prompt!=""?s.Prompt:"请使用 $"+s.Name+" 处理我接下来提供的任务。")+"\n\n自动匹配\n"+s.Policy+"\n\n完整说明\n"+s.Body.Replace("\n","\r\n").Replace("\r\r\n","\r\n");
  diagnostics.Text="实际文件\n"+s.Path+"\n\n调用名称来自\n"+(s.Registered?"Codex skills/list（含插件命名空间）":s.Seen?"最近聊天的可用技能目录":"SKILL.md 的 name；未确认已注册")+"\n\n目录证据\n"+s.Evidence+"\n"+(s.Registered?"本地服务已为所选项目注册此技能。":"")+(s.Seen?"最近聊天见过该技能；不保证每个聊天都可用。":"")+"\n\n最近聊天目录时间\n"+inventory.SessionDate+"\n项目："+inventory.SessionCwd+"\n来源："+inventory.Session+"\n\n检查提示（启发式，不是错误判定）\n"+(s.Notice==""?"没有命中当前简单检查。":s.Notice)+"\n\n来源说明\n插件磁盘缓存可能包含未启用插件、旧版本或别名；已按文件真实路径去重。最近聊天目录只读取系统提供的技能清单，不执行技能。\n\n扫描提示\n"+String.Join("\n",inventory.Notes)+"\n\n自选目录\n"+String.Join("\n",extras);
 }
 void Copy(bool prompt){var s=Current();if(s==null)return;Clipboard.SetText(prompt?s.Prompt:"$"+s.Name);count.Text="已复制 $"+s.Name+"，请粘贴到 Codex 输入框，并补充你的任务。";}
 void Launch(bool dir){var s=Current();if(s==null)return;if(!File.Exists(s.Path))throw new Exception("文件已移动或删除，请重新扫描。");if(dir)Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+s.Path+"\""){UseShellExecute=true});else{try{Process.Start(new ProcessStartInfo(s.Path){UseShellExecute=true});}catch{Process.Start("notepad.exe","\""+s.Path+"\"");}}}

}
public static class Program {
 [STAThread] public static void Main(string[] args){
  try{
   if(args.Length>0&&args[0]=="--inventory"){
    var r=Scan.Run(args.Length>2?args[2]:Directory.GetCurrentDirectory(),new string[0]);File.WriteAllText(args[1],Scan.Json().Serialize(new{skills=r.Skills,notes=r.Notes,live=r.Live,session=r.Session,sessionDate=r.SessionDate,sessionCwd=r.SessionCwd}),Encoding.UTF8);return;
   }
   if(args.Length>0&&args[0]=="--self-test"){
    var d=Scan.Front("---\nname: 'test-skill'\ndescription: >\n  line one\n  line two\n---\n# Title");if(d["name"]!="test-skill"||d["description"]!="line one line two")throw new Exception("frontmatter block scalar");
    var q=Scan.Front("---\nname: demo\ndescription: \"quoted \\\"word\\\"\"\n---\n");if(q["description"]!="quoted \"word\"")throw new Exception("frontmatter quoted scalar");
    Console.WriteLine("PASS frontmatter folded and quoted values");return;
   }
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new Viewer());
  }catch(Exception e){if(args.Length>0){File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"last-error.txt"),e.ToString());Environment.ExitCode=1;}else MessageBox.Show(e.Message,"Codex 技能查看器");}
 }
}
