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
        private string? username;
        private bool isReceiving = false;
        private readonly StringBuilder receiveBuffer = new StringBuilder();

        private const string MAX_FILE_SIZE_PREFIX = "MAX_FILE_SIZE:";

        private const string IMAGE_PREFIX = "IMAGE_DATA:";
        private const int MAX_IMAGE_MESSAGE_LENGTH = 750 * 1024;

        private const string FILE_PREFIX = "FILE_DATA:";

        private const string NEW_MAX_FILE_MESSAGE_STR = "NEW_MAX_FILE_SIZE:";
        private int MAX_FILE_MESSAGE_LENGTH = 0;

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
        private async void sendFileItem_Click(object? sender, EventArgs e)
        {
            if (clientStream == null || !clientStream.CanWrite)
            {
                MessageBox.Show("Not connected to server.", "Error.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "All Files (*.*)|*.*";
                openFileDialog.Title = "Select file to send.";

                // Declare filePath here so it's accessible within the try block
                string filePath = string.Empty;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = openFileDialog.FileName; // Assign the value here
                }
                else
                {
                    // If the dialog was cancelled, just return
                    return;
                }

                try
                {
                    FileInfo fileInfo = new FileInfo(filePath);

                    string fileName = Path.GetFileName(filePath);
                    int maxAllowedBase64Length = CalculateMaxBase64FileLength(MAX_FILE_MESSAGE_LENGTH, username ?? "", fileName);

                    long estimatedMaxRawBytes = (long)(maxAllowedBase64Length * 0.75);

                    if (fileInfo.Length > estimatedMaxRawBytes)
                    {
                        MessageBox.Show($"File is too large to send. Maximum allowed raw file size is approximately {estimatedMaxRawBytes / 1024} KB.", "File Too Large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    byte[] fileBytes = File.ReadAllBytes(filePath);

                    string base64File = Convert.ToBase64String(fileBytes);

                    if (base64File.Length > maxAllowedBase64Length)
                    {
                        MessageBox.Show("File is too large to send after encoding (Base64 exceeds limit).", "File Too Large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    string base64FileHash = CalculateSha256Hash(base64File);

                    string fileMessage = $"{FILE_PREFIX}{username}|{fileName}|{base64File}|{base64FileHash}\n";

                    if (fileMessage.Length > MAX_FILE_MESSAGE_LENGTH)
                    {
                        MessageBox.Show("File message string is too large to send (exceeds total message limit). This should not happen if previous checks are correct.", "Internal Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    byte[] dataToSend = Encoding.UTF8.GetBytes(fileMessage);
                    await clientStream.WriteAsync(dataToSend, 0, dataToSend.Length);

                    AppendChatText($"You sent file: {fileName}");
                }
                catch (OutOfMemoryException)
                {
                    MessageBox.Show("Not enough memory to process this file. It's likely still too large.", "Out of Memory", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to send file: {filePath}. \n {ex.Message}", "Error.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
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
            if (username.Length > 7)
            {
                MessageBox.Show("Username cannot be more than 7 characters", "Invalid Username", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            else if (receivedData.StartsWith(MAX_FILE_SIZE_PREFIX)) // This handles the initial MAX_FILE_SIZE message
            {
                string payload = receivedData.Substring(MAX_FILE_SIZE_PREFIX.Length);
                int pipeIndex = payload.IndexOf('|');
                if (pipeIndex > 0 && pipeIndex < payload.Length - 1)
                {
                    string numberPart = payload.Substring(0, pipeIndex);
                    string hashPart = payload.Substring(pipeIndex + 1);

                    string calculatedHash = CalculateSha256Hash(MAX_FILE_SIZE_PREFIX + numberPart);

                    if (calculatedHash.Equals(hashPart, StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(numberPart, out int maxFileSize))
                        {
                            MAX_FILE_MESSAGE_LENGTH = maxFileSize; // Update the client's stored max file size
                            AppendChatText($"Maximum file size is now: {MAX_FILE_MESSAGE_LENGTH} bytes.");
                        }
                        else
                        {
                            AppendChatText("Server sent invalid max file size value.");
                        }
                    }
                    else
                    {
                        AppendChatText("Server sent invalid max file size hash. Ignoring update.");
                    }
                }
                else
                {
                    AppendChatText("Malformed max file size message from server.");
                }
            }
            // Add this new block to handle the NEW_MAX_FILE_SIZE messages
            else if (receivedData.StartsWith(NEW_MAX_FILE_MESSAGE_STR)) // This will handle updates from the server command
            {
                string payload = receivedData.Substring(NEW_MAX_FILE_MESSAGE_STR.Length);
                int pipeIndex = payload.IndexOf('|');
                if (pipeIndex > 0 && pipeIndex < payload.Length - 1)
                {
                    string numberPart = payload.Substring(0, pipeIndex);
                    string hashPart = payload.Substring(pipeIndex + 1);

                    // IMPORTANT: The server calculates the hash of "NEW_MAX_FILE_SIZE:SIZE"
                    // So, you need to calculate the hash for verification using the same prefix.
                    string calculatedHash = CalculateSha256Hash(NEW_MAX_FILE_MESSAGE_STR + numberPart);

                    if (calculatedHash.Equals(hashPart, StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(numberPart, out int newMaxFileSize))
                        {
                            MAX_FILE_MESSAGE_LENGTH = newMaxFileSize; // Update the client's stored max file size
                            AppendChatText($"Server: Maximum file size updated to {MAX_FILE_MESSAGE_LENGTH} bytes.");
                        }
                        else
                        {
                            AppendChatText("Server sent an invalid new max file size value.");
                        }
                    }
                    else
                    {
                        AppendChatText("Server sent invalid new max file size hash. Ignoring update.");
                    }
                }
                else
                {
                    AppendChatText("Malformed new max file size message from server.");
                }
            }


            // Handle file data
            else if (receivedData.StartsWith(FILE_PREFIX))
            {
                string fileDataWithHash = receivedData.Substring(FILE_PREFIX.Length);
                // Now: username|filename|base64|hash
                int firstPipe = fileDataWithHash.IndexOf('|');
                int secondPipe = fileDataWithHash.IndexOf('|', firstPipe + 1);
                int lastPipe = fileDataWithHash.LastIndexOf('|');

                if (firstPipe != -1 && secondPipe != -1 && lastPipe != -1 && firstPipe < secondPipe && secondPipe < lastPipe)
                {
                    string senderUsername = fileDataWithHash.Substring(0, firstPipe);
                    string originalFileName = fileDataWithHash.Substring(firstPipe + 1, secondPipe - firstPipe - 1);
                    string base64File = fileDataWithHash.Substring(secondPipe + 1, lastPipe - secondPipe - 1);

                    string receivedFileHash = fileDataWithHash.Substring(lastPipe + 1);
                    string calculatedFileHash = CalculateSha256Hash(base64File);

                    DialogResult result = MessageBox.Show(
                        $"{senderUsername} sent a file: {originalFileName}.\nYou should only accept files that you trust.\nAccept?",
                        "Incoming file.",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );

                    if (calculatedFileHash.Equals(receivedFileHash, StringComparison.OrdinalIgnoreCase) && result == DialogResult.Yes)
                    {
                        try
                        {
                            byte[] fileBytes = Convert.FromBase64String(base64File);
                            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                            {
                                saveFileDialog.Title = $"Save file from {senderUsername}";
                                saveFileDialog.Filter = "All Files (*.*)|*.*";
                                saveFileDialog.FileName = originalFileName;
                                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                                {
                                    File.WriteAllBytes(saveFileDialog.FileName, fileBytes);
                                    AppendChatText($"{senderUsername} sent a file: {Path.GetFileName(saveFileDialog.FileName)}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            AppendChatText($"[Error saving file: {ex.Message}]");
                        }
                    }

                    else if (result == DialogResult.No)
                    {
                        AppendChatText($"{senderUsername} sent a file: {originalFileName}. [NOT SAVED]");
                    }

                    else
                    {
                        AppendChatText($"[CORRUPTED FILE RECEIVED from {senderUsername}]");
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

        private void exitMenuItem_Click(object? sender, EventArgs e)
        {
            this.Close();
        }

        private void aboutMenuItem_Click(object? sender, EventArgs e)
        {
            MessageBox.Show("XZChat v0.0.1 RC2 \nMade with love from GalaxyDoge72! <3\nCheck out the source code: https://github.com/GalaxyDoge72/XZChat", "About");
        }

        // Add this helper method to calculate the maximum allowed base64 file length
        private int CalculateMaxBase64FileLength(int maxFileMessageLength, string username, string fileName)
        {
            // FILE_PREFIX + username + '|' + fileName + '|' + base64File + '|' + hash + '\n'
            int hashLength = 64; // SHA256 hex
            int overhead =
                FILE_PREFIX.Length +
                username.Length + 1 +
                fileName.Length + 1 +
                1 + // for the '|' before hash
                hashLength + 1; // for '\n'
            return maxFileMessageLength - overhead;
        }
    }
}