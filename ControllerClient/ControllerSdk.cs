using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ControllerClient
{
    public sealed class Controller : IDisposable
    {
        private ClientWebSocket ws;
        private CancellationTokenSource cts;

        private readonly ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();

        private readonly MotionProcessor motion;
        public readonly ActionState Actions = new ActionState();

        private long lastTimestamp;
        private long lastReceiveTime;

        // ---------- Events ----------
        public event Action<MotionIntent> OnMotion;
        public event Action<String> OnCommand;
        public event Action<bool> OnConnectionStateChanged;
        public event Action<ScreenshotResult> OnScreenshot;
        public event Action<GpxStartedResult> OnGpxStarted;
        public event Action<GpxExportResult> OnGpxExported;

        public bool IsConnected => ws != null && ws.State == WebSocketState.Open;

        private MotionIntent lastIntent;
        private long lastMotionTime;
        private long _lastGpxLocationSendTime;

        public Controller(MotionSettings settings = null)
        {
            motion = new MotionProcessor(settings ?? new MotionSettings());
        }

        public void Connect(string url = "ws://localhost:8765/sensor")
        {
            ws = new ClientWebSocket();
            cts = new CancellationTokenSource();
            _ = Task.Run(() => ConnectLoop(url, cts.Token));
        }

        // ---------- Screenshot Capture API ----------

        /// <summary>
        /// Request a full-monitor screenshot from the PC server.
        /// The result is delivered asynchronously via the OnScreenshot event.
        /// </summary>
        public void CaptureScreen()
        {
            var json = JsonSerializer.Serialize(new
            {
                packetType = "captureScreen",
                timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = new { mode = "monitor" }
            });
            _ = SendAsync(json);
        }

        /// <summary>
        /// Request a window-only screenshot from the PC server.
        /// When windowMode is true, captures the foreground window (auto-detected).
        /// The result is delivered asynchronously via the OnScreenshot event.
        /// </summary>
        public void CaptureScreen(bool windowMode)
        {
            var json = JsonSerializer.Serialize(new
            {
                packetType = "captureScreen",
                timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = new { mode = windowMode ? "window" : "monitor" }
            });
            _ = SendAsync(json);
        }

        /// <summary>
        /// Request a window-only screenshot by window title from the PC server.
        /// The server searches for a visible window whose title contains the given text
        /// (case-insensitive partial match).
        /// The result is delivered asynchronously via the OnScreenshot event.
        /// </summary>
        public void CaptureScreen(string windowTitle)
        {
            var json = JsonSerializer.Serialize(new
            {
                packetType = "captureScreen",
                timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = new { mode = "window", title = windowTitle }
            });
            _ = SendAsync(json);
        }

        // ---------- Vibration API ----------

        /// <summary>
        /// Request a vibration on the controller.
        /// </summary>
        /// <param name="durationMs">The duration of the vibration in milliseconds (default is 200).</param>
        public void Vibrate(int durationMs = 200)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                packetType = "vibrate",
                timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = new { duration = durationMs }
            });
            _ = SendAsync(json);
        }

        // ---------- GPX Recording API ----------

        /// <summary>
        /// Start GPX recording with a random trail using the server's default start point.
        /// The trail advances based on the phone sensor's step cadence.
        /// </summary>
        public void StartGpx()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                packetType = "gpxStart",
                timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = new { }
            });
            _ = SendAsync(json);
        }

        /// <summary>
        /// Start GPX recording. When manualLocation is true, the game must send
        /// character positions via <see cref="UpdateGpxLocation"/> instead of
        /// relying on the random trail.
        /// Uses the server's default start point.
        /// </summary>
        public void StartGpx(bool manualLocation)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                packetType = "gpxStart",
                timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = new { manualLocation }
            });
            _ = SendAsync(json);
        }

        /// <summary>
        /// Start GPX recording with a random trail from the given start coordinates.
        /// The trail advances based on the phone sensor's step cadence.
        /// </summary>
        public void StartGpx(double lat, double lon)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                packetType = "gpxStart",
                timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = new { lat, lon }
            });
            _ = SendAsync(json);
        }

        /// <summary>
        /// Start GPX recording. When manualLocation is true, the game must send
        /// character positions via <see cref="UpdateGpxLocation"/> instead of
        /// relying on the random trail.
        /// </summary>
        public void StartGpx(double lat, double lon, bool manualLocation)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                packetType = "gpxStart",
                timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = new { lat, lon, manualLocation }
            });
            _ = SendAsync(json);
        }

        /// <summary>
        /// Send the game character's current location to the PC server for GPX recording.
        /// Only effective when GPX was started in manual-location mode.
        /// Throttled client-side to ~2Hz (calls within 500ms are dropped).
        /// </summary>
        public void UpdateGpxLocation(double lat, double lon)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_lastGpxLocationSendTime > 0 && (now - _lastGpxLocationSendTime) < 500)
                return;
            _lastGpxLocationSendTime = now;

            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                packetType = "gpxUpdateLocation",
                timeStamp = now,
                payload = new { lat, lon }
            });
            _ = SendAsync(json);
        }

        /// <summary>
        /// Export the current GPX session to a file on the PC server.
        /// The result is delivered asynchronously via the <see cref="OnGpxExported"/> event.
        /// </summary>
        public void ExportGpx()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                packetType = "gpxExport",
                timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                payload = new { }
            });
            _ = SendAsync(json);
        }

        private async Task SendAsync(string json)
        {
            if (ws == null || ws.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(json);
            try
            {
                await ws.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    cts?.Token ?? CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SendAsync error: {ex.Message}");
            }
        }

        private async Task ConnectLoop(string url, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var connected = false;
                try
                {
                    ws?.Dispose();
                    ws = new ClientWebSocket();
                    ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

                    await ws.ConnectAsync(new Uri(url), token);

                    connected = true;
                    Enqueue(() => OnConnectionStateChanged?.Invoke(true));

                    await ReceiveLoop(token); // exits when disconnected
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Connection lost. Retrying in 2 seconds... {ex.Message}");
                }
                finally
                {
                    if (connected)
                    {
                        Enqueue(() => OnConnectionStateChanged?.Invoke(false));
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    await Task.Delay(2000, token);
                }
            }
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            var buffer = new byte[2048];
            var sb = new StringBuilder();

            while (!token.IsCancellationRequested)
            {
                WebSocketReceiveResult result;

                do
                {
                    result = await ws.ReceiveAsync(buffer, token);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                lastReceiveTime = Stopwatch.GetTimestamp();
                HandleMessage(sb.ToString());
                sb.Clear();
            }
        }



        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private void HandleMessage(string json)
        {
            try
            {
                var typedPacket = JsonSerializer.Deserialize<TypedPacket>(json, jsonOptions);

                switch (typedPacket.Type)
                {
                    case "movement":
                        var movement = JsonSerializer.Deserialize<MovementPacket>(json, jsonOptions);
                        EmitMotion(
                            movement.X,
                            movement.Y,
                            movement.Z,
                            movement.Steps,
                            movement.StepsCadence,
                            movement.Timestamp
                        );

                        if (movement.Buttons != null)
                        {
                            Enqueue(() => Actions.Update(movement.Buttons, movement.Timestamp));
                        }
                        break;

                    case "command":
                        var command = JsonSerializer.Deserialize<CommandPacket>(json, jsonOptions);
                        if (command.Value == "controller_disconnected")
                            Enqueue(() => OnConnectionStateChanged?.Invoke(false));
                        else if (command.Value == "controller_connected")
                            Enqueue(() => OnConnectionStateChanged?.Invoke(true));
                        else
                            Enqueue(() => OnCommand?.Invoke(command.Value));
                        break;

                    case "screenshot":
                        var screenshot = System.Text.Json.JsonSerializer.Deserialize<ScreenshotPacket>(json, jsonOptions);
                        if (screenshot != null)
                        {
                            var result = new ScreenshotResult
                            {
                                FilePath = screenshot.Path,
                                Width = screenshot.Width,
                                Height = screenshot.Height
                            };
                            Enqueue(() => OnScreenshot?.Invoke(result));
                        }
                        break;

                    case "gpxStarted":
                        var gpxStarted = System.Text.Json.JsonSerializer.Deserialize<GpxStartedPacket>(json, jsonOptions);
                        if (gpxStarted != null)
                        {
                            var startedResult = new GpxStartedResult
                            {
                                Mode = gpxStarted.Mode,
                                Error = gpxStarted.Error
                            };
                            Enqueue(() => OnGpxStarted?.Invoke(startedResult));
                        }
                        break;

                    case "gpxExported":
                        var gpxExported = System.Text.Json.JsonSerializer.Deserialize<GpxExportedPacket>(json, jsonOptions);
                        if (gpxExported != null)
                        {
                            var gpxResult = new GpxExportResult
                            {
                                FilePath = gpxExported.Path,
                                DistanceKm = gpxExported.Distance,
                                Duration = gpxExported.Duration,
                                Error = gpxExported.Error
                            };
                            Enqueue(() => OnGpxExported?.Invoke(gpxResult));
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Invalid message: {ex}");
            }
        }


        private void EmitMotion(float ax, float ay, float az, int steps, float stepsCadence, long ts)
        {
            if (lastTimestamp == 0)
            {
                lastTimestamp = ts;
                return;
            }

            float dt;

            if (ts <= lastTimestamp)
            {
                dt = 1f / 60f;
            }
            else
            {
                dt = (ts - lastTimestamp) / 1000f;
            }

            lastTimestamp = ts;


            var intent = motion.Update(ax, ay, az, steps, stepsCadence, dt, ts);
            lastIntent = intent;
            lastMotionTime = Stopwatch.GetTimestamp();
            Enqueue(() => OnMotion?.Invoke(intent));
        }

        private void Enqueue(Action action)
        {
            mainThreadQueue.Enqueue(action);
        }


        public void Dispatch()
        {
            var now = Stopwatch.GetTimestamp();
            var elapsedMs = (now - lastMotionTime) * 1000 / Stopwatch.Frequency;

            if (elapsedMs > 250)
            {
                Enqueue(() => OnMotion?.Invoke(default));
                lastMotionTime = now;
            }

            Actions.ReleaseStaleButtons(
                Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000)
            );

            while (mainThreadQueue.TryDequeue(out var a))
                a();
        }


        public void Dispose()
        {
            cts?.Cancel();
            ws?.Dispose();
        }
    }

    public class TypedPacket
    {
        public string Type { get; set; }
    }
}