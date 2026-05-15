using System;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace DeepModel.Modeling
{
    /// <summary>
    /// 纯建模逻辑 —— 不依赖 UI，不依赖 SW 回调机制。
    /// 可被 SW 按钮、Pipe 命令、外部 Agent 等任意入口调用。
    /// </summary>
    public static class CubeBuilder
    {
        /// <summary>在指定 SolidWorks 实例的当前零件中生成正方体</summary>
        /// <returns>null on success, error message on failure</returns>
        public static string Build(ISldWorks app, double sideMm)
        {
            if (app == null) return "SolidWorks 实例为空";

            IModelDoc2 doc = app.ActiveDoc;
            if (doc == null) return "没有打开的文档";
            if (doc.GetType() != (int)swDocumentTypes_e.swDocPART)
                return "当前文档不是零件";

            try
            {
                double L = sideMm / 1000.0; // mm → m

                doc.Extension.SelectByID2("前视基准面", "PLANE", 0, 0, 0, false, 0, null, 0);
                doc.SketchManager.InsertSketch(true);
                doc.SketchManager.CreateCornerRectangle(0, 0, 0, L, L, 0);
                doc.SketchManager.InsertSketch(true);

                IFeature f = doc.FeatureManager.FeatureExtrusion2(
                    true, false, false, 0, 0, L, 0.01,
                    false, false, false, false, 0, 0,
                    false, false, false, false,
                    true, true, true, 0, 0, false);

                if (f == null) return "FeatureExtrusion2 返回 null";

                return null; // success
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
