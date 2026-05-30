# DeepModel —— 基于大语言模型的 SolidWorks 智能建模助手

自然语言驱动 SolidWorks 三维建模的 AI Agent 插件。基于 .NET Framework 4.8 + SolidWorks 2023 API + DeepSeek Function Calling。

![DeepModel](Capture001.png)

## 功能

- **AI Agent 对话** — 集成 DeepSeek API，通过自然语言指令自动执行建模操作
- **12 个建模工具** — new_part, sketch_start, draw_rect, draw_circle, draw_line, extrude, extrude_cut, rename_feature, delete_feature, get_tree...
- **特征检测** — 读取设计树详情（拉伸深度、显示尺寸、特征类型）
- **多对话管理** — 项目目录 + JSON 持久化，支持对话切换/历史回溯
- **Named Pipe 通信** — Agent ↔ SolidWorks 双向文本协议，零依赖，跨语言兼容
- **Markdown 渲染** — AI 回复支持粗体、行内代码
- **工具调用追踪** — 每次回复末尾自动列出工具调用顺序表

## 快速开始

### 环境要求

- Windows 10/11 x64
- SolidWorks 2023+
- .NET Framework 4.8
- DeepSeek API Key ([获取](https://platform.deepseek.com/))

### 配置

编辑 `%LOCALAPPDATA%\DeepModel\agent_config.json`（或点击 Agent 窗口左侧 `cfg` 按钮）：

```json
{
  "ApiKey": "sk-your-deepseek-key",
  "Model": "deepseek-chat",
  "BaseUrl": "https://api.deepseek.com/v1",
  "ContextTokens": 65536,
  "MaxTokens": 4096,
  "Temperature": 0.3,
  "MaxToolRounds": 50
}
```

### 安装

```batch
# 以管理员身份运行
dotnet build DeepModel.csproj
register.bat
```

### 使用

1. 启动 SolidWorks，打开或新建零件文档
2. 点击 `DeepModel` 选项卡中的 `AI Agent` 按钮
3. 输入自然语言指令，如："新建一个边长 100mm 的正方体"

## 项目结构

```
├── DeepModelAddIn.cs         # SW 插件入口 (ISwAddin)
├── Modeling/
│   ├── CubeBuilder.cs        # 正方体建模
│   ├── DocumentOps.cs        # 文档操作 (NEW/RENAME/TREE)
│   ├── FeatureInspector.cs   # 特征参数检测
│   └── ModelingOps.cs        # 直接 API 建模命令
├── Pipe/
│   └── PipeServer.cs         # Named Pipe 服务端
├── Agent/
│   ├── AgentEngine.cs        # DeepSeek API + Tool Calling 循环
│   ├── AgentChatForm.cs      # Agent 聊天窗口 (Markdown + 多对话)
│   ├── AgentConfig.cs        # JSON 配置文件模型
│   ├── ConversationManager.cs # 多对话持久化管理
│   └── SystemPrompt.cs       # 系统提示词
├── UI/
│   ├── AgentForm.cs          # Pipe 命令控制台
│   └── CubeDialog.cs         # 参数输入弹窗
├── register.bat              # COM 注册脚本 (管理员运行)
└── unregister.bat            # COM 卸载脚本
```

## Pipe 协议（15 条命令）

```
请求:  CMD arg1 arg2 ...
响应:  OK result / ERR message

NEW                          → OK new part created
NAME                         → OK Part1.SLDPRT
RENAME MyPart                → OK renamed to MyPart
SKETCH FRONT                 → OK sketch on FRONT
RECT 100 50                  → OK rect 100x50mm
CIRCLE 50                    → OK circle d=50mm
LINE 0 0 100 0               → OK line (0,0)-(100,0)
EXTRUDE 20                   → OK extrude 20mm
EXTRUDE_CUT 10               → OK extrude cut 10mm
RENAME_FEATURE old new       → OK renamed old -> new
DELETE_FEATURE name          → OK deleted name
TREE                         → OK n|feat1|feat2|...
DETAIL                       → OK n|detail1|detail2|...
CUBE 100                     → OK cube 100mm created
```

## 架构

```
用户 (自然语言)
    │
AgentEngine (DeepSeek API + Function Calling)
    │ Named Pipe (文本协议)
PipeServer (STA 线程, COM Marshaling)
    │ COM Interop
SolidWorks API (ISldWorks, IModelDoc2, IFeatureManager...)
```

## 技术栈

| 层 | 技术 |
|---|---|
| AI 引擎 | DeepSeek-chat (128K) + Function Calling |
| 通信 | Named Pipe (文本协议, 零依赖) |
| CAD 接口 | SolidWorks COM API (ISldWorks) |
| 插件框架 | ISwAddin + COM Interop + 强名称签名 |
| UI | WinForms (TopMost, Markdown) |
| 持久化 | JSON 文件系统 |

## TODO

- [ ] 修复关闭 Agent 窗口后 SW 工具栏按钮变灰
- [ ] 单草图多 Draw 命令时挤出草图选择优化
- [ ] MCP (Model Context Protocol) 标准化接口
- [ ] 高级建模特征（阵列、扫描、放样）
- [ ] 视觉反馈闭环（截图→AI分析→修正）

## License

MIT
