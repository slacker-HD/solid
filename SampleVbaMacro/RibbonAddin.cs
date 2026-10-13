using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SampleVbaMacro
{
    [ComVisible(true)]
    [DisplayName("SampleVbaMacro")]
    [Description("Ribbon 按钮 + 输入并运行 SolidWorks VBA 宏 add-in 示例")]
    [Guid("6E234ED9-1455-4ACC-B568-A994946E5192")]
    public class VbaMacroAddin : ISwAddin
    {
        // ========================================
        // 字段
        // ========================================

        /// <summary>对话框单例（关闭时只隐藏，不释放）</summary>
        private frmMacro _frmMacroInstance;

        private ISldWorks iSwApp = null;
        private ICommandManager iCmdMgr = null;
        private int addinCookieID;

        /// <summary>主命令组ID</summary>
        public const int mainCmdGroupID = 5001;

        /// <summary>“运行 VBA 宏”按钮命令ID</summary>
        public const int cmdRunMacroId = 1002;

        /// <summary>主图标6种尺寸（命令组在标签页上的大图标）</summary>
        private string[] mainIcons = new string[6];

        /// <summary>按钮图标6种尺寸（横向图标条）</summary>
        private string[] icons = new string[6];

        // ========================================
        // 插件生命周期
        // ========================================

        /// <summary>【入口】连接到 SOLIDWORKS</summary>
        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            iSwApp = (ISldWorks)ThisSW;
            addinCookieID = Cookie;

            // 设置回调信息，告诉 SOLIDWORKS 回调方法定义在本类中
            iSwApp.SetAddinCallbackInfo(0, this, addinCookieID);

            // 获取命令管理器
            iCmdMgr = iSwApp.GetCommandManager(addinCookieID);

            // 创建工具栏和按钮
            AddCommandMgr();

            return true;
        }

        /// <summary>【出口】断开与 SOLIDWORKS 的连接</summary>
        public bool DisconnectFromSW()
        {
            // 关闭并释放对话框
            if (_frmMacroInstance != null && !_frmMacroInstance.IsDisposed)
            {
                _frmMacroInstance.FormClosing -= FrmMacroInstance_FormClosing;
                _frmMacroInstance.Close();
                _frmMacroInstance.Dispose();
                _frmMacroInstance = null;
            }

            // 移除命令组
            iCmdMgr.RemoveCommandGroup(mainCmdGroupID);

            // 释放COM对象
            Marshal.ReleaseComObject(iCmdMgr);
            iCmdMgr = null;
            Marshal.ReleaseComObject(iSwApp);
            iSwApp = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            return true;
        }

        // ========================================
        // 工具栏创建（核心方法）
        // ========================================

        /// <summary>创建命令组、添加按钮、设置图标、激活、添加Ribbon标签页</summary>
        public void AddCommandMgr()
        {
            try
            {
                ICommandGroup cmdGroup;

                string Title = "VBA宏";
                string ToolTip = "VBA宏";

                // 支持的文档类型：零件、装配体、工程图
                int[] docTypes = new int[]{
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swDocumentTypes_e.swDocDRAWING,
                    (int)swDocumentTypes_e.swDocPART
                };

                int cmdGroupErr = 0;
                bool ignorePrevious = false;

                // 从注册表获取之前保存的命令ID
                object registryIDs;
                bool getDataResult = iCmdMgr.GetGroupDataFromRegistry(mainCmdGroupID, out registryIDs);

                // 当前版本的命令ID数组
                int[] knownIDs = new int[] { cmdRunMacroId };

                // 比较ID是否一致，不一致则强制重建
                if (getDataResult && registryIDs is int[])
                {
                    if (!CompareIDs((int[])registryIDs, knownIDs))
                    {
                        ignorePrevious = true;
                    }
                }
                else
                {
                    ignorePrevious = true;
                }

                // 创建命令组
                cmdGroup = iCmdMgr.CreateCommandGroup2(mainCmdGroupID, Title, ToolTip, "", -1, ignorePrevious, ref cmdGroupErr);

                // 设置图标路径（DLL同级目录下的icons文件夹）
                LoadIcons();

                cmdGroup.MainIconList = mainIcons;  // 命令组大图标
                cmdGroup.IconList = icons;           // 各命令项图标（横向图标条）

                // 命令显示类型：同时显示在菜单和工具栏
                int menuToolbarOption = (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem);

                // 添加“运行 VBA 宏”按钮
                cmdGroup.AddCommandItem2(
                    "运行VBA宏",                 // 按钮名称
                    -1,                          // 位置（-1 表示末尾）
                    "输入并运行 SolidWorks VBA 代码", // 状态栏提示
                    "运行VBA宏",                 // 工具提示
                    0,                           // 使用图标条中的第1个图标
                    "OnRunMacro_Click",          // 点击回调
                    "OnEnableAll",               // 启用回调
                    cmdRunMacroId,               // 命令ID
                    menuToolbarOption);          // 菜单+工具栏

                cmdGroup.HasToolbar = true;
                cmdGroup.HasMenu = true;

                // 激活命令组（必须调用才能显示）
                cmdGroup.Activate();

                // 为每种文档类型添加Ribbon标签页
                foreach (int type in docTypes)
                {
                    AddRibbonTab(type, Title, cmdGroup);
                }
            }
            catch (Exception ex)
            {
                iSwApp.SendMsgToUser(ex.StackTrace);
            }
        }

        // ========================================
        // 图标加载
        // ========================================

        /// <summary>从DLL同级目录的icons文件夹加载图标路径</summary>
        private void LoadIcons()
        {
            string dir = $@"{RegDllPath()}\icons";

            // 命令组大图标（6种尺寸）
            mainIcons[0] = $@"{dir}\mainicon_20.png";
            mainIcons[1] = $@"{dir}\mainicon_32.png";
            mainIcons[2] = $@"{dir}\mainicon_40.png";
            mainIcons[3] = $@"{dir}\mainicon_64.png";
            mainIcons[4] = $@"{dir}\mainicon_96.png";
            mainIcons[5] = $@"{dir}\mainicon_128.png";

            // 按钮图标（6种尺寸，每个文件是横向图标条）
            icons[0] = $@"{dir}\toolbar20x.png";
            icons[1] = $@"{dir}\toolbar32x.png";
            icons[2] = $@"{dir}\toolbar40x.png";
            icons[3] = $@"{dir}\toolbar64x.png";
            icons[4] = $@"{dir}\toolbar96x.png";
            icons[5] = $@"{dir}\toolbar128x.png";
        }

        // ========================================
        // Ribbon标签页
        // ========================================

        /// <summary>为指定文档类型添加Ribbon标签页</summary>
        private void AddRibbonTab(int docType, string title, ICommandGroup cmdGroup)
        {
            CommandTab cmdTab;

            // 获取已存在的标签页
            cmdTab = iCmdMgr.GetCommandTab(docType, title);

            // 如果存在则移除（避免重复）
            if (cmdTab != null)
            {
                iCmdMgr.RemoveCommandTab(cmdTab);
                cmdTab = null;
            }

            // 创建新标签页
            cmdTab = iCmdMgr.AddCommandTab(docType, title);

            // 文本显示方式：文字在图标下方
            int textBelow = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;

            CommandTabBox boxButtons = cmdTab.AddCommandTabBox();
            int[] buttonIDs = new int[] { cmdGroup.get_CommandID(0) };
            int[] buttonTextTypes = new int[] { textBelow };
            boxButtons.AddCommands(buttonIDs, buttonTextTypes);
        }

        // ========================================
        // 按钮回调
        // ========================================

        /// <summary>按钮点击回调：打开“运行 VBA 宏”对话框</summary>
        public void OnRunMacro_Click()
        {
            // 如果窗体未创建或已关闭，则创建新实例
            if (_frmMacroInstance == null || _frmMacroInstance.IsDisposed)
            {
                _frmMacroInstance = new frmMacro();

                // 【关键】把 SolidWorks 实例传入对话框，宏才能运行
                _frmMacroInstance.SwApp = iSwApp;

                // 设置窗体关闭时只隐藏不释放，保持单例
                _frmMacroInstance.FormClosing += FrmMacroInstance_FormClosing;
            }

            // 显示窗体并置顶
            if (!_frmMacroInstance.Visible)
            {
                _frmMacroInstance.Show();
            }
            _frmMacroInstance.BringToFront();
            _frmMacroInstance.Activate();
        }

        /// <summary>对话框关闭时只隐藏不释放</summary>
        private void FrmMacroInstance_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;                 // 取消关闭操作
            _frmMacroInstance.Hide();        // 隐藏窗体
        }

        /// <summary>按钮启用状态回调：恒启用</summary>
        public int OnEnableAll()
        {
            return 1;
        }

        // ========================================
        // 工具方法
        // ========================================

        /// <summary>比较两组ID是否相同</summary>
        public bool CompareIDs(int[] storedIDs, int[] addinIDs)
        {
            List<int> storedList = new List<int>(storedIDs);
            List<int> addinList = new List<int>(addinIDs);

            addinList.Sort();
            storedList.Sort();

            if (addinList.Count != storedList.Count)
            {
                return false;
            }

            for (int i = 0; i < addinList.Count; i++)
            {
                if (addinList[i] != storedList[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>获取当前DLL所在目录路径</summary>
        public string RegDllPath()
        {
            try
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
            catch (Exception)
            {
                return "";
            }
        }

        #region COM注册函数

        private const string ADDIN_KEY_TEMPLATE = @"SOFTWARE\SolidWorks\Addins\{{{0}}}";
        private const string ADDIN_STARTUP_KEY_TEMPLATE = @"Software\SolidWorks\AddInsStartup\{{{0}}}";
        private const string ADD_IN_TITLE_REG_KEY_NAME = "Title";
        private const string ADD_IN_DESCRIPTION_REG_KEY_NAME = "Description";

        [ComRegisterFunction]
        public static void RegisterFunction(Type t)
        {
            try
            {
                var addInTitle = "";
                var loadAtStartup = true;
                var addInDesc = "";

                var dispNameAtt = t.GetCustomAttributes(false).OfType<DisplayNameAttribute>().FirstOrDefault();

                if (dispNameAtt != null)
                {
                    addInTitle = dispNameAtt.DisplayName;
                }
                else
                {
                    addInTitle = t.ToString();
                }

                var descAtt = t.GetCustomAttributes(false).OfType<DescriptionAttribute>().FirstOrDefault();

                if (descAtt != null)
                {
                    addInDesc = descAtt.Description;
                }
                else
                {
                    addInDesc = t.ToString();
                }

                var addInkey = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(
                    string.Format(ADDIN_KEY_TEMPLATE, t.GUID));

                addInkey.SetValue(null, 0);

                addInkey.SetValue(ADD_IN_TITLE_REG_KEY_NAME, addInTitle);
                addInkey.SetValue(ADD_IN_DESCRIPTION_REG_KEY_NAME, addInDesc);

                var addInStartupkey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    string.Format(ADDIN_STARTUP_KEY_TEMPLATE, t.GUID));

                addInStartupkey.SetValue(null, Convert.ToInt32(loadAtStartup), Microsoft.Win32.RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while registering the addin: " + ex.Message);
            }
        }

        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                Microsoft.Win32.Registry.LocalMachine.DeleteSubKey(
                    string.Format(ADDIN_KEY_TEMPLATE, t.GUID));

                Microsoft.Win32.Registry.CurrentUser.DeleteSubKey(
                    string.Format(ADDIN_STARTUP_KEY_TEMPLATE, t.GUID));
            }
            catch (Exception e)
            {
                Console.WriteLine("Error while unregistering the addin: " + e.Message);
            }
        }

        #endregion
    }
}
