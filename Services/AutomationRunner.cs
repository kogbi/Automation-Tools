using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using WormholeAutomationUI.Models;

namespace WormholeAutomationUI.Services;

public class AutomationRunner
{
    private static readonly Regex PinRegex = new("(\\d{6})", RegexOptions.Compiled);

    public Task RunAsync(AutomationConfig config, CancellationToken token, Action<string>? log = null)
    {
        return Task.Run(() => RunInternal(config, token, log), token);
    }

    private void RunInternal(AutomationConfig config, CancellationToken token, Action<string>? log)
    {
        var handle = config.WindowHandle != 0 ? new IntPtr(config.WindowHandle) : FindWindowByTitle(config.WindowTitle);
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Window not found: {config.WindowTitle}");
        }

        SetForegroundWindow(handle);
        var index = 0;
        while (index < config.Steps.Count)
        {
            token.ThrowIfCancellationRequested();
            var step = config.Steps[index];
            log?.Invoke($"Step {index + 1}: {step.Name} ({step.Action})");

            var success = ExecuteStep(step, config, handle, token, log);
            var valid = !step.UseValidation || ValidateStep(step, config, handle, token, log);

            if (success && valid)
            {
                index++;
                if (index >= config.Steps.Count && config.LoopEnabled)
                {
                    index = 0;
                }
                SleepWithCancel(1500, token);
                continue;
            }

            ApplyFailAction(step, config, ref index, log);
            if (index >= config.Steps.Count && config.LoopEnabled)
            {
                index = 0;
            }
            SleepWithCancel(1500, token);
        }
    }

    private bool ExecuteStep(StepConfig step, AutomationConfig config, IntPtr handle, CancellationToken token, Action<string>? log)
    {
        switch (step.Action)
        {
            case StepAction.ClickImage:
                return ClickImage(step, config, handle, token, log);
            case StepAction.WaitImage:
                return WaitForImage(step, config, handle, token, log);
            case StepAction.SwipeWindow:
                SwipeWindow(step, handle);
                return true;
            case StepAction.InputPin:
                return InputPin(step, config, handle, token, log);
            case StepAction.SendSignal:
                SendSignal(step.Text, config);
                return true;
            default:
                return true;
        }
    }

    private bool ValidateStep(StepConfig step, AutomationConfig config, IntPtr handle, CancellationToken token, Action<string>? log)
    {
        if (step.Action is StepAction.ClickImage or StepAction.WaitImage)
        {
            return WaitForImage(step, config, handle, token, log);
        }

        return true;
    }

    private void ApplyFailAction(StepConfig step, AutomationConfig config, ref int index, Action<string>? log)
    {
        log?.Invoke($"Step failed: {step.Name}");
        switch (step.FailAction)
        {
            case FailAction.JumpToStep:
                var target = step.FailTargetStepIndex - 1;
                index = target >= 0 && target < config.Steps.Count ? target : index + 1;
                return;
            case FailAction.SendSignal:
                SendSignal(step.FailMessage, config);
                index++;
                return;
            default:
                index++;
                return;
        }
    }

    private bool ClickImage(StepConfig step, AutomationConfig config, IntPtr handle, CancellationToken token, Action<string>? log)
    {
        var match = FindTemplate(step, config, handle, step.TimeoutSec, token, log);
        if (match == null)
        {
            return false;
        }

        SetForegroundWindow(handle);
        Click(match.Value.X, match.Value.Y);
        return true;
    }

    private bool WaitForImage(StepConfig step, AutomationConfig config, IntPtr handle, CancellationToken token, Action<string>? log)
    {
        var match = FindTemplate(step, config, handle, step.TimeoutSec, token, log);
        return match != null;
    }

    private bool InputPin(StepConfig step, AutomationConfig config, IntPtr handle, CancellationToken token, Action<string>? log)
    {
        var pin = ReadPin(config, token);
        if (string.IsNullOrWhiteSpace(pin))
        {
            log?.Invoke("PIN not received.");
            return false;
        }

        SetForegroundWindow(handle);
        System.Windows.Forms.SendKeys.SendWait(pin);
        return true;
    }

    private void SwipeWindow(StepConfig step, IntPtr handle)
    {
        var rect = GetWindowRect(handle);
        var startX = rect.Left + step.StartX;
        var startY = rect.Top + step.StartY;
        var endX = rect.Left + step.EndX;
        var endY = rect.Top + step.EndY;

        SetForegroundWindow(handle);
        SetCursorPos(startX, startY);
        mouse_event(MouseEventFlags.LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        SmoothMove(startX, startY, endX, endY, 15, 15);
        mouse_event(MouseEventFlags.LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }

    private static void SmoothMove(int startX, int startY, int endX, int endY, int steps, int stepDelayMs)
    {
        steps = Math.Max(1, steps);
        for (var i = 1; i <= steps; i++)
        {
            var x = startX + (endX - startX) * i / steps;
            var y = startY + (endY - startY) * i / steps;
            SetCursorPos(x, y);
            Thread.Sleep(stepDelayMs);
        }
    }

    private System.Drawing.Point? FindTemplate(StepConfig step, AutomationConfig config, IntPtr handle, int timeoutSec, CancellationToken token, Action<string>? log)
    {
        var templateBytes = GetTemplateBytes(step);
        if (templateBytes == null)
        {
            log?.Invoke("Template missing.");
            return null;
        }

        using var templateMat = Cv2.ImDecode(templateBytes, ImreadModes.Grayscale);
        if (templateMat.Empty())
        {
            log?.Invoke("Template decode failed.");
            return null;
        }

        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalSeconds < timeoutSec)
        {
            token.ThrowIfCancellationRequested();
            using var screenshot = CaptureWindow(handle);
            using var sourceMatColor = BitmapConverter.ToMat(screenshot);
            using var sourceMat = new Mat();
            if (sourceMatColor.Channels() == 4)
            {
                Cv2.CvtColor(sourceMatColor, sourceMat, ColorConversionCodes.BGRA2GRAY);
            }
            else if (sourceMatColor.Channels() == 3)
            {
                Cv2.CvtColor(sourceMatColor, sourceMat, ColorConversionCodes.BGR2GRAY);
            }
            else
            {
                sourceMatColor.CopyTo(sourceMat);
            }

            if (sourceMat.Depth() != MatType.CV_8U)
            {
                sourceMat.ConvertTo(sourceMat, MatType.CV_8U);
            }

            var resultCols = sourceMat.Cols - templateMat.Cols + 1;
            var resultRows = sourceMat.Rows - templateMat.Rows + 1;
            if (resultCols <= 0 || resultRows <= 0)
            {
                return null;
            }

            using var result = new Mat(resultRows, resultCols, MatType.CV_32FC1);
            try
            {
                Cv2.MatchTemplate(sourceMat, templateMat, result, TemplateMatchModes.CCoeffNormed);
            }
            catch (OpenCvSharpException ex)
            {
                log?.Invoke($"MatchTemplate failed: {ex.Message}");
                return null;
            }
            Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out OpenCvSharp.Point maxLoc);

            if (maxVal >= config.MatchConfidence)
            {
                var rect = GetWindowRect(handle);
                var centerX = rect.Left + maxLoc.X + (templateMat.Width / 2);
                var centerY = rect.Top + maxLoc.Y + (templateMat.Height / 2);
                return new System.Drawing.Point(centerX, centerY);
            }

            Thread.Sleep(300);
        }

        return null;
    }

    private byte[]? GetTemplateBytes(StepConfig step)
    {
        if (step.TemplateImageBytes != null && step.TemplateImageBytes.Length > 0)
        {
            return step.TemplateImageBytes;
        }

        if (!string.IsNullOrWhiteSpace(step.TemplateFile) && File.Exists(step.TemplateFile))
        {
            return File.ReadAllBytes(step.TemplateFile);
        }

        return null;
    }

    private Bitmap CaptureWindow(IntPtr handle)
    {
        var rect = GetWindowRect(handle);
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new System.Drawing.Size(width, height));
        return bitmap;
    }

    private string ReadPin(AutomationConfig config, CancellationToken token)
    {
        try
        {
            using var client = new TcpClient();
            client.Connect(config.PinHost, config.PinPort);
            using var stream = client.GetStream();
            stream.ReadTimeout = 1000;
            stream.WriteTimeout = 3000;
            var request = Encoding.ASCII.GetBytes("GET_PIN");
            stream.Write(request, 0, request.Length);
            stream.Flush();
            var start = DateTime.UtcNow;
            var buffer = new byte[1024];
            var sb = new StringBuilder();
            while ((DateTime.UtcNow - start).TotalSeconds < 10)
            {
                token.ThrowIfCancellationRequested();
                var bytesRead = 0;
                try
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                }
                catch (IOException)
                {
                    bytesRead = 0;
                }

                if (bytesRead > 0)
                {
                    sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
                }

                Thread.Sleep(100);
                var match = PinRegex.Match(sb.ToString());
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return string.Empty;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void SleepWithCancel(int milliseconds, CancellationToken token)
    {
        var elapsed = 0;
        while (elapsed < milliseconds)
        {
            token.ThrowIfCancellationRequested();
            Thread.Sleep(50);
            elapsed += 50;
        }
    }

    private void SendSignal(string message, AutomationConfig config)
    {
        var payload = string.IsNullOrWhiteSpace(message) ? config.DoneMessage : message;
        try
        {
            using var client = new TcpClient();
            client.Connect(config.PinHost, config.PinPort);
            using var stream = client.GetStream();
            var bytes = Encoding.ASCII.GetBytes(payload);
            stream.Write(bytes, 0, bytes.Length);
        }
        catch
        {
            // Ignore send errors for now.
        }
    }

    private static IntPtr FindWindowByTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return IntPtr.Zero;
        }

        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, lParam) =>
        {
            var sb = new StringBuilder(512);
            GetWindowText(hWnd, sb, sb.Capacity);
            var text = sb.ToString();
            if (!string.IsNullOrWhiteSpace(text) && text.Contains(title, StringComparison.OrdinalIgnoreCase))
            {
                found = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        return found;
    }

    private static Rect GetWindowRect(IntPtr handle)
    {
        GetWindowRect(handle, out var rect);
        return rect;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(MouseEventFlags dwFlags, int dx, int dy, int dwData, UIntPtr dwExtraInfo);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [Flags]
    private enum MouseEventFlags
    {
        LEFTDOWN = 0x0002,
        LEFTUP = 0x0004
    }

    private static void Click(int x, int y)
    {
        SetCursorPos(x, y);
        mouse_event(MouseEventFlags.LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        mouse_event(MouseEventFlags.LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }
}
