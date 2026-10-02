# Codex Skill Viewer · Codex 技能查看器

一个轻量的 Windows 桌面工具，帮助你看清：**安装了哪些 Codex Skills、各自做什么、位于哪里，以及怎样调用。**

原生 C# / Windows Forms，免安装，不需要 Python、Node.js 或 API Key。

## 下载与使用

从 [Releases 下载页面](https://github.com/fangeshequn/codex-skill-viewer/releases/latest) 下载 `CodexSkillViewer-v1.0.0-windows-x64.zip`，解压后双击 `CodexSkillViewer.exe`。

运行环境：Windows x64、.NET Framework 4.8。界面为中文。此项目是独立社区工具，与 OpenAI 没有隶属关系。

1. 打开后自动扫描；搜索名称、用途或说明，例如“PDF”“改稿”“论文”。
2. 点击技能，查看简介、使用说明、完整 `SKILL.md` 与文件位置。
3. 点击“复制调用名”，粘贴到 Codex，并补充你的具体任务。
4. 使用“打开原文件”“打开文件夹”查看实际来源。
5. 新增、修改或删除技能后，点击“重新扫描”。
6. 其他项目使用“选择项目”；自定义技能位置使用“添加目录”。

## 功能

- 动态扫描真实技能来源，不内置固定技能名单。
- 搜索名称、简介、路径和说明正文。
- 按来源与可用证据筛选。
- 展示名称、简介、调用名、文件路径、原文和自动匹配配置。
- 读取用户级、通用 Agents、系统、插件与项目技能。
- 识别本地禁用配置，处理目录链接并避免链接循环。
- 只读查看技能，不运行其中的脚本，也不修改或删除技能。

## 为什么有不同的技能数量

工具区分三种证据：

| 标记 | 含义 |
|---|---|
| 本地服务已注册 | 本机 Codex `skills/list` 为所选项目返回的技能 |
| 最近聊天可见 | 最近本地聊天的开发者技能目录中曾出现；是有时间与项目来源的快照 |
| 仅磁盘发现 | 找到 `SKILL.md`，但以上两种来源没有确认其可用性 |

磁盘上的旧插件、缓存、同名文件不一定能在当前聊天使用。最近聊天中的插件也不一定出现在独立启动的本地服务目录中。因此，工具不将任何一个总数等同于所有聊天实时可用的技能。

默认显示已注册或最近聊天可见的技能；两者都未发现时自动显示磁盘结果。

## 扫描来源

- `%CODEX_HOME%\skills`，未设置时使用 `%USERPROFILE%\.codex\skills`。
- `%USERPROFILE%\.agents\skills`。
- 所选项目及祖先目录的 `.agents\skills`、兼容目录 `.codex\skills`，遇到 Git 根目录停止向上查找。
- Codex 插件缓存中的各版本 `skills` 目录。
- 用户手动添加的目录。
- 本机 Codex 服务目录与最近聊天开头的技能清单。

不会全盘遍历所有项目，也不会把归档备份自动算作已安装技能。

## 数据与隐私

本程序不调用 AI 模型，不上传技能或聊天内容，也不内置遥测服务。

为了读取注册目录，它可能启动本机已有的 `codex.exe app-server --stdio`，调用 `initialize` 与只读 `skills/list`，然后结束这个子进程。Codex 自身可能根据其配置维护缓存或使用网络。未找到该服务或查询失败时，程序仍可显示磁盘和最近聊天目录。

最近聊天检测会读取最多 24 个本地会话文件各自开头最多 30 行，仅提取开发者消息中的技能列表。不会把用户对话作为界面内容或上传。聊天日志格式及内部服务位置可能随 Codex 更新而变化；这项能力是尽力识别。

程序自己的设置保存在 `%LOCALAPPDATA%\CodexSkillViewer\settings.json`，包括上次选择的项目与额外扫描目录。

命令行导出的清单会包含本机路径和技能原文，发布这类清单前请自行检查。仓库和发行包不包含任何用户的技能清单、聊天记录或凭据。

## 从源码构建

在 Windows PowerShell 中：

```powershell
git clone https://github.com/fangeshequn/codex-skill-viewer.git
cd codex-skill-viewer
.\build.ps1
.\dist\CodexSkillViewer.exe
```

构建使用 Windows 自带 .NET Framework C# 编译器，不下载 NuGet 包。输出位于 `dist`。源码使用 Windows Forms、System.Drawing 与 System.Web.Extensions。

运行验证：

```powershell
.\tests\verify.ps1
```

测试在独立临时目录创建技能样本，验证新增、修改、禁用、目录链接循环以及删除后的扫描行为；不会修改已安装技能。

## 命令行

```powershell
# 导出当前项目的清单，输出可能包含本机路径和技能原文
.\dist\CodexSkillViewer.exe --inventory inventory.json C:\YourProject

# 基本元数据解析检查
.\dist\CodexSkillViewer.exe --self-test
```

程序是 GUI 子系统可执行文件。脚本需要等待结果时请使用 `Start-Process -Wait`，并检查退出码与输出文件。

## 当前边界

- 仅支持 Windows x64；采用 Windows 整体缩放以保持布局一致。
- Markdown 以文本显示；不是完整 Markdown 渲染器。
- YAML / TOML 读取只覆盖目前使用的简单元数据形式，不是通用解析器。
- “简介较长”是字符数量提醒，不是精确 token 计算或质量评价。
- 不负责安装、删除、禁用或自动运行技能。
- 本地服务发现目前检查 Codex 桌面版的两个常见位置；纯 CLI 或未来版本可能只能使用磁盘扫描。
- 下载程序未做代码签名；也可以从此仓库自行构建。

## 贡献与许可

欢迎提交 Issue 和 Pull Request。请勿在 Issue 附上未经检查的完整技能清单或会话日志。

以 [MIT License](LICENSE) 开源。软件允许自由使用、修改和分发；不附带任何已安装的第三方 Skill，其许可仍由原作者决定。
