using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace DeepModel.Agent
{
    /// <summary>
    /// 对话持久化管理：项目目录、多对话、自动保存
    /// </summary>
    public class ConversationManager
    {
        private static readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private static readonly string BaseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeepModel", "projects");

        public string CurrentProject { get; private set; } = "default";
        public List<ConvInfo> Conversations { get; private set; } = new List<ConvInfo>();
        public ConvInfo ActiveConversation { get; set; }

        public class ConvInfo
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string FilePath { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public override string ToString()
            {
                string t = Title ?? "(new)";
                if (t.Length > 24) t = t.Substring(0, 24);
                return t.PadRight(26) + UpdatedAt.ToString("MM-dd HH:mm");
            }
        }

        public string ProjectDir => Path.Combine(BaseDir, CurrentProject);
        public string ConvDir => Path.Combine(ProjectDir, "conversations");

        public static string[] ListProjects()
        {
            Directory.CreateDirectory(BaseDir);
            try { return Directory.GetDirectories(BaseDir).Select(Path.GetFileName).ToArray(); }
            catch { return new[] { "default" }; }
        }

        public void SetProject(string name)
        {
            CurrentProject = string.IsNullOrWhiteSpace(name) ? "default" : name;
            Directory.CreateDirectory(ConvDir);
            RefreshList();
        }

        public void RefreshList()
        {
            Directory.CreateDirectory(ConvDir);
            Conversations.Clear();
            foreach (var f in Directory.GetFiles(ConvDir, "*.json").OrderByDescending(f => f))
            {
                try
                {
                    var info = new FileInfo(f);
                    string json = File.ReadAllText(f);
                    var data = _json.Deserialize<Dictionary<string, object>>(json);
                    Conversations.Add(new ConvInfo
                    {
                        Id = Path.GetFileNameWithoutExtension(f),
                        Title = data.ContainsKey("title") ? data["title"]?.ToString() ?? "(empty)" : "(empty)",
                        FilePath = f,
                        CreatedAt = info.CreationTime,
                        UpdatedAt = info.LastWriteTime
                    });
                }
                catch { /* skip corrupted files */ }
            }
        }

        public ConvInfo CreateConversation()
        {
            var id = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var info = new ConvInfo
            {
                Id = id,
                Title = "(new)",
                FilePath = Path.Combine(ConvDir, $"{id}.json"),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            Conversations.Insert(0, info);
            ActiveConversation = info;
            Save(info, new List<Dictionary<string, object>>());
            return info;
        }

        public void SaveCurrent(List<Dictionary<string, object>> messages)
        {
            if (ActiveConversation == null) return;
            ActiveConversation.UpdatedAt = DateTime.Now;
            // 用第一条用户消息做标题
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].TryGetValue("role", out var r) && "user".Equals(r as string))
                {
                    string t = messages[i]["content"]?.ToString() ?? "";
                    if (t.Length > 30) t = t.Substring(0, 30) + "...";
                    if (t.Length > 0) ActiveConversation.Title = t;
                    break;
                }
            }
            Save(ActiveConversation, messages);
        }

        public List<Dictionary<string, object>> LoadMessages(ConvInfo info)
        {
            try
            {
                if (!File.Exists(info.FilePath)) return null;
                string json = File.ReadAllText(info.FilePath);
                var data = _json.Deserialize<Dictionary<string, object>>(json);
                if (data.ContainsKey("messages"))
                    return _json.Deserialize<List<Dictionary<string, object>>>(_json.Serialize(data["messages"]));
            }
            catch { }
            return null;
        }

        public void DeleteConversation(ConvInfo info)
        {
            try { if (File.Exists(info.FilePath)) File.Delete(info.FilePath); } catch { }
            Conversations.Remove(info);
            if (ActiveConversation == info) ActiveConversation = null;
        }

        private void Save(ConvInfo info, List<Dictionary<string, object>> messages)
        {
            var data = new Dictionary<string, object>
            {
                ["id"] = info.Id,
                ["title"] = info.Title,
                ["created_at"] = info.CreatedAt.ToString("o"),
                ["updated_at"] = info.UpdatedAt.ToString("o"),
                ["messages"] = messages
            };
            string json = _json.Serialize(data);
            File.WriteAllText(info.FilePath, json);
        }
    }
}
