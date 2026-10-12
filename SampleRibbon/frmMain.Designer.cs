using System.Drawing;
using System.Windows.Forms;

namespace SampleRibbon
{
    partial class frmMain
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblInfo;
        private System.Windows.Forms.TextBox txtInput;
        private System.Windows.Forms.Button btnShow;
        private System.Windows.Forms.Button btnClose;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblInfo = new System.Windows.Forms.Label();
            this.txtInput = new System.Windows.Forms.TextBox();
            this.btnShow = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 14F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(16, 15);
            this.lblTitle.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(220, 26);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "这是一个测试 C# 对话框";
            // 
            // lblInfo
            // 
            this.lblInfo.AutoSize = true;
            this.lblInfo.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.lblInfo.Location = new System.Drawing.Point(18, 41);
            this.lblInfo.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblInfo.Name = "lblInfo";
            this.lblInfo.Size = new System.Drawing.Size(383, 17);
            this.lblInfo.TabIndex = 1;
            this.lblInfo.Text = "该窗体由 C# WinForms 编写（frmMain），用于测试按钮弹出效果。";
            // 
            // txtInput
            // 
            this.txtInput.Location = new System.Drawing.Point(20, 64);
            this.txtInput.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.txtInput.Name = "txtInput";
            this.txtInput.Size = new System.Drawing.Size(241, 21);
            this.txtInput.TabIndex = 2;
            this.txtInput.Text = "在此输入要测试的内容";
            // 
            // btnShow
            // 
            this.btnShow.Location = new System.Drawing.Point(20, 93);
            this.btnShow.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnShow.Name = "btnShow";
            this.btnShow.Size = new System.Drawing.Size(87, 25);
            this.btnShow.TabIndex = 3;
            this.btnShow.Text = "显示输入内容";
            this.btnShow.UseVisualStyleBackColor = true;
            this.btnShow.Click += new System.EventHandler(this.btnShow_Click);
            // 
            // btnClose
            // 
            this.btnClose.Location = new System.Drawing.Point(120, 93);
            this.btnClose.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(87, 25);
            this.btnClose.TabIndex = 4;
            this.btnClose.Text = "关闭";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(452, 235);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnShow);
            this.Controls.Add(this.txtInput);
            this.Controls.Add(this.lblInfo);
            this.Controls.Add(this.lblTitle);
            this.Margin = new System.Windows.Forms.Padding(1, 1, 1, 1);
            this.Name = "frmMain";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "测试 C# 对话框";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
    }
}
