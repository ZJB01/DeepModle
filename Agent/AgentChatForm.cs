using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace DeepModel.Agent
{
    internal class AgentChatForm : Form
    {
        private readonly AgentEngine _engine;
        private RichTextBox _txtChat;
        private TextBox _txtInput;
        private Button _btnSend;
        private Label _lblStatus;
        private bool _busy;

        private static readonly string[] DefaultPrompts =
        {
            "新建一个零件，在里面画一个边长100mm的正方体",
            "创建一个名为底板的新零件，并在其中绘制100*50*20的长方体",
            "画一个直径为50mm、高度为80mm的圆柱体",
            "先新建零件，然后生成一个边长为200mm的立方体",
            "帮我创建一个机械底板：200mm长、100mm宽、10mm厚的长方体",
        };

        private static readonly Random _rng = new Random();

        public AgentChatForm(AgentEngine engine)
        {
            _engine = engine;
            Text = "DeepModel Agent";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(600, 530);
            MinimumSize = new Size(400, 350);

            // 状态标签（顶部）
            _lblStatus = new Label
            {
                Text = "就绪",
                Location = new Point(8, 4),
                AutoSize = true,
                ForeColor = Color.FromArgb(150, 150, 150),
                Font = new Font("Microsoft YaHei", 8),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(_lblStatus);

            // 聊天历史
            _txtChat = new RichTextBox
            {
                Location = new Point(8, 20),
                Width = 584,
                Height = 390,
                ReadOnly = true,
                BackColor = Color.FromArgb(35, 35, 40),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Microsoft YaHei", 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BorderStyle = BorderStyle.None
            };
            Controls.Add(_txtChat);

            // 输入区域
            var panel = new Panel
            {
                Location = new Point(8, 416),
                Width = 584,
                Height = 76,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(50, 50, 55)
            };
            Controls.Add(panel);

            _txtInput = new TextBox
            {
                Location = new Point(8, 8),
                Width = 478,
                Height = 60,
                Multiline = true,
                BackColor = Color.FromArgb(60, 60, 65),
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei", 10),
                BorderStyle = BorderStyle.None,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
                Text = DefaultPrompts[_rng.Next(DefaultPrompts.Length)]
            };
            _txtInput.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.Enter)
                { SendMessage(); e.SuppressKeyPress = true; }
            };
            panel.Controls.Add(_txtInput);

            _btnSend = new Button
            {
                Text = "发送",
                Location = new Point(492, 8),
                Width = 84,
                Height = 44,
                Font = new Font("Microsoft YaHei", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(70, 130, 220),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            _btnSend.FlatAppearance.BorderSize = 0;
            _btnSend.Click += (s, e) => SendMessage();
            panel.Controls.Add(_btnSend);

            // 随机提示按钮
            var btnRandom = new Button
            {
                Text = "换一个",
                Location = new Point(492, 52),
                Width = 84,
                Height = 20,
                Font = new Font("Microsoft YaHei", 7),
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom
            };
            btnRandom.Click += (s, e) =>
            {
                _txtInput.Text = DefaultPrompts[_rng.Next(DefaultPrompts.Length)];
            };
            panel.Controls.Add(btnRandom);

            // 清空按钮
            var btnClear = new Button
            {
                Text = "清空",
                Location = new Point(8, 498),
                Width = 50,
                Height = 24,
                Font = new Font("Microsoft YaHei", 8),
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnClear.Click += (s, e) =>
            {
                _txtChat.Clear();
                _engine.Reset();
                _txtInput.Text = DefaultPrompts[_rng.Next(DefaultPrompts.Length)];
                ChatAppend("(已清空)\n", Color.Gray);
            };
            Controls.Add(btnClear);

            ChatAppend("DeepModel Agent ready.\n", Color.Gray);
        }

        private void SendMessage()
        {
            if (_busy) return;
            string text = _txtInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _txtInput.Clear();
            _busy = true;
            _btnSend.Text = "...";
            ChatAppend($"> {text}\n", Color.Cyan);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string reply = _engine.Chat(text);
                    this.BeginInvoke((Action)(() =>
                    {
                        ChatAppend($"\n{reply}\n\n", Color.White);
                        _lblStatus.Text = "就绪";
                        _lblStatus.ForeColor = Color.FromArgb(150, 150, 150);
                    }));
                }
                catch (Exception ex)
                {
                    this.BeginInvoke((Action)(() =>
                    {
                        ChatAppend($"\n[ERR] {ex.Message}\n\n", Color.Red);
                        _lblStatus.Text = "错误";
                        _lblStatus.ForeColor = Color.Red;
                    }));
                }
                finally
                {
                    this.BeginInvoke((Action)(() =>
                    {
                        _busy = false;
                        _btnSend.Text = "发送";
                    }));
                }
            });
        }

        private void ChatAppend(string msg, Color color)
        {
            int start = _txtChat.TextLength;
            _txtChat.AppendText(msg);
            _txtChat.Select(start, msg.Length);
            _txtChat.SelectionColor = color;
            _txtChat.Select(_txtChat.TextLength, 0);
            _txtChat.ScrollToCaret();
        }

        // 供 AgentEngine 回调更新状态
        public void SetStatus(string status)
        {
            if (InvokeRequired)
            { BeginInvoke((Action)(() => SetStatus(status))); return; }
            _lblStatus.Text = status;
            _lblStatus.ForeColor = status == "就绪"
                ? Color.FromArgb(150, 150, 150)
                : Color.FromArgb(255, 200, 50);
        }
    }
}
