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

namespace SampleStatusBar
{
    /// <summary>
    /// 演示 SOLIDWORKS 的状态栏：
    ///   1. 用 IFrame.SetStatusBarText 修改底部状态栏的提示文本；
    ///   2. 用 IFrame.GetStatusBarPane 获取状态栏窗格，并用开关按钮切换其显示。
    /// </summary>
    [ComVisible(true)]
    [DisplayName("SampleStatusBar")]
    [Description("状态栏 add-in 示例")]
    [Guid("2dea7e2a-f15c-443f-9ad3-e3eaf8dd8a7d")]
    public class StatusBarAddin : ISwAddin
    {
        // ========================================
        // 字段
        // ========================================
        private ISldWorks iSwApp = null;
        private ICommandManager iCmdMgr = null;
        private int addinCookieID;

        /// <summary>主命令组ID</summary>
        public const int mainCmdGroupID = 7001;

        /// <summary>按钮1：设置状态栏文本</summary>
        public const int cmdStatusTextId = 1001;

        /// <summary>按钮2：切换状态栏窗格</summary>
        public const int cmdStatusPaneId = 1002;

        /// <summary>状态栏窗格当前显示状态</summary>
        private bool _paneVisible = false;

        /// <summary>状态栏窗格对象（只创建一次，反复调用 GetStatusBarPane 会产生多个窗格）</summary>
        private StatusBarPane _pane = null;

        private string[] mainIcons = new string[6];
        private string[] icons = new string[6];

        // ========================================
        // 插件生命周期
        // ========================================

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            iSwApp = (ISldWorks)ThisSW;
            addinCookieID = Cookie;

            iSwApp.SetAddinCallbackInfo(0, this, addinCookieID);

            iCmdMgr = iSwApp.GetCommandManager(addinCookieID);

            AddCommandMgr();

            return true;
        }

        public bool DisconnectFromSW()
        {
            iCmdMgr.RemoveCommandGroup(mainCmdGroupID);

            if (_pane != null)
            {
                Marshal.ReleaseComObject(_pane);
                _pane = null;
            }

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
        // 工具栏创建
        // ========================================

        public void AddCommandMgr()
        {
            try
            {
                ICommandGroup cmdGroup;

                string Title = "SampleStatusBar";
                string ToolTip = "SampleStatusBar";

                int[] docTypes = new int[]{
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swDocumentTypes_e.swDocDRAWING,
                    (int)swDocumentTypes_e.swDocPART
                };

                int cmdGroupErr = 0;
                bool ignorePrevious = false;

                object registryIDs;
                bool getDataResult = iCmdMgr.GetGroupDataFromRegistry(mainCmdGroupID, out registryIDs);

                int[] knownIDs = new int[] { cmdStatusTextId, cmdStatusPaneId };

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

                cmdGroup.AddCommandItem2("状态栏文本", -1, "修改底部状态栏的提示文本", "状态栏文本",
                    0, "OnSetStatusText", "OnEnable", cmdStatusTextId, menuToolbarOption);

                cmdGroup.AddCommandItem2("状态栏窗格", -1, "显示/隐藏自定义状态栏窗格", "状态栏窗格",
                    1, "OnTogglePane", "OnPaneEnable", cmdStatusPaneId, menuToolbarOption);

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

        // ========================================
        // 图标加载
        // ========================================

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

        // ========================================
        // Ribbon标签页
        // ========================================

        private void AddRibbonTab(int docType, string title, ICommandGroup cmdGroup)
        {
            CommandTab cmdTab = iCmdMgr.GetCommandTab(docType, title);

            if (cmdTab != null)
            {
                iCmdMgr.RemoveCommandTab(cmdTab);
                cmdTab = null;
            }

            cmdTab = iCmdMgr.AddCommandTab(docType, title);

            int textBelow = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow;

            CommandTabBox box = cmdTab.AddCommandTabBox();
            int[] buttonIDs = new int[]
            {
                cmdGroup.get_CommandID(0),
                cmdGroup.get_CommandID(1)
            };
            int[] buttonTextTypes = new int[] { textBelow, textBelow };
            box.AddCommands(buttonIDs, buttonTextTypes);
        }

        // ========================================
        // 命令回调
        // ========================================

        /// <summary>按钮启用状态回调：恒启用</summary>
        public int OnEnable()
        {
            return 1;
        }

        /// <summary>状态栏窗格按钮的更新回调：返回选中/启用状态，实现开关按钮</summary>
        public int OnPaneEnable()
        {
            // 1 = 未选中且启用；3 = 选中且启用
            return _paneVisible ? 3 : 1;
        }

        /// <summary>按钮1：修改状态栏提示文本</summary>
        public void OnSetStatusText()
        {
            Frame frame = iSwApp.IFrameObject();
            if (frame != null)
            {
                frame.SetStatusBarText("状态栏文本已更新：" + DateTime.Now.ToString("HH:mm:ss"));
            }
        }

        /// <summary>按钮2：切换状态栏窗格显示</summary>
        public void OnTogglePane()
        {
            // 窗格只创建一次，之后仅切换其可见性，避免每次调用产生多个窗格
            if (_pane == null)
            {
                Frame frame = iSwApp.IFrameObject();
                if (frame == null)
                {
                    return;
                }

                _pane = frame.GetStatusBarPane();
                if (_pane == null)
                {
                    iSwApp.SendMsgToUser("未获取到状态栏窗格。");
                    return;
                }

                _pane.Text = "自定义窗格";
            }

            _paneVisible = !_paneVisible;
            _pane.Visible = _paneVisible;
        }

        // ========================================
        // 工具方法
        // ========================================

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
