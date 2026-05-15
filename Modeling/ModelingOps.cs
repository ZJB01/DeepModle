using System;
using SolidWorks.Interop.sldworks;

namespace DeepModel.Modeling
{
    public static class ModelingOps
    {
        public static string StartSketch(ISldWorks app, string planeName)
        {
            if (app == null) return "app is null";
            var doc = app.ActiveDoc as IModelDoc2;
            if (doc == null) return "no document";

            string swPlane;
            var u = planeName.ToUpperInvariant();
            if (u == "FRONT" || u == "前视" || u == "前") swPlane = "前视基准面"; // 前视基准面
            else if (u == "TOP" || u == "上视" || u == "上") swPlane = "上视基准面"; // 上视基准面
            else if (u == "RIGHT" || u == "右视" || u == "右") swPlane = "右视基准面"; // 右视基准面
            else swPlane = planeName;

            bool ok = doc.Extension.SelectByID2(swPlane, "PLANE", 0, 0, 0, false, 0, null, 0);
            if (!ok) return "plane not found: " + planeName;

            doc.SketchManager.InsertSketch(true);
            return null;
        }

        public static string DrawRect(ISldWorks app, double wMm, double hMm, double cxMm, double cyMm)
        {
            var doc = app.ActiveDoc as IModelDoc2;
            if (doc == null) return "no document";

            double w = wMm / 1000.0;
            double h = hMm / 1000.0;
            double cx = cxMm / 1000.0;
            double cy = cyMm / 1000.0;

            doc.SketchManager.CreateCornerRectangle(cx - w / 2, cy - h / 2, 0,
                                                     cx + w / 2, cy + h / 2, 0);
            doc.SketchManager.InsertSketch(true);
            return null;
        }

        public static string DrawCircle(ISldWorks app, double dMm, double cxMm, double cyMm)
        {
            var doc = app.ActiveDoc as IModelDoc2;
            if (doc == null) return "no document";

            double r = dMm / 2000.0;
            double cx = cxMm / 1000.0;
            double cy = cyMm / 1000.0;

            doc.SketchManager.CreateCircle(cx, cy, 0, cx + r, cy, 0);
            doc.SketchManager.InsertSketch(true);
            return null;
        }

        public static string Extrude(ISldWorks app, double depthMm)
        {
            var doc = app.ActiveDoc as IModelDoc2;
            if (doc == null) return "no document";

            double d = depthMm / 1000.0;
            IFeature f = doc.FeatureManager.FeatureExtrusion2(
                true, false, false, 0, 0, d, 0.01,
                false, false, false, false, 0, 0,
                false, false, false, false,
                true, true, true, 0, 0, false);

            return f == null ? "extrude failed (depth=" + depthMm + "mm)" : null;
        }
    }
}
