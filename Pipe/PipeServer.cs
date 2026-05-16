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
                        var lines = tree.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        return $"OK {lines.Length}|{string.Join("|", lines)}";

                    case "SKETCH":
                        if (string.IsNullOrWhiteSpace(arg)) return "ERR usage: SKETCH <FRONT|TOP|RIGHT>";
                        string sketchErr = Modeling.ModelingOps.StartSketch(app, arg);
                        return sketchErr == null ? $"OK sketch on {arg}" : $"ERR {sketchErr}";

                    case "RECT":
                        // RECT <w> <h> [cx] [cy]
                        var rp = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (rp.Length < 2) return "ERR usage: RECT <w_mm> <h_mm> [cx_mm] [cy_mm]";
                        if (!double.TryParse(rp[0], out double rw)) return "ERR bad w";
                        if (!double.TryParse(rp[1], out double rh)) return "ERR bad h";
                        double rcx = rp.Length > 2 && double.TryParse(rp[2], out rcx) ? rcx : 0;
                        double rcy = rp.Length > 3 && double.TryParse(rp[3], out rcy) ? rcy : 0;
                        string rectErr = Modeling.ModelingOps.DrawRect(app, rw, rh, rcx, rcy);
                        return rectErr == null ? $"OK rect {rw}x{rh}mm" : $"ERR {rectErr}";

                    case "CIRCLE":
                        var cp = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (cp.Length < 1) return "ERR usage: CIRCLE <d_mm> [cx_mm] [cy_mm]";
                        if (!double.TryParse(cp[0], out double cd)) return "ERR bad d";
                        double ccx = cp.Length > 1 && double.TryParse(cp[1], out ccx) ? ccx : 0;
                        double ccy = cp.Length > 2 && double.TryParse(cp[2], out ccy) ? ccy : 0;
                        string circleErr = Modeling.ModelingOps.DrawCircle(app, cd, ccx, ccy);
                        return circleErr == null ? $"OK circle d={cd}mm" : $"ERR {circleErr}";

                    case "LINE":
                        var lp = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (lp.Length < 4) return "ERR usage: LINE <x1_mm> <y1_mm> <x2_mm> <y2_mm>";
                        if (!double.TryParse(lp[0], out double x1)) return "ERR bad x1";
                        if (!double.TryParse(lp[1], out double y1)) return "ERR bad y1";
                        if (!double.TryParse(lp[2], out double x2)) return "ERR bad x2";
                        if (!double.TryParse(lp[3], out double y2)) return "ERR bad y2";
                        string lineErr = Modeling.ModelingOps.DrawLine(app, x1, y1, x2, y2);
                        return lineErr == null ? $"OK line ({x1},{y1})-({x2},{y2})" : $"ERR {lineErr}";

                    case "EXTRUDE":
                        if (!double.TryParse(arg, out double ed)) return "ERR usage: EXTRUDE <depth_mm>";
                        string extErr = Modeling.ModelingOps.Extrude(app, ed);
                        return extErr == null ? $"OK extrude {ed}mm" : $"ERR {extErr}";

                    case "EXTRUDE_CUT":
                        if (!double.TryParse(arg, out double ecd)) return "ERR usage: EXTRUDE_CUT <depth_mm>";
                        string cutErr = Modeling.ModelingOps.ExtrudeCut(app, ecd);
                        return cutErr == null ? $"OK extrude cut {ecd}mm" : $"ERR {cutErr}";

                    case "RENAME_FEATURE":
                        var rf = arg.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (rf.Length < 2) return "ERR usage: RENAME_FEATURE <old_name> <new_name>";
                        string rfErr = Modeling.ModelingOps.RenameFeature(app, rf[0], rf[1]);
                        return rfErr == null ? $"OK renamed {rf[0]} -> {rf[1]}" : $"ERR {rfErr}";

                    case "DELETE_FEATURE":
                        if (string.IsNullOrWhiteSpace(arg)) return "ERR usage: DELETE_FEATURE <name>";
                        string delErr = Modeling.ModelingOps.DeleteFeature(app, arg);
                        return delErr == null ? $"OK deleted {arg}" : $"ERR {delErr}";

                    case "DETAIL":
                        string detErr = Modeling.FeatureInspector.GetDetails(app, out string det);
                        if (detErr != null) return "ERR " + detErr;
                        if (string.IsNullOrEmpty(det)) return "OK (empty)";
                        var dl = det.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        return $"OK {dl.Length}|{string.Join("|", dl)}";

                    default:
                        return $"ERR unknown cmd: {cmd}";
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
