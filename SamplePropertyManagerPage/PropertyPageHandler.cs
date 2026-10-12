using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using System;
using System.Runtime.InteropServices;

namespace SamplePropertyManagerPage
{
    /// <summary>
    /// 属性管理器页的事件处理程序。
    /// 必须实现 IPropertyManagerPage2Handler9 接口，SOLIDWORKS 会在页面的不同
    /// 交互（修改控件、点击按钮、关闭页面等）时回调这里的方法。
    /// 注意：该类会作为 COM 回调对象传给 SOLIDWORKS，必须标记 [ComVisible(true)]，
    /// 否则在 CreatePropertyManagerPage 阶段无法生成 COM 包装，会抛出
    /// “指定的转换无效”（InvalidCastException）。
    /// </summary>
    [ComVisible(true)]
    public class PropertyPageHandler : IPropertyManagerPage2Handler9
    {
        private readonly ISldWorks _swApp;

        // 记录各控件当前值（在事件回调中更新），供 OnClose 汇总使用
        private string _text = "";
        private double _number = 10;
        private int _slider = 5;
        private string _list = "";
        private string _combo = "";
        private bool _checked;
        private int _selectionCount;
        private int _optionId = -1;
        private int _buttonClicks;

        /// <summary>可勾选的位图按钮（BitmapButton），由创建页面的代码赋值，OnClose 时读取其勾选状态</summary>
        public PropertyManagerPageBitmapButton CheckableBitmapButton;

        /// <summary>页面上的 ActiveX 控件（用于在其创建完成后加载内容）</summary>
        public PropertyManagerPageActiveX ActiveXControl;

        /// <summary>ActiveX 控件的控件 Id</summary>
        public int ActiveXId = -1;

        public PropertyPageHandler(ISldWorks swApp)
        {
            _swApp = swApp;
        }

        /// <summary>写入各控件的初始值，避免用户不操作时汇总为空</summary>
        public void SetInitial(string text, double number, int slider, string list, string combo, bool checkedState)
        {
            _text = text;
            _number = number;
            _slider = slider;
            _list = list;
            _combo = combo;
            _checked = checkedState;
        }

        #region 关心的回调

        public void OnTextboxChanged(int Id, string Text) { _text = Text; }

        public void OnNumberboxChanged(int Id, double Value) { _number = Value; }

        public void OnNumberBoxTrackingCompleted(int Id, double Value) { _number = Value; }

        public void OnSliderPositionChanged(int Id, double Value) { _slider = (int)Value; }

        public void OnSliderTrackingCompleted(int Id, double Value) { _slider = (int)Value; }

        public void OnListboxSelectionChanged(int Id, int Item) { _list = "索引 " + Item; }

        public void OnComboboxSelectionChanged(int Id, int Item) { _combo = "索引 " + Item; }

        public void OnComboboxEditChanged(int Id, string Text) { _combo = Text; }

        public void OnCheckboxCheck(int Id, bool Checked) { _checked = Checked; }

        public void OnSelectionboxListChanged(int Id, int Count) { _selectionCount = Count; }

        public void OnOptionCheck(int Id) { _optionId = Id; }

        public void OnButtonPress(int Id)
        {
            _buttonClicks++;
            _swApp.SendMsgToUser("按钮被点击（Id=" + Id + "）。本页仅演示控件，不会修改模型。");
        }

        /// <summary>页面关闭</summary>
        public void OnClose(int Reason)
        {
            if (Reason == (int)swPropertyManagerPageCloseReasons_e.swPropertyManagerPageClose_Okay)
            {
                bool bitmapChecked = CheckableBitmapButton != null && CheckableBitmapButton.Checked;
                _swApp.SendMsgToUser(
                    "【测试】属性管理器页已确定，以下为所填内容（未修改模型）：" + System.Environment.NewLine +
                    "名称 = " + _text + System.Environment.NewLine +
                    "数量 = " + _number + System.Environment.NewLine +
                    "滑块 = " + _slider + System.Environment.NewLine +
                    "列表 = " + _list + System.Environment.NewLine +
                    "下拉 = " + _combo + System.Environment.NewLine +
                    "复选框 = " + (_checked ? "是" : "否") + System.Environment.NewLine +
                    "已选实体数 = " + _selectionCount + System.Environment.NewLine +
                    "单选按钮 Id = " + _optionId + System.Environment.NewLine +
                    "按钮点击次数 = " + _buttonClicks + System.Environment.NewLine +
                    "位图按钮勾选 = " + (bitmapChecked ? "是" : "否"));
            }
        }

        #endregion

        #region IPropertyManagerPage2Handler9 其余回调（本示例未使用，留空实现）

        public void AfterActivation() { }

        public void AfterClose() { }

        public bool OnHelp() { return false; }

        public bool OnPreviousPage() { return false; }

        public bool OnNextPage() { return false; }

        public bool OnPreview() { return false; }

        public void OnWhatsNew() { }

        public void OnUndo() { }

        public void OnRedo() { }

        public bool OnTabClicked(int Id) { return false; }

        public void OnGroupExpand(int Id, bool Expanded) { }

        public void OnGroupCheck(int Id, bool Checked) { }

        public void OnSelectionboxFocusChanged(int Id) { }

        public void OnSelectionboxCalloutCreated(int Id) { }

        public void OnSelectionboxCalloutDestroyed(int Id) { }

        public bool OnSubmitSelection(int Id, object Selection, int SelType, ref string ItemText)
        {
            return false;
        }

        public int OnActiveXControlCreated(int Id, bool Status)
        {
            // ActiveX 控件创建完成后（Status=true）才能取得其内部对象，
            // 这里以 Microsoft Web Browser 为例，导航到 about:blank 并写入一段简单内容。
            if (Status && ActiveXControl != null && Id == ActiveXId)
            {
                try
                {
                    dynamic browser = ActiveXControl.GetControl();
                    browser.Navigate("about:blank");
                    dynamic doc = browser.Document;
                    if (doc != null)
                    {
                        doc.Write("<html><body style='font-family:Segoe UI;font-size:12px;margin:6px'>"
                                + "<b>这是嵌入的Microsoft Web Browser ActiveX 控件。<br>"
                                + "</body></html>");
                        doc.Close();
                    }
                }
                catch
                {
                    // 导航失败不影响其它控件
                }
            }

            return 0;
        }

        public bool OnKeystroke(int Wparam, int Message, int Lparam, int Id)
        {
            return false;
        }

        public void OnPopupMenuItem(int Id) { }

        public void OnPopupMenuItemUpdate(int Id, ref int retval)
        {
            retval = 0;
        }

        public void OnGainedFocus(int Id) { }

        public void OnLostFocus(int Id) { }

        public int OnWindowFromHandleControlCreated(int Id, bool Status)
        {
            return 0;
        }

        public void OnListboxRMBUp(int Id, int PosX, int PosY) { }

        #endregion
    }
}
