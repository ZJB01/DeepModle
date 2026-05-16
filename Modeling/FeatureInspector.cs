using System;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace DeepModel.Modeling
{
    /// <summary>
    /// 读取特征和草图的详细参数
    /// </summary>
    public static class FeatureInspector
    {
        public static string GetDetails(ISldWorks app, out string output)
        {
            output = null;
            if (app == null) return "app is null";
            var doc = app.ActiveDoc as IModelDoc2;
            if (doc == null) return "no document";

            var sb = new StringBuilder();
            IFeature feat = doc.FirstFeature();
            int count = 0;

            while (feat != null && count < 50)
            {
                string name = feat.Name;
                string type = feat.GetTypeName2() ?? feat.GetTypeName() ?? "?";
                sb.AppendLine($"[{name}] ({type})");

                // 尝试读取特征参数
                try
                {
                    if (type.IndexOf("Extrude", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        type.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var data = feat.GetDefinition() as IExtrudeFeatureData2;
                        if (data != null)
                        {
                            double depthM = data.GetDepth(true);
                            sb.AppendLine($"  depth: {depthM * 1000:F1}mm");
                        }
                    }

                    // 读取显示尺寸
                    var dim = feat.GetFirstDisplayDimension() as IDisplayDimension;
                    while (dim != null)
                    {
                        var dimObj = dim.GetDimension() as IDimension;
                        if (dimObj != null)
                        {
                            double val = dimObj.GetSystemValue2("");
                            string dimName = dimObj.FullName;
                            if (!string.IsNullOrEmpty(dimName))
                                sb.AppendLine($"  dim [{dimName}]: {val * 1000:F1}mm");
                        }
                        dim = feat.GetNextDisplayDimension(dim) as IDisplayDimension;
                    }
                }
                catch { }

                feat = feat.GetNextFeature();
                count++;
            }

            if (count == 0) sb.AppendLine("(empty)");
            output = sb.ToString().TrimEnd('\r', '\n');
            return null;
        }
    }
}
