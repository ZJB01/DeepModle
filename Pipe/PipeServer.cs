using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using SolidWorks.Interop.sldworks;

namespace DeepModel.Pipe
{
    /// <summary>
    /// Named Pipe 服务端 —— 后台 STA 线程监听，COM marshaling 到主线程。
    /// 协议（纯文本，零依赖）：CUBE &lt;边长mm&gt; → OK &lt;msg&gt; / ERR &lt;msg&gt;
    /// </summary>
    public class PipeServer : IDisposable
    {
        private readonly string _pipeName;
        private readonly string _logPath;
        private Thread _thread;
        private volatile bool _running;

        public PipeServer(string pipeName, string logDir)
        {
            _pipeName = pipeName;
            _logPath = Path.Combine(logDir, $"{pipeName}.log");
        }

        public void Start()
        {
            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = _pipeName };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            Log("Started (STA, inline)");
        }

        public void Dispose()
        {
            _running = false;
            _thread = null;
        }

        private void Loop()
        {
            while (_running)
            {
                try
                {
                    using (var server = new NamedPipeServerStream(_pipeName,
                        PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.None))
                    {
                        server.WaitForConnection();
                        using (var reader = new StreamReader(server))
                        using (var writer = new StreamWriter(server) { AutoFlush = true })
                        {
                            string req = reader.ReadLine();
                            Log($"REQ: \"{req}\"");
                            if (string.IsNullOrWhiteSpace(req))
                            { writer.WriteLine("ERR empty"); continue; }

                            string resp = Dispatch(req);
                            Log($"RESP: \"{resp}\"");
                            writer.WriteLine(resp);
                            writer.Flush();
                        }
                        server.Disconnect();
                    }
                }
                catch (Exception ex) { Log($"Error: {ex.Message}"); Thread.Sleep(500); }
            }
            Log("Stopped");
        }

        /// <summary>协议分发。支持: CUBE, NEW, NAME, RENAME, TREE</summary>
        protected virtual string Dispatch(string request)
        {
            try
            {
                var parts = request.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                string cmd = parts[0].ToUpperInvariant();
                string arg = parts.Length > 1 ? parts[1] : "";

                ISldWorks app = (ISldWorks)Marshal.GetActiveObject("SldWorks.Application");

                switch (cmd)
                {
                    case "CUBE":
                        if (!double.TryParse(arg, out double mm) || mm <= 0)
                            return "ERR bad value, usage: CUBE <mm>";
                        string cubeErr = Modeling.CubeBuilder.Build(app, mm);
                        return cubeErr == null ? $"OK cube {mm}mm created" : $"ERR {cubeErr}";

                    case "NEW":
                        string newErr = Modeling.DocumentOps.CreateNewPart(app);
                        return newErr == null ? "OK new part created" : $"ERR {newErr}";

                    case "NAME":
                        string nameErr = Modeling.DocumentOps.GetTitle(app, out string title);
                        return nameErr == null ? $"OK {title}" : $"ERR {nameErr}";

                    case "RENAME":
                        if (string.IsNullOrWhiteSpace(arg)) return "ERR usage: RENAME <new_name>";
                        string renErr = Modeling.DocumentOps.SetTitle(app, arg);
                        return renErr == null ? $"OK renamed to {arg}" : $"ERR {renErr}";

                    case "TREE":
                        string treeErr = Modeling.DocumentOps.GetDesignTree(app, out string tree);
                        if (treeErr != null) return $"ERR {treeErr}";
                        if (string.IsNullOrEmpty(tree)) return "OK (empty)";
                        // 单行输出，用 | 分隔特征
                        var lines = tree.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        return $"OK {lines.Length}|{string.Join("|", lines)}";

                    default:
                        return $"ERR unknown cmd: {cmd}. Usage: CUBE|NEW|NAME|RENAME|TREE";
                }
            }
            catch (Exception ex) { return $"ERR {ex.GetType().Name}: {ex.Message}"; }
        }

        private void Log(string msg)
        {
            try { File.AppendAllText(_logPath, $"{DateTime.Now:HH:mm:ss.fff} [{_pipeName}] {msg}\n"); }
            catch { }
        }
    }
}
