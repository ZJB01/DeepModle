using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace DeepModel.Agent
{
    internal class AgentChatForm : Form
    {
        private readonly AgentEngine _engine;
        private readonly ConversationManager _convMgr;
        private RichTextBox _txtChat;
        private TextBox _txtInput;
        private Button _btnSend;
        private Label _lblStatus;
        private ListBox _lstConv;
        private ComboBox _cmbProject;
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
            _convMgr = new ConversationManager();
            BackColor = Color.FromArgb(30, 30, 36);

            Text = "DeepModel Agent";
            FormBorderStyle = FormBorderStyle.Sizable;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(820, 560);
            MinimumSize = new Size(520, 380);

            // ---- 左面板 ----
            var left = new Panel
            {
                Location = new Point(0, 0), Width = 210, Height = 560,
                BackColor = Color.FromArgb(24, 24, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
            };
            Controls.Add(left);

            int y = 4;
            // Project 标签 + 下拉框 — 带颜色区分
            var lblProj = new Label
            {
                Text = "Project", Location = new Point(6, y), AutoSize = true,
                ForeColor = Color.FromArgb(100, 180, 255), Font = new Font("Segoe UI", 8, FontStyle.Bold)
            };
            left.Controls.Add(lblProj);
            y += 16;

            _cmbProject = new ComboBox
            {
                Location = new Point(6, y), Width = 196, DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9), FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 58), ForeColor = Color.White
            };
            _cmbProject.SelectedIndexChanged += OnProjectChanged;
            left.Controls.Add(_cmbProject);
            y += 28;

            // 对话标签
            left.Controls.Add(new Label
            {
                Text = "Conversations", Location = new Point(6, y), AutoSize = true,
                ForeColor = Color.FromArgb(100, 180, 255), Font = new Font("Segoe UI", 8, FontStyle.Bold)
            });
            y += 16;

            // 标准 ListBox，简单可靠
            _lstConv = new ListBox
            {
                Location = new Point(6, y), Width = 196, Height = 380,
                BackColor = Color.FromArgb(36, 36, 42), ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Microsoft YaHei", 9), BorderStyle = BorderStyle.None,
                IntegralHeight = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
            };
            _lstConv.SelectedIndexChanged += OnConvSelected;
            left.Controls.Add(_lstConv);
            y += 388;

            // 按钮行
            var btnNew = new Button
            {
                Text = "+", Location = new Point(6, y), Width = 28, Height = 24,
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(60, 140, 80), ForeColor = Color.White
            };
            btnNew.FlatAppearance.BorderSize = 0;
            btnNew.Click += (s, e) => NewConversation();
            left.Controls.Add(btnNew);

            var btnDel = new Button
            {
                Text = "x", Location = new Point(38, y), Width = 28, Height = 24,
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(180, 60, 60), ForeColor = Color.White
            };
            btnDel.FlatAppearance.BorderSize = 0;
            btnDel.Click += (s, e) => DeleteConversation();
            left.Controls.Add(btnDel);

            var btnClear = new Button
            {
                Text = "Clr", Location = new Point(118, y), Width = 32, Height = 24,
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 7),
                BackColor = Color.FromArgb(80, 80, 90), ForeColor = Color.White
            };
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += (s, e) =>
            {
                _txtChat.Clear();
                _engine.Reset();
                Append("(cleared)\n", Color.Gray);
            };
            left.Controls.Add(btnClear);

            var btnRnd = new Button
            {
                Text = "?", Location = new Point(154, y), Width = 24, Height = 24,
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8),
                BackColor = Color.FromArgb(80, 80, 90), ForeColor = Color.White
            };
            btnRnd.FlatAppearance.BorderSize = 0;
            btnRnd.Click += (s, e) =>
            { _txtInput.Text = DefaultPrompts[_rng.Next(DefaultPrompts.Length)]; };
            left.Controls.Add(btnRnd);

            var btnCfg = new Button
            {
                Text = "cfg", Location = new Point(180, y), Width = 26, Height = 24,
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 7),
                BackColor = Color.FromArgb(60, 60, 70), ForeColor = Color.FromArgb(180, 180, 200)
            };
            btnCfg.FlatAppearance.BorderSize = 0;
            btnCfg.Click += (s, e) =>
            {
                try { System.Diagnostics.Process.Start("notepad.exe", AgentConfig.ConfigPath); }
                catch { }
            };
            left.Controls.Add(btnCfg);

            // ---- 右面板 ----
            int rx = 214, rw = 600;

            // 标题栏
            var titleBar = new Panel
            {
                Location = new Point(rx, 0), Width = rw, Height = 24,
                BackColor = Color.FromArgb(50, 55, 65),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(titleBar);
            _lblStatus = new Label
            {
                Text = "", Location = new Point(8, 4), AutoSize = true,
                ForeColor = Color.FromArgb(180, 200, 220), Font = new Font("Segoe UI", 8)
            };
            titleBar.Controls.Add(_lblStatus);

            _txtChat = new RichTextBox
            {
                Location = new Point(rx, 26), Width = rw, Height = 418,
                ReadOnly = true, BackColor = Color.FromArgb(28, 28, 34),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Microsoft YaHei", 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BorderStyle = BorderStyle.None, WordWrap = true
            };
            Controls.Add(_txtChat);

            var inputPanel = new Panel
            {
                Location = new Point(rx, 450), Width = rw, Height = 88,
                BackColor = Color.FromArgb(40, 40, 48),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(inputPanel);

            _txtInput = new TextBox
            {
                Location = new Point(6, 6), Width = 498, Height = 76,
                Multiline = true, BackColor = Color.FromArgb(55, 55, 62),
                ForeColor = Color.White, Font = new Font("Microsoft YaHei", 10),
                BorderStyle = BorderStyle.None,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
                Text = DefaultPrompts[_rng.Next(DefaultPrompts.Length)]
            };
            _txtInput.KeyDown += (s, e) =>
            { if (e.Control && e.KeyCode == Keys.Enter) { SendMessage(); e.SuppressKeyPress = true; } };
            inputPanel.Controls.Add(_txtInput);

            _btnSend = new Button
            {
                Text = "Send", Location = new Point(508, 6), Width = 86, Height = 76,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(60, 130, 220), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom
            };
            _btnSend.FlatAppearance.BorderSize = 0;
            _btnSend.Click += (s, e) => SendMessage();
            inputPanel.Controls.Add(_btnSend);

            // 初始化
            LoadProjects();
            RefreshConversations();
            if (_convMgr.Conversations.Count > 0) _lstConv.SelectedIndex = 0;
            else NewConversation();
        }

        // ---- 项目管理 ----

        private void LoadProjects()
        {
            _cmbProject.Items.Clear();
            var projects = ConversationManager.ListProjects();
            if (projects.Length == 0) projects = new[] { "default" };
            foreach (var p in projects) _cmbProject.Items.Add(p);
            _cmbProject.SelectedItem = "default";
        }

        private void OnProjectChanged(object sender, EventArgs e)
        {
            _convMgr.SetProject(_cmbProject.SelectedItem?.ToString());
            RefreshConversations();
            if (_convMgr.Conversations.Count > 0) _lstConv.SelectedIndex = 0;
            else NewConversation();
        }

        private void RefreshConversations()
        {
            string selId = (_lstConv.SelectedItem as ConversationManager.ConvInfo)?.Id;
            _convMgr.RefreshList();
            _lstConv.BeginUpdate();
            _lstConv.Items.Clear();
            foreach (var c in _convMgr.Conversations)
                _lstConv.Items.Add(c);
            if (selId != null)
            {
                for (int i = 0; i < _lstConv.Items.Count; i++)
                    if (((ConversationManager.ConvInfo)_lstConv.Items[i]).Id == selId)
                    { _lstConv.SelectedIndex = i; break; }
            }
            _lstConv.EndUpdate();
        }

        private void NewConversation()
        {
            var info = _convMgr.CreateConversation();
            _lstConv.Items.Insert(0, info);
            _lstConv.SelectedIndex = 0;
            _txtChat.Clear();
            _engine.SetMessages(new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { ["role"] = "system", ["content"] = SystemPrompt.Text }
            });
            _txtInput.Text = DefaultPrompts[_rng.Next(DefaultPrompts.Length)];
            Append("[" + info.Id + "]\n", Color.Gray);
        }

        private void DeleteConversation()
        {
            var info = _lstConv.SelectedItem as ConversationManager.ConvInfo;
            if (info == null) return;
            _convMgr.DeleteConversation(info);
            string selId = _convMgr.ActiveConversation?.Id;
            _lstConv.BeginUpdate();
            _lstConv.Items.Clear();
            foreach (var c in _convMgr.Conversations) _lstConv.Items.Add(c);
            _lstConv.EndUpdate();
            if (_convMgr.Conversations.Count == 0) NewConversation();
            else if (selId != null)
            {
                for (int i = 0; i < _lstConv.Items.Count; i++)
                    if (((ConversationManager.ConvInfo)_lstConv.Items[i]).Id == selId)
                    { _lstConv.SelectedIndex = i; break; }
            }
        }

        private void OnConvSelected(object sender, EventArgs e)
        {
            var info = _lstConv.SelectedItem as ConversationManager.ConvInfo;
            if (info == null || _convMgr.ActiveConversation == info) return;
            _convMgr.ActiveConversation = info;
            _txtChat.Clear();
            var msgs = _convMgr.LoadMessages(info);
            _engine.SetMessages(msgs ?? new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { ["role"] = "system", ["content"] = SystemPrompt.Text }
            });
            if (msgs != null)
            {
                foreach (var m in msgs)
                {
                    string role = m.ContainsKey("role") ? m["role"] as string : "";
                    if (role == "user") Append("\n> " + m["content"] + "\n\n", Color.Cyan);
                    else if (role == "assistant" && m.ContainsKey("content") && m["content"] != null)
                        AppendMd("\n" + (m["content"] as string) + "\n\n");
                }
            }
        }

        // ---- 消息发送 ----

        private void SendMessage()
        {
            if (_busy) return;
            string text = _txtInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;
            _txtInput.Clear();
            _busy = true;
            _btnSend.Text = "...";
            _btnSend.BackColor = Color.FromArgb(180, 100, 40);
            Append("\n> " + text + "\n\n", Color.Cyan);
            SetStatus("thinking...", Color.FromArgb(255, 200, 60));

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string reply = _engine.Chat(text);
                    try { _convMgr.SaveCurrent(_engine.GetMessages()); } catch { }
                    this.BeginInvoke((Action)(() =>
                    {
                        AppendMd("\n" + reply + "\n\n");
                        SetStatus("ready", Color.FromArgb(120, 200, 120));
                        RefreshConversations();
                    }));
                }
                catch (Exception ex)
                {
                    this.BeginInvoke((Action)(() =>
                    {
                        Append("\n[ERR] " + ex.Message + "\n\n", Color.Red);
                        SetStatus("error", Color.Red);
                    }));
                }
                finally
                {
                    this.BeginInvoke((Action)(() =>
                    {
                        _busy = false;
                        _btnSend.Text = "Send";
                        _btnSend.BackColor = Color.FromArgb(60, 130, 220);
                    }));
                }
            });
        }

        // ---- 显示 ----

        private void Append(string msg, Color color)
        {
            int s = _txtChat.TextLength;
            _txtChat.AppendText(msg);
            _txtChat.Select(s, msg.Length);
            _txtChat.SelectionColor = color;
            _txtChat.Select(_txtChat.TextLength, 0);
            _txtChat.ScrollToCaret();
        }

        private void AppendMd(string md)
        {
            if (string.IsNullOrEmpty(md)) return;
            var box = _txtChat;
            int i = 0;
            while (i < md.Length)
            {
                // **bold**
                if (i + 3 <= md.Length && md[i] == '*' && md[i + 1] == '*')
                {
                    int e2 = md.IndexOf("**", i + 2);
                    if (e2 > i + 2)
                    {
                        string b = md.Substring(i + 2, e2 - i - 2);
                        int s = box.TextLength;
                        box.AppendText(b);
                        box.Select(s, b.Length);
                        box.SelectionFont = new Font(box.Font, FontStyle.Bold);
                        box.Select(box.TextLength, 0);
                        box.SelectionFont = box.Font;
                        i = e2 + 2; continue;
                    }
                }
                // `code`
                if (i + 1 < md.Length && md[i] == '`')
                {
                    int e2 = md.IndexOf('`', i + 1);
                    if (e2 > i + 1)
                    {
                        string c = md.Substring(i + 1, e2 - i - 1);
                        int s = box.TextLength;
                        box.AppendText(c);
                        box.Select(s, c.Length);
                        box.SelectionFont = new Font("Consolas", box.Font.Size);
                        box.SelectionColor = Color.FromArgb(255, 180, 80);
                        box.Select(box.TextLength, 0);
                        box.SelectionFont = box.Font;
                        i = e2 + 1; continue;
                    }
                }
                // 普通 — 取到下一个特殊字符
                int n = md.Length;
                for (int j = i + 1; j < md.Length; j++)
                { if (md[j] == '*' || md[j] == '`') { n = j; break; } }
                string t = md.Substring(i, n - i);
                Append(t, Color.White);
                i = n;
            }
        }

        private void SetStatus(string text, Color color)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => SetStatus(text, color))); return; }
            _lblStatus.Text = text;
            _lblStatus.ForeColor = color;
        }

        public void SetStatusText(string text, Color color) => SetStatus(text, color);
    }
}
