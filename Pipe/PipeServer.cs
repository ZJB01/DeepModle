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

        /// <summary>解析命令并执行。子类可重写此方法扩展协议。</summary>
        protected virtual string Dispatch(string request)
        {
            try
            {
                var parts = request.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) return "ERR format: CUBE <mm>";

                string cmd = parts[0].ToUpperInvariant();
                if (cmd != "CUBE") return $"ERR unknown cmd: {cmd}";

                if (!double.TryParse(parts[1], out double mm) || mm <= 0)
                    return "ERR bad value";

                ISldWorks app = (ISldWorks)Marshal.GetActiveObject("SldWorks.Application");
                string err = Modeling.CubeBuilder.Build(app, mm);
                return err == null ? $"OK cube {mm}mm created" : $"ERR {err}";
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
