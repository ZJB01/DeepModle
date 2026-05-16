using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Web.Script.Serialization;

namespace DeepModel.Agent
{
    public class AgentConfig
    {
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "deepseek-chat";
        public string BaseUrl { get; set; } = "https://api.deepseek.com/v1";
        public int MaxTokens { get; set; } = 4096;
        public int ContextTokens { get; set; } = 65536;
        public double Temperature { get; set; } = 0.3;
        public int MaxToolRounds { get; set; } = 50;

        public static string ConfigDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeepModel");

        public static string ConfigPath => Path.Combine(ConfigDir, "agent_config.json");

        public static AgentConfig Load()
        {
            Directory.CreateDirectory(ConfigDir);

            if (!File.Exists(ConfigPath))
            {
                new AgentConfig().Save();
                return new AgentConfig();
            }

            try
            {
                string json = File.ReadAllText(ConfigPath);
                var ser = new JavaScriptSerializer();
                // 先解析为字典，兼容 snake_case 和 PascalCase
                var raw = ser.Deserialize<Dictionary<string, object>>(json);
                if (raw == null) return new AgentConfig();

                return new AgentConfig
                {
                    ApiKey        = GetStr(raw, "api_key", "ApiKey"),
                    Model         = GetStr(raw, "model", "Model", "deepseek-chat"),
                    BaseUrl       = GetStr(raw, "base_url", "BaseUrl", "https://api.deepseek.com/v1"),
                    MaxTokens     = GetInt(raw, "max_tokens", "MaxTokens", 4096),
                    ContextTokens = GetInt(raw, "context_tokens", "ContextTokens", 65536),
                    Temperature   = GetDbl(raw, "temperature", "Temperature", 0.3),
                    MaxToolRounds = GetInt(raw, "max_tool_rounds", "MaxToolRounds", 5)
                };
            }
            catch
            {
                try { File.Move(ConfigPath, ConfigPath + ".bak"); } catch { }
                new AgentConfig().Save();
                return new AgentConfig();
            }
        }

        public void Save()
        {
            var ser = new JavaScriptSerializer();
            string json = ser.Serialize(this);
            File.WriteAllText(ConfigPath, json);
        }

        public static AgentConfig LoadAndOpen()
        {
            var cfg = Load();
            if (string.IsNullOrWhiteSpace(cfg.ApiKey))
            {
                try { Process.Start("notepad.exe", $"\"{ConfigPath}\""); } catch { }
            }
            return cfg;
        }

        private static string GetStr(Dictionary<string, object> d, string key1, string key2, string def = "")
        {
            if (d.TryGetValue(key1, out var v) && v != null) return v.ToString();
            if (d.TryGetValue(key2, out v) && v != null) return v.ToString();
            return def;
        }

        private static int GetInt(Dictionary<string, object> d, string key1, string key2, int def = 0)
        {
            if (d.TryGetValue(key1, out var v) && v != null && int.TryParse(v.ToString(), out int i1)) return i1;
            if (d.TryGetValue(key2, out v) && v != null && int.TryParse(v.ToString(), out int i2)) return i2;
            return def;
        }

        private static double GetDbl(Dictionary<string, object> d, string key1, string key2, double def = 0)
        {
            if (d.TryGetValue(key1, out var v) && v != null && double.TryParse(v.ToString(), out double d1)) return d1;
            if (d.TryGetValue(key2, out v) && v != null && double.TryParse(v.ToString(), out double d2)) return d2;
            return def;
        }
    }
}
