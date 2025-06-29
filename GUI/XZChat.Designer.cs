namespace ChatClientGUICS
{
    partial class XZChat
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
            button1 = new Button();
            IPBox = new TextBox();
            chatWindow = new RichTextBox();
            button2 = new Button();
            chatBox = new TextBox();
            UsernameBox = new TextBox();
            btnSendImage = new Button();
            menuStrip1 = new MenuStrip();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.Name = "menuStrip1";
            menuStrip1.TabIndex = 100;
            menuStrip1.Text = "menuStrip1";
            menuStrip1.Dock = DockStyle.Top;

            // Example menu items
            var fileMenu = new ToolStripMenuItem("File");
            var helpMenu = new ToolStripMenuItem("Help");

            var sendFileItem = new ToolStripMenuItem("Send");
            var exitMenuItem = new ToolStripMenuItem("Exit");

            exitMenuItem.Click += exitMenuItem_Click;
            sendFileItem.Click += sendFileItem_Click;
            fileMenu.DropDownItems.Add(exitMenuItem);
            fileMenu.DropDownItems.Add(sendFileItem);

 
            var aboutMenuItem = new ToolStripMenuItem("About");
            aboutMenuItem.Click += aboutMenuItem_Click;
            helpMenu.DropDownItems.Add(aboutMenuItem);

            menuStrip1.Items.Add(fileMenu);
            menuStrip1.Items.Add(helpMenu);

            // Add the MenuStrip to the form FIRST
            this.Controls.Add(menuStrip1);
            this.MainMenuStrip = menuStrip1;

            // 
            // button1
            // 
            button1.Font = new Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            button1.Location = new Point(627, 36); // was 12, now 36
            button1.Name = "button1";
            button1.Size = new Size(161, 34);
            button1.TabIndex = 0;
            button1.Text = "Connect to server...";
            button1.UseVisualStyleBackColor = true;
            // 
            // IPBox
            // 
            IPBox.Font = new Font("Consolas", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            IPBox.Location = new Point(96, 55); // was 31, now 55
            IPBox.Name = "IPBox";
            IPBox.Size = new Size(132, 25);
            IPBox.TabIndex = 1;
            // 
            // chatWindow
            // 
            chatWindow.Location = new Point(12, 95); // was 71, now 95
            chatWindow.Name = "chatWindow";
            chatWindow.Size = new Size(776, 260);
            chatWindow.TabIndex = 2;
            chatWindow.Text = "";
            // 
            // button2
            // 
            button2.Font = new Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            button2.Location = new Point(677, 424); // was 400, now 424
            button2.Name = "button2";
            button2.Size = new Size(131, 47);
            button2.TabIndex = 3;
            button2.Text = "Send Message";
            button2.UseVisualStyleBackColor = true;
            // 
            // chatBox
            // 
            chatBox.Font = new Font("Consolas", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            chatBox.Location = new Point(12, 376); // was 352, now 376
            chatBox.Name = "chatBox";
            chatBox.Size = new Size(628, 25);
            chatBox.TabIndex = 4;
            // 
            // UsernameBox
            // 
            UsernameBox.Location = new Point(96, 26); // was 2, now 26
            UsernameBox.Name = "UsernameBox";
            UsernameBox.Size = new Size(132, 23);
            UsernameBox.TabIndex = 5;
            // 
            // btnSendImage
            // 
            btnSendImage.Location = new Point(677, 376); // was 352, now 376
            btnSendImage.Name = "btnSendImage";
            btnSendImage.Size = new Size(131, 42);
            btnSendImage.TabIndex = 8;
            btnSendImage.Text = "Send Image...";
            btnSendImage.UseVisualStyleBackColor = true;

            // 
            // XZChat
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(820, 483); // increased height for new layout
            Controls.Add(btnSendImage);
            Controls.Add(UsernameBox);
            Controls.Add(chatBox);
            Controls.Add(button2);
            Controls.Add(chatWindow);  
            Controls.Add(IPBox);
            Controls.Add(button1);
            Name = "XZChat";
            Text = "XZChat";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button button1;
        private TextBox IPBox;
        private RichTextBox chatWindow;
        private Button button2;
        private TextBox chatBox;
        private TextBox UsernameBox;
        private Button btnSendImage;
        private MenuStrip menuStrip1;
    }
}
