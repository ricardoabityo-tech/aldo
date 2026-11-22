using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

namespace LegalInsight
{
    class Program
    {
        private static TcpListener _listener;
        private static string _baseDir;
        private static int _port = 5000;
        private static bool _isRunning = true;

        // In-memory persistent data store
        private static List<string> _consultations = new List<string>();
        private static List<string> _documents = new List<string>();

        static void Main(string[] args)
        {
            Console.Title = "Legal Insight - Server Platform Hukum Digital (Visual Studio C#)";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===============================================================================");
            Console.WriteLine("             LEGAL INSIGHT - PLATFORM LAYANAN HUKUM DIGITAL INDONESIA         ");
            Console.WriteLine("               Proyek Web C# / ASP.NET untuk Visual Studio & VS Code           ");
            Console.WriteLine("===============================================================================");
            Console.ResetColor();

            _baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!File.Exists(Path.Combine(_baseDir, "index.html")) && File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "index.html")))
            {
                _baseDir = Directory.GetCurrentDirectory();
            }

            // Find an available port starting at 5000
            bool started = false;
            for (int p = 5000; p <= 5015; p++)
            {
                try
                {
                    _port = p;
                    _listener = new TcpListener(IPAddress.Loopback, _port);
                    _listener.Start();
                    started = true;
                    break;
                }
                catch (Exception)
                {
                    if (_listener != null)
                    {
                        try { _listener.Stop(); } catch { }
                    }
                }
            }

            if (!started)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Gagal membuka port server.");
                Console.ResetColor();
                return;
            }

            string url = string.Format("http://localhost:{0}/", _port);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(string.Format("[OK] Server Legal Insight berjalan aktif di: {0}", url));
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(string.Format("[DIR] Direktori Web: {0}", _baseDir));
            Console.ResetColor();
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine("Modul & Fitur Aktif:");
            Console.WriteLine(" 1. Halaman Utama (Homepage Legal Insight & Profil Singkat)");
            Console.WriteLine(" 2. Profil Advokat (Direktori & Detail Lengkap Advokat Mitra)");
            Console.WriteLine(" 3. Konsultasi Hukum (Formulir Masalah, Jadwal, Pembayaran, Ruang Konsultasi)");
            Console.WriteLine(" 4. Layanan Dokumen Hukum (Penyusunan & Peninjauan Dokumen)");
            Console.WriteLine(" 5. Edukasi Hukum, Legal Academy, & Berita Hukum");
            Console.WriteLine(" 6. Dashboard Pengguna, Advokat, dan Administrator");
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine("Membuka browser otomatis...");

            try
            {
                Process.Start(url);
            }
            catch
            {
                Console.WriteLine("Silakan buka manual browser Anda ke alamat: " + url);
            }

            // Start listening thread
            ThreadPool.QueueUserWorkItem(ListenLoop);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Tekan [Q] atau [Ctrl+C] untuk menghentikan server...\n");
            Console.ResetColor();

            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e) {
                e.Cancel = true;
                _isRunning = false;
            };

            while (_isRunning)
            {
                try
                {
                    if (Environment.UserInteractive && !Console.IsInputRedirected && Console.KeyAvailable)
                    {
                        ConsoleKeyInfo key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Q || key.Key == ConsoleKey.Escape)
                        {
                            _isRunning = false;
                            break;
                        }
                    }
                }
                catch { }
                Thread.Sleep(200);
            }

            try
            {
                _listener.Stop();
            }
            catch { }

            Console.WriteLine("\nServer dimatikan. Terima kasih telah menggunakan Legal Insight.");
        }

        private static void ListenLoop(object state)
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(ProcessClient, client);
                }
                catch (Exception)
                {
                    if (!_isRunning) break;
                }
            }
        }

        private static void ProcessClient(object state)
        {
            TcpClient client = (TcpClient)state;
            try
            {
                using (NetworkStream stream = client.GetStream())
                {
                    stream.ReadTimeout = 5000;
                    stream.WriteTimeout = 5000;

                    byte[] buffer = new byte[8192];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead <= 0) return;

                    string requestText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    string[] lines = requestText.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
                    if (lines.Length == 0) return;

                    string requestLine = lines[0];
                    string[] parts = requestLine.Split(' ');
                    if (parts.Length < 2) return;

                    string method = parts[0].ToUpper();
                    string rawUrl = parts[1];

                    // Read content length if present
                    int contentLength = 0;
                    foreach (string line in lines)
                    {
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            int.TryParse(line.Substring(15).Trim(), out contentLength);
                        }
                    }

                    // Extract body if POST
                    string body = "";
                    int bodyIndex = requestText.IndexOf("\r\n\r\n");
                    if (bodyIndex >= 0)
                    {
                        body = requestText.Substring(bodyIndex + 4);
                    }

                    // Handle CORS preflight
                    if (method == "OPTIONS")
                    {
                        SendResponse(stream, 200, "OK", "text/plain", new byte[0]);
                        return;
                    }

                    // Handle API Endpoints
                    if (rawUrl.StartsWith("/api/"))
                    {
                        HandleApi(stream, method, rawUrl, body);
                        return;
                    }

                    // Handle Static Files
                    string cleanPath = rawUrl.Split('?')[0].TrimStart('/');
                    if (string.IsNullOrEmpty(cleanPath) || cleanPath == "index" || cleanPath == "index.html")
                    {
                        cleanPath = "index.html";
                    }

                    string filePath = Path.Combine(_baseDir, cleanPath.Replace('/', Path.DirectorySeparatorChar));

                    if (File.Exists(filePath))
                    {
                        byte[] fileBytes = File.ReadAllBytes(filePath);
                        string mime = GetMimeType(filePath);
                        SendResponse(stream, 200, "OK", mime, fileBytes);
                    }
                    else
                    {
                        // Fallback to index.html for SPA client routes
                        string fallback = Path.Combine(_baseDir, "index.html");
                        if (!Path.HasExtension(cleanPath) && File.Exists(fallback))
                        {
                            byte[] fileBytes = File.ReadAllBytes(fallback);
                            SendResponse(stream, 200, "OK", "text/html; charset=utf-8", fileBytes);
                        }
                        else
                        {
                            byte[] notFound = Encoding.UTF8.GetBytes("<html><body><h1>404 File Not Found</h1><p>" + cleanPath + "</p></body></html>");
                            SendResponse(stream, 404, "Not Found", "text/html; charset=utf-8", notFound);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Client disconnected or read timeout
            }
            finally
            {
                try { client.Close(); } catch { }
            }
        }

        private static void HandleApi(NetworkStream stream, string method, string path, string body)
        {
            string json = "{}";

            if (path == "/api/status")
            {
                json = "{\"status\":\"ok\",\"platform\":\"Legal Insight\",\"version\":\"1.0.0\",\"server\":\"Visual Studio C# .NET\"}";
            }
            else if (path == "/api/consultations")
            {
                if (method == "POST")
                {
                    if (!string.IsNullOrEmpty(body))
                    {
                        _consultations.Add(body);
                    }
                    json = "{\"status\":\"success\",\"message\":\"Konsultasi berhasil disimpan\",\"data\":" + (string.IsNullOrEmpty(body) ? "{}" : body) + "}";
                }
                else
                {
                    json = "[" + string.Join(",", _consultations.ToArray()) + "]";
                }
            }
            else if (path == "/api/documents")
            {
                if (method == "POST")
                {
                    if (!string.IsNullOrEmpty(body))
                    {
                        _documents.Add(body);
                    }
                    json = "{\"status\":\"success\",\"message\":\"Permintaan dokumen hukum berhasil diterima\",\"data\":" + (string.IsNullOrEmpty(body) ? "{}" : body) + "}";
                }
                else
                {
                    json = "[" + string.Join(",", _documents.ToArray()) + "]";
                }
            }
            else
            {
                json = "{\"status\":\"ok\",\"endpoint\":\"" + path + "\"}";
            }

            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            SendResponse(stream, 200, "OK", "application/json; charset=utf-8", jsonBytes);
        }

        private static void SendResponse(NetworkStream stream, int statusCode, string statusMsg, string contentType, byte[] content)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("HTTP/1.1 {0} {1}\r\n", statusCode, statusMsg);
            sb.AppendFormat("Content-Type: {0}\r\n", contentType);
            sb.AppendFormat("Content-Length: {0}\r\n", content.Length);
            sb.Append("Access-Control-Allow-Origin: *\r\n");
            sb.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS, PUT, DELETE\r\n");
            sb.Append("Access-Control-Allow-Headers: Content-Type, Authorization, X-Requested-With\r\n");
            sb.Append("Connection: close\r\n");
            sb.Append("\r\n");

            byte[] headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
            stream.Write(headerBytes, 0, headerBytes.Length);
            if (content.Length > 0)
            {
                stream.Write(content, 0, content.Length);
            }
            stream.Flush();
        }

        private static string GetMimeType(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            switch (ext)
            {
                case ".html":
                case ".htm": return "text/html; charset=utf-8";
                case ".css": return "text/css; charset=utf-8";
                case ".js": return "application/javascript; charset=utf-8";
                case ".json": return "application/json; charset=utf-8";
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".svg": return "image/svg+xml";
                case ".ico": return "image/x-icon";
                case ".pdf": return "application/pdf";
                case ".woff": return "font/woff";
                case ".woff2": return "font/woff2";
                default: return "application/octet-stream";
            }
        }
    }
}
