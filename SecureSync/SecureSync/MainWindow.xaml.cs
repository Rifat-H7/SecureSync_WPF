using Microsoft.Win32;
using System.IO;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Microsoft.AspNetCore.SignalR.Client;

namespace SecureSync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow 
    {
        private const int CHUNK_SIZE = 20 * 1024 * 1024; // 20MB
        private HubConnection _hubConnection;
        private readonly HttpClient _httpClient = new HttpClient();
        private string _selectedFilePath;

        public MainWindow()
        {
            InitializeComponent();
            InitializeSignalRConnection();
        }

        private void InitializeSignalRConnection()
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5000/fileuploadhub")
                .Build();

            _hubConnection.On<string, int, string>("ReceiveMessage", (fileName, chunkNumber, message) =>
            {
                Dispatcher.Invoke(() =>
                {
                    UploadStatusText.Text = $"File: {fileName}, Chunk: {chunkNumber}, Status: {message}";
                });
            });

            _hubConnection.StartAsync().ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    MessageBox.Show("SignalR connection failed: " + task.Exception?.Message);
                }
            });
        }

        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                _selectedFilePath = openFileDialog.FileName;
                SelectedFileText.Text = _selectedFilePath;
                StartFileUploadAsync();
            }
        }

        private async Task StartFileUploadAsync()
        {
            if (string.IsNullOrEmpty(_selectedFilePath)) return;

            var fileName = Path.GetFileName(_selectedFilePath);
            var fileBytes = await File.ReadAllBytesAsync(_selectedFilePath);
            var totalChunks = (int)Math.Ceiling(fileBytes.Length / (double)CHUNK_SIZE);

            for (int chunkNumber = 0; chunkNumber < totalChunks; chunkNumber++)
            {
                var chunkData = GetFileChunk(fileBytes, chunkNumber);
                var chunkFilePath = SaveChunkToTempFile(chunkData, chunkNumber);

                var isUploaded = await UploadChunkAsync(fileName, chunkNumber, chunkFilePath, totalChunks);
                if (isUploaded)
                {
                    File.Delete(chunkFilePath); // Delete temporary chunk file after successful upload
                }
                else
                {
                    while (!await UploadChunkAsync(fileName, chunkNumber, chunkFilePath, totalChunks))
                    {
                        UploadStatusText.Text = $"Retrying upload of chunk {chunkNumber}...";
                    }
                    File.Delete(chunkFilePath); // Delete after retry success
                }

                // Update progress
                UploadProgressBar.Value = ((double)(chunkNumber + 1) / totalChunks) * 100;
            }

            UploadStatusText.Text = "File upload completed.";
        }

        private byte[] GetFileChunk(byte[] fileBytes, int chunkNumber)
        {
            int offset = chunkNumber * CHUNK_SIZE;
            int remainingBytes = Math.Min(CHUNK_SIZE, fileBytes.Length - offset);
            byte[] chunkData = new byte[remainingBytes];
            Array.Copy(fileBytes, offset, chunkData, 0, remainingBytes);
            return chunkData;
        }

        private string SaveChunkToTempFile(byte[] chunkData, int chunkNumber)
        {
            string tempFilePath = Path.Combine(Path.GetTempPath(), $"chunk_{chunkNumber}.tmp");
            MessageBox.Show(tempFilePath);
            File.WriteAllBytes(tempFilePath, chunkData);
            return tempFilePath;
        }

        private async Task<bool> UploadChunkAsync(string fileName, int chunkNumber, string chunkFilePath, int totalChunks)
        {
            using (var content = new MultipartFormDataContent())
            using (var chunkStream = new FileStream(chunkFilePath, FileMode.Open))
            {
                var chunkContent = new StreamContent(chunkStream);
                chunkContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                content.Add(chunkContent, "FileChunk", $"{fileName}.chunk{chunkNumber}");
                content.Add(new StringContent(fileName), "FileName");
                content.Add(new StringContent(chunkNumber.ToString()), "ChunkNumber");

                var response = await _httpClient.PostAsync("http://localhost:5000/api/fileupload/upload", content);

                return response.IsSuccessStatusCode;
            }
        }

        private async void DownloadFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath)) return;

            var fileName = Path.GetFileName(_selectedFilePath);
            var response = await _httpClient.GetAsync($"http://localhost:5000/api/fileupload/download/{fileName}");

            if (response.IsSuccessStatusCode)
            {
                var downloadFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
                await using (var fileStream = new FileStream(downloadFilePath, FileMode.Create, FileAccess.Write))
                {
                    await response.Content.CopyToAsync(fileStream);
                }

                DownloadStatusText.Text = $"File downloaded to {downloadFilePath}";
            }
            else
            {
                DownloadStatusText.Text = "Failed to download file.";
            }
        }
    }
}