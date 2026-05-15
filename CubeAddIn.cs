using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;

namespace SwCubeAddIn
{
    [Guid("B2A3F4C5-6D7E-8F9A-0B1C-2D3E4F5A6B7C")]
    [ComVisible(true)]
    [ProgId("SwCubeAddIn.CubeAddIn")]
    public class CubeAddIn : ISwAddin
    {
        private ISldWorks swApp;
        private int cookie;
        private const int GroupUID = 50001;
        private const int CmdUID = 50002;

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            swApp = (ISldWorks)ThisSW;
            cookie = Cookie;

            swApp.SetAddinCallbackInfo2(0, this, cookie);

            int docType = (int)swDocumentTypes_e.swDocPART;

            swApp.AddMenuItem2(docType, cookie, "生成正方体", -1,
                "CreateCube", "CreateCubeEnable",
                "输入边长参数，生成正方体");

            AddCommandTab(docType);

            return true;
        }

        private void AddCommandTab(int docType)
        {
            CommandManager cmdMgr = swApp.GetCommandManager(cookie);
            if (cmdMgr == null) return;

            int err = 0;
            object gid = cmdMgr.CreateCommandGroup2(
                GroupUID, "正方体插件", "正方体插件",
                "参数化建模", -1, true, ref err);

            CommandGroup cg = (CommandGroup)gid;

            int cmdId = cg.AddCommandItem2(
                "生成正方体", -1,
                "输入边长参数生成正方体", "正方体插件", -1,
                "CreateCube", "",
                CmdUID,
                (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem));

            CommandTab tab = cmdMgr.AddCommandTab(docType, "正方体插件");
            CommandTabBox box = tab.AddCommandTabBox();
            box.AddCommands(new int[] { cmdId }, new int[] { 0 });

            cg.HasToolbar = true;
            cg.HasMenu = true;
            cg.Activate();

            tab.Visible = true;
            tab.Active = true;
        }

        public bool DisconnectFromSW()
        {
            try
            {
                var cmdMgr = swApp.GetCommandManager(cookie);
                if (cmdMgr != null)
                {
                    cmdMgr.RemoveCommandGroup(GroupUID);
                    var tab = cmdMgr.GetCommandTab((int)swDocumentTypes_e.swDocPART, "正方体插件");
                    if (tab != null) cmdMgr.RemoveCommandTab(tab);
                }
            }
            catch { }
            return true;
        }

        // ===== 回调入口 =====

        public int CreateCube()
        {
            using (var dlg = new CubeDialog())
            {
                if (dlg.ShowDialog() != DialogResult.OK)
                    return 0; // 用户取消

                double sideMm = dlg.SideLengthMm;
                BuildCube(sideMm);
                return 0;
            }
        }

        public int CreateCubeEnable() { return 1; }

        // ===== 建模核心 =====

        private void BuildCube(double sideMm)
        {
            IModelDoc2 doc = swApp.ActiveDoc;
            if (doc == null || doc.GetType() != (int)swDocumentTypes_e.swDocPART)
            {
                MessageBox.Show("请先打开或新建一个零件文档。");
                return;
            }

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

            MessageBox.Show(f != null
                ? $"正方体 {sideMm}mm 创建成功！"
                : "创建失败。");
        }
    }

    // ===== 参数输入弹窗 =====

    internal class CubeDialog : Form
    {
        private NumericUpDown numSide;
        private Button btnOk;
        private Button btnCancel;

        public double SideLengthMm => (double)numSide.Value;

        public CubeDialog()
        {
            Text = "正方体参数";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(260, 120);

            var lbl = new Label
            {
                Text = "边长 (mm)：",
                Location = new Point(20, 22),
                AutoSize = true
            };

            numSide = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 10000,
                Value = 100,
                DecimalPlaces = 1,
                Increment = 10,
                Location = new Point(100, 20),
                Width = 100
            };

            btnOk = new Button
            {
                Text = "生成",
                DialogResult = DialogResult.OK,
                Location = new Point(60, 55),
                Width = 80
            };

            btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(150, 55),
                Width = 80
            };

            Controls.AddRange(new Control[] { lbl, numSide, btnOk, btnCancel });
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
