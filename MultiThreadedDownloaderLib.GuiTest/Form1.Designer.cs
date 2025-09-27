namespace MultiThreadedDownloaderLib.GuiTest
{
	partial class Form1
	{
		/// <summary>
		/// Обязательная переменная конструктора.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Освободить все используемые ресурсы.
		/// </summary>
		/// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Код, автоматически созданный конструктором форм Windows

		/// <summary>
		/// Требуемый метод для поддержки конструктора — не изменяйте 
		/// содержимое этого метода с помощью редактора кода.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
			this.numericUpDownProxyPort = new System.Windows.Forms.NumericUpDown();
			this.textBoxProxyAddress = new System.Windows.Forms.TextBox();
			this.checkBoxFakeDownloading = new System.Windows.Forms.CheckBox();
			this.checkBoxDownloadToRAM = new System.Windows.Forms.CheckBox();
			this.checkBoxMergeChunksAutomatically = new System.Windows.Forms.CheckBox();
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPageMain = new System.Windows.Forms.TabPage();
			this.checkBoxDeleteOnlyIncompleteChunks = new System.Windows.Forms.CheckBox();
			this.checkBoxDeleteTempFiles = new System.Windows.Forms.CheckBox();
			this.label4 = new System.Windows.Forms.Label();
			this.textBoxHeaderRequestMethod = new System.Windows.Forms.TextBox();
			this.checkBoxIgnoreHeaderRequestErrors = new System.Windows.Forms.CheckBox();
			this.checkBoxSkipHeadRequest = new System.Windows.Forms.CheckBox();
			this.label13 = new System.Windows.Forms.Label();
			this.numericUpDownRetryInterval = new System.Windows.Forms.NumericUpDown();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.lblCookieCount = new System.Windows.Forms.Label();
			this.btnCookies = new System.Windows.Forms.Button();
			this.groupBoxProxy = new System.Windows.Forms.GroupBox();
			this.label12 = new System.Windows.Forms.Label();
			this.label11 = new System.Windows.Forms.Label();
			this.numericUpDownConnectionTimeout = new System.Windows.Forms.NumericUpDown();
			this.label9 = new System.Windows.Forms.Label();
			this.checkBoxUseAccurateMode = new System.Windows.Forms.CheckBox();
			this.label10 = new System.Windows.Forms.Label();
			this.numericUpDownTryCountInsideEachThread = new System.Windows.Forms.NumericUpDown();
			this.numericUpDownTryCountPerThread = new System.Windows.Forms.NumericUpDown();
			this.label8 = new System.Windows.Forms.Label();
			this.progressBarDownload = new MultiThreadedDownloaderLib.MultipleProgressBar();
			this.numericUpDownChunksMergingUpdateInterval = new System.Windows.Forms.NumericUpDown();
			this.label7 = new System.Windows.Forms.Label();
			this.numericUpDownUpdateInterval = new System.Windows.Forms.NumericUpDown();
			this.label6 = new System.Windows.Forms.Label();
			this.checkBoxUseRamForTempFiles = new System.Windows.Forms.CheckBox();
			this.btnHeaders = new System.Windows.Forms.Button();
			this.lblMergeProgress = new System.Windows.Forms.Label();
			this.label5 = new System.Windows.Forms.Label();
			this.numericUpDownThreadCount = new System.Windows.Forms.NumericUpDown();
			this.btnSelectTempDir = new System.Windows.Forms.Button();
			this.btnSelectFile = new System.Windows.Forms.Button();
			this.txtBoxTempDir = new System.Windows.Forms.TextBox();
			this.textBoxOutputFileName = new System.Windows.Forms.TextBox();
			this.label3 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.lblDownloadProgress = new System.Windows.Forms.Label();
			this.btnDownloadMultiThreaded = new System.Windows.Forms.Button();
			this.textBoxUrl = new System.Windows.Forms.TextBox();
			this.btnDownloadSingleThreaded = new System.Windows.Forms.Button();
			this.tabPageLogger = new System.Windows.Forms.TabPage();
			this.checkBoxAutoscrollLog = new System.Windows.Forms.CheckBox();
			this.listViewLog = new System.Windows.Forms.ListView();
			this.columnHeaderEventDate = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeaderEventText = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownProxyPort)).BeginInit();
			this.tabControl1.SuspendLayout();
			this.tabPageMain.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownRetryInterval)).BeginInit();
			this.groupBox1.SuspendLayout();
			this.groupBoxProxy.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownConnectionTimeout)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownTryCountInsideEachThread)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownTryCountPerThread)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownChunksMergingUpdateInterval)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownUpdateInterval)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownThreadCount)).BeginInit();
			this.tabPageLogger.SuspendLayout();
			this.SuspendLayout();
			// 
			// numericUpDownProxyPort
			// 
			this.numericUpDownProxyPort.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
			| System.Windows.Forms.AnchorStyles.Right)));
			this.numericUpDownProxyPort.Location = new System.Drawing.Point(65, 45);
			this.numericUpDownProxyPort.Maximum = new decimal(new int[] {
			65000,
			0,
			0,
			0});
			this.numericUpDownProxyPort.Name = "numericUpDownProxyPort";
			this.numericUpDownProxyPort.Size = new System.Drawing.Size(356, 20);
			this.numericUpDownProxyPort.TabIndex = 2;
			this.toolTip1.SetToolTip(this.numericUpDownProxyPort, "Введите 0, чтобы не использовать прокси-сервер");
			// 
			// textBoxProxyAddress
			// 
			this.textBoxProxyAddress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
			| System.Windows.Forms.AnchorStyles.Right)));
			this.textBoxProxyAddress.Location = new System.Drawing.Point(65, 19);
			this.textBoxProxyAddress.Name = "textBoxProxyAddress";
			this.textBoxProxyAddress.Size = new System.Drawing.Size(356, 20);
			this.textBoxProxyAddress.TabIndex = 1;
			this.toolTip1.SetToolTip(this.textBoxProxyAddress, "Оставьте это поле пустым, чтобы не использовать прокси-сервер");
			// 
			// checkBoxFakeDownloading
			// 
			this.checkBoxFakeDownloading.AutoSize = true;
			this.checkBoxFakeDownloading.Location = new System.Drawing.Point(245, 139);
			this.checkBoxFakeDownloading.Name = "checkBoxFakeDownloading";
			this.checkBoxFakeDownloading.Size = new System.Drawing.Size(141, 17);
			this.checkBoxFakeDownloading.TabIndex = 78;
			this.checkBoxFakeDownloading.Text = "Фейковое скачивание";
			this.toolTip1.SetToolTip(this.checkBoxFakeDownloading, "В этом режиме скачанные данные никуда не сохраняются");
			this.checkBoxFakeDownloading.UseVisualStyleBackColor = true;
			// 
			// checkBoxDownloadToRAM
			// 
			this.checkBoxDownloadToRAM.AutoSize = true;
			this.checkBoxDownloadToRAM.Location = new System.Drawing.Point(9, 139);
			this.checkBoxDownloadToRAM.Name = "checkBoxDownloadToRAM";
			this.checkBoxDownloadToRAM.Size = new System.Drawing.Size(230, 17);
			this.checkBoxDownloadToRAM.TabIndex = 77;
			this.checkBoxDownloadToRAM.Text = "Скачивать в оперативную память (RAM)";
			this.toolTip1.SetToolTip(this.checkBoxDownloadToRAM, "Внимание! Если скачиваемый файл большой, то для правильной работы требуется очень" +
		" много свободной оперативной памяти!");
			this.checkBoxDownloadToRAM.UseVisualStyleBackColor = true;
			// 
			// checkBoxMergeChunksAutomatically
			// 
			this.checkBoxMergeChunksAutomatically.AutoSize = true;
			this.checkBoxMergeChunksAutomatically.Checked = true;
			this.checkBoxMergeChunksAutomatically.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBoxMergeChunksAutomatically.Location = new System.Drawing.Point(9, 93);
			this.checkBoxMergeChunksAutomatically.Name = "checkBoxMergeChunksAutomatically";
			this.checkBoxMergeChunksAutomatically.Size = new System.Drawing.Size(457, 17);
			this.checkBoxMergeChunksAutomatically.TabIndex = 76;
			this.checkBoxMergeChunksAutomatically.Text = "Автоматически объединить чанки после скачивания (только многопоточный режим)";
			this.toolTip1.SetToolTip(this.checkBoxMergeChunksAutomatically, "Будет использован встроенный алгоритм объединения");
			this.checkBoxMergeChunksAutomatically.UseVisualStyleBackColor = true;
			// 
			// tabControl1
			// 
			this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
			| System.Windows.Forms.AnchorStyles.Left) 
			| System.Windows.Forms.AnchorStyles.Right)));
			this.tabControl1.Controls.Add(this.tabPageMain);
			this.tabControl1.Controls.Add(this.tabPageLogger);
			this.tabControl1.Location = new System.Drawing.Point(12, 12);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(833, 578);
			this.tabControl1.TabIndex = 0;
			// 
			// tabPageMain
			// 
			this.tabPageMain.BackColor = System.Drawing.SystemColors.ButtonFace;
			this.tabPageMain.Controls.Add(this.checkBoxDeleteOnlyIncompleteChunks);
			this.tabPageMain.Controls.Add(this.checkBoxDeleteTempFiles);
			this.tabPageMain.Controls.Add(this.label4);
			this.tabPageMain.Controls.Add(this.textBoxHeaderRequestMethod);
			this.tabPageMain.Controls.Add(this.checkBoxIgnoreHeaderRequestErrors);
			this.tabPageMain.Controls.Add(this.checkBoxSkipHeadRequest);
			this.tabPageMain.Controls.Add(this.label13);
			this.tabPageMain.Controls.Add(this.numericUpDownRetryInterval);
			this.tabPageMain.Controls.Add(this.groupBox1);
			this.tabPageMain.Controls.Add(this.groupBoxProxy);
			this.tabPageMain.Controls.Add(this.checkBoxFakeDownloading);
			this.tabPageMain.Controls.Add(this.checkBoxDownloadToRAM);
			this.tabPageMain.Controls.Add(this.checkBoxMergeChunksAutomatically);
			this.tabPageMain.Controls.Add(this.numericUpDownConnectionTimeout);
			this.tabPageMain.Controls.Add(this.label9);
			this.tabPageMain.Controls.Add(this.checkBoxUseAccurateMode);
			this.tabPageMain.Controls.Add(this.label10);
			this.tabPageMain.Controls.Add(this.numericUpDownTryCountInsideEachThread);
			this.tabPageMain.Controls.Add(this.numericUpDownTryCountPerThread);
			this.tabPageMain.Controls.Add(this.label8);
			this.tabPageMain.Controls.Add(this.progressBarDownload);
			this.tabPageMain.Controls.Add(this.numericUpDownChunksMergingUpdateInterval);
			this.tabPageMain.Controls.Add(this.label7);
			this.tabPageMain.Controls.Add(this.numericUpDownUpdateInterval);
			this.tabPageMain.Controls.Add(this.label6);
			this.tabPageMain.Controls.Add(this.checkBoxUseRamForTempFiles);
			this.tabPageMain.Controls.Add(this.btnHeaders);
			this.tabPageMain.Controls.Add(this.lblMergeProgress);
			this.tabPageMain.Controls.Add(this.label5);
			this.tabPageMain.Controls.Add(this.numericUpDownThreadCount);
			this.tabPageMain.Controls.Add(this.btnSelectTempDir);
			this.tabPageMain.Controls.Add(this.btnSelectFile);
			this.tabPageMain.Controls.Add(this.txtBoxTempDir);
			this.tabPageMain.Controls.Add(this.textBoxOutputFileName);
			this.tabPageMain.Controls.Add(this.label3);
			this.tabPageMain.Controls.Add(this.label2);
			this.tabPageMain.Controls.Add(this.label1);
			this.tabPageMain.Controls.Add(this.lblDownloadProgress);
			this.tabPageMain.Controls.Add(this.btnDownloadMultiThreaded);
			this.tabPageMain.Controls.Add(this.textBoxUrl);
			this.tabPageMain.Controls.Add(this.btnDownloadSingleThreaded);
			this.tabPageMain.Location = new System.Drawing.Point(4, 22);
			this.tabPageMain.Name = "tabPageMain";
			this.tabPageMain.Padding = new System.Windows.Forms.Padding(3);
			this.tabPageMain.Size = new System.Drawing.Size(825, 552);
			this.tabPageMain.TabIndex = 0;
			this.tabPageMain.Text = "Скачивание";
			// 
			// checkBoxDeleteOnlyIncompleteChunks
			// 
			this.checkBoxDeleteOnlyIncompleteChunks.AutoSize = true;
			this.checkBoxDeleteOnlyIncompleteChunks.Location = new System.Drawing.Point(24, 254);
			this.checkBoxDeleteOnlyIncompleteChunks.Name = "checkBoxDeleteOnlyIncompleteChunks";
			this.checkBoxDeleteOnlyIncompleteChunks.Size = new System.Drawing.Size(215, 17);
			this.checkBoxDeleteOnlyIncompleteChunks.TabIndex = 88;
			this.checkBoxDeleteOnlyIncompleteChunks.Text = "Удалять только недокачанные чанки";
			this.checkBoxDeleteOnlyIncompleteChunks.UseVisualStyleBackColor = true;
			// 
			// checkBoxDeleteTempFiles
			// 
			this.checkBoxDeleteTempFiles.AutoSize = true;
			this.checkBoxDeleteTempFiles.Checked = true;
			this.checkBoxDeleteTempFiles.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBoxDeleteTempFiles.Location = new System.Drawing.Point(9, 231);
			this.checkBoxDeleteTempFiles.Name = "checkBoxDeleteTempFiles";
			this.checkBoxDeleteTempFiles.Size = new System.Drawing.Size(520, 17);
			this.checkBoxDeleteTempFiles.TabIndex = 87;
			this.checkBoxDeleteTempFiles.Text = "Удалять временные файлы при ошибках или отмене скачивания (только многопоточный р" +
	"ежим)";
			this.checkBoxDeleteTempFiles.UseVisualStyleBackColor = true;
			// 
			// label4
			// 
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(9, 280);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(191, 13);
			this.label4.TabIndex = 86;
			this.label4.Text = "Метод получения HTTP-заголовков:";
			// 
			// textBoxHeaderRequestMethod
			// 
			this.textBoxHeaderRequestMethod.Location = new System.Drawing.Point(332, 277);
			this.textBoxHeaderRequestMethod.Name = "textBoxHeaderRequestMethod";
			this.textBoxHeaderRequestMethod.Size = new System.Drawing.Size(67, 20);
			this.textBoxHeaderRequestMethod.TabIndex = 85;
			this.textBoxHeaderRequestMethod.Text = "HEAD";
			// 
			// checkBoxIgnoreHeaderRequestErrors
			// 
			this.checkBoxIgnoreHeaderRequestErrors.AutoSize = true;
			this.checkBoxIgnoreHeaderRequestErrors.Checked = true;
			this.checkBoxIgnoreHeaderRequestErrors.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBoxIgnoreHeaderRequestErrors.Location = new System.Drawing.Point(9, 185);
			this.checkBoxIgnoreHeaderRequestErrors.Name = "checkBoxIgnoreHeaderRequestErrors";
			this.checkBoxIgnoreHeaderRequestErrors.Size = new System.Drawing.Size(534, 17);
			this.checkBoxIgnoreHeaderRequestErrors.TabIndex = 84;
			this.checkBoxIgnoreHeaderRequestErrors.Text = "Начать скачивание, даже если не удалось получить HTTP-заголовки (только однопоточ" +
	"ный режим)";
			this.checkBoxIgnoreHeaderRequestErrors.UseVisualStyleBackColor = true;
			// 
			// checkBoxSkipHeadRequest
			// 
			this.checkBoxSkipHeadRequest.AutoSize = true;
			this.checkBoxSkipHeadRequest.Location = new System.Drawing.Point(9, 208);
			this.checkBoxSkipHeadRequest.Name = "checkBoxSkipHeadRequest";
			this.checkBoxSkipHeadRequest.Size = new System.Drawing.Size(473, 17);
			this.checkBoxSkipHeadRequest.TabIndex = 83;
			this.checkBoxSkipHeadRequest.Text = "Не получать HTTP-заголовки перед началом скачивания (только однопоточный режим)";
			this.checkBoxSkipHeadRequest.UseVisualStyleBackColor = true;
			// 
			// label13
			// 
			this.label13.AutoSize = true;
			this.label13.Location = new System.Drawing.Point(9, 459);
			this.label13.Name = "label13";
			this.label13.Size = new System.Drawing.Size(177, 13);
			this.label13.TabIndex = 82;
			this.label13.Text = "Интервал между попытками (ms):";
			// 
			// numericUpDownRetryInterval
			// 
			this.numericUpDownRetryInterval.Increment = new decimal(new int[] {
			100,
			0,
			0,
			0});
			this.numericUpDownRetryInterval.Location = new System.Drawing.Point(332, 457);
			this.numericUpDownRetryInterval.Maximum = new decimal(new int[] {
			10000,
			0,
			0,
			0});
			this.numericUpDownRetryInterval.Minimum = new decimal(new int[] {
			500,
			0,
			0,
			0});
			this.numericUpDownRetryInterval.Name = "numericUpDownRetryInterval";
			this.numericUpDownRetryInterval.Size = new System.Drawing.Size(54, 20);
			this.numericUpDownRetryInterval.TabIndex = 81;
			this.numericUpDownRetryInterval.Value = new decimal(new int[] {
			3000,
			0,
			0,
			0});
			// 
			// groupBox1
			// 
			this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
			| System.Windows.Forms.AnchorStyles.Right)));
			this.groupBox1.Controls.Add(this.lblCookieCount);
			this.groupBox1.Controls.Add(this.btnCookies);
			this.groupBox1.Location = new System.Drawing.Point(393, 381);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(427, 44);
			this.groupBox1.TabIndex = 80;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Cookies";
			// 
			// lblCookieCount
			// 
			this.lblCookieCount.AutoSize = true;
			this.lblCookieCount.Location = new System.Drawing.Point(114, 21);
			this.lblCookieCount.Name = "lblCookieCount";
			this.lblCookieCount.Size = new System.Drawing.Size(96, 13);
			this.lblCookieCount.TabIndex = 1;
			this.lblCookieCount.Text = "No cookies loaded";
			// 
			// btnCookies
			// 
			this.btnCookies.Location = new System.Drawing.Point(6, 16);
			this.btnCookies.Name = "btnCookies";
			this.btnCookies.Size = new System.Drawing.Size(102, 23);
			this.btnCookies.TabIndex = 0;
			this.btnCookies.Text = "Добавить куки";
			this.btnCookies.UseVisualStyleBackColor = true;
			this.btnCookies.Click += new System.EventHandler(this.btnCookies_Click);
			// 
			// groupBoxProxy
			// 
			this.groupBoxProxy.Controls.Add(this.label12);
			this.groupBoxProxy.Controls.Add(this.numericUpDownProxyPort);
			this.groupBoxProxy.Controls.Add(this.textBoxProxyAddress);
			this.groupBoxProxy.Controls.Add(this.label11);
			this.groupBoxProxy.Location = new System.Drawing.Point(393, 303);
			this.groupBoxProxy.Name = "groupBoxProxy";
			this.groupBoxProxy.Size = new System.Drawing.Size(427, 70);
			this.groupBoxProxy.TabIndex = 79;
			this.groupBoxProxy.TabStop = false;
			this.groupBoxProxy.Text = "Прокси-сервер";
			// 
			// label12
			// 
			this.label12.AutoSize = true;
			this.label12.Location = new System.Drawing.Point(18, 47);
			this.label12.Name = "label12";
			this.label12.Size = new System.Drawing.Size(35, 13);
			this.label12.TabIndex = 3;
			this.label12.Text = "Порт:";
			// 
			// label11
			// 
			this.label11.AutoSize = true;
			this.label11.Location = new System.Drawing.Point(18, 22);
			this.label11.Name = "label11";
			this.label11.Size = new System.Drawing.Size(41, 13);
			this.label11.TabIndex = 0;
			this.label11.Text = "Адрес:";
			// 
			// numericUpDownConnectionTimeout
			// 
			this.numericUpDownConnectionTimeout.Increment = new decimal(new int[] {
			100,
			0,
			0,
			0});
			this.numericUpDownConnectionTimeout.Location = new System.Drawing.Point(333, 431);
			this.numericUpDownConnectionTimeout.Maximum = new decimal(new int[] {
			100000,
			0,
			0,
			0});
			this.numericUpDownConnectionTimeout.Minimum = new decimal(new int[] {
			500,
			0,
			0,
			0});
			this.numericUpDownConnectionTimeout.Name = "numericUpDownConnectionTimeout";
			this.numericUpDownConnectionTimeout.Size = new System.Drawing.Size(67, 20);
			this.numericUpDownConnectionTimeout.TabIndex = 75;
			this.numericUpDownConnectionTimeout.Value = new decimal(new int[] {
			5000,
			0,
			0,
			0});
			// 
			// label9
			// 
			this.label9.AutoSize = true;
			this.label9.Location = new System.Drawing.Point(10, 433);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(181, 13);
			this.label9.TabIndex = 74;
			this.label9.Text = "Время ожидания соединения (ms):";
			// 
			// checkBoxUseAccurateMode
			// 
			this.checkBoxUseAccurateMode.AutoSize = true;
			this.checkBoxUseAccurateMode.Location = new System.Drawing.Point(9, 162);
			this.checkBoxUseAccurateMode.Name = "checkBoxUseAccurateMode";
			this.checkBoxUseAccurateMode.Size = new System.Drawing.Size(374, 17);
			this.checkBoxUseAccurateMode.TabIndex = 73;
			this.checkBoxUseAccurateMode.Text = "Использовать аккуратный режим (только при двух и более потоках)";
			this.checkBoxUseAccurateMode.UseVisualStyleBackColor = true;
			// 
			// label10
			// 
			this.label10.AutoSize = true;
			this.label10.Location = new System.Drawing.Point(8, 355);
			this.label10.Name = "label10";
			this.label10.Size = new System.Drawing.Size(319, 13);
			this.label10.TabIndex = 72;
			this.label10.Text = "Количество попыток внутри каждого потока (0 - бесконечно):";
			// 
			// numericUpDownTryCountInsideEachThread
			// 
			this.numericUpDownTryCountInsideEachThread.Location = new System.Drawing.Point(333, 353);
			this.numericUpDownTryCountInsideEachThread.Name = "numericUpDownTryCountInsideEachThread";
			this.numericUpDownTryCountInsideEachThread.Size = new System.Drawing.Size(54, 20);
			this.numericUpDownTryCountInsideEachThread.TabIndex = 71;
			this.numericUpDownTryCountInsideEachThread.Value = new decimal(new int[] {
			2,
			0,
			0,
			0});
			// 
			// numericUpDownTryCountPerThread
			// 
			this.numericUpDownTryCountPerThread.Location = new System.Drawing.Point(333, 327);
			this.numericUpDownTryCountPerThread.Name = "numericUpDownTryCountPerThread";
			this.numericUpDownTryCountPerThread.Size = new System.Drawing.Size(54, 20);
			this.numericUpDownTryCountPerThread.TabIndex = 70;
			this.numericUpDownTryCountPerThread.Value = new decimal(new int[] {
			5,
			0,
			0,
			0});
			// 
			// label8
			// 
			this.label8.AutoSize = true;
			this.label8.Location = new System.Drawing.Point(8, 329);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(303, 13);
			this.label8.TabIndex = 69;
			this.label8.Text = "Количество попыток для каждого потока (0 - бесконечно):";
			// 
			// progressBarDownload
			// 
			this.progressBarDownload.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
			| System.Windows.Forms.AnchorStyles.Right)));
			this.progressBarDownload.Location = new System.Drawing.Point(12, 526);
			this.progressBarDownload.Name = "progressBarDownload";
			this.progressBarDownload.Size = new System.Drawing.Size(807, 23);
			this.progressBarDownload.TabIndex = 68;
			this.progressBarDownload.Text = "multipleProgressBar1";
			// 
			// numericUpDownChunksMergingUpdateInterval
			// 
			this.numericUpDownChunksMergingUpdateInterval.Increment = new decimal(new int[] {
			50,
			0,
			0,
			0});
			this.numericUpDownChunksMergingUpdateInterval.Location = new System.Drawing.Point(333, 405);
			this.numericUpDownChunksMergingUpdateInterval.Maximum = new decimal(new int[] {
			1000,
			0,
			0,
			0});
			this.numericUpDownChunksMergingUpdateInterval.Minimum = new decimal(new int[] {
			50,
			0,
			0,
			0});
			this.numericUpDownChunksMergingUpdateInterval.Name = "numericUpDownChunksMergingUpdateInterval";
			this.numericUpDownChunksMergingUpdateInterval.Size = new System.Drawing.Size(54, 20);
			this.numericUpDownChunksMergingUpdateInterval.TabIndex = 67;
			this.numericUpDownChunksMergingUpdateInterval.Value = new decimal(new int[] {
			100,
			0,
			0,
			0});
			// 
			// label7
			// 
			this.label7.AutoSize = true;
			this.label7.Location = new System.Drawing.Point(8, 407);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(266, 13);
			this.label7.TabIndex = 66;
			this.label7.Text = "Частота обновления при объединении чанков (ms):";
			// 
			// numericUpDownUpdateInterval
			// 
			this.numericUpDownUpdateInterval.Increment = new decimal(new int[] {
			10,
			0,
			0,
			0});
			this.numericUpDownUpdateInterval.Location = new System.Drawing.Point(333, 379);
			this.numericUpDownUpdateInterval.Maximum = new decimal(new int[] {
			2000,
			0,
			0,
			0});
			this.numericUpDownUpdateInterval.Minimum = new decimal(new int[] {
			50,
			0,
			0,
			0});
			this.numericUpDownUpdateInterval.Name = "numericUpDownUpdateInterval";
			this.numericUpDownUpdateInterval.Size = new System.Drawing.Size(54, 20);
			this.numericUpDownUpdateInterval.TabIndex = 65;
			this.numericUpDownUpdateInterval.Value = new decimal(new int[] {
			100,
			0,
			0,
			0});
			// 
			// label6
			// 
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(8, 381);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(220, 13);
			this.label6.TabIndex = 64;
			this.label6.Text = "Частота обновления при скачивании (ms):";
			// 
			// checkBoxUseRamForTempFiles
			// 
			this.checkBoxUseRamForTempFiles.AutoSize = true;
			this.checkBoxUseRamForTempFiles.Location = new System.Drawing.Point(9, 116);
			this.checkBoxUseRamForTempFiles.Name = "checkBoxUseRamForTempFiles";
			this.checkBoxUseRamForTempFiles.Size = new System.Drawing.Size(650, 17);
			this.checkBoxUseRamForTempFiles.TabIndex = 63;
			this.checkBoxUseRamForTempFiles.Text = "Использовать оперативную память для хранения временных файлов (только многопоточн" +
	"ый режим), Экспериментально!";
			this.checkBoxUseRamForTempFiles.UseVisualStyleBackColor = true;
			// 
			// btnHeaders
			// 
			this.btnHeaders.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.btnHeaders.Location = new System.Drawing.Point(744, 93);
			this.btnHeaders.Name = "btnHeaders";
			this.btnHeaders.Size = new System.Drawing.Size(75, 23);
			this.btnHeaders.TabIndex = 62;
			this.btnHeaders.Text = "Заголовки";
			this.btnHeaders.UseVisualStyleBackColor = true;
			this.btnHeaders.Click += new System.EventHandler(this.btnHeaders_Click);
			// 
			// lblMergeProgress
			// 
			this.lblMergeProgress.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.lblMergeProgress.AutoSize = true;
			this.lblMergeProgress.Location = new System.Drawing.Point(73, 510);
			this.lblMergeProgress.Name = "lblMergeProgress";
			this.lblMergeProgress.Size = new System.Drawing.Size(88, 13);
			this.lblMergeProgress.TabIndex = 60;
			this.lblMergeProgress.Text = "lblMergeProgress";
			// 
			// label5
			// 
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(8, 305);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(113, 13);
			this.label5.TabIndex = 59;
			this.label5.Text = "Количество потоков:";
			// 
			// numericUpDownThreadCount
			// 
			this.numericUpDownThreadCount.Location = new System.Drawing.Point(333, 303);
			this.numericUpDownThreadCount.Maximum = new decimal(new int[] {
			25,
			0,
			0,
			0});
			this.numericUpDownThreadCount.Minimum = new decimal(new int[] {
			1,
			0,
			0,
			0});
			this.numericUpDownThreadCount.Name = "numericUpDownThreadCount";
			this.numericUpDownThreadCount.Size = new System.Drawing.Size(54, 20);
			this.numericUpDownThreadCount.TabIndex = 58;
			this.numericUpDownThreadCount.Value = new decimal(new int[] {
			4,
			0,
			0,
			0});
			// 
			// btnSelectTempDir
			// 
			this.btnSelectTempDir.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.btnSelectTempDir.Location = new System.Drawing.Point(780, 64);
			this.btnSelectTempDir.Name = "btnSelectTempDir";
			this.btnSelectTempDir.Size = new System.Drawing.Size(39, 23);
			this.btnSelectTempDir.TabIndex = 56;
			this.btnSelectTempDir.Text = "...";
			this.btnSelectTempDir.UseVisualStyleBackColor = true;
			this.btnSelectTempDir.Click += new System.EventHandler(this.btnSelectTempDir_Click);
			// 
			// btnSelectFile
			// 
			this.btnSelectFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.btnSelectFile.Location = new System.Drawing.Point(780, 35);
			this.btnSelectFile.Name = "btnSelectFile";
			this.btnSelectFile.Size = new System.Drawing.Size(39, 22);
			this.btnSelectFile.TabIndex = 55;
			this.btnSelectFile.Text = "...";
			this.btnSelectFile.UseVisualStyleBackColor = true;
			this.btnSelectFile.Click += new System.EventHandler(this.btnSelectFile_Click);
			// 
			// txtBoxTempDir
			// 
			this.txtBoxTempDir.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
			| System.Windows.Forms.AnchorStyles.Right)));
			this.txtBoxTempDir.Location = new System.Drawing.Point(183, 66);
			this.txtBoxTempDir.Name = "txtBoxTempDir";
			this.txtBoxTempDir.Size = new System.Drawing.Size(591, 20);
			this.txtBoxTempDir.TabIndex = 53;
			// 
			// textBoxOutputFileName
			// 
			this.textBoxOutputFileName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
			| System.Windows.Forms.AnchorStyles.Right)));
			this.textBoxOutputFileName.Location = new System.Drawing.Point(61, 37);
			this.textBoxOutputFileName.Name = "textBoxOutputFileName";
			this.textBoxOutputFileName.Size = new System.Drawing.Size(713, 20);
			this.textBoxOutputFileName.TabIndex = 52;
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(6, 69);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(164, 13);
			this.label3.TabIndex = 50;
			this.label3.Text = "Папка для временных файлов:";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(6, 40);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(39, 13);
			this.label2.TabIndex = 49;
			this.label2.Text = "Файл:";
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(6, 9);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(49, 13);
			this.label1.TabIndex = 48;
			this.label1.Text = "Ссылка:";
			// 
			// lblDownloadProgress
			// 
			this.lblDownloadProgress.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.lblDownloadProgress.AutoSize = true;
			this.lblDownloadProgress.Location = new System.Drawing.Point(9, 510);
			this.lblDownloadProgress.Name = "lblDownloadProgress";
			this.lblDownloadProgress.Size = new System.Drawing.Size(58, 13);
			this.lblDownloadProgress.TabIndex = 47;
			this.lblDownloadProgress.Text = "lblProgress";
			// 
			// btnDownloadMultiThreaded
			// 
			this.btnDownloadMultiThreaded.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnDownloadMultiThreaded.Location = new System.Drawing.Point(156, 484);
			this.btnDownloadMultiThreaded.Name = "btnDownloadMultiThreaded";
			this.btnDownloadMultiThreaded.Size = new System.Drawing.Size(148, 23);
			this.btnDownloadMultiThreaded.TabIndex = 46;
			this.btnDownloadMultiThreaded.Text = "Download multi threaded";
			this.btnDownloadMultiThreaded.UseVisualStyleBackColor = true;
			this.btnDownloadMultiThreaded.Click += new System.EventHandler(this.btnDownloadMultiThreaded_Click);
			// 
			// textBoxUrl
			// 
			this.textBoxUrl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
			| System.Windows.Forms.AnchorStyles.Right)));
			this.textBoxUrl.Location = new System.Drawing.Point(61, 6);
			this.textBoxUrl.Name = "textBoxUrl";
			this.textBoxUrl.Size = new System.Drawing.Size(758, 20);
			this.textBoxUrl.TabIndex = 45;
			// 
			// btnDownloadSingleThreaded
			// 
			this.btnDownloadSingleThreaded.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnDownloadSingleThreaded.Location = new System.Drawing.Point(12, 484);
			this.btnDownloadSingleThreaded.Name = "btnDownloadSingleThreaded";
			this.btnDownloadSingleThreaded.Size = new System.Drawing.Size(138, 23);
			this.btnDownloadSingleThreaded.TabIndex = 44;
			this.btnDownloadSingleThreaded.Text = "Download single threaded";
			this.btnDownloadSingleThreaded.UseVisualStyleBackColor = true;
			this.btnDownloadSingleThreaded.Click += new System.EventHandler(this.btnDownloadSingleThreaded_Click);
			// 
			// tabPageLogger
			// 
			this.tabPageLogger.BackColor = System.Drawing.SystemColors.ButtonFace;
			this.tabPageLogger.Controls.Add(this.checkBoxAutoscrollLog);
			this.tabPageLogger.Controls.Add(this.listViewLog);
			this.tabPageLogger.Location = new System.Drawing.Point(4, 22);
			this.tabPageLogger.Name = "tabPageLogger";
			this.tabPageLogger.Padding = new System.Windows.Forms.Padding(3);
			this.tabPageLogger.Size = new System.Drawing.Size(825, 552);
			this.tabPageLogger.TabIndex = 1;
			this.tabPageLogger.Text = "Лог";
			// 
			// checkBoxAutoscrollLog
			// 
			this.checkBoxAutoscrollLog.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.checkBoxAutoscrollLog.AutoSize = true;
			this.checkBoxAutoscrollLog.Checked = true;
			this.checkBoxAutoscrollLog.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBoxAutoscrollLog.Location = new System.Drawing.Point(717, 529);
			this.checkBoxAutoscrollLog.Name = "checkBoxAutoscrollLog";
			this.checkBoxAutoscrollLog.Size = new System.Drawing.Size(102, 17);
			this.checkBoxAutoscrollLog.TabIndex = 1;
			this.checkBoxAutoscrollLog.Text = "Автопрокрутка";
			this.checkBoxAutoscrollLog.UseVisualStyleBackColor = true;
			// 
			// listViewLog
			// 
			this.listViewLog.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
			this.columnHeaderEventDate,
			this.columnHeaderEventText});
			this.listViewLog.FullRowSelect = true;
			this.listViewLog.HideSelection = false;
			this.listViewLog.Location = new System.Drawing.Point(6, 6);
			this.listViewLog.MultiSelect = false;
			this.listViewLog.Name = "listViewLog";
			this.listViewLog.Size = new System.Drawing.Size(813, 520);
			this.listViewLog.TabIndex = 0;
			this.listViewLog.UseCompatibleStateImageBehavior = false;
			this.listViewLog.View = System.Windows.Forms.View.Details;
			// 
			// columnHeaderEventDate
			// 
			this.columnHeaderEventDate.Text = "Дата и время";
			this.columnHeaderEventDate.Width = 140;
			// 
			// columnHeaderEventText
			// 
			this.columnHeaderEventText.Text = "Событие";
			this.columnHeaderEventText.Width = 666;
			// 
			// Form1
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(857, 602);
			this.Controls.Add(this.tabControl1);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.MaximizeBox = false;
			this.Name = "Form1";
			this.Text = "Multi threaded downloader library (GUI test)";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
			this.Load += new System.EventHandler(this.Form1_Load);
			this.Paint += new System.Windows.Forms.PaintEventHandler(this.Form1_Paint);
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownProxyPort)).EndInit();
			this.tabControl1.ResumeLayout(false);
			this.tabPageMain.ResumeLayout(false);
			this.tabPageMain.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownRetryInterval)).EndInit();
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.groupBoxProxy.ResumeLayout(false);
			this.groupBoxProxy.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownConnectionTimeout)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownTryCountInsideEachThread)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownTryCountPerThread)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownChunksMergingUpdateInterval)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownUpdateInterval)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownThreadCount)).EndInit();
			this.tabPageLogger.ResumeLayout(false);
			this.tabPageLogger.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion
		private System.Windows.Forms.ToolTip toolTip1;
		private System.Windows.Forms.TabControl tabControl1;
		private System.Windows.Forms.TabPage tabPageMain;
		private System.Windows.Forms.CheckBox checkBoxSkipHeadRequest;
		private System.Windows.Forms.Label label13;
		private System.Windows.Forms.NumericUpDown numericUpDownRetryInterval;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.Label lblCookieCount;
		private System.Windows.Forms.Button btnCookies;
		private System.Windows.Forms.GroupBox groupBoxProxy;
		private System.Windows.Forms.Label label12;
		private System.Windows.Forms.NumericUpDown numericUpDownProxyPort;
		private System.Windows.Forms.TextBox textBoxProxyAddress;
		private System.Windows.Forms.Label label11;
		private System.Windows.Forms.CheckBox checkBoxFakeDownloading;
		private System.Windows.Forms.CheckBox checkBoxDownloadToRAM;
		private System.Windows.Forms.CheckBox checkBoxMergeChunksAutomatically;
		private System.Windows.Forms.NumericUpDown numericUpDownConnectionTimeout;
		private System.Windows.Forms.Label label9;
		private System.Windows.Forms.CheckBox checkBoxUseAccurateMode;
		private System.Windows.Forms.Label label10;
		private System.Windows.Forms.NumericUpDown numericUpDownTryCountInsideEachThread;
		private System.Windows.Forms.NumericUpDown numericUpDownTryCountPerThread;
		private System.Windows.Forms.Label label8;
		private MultipleProgressBar progressBarDownload;
		private System.Windows.Forms.NumericUpDown numericUpDownChunksMergingUpdateInterval;
		private System.Windows.Forms.Label label7;
		private System.Windows.Forms.NumericUpDown numericUpDownUpdateInterval;
		private System.Windows.Forms.Label label6;
		private System.Windows.Forms.CheckBox checkBoxUseRamForTempFiles;
		private System.Windows.Forms.Button btnHeaders;
		private System.Windows.Forms.Label lblMergeProgress;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.NumericUpDown numericUpDownThreadCount;
		private System.Windows.Forms.Button btnSelectTempDir;
		private System.Windows.Forms.Button btnSelectFile;
		private System.Windows.Forms.TextBox txtBoxTempDir;
		private System.Windows.Forms.TextBox textBoxOutputFileName;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label lblDownloadProgress;
		private System.Windows.Forms.Button btnDownloadMultiThreaded;
		private System.Windows.Forms.TextBox textBoxUrl;
		private System.Windows.Forms.Button btnDownloadSingleThreaded;
		private System.Windows.Forms.TabPage tabPageLogger;
		private System.Windows.Forms.ListView listViewLog;
		private System.Windows.Forms.ColumnHeader columnHeaderEventDate;
		private System.Windows.Forms.ColumnHeader columnHeaderEventText;
		private System.Windows.Forms.CheckBox checkBoxAutoscrollLog;
		private System.Windows.Forms.CheckBox checkBoxIgnoreHeaderRequestErrors;
		private System.Windows.Forms.TextBox textBoxHeaderRequestMethod;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.CheckBox checkBoxDeleteTempFiles;
		private System.Windows.Forms.CheckBox checkBoxDeleteOnlyIncompleteChunks;
	}
}
