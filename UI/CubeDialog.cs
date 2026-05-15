using System.Drawing;
using System.Windows.Forms;

namespace DeepModel.UI
{
    /// <summary>正方体参数输入弹窗</summary>
    internal class CubeDialog : Form
    {
        private readonly NumericUpDown _numSide;
        public double SideLengthMm => (double)_numSide.Value;

        public CubeDialog()
        {
            Text = "DeepModel - 正方体参数";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(300, 120);

            Controls.Add(new Label
            {
                Text = "边长 (mm):",
                Location = new Point(20, 24),
                AutoSize = true
            });

            _numSide = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 10000,
                Value = 100,
                DecimalPlaces = 1,
                Increment = 10,
                Location = new Point(120, 20),
                Width = 130
            };
            Controls.Add(_numSide);

            var ok = new Button
            {
                Text = "生成",
                DialogResult = DialogResult.OK,
                Location = new Point(80, 55),
                Width = 80
            };
            var cancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(170, 55),
                Width = 80
            };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
