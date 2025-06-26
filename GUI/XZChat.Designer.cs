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
            SuspendLayout();
            // 
            // button1
            // 
            button1.Font = new Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            button1.Location = new Point(627, 12);
            button1.Name = "button1";
            button1.Size = new Size(161, 34);
            button1.TabIndex = 0;
            button1.Text = "Connect to server...";
            button1.UseVisualStyleBackColor = true;
            // 
            // IPBox
            // 
            IPBox.Font = new Font("Consolas", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            IPBox.Location = new Point(96, 31);
            IPBox.Name = "IPBox";
            IPBox.Size = new Size(132, 25);
            IPBox.TabIndex = 1;
            // 
            // chatWindow
            // 
            chatWindow.Location = new Point(12, 71);
            chatWindow.Name = "chatWindow";
            chatWindow.Size = new Size(776, 260);
            chatWindow.TabIndex = 2;
            chatWindow.Text = "";
            // 
            // button2
            // 
            button2.Font = new Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            button2.Location = new Point(677, 400);
            button2.Name = "button2";
            button2.Size = new Size(131, 47);
            button2.TabIndex = 3;
            button2.Text = "Send Message";
            button2.UseVisualStyleBackColor = true;
            // 
            // chatBox
            // 
            chatBox.Font = new Font("Consolas", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            chatBox.Location = new Point(12, 352);
            chatBox.Name = "chatBox";
            chatBox.Size = new Size(628, 25);
            chatBox.TabIndex = 4;
            // 
            // UsernameBox
            // 
            UsernameBox.Location = new Point(96, 2);
            UsernameBox.Name = "UsernameBox";
            UsernameBox.Size = new Size(132, 23);
            UsernameBox.TabIndex = 5;
            // 
            // btnSendImage
            // 
            btnSendImage.Location = new Point(677, 352);
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
            ClientSize = new Size(820, 459);
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
    }
}
