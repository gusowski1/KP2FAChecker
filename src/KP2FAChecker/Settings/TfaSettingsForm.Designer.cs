namespace KP2FAChecker.Settings
{
    partial class TfaSettingsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this._grpScope            = new System.Windows.Forms.GroupBox();
            this._lblScope            = new System.Windows.Forms.Label();
            this._cboScope            = new System.Windows.Forms.ComboBox();
            this._grpRefresh          = new System.Windows.Forms.GroupBox();
            this._lblInterval         = new System.Windows.Forms.Label();
            this._nudInterval         = new System.Windows.Forms.NumericUpDown();
            this._lblHours            = new System.Windows.Forms.Label();
            this._chkPgp              = new System.Windows.Forms.CheckBox();
            this._grpCache            = new System.Windows.Forms.GroupBox();
            this._lblCacheStatus      = new System.Windows.Forms.Label();
            this._btnRefreshNow       = new System.Windows.Forms.Button();
            this._lblAttribution      = new System.Windows.Forms.Label();
            this._btnOk               = new System.Windows.Forms.Button();
            this._btnCancel           = new System.Windows.Forms.Button();
            this._grpScope.SuspendLayout();
            this._grpRefresh.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._nudInterval)).BeginInit();
            this._grpCache.SuspendLayout();
            this.SuspendLayout();

            // _grpScope
            this._grpScope.Controls.Add(this._cboScope);
            this._grpScope.Controls.Add(this._lblScope);
            this._grpScope.Location  = new System.Drawing.Point(12, 12);
            this._grpScope.Name      = "_grpScope";
            this._grpScope.Size      = new System.Drawing.Size(410, 70);
            this._grpScope.TabIndex  = 0;
            this._grpScope.TabStop   = false;
            this._grpScope.Text      = "Data Scope";

            // _lblScope
            this._lblScope.AutoSize = true;
            this._lblScope.Location = new System.Drawing.Point(12, 30);
            this._lblScope.Name     = "_lblScope";
            this._lblScope.Text     = "Show 2FA methods for:";

            // _cboScope
            this._cboScope.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._cboScope.Location      = new System.Drawing.Point(150, 26);
            this._cboScope.Name          = "_cboScope";
            this._cboScope.Size          = new System.Drawing.Size(244, 21);
            this._cboScope.TabIndex      = 0;

            // _grpRefresh
            this._grpRefresh.Controls.Add(this._lblInterval);
            this._grpRefresh.Controls.Add(this._nudInterval);
            this._grpRefresh.Controls.Add(this._lblHours);
            this._grpRefresh.Controls.Add(this._chkPgp);
            this._grpRefresh.Location = new System.Drawing.Point(12, 92);
            this._grpRefresh.Name     = "_grpRefresh";
            this._grpRefresh.Size     = new System.Drawing.Size(410, 80);
            this._grpRefresh.TabIndex = 1;
            this._grpRefresh.TabStop  = false;
            this._grpRefresh.Text     = "Refresh";

            // _lblInterval
            this._lblInterval.AutoSize = true;
            this._lblInterval.Location = new System.Drawing.Point(12, 26);
            this._lblInterval.Name     = "_lblInterval";
            this._lblInterval.Text     = "Check for updates every:";

            // _nudInterval
            this._nudInterval.Location = new System.Drawing.Point(180, 22);
            this._nudInterval.Maximum  = new decimal(new int[] { 720, 0, 0, 0 });
            this._nudInterval.Minimum  = new decimal(new int[] { 1,   0, 0, 0 });
            this._nudInterval.Name     = "_nudInterval";
            this._nudInterval.Size     = new System.Drawing.Size(60, 20);
            this._nudInterval.TabIndex = 0;
            this._nudInterval.Value    = new decimal(new int[] { 24, 0, 0, 0 });

            // _lblHours
            this._lblHours.AutoSize = true;
            this._lblHours.Location = new System.Drawing.Point(246, 26);
            this._lblHours.Name     = "_lblHours";
            this._lblHours.Text     = "hours";

            // _chkPgp
            this._chkPgp.AutoSize = true;
            this._chkPgp.Location = new System.Drawing.Point(12, 52);
            this._chkPgp.Name     = "_chkPgp";
            this._chkPgp.TabIndex = 1;
            this._chkPgp.Text     = "Verify &PGP signature of downloaded data";

            // _grpCache
            this._grpCache.Controls.Add(this._lblCacheStatus);
            this._grpCache.Controls.Add(this._btnRefreshNow);
            this._grpCache.Location = new System.Drawing.Point(12, 182);
            this._grpCache.Name     = "_grpCache";
            this._grpCache.Size     = new System.Drawing.Size(410, 80);
            this._grpCache.TabIndex = 2;
            this._grpCache.TabStop  = false;
            this._grpCache.Text     = "Cache Status";

            // _lblCacheStatus
            this._lblCacheStatus.AutoSize  = false;
            this._lblCacheStatus.Location  = new System.Drawing.Point(12, 24);
            this._lblCacheStatus.Name      = "_lblCacheStatus";
            this._lblCacheStatus.Size      = new System.Drawing.Size(300, 40);
            this._lblCacheStatus.Text      = "Loading...";

            // _btnRefreshNow
            this._btnRefreshNow.Location = new System.Drawing.Point(316, 22);
            this._btnRefreshNow.Name     = "_btnRefreshNow";
            this._btnRefreshNow.Size     = new System.Drawing.Size(88, 26);
            this._btnRefreshNow.TabIndex = 0;
            this._btnRefreshNow.Text     = "&Refresh Now";
            this._btnRefreshNow.Click   += new System.EventHandler(this.OnRefreshNowClick);

            // _lblAttribution
            this._lblAttribution.AutoSize  = true;
            this._lblAttribution.ForeColor = System.Drawing.SystemColors.GrayText;
            this._lblAttribution.Location  = new System.Drawing.Point(12, 272);
            this._lblAttribution.Name      = "_lblAttribution";
            this._lblAttribution.Text      = "Data sourced from 2FA Directory by 2factorauth. (MIT)";

            // _btnOk
            this._btnOk.Anchor       = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            this._btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this._btnOk.Location     = new System.Drawing.Point(256, 296);
            this._btnOk.Name         = "_btnOk";
            this._btnOk.Size         = new System.Drawing.Size(80, 26);
            this._btnOk.TabIndex     = 10;
            this._btnOk.Text         = "OK";
            this._btnOk.Click       += new System.EventHandler(this.OnOkClick);

            // _btnCancel
            this._btnCancel.Anchor       = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            this._btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._btnCancel.Location     = new System.Drawing.Point(342, 296);
            this._btnCancel.Name         = "_btnCancel";
            this._btnCancel.Size         = new System.Drawing.Size(80, 26);
            this._btnCancel.TabIndex     = 11;
            this._btnCancel.Text         = "Cancel";

            // TfaSettingsForm
            this.AcceptButton    = this._btnOk;
            this.CancelButton    = this._btnCancel;
            this.ClientSize      = new System.Drawing.Size(434, 334);
            this.Controls.Add(this._grpScope);
            this.Controls.Add(this._grpRefresh);
            this.Controls.Add(this._grpCache);
            this.Controls.Add(this._lblAttribution);
            this.Controls.Add(this._btnOk);
            this.Controls.Add(this._btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.Name            = "TfaSettingsForm";
            this.ShowInTaskbar   = false;
            this.StartPosition   = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text            = "2FA Checker — Settings";

            this._grpScope.ResumeLayout(false);
            this._grpScope.PerformLayout();
            this._grpRefresh.ResumeLayout(false);
            this._grpRefresh.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._nudInterval)).EndInit();
            this._grpCache.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.GroupBox    _grpScope;
        private System.Windows.Forms.Label       _lblScope;
        private System.Windows.Forms.ComboBox    _cboScope;
        private System.Windows.Forms.GroupBox    _grpRefresh;
        private System.Windows.Forms.Label       _lblInterval;
        private System.Windows.Forms.NumericUpDown _nudInterval;
        private System.Windows.Forms.Label       _lblHours;
        private System.Windows.Forms.CheckBox    _chkPgp;
        private System.Windows.Forms.GroupBox    _grpCache;
        private System.Windows.Forms.Label       _lblCacheStatus;
        private System.Windows.Forms.Button      _btnRefreshNow;
        private System.Windows.Forms.Label       _lblAttribution;
        private System.Windows.Forms.Button      _btnOk;
        private System.Windows.Forms.Button      _btnCancel;
    }
}
