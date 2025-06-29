using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography; // Needed for SHA256
using System.IO; // Needed for IOException, File operations
using System.Drawing; // Needed for Image objects

namespace ChatClientGUICS
{
    public partial class XZChat : Form
    {
        private TcpClient? clientConnection;
        private NetworkStream? clientStream;
        private const int DEFAULT_SERVER_PORT = 3708;
        private const int DEFAULT_FILE_PORT = 4045;
        private string? username;
        private bool isReceiving = false;
        private readonly StringBuilder receiveBuffer = new StringBuilder();

        private const string IMAGE_PREFIX = "IMAGE_DATA:";
        private const int MAX_IMAGE_MESSAGE_LENGTH = 750 * 1024;

        public XZChat()
        {
            this.Name = "XZChat";
            InitializeComponent();
            var topPanel = new TableLayoutPanel
            {
                ColumnCount = 3,
                RowCount = 2,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 6)
            };

            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); 
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      

            var ipLabel = new Label { Text = "IP Address:", TextAlign = ContentAlignment.MiddleLeft, Anchor = AnchorStyles.Left, AutoSize = true };
            var userLabel = new Label { Text = "Username:", TextAlign = ContentAlignment.MiddleLeft, Anchor = AnchorStyles.Left, AutoSize = true };

            this.Controls.Remove(IPBox);
            this.Controls.Remove(UsernameBox);
            this.Controls.Remove(button1);

            topPanel.Controls.Add(ipLabel, 0, 0);
            topPanel.Controls.Add(IPBox, 1, 0);
            topPanel.Controls.Add(button1, 2, 0);

            topPanel.Controls.Add(userLabel, 0, 1);
            topPanel.Controls.Add(UsernameBox, 1, 1);

            IPBox.Dock = DockStyle.Fill;
            UsernameBox.Dock = DockStyle.Fill;

            button1.Dock = DockStyle.Fill;

            this.Controls.Add(topPanel);

            this.MinimumSize = new System.Drawing.Size(500, 400);

            chatWindow.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            chatBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            button2.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;       
            btnSendImage.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; 


            this.FormClosing += XZChat_FormClosing;

            // Disable controls until connected //
            SetUIState(isConnected: false);

