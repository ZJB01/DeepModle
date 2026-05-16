using System;
using System.Runtime.InteropServices;
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
            if (u == "FRONT" || u == "前视" || u == "前") swPlane = "前视基准面";
            else if (u == "TOP" || u == "上视" || u == "上") swPlane = "上视基准面";
            else if (u == "RIGHT" || u == "右视" || u == "右") swPlane = "右视基准面";
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
            double w = wMm / 1000.0, h = hMm / 1000.0, cx = cxMm / 1000.0, cy = cyMm / 1000.0;
            doc.SketchManager.CreateCornerRectangle(cx - w / 2, cy - h / 2, 0, cx + w / 2, cy + h / 2, 0);
            doc.SketchManager.InsertSketch(true);
            return null;
        }

        public static string DrawLine(ISldWorks app, double x1Mm, double y1Mm, double x2Mm, double y2Mm)
        {
            var doc = app.ActiveDoc as IModelDoc2;
            if (doc == null) return "no document";
            doc.SketchManager.CreateLine(x1Mm / 1000.0, y1Mm / 1000.0, 0, x2Mm / 1000.0, y2Mm / 1000.0, 0);
            doc.SketchManager.InsertSketch(true);
            return null;
        }

        public static string DrawCircle(ISldWorks app, double dMm, double cxMm, double cyMm)
        {
            var doc = app.ActiveDoc as IModelDoc2;
            if (doc == null) return "no document";
            double r = dMm / 2000.0, cx = cxMm / 1000.0, cy = cyMm / 1000.0;
            doc.SketchManager.CreateCircle(cx, cy, 0, cx + r, cy, 0);
            doc.SketchManager.InsertSketch(true);
            return null;
        }

        // 选择最后一个草图特征
        private static IFeature GetLastSketch(IModelDoc2 doc)
        {
            IFeature feat = doc.FirstFeature();
            IFeature lastSketch = null;
            while (feat != null)
            {
                string t = feat.GetTypeName2() ?? feat.GetTypeName() ?? "";
                if (t.IndexOf("Sketch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.IndexOf("Profile", StringComparison.OrdinalIgnoreCase) >= 0)
                    lastSketch = feat;
                feat = feat.GetNextFeature();
            }
            return lastSketch;
        }

        public static string Extrude(ISldWorks app, double depthMm)
        {
            var doc = app.ActiveDoc as IModelDoc2;
            if (doc == null) return "no document";
            double d = depthMm / 1000.0;

            // 先选中最后的草图，确保挤出正确的草图
            var lastSketch = GetLastSketch(doc);
            if (lastSketch != null)
                doc.Extension.SelectByID2(lastSketch.Name, "SKETCH", 0, 0, 0, false, 0, null, 0);

            IFeature f = doc.FeatureManager.FeatureExtrusion2(
                true, false, false, 0, 0, d, 0.01,
                false, false, false, false, 0, 0,
                false, false, false, false,
                true, true, true, 0, 0, false);

            return f == null ? "extrude failed (depth=" + depthMm + "mm)" : null;
        }

        public static string ExtrudeCut(ISldWorks app, double depthMm)
        {
            var doc = app.ActiveDoc as IModelDoc2;
            if (doc == null) return "no document";
            double d = depthMm / 1000.0;

            var lastSketch = GetLastSketch(doc);
            if (lastSketch != null)
                doc.Extension.SelectByID2(lastSketch.Name, "SKETCH", 0, 0, 0, false, 0, null, 0);

            try
            {
                IFeature f = doc.FeatureManager.FeatureCut(
                    true, false, false, 0, 0, d, d,
                    false, false, false, false,
                    0, 0, false, false, false, false, false, false, false);

                return f == null ? "extrude_cut failed: no intersection with body" : null;
            }
            catch (COMException ex)
            {
                return "extrude_cut error: " + ex.Message + " (可能切除范围与实体无交集)";
            }
            catch (Exception ex)
            {
                return "extrude_cut error: " + ex.Message;
            }
        }

        public static string DeleteFeature(ISldWorks app, string name)
        {
            var doc = app.ActiveDoc as IModelDoc2;
            if (doc == null) return "no document";
            bool ok = doc.Extension.SelectByID2(name, "BODYFEATURE", 0, 0, 0, false, 0, null, 0)
                   || doc.Extension.SelectByID2(name, "SKETCH", 0, 0, 0, false, 0, null, 0);
            if (!ok) return "feature not found: " + name;
            doc.Extension.DeleteSelection2((int)SolidWorks.Interop.swconst.swDeleteSelectionOptions_e.swDelete_Absorbed);
            return null;
        }

        public static string RenameFeature(ISldWorks app, string oldName, string newName)
        {
            var doc = app.ActiveDoc as IModelDoc2;
            if (doc == null) return "no document";
            IFeature feat = doc.FirstFeature();
            while (feat != null)
            {
                if (feat.Name == oldName)
                {
                    feat.Name = newName;
                    return null;
                }
                feat = feat.GetNextFeature();
            }
            return "feature not found: " + oldName;
        }
    }
}
