using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace QuizVision__Alpha_
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient = new HttpClient();

        // ==========================================
        // WINDOWS API
        // ==========================================

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(
            EnumWindowsProc lpEnumFunc,
            IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(
            IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(
            IntPtr hWnd,
            StringBuilder lpString,
            int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(
            IntPtr hWnd,
            out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(
            IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(
            IntPtr hWnd,
            IntPtr hDC);

        // ==========================================
        // GDI
        // ==========================================

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(
            IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(
            IntPtr hDC,
            int width,
            int height);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(
            IntPtr hDC,
            IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(
            IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(
            IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(
            IntPtr hdcDest,
            int nXDest,
            int nYDest,
            int nWidth,
            int nHeight,
            IntPtr hdcSrc,
            int nXSrc,
            int nYSrc,
            uint dwRop);

        private const uint SRCCOPY = 0x00CC0020;

        private delegate bool EnumWindowsProc(
            IntPtr hWnd,
            IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // ==========================================
        // CONSTRUCTOR
        // ==========================================

        public MainWindow()
        {
            InitializeComponent();
            RefreshWindows();
        }

        // ==========================================
        // REFRESH WINDOWS
        // ==========================================

        private void RefreshButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            RefreshWindows();
        }

        private void RefreshWindows()
        {
            WindowList.Items.Clear();

            // Get QuizVision's own window handle
            IntPtr ownHandle =
                new WindowInteropHelper(this).Handle;

            EnumWindows((hWnd, lParam) =>
            {
                // Don't show QuizVision itself
                if (hWnd == ownHandle)
                    return true;

                if (!IsWindowVisible(hWnd))
                    return true;

                StringBuilder title =
                    new StringBuilder(256);

                GetWindowText(
                    hWnd,
                    title,
                    title.Capacity);

                string windowTitle =
                    title.ToString();

                if (!string.IsNullOrWhiteSpace(windowTitle))
                {
                    WindowList.Items.Add(
                        new WindowInfo
                        {
                            Handle = hWnd,
                            Title = windowTitle
                        });
                }

                return true;

            }, IntPtr.Zero);

            if (WindowList.Items.Count > 0)
                WindowList.SelectedIndex = 0;
        }

        // ==========================================
        // SCAN
        // ==========================================

        private async void ScanButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (WindowList.SelectedItem is not WindowInfo selectedWindow)
            {
                ScanStatusText.Text = "NO WINDOW";

                ScanStatusText.Foreground =
                    System.Windows.Media.Brushes.Red;

                PreviewWindowText.Text =
                    "Please select a window first.";

                PreviewHandleText.Text =
                    "Handle: —";

                return;
            }

            ScanStatusText.Text =
                "CAPTURING...";

            ScanStatusText.Foreground =
                System.Windows.Media.Brushes.Orange;

            PreviewWindowText.Text =
                selectedWindow.Title;

            PreviewHandleText.Text =
                $"Handle: {selectedWindow.Handle}";

            BitmapSource? screenshot = null;

            // ==========================================
            // HIDE QUIZVISION TEMPORARILY
            // ==========================================

            try
            {
                // Hide our own app so it doesn't cover
                // the window we're trying to capture.
                Hide();

                // Give Windows a moment to redraw Chrome
                // / the selected window underneath.
                await Task.Delay(120);

                screenshot =
                    CaptureVisibleWindow(
                        selectedWindow.Handle);
            }
            finally
            {
                // ALWAYS bring QuizVision back,
                // even if capture fails.
                Show();

                Activate();
            }

            // ==========================================
            // CAPTURE FAILED
            // ==========================================

            if (screenshot == null)
            {
                PreviewPlaceholder.Visibility =
                    Visibility.Visible;

                ScanStatusText.Text =
                    "CAPTURE FAILED";

                ScanStatusText.Foreground =
                    System.Windows.Media.Brushes.Red;

                AnswerText.Text =
                    "Capture failed";

                ExplanationText.Text =
                    "Could not capture the selected window.";

                return;
            }

            // ==========================================
            // SHOW SCREENSHOT
            // ==========================================

            PreviewImage.Source =
                screenshot;

            PreviewPlaceholder.Visibility =
                Visibility.Collapsed;

            // ==========================================
            // SEND TO AI
            // ==========================================

            ScanStatusText.Text =
                "ANALYZING...";

            ScanStatusText.Foreground =
                System.Windows.Media.Brushes.Orange;

            AnswerText.Text =
                "Analyzing...";

            ExplanationText.Text =
                "Gemini is reading the screenshot.";

            await AnalyzeScreenshotWithGemini(
                screenshot);
        }

        // ==========================================
        // GEMINI
        // ==========================================

        private async Task AnalyzeScreenshotWithGemini(
            BitmapSource screenshot)
        {
            try
            {
                string? apiKey =
                    Environment.GetEnvironmentVariable(
                        "GEMINI_API_KEY",
                        EnvironmentVariableTarget.User);

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new Exception(
                        "GEMINI_API_KEY was not found.");
                }

                byte[] imageBytes =
                    BitmapSourceToPngBytes(
                        screenshot);

                string base64Image =
                    Convert.ToBase64String(
                        imageBytes);

                string url =
                    "https://generativelanguage.googleapis.com/v1beta/" +
                    "models/gemini-3.5-flash-lite:generateContent";

                string prompt =
                    """
                    You are QuizVision, an educational visual assistant.

                    Carefully inspect the screenshot.

                    Determine whether an actual question is visible.

                    If an MCQ is visible:
                    - Extract the complete question.
                    - Extract all visible options in order.
                    - Determine the best answer.
                    - Return the answer with its option letter if possible.
                    - Give a short explanation.

                    If there is no question because the page is loading,
                    blank, an error page, login page, or otherwise does not
                    contain a question, use NO_QUESTION.

                    NEVER invent text or options that are not visible.

                    Return ONLY valid JSON.
                    """;

                var responseSchema = new
                {
                    type = "OBJECT",

                    properties = new
                    {
                        type = new
                        {
                            type = "STRING",
                            description =
                                "MCQ or NO_QUESTION."
                        },

                        question = new
                        {
                            type = "STRING",
                            description =
                                "The visible question."
                        },

                        options = new
                        {
                            type = "ARRAY",

                            items = new
                            {
                                type = "STRING"
                            },

                            description =
                                "All visible answer choices."
                        },

                        answer = new
                        {
                            type = "STRING",
                            description =
                                "The best answer including its option letter when possible."
                        },

                        explanation = new
                        {
                            type = "STRING",
                            description =
                                "Short explanation for the answer."
                        }
                    },

                    required = new[]
                    {
                        "type",
                        "question",
                        "options",
                        "answer",
                        "explanation"
                    }
                };

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new
                                {
                                    text = prompt
                                },

                                new
                                {
                                    inlineData = new
                                    {
                                        mimeType =
                                            "image/png",

                                        data =
                                            base64Image
                                    }
                                }
                            }
                        }
                    },

                    generationConfig = new
                    {
                        responseMimeType =
                            "application/json",

                        responseSchema
                    }
                };

                string json =
                    JsonSerializer.Serialize(
                        requestBody);

                using var request =
                    new HttpRequestMessage(
                        HttpMethod.Post,
                        url);

                request.Headers.Add(
                    "x-goog-api-key",
                    apiKey);

                request.Content =
                    new StringContent(
                        json,
                        Encoding.UTF8,
                        "application/json");

                using HttpResponseMessage response =
                    await _httpClient.SendAsync(
                        request);

                string responseText =
                    await response.Content
                        .ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(
                        $"Gemini API returned HTTP " +
                        $"{(int)response.StatusCode}.\n\n" +
                        responseText);
                }

                using JsonDocument document =
                    JsonDocument.Parse(
                        responseText);

                string resultJson =
                    document.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString()
                        ?? throw new Exception(
                            "Gemini returned no text.");

                QuizAnalysis? result =
                    JsonSerializer.Deserialize<QuizAnalysis>(
                        resultJson,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                if (result == null)
                {
                    throw new Exception(
                        "Could not parse Gemini result.");
                }

                ScanStatusText.Text =
                    "AI COMPLETE";

                ScanStatusText.Foreground =
                    System.Windows.Media.Brushes.Green;

                if (!result.Type.Equals(
                    "MCQ",
                    StringComparison.OrdinalIgnoreCase))
                {
                    AnswerText.Text =
                        "No question detected";

                    ExplanationText.Text =
                        string.IsNullOrWhiteSpace(
                            result.Explanation)
                            ? "No question is currently visible."
                            : result.Explanation;

                    return;
                }

                string answer =
                    result.Answer?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(answer))
                    answer = "Answer not detected";

                AnswerText.Text =
                    $"ANSWER: {answer}";

                StringBuilder details =
                    new StringBuilder();

                if (!string.IsNullOrWhiteSpace(
                    result.Question))
                {
                    details.AppendLine(
                        "QUESTION:");

                    details.AppendLine(
                        result.Question);

                    details.AppendLine();
                }

                if (result.Options != null &&
                    result.Options.Length > 0)
                {
                    details.AppendLine(
                        "OPTIONS:");

                    for (int i = 0;
                         i < result.Options.Length;
                         i++)
                    {
                        char letter =
                            (char)('A' + i);

                        details.AppendLine(
                            $"{letter}. {result.Options[i]}");
                    }

                    details.AppendLine();
                }

                details.AppendLine(
                    "EXPLANATION:");

                details.AppendLine(
                    string.IsNullOrWhiteSpace(
                        result.Explanation)
                        ? "No explanation returned."
                        : result.Explanation);

                ExplanationText.Text =
                    details.ToString();
            }
            catch (Exception ex)
            {
                ScanStatusText.Text =
                    "AI ERROR";

                ScanStatusText.Foreground =
                    System.Windows.Media.Brushes.Red;

                AnswerText.Text =
                    "Gemini vision failed";

                ExplanationText.Text =
                    ex.Message;
            }
        }

        // ==========================================
        // BITMAP → PNG
        // ==========================================

        private byte[] BitmapSourceToPngBytes(
            BitmapSource bitmap)
        {
            using MemoryStream stream =
                new MemoryStream();

            PngBitmapEncoder encoder =
                new PngBitmapEncoder();

            encoder.Frames.Add(
                BitmapFrame.Create(bitmap));

            encoder.Save(stream);

            return stream.ToArray();
        }

        // ==========================================
        // SCREENSHOT CAPTURE
        // ==========================================

        private BitmapSource? CaptureVisibleWindow(
            IntPtr hWnd)
        {
            if (!GetWindowRect(
                    hWnd,
                    out RECT rect))
            {
                return null;
            }

            int width =
                rect.Right - rect.Left;

            int height =
                rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
                return null;

            IntPtr screenDC =
                GetDC(IntPtr.Zero);

            if (screenDC == IntPtr.Zero)
                return null;

            IntPtr memoryDC =
                CreateCompatibleDC(
                    screenDC);

            if (memoryDC == IntPtr.Zero)
            {
                ReleaseDC(
                    IntPtr.Zero,
                    screenDC);

                return null;
            }

            IntPtr bitmap =
                CreateCompatibleBitmap(
                    screenDC,
                    width,
                    height);

            if (bitmap == IntPtr.Zero)
            {
                DeleteDC(memoryDC);

                ReleaseDC(
                    IntPtr.Zero,
                    screenDC);

                return null;
            }

            IntPtr oldBitmap =
                SelectObject(
                    memoryDC,
                    bitmap);

            bool success =
                BitBlt(
                    memoryDC,
                    0,
                    0,
                    width,
                    height,
                    screenDC,
                    rect.Left,
                    rect.Top,
                    SRCCOPY);

            SelectObject(
                memoryDC,
                oldBitmap);

            DeleteDC(memoryDC);

            ReleaseDC(
                IntPtr.Zero,
                screenDC);

            if (!success)
            {
                DeleteObject(bitmap);
                return null;
            }

            BitmapSource image =
                Imaging.CreateBitmapSourceFromHBitmap(
                    bitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

            DeleteObject(bitmap);

            image.Freeze();

            return image;
        }
    }

    // ==========================================
    // AI RESULT
    // ==========================================

    public class QuizAnalysis
    {
        public string Type { get; set; } = "";

        public string Question { get; set; } = "";

        public string[] Options { get; set; } =
            Array.Empty<string>();

        public string Answer { get; set; } = "";

        public string Explanation { get; set; } = "";
    }

    // ==========================================
    // WINDOW INFO
    // ==========================================

    public class WindowInfo
    {
        public IntPtr Handle { get; set; }

        public string Title { get; set; } = "";

        public override string ToString()
        {
            return Title;
        }
    }
}