            button1.Click += button1_Click;
            button2.Click += Button2_Click;
            btnSendImage.Click += BtnSendImage_Click;
        }

        private string CalculateSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create()) 
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2")); // Convert to hexadecimal and append //
                }
                return builder.ToString();
            }
        }

        private async void Button2_Click(object? sender, EventArgs e)
        {
            string messageToSend = chatBox.Text.Trim();
            if (clientStream == null || !clientStream.CanWrite || string.IsNullOrEmpty(messageToSend))
            {
                return; // There's nothing to send, so skip //
            }

            try
            {
                string hash = CalculateSha256Hash(messageToSend); // Calculate the hash //
                string messageWithHash = messageToSend + "|" + hash + "\n"; // Add the hash to the end of the message //
                byte[] data = Encoding.UTF8.GetBytes(messageWithHash); // Convert to Unicode //
                await clientStream.WriteAsync(data, 0, data.Length); // Wait for write operation and then send //

                AppendChatText($"You: {messageToSend}"); // Show message locally //
                chatBox.Clear();
            }
            catch (Exception ex)
            {
                AppendChatText($"Send Error: {ex.Message}");
            }
        }

        private async void BtnSendImage_Click(object? sender, EventArgs e)
        {
            if (clientStream == null || !clientStream.CanWrite) // Check if we can even send messages to begin with //
            {
                MessageBox.Show("Not connected to server.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All Files|*.*";
                openFileDialog.Title = "Select an Image to Send";

                if (openFileDialog.ShowDialog() == DialogResult.OK) // Check if we got a valid file to encode //
                {
                    try
                    {
                        byte[] imageBytes = File.ReadAllBytes(openFileDialog.FileName); // Read file //

                        // Convert to Base64 because I didn't design images to be sent when first making this like a fuckwit... //
                        string base64Image = Convert.ToBase64String(imageBytes);

                        string imageHash = CalculateSha256Hash(base64Image); // Calculate the hash of the image //

                        // Add an IMAGE_PREFIX, so other clients can pick up what we're sending //
                        string imageMessage = $"{IMAGE_PREFIX}{base64Image}|{imageHash}\n"; 

                        if (imageMessage.Length > MAX_IMAGE_MESSAGE_LENGTH) // Flip shit if we can't fit the image
                        {
                            MessageBox.Show("Image is too large to send.", "Image Too Large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        byte[] dataToSend = Encoding.UTF8.GetBytes(imageMessage); // Encode to UTF-8 //
                        await clientStream.WriteAsync(dataToSend, 0, dataToSend.Length); // Send Base64 encoded image //

                        AppendImageToChat(Image.FromFile(openFileDialog.FileName), "You");
                    }
                    catch (Exception ex)
                    {
                        AppendChatText($"Error sending image: {ex.Message}");
                        MessageBox.Show($"Error sending image: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            string ipAddressString = IPBox.Text.Trim();
            username = UsernameBox.Text.Trim();
            if (string.IsNullOrEmpty(username))
            {
                username = "Anonymous"; // Use this if the user doesn't provide a username //
            }

            if (!IPAddress.TryParse(ipAddressString, out IPAddress? serverIP)) // Check if we've got a valid IP and if we don't, flip shit //
            {
                MessageBox.Show("Please enter a valid IP address.", "Invalid IP", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                Disconnect(); // Ensure any previous connection is closed

                clientConnection = new TcpClient(); // Open TCP connection //
                SetUIState(isConnecting: true);
                AppendChatText($"Attempting to connect to {serverIP}:{DEFAULT_SERVER_PORT}...");

                await clientConnection.ConnectAsync(serverIP, DEFAULT_SERVER_PORT); // Try connection to the IP the user gave //

                clientStream = clientConnection.GetStream();
                AppendChatText("Connected to server!");

                // Set nickname on server
                string nickCommand = "/nick " + username;
                string nickHash = CalculateSha256Hash(nickCommand);
                string nickMessage = $"{nickCommand}|{nickHash}\n";
                byte[] usernameData = Encoding.UTF8.GetBytes(nickMessage);
                await clientStream.WriteAsync(usernameData, 0, usernameData.Length);

                SetUIState(isConnected: true);
                chatBox.Focus();

                isReceiving = true;
                _ = ReceiveMessagesAsync(); // Start receiving messages

                this.Text = $"XZChat - Logged in as {username}";
            }
            catch (Exception ex)
            {
                // This should only happen if I've fucked up the server software REALLY badly... //
                AppendChatText($"Connection Error: {ex.Message}"); 
                MessageBox.Show($"Connection failed: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetUIState(isConnected: false);
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            byte[] buffer = new byte[8192]; // 8KB buffer for receiving chunks
            try
            {
                while (isReceiving && clientConnection != null && clientConnection.Connected && clientStream != null && clientStream.CanRead)
                {
                    int bytesRead = await clientStream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) // If the server doesn't send anything //
                    {
                        AppendChatText("Server has closed the connection.");
                        break;
                    }

                    receiveBuffer.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                    string data = receiveBuffer.ToString();

                    int newlineIndex;
                    while ((newlineIndex = data.IndexOf('\n')) != -1)
                    {
                        string message = data.Substring(0, newlineIndex).TrimEnd('\r');
                        ProcessReceivedMessage(message);
                        data = data.Substring(newlineIndex + 1);
                    }
                    receiveBuffer.Clear();
                    receiveBuffer.Append(data);
                }
            }
            catch (IOException)
            {
                AppendChatText("Disconnected from server.");
            }
            catch (Exception ex)
            {
                if (isReceiving) // Only show error if we weren't expecting to disconnect
                {
                    AppendChatText($"Receive Error: {ex.Message}");
                }
            }
            finally
            {
                if (isReceiving) // oh no, our loop! it's broken! //
                {
                    this.Invoke(new Action(() => Disconnect()));
                }
            }
        }

        private void ProcessReceivedMessage(string receivedData)
        {
            // Handle Image Data
            if (receivedData.StartsWith(IMAGE_PREFIX))
            {
                string imageDataWithHash = receivedData.Substring(IMAGE_PREFIX.Length);
                int hashSeparatorIndex = imageDataWithHash.LastIndexOf('|');

                if (hashSeparatorIndex != -1)
                {
                    string base64Image = imageDataWithHash.Substring(0, hashSeparatorIndex);
                    string receivedImageHash = imageDataWithHash.Substring(hashSeparatorIndex + 1);
                    string calculatedImageHash = CalculateSha256Hash(base64Image);

                    if (calculatedImageHash.Equals(receivedImageHash, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            byte[] imageBytes = Convert.FromBase64String(base64Image);
                            using (MemoryStream ms = new MemoryStream(imageBytes))
                            {
                                AppendImageToChat(Image.FromStream(ms), "Remote User");
                            }
                        }
                        catch (Exception imgEx)
                        {
                            AppendChatText($"[Error processing image: {imgEx.Message}]");
                        }
                    }
                    else
                    {
                        AppendChatText($"[CORRUPTED IMAGE RECEIVED]");
                    }
                }
            }
            // Handle Text Data
            else
            {
                int lastPipeIndex = receivedData.LastIndexOf('|');
                if (lastPipeIndex != -1 && lastPipeIndex < receivedData.Length - 1)
                {
                    string messagePart = receivedData.Substring(0, lastPipeIndex);
                    string hashPart = receivedData.Substring(lastPipeIndex + 1);
                    string calculatedHash = CalculateSha256Hash(messagePart);

                    if (calculatedHash.Equals(hashPart, StringComparison.OrdinalIgnoreCase))
                    {
                        AppendChatText($"{messagePart}");
                    }
                    else
                    {
                        AppendChatText($"[CORRUPTED] {messagePart}");
                    }
                }
                else
                {
                    AppendChatText($"[RAW] {receivedData}");
                }
            }
        }

        private void AppendChatText(string text)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AppendChatText(text)));
            }
            else
            {
                chatWindow.AppendText(text + Environment.NewLine);
                chatWindow.ScrollToCaret();
            }
        }

        private void AppendImageToChat(Image image, string senderName)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => AppendImageToChat(image, senderName)));
                return;
            }

            AppendChatText($"{senderName} sent an image:");

            int maxWidth = chatWindow.ClientSize.Width - 25;
            int newWidth = Math.Min(image.Width, maxWidth);
            double scale = (double)newWidth / image.Width;
            int newHeight = (int)(image.Height * scale);

            using (Bitmap scaledImage = new Bitmap(image, new Size(newWidth, newHeight)))
            {
                IDataObject backupClipboard = Clipboard.GetDataObject();
                try
                {
                    Clipboard.SetImage(scaledImage);
                    chatWindow.Paste();
                }
                finally
                {
                    if (backupClipboard != null) Clipboard.SetDataObject(backupClipboard, true);
                }
            }
            AppendChatText(""); // Add a newline for spacing
            image.Dispose();
        }

        private void Disconnect()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(Disconnect));
                return;
            }

            isReceiving = false;

            clientStream?.Close();
            clientStream?.Dispose();
            clientStream = null;

            clientConnection?.Close();
            clientConnection?.Dispose();
            clientConnection = null;

            SetUIState(isConnected: false);
        }

        private void SetUIState(bool isConnected = false, bool isConnecting = false)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetUIState(isConnected, isConnecting)));
                return;
            }

            button1.Enabled = !isConnecting;
            IPBox.Enabled = !isConnecting && !isConnected;
            UsernameBox.Enabled = !isConnecting && !isConnected;

            button2.Enabled = isConnected;
            chatBox.Enabled = isConnected;
            btnSendImage.Enabled = isConnected;

            button1.Text = isConnected ? "Connected" : (isConnecting ? "Connecting..." : "Connect");
        }

        private void XZChat_FormClosing(object? sender, FormClosingEventArgs e)
        {
            Disconnect(); // Disconnect when we close the app //
        }
    }
}