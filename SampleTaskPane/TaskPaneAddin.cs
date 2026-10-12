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

namespace SampleTaskPane
{
    /// <summary>
    /// 演示如何在 SOLIDWORKS 插件中创建「任务窗格（Task Pane）」，并把一个
    /// WinForms 用户控件（内含大量常见控件）嵌入其中。
    /// 核心 API：
    ///   1. ISldWorks.CreateTaskpaneView2(bitmap, toolTip) 创建任务窗格页；
    ///   2. ITaskpaneView.DisplayWindowFromHandlex64(handle) 把 .NET 控件窗口嵌入；
    ///   3. ITaskpaneView.AddStandardButton / AddCustomButton 添加工具栏按钮；
    ///   4. ITaskpaneView 的 TaskPaneToolbarButtonClicked 事件处理按钮点击。
    /// 入口：SOLIDWORKS 加载插件时调用 ConnectToSW；插件没有 Main。
    /// 界面上通过工具栏/Ribbon 按钮「任务窗格」显示 / 隐藏任务窗格。
    /// </summary>
    [ComVisible(true)]
    [DisplayName("SampleTaskPane")]
    [Description("任务窗格(Task Pane) add-in 示例")]
    [Guid("7f4c2b19-3e6a-4d58-9b1c-2a5d8e7f3c62")]
    public class TaskPaneAddin : ISwAddin
    {
        // ========================================
        // 字段
        // ========================================
        private ISldWorks iSwApp = null;
        private ICommandManager iCmdMgr = null;
        private int addinCookieID;

        /// <summary>主命令组ID</summary>
        public const int mainCmdGroupID = 7001;

        /// <summary>按钮命令ID（显示/隐藏任务窗格）</summary>
        public const int cmdButtonId = 1202;

        /// <summary>主图标6种尺寸（命令组大图标）</summary>
        private string[] mainIcons = new string[6];

        /// <summary>工具栏图标6种尺寸（按钮图标，每个文件是一张横向图标条）</summary>
        private string[] icons = new string[6];

        /// <summary>任务窗格视图（保持引用，避免被回收）</summary>
        private TaskpaneView _taskPaneView;

        /// <summary>嵌入到任务窗格中的 WinForms 控件（保持引用）</summary>
        private TaskPaneControl _control;

        /// <summary>任务窗格当前是否可见（用于 toggle 按钮的按下状态）</summary>
        private bool _paneVisible;

        /// <summary>宿主父窗口句柄（只读使用，用于把控件拉到与任务窗格等宽）</summary>
        private IntPtr _hostParent = IntPtr.Zero;

        /// <summary>尺寸跟随定时器（轮询宿主客户区，安全：只改本控件尺寸）</summary>
        private System.Windows.Forms.Timer _sizeTimer;

        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

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

            // 创建工具栏/Ribbon按钮
            AddCommandMgr();

            // 创建任务窗格（默认显示）
            CreateTaskPane();

            return true;
        }

        /// <summary>
        /// 【出口】断开与SOLIDWORKS的连接
        /// </summary>
        public bool DisconnectFromSW()
        {
            try
            {
                StopSizeWatch();

                if (_taskPaneView != null)
                {
                    _taskPaneView.DeleteView();
                    Marshal.ReleaseComObject(_taskPaneView);
                    _taskPaneView = null;
                }

                _control = null;

                if (iCmdMgr != null)
                {
                    iCmdMgr.RemoveCommandGroup(mainCmdGroupID);
                    Marshal.ReleaseComObject(iCmdMgr);
                    iCmdMgr = null;
                }
            }
            catch (Exception ex)
            {
                iSwApp.SendMsgToUser(ex.Message);
            }

            Marshal.ReleaseComObject(iSwApp);
            iSwApp = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            return true;
        }

        // ========================================
        // 工具栏 / Ribbon
        // ========================================

