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

            string fullPath = doc.GetPathName();
            if (string.IsNullOrEmpty(fullPath))
            {
                string dir = System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.Desktop);
                string newPath = System.IO.Path.Combine(dir, newTitle);
                if (!newPath.EndsWith(".SLDPRT", StringComparison.OrdinalIgnoreCase))
                    newPath += ".SLDPRT";

                int ver = (int)swSaveAsVersion_e.swSaveAsCurrentVersion;
                if (doc.SaveAs3(newPath, ver, 0) != 0)
                    return "SaveAs returned error";
            }
            else
            {
                string dir = System.IO.Path.GetDirectoryName(fullPath);
                string newPath = System.IO.Path.Combine(dir, newTitle);
                string ext = System.IO.Path.GetExtension(fullPath);
                if (!newPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    newPath += ext;

                int ver = (int)swSaveAsVersion_e.swSaveAsCurrentVersion;
                if (doc.SaveAs3(newPath, ver, 0) != 0)
                    return "SaveAs returned error";
            }

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
