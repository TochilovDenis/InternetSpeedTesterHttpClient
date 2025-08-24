namespace InternetSpeedTesterHttpClient
{
    partial class ISTHC
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

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
            StartTestButton = new Button();
            CancelTestButton = new Button();
            ProgressBar = new ProgressBar();
            StatusLabel = new Label();
            PingResultLabel = new Label();
            DownloadResultLabel = new Label();
            UploadResultLabel = new Label();
            ProgressLabel = new Label();
            SpeedLabel = new Label();
            ReauthButton = new Button();
            UploadTestButton = new Button();
            SuspendLayout();
            // 
            // StartTestButton
            // 
            StartTestButton.Font = new Font("Consolas", 14.25F);
            StartTestButton.Location = new Point(12, 248);
            StartTestButton.Name = "StartTestButton";
            StartTestButton.Size = new Size(86, 34);
            StartTestButton.TabIndex = 0;
            StartTestButton.Text = "Start";
            StartTestButton.UseVisualStyleBackColor = true;
            StartTestButton.Click += StartTestButton_Click;
            // 
            // CancelTestButton
            // 
            CancelTestButton.Font = new Font("Consolas", 14.25F);
            CancelTestButton.Location = new Point(278, 248);
            CancelTestButton.Name = "CancelTestButton";
            CancelTestButton.Size = new Size(81, 34);
            CancelTestButton.TabIndex = 1;
            CancelTestButton.Text = "Cancel";
            CancelTestButton.UseVisualStyleBackColor = true;
            CancelTestButton.Click += CancelTestButton_Click;
            // 
            // ProgressBar
            // 
            ProgressBar.Location = new Point(12, 219);
            ProgressBar.Name = "ProgressBar";
            ProgressBar.Size = new Size(347, 24);
            ProgressBar.TabIndex = 2;
            // 
            // StatusLabel
            // 
            StatusLabel.AutoSize = true;
            StatusLabel.Font = new Font("Consolas", 14.25F);
            StatusLabel.Location = new Point(12, 9);
            StatusLabel.Name = "StatusLabel";
            StatusLabel.Size = new Size(70, 22);
            StatusLabel.TabIndex = 3;
            StatusLabel.Text = "Статус";
            // 
            // PingResultLabel
            // 
            PingResultLabel.AutoSize = true;
            PingResultLabel.Font = new Font("Consolas", 14.25F);
            PingResultLabel.Location = new Point(12, 45);
            PingResultLabel.Name = "PingResultLabel";
            PingResultLabel.Size = new Size(70, 22);
            PingResultLabel.TabIndex = 4;
            PingResultLabel.Text = "Ping: ";
            // 
            // DownloadResultLabel
            // 
            DownloadResultLabel.AutoSize = true;
            DownloadResultLabel.Font = new Font("Consolas", 14.25F);
            DownloadResultLabel.Location = new Point(12, 79);
            DownloadResultLabel.Name = "DownloadResultLabel";
            DownloadResultLabel.Size = new Size(130, 22);
            DownloadResultLabel.TabIndex = 5;
            DownloadResultLabel.Text = "Скачивание: ";
            // 
            // UploadResultLabel
            // 
            UploadResultLabel.AutoSize = true;
            UploadResultLabel.Font = new Font("Consolas", 14.25F);
            UploadResultLabel.Location = new Point(12, 113);
            UploadResultLabel.Name = "UploadResultLabel";
            UploadResultLabel.Size = new Size(110, 22);
            UploadResultLabel.TabIndex = 6;
            UploadResultLabel.Text = "Загрузка: ";
            // 
            // ProgressLabel
            // 
            ProgressLabel.AutoSize = true;
            ProgressLabel.Font = new Font("Consolas", 14.25F);
            ProgressLabel.Location = new Point(12, 146);
            ProgressLabel.Name = "ProgressLabel";
            ProgressLabel.Size = new Size(110, 22);
            ProgressLabel.TabIndex = 7;
            ProgressLabel.Text = "Прогресс: ";
            // 
            // SpeedLabel
            // 
            SpeedLabel.AutoSize = true;
            SpeedLabel.Font = new Font("Consolas", 14.25F);
            SpeedLabel.Location = new Point(12, 180);
            SpeedLabel.Name = "SpeedLabel";
            SpeedLabel.Size = new Size(190, 22);
            SpeedLabel.TabIndex = 8;
            SpeedLabel.Text = "Текущая скорость: ";
            // 
            // ReauthButton
            // 
            ReauthButton.Font = new Font("Consolas", 14.25F);
            ReauthButton.Location = new Point(104, 248);
            ReauthButton.Name = "ReauthButton";
            ReauthButton.Size = new Size(81, 34);
            ReauthButton.TabIndex = 9;
            ReauthButton.Text = "Reauth";
            ReauthButton.UseVisualStyleBackColor = true;
            ReauthButton.Click += ReAuthButton_Click;
            // 
            // UploadTestButton
            // 
            UploadTestButton.Font = new Font("Consolas", 14.25F);
            UploadTestButton.Location = new Point(191, 248);
            UploadTestButton.Name = "UploadTestButton";
            UploadTestButton.Size = new Size(81, 34);
            UploadTestButton.TabIndex = 10;
            UploadTestButton.Text = "Upload";
            UploadTestButton.UseVisualStyleBackColor = true;
            UploadTestButton.Click += UploadTestButton_Click;
            // 
            // ISTHC
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(454, 301);
            Controls.Add(UploadTestButton);
            Controls.Add(ReauthButton);
            Controls.Add(SpeedLabel);
            Controls.Add(ProgressLabel);
            Controls.Add(UploadResultLabel);
            Controls.Add(DownloadResultLabel);
            Controls.Add(PingResultLabel);
            Controls.Add(StatusLabel);
            Controls.Add(ProgressBar);
            Controls.Add(CancelTestButton);
            Controls.Add(StartTestButton);
            Name = "ISTHC";
            Text = "Internet Speed Tester HttpClient";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button StartTestButton;
        private Button CancelTestButton;
        private ProgressBar ProgressBar;
        private Label StatusLabel;
        private Label PingResultLabel;
        private Label DownloadResultLabel;
        private Label UploadResultLabel;
        private Label ProgressLabel;
        private Label SpeedLabel;
        private Button ReauthButton;
        private Button UploadTestButton;
    }
}
