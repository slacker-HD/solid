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
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SampleRibbon
{
    [ComVisible(true)]
    [DisplayName("SampleRibbon")]
    [Description("Ribbon 工具栏/菜单 add-in 示例")]
    [Guid("E6A26A02-EDFF-4D52-A3CB-892AFA4731A8")]
    public class RibbonAddin : ISwAddin
    {
        // ========================================
        // 字段
        // ========================================
        private frmMain _frmMainInstance;

        private ISldWorks iSwApp = null;
        private ICommandManager iCmdMgr = null;
        private int addinCookieID;

        /// <summary>主命令组ID</summary>
        public const int mainCmdGroupID = 5001;

        /// <summary>按钮1命令ID（打开窗体）</summary>
        public const int cmdButtonId = 1002;

        /// <summary>按钮2命令ID（信息提示）</summary>
        public const int cmdButton2Id = 1003;

        /// <summary>按钮3命令ID（开关按钮）</summary>
        public const int cmdButton3Id = 1004;

        /// <summary>下拉菜单（Flyout）分组ID</summary>
        public const int flyoutGroupID = 5002;

        /// <summary>开关按钮当前状态</summary>
        private bool _toggleState;

        /// <summary>下拉菜单按钮（Flyout）的命令ID</summary>
        private int _flyoutCmdId;

        /// <summary>主图标6种尺寸（命令组大图标）</summary>
        private string[] mainIcons = new string[6];

        /// <summary>工具栏图标6种尺寸（按钮图标，每个文件是一张横向图标条）</summary>
        private string[] icons = new string[6];

        /// <summary>下拉菜单按钮图标6种尺寸（大图标）</summary>
        private string[] flyoutMainIcons = new string[6];

        /// <summary>下拉菜单项图标6种尺寸（图标条）</summary>
        private string[] flyoutIcons = new string[6];

        // ========================================
        // 插件生命周期
        // ========================================

        /// <summary>
        /// 【入口】连接到SOLIDWORKS
        /// </summary>
        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            iSwApp = (ISldWorks)ThisSW;
            addinCookieID = Cookie;

            // 设置回调信息，告诉SOLIDWORKS回调方法定义在本类中
            iSwApp.SetAddinCallbackInfo(0, this, addinCookieID);

            // 获取命令管理器
            iCmdMgr = iSwApp.GetCommandManager(addinCookieID);

            // 创建工具栏和按钮
            AddCommandMgr();

            return true;
        }

        /// <summary>
        /// 【出口】断开与SOLIDWORKS的连接
        /// </summary>
        public bool DisconnectFromSW()
        {
            // 移除下拉菜单分组
            iCmdMgr.RemoveFlyoutGroup(flyoutGroupID);

            // 移除命令组
            iCmdMgr.RemoveCommandGroup(mainCmdGroupID);

            // 释放COM对象
            Marshal.ReleaseComObject(iCmdMgr);
            iCmdMgr = null;
            Marshal.ReleaseComObject(iSwApp);
            iSwApp = null;

            // 强制垃圾回收
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            return true;
        }

        // ========================================
        // 工具栏创建（核心方法）
        // ========================================

    /// <summary>
    /// 创建命令组、添加按钮、设置图标、激活、添加Ribbon标签页
    /// </summary>
    public void AddCommandMgr()
        {
            try
            {
                ICommandGroup cmdGroup;

                string Title = "SampleRibbon";
                string ToolTip = "SampleRibbon";

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
                int[] knownIDs = new int[] { cmdButtonId, cmdButton2Id, cmdButton3Id };

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

                // 赋值图标给命令组
                cmdGroup.MainIconList = mainIcons;  // 命令组在CommandManager标签页上的大图标
                cmdGroup.IconList = icons;           // 各命令项图标（横向图标条）

                // 命令显示类型：同时显示在菜单和工具栏
                int menuToolbarOption = (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem);

                // ------------------------------------------------------------
                // 添加命令项（官方控件示例）
                // 参数说明：
                //   1. Name: 菜单/工具栏显示名称
                //   2. Position: 位置，-1表示末尾
                //   3. HintString: 状态栏提示
                //   4. Tooltip: 鼠标悬停提示
                //   5. ImageListIndex: 图标索引（对应icons图标条中的第几个图标）
                //   6. CallbackFunction: 点击回调方法名
                //   7. EnableMethod: 启用/状态回调方法名
                //   8. UserID: 命令唯一ID
                //   9. ItemType: 显示类型
                // ------------------------------------------------------------

                // 回调采用“无参数”约定（参考 SOLIDWORKS 帮助：Add-in Callback and Enable Methods），
                // 每个命令使用各自独立的回调方法。

                // 按钮1：打开窗体
                cmdGroup.AddCommandItem2(
                    "打开窗体",                              // 按钮名称
                    -1,                                      // 位置
                    "打开测试窗体",                           // 提示信息
                    "打开窗体",                              // 工具提示
                    0,                                       // 使用icons图标条中的第1个图标
                    "OnCmd1_Click",                          // 点击回调
                    "OnEnableAll",                           // 启用回调
                    cmdButtonId,                             // 命令ID
                    menuToolbarOption);                      // 菜单+工具栏

                // 按钮2：信息提示
                cmdGroup.AddCommandItem2(
                    "信息提示",
                    -1,
                    "显示一条提示信息",
                    "信息提示",
                    1,                                       // 使用第2个图标
                    "OnCmd2_Click",
                    "OnEnableAll",
                    cmdButton2Id,
                    menuToolbarOption);

                // 按钮3：开关按钮（通过Enable回调返回选中状态实现按下/弹起效果）
                cmdGroup.AddCommandItem2(
                    "开关按钮",
                    -1,
                    "切换选中状态",
                    "开关按钮",
                    2,                                       // 使用第3个图标
                    "OnCmd3_Click",
                    "OnEnableToggle",
                    cmdButton3Id,
                    menuToolbarOption);

                // 在工具栏和菜单中都显示
                cmdGroup.HasToolbar = true;
                cmdGroup.HasMenu = true;

                // 激活命令组（必须调用才能显示）
                cmdGroup.Activate();

                // 创建下拉菜单（Flyout）分组
                CreateFlyout();

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
        // 下拉菜单（Flyout）创建
        // ========================================

        /// <summary>
        /// 创建一个下拉菜单按钮（Flyout），包含若干子命令
        /// </summary>
        private void CreateFlyout()
        {
            // 如果已存在先移除，避免重复
            FlyoutGroup oldFlyout = iCmdMgr.GetFlyoutGroup(flyoutGroupID) as FlyoutGroup;
            if (oldFlyout != null)
            {
                iCmdMgr.RemoveFlyoutGroup(flyoutGroupID);
                oldFlyout = null;
            }

            // 创建下拉菜单分组
            // 回调采用“无参数”约定（与官方 Flyout 示例一致，并与本插件其它命令保持统一）
            FlyoutGroup flyGroup = iCmdMgr.CreateFlyoutGroup2(
                flyoutGroupID,
                "下拉菜单 Tooltip",  // 标题（SOLIDWORKS 中此参数实际显示为按钮工具提示）
                "下拉菜单",          // 工具提示（SOLIDWORKS 中此参数实际显示为按钮名称）
                "下拉菜单提示",       // 状态栏提示
                flyoutMainIcons,     // 下拉按钮自身图标
                flyoutIcons,         // 下拉菜单项图标（图标条）
                "FlyoutCallback",    // 下拉按钮点击回调
                "FlyoutEnable");     // 下拉按钮启用回调

            if (flyGroup == null)
            {
                return;
            }

            // 下拉菜单样式：简单下拉（与 Ribbon 按钮的 SimpleFlyout 相匹配）
            flyGroup.FlyoutType = (int)swCommandFlyoutStyle_e.swCommandFlyoutStyle_Simple;

            // 注意：下拉菜单项不在启动时添加，而是在 FlyoutCallback 中动态添加。
            // SOLIDWORKS 官方示例与社区实践均如此：启动时添加会导致点击下拉按钮报“遇到无效参数”。

            // 记录下拉按钮的命令ID，用于添加到Ribbon
            _flyoutCmdId = flyGroup.CmdID;
        }

        // ========================================
        // 图标加载
        // ========================================

        /// <summary>
        /// 从DLL同级目录的icons文件夹加载图标路径
        /// </summary>
        private void LoadIcons()
        {
            string iconPath = RegDllPath();
            string dir = $@"{iconPath}\icons";

            // 工具栏按钮图标（6种尺寸，每个文件是3个图标的横向图标条）
            icons[0] = $@"{dir}\toolbar20x.png";
            icons[1] = $@"{dir}\toolbar32x.png";
            icons[2] = $@"{dir}\toolbar40x.png";
            icons[3] = $@"{dir}\toolbar64x.png";
            icons[4] = $@"{dir}\toolbar96x.png";
            icons[5] = $@"{dir}\toolbar128x.png";

            // 命令组主图标（6种尺寸）
            mainIcons[0] = $@"{dir}\mainicon_20.png";
            mainIcons[1] = $@"{dir}\mainicon_32.png";
            mainIcons[2] = $@"{dir}\mainicon_40.png";
            mainIcons[3] = $@"{dir}\mainicon_64.png";
            mainIcons[4] = $@"{dir}\mainicon_96.png";
            mainIcons[5] = $@"{dir}\mainicon_128.png";

            // 下拉菜单按钮图标（6种尺寸）
            flyoutMainIcons[0] = $@"{dir}\flyoutmain_20.png";
            flyoutMainIcons[1] = $@"{dir}\flyoutmain_32.png";
            flyoutMainIcons[2] = $@"{dir}\flyoutmain_40.png";
            flyoutMainIcons[3] = $@"{dir}\flyoutmain_64.png";
            flyoutMainIcons[4] = $@"{dir}\flyoutmain_96.png";
            flyoutMainIcons[5] = $@"{dir}\flyoutmain_128.png";

            // 下拉菜单项图标（6种尺寸，图标条）
            flyoutIcons[0] = $@"{dir}\flyout20x.png";
            flyoutIcons[1] = $@"{dir}\flyout32x.png";
            flyoutIcons[2] = $@"{dir}\flyout40x.png";
            flyoutIcons[3] = $@"{dir}\flyout64x.png";
            flyoutIcons[4] = $@"{dir}\flyout96x.png";
            flyoutIcons[5] = $@"{dir}\flyout128x.png";
        }

        // ========================================
        // Ribbon标签页
        // ========================================

        /// <summary>
        /// 为指定文档类型添加Ribbon标签页
        /// </summary>
        /// <param name="docType">文档类型</param>
        /// <param name="title">标签页标题</param>
        /// <param name="cmdGroup">关联的命令组</param>
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

            // 下拉按钮样式：简单下拉（点击按钮仅展开菜单，不执行默认动作）
            int flyoutStyle = (int)swCommandTabButtonFlyoutStyle_e.swCommandTabButton_SimpleFlyout;

            // 第一个分组：普通按钮（打开窗体、信息提示、开关按钮）
            CommandTabBox boxButtons = cmdTab.AddCommandTabBox();
            int[] buttonIDs = new int[]
            {
                cmdGroup.get_CommandID(0),
                cmdGroup.get_CommandID(1),
                cmdGroup.get_CommandID(2)
            };
            int[] buttonTextTypes = new int[] { textBelow, textBelow, textBelow };
            boxButtons.AddCommands(buttonIDs, buttonTextTypes);

            // 第二个分组：下拉菜单按钮（Flyout）
            // 多个CommandTabBox之间会自动显示分隔线
            if (_flyoutCmdId > 0)
            {
                CommandTabBox boxFlyout = cmdTab.AddCommandTabBox();
                int[] flyoutIDs = new int[] { _flyoutCmdId };
                int[] flyoutTextTypes = new int[] { textBelow | flyoutStyle };
                boxFlyout.AddCommands(flyoutIDs, flyoutTextTypes);
            }
        }

        // ========================================
        // 按钮回调
        // ========================================

        /// <summary>
        /// 弹出“对话框 1”（frmMain），保持单例
        /// </summary>
        private void ShowDialog1()
        {
            // 如果窗体未创建或已关闭，则创建新实例
            if (_frmMainInstance == null || _frmMainInstance.IsDisposed)
            {
                _frmMainInstance = new frmMain();

                // 设置窗体关闭时只隐藏不释放，保持单例
                _frmMainInstance.FormClosing += (sender, e) =>
                {
                    e.Cancel = true;           // 取消关闭操作
                    _frmMainInstance.Hide();   // 隐藏窗体
                };
            }

            // 显示窗体（如果已隐藏则重新显示，如果已在显示则置顶）
            if (!_frmMainInstance.Visible)
            {
                _frmMainInstance.Show();
            }

            // 将窗体带到最前面
            _frmMainInstance.BringToFront();
            _frmMainInstance.Activate();
        }

        /// <summary>按钮1点击回调：打开“对话框 1”（frmMain）</summary>
        public void OnCmd1_Click()
        {
            ShowDialog1();
        }

        /// <summary>按钮2点击回调：显示一条提示信息</summary>
        public void OnCmd2_Click()
        {
            iSwApp.SendMsgToUser("这是按钮2的提示信息");
        }

        /// <summary>按钮3点击回调：切换开关状态</summary>
        public void OnCmd3_Click()
        {
            _toggleState = !_toggleState;
            iSwApp.SendMsgToUser($"开关状态：{(_toggleState ? "开" : "关")}");
        }

        /// <summary>按钮启用状态回调：恒启用</summary>
        public int OnEnableAll()
        {
            return 1;
        }

        /// <summary>
        /// 开关按钮的状态回调
        /// 返回值：0=取消选中并禁用, 1=取消选中并启用, 2=选中并禁用, 3=选中并启用
        /// </summary>
        public int OnEnableToggle()
        {
            return _toggleState ? 3 : 1;
        }

        // ========================================
        // 下拉菜单（Flyout）回调
        // 采用“无参数”回调约定（与官方 Flyout 示例一致），
        // 每个下拉菜单项使用各自独立的回调方法。
        // ========================================

        /// <summary>
        /// 下拉按钮点击回调：展开前动态构建两个菜单项（不弹出对话框）。
        /// 官方示例即在此回调中 RemoveAllCommandItems + AddCommandItem。
        /// </summary>
        public void FlyoutCallback()
        {
            FlyoutGroup flyGroup = iCmdMgr.GetFlyoutGroup(flyoutGroupID) as FlyoutGroup;
            if (flyGroup == null)
            {
                return;
            }

            flyGroup.RemoveAllCommandItems();
            flyGroup.AddCommandItem("菜单项 1", "弹出对话框 1", 0, "FlyoutItem1", "FlyoutItemEnable");
            flyGroup.AddCommandItem("菜单项 2", "弹出对话框 2", 1, "FlyoutItem2", "FlyoutItemEnable");
        }

        /// <summary>下拉按钮启用回调</summary>
        public int FlyoutEnable()
        {
            return 1;
        }

        /// <summary>下拉菜单项启用回调：两个菜单项均启用</summary>
        public int FlyoutItemEnable()
        {
            return 1;
        }

        /// <summary>下拉菜单项1回调：弹出提示信息</summary>
        public void FlyoutItem1()
        {
            MessageBox.Show("这是下拉菜单项 1 弹出的提示信息", "菜单项 1", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>下拉菜单项2回调：弹出提示信息</summary>
        public void FlyoutItem2()
        {
            MessageBox.Show("这是下拉菜单项 2 弹出的提示信息", "菜单项 2", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ========================================
        // 工具方法
        // ========================================

        /// <summary>
        /// 比较两组ID是否相同
        /// </summary>
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

        /// <summary>
        /// 获取当前DLL所在目录路径
        /// </summary>
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