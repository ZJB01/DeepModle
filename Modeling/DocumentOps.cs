using System;
using System.Collections.Generic;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace DeepModel.Modeling
{
    /// <summary>
    /// 文档级操作：新建 / 标题 / 设计树
    /// </summary>
    public static class DocumentOps
    {
        public static string CreateNewPart(ISldWorks app)
        {
            if (app == null) return "app is null";
            var doc = app.NewPart();
            if (doc == null) return "NewPart failed";
            return null; // success
        }

        public static string GetTitle(ISldWorks app, out string title)
        {
            title = null;
            if (app == null) return "app is null";
            var doc = app.ActiveDoc as IModelDoc2;
            if (doc == null) return "no document";
            title = doc.GetTitle();
            return null;
        }

        public static string SetTitle(ISldWorks app, string newTitle)
        {
            if (app == null) return "app is null";
            var doc = app.ActiveDoc as IModelDoc2;
            if (doc == null) return "no document";

            // 纯内存重命名，不写磁盘。用户需手动 Ctrl+S 保存
            doc.SetTitle2(newTitle);
            return null;
        }

        public static string GetDesignTree(ISldWorks app, out string tree)
        {
            tree = null;
            if (app == null) return "app is null";
            var doc = app.ActiveDoc as IModelDoc2;
            if (doc == null) return "no document";

            var sb = new StringBuilder();
            IFeature feat = doc.FirstFeature();
            while (feat != null)
            {
                string name = feat.Name;
                string type = feat.GetTypeName2() ?? feat.GetTypeName() ?? "?";
                sb.AppendLine($"{type}\t{name}");
                feat = feat.GetNextFeature();
            }

            if (sb.Length == 0) tree = "(empty)";
            else tree = sb.ToString().TrimEnd('\r', '\n');
            return null;
        }
    }
}
