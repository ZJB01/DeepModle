using System;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace DeepModel.UI
{
    /// <summary>
    /// Agent 模拟控制台 —— 通过 Named Pipe 发送建模命令。
    /// 后台线程收发，避免阻塞 SolidWorks 主线程导致 COM 死锁。
    /// </summary>
    internal class AgentForm : Form
    {
        private readonly TextBox _txtCmd;
        private readonly RichTextBox _txtLog;
        private readonly Button _btnSend;
        private readonly string _pipeName;

        public AgentForm(string pipeName)
        {
            _pipeName = pipeName;
            Text = $"DeepModel Agent - {pipeName}";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(520, 380);

            int y = 10;

            // Quick buttons
            var quickCmds = new[] {
                ("NEW",    "新建零件"),
                ("NAME",   "获取名称"),
                ("TREE",   "设计树"),
                ("CUBE 100","正方体100"),
                ("CUBE 200","正方体200"),
            };
            int bx = 12;
            foreach (var (cmd, label) in quickCmds)
            {
                var btn = new Button
                {
                    Text = label,
                    Location = new Point(bx, y),
                    Width = 78,
                    Height = 24,
                    Tag = cmd
                };
                btn.Click += (s, e2) => Send(((Button)s).Tag.ToString());
                Controls.Add(btn);
                bx += 82;
            }
            y += 30;

            Controls.Add(new Label
            {
                Text = "命令: CUBE <mm> | NEW | NAME | RENAME <name> | TREE",
                Location = new Point(12, y),
                AutoSize = true
            });
            y += 22;

            _txtCmd = new TextBox
            {
                Text = "CUBE 200",
                Location = new Point(12, y),
                Width = 370
            };
            Controls.Add(_txtCmd);

            _btnSend = new Button
            {
                Text = "发送",
                Location = new Point(390, y - 1),
                Width = 110,
                Height = 25
            };
            _btnSend.Click += OnSend;
            Controls.Add(_btnSend);
            y += 35;

            _txtLog = new RichTextBox
            {
                Location = new Point(12, y),
                Width = 490,
                Height = 250,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 10)
            };
            Controls.Add(_txtLog);

            _txtCmd.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.Enter) OnSend(null, null);
            };

            Log("DeepModel Agent ready.");
        }

        private void Send(string cmd)
        {
            if (string.IsNullOrEmpty(cmd)) return;
            _txtCmd.Text = cmd;
            OnSend(null, null);
        }

        private void OnSend(object sender, EventArgs e)
        {
            string cmd = _txtCmd.Text.Trim();
            if (string.IsNullOrEmpty(cmd)) return;

            Log($">>> {cmd}");
            _btnSend.Enabled = false;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                string resp = PipeSend(cmd);
                this.BeginInvoke((Action)(() =>
                {
                    Log($"<<< {resp}\n");
                    _btnSend.Enabled = true;
                }));
            });
        }

        private string PipeSend(string msg)
        {
            try
            {
                using (var c = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut))
                {
                    c.Connect(5000);
                    using (var r = new StreamReader(c))
                    using (var w = new StreamWriter(c) { AutoFlush = true })
                    {
                        w.WriteLine(msg);
                        string resp = r.ReadLine() ?? "ERR no response";
                        // 格式化 TREE 响应：将 | 分隔展开为多行
                        if (resp.StartsWith("OK ") && resp.Contains("|"))
                        {
                            var parts = resp.Split('|');
                            if (parts.Length > 2)
                                resp = parts[0] + "\n  " + string.Join("\n  ", parts.Skip(1));
                        }
                        return resp;
                    }
                }
            }
            catch (TimeoutException) { return "ERR connect timeout"; }
            catch (Exception ex) { return $"ERR {ex.Message}"; }
        }

        private void Log(string msg)
        {
            _txtLog.AppendText(msg + "\n");
            _txtLog.ScrollToCaret();
        }
    }
}
