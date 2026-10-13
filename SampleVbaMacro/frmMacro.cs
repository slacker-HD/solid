using SolidWorks.Interop.sldworks;
using System;
using System.Windows.Forms;

namespace SampleVbaMacro
{
    public partial class frmMacro : Form
    {
        /// <summary>由插件传入的 SolidWorks 应用实例</summary>
        public ISldWorks SwApp { get; set; }

        /// <summary>
        /// 默认示例代码：拉一个 10mm x 10mm x 10mm 的正方体。
        /// 用户可以在文本框里直接修改，然后点击“运行”。
        /// </summary>
        private const string DefaultCubeMacro = @"Sub Main()
    ' 拉一个 10mm x 10mm x 10mm 的正方体
    ' SolidWorks API 的长度单位是米，所以 10mm = 0.01m
    Dim swApp As SldWorks.SldWorks
    Set swApp = Application.SldWorks
    Dim swModel As SldWorks.ModelDoc2
    Set swModel = swApp.ActiveDoc

    Dim boolstatus As Boolean
    Dim swSketch As SldWorks.SketchManager
    Dim swFeat As SldWorks.Feature
    Dim halfSide As Double
    Dim height As Double

    If swModel Is Nothing Then
        MsgBox ""请先打开一个零件文档""
        Exit Sub
    End If

    halfSide = 0.005    ' 边长的一半：10mm / 2
    height = 0.01       ' 拉伸高度：10mm

    ' 选择前视基准面
    boolstatus = swModel.Extension.SelectByID2(""Front Plane"", ""PLANE"", 0, 0, 0, False, 0, Nothing, 0)

    ' 进入草图，画 10x10 的正方形，然后退出草图
    Set swSketch = swModel.SketchManager
    swSketch.InsertSketch True
    swSketch.CreateCornerRectangle -halfSide, -halfSide, 0, halfSide, halfSide, 0
    swSketch.InsertSketch True

    ' 拉伸 10mm，生成正方体
    Set swFeat = swModel.FeatureManager.FeatureExtrusion2( _
        True, False, False, _
        0, 0, height, 0, _
        False, False, False, False, _
        0, 0, _
        False, False, _
        False, False, _
        True, _
        True, True, _
        0, 0, False _
        )
End Sub";

        public frmMacro()
        {
            InitializeComponent();

            // 预填示例代码，用户可直接修改后运行
            if (string.IsNullOrEmpty(txtCode.Text))
            {
                // 统一换行符为 CRLF：文本框只认 CRLF，裸 LF 会挤成一行
                txtCode.Text = DefaultCubeMacro
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Replace("\n", "\r\n");
            }
        }

        /// <summary>
        /// 运行按钮：把文本框里的 VBA 代码写成临时宏并执行。
        /// </summary>
        private void btnRun_Click(object sender, EventArgs e)
        {
            btnRun.Enabled = false;
            Cursor = Cursors.WaitCursor;
            try
            {
                string log = VbaMacroRunner.Run(SwApp, txtCode.Text, out int errorCode);
                txtLog.Text = log;
            }
            catch (Exception ex)
            {
                txtLog.Text = "运行失败：" + ex.Message;
            }
            finally
            {
                Cursor = Cursors.Default;
                btnRun.Enabled = true;
            }
        }

        /// <summary>
        /// 关闭按钮：只隐藏窗口（由插件保持单例）
        /// </summary>
        private void btnClose_Click(object sender, EventArgs e)
        {
            Hide();
        }
    }
}
