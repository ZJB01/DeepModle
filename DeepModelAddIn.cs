using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using DeepModel.Agent;
using DeepModel.Modeling;
using DeepModel.Pipe;
using DeepModel.UI;

namespace DeepModel
{
    [Guid("D9E2F1A4-8B7C-6E5D-3F2A-1C0B9D8E7F6A")]
    [ComVisible(true)]
    [ProgId("DeepModel.AddIn")]
    public class DeepModelAddIn : ISwAddin
    {
        private ISldWorks _swApp;
        private int _cookie;
        private PipeServer _pipeServer;
        private AgentEngine _agentEngine;

        private const int GroupUID = 50001;
        private const int CmdUID_Cube = 50002;
        private const int CmdUID_Console = 50003;
        private const int CmdUID_Agent = 50004;
        private const string TabName = "DeepModel";
        private const string PipeName = "DeepModel_Pipe";

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            _swApp = (ISldWorks)ThisSW;
            _cookie = Cookie;
            _swApp.SetAddinCallbackInfo2(0, this, _cookie);

            int docType = (int)swDocumentTypes_e.swDocPART;

            _swApp.AddMenuItem2(docType, _cookie, "生成正方体", -1,
                "CreateCube", "CreateCubeEnable", "输入边长参数，生成正方体");

            CreateCommandTab(docType);
            StartPipeServer();

            return true;
        }

        public bool DisconnectFromSW()
        {
            _pipeServer?.Dispose();
            try
            {
                var cmdMgr = _swApp.GetCommandManager(_cookie);
                if (cmdMgr != null)
                {
                    cmdMgr.RemoveCommandGroup(GroupUID);
                    var tab = cmdMgr.GetCommandTab((int)swDocumentTypes_e.swDocPART, TabName);
                    if (tab != null) cmdMgr.RemoveCommandTab(tab);
                }
            }
            catch { }
            return true;
        }

        private void CreateCommandTab(int docType)
        {
            var cmdMgr = _swApp.GetCommandManager(_cookie);
            if (cmdMgr == null) return;

            int err = 0;
            object gid = cmdMgr.CreateCommandGroup2(
                GroupUID, TabName, TabName,
                "参数化建模 + Agent", -1, true, ref err);

            var cg = (CommandGroup)gid;

            int cmd1 = cg.AddCommandItem2(
                "生成正方体", -1,
                "输入边长参数生成正方体", TabName, -1,
                "CreateCube", "", CmdUID_Cube,
                (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem));

            int cmd2 = cg.AddCommandItem2(
                "Pipe控制台", -1,
                "打开 Pipe 命令控制台", TabName, -1,
                "PipeConsole", "", CmdUID_Console,
                (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem));

            int cmd3 = cg.AddCommandItem2(
                "AI Agent", -1,
                "打开 AI Agent 对话窗口", TabName, -1,
                "AgentChat", "", CmdUID_Agent,
                (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem));

            var tab = cmdMgr.AddCommandTab(docType, TabName);
            var box = tab.AddCommandTabBox();
            box.AddCommands(new int[] { cmd1, cmd2, cmd3 }, new int[] { 0, 0, 0 });

            cg.HasToolbar = true;
            cg.HasMenu = true;
            cg.Activate();
            tab.Visible = true;
            tab.Active = true;
        }

        private void StartPipeServer()
        {
            string logDir = System.Environment.GetFolderPath(
                System.Environment.SpecialFolder.LocalApplicationData);
            _pipeServer = new PipeServer(PipeName, logDir);
            _pipeServer.Start();
        }

        private AgentEngine GetOrCreateAgent()
        {
            if (_agentEngine == null)
            {
                var cfg = AgentConfig.LoadAndOpen();
                if (string.IsNullOrWhiteSpace(cfg.ApiKey))
                {
                    MessageBox.Show(
                        $"配置文件已打开。请将 ApiKey 改为你的 DeepSeek API Key:\n\n" +
                        $"{AgentConfig.ConfigPath}\n\n" +
                        "注意: JSON key 必须用 PascalCase（ApiKey, Model, BaseUrl...）",
                        "DeepModel Agent - 配置 API Key");
                    return null;
                }

                _agentEngine = new AgentEngine(cfg, msg =>
                {
                    // 同步发送 pipe 命令。AgentEngine 在后台线程调用此方法。
                    using (var c = new System.IO.Pipes.NamedPipeClientStream(".", PipeName,
                        System.IO.Pipes.PipeDirection.InOut))
                    {
                        c.Connect(5000);
                        using (var r = new System.IO.StreamReader(c))
                        using (var w = new System.IO.StreamWriter(c) { AutoFlush = true })
                        {
                            w.WriteLine(msg);
                            return r.ReadLine() ?? "ERR no response";
                        }
                    }
                });
            }
            return _agentEngine;
        }

        // ===== SW 回调 =====

        public int CreateCube()
        {
            using (var dlg = new CubeDialog())
            {
                if (dlg.ShowDialog() != DialogResult.OK) return 0;
                string err = CubeBuilder.Build(_swApp, dlg.SideLengthMm);
                if (err != null) MessageBox.Show(err, "DeepModel");
                else MessageBox.Show($"正方体 {dlg.SideLengthMm}mm 创建成功！", "DeepModel");
                return 0;
            }
        }

        public int PipeConsole()
        {
            if (_pipeServer == null) { MessageBox.Show("Pipe 未启动。"); return 0; }
            new AgentForm(PipeName).Show();
            return 0;
        }

        public int AgentChat()
        {
            try
            {
                if (_pipeServer == null) { MessageBox.Show("Pipe 未启动。"); return 0; }
                var engine = GetOrCreateAgent();
                if (engine == null) return 0;
                engine.Reset();
                var form = new AgentChatForm(engine);
                engine.OnStatus = s => form.SetStatusText(s, Color.FromArgb(255, 200, 50));
                form.Show();
                return 0;
            }
            catch (Exception ex) { MessageBox.Show("Agent error: " + ex.Message); return 0; }
        }

        public int CreateCubeEnable() => 1;
        public int PipeConsoleEnable() => 1;
        public int AgentChatEnable() => 1;
    }
}
