using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Project.InputAbstraction.Editor
{
    // Opt-in, local-only capture of the real OS/device + Editor path. QueueStateEvent
    // tests cannot reproduce Windows message pumping or native editor repaint stalls.
    internal static class MouseMotionDiagnostics
    {
        private const string Menu = "Tools/诊断/录制鼠标卡顿（10秒）";
        private readonly struct Frame
        {
            internal readonly double Milliseconds;
            internal readonly double WallMilliseconds;
            internal readonly int MouseEvents;
            internal readonly bool UnityFocused, GameFocused;
            internal Frame(double ms, double wallMs, int events, bool focused, bool gameFocused)
            {
                Milliseconds = ms; WallMilliseconds = wallMs; MouseEvents = events;
                UnityFocused = focused; GameFocused = gameFocused;
            }
        }
        private static readonly List<Frame> frames = new List<Frame>(4096);
        private static bool recording, profilerWasEnabled, editorWasProfiled;
        private static double startedAt, lastFrameAt;
        private static int lastFrame, mouseEvents;

        [MenuItem(Menu)]
        private static void Start()
        {
            if (!EditorApplication.isPlaying || recording) return;
            frames.Clear();
            mouseEvents = 0;
            lastFrame = Time.frameCount;
            startedAt = lastFrameAt = EditorApplication.timeSinceStartup;
            profilerWasEnabled = ProfilerDriver.enabled;
            editorWasProfiled = ProfilerDriver.profileEditor;
            if (!profilerWasEnabled) ProfilerDriver.ClearAllFrames();
            // Player-only profiling folds native/editor callbacks into an opaque
            // EditorLoop sample. Record the Editor too; restore the user's mode.
            ProfilerDriver.profileEditor = true;
            ProfilerDriver.enabled = true; // Never enables Deep Profile.
            recording = true;
            InputSystem.onEvent += OnInput;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            // Focus only the existing Game view after the user's explicit menu action.
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }

        [MenuItem(Menu, true)]
        private static bool CanStart() => EditorApplication.isPlaying && !recording;

        private static void OnInput(InputEventPtr input, InputDevice device)
        {
            if (device is Mouse && (input.type == StateEvent.Type || input.type == DeltaStateEvent.Type))
                mouseEvents++;
        }

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (lastFrame != Time.frameCount)
            {
                bool gameFocused = EditorWindow.focusedWindow != null &&
                    EditorWindow.focusedWindow.GetType().FullName == "UnityEditor.GameView";
                frames.Add(new Frame(Time.unscaledDeltaTime * 1000d, (now - lastFrameAt) * 1000d,
                    mouseEvents, Application.isFocused, gameFocused));
                mouseEvents = 0;
                lastFrame = Time.frameCount;
                lastFrameAt = now;
            }
            if (!EditorApplication.isPlaying || now - startedAt >= 10d) Stop();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Stop();
        }

        private static void Stop()
        {
            if (!recording) return;
            recording = false;
            InputSystem.onEvent -= OnInput;
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            try
            {
                string folder = Path.GetFullPath("Library/MouseMotionDiagnostics");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                var csv = new StringBuilder("frame_ms,mouse_state_events_seen_by_InputSystem,wall_ms,unity_focused,game_view_focused\n");
                var durations = new List<double>(frames.Count);
                int totalEvents = 0;
                foreach (Frame frame in frames)
                {
                    csv.Append(frame.Milliseconds.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                        .Append(frame.MouseEvents).Append(',')
                        .Append(frame.WallMilliseconds.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                        .Append(frame.UnityFocused ? '1' : '0').Append(',').Append(frame.GameFocused ? '1' : '0').Append('\n');
                    durations.Add(frame.Milliseconds);
                    totalEvents += frame.MouseEvents;
                }
                File.WriteAllText(path + ".csv", csv.ToString());
                ProfilerDriver.SaveProfile(path + ".data");
                durations.Sort();
                string summary = $"Unity {Application.unityVersion}; {SystemInfo.graphicsDeviceType}; " +
                    $"Game {Screen.width}x{Screen.height}; frames={frames.Count}; InputSystem mouse events={totalEvents}.\n" +
                    "Profile Editor: true (temporary); detailed editor samples included; Deep Profile unchanged.\n";
                if (durations.Count > 0)
                    summary += $"Frame ms median={durations[durations.Count / 2]:F2}, " +
                        $"p95={durations[(int)((durations.Count - 1) * .95)]:F2}, max={durations[durations.Count - 1]:F2}.\n";
                summary += "Local diagnostic only. Mouse event counts are after InputSystem merging, not the hardware polling rate.\n" +
                    "Profiler recording adds overhead; compare stationary/moving intervals within the same capture.\n";
                File.WriteAllText(path + ".txt", summary);
                Debug.Log("鼠标卡顿采样已保存（未上传）：" + path + ".data / .csv / .txt");
            }
            catch (Exception error) { Debug.LogException(error); }
            finally
            {
                ProfilerDriver.profileEditor = editorWasProfiled;
                ProfilerDriver.enabled = profilerWasEnabled;
            }
        }
    }
}
