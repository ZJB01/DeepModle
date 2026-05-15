using System;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
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
            Controls.Add(new Label
            {
                Text = "协议: CUBE <边长mm>   |   示例: CUBE 200",
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
                Height = 280,
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

            Log("DeepModel Agent 就绪。");
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
                        return r.ReadLine() ?? "ERR no response";
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
