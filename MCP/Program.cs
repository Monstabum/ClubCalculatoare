using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Transport;

namespace MCPDesktopAutomation;

/// <summary>
/// MCP-сервер для автоматизации тестирования приложения AppClubCalculatoare.
/// Использует Windows API для управления мышкой и клавиатурой.
/// </summary>
class Program
{
    // ── Windows API для управления мышкой ─────────────────────────
    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int nIndex);

    // ── Константы для мыши ────────────────────────────────────────
    const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    const uint MOUSEEVENTF_LEFTUP = 0x0004;
    const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    const uint MOUSEEVENTF_WHEEL = 0x0800;
    const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

    // ── Константы для клавиатуры ──────────────────────────────────
    const uint KEYEVENTF_KEYUP = 0x0002;
    const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    // ── Структуры ─────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // ── Основной код ──────────────────────────────────────────────
    static async Task Main(string[] args)
    {
        Console.WriteLine("MCP Desktop Automation Server запущен...");
        Console.WriteLine("Ожидание подключения клиента...");

        // Создаем MCP-сервер
        var server = new McpServer(
            name: "desktop-automation",
            version: "1.0.0",
            tools: new[]
            {
                CreateTool("click", "Клик левой кнопкой мыши по указанным координатам",
                    new { x = 0, y = 0 },
                    (args) =>
                    {
                        int x = args.GetProperty("x").GetInt32();
                        int y = args.GetProperty("y").GetInt32();
                        MouseClick(x, y, MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP);
                        return Task.FromResult($"Клик выполнен по координатам ({x}, {y})");
                    }),

                CreateTool("double_click", "Двойной клик левой кнопкой мыши",
                    new { x = 0, y = 0 },
                    (args) =>
                    {
                        int x = args.GetProperty("x").GetInt32();
                        int y = args.GetProperty("y").GetInt32();
                        MouseDoubleClick(x, y);
                        return Task.FromResult($"Двойной клик выполнен по координатам ({x}, {y})");
                    }),

                CreateTool("right_click", "Клик правой кнопкой мыши",
                    new { x = 0, y = 0 },
                    (args) =>
                    {
                        int x = args.GetProperty("x").GetInt32();
                        int y = args.GetProperty("y").GetInt32();
                        MouseClick(x, y, MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP);
                        return Task.FromResult($"Правый клик выполнен по координатам ({x}, {y})");
                    }),

                CreateTool("move_mouse", "Перемещение мыши к указанным координатам",
                    new { x = 0, y = 0 },
                    (args) =>
                    {
                        int x = args.GetProperty("x").GetInt32();
                        int y = args.GetProperty("y").GetInt32();
                        SetCursorPos(x, y);
                        return Task.FromResult($"Мышь перемещена к ({x}, {y})");
                    }),

                CreateTool("type_text", "Ввод текста с клавиатуры",
                    new { text = "", interval = 0.05 },
                    (args) =>
                    {
                        string text = args.GetProperty("text").GetString();
                        double interval = args.GetProperty("interval").GetDouble();
                        SendKeys.SendWait(text);
                        return Task.FromResult($"Текст введен: '{text}'");
                    }),

                CreateTool("hotkey", "Нажатие комбинации клавиш",
                    new { keys = Array.Empty<string>() },
                    (args) =>
                    {
                        var keys = args.GetProperty("keys").EnumerateArray().Select(e => e.GetString()).ToArray();
                        SendHotkey(keys);
                        return Task.FromResult($"Комбинация клавиш нажата: {string.Join(" + ", keys)}");
                    }),

                CreateTool("press_key", "Нажатие одной клавиши",
                    new { key = "", presses = 1 },
                    (args) =>
                    {
                        string key = args.GetProperty("key").GetString();
                        int presses = args.GetProperty("presses").GetInt32();
                        for (int i = 0; i < presses; i++)
                        {
                            SendKeys.SendWait(key);
                            Thread.Sleep(50);
                        }
                        return Task.FromResult($"Клавиша '{key}' нажата {presses} раз(а)");
                    }),

                CreateTool("screenshot", "Сделать скриншот экрана",
                    new { savePath = "" },
                    (args) =>
                    {
                        string savePath = args.GetProperty("savePath").GetString();
                        if (string.IsNullOrEmpty(savePath))
                            savePath = Path.Combine(Path.GetTempPath(), $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                        using var bitmap = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
                        using var graphics = Graphics.FromImage(bitmap);
                        graphics.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
                        bitmap.Save(savePath, ImageFormat.Png);

                        return Task.FromResult($"Скриншот сохранен: {savePath}");
                    }),

                CreateTool("scroll", "Прокрутка колеса мыши",
                    new { clicks = 0, x = 0, y = 0 },
                    (args) =>
                    {
                        int clicks = args.GetProperty("clicks").GetInt32();
                        int x = args.GetProperty("x").GetInt32();
                        int y = args.GetProperty("y").GetInt32();

                        if (x != 0 || y != 0)
                            SetCursorPos(x, y);

                        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)(clicks * 120), UIntPtr.Zero);

                        string direction = clicks > 0 ? "вверх" : "вниз";
                        return Task.FromResult($"Прокрутка {direction} на {Math.Abs(clicks)} щелчков");
                    }),

                CreateTool("wait", "Ожидание указанного количества секунд",
                    new { seconds = 0.0 },
                    (args) =>
                    {
                        double seconds = args.GetProperty("seconds").GetDouble();
                        Thread.Sleep((int)(seconds * 1000));
                        return Task.FromResult($"Ожидание {seconds} секунд завершено");
                    }),

                CreateTool("get_mouse_position", "Получить текущую позицию курсора мыши",
                    new { },
                    (args) =>
                    {
                        GetCursorPos(out POINT pos);
                        return Task.FromResult($"Текущая позиция мыши: ({pos.X}, {pos.Y})");
                    }),

                CreateTool("get_screen_size", "Получить размер экрана",
                    new { },
                    (args) =>
                    {
                        int width = Screen.PrimaryScreen.Bounds.Width;
                        int height = Screen.PrimaryScreen.Bounds.Height;
                        return Task.FromResult($"Размер экрана: {width}x{height}");
                    }),

                CreateTool("run_test_scenario", "Запустить тестовый сценарий",
                    new { scenario = "" },
                    (args) =>
                    {
                        string scenario = args.GetProperty("scenario").GetString();
                        return RunTestScenario(scenario);
                    }),
            }
        );

        // Запускаем сервер
        await server.RunAsync();
    }

    // ── Вспомогательные методы ────────────────────────────────────

    static Tool CreateTool(string name, string description, object inputSchema, Func<JsonElement, Task<string>> handler)
    {
        return new Tool
        {
            Name = name,
            Description = description,
            InputSchema = System.Text.Json.JsonSerializer.SerializeToElement(inputSchema),
            Handler = async (request) =>
            {
                try
                {
                    var result = await handler(request.Arguments);
                    return new ToolResult
                    {
                        Content = new[] { new TextContent { Text = result } }
                    };
                }
                catch (Exception ex)
                {
                    return new ToolResult
                    {
                        Content = new[] { new TextContent { Text = $"Ошибка: {ex.Message}" } },
                        IsError = true
                    };
                }
            }
        };
    }

    static void MouseClick(int x, int y, uint flags)
    {
        SetCursorPos(x, y);
        Thread.Sleep(50);
        mouse_event(flags, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(50);
    }

    static void MouseDoubleClick(int x, int y)
    {
        SetCursorPos(x, y);
        Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(50);
    }

    static void SendHotkey(string[] keys)
    {
        // Нажимаем все клавиши
        foreach (var key in keys)
        {
            byte vk = GetVirtualKey(key);
            keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
        }

        // Отпускаем в обратном порядке
        foreach (var key in keys.Reverse())
        {
            byte vk = GetVirtualKey(key);
            keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }

    static byte GetVirtualKey(string key)
    {
        return key.ToLower() switch
        {
            "ctrl" => 0x11,
            "control" => 0x11,
            "shift" => 0x10,
            "alt" => 0x12,
            "win" => 0x5B,
            "enter" => 0x0D,
            "return" => 0x0D,
            "tab" => 0x09,
            "escape" => 0x1B,
            "esc" => 0x1B,
            "space" => 0x20,
            "backspace" => 0x08,
            "delete" => 0x2E,
            "del" => 0x2E,
            "up" => 0x26,
            "down" => 0x28,
            "left" => 0x25,
            "right" => 0x27,
            "home" => 0x24,
            "end" => 0x23,
            "pageup" => 0x21,
            "pagedown" => 0x22,
            "f1" => 0x70,
            "f2" => 0x71,
            "f3" => 0x72,
            "f4" => 0x73,
            "f5" => 0x74,
            "f6" => 0x75,
            "f7" => 0x76,
            "f8" => 0x77,
            "f9" => 0x78,
            "f10" => 0x79,
            "f11" => 0x7A,
            "f12" => 0x7B,
            _ => key.Length == 1 ? Convert.ToByte(key.ToUpper()[0]) : 0
        };
    }

    static Task<string> RunTestScenario(string scenario)
    {
        return scenario.ToLower() switch
        {
            "login" => Task.FromResult("Тестовый сценарий: Вход в систему. Используйте инструменты click, type_text для автоматизации."),
            "dashboard" => Task.FromResult("Тестовый сценарий: Проверка дашборда. Используйте screenshot для проверки."),
            "crud" => Task.FromResult("Тестовый сценарий: CRUD-операции. Используйте click, type_text для автоматизации."),
            "reports" => Task.FromResult("Тестовый сценарий: Отчеты. Используйте click, type_text для автоматизации."),
            _ => Task.FromResult($"Неизвестный сценарий: {scenario}")
        };
    }
}
