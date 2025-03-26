namespace MultiThreadedDownloaderLib.RequestsTest
{
	partial class FormCookieEditor
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
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
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.listViewCookies = new System.Windows.Forms.ListView();
			this.columnHeaderDomain = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeaderPath = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeaderName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeaderValue = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.btnLoadFromFile = new System.Windows.Forms.Button();
			this.btnClear = new System.Windows.Forms.Button();
			this.btnOk = new System.Windows.Forms.Button();
			this.statusStrip1 = new System.Windows.Forms.StatusStrip();
			this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
			this.statusStrip1.SuspendLayout();
			this.SuspendLayout();
			// 
			// listViewCookies
			// 
			this.listViewCookies.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
			| System.Windows.Forms.AnchorStyles.Left) 
			| System.Windows.Forms.AnchorStyles.Right)));
			this.listViewCookies.BackColor = System.Drawing.SystemColors.ButtonFace;
			this.listViewCookies.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
			this.columnHeaderDomain,
			this.columnHeaderPath,
			this.columnHeaderName,
			this.columnHeaderValue});
			this.listViewCookies.FullRowSelect = true;
			this.listViewCookies.HideSelection = false;
			this.listViewCookies.Location = new System.Drawing.Point(12, 12);
			this.listViewCookies.Name = "listViewCookies";
			this.listViewCookies.Size = new System.Drawing.Size(776, 195);
			this.listViewCookies.TabIndex = 0;
			this.listViewCookies.UseCompatibleStateImageBehavior = false;
			this.listViewCookies.View = System.Windows.Forms.View.Details;
			// 
			// columnHeaderDomain
			// 
			this.columnHeaderDomain.Text = "Domain";
			this.columnHeaderDomain.Width = 120;
			// 
			// columnHeaderPath
			// 
			this.columnHeaderPath.Text = "Path";
			// 
			// columnHeaderName
			// 
			this.columnHeaderName.Text = "Name";
			this.columnHeaderName.Width = 120;
			// 
			// columnHeaderValue
			// 
			this.columnHeaderValue.Text = "Value";
			this.columnHeaderValue.Width = 427;
			// 
			// btnLoadFromFile
			// 
			this.btnLoadFromFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnLoadFromFile.Location = new System.Drawing.Point(12, 213);
			this.btnLoadFromFile.Name = "btnLoadFromFile";
			this.btnLoadFromFile.Size = new System.Drawing.Size(96, 23);
			this.btnLoadFromFile.TabIndex = 1;
			this.btnLoadFromFile.Text = "Load from file";
			this.btnLoadFromFile.UseVisualStyleBackColor = true;
			this.btnLoadFromFile.Click += new System.EventHandler(this.btnLoadFromFile_Click);
			// 
			// btnClear
			// 
			this.btnClear.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnClear.Location = new System.Drawing.Point(114, 213);
			this.btnClear.Name = "btnClear";
			this.btnClear.Size = new System.Drawing.Size(75, 23);
			this.btnClear.TabIndex = 2;
			this.btnClear.Text = "Clear";
			this.btnClear.UseVisualStyleBackColor = true;
			this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
			// 
			// btnOk
			// 
			this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
			this.btnOk.Location = new System.Drawing.Point(713, 213);
			this.btnOk.Name = "btnOk";
			this.btnOk.Size = new System.Drawing.Size(75, 23);
			this.btnOk.TabIndex = 3;
			this.btnOk.Text = "OK";
			this.btnOk.UseVisualStyleBackColor = true;
			// 
			// statusStrip1
			// 
			this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
			this.toolStripStatusLabel1});
			this.statusStrip1.Location = new System.Drawing.Point(0, 239);
			this.statusStrip1.Name = "statusStrip1";
			this.statusStrip1.Size = new System.Drawing.Size(800, 22);
			this.statusStrip1.TabIndex = 4;
			this.statusStrip1.Text = "statusStrip1";
			// 
			// toolStripStatusLabel1
			// 
			this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
			this.toolStripStatusLabel1.Size = new System.Drawing.Size(105, 17);
			this.toolStripStatusLabel1.Text = "No cookies loaded";
			// 
			// FormCookieEditor
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(800, 261);
			this.Controls.Add(this.statusStrip1);
			this.Controls.Add(this.btnOk);
			this.Controls.Add(this.btnClear);
			this.Controls.Add(this.btnLoadFromFile);
			this.Controls.Add(this.listViewCookies);
			this.Name = "FormCookieEditor";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Cookies";
			this.statusStrip1.ResumeLayout(false);
			this.statusStrip1.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.ListView listViewCookies;
		private System.Windows.Forms.ColumnHeader columnHeaderDomain;
		private System.Windows.Forms.ColumnHeader columnHeaderPath;
		private System.Windows.Forms.ColumnHeader columnHeaderName;
		private System.Windows.Forms.ColumnHeader columnHeaderValue;
		private System.Windows.Forms.Button btnLoadFromFile;
		private System.Windows.Forms.Button btnClear;
		private System.Windows.Forms.Button btnOk;
		private System.Windows.Forms.StatusStrip statusStrip1;
		private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
	}
}