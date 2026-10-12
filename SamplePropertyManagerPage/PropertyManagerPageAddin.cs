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

namespace SamplePropertyManagerPage
{
    /// <summary>
    /// 演示如何在 SOLIDWORKS 插件中创建并显示「属性管理器页（PropertyManager Page）」。
    /// 点击工具栏/菜单按钮后弹出一个自定义属性管理器页，页面上包含标签、文本框、
    /// 下拉框和复选框等控件。
    /// </summary>
    [ComVisible(true)]
    [DisplayName("SamplePropertyManagerPage")]
    [Description("属性管理器页 add-in 示例")]
    [Guid("aa3708d3-77d7-46c8-96f9-ea7e9094c2d6")]
    public class PropertyManagerPageAddin : ISwAddin
    {
        // ========================================
        // 字段
        // ========================================
        private ISldWorks iSwApp = null;
        private ICommandManager iCmdMgr = null;
        private int addinCookieID;

        /// <summary>主命令组ID</summary>
        public const int mainCmdGroupID = 6001;

        /// <summary>按钮命令ID（打开属性管理器页）</summary>
        public const int cmdButtonId = 1102;

        /// <summary>主图标6种尺寸（命令组大图标）</summary>
        private string[] mainIcons = new string[6];

        /// <summary>工具栏图标6种尺寸（按钮图标，每个文件是一张横向图标条）</summary>
        private string[] icons = new string[6];

        /// <summary>属性管理器页处理程序（保持引用，避免显示期间被回收）</summary>
        private PropertyPageHandler _pageHandler;

        /// <summary>内嵌到属性管理器页里的 WinForms 控件（保持引用，避免被回收）</summary>
        private System.Windows.Forms.Control _embeddedControl;

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
            iCmdMgr.RemoveCommandGroup(mainCmdGroupID);

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

