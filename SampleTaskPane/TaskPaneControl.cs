using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SampleTaskPane
{
    /// <summary>
    /// 作为任务窗格内容的 WinForms 用户控件（精简版：只放几个常用控件做演示）。
    /// 通过 ITaskpaneView.DisplayWindowFromHandlex64 的窗口句柄，
    /// 由 SOLIDWORKS “重父化”嵌入到任务窗格中。
    /// 布局用 TableLayoutPanel + Dock/Anchor，控件会随任务窗格拉伸自动改变大小与位置。
    /// 注意：控件会作为 COM 窗口宿主，标记 [ComVisible(true)] 更稳妥。
    /// </summary>
    [ComVisible(true)]
    public class TaskPaneControl : UserControl
    {
        private const int NominalWidth = 300;

        private Label _lblStatus;

        // 任务窗格被缩放时，SW 只拉伸宿主窗口，各子窗口常常来不及重绘 —— 屏幕上就出现
        // 撕裂、错位的“花屏”。这靠 DoubleBuffered（逐控件缓冲）解决不了；正确做法是给窗口
        // 加 WS_EX_COMPOSITED：由操作系统把本窗口及其所有子窗口先画到离屏位图，再一次性贴上屏。
        private const int WS_EX_COMPOSITED = 0x02000000;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_COMPOSITED;
                return cp;
            }
        }

        public TaskPaneControl()
        {
            // 根容器开启双缓冲，配合 WS_EX_COMPOSITED 减少擦除闪烁。
            this.SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            this.UpdateStyles();

            BuildUi();
        }

        /// <summary>跳过背景擦除，避免缩放时的擦除闪烁。</summary>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        // ========================================
        // 界面构建（TableLayoutPanel 自动布局）
        // ========================================

        private void BuildUi()
        {
            this.BackColor = SystemColors.Control;
            this.Size = new Size(NominalWidth, 420);

            _lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = SystemColors.Info,
                Padding = new Padding(4, 0, 4, 0),
                Text = "当前操作：等待交互…"
            };

            // 主容器：上面是表单区（填满），下面固定一行状态栏。
            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(8)
            };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            main.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // 表单区：两列 —— 左列标签自适应宽度，右列控件填满剩余宽度。
            var form = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 0,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            AddRow(form, "文本框 TextBox：", BuildTextBox());
            AddRow(form, "下拉框 ComboBox：", BuildComboBox());
            AddRow(form, null, BuildCheckBox());
            AddRow(form, "列表框 ListBox：", BuildListBox());
            AddRow(form, null, BuildButton());
            AddRow(form, "图片 PictureBox：", BuildPictureBox());

            main.Controls.Add(form, 0, 0);
            main.Controls.Add(_lblStatus, 0, 1);
            this.Controls.Add(main);
        }

        /// <summary>在表单里追加一行：左侧标签（可为空）+ 右侧控件。</summary>
        private static void AddRow(TableLayoutPanel table, string labelText, Control control)
        {
            int row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            if (labelText != null)
            {
                var lbl = new Label
                {
                    Text = labelText,
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    Margin = new Padding(3, 8, 6, 0)
                };
                table.Controls.Add(lbl, 0, row);
            }

            control.Margin = new Padding(3, 4, 3, 4);
            table.Controls.Add(control, 1, row);
        }

        // ========================================
        // 各控件
        // ========================================

        private Control BuildTextBox()
        {
            var txt = new TextBox
            {
                Text = "测试文本",
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            txt.TextChanged += (s, e) => Report("TextBox = " + txt.Text);
            return txt;
        }

        private Control BuildComboBox()
        {
            var cmb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            cmb.Items.AddRange(new object[] { "选项 A", "选项 B", "选项 C" });
            cmb.SelectedIndex = 0;
            cmb.SelectedIndexChanged += (s, e) => Report("ComboBox = " + cmb.Text);
            return cmb;
        }

        private Control BuildCheckBox()
        {
            var chk = new CheckBox { Text = "复选框 CheckBox", Anchor = AnchorStyles.Left };
            chk.CheckedChanged += (s, e) => Report("CheckBox = " + chk.Checked);
            return chk;
        }

        private Control BuildListBox()
        {
            var list = new ListBox
            {
                Height = 90,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            list.Items.AddRange(new object[] { "列表项 1", "列表项 2", "列表项 3" });
            list.SelectedIndexChanged += (s, e) => Report("ListBox 选中索引 = " + list.SelectedIndex);
            return list;
        }

        private Control BuildButton()
        {
            var btn = new Button
            {
                Text = "按钮 Button",
                Height = 30,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            btn.Click += (s, e) => Report("Button 被点击");
            return btn;
        }

        private Control BuildPictureBox()
        {
            return new PictureBox
            {
                Width = 44,
                Height = 44,
                Anchor = AnchorStyles.Left,
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.CenterImage,
                Image = CreateSampleImage()
            };
        }

        private void Report(string text)
        {
            if (_lblStatus != null)
            {
                _lblStatus.Text = "当前操作：" + text;
            }
        }

        // ========================================
        // 辅助
        // ========================================

        private static Image CreateSampleImage()
        {
            var bmp = new Bitmap(40, 40);
            using (Graphics gr = Graphics.FromImage(bmp))
            {
                gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                gr.Clear(Color.White);

                // 画一个“图片”小图标：相框 + 山峰 + 太阳
                using (var frame = new SolidBrush(Color.FromArgb(0xEE, 0xF3, 0xFA)))
                using (var border = new Pen(Color.SteelBlue, 2))
                {
                    gr.FillRectangle(frame, 3, 3, 34, 34);
                    gr.DrawRectangle(border, 3, 3, 34, 34);
                }

                using (var sun = new SolidBrush(Color.Gold))
                {
                    gr.FillEllipse(sun, 22, 9, 9, 9);
                }

                using (var hill = new SolidBrush(Color.SeaGreen))
                {
                    gr.FillPolygon(hill, new[]
                    {
                        new Point(6, 34),
                        new Point(18, 15),
                        new Point(31, 34)
                    });
                    gr.FillPolygon(hill, new[]
                    {
                        new Point(20, 34),
                        new Point(28, 22),
                        new Point(36, 34)
                    });
                }
            }
            return bmp;
        }
    }
}