        /// <summary>
        /// 创建命令组、添加按钮、设置图标、激活、添加Ribbon标签页
        /// </summary>
        public void AddCommandMgr()
        {
            try
            {
                ICommandGroup cmdGroup;

                string Title = "SampleTaskPane";
                string ToolTip = "SampleTaskPane";

                int[] docTypes = new int[]{
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swDocumentTypes_e.swDocDRAWING,
                    (int)swDocumentTypes_e.swDocPART
                };

                int cmdGroupErr = 0;
                bool ignorePrevious = false;

                object registryIDs;
                bool getDataResult = iCmdMgr.GetGroupDataFromRegistry(mainCmdGroupID, out registryIDs);

                int[] knownIDs = new int[] { cmdButtonId };

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

                cmdGroup = iCmdMgr.CreateCommandGroup2(mainCmdGroupID, Title, ToolTip, "", -1, ignorePrevious, ref cmdGroupErr);

                LoadIcons();

                cmdGroup.MainIconList = mainIcons;
                cmdGroup.IconList = icons;

                int menuToolbarOption = (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem);

                cmdGroup.AddCommandItem2(
                    "任务窗格",
                    -1,
                    "显示/隐藏任务窗格",
                    "任务窗格",
                    0,
                    "OnCmd_Click",
                    "OnEnable",
                    cmdButtonId,
                    menuToolbarOption);

                cmdGroup.HasToolbar = true;
                cmdGroup.HasMenu = true;

                cmdGroup.Activate();

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

        /// <summary>
        /// 从DLL同级目录的icons文件夹加载图标路径
        /// </summary>
        private void LoadIcons()
        {
            string iconPath = RegDllPath();
            string dir = $@"{iconPath}\icons";

            icons[0] = $@"{dir}\toolbar20x.png";
            icons[1] = $@"{dir}\toolbar32x.png";
            icons[2] = $@"{dir}\toolbar40x.png";
            icons[3] = $@"{dir}\toolbar64x.png";
            icons[4] = $@"{dir}\toolbar96x.png";
            icons[5] = $@"{dir}\toolbar128x.png";

            mainIcons[0] = $@"{dir}\mainicon_20.png";
            mainIcons[1] = $@"{dir}\mainicon_32.png";
            mainIcons[2] = $@"{dir}\mainicon_40.png";
            mainIcons[3] = $@"{dir}\mainicon_64.png";
            mainIcons[4] = $@"{dir}\mainicon_96.png";
            mainIcons[5] = $@"{dir}\mainicon_128.png";
        }

        /// <summary>
        /// 为指定文档类型添加Ribbon标签页
        /// </summary>
        private void AddRibbonTab(int docType, string title, ICommandGroup cmdGroup)
        {
            CommandTab cmdTab;

            cmdTab = iCmdMgr.GetCommandTab(docType, title);

            if (cmdTab != null)
            {
                iCmdMgr.RemoveCommandTab(cmdTab);
                cmdTab = null;
            }

            cmdTab = iCmdMgr.AddCommandTab(docType, title);

            int textBelow = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;

            CommandTabBox boxButtons = cmdTab.AddCommandTabBox();
            int[] buttonIDs = new int[] { cmdGroup.get_CommandID(0) };
            int[] buttonTextTypes = new int[] { textBelow };
            boxButtons.AddCommands(buttonIDs, buttonTextTypes);
        }

        /// <summary>
        /// 按钮状态回调（SOLIDWORKS 在显示按钮前调用）：
        /// 返回值 0=未选中+禁用, 1=未选中+启用, 2=选中+禁用, 3=选中+启用。
        /// 返回 3 时按钮呈“按下/选中”外观，从而实现 toggle 按钮。
        /// </summary>
        public int OnEnable()
        {
            return _paneVisible ? 3 : 1;
        }

        /// <summary>按钮点击回调：切换任务窗格的显示 / 隐藏</summary>
        public void OnCmd_Click()
        {
            ToggleTaskPane();
        }

        // ========================================
        // 任务窗格
        // ========================================

        /// <summary>
        /// 创建任务窗格页，添加工具栏按钮，并嵌入 WinForms 控件
        /// </summary>
        private void CreateTaskPane()
        {
            try
            {
                if (_taskPaneView != null)
                {
                    return;
                }

                string iconDir = RegDllPath() + @"\icons";
                string icon = iconDir + @"\mainicon_20.png";

                // 1) 创建任务窗格页（图标 + 工具提示）
                _taskPaneView = iSwApp.CreateTaskpaneView2(icon, "SampleTaskPane 演示");

                if (_taskPaneView == null)
                {
                    iSwApp.SendMsgToUser("创建任务窗格失败：CreateTaskpaneView2 返回 null\n图标：" + icon);
                    return;
                }

                // 2) 任务窗格工具栏按钮
                _taskPaneView.AddStandardButton(
                    (int)swTaskPaneBitmapsOptions_e.swTaskPaneBitmapsOptions_Help, "帮助");
                _taskPaneView.AddStandardButton(
                    (int)swTaskPaneBitmapsOptions_e.swTaskPaneBitmapsOptions_Options, "选项");
                _taskPaneView.AddCustomButton(icon, "自定义按钮");

                // 3) 创建 WinForms 控件并嵌入到任务窗格
                _control = new TaskPaneControl();
                _control.CreateControl();   // 强制创建窗口句柄
                _taskPaneView.DisplayWindowFromHandlex64(_control.Handle.ToInt64());

                // 4) 订阅工具栏按钮点击事件（失败不影响任务窗格本身）
                try
                {
                    _taskPaneView.TaskPaneToolbarButtonClicked += OnTaskPaneToolbarButtonClicked;
                }
                catch (Exception)
                {
                }

                // 5) 显示任务窗格页
                _taskPaneView.ShowView();
                _paneVisible = true;

                // 6) 让嵌入控件跟随任务窗格宽度（轮询宿主客户区，只改本控件）
                StartSizeWatch();
            }
            catch (Exception ex)
            {
                iSwApp.SendMsgToUser(ex.Message);
            }
        }

        // ========================================
        // 尺寸跟随（安全：只改本控件，不碰宿主窗口）
        // ========================================

        /// <summary>
        /// 启动尺寸跟随：SOLIDWORKS 不会自动 resize 嵌入的控件，
        /// 这里用一个 UI 定时器轮询宿主父窗口的客户区，把控件拉到同样大小。
        /// 对宿主窗口只做只读调用（GetParent/GetClientRect），只修改本控件。
        /// </summary>
        private void StartSizeWatch()
        {
            if (_control == null || !_control.IsHandleCreated)
            {
                return;
            }

            _hostParent = GetParent(_control.Handle);

            if (_sizeTimer == null)
            {
                _sizeTimer = new System.Windows.Forms.Timer { Interval = 60 };
                _sizeTimer.Tick += (s, e) => SyncControlToHost();
            }
            _sizeTimer.Start();
        }

        private void StopSizeWatch()
        {
            if (_sizeTimer != null)
            {
                _sizeTimer.Stop();
                _sizeTimer.Dispose();
                _sizeTimer = null;
            }
        }

        /// <summary>把控件拉到宿主父窗口客户区大小（尺寸不变时什么都不做）。</summary>
        private void SyncControlToHost()
        {
            if (_control == null || !_control.IsHandleCreated)
            {
                return;
            }

            if (_hostParent == IntPtr.Zero || !IsWindow(_hostParent))
            {
                _hostParent = GetParent(_control.Handle);
                if (_hostParent == IntPtr.Zero)
                {
                    return;
                }
            }

            RECT rc;
            if (!GetClientRect(_hostParent, out rc))
            {
                return;
            }

            int clientW = rc.Right - rc.Left;
            int clientH = rc.Bottom - rc.Top;
            if (clientW <= 0 || clientH <= 0)
            {
                return;
            }

            // 保持 SOLIDWORKS 给控件设置的左上偏移（任务窗格顶部有一排原生按钮），
            // 只填满剩余区域，避免控件上移遮挡那排按钮。
            RECT cr;
            if (!GetWindowRect(_control.Handle, out cr))
            {
                return;
            }

            var origin = new POINT { X = 0, Y = 0 };
            ClientToScreen(_hostParent, ref origin);
            int offsetX = cr.Left - origin.X;
            int offsetY = cr.Top - origin.Y;
            if (offsetX < 0)
            {
                offsetX = 0;
            }
            if (offsetY < 0)
            {
                offsetY = 0;
            }

            int w = clientW - offsetX;
            int h = clientH - offsetY;
            if (w < 0 || h < 0)
            {
                return;
            }

            if (_control.Width != w || _control.Height != h)
            {
                SetWindowPos(_control.Handle, IntPtr.Zero, 0, 0, w, h,
                    SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
            }
        }

        /// <summary>
        /// 切换任务窗格的显示 / 隐藏（toggle 按钮）
        /// </summary>
        private void ToggleTaskPane()
        {
            try
            {
                if (_taskPaneView == null)
                {
                    CreateTaskPane();
                    return;
                }

                if (_paneVisible)
                {
                    // 注意：任务窗格处于“固定”状态时不会收起，需先取消固定
                    iSwApp.TaskPaneIsPinned = false;

                    _taskPaneView.HideView();
                    _paneVisible = false;
                }
                else
                {
                    _taskPaneView.ShowView();

                    // 固定后任务窗格保持展开
                    iSwApp.TaskPaneIsPinned = true;

                    _paneVisible = true;
                }
            }
            catch (Exception ex)
            {
                iSwApp.SendMsgToUser(ex.Message);
            }
        }

        /// <summary>
        /// 任务窗格工具栏按钮点击事件
        /// </summary>
        /// <param name="ButtonIndex">按钮索引（按添加顺序，从 0 开始）</param>
        private int OnTaskPaneToolbarButtonClicked(int ButtonIndex)
        {
            iSwApp.SendMsgToUser("任务窗格工具栏按钮被点击，索引：" + ButtonIndex);
            return 0;
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