        /// <summary>
        /// 创建命令组、添加按钮、设置图标、激活、添加Ribbon标签页
        /// </summary>
        public void AddCommandMgr()
        {
            try
            {
                ICommandGroup cmdGroup;

                string Title = "SamplePropertyManagerPage";
                string ToolTip = "SamplePropertyManagerPage";

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

                // 设置图标路径（DLL同级目录下的icons文件夹）
                LoadIcons();

                cmdGroup.MainIconList = mainIcons;
                cmdGroup.IconList = icons;

                int menuToolbarOption = (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem);

                cmdGroup.AddCommandItem2(
                    "属性管理器页",
                    -1,
                    "打开测试用属性管理器页",
                    "属性管理器页",
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

            // 工具栏按钮图标（6种尺寸，每个文件是图标的横向图标条）
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
        }

        // ========================================
        // Ribbon标签页
        // ========================================

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

        // ========================================
        // 命令回调
        // ========================================

        /// <summary>按钮启用状态回调：恒启用</summary>
        public int OnEnable()
        {
            return 1;
        }

        /// <summary>按钮点击回调：打开属性管理器页</summary>
        public void OnCmd_Click()
        {
            ShowPropertyPage();
        }

        /// <summary>
        /// 创建并显示自定义属性管理器页（尽可能覆盖 SOLIDWORKS 支持的全部控件类型）。
        /// 每个控件单独捕获异常：若某控件在当前版本/系统不可用，则跳过该控件并在页面
        /// 顶部提示信息中说明，页面其余控件仍可正常显示。
        /// 注意：内嵌窗口（WindowFromHandle）与 ActiveX 必须用 AddControl2 添加。
        /// </summary>
        private void ShowPropertyPage()
        {
            try
            {
                // 页面处理程序（接收页面的各种事件回调）
                _pageHandler = new PropertyPageHandler(iSwApp);

                int errors = 0;

                // 页面按钮：确定 + 取消
                int options = (int)swPropertyManagerPageOptions_e.swPropertyManagerOptions_OkayButton
                            | (int)swPropertyManagerPageOptions_e.swPropertyManagerOptions_CancelButton;

                PropertyManagerPage2 page = iSwApp.ICreatePropertyManagerPage(
                    "属性管理器页测试（全部控件）", options, _pageHandler, ref errors);

                if (page == null)
                {
                    iSwApp.SendMsgToUser("创建属性管理器页失败，错误码：" + errors);
                    return;
                }

                string baseMessage = "【测试用界面】本页演示属性管理器页支持的全部控件，不会修改模型。可随意填写，点击“确定”后弹出所填内容。";

                // 顶部提示信息
                page.SetMessage2(baseMessage, (int)swPropertyManagerPageMessageVisibility.swMessageBoxVisible, "说明");

                // 常用参数
                int groupOptions = (int)swAddGroupBoxOptions_e.swGroupBoxOptions_Visible
                                 | (int)swAddGroupBoxOptions_e.swGroupBoxOptions_Expanded;
                short alignLeft = (short)swPropertyManagerPageControlLeftAlign_e.swControlAlign_LeftEdge;
                int visEnabled = (int)swAddControlOptions_e.swControlOptions_Visible
                               | (int)swAddControlOptions_e.swControlOptions_Enabled;

                // 控件类型简写
                short LBL = (short)swPropertyManagerPageControlType_e.swControlType_Label;
                short TXT = (short)swPropertyManagerPageControlType_e.swControlType_Textbox;
                short NUM = (short)swPropertyManagerPageControlType_e.swControlType_Numberbox;
                short SLD = (short)swPropertyManagerPageControlType_e.swControlType_Slider;
                short LST = (short)swPropertyManagerPageControlType_e.swControlType_Listbox;
                short CMB = (short)swPropertyManagerPageControlType_e.swControlType_Combobox;
                short CHK = (short)swPropertyManagerPageControlType_e.swControlType_Checkbox;
                short OPT = (short)swPropertyManagerPageControlType_e.swControlType_Option;
                short BTN = (short)swPropertyManagerPageControlType_e.swControlType_Button;
                short BMP = (short)swPropertyManagerPageControlType_e.swControlType_Bitmap;
                short BMPBTN = (short)swPropertyManagerPageControlType_e.swControlType_BitmapButton;
                short SELBOX = (short)swPropertyManagerPageControlType_e.swControlType_Selectionbox;
                short WNDH = (short)swPropertyManagerPageControlType_e.swControlType_WindowFromHandle;
                short AX = (short)swPropertyManagerPageControlType_e.swControlType_ActiveX;

                // 每个控件单独捕获异常：不可用时跳过并记录，页面仍显示
                List<string> failed = new List<string>();
                Action<string, Action> tryDo = (name, act) =>
                {
                    try { act(); }
                    catch (Exception ex) { failed.Add(name + "：" + ex.Message); }
                };

                PropertyManagerPageGroup g1 = null, g2 = null, g3 = null, g4 = null, g5 = null, g6 = null, g7 = null, g8 = null, g9 = null;

                // ---------- 分组1：文本 / 数值 / 滑块 ----------
                tryDo("分组1-文本框", () =>
                {
                    g1 = (PropertyManagerPageGroup)page.AddGroupBox(1, "文本 / 数值", groupOptions);
                    g1.AddControl(20, LBL, "名称（Textbox）：", alignLeft, 0, "");
                    PropertyManagerPageTextbox txt = (PropertyManagerPageTextbox)g1.AddControl(
                        21, TXT, "", alignLeft, visEnabled, "测试用文本框，不影响模型");
                    txt.Text = "测试文本";
                });

                tryDo("分组1-数值框", () =>
                {
                    g1.AddControl(22, LBL, "数量（Numberbox）：", alignLeft, 0, "");
                    PropertyManagerPageNumberbox num = (PropertyManagerPageNumberbox)g1.AddControl(
                        23, NUM, "", alignLeft, visEnabled, "测试用数值框，不影响模型");
                    num.SetRange2((int)swNumberboxUnitType_e.swNumberBox_UnitlessDouble, 0, 100, true, 1, 10, 1);
                    num.Value = 10;
                });

                tryDo("分组1-滑块", () =>
                {
                    g1.AddControl(24, LBL, "滑块（Slider）：", alignLeft, 0, "");
                    PropertyManagerPageSlider slider = (PropertyManagerPageSlider)g1.AddControl(
                        25, SLD, "", alignLeft, visEnabled, "测试用滑块，不影响模型");
                    slider.SetRange(0, 10);
                    slider.Position = 5;
                    slider.TickFrequency = 1;
                });

                // ---------- 分组2：列表 / 下拉 / 复选框 ----------
                tryDo("分组2-列表框", () =>
                {
                    g2 = (PropertyManagerPageGroup)page.AddGroupBox(2, "列表 / 下拉 / 复选框", groupOptions);
                    g2.AddControl(30, LBL, "列表框（Listbox）：", alignLeft, 0, "");
                    PropertyManagerPageListbox list = (PropertyManagerPageListbox)g2.AddControl(
                        31, LST, "", alignLeft, visEnabled, "测试用列表框，不影响模型");
                    list.AddItems(new string[]
                    {
                        "列表项 1（普通钢）", "列表项 2（不锈钢）", "列表项 3（铝合金）",
                        "列表项 4（铜合金）", "列表项 5（钛合金）", "列表项 6（塑料）",
                        "列表项 7（橡胶）", "列表项 8（木材）", "列表项 9（陶瓷）", "列表项 10（复合材料）"
                    });
                    list.Height = 80;
                    list.CurrentSelection = 0;
                });

                tryDo("分组2-下拉框", () =>
                {
                    g2.AddControl(32, LBL, "下拉框（Combobox）：", alignLeft, 0, "");
                    PropertyManagerPageCombobox cmb = (PropertyManagerPageCombobox)g2.AddControl(
                        33, CMB, "", alignLeft, visEnabled, "测试用下拉框，不影响模型");
                    cmb.AddItems(new string[]
                    {
                        "下拉项 A（默认）", "下拉项 B", "下拉项 C", "下拉项 D",
                        "下拉项 E", "下拉项 F", "下拉项 G", "下拉项 H"
                    });
                    cmb.CurrentSelection = 0;
                });

                tryDo("分组2-复选框", () =>
                {
                    PropertyManagerPageCheckbox chk = (PropertyManagerPageCheckbox)g2.AddControl(
                        34, CHK, "复选框测试", alignLeft, visEnabled, "测试用复选框，不影响模型");
                    chk.Checked = true;
                });

                // ---------- 分组3：单选按钮（Option） ----------
                tryDo("分组3-单选按钮", () =>
                {
                    g3 = (PropertyManagerPageGroup)page.AddGroupBox(3, "单选按钮（Option）", groupOptions);
                    g3.AddControl(40, LBL, "请选择一项：", alignLeft, 0, "");
                    PropertyManagerPageOption opt1 = (PropertyManagerPageOption)g3.AddControl(
                        41, OPT, "选项一", alignLeft, visEnabled, "");
                    opt1.Checked = true;
                    g3.AddControl(42, OPT, "选项二", alignLeft, visEnabled, "");
                    g3.AddControl(43, OPT, "选项三", alignLeft, visEnabled, "");
                });

                // ---------- 分组4：按钮（Button） ----------
                tryDo("分组4-按钮", () =>
                {
                    g4 = (PropertyManagerPageGroup)page.AddGroupBox(4, "按钮（Button）", groupOptions);
                    g4.AddControl(50, LBL, "点击下面的按钮弹出提示框：", alignLeft, 0, "");
                    g4.AddControl(51, BTN, "普通按钮", alignLeft, visEnabled, "点击后弹窗提示");
                });

                // ---------- 分组5：位图（Bitmap） ----------
                tryDo("分组5-位图（Bitmap）", () =>
                {
                    g5 = (PropertyManagerPageGroup)page.AddGroupBox(5, "位图（Bitmap）", groupOptions);
                    g5.AddControl(52, LBL, "静态图片：", alignLeft, 0, "");
                    PropertyManagerPageBitmap bmp = (PropertyManagerPageBitmap)g5.AddControl(
                        53, BMP, "", alignLeft, visEnabled, "");
                    // 优先使用 icons 目录下的图片文件；失败则退回 SOLIDWORKS 内置位图
                    string bmpFile = System.IO.Path.Combine(RegDllPath(), "icons", "pmp_bitmap.bmp");
                    bool ok = false;
                    try
                    {
                        ok = System.IO.File.Exists(bmpFile) && bmp.SetBitmapByName(bmpFile, bmpFile);
                    }
                    catch
                    {
                        ok = false;
                    }

                    if (!ok)
                    {
                        bmp.SetStandardBitmap((int)swBitmapControlStandardTypes_e.swBitmapControl_Volume);
                    }
                });

                // ---------- 分组6：位图按钮（BitmapButton） ----------
                tryDo("分组6-位图按钮", () =>
                {
                    g6 = (PropertyManagerPageGroup)page.AddGroupBox(6, "位图按钮（BitmapButton）", groupOptions);
                    g6.AddControl(54, LBL, "位图按钮（点击弹窗）：", alignLeft, 0, "");
                    PropertyManagerPageBitmapButton b = (PropertyManagerPageBitmapButton)g6.AddControl(
                        55, BMPBTN, "", alignLeft, visEnabled, "位图按钮，点击后弹窗提示");
                    b.SetStandardBitmaps((int)swPropertyManagerPageBitmapButtons_e.swBitmapButtonImage_angle);

                    g6.AddControl(56, LBL, "可勾选位图按钮：", alignLeft, 0, "");
                    PropertyManagerPageBitmapButton cb = (PropertyManagerPageBitmapButton)g6.AddControl2(
                        57, BMPBTN, "", alignLeft, visEnabled, "可勾选的位图按钮");
                    cb.SetStandardBitmaps((int)swPropertyManagerPageBitmapButtons_e.swBitmapButtonImage_distance1);
                    try
                    {
                        cb.IsCheckable = true;
                        cb.Checked = true;
                    }
                    catch
                    {
                        // 该版本不支持勾选状态则退化为普通位图按钮
                    }
                    _pageHandler.CheckableBitmapButton = cb;
                });

                // ---------- 分组7：选择框（Selectionbox） ----------
                tryDo("分组7-选择框", () =>
                {
                    g7 = (PropertyManagerPageGroup)page.AddGroupBox(7, "选择框（Selectionbox）", groupOptions);
                    g7.AddControl(60, LBL, "请选择面或边：", alignLeft, 0, "");
                    PropertyManagerPageSelectionbox sel = (PropertyManagerPageSelectionbox)g7.AddControl(
                        61, SELBOX, "", alignLeft, visEnabled, "可选取面或边");
                    sel.SetSelectionFilters(new int[] { (int)swSelectType_e.swSelFACES, (int)swSelectType_e.swSelEDGES });
                    sel.Height = 50;
                    sel.SingleEntityOnly = false;
                });

                // ---------- 分组8：内嵌 WinForms 控件（WindowFromHandle，用 AddControl2） ----------
                tryDo("分组8-内嵌WinForms", () =>
                {
                    g8 = (PropertyManagerPageGroup)page.AddGroupBox(8, "内嵌控件（WindowFromHandle）", groupOptions);
                    g8.AddControl(70, LBL, "内嵌的 WinForms 控件：", alignLeft, 0, "");
                    PropertyManagerPageWindowFromHandle winHost = (PropertyManagerPageWindowFromHandle)g8.AddControl2(
                        71, WNDH, "", alignLeft, visEnabled, "");
                    winHost.Height = 60;
                    _embeddedControl = BuildEmbeddedControl();
                    _embeddedControl.CreateControl();
                    winHost.SetWindowHandlex64(_embeddedControl.Handle.ToInt64());
                });

                // ---------- 分组9：ActiveX 控件（Microsoft Web Browser，用 AddControl2） ----------
                tryDo("分组9-ActiveX", () =>
                {
                    g9 = (PropertyManagerPageGroup)page.AddGroupBox(9, "ActiveX 控件", groupOptions);
                    g9.AddControl(80, LBL, "ActiveX（Microsoft Web Browser，随 Windows 自带）：", alignLeft, 0, "");
                    PropertyManagerPageActiveX ax = (PropertyManagerPageActiveX)g9.AddControl2(
                        81, AX, "", alignLeft, visEnabled, "");
                    ax.Height = 80;
                    // Microsoft Web Browser (Shell.Explorer)，CLSID 固定，普通 Windows 电脑都自带；
                    // 创建完成后会回调 OnActiveXControlCreated，在那里向浏览器写入内容。
                    ax.SetClass("{8856F961-340A-11D0-A96B-00C04FD705A2}", "");

                    _pageHandler.ActiveXId = 81;
                    _pageHandler.ActiveXControl = ax;
                });

                // 写入初始值，避免用户不操作时汇总为空
                _pageHandler.SetInitial("测试文本", 10, 5, "索引 0", "索引 0", true);

                // 若有控件被跳过，在提示信息中说明
                if (failed.Count > 0)
                {
                    page.SetMessage2(
                        baseMessage + System.Environment.NewLine + System.Environment.NewLine +
                        "以下控件在本机不可用，已跳过：" + System.Environment.NewLine +
                        string.Join(System.Environment.NewLine, failed),
                        (int)swPropertyManagerPageMessageVisibility.swMessageBoxVisible,
                        "说明");
                }

                // 显示页面
                page.Show2(0);
            }
            catch (Exception ex)
            {
                iSwApp.SendMsgToUser(ex.Message);
            }
        }

        /// <summary>
        /// 构建内嵌到属性管理器页里的 WinForms 控件
        /// </summary>
        private System.Windows.Forms.Control BuildEmbeddedControl()
        {
            System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel();
            panel.BackColor = System.Drawing.Color.WhiteSmoke;
            panel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

            System.Windows.Forms.Label label = new System.Windows.Forms.Label();
            label.Text = "这是嵌进属性管理器页的 WinForms 控件";
            label.AutoSize = true;
            label.Location = new System.Drawing.Point(8, 8);
            panel.Controls.Add(label);

            System.Windows.Forms.Button button = new System.Windows.Forms.Button();
            button.Text = "WinForms 按钮";
            button.Location = new System.Drawing.Point(8, 30);
            button.Click += (s, e) => iSwApp.SendMsgToUser("WinForms 按钮被点击（此非 SOLIDWORKS 原生控件）");
            panel.Controls.Add(button);

            return panel;
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
