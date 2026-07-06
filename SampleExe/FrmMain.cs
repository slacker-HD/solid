using SolidWorks.Interop.sldworks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SampleExe
{
    public partial class FrmMain : Form
    {
        // 全局SolidWorks实例，整个窗体生命周期共用
        private SldWorks _swApp = null;

        public FrmMain()
        {
            InitializeComponent();
        }

        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_swApp != null)
            {
                try
                {
                    // 退出SolidWorks程序
                    _swApp.ExitApp();
                    // 释放COM资源
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(_swApp);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"关闭SolidWorks异常：{ex.Message}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally
                {
                    _swApp = null;
                }
            }
        }

        private void BtnOpen_Click(object sender, EventArgs e)
        {
            // 初始化打开文件对话框
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            // 筛选后缀，完全匹配图片里：*.prt;*.asm;*.drw;*.sldprt;*.sldasm;*.slddrw
            openFileDialog1.Filter = "SolidWorks文件(*.prt;*.asm;*.drw;*.sldprt;*.sldasm;*.slddrw)|*.prt;*.asm;*.drw;*.sldprt;*.sldasm;*.slddrw|所有文件(*.*)|*.*";
            openFileDialog1.Title = "选择SolidWorks零件/装配/工程图文件";

            // 判断是否选中文件并点击确定
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                // 获取选中文件完整路径
                string filePath = openFileDialog1.FileName;
                int error = 0;
                int warning = 0;

                // 如果实例不存在才新建，避免重复启动多个SW进程
                if (_swApp == null)
                {
                    _swApp = new SldWorks();
                }

                // 根据文件后缀自动匹配文档类型（OpenDoc6第二个参数）
                int docType;
                string ext = System.IO.Path.GetExtension(filePath).ToLower();
                switch (ext)
                {
                    case ".prt":
                    case ".sldprt":
                        docType = 1; // swDocPART 零件
                        break;
                    case ".asm":
                    case ".sldasm":
                        docType = 2; // swDocASSEMBLY 装配体
                        break;
                    case ".drw":
                    case ".slddrw":
                        docType = 3; // swDocDRAWING 工程图
                        break;
                    default:
                        MessageBox.Show("不支持的文件格式！");
                        return;
                }

                // 打开文档
                _swApp.OpenDoc6(filePath, docType, 1, "", ref error, ref warning);
                _swApp.Visible = true;

                if (error != 0)
                {
                    MessageBox.Show($"打开失败，错误码：{error}");
                }
            }
        }
    }
}