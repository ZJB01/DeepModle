using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace DeepModel.Agent
{
    public class AgentEngine
    {
        private readonly AgentConfig _cfg;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private readonly Func<string, string> _pipeSend;
        private readonly string _logPath;

        private List<Dictionary<string, object>> _messages;

        /// <summary>可设置的状态回调，用于更新 UI 状态标签</summary>
        public Action<string> OnStatus { get; set; }

        public AgentEngine(AgentConfig config, Func<string, string> pipeSender)
        {
            _cfg = config;
            _pipeSend = pipeSender;
            _logPath = Path.Combine(AgentConfig.ConfigDir, "agent.log");
            Reset();
        }

        public void Reset()
        {
            _messages = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { ["role"] = "system", ["content"] = SystemPrompt.Text }
            };
        }

        public string Chat(string userMessage)
        {
            _messages.Add(new Dictionary<string, object> { ["role"] = "user", ["content"] = userMessage });

            for (int round = 0; round < _cfg.MaxToolRounds; round++)
            {
                Status($"思考中... (round {round + 1})");

                Dictionary<string, object> resp;
                try { resp = CallAPI(); }
                catch (Exception ex) { Log($"API error: {ex}"); return $"[API错误] {ex.Message}"; }

                var choices = resp["choices"] as IList;
                var choice = (choices != null && choices.Count > 0) ? choices[0] as Dictionary<string, object> : null;
                var msg = choice?["message"] as Dictionary<string, object>;

                if (msg == null)
                {
                    Log($"No message in response. Keys: {string.Join(", ", resp.Keys)}");
                    return "[错误] 响应中没有 message";
                }

                // 检查 tool_calls
                if (msg.ContainsKey("tool_calls"))
                {
                    var toolCalls = msg["tool_calls"] as IList;
                    if (toolCalls == null || toolCalls.Count == 0) break;

                    _messages.Add(new Dictionary<string, object>
                    {
                        ["role"] = "assistant",
                        ["content"] = msg.ContainsKey("content") ? msg["content"] : null,
                        ["tool_calls"] = toolCalls
                    });

                    foreach (var tc in toolCalls)
                    {
                        var tcDict = tc as Dictionary<string, object>;
                        string callId = tcDict["id"] as string;
                        var func = tcDict["function"] as Dictionary<string, object>;
                        string funcName = func["name"] as string;
                        string args = func["arguments"] as string ?? "{}";

                        Status($"执行工具: {funcName}");
                        Log($"Tool call: {funcName}({args})");
                        string result = ExecuteTool(funcName, args);
                        Log($"Tool result: {result}");

                        _messages.Add(new Dictionary<string, object>
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = callId,
                            ["content"] = result
                        });
                    }

                    Status($"工具执行完成，继续思考...");
                }
                else
                {
                    string content = msg.ContainsKey("content") ? (msg["content"] as string) : "";
                    Log($"Final response (len={content?.Length ?? 0}): \"{content?.Substring(0, Math.Min(content?.Length ?? 0, 200))}\"");
                    _messages.Add(new Dictionary<string, object> { ["role"] = "assistant", ["content"] = content });
                    Status("就绪");
                    return content ?? "";
                }
            }

            Status("就绪");
            return "(达到最大工具轮次)";
        }

        private Dictionary<string, object> CallAPI()
        {
            var req = new Dictionary<string, object>
            {
                ["model"] = _cfg.Model,
                ["messages"] = _messages,
                ["max_tokens"] = _cfg.MaxTokens,
                ["temperature"] = _cfg.Temperature,
                ["tools"] = BuildToolDefs()
            };

            string json = _json.Serialize(req);
            Log($"API request: {_messages.Count} msgs, {json.Length} bytes");

            var http = (HttpWebRequest)WebRequest.Create($"{_cfg.BaseUrl}/chat/completions");
            http.Method = "POST";
            http.ContentType = "application/json";
            http.Headers.Add("Authorization", $"Bearer {_cfg.ApiKey}");
            http.Timeout = 180000;

            byte[] data = Encoding.UTF8.GetBytes(json);
            http.ContentLength = data.Length;
            using (var stream = http.GetRequestStream())
                stream.Write(data, 0, data.Length);

            HttpWebResponse response;
            try { response = (HttpWebResponse)http.GetResponse(); }
            catch (WebException we)
            {
                var errResp = (HttpWebResponse)we.Response;
                string errBody = "";
                if (errResp != null)
                    using (var sr = new StreamReader(errResp.GetResponseStream()))
                        errBody = sr.ReadToEnd();
                Log($"API ERROR {(int)errResp?.StatusCode}: {errBody}");
                throw new Exception($"API error: {errBody}");
            }

            string body;
            using (var sr = new StreamReader(response.GetResponseStream()))
                body = sr.ReadToEnd();

            Log($"API response ({response.StatusCode}): {body.Length} bytes");

            return _json.Deserialize<Dictionary<string, object>>(body);
        }

        private string ExecuteTool(string name, string argsJson)
        {
            try
            {
                var args = _json.Deserialize<Dictionary<string, object>>(argsJson) ?? new Dictionary<string, object>();

                switch (name)
                {
                    case "new_part":      return _pipeSend("NEW");
                    case "get_name":      return _pipeSend("NAME");
                    case "rename_part":   string rn = args.ContainsKey("name") ? args["name"].ToString() : "";
                                          return _pipeSend($"RENAME {rn}");
                    case "get_tree":      return _pipeSend("TREE");
                    case "sketch_start":  string plane = args.ContainsKey("plane") ? args["plane"].ToString() : "FRONT";
                                          return _pipeSend($"SKETCH {plane}");
                    case "draw_rect":     double w = GetNum(args, "width_mm");
                                          double h = GetNum(args, "height_mm");
                                          double cx = GetNum(args, "center_x_mm");
                                          double cy = GetNum(args, "center_y_mm");
                                          return _pipeSend($"RECT {w} {h} {cx} {cy}");
                    case "draw_circle":   double d = GetNum(args, "diameter_mm");
                                          double ccx = GetNum(args, "center_x_mm");
                                          double ccy = GetNum(args, "center_y_mm");
                                          return _pipeSend($"CIRCLE {d} {ccx} {ccy}");
                    case "extrude":       double ed = GetNum(args, "depth_mm");
                                          return _pipeSend($"EXTRUDE {ed}");
                    default:              return $"ERR unknown tool: {name}";
                }
            }
            catch (Exception ex) { return $"ERR tool execution: {ex.Message}"; }
        }

        private static double GetNum(Dictionary<string, object> args, string key, double def = 0)
        {
            if (args.TryGetValue(key, out var v) && v != null && double.TryParse(v.ToString(), out double d))
                return d;
            return def;
        }

        private void Status(string s) { try { OnStatus?.Invoke(s); } catch { } }

        private void Log(string msg)
        {
            try { File.AppendAllText(_logPath, $"{DateTime.Now:HH:mm:ss.fff} [Agent] {msg}\n"); }
            catch { }
        }

        // ===== Tool Schema =====

        private static List<object> BuildToolDefs()
        {
            return new List<object>
            {
                MakeTool("new_part", "Create a new empty part document in SolidWorks.", null, new string[0]),
                MakeTool("get_name", "Get the file name of the currently active SolidWorks document.", null, new string[0]),
                MakeTool("rename_part", "Rename and save the current document.",
                    new Dictionary<string, object> { ["name"] = Prop("string", "New file name without extension") }, new[] { "name" }),
                MakeTool("get_tree", "Read the FeatureManager design tree of the active document.", null, new string[0]),
                MakeTool("sketch_start", "Start a sketch on a specified plane. Call this BEFORE any draw/extrude commands.",
                    new Dictionary<string, object> { ["plane"] = Prop("string", "Plane name: FRONT, TOP, or RIGHT") }, new[] { "plane" }),
                MakeTool("draw_rect", "Draw a center-based rectangle on the active sketch. Dimensions in mm.",
                    new Dictionary<string, object>
                    {
                        ["width_mm"] = Prop("number", "Rectangle width in mm"),
                        ["height_mm"] = Prop("number", "Rectangle height in mm"),
                        ["center_x_mm"] = Prop("number", "Center X offset in mm (default 0)"),
                        ["center_y_mm"] = Prop("number", "Center Y offset in mm (default 0)")
                    }, new[] { "width_mm", "height_mm" }),
                MakeTool("draw_circle", "Draw a circle on the active sketch. Dimensions in mm.",
                    new Dictionary<string, object>
                    {
                        ["diameter_mm"] = Prop("number", "Circle diameter in mm"),
                        ["center_x_mm"] = Prop("number", "Center X offset in mm (default 0)"),
                        ["center_y_mm"] = Prop("number", "Center Y offset in mm (default 0)")
                    }, new[] { "diameter_mm" }),
                MakeTool("extrude", "Extrude the current sketch to create a solid feature.",
                    new Dictionary<string, object> { ["depth_mm"] = Prop("number", "Extrusion depth in mm") }, new[] { "depth_mm" })
            };
        }

        private static Dictionary<string, object> Prop(string type, string desc)
            => new Dictionary<string, object> { ["type"] = type, ["description"] = desc };

        private static Dictionary<string, object> MakeTool(string name, string desc,
            Dictionary<string, object> props, string[] required)
        {
            var parameters = new Dictionary<string, object> { ["type"] = "object" };
            if (props != null && props.Count > 0)
            { parameters["properties"] = props; parameters["required"] = required; }

            return new Dictionary<string, object>
            {
                ["type"] = "function",
                ["function"] = new Dictionary<string, object>
                {
                    ["name"] = name, ["description"] = desc, ["parameters"] = parameters
                }
            };
        }
    }
}
