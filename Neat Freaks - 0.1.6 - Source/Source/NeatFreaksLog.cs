using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace NeatFreaks
{
    // Keep one writer per session outside Unity so rotation and
    // real file-lock failures can be tested. No global log capture or frame polling.
    public sealed class SessionLog : IDisposable
    {
        readonly string directory;
        readonly Action<string, string> normalLog;
        readonly object gate = new object();
        StreamWriter writer;
        bool rotated, failed, ended;
        int bufferedLines;
        public bool Enabled { get; private set; }
        public bool Verbose { get; set; }
        // Filtering here keeps disabled detail out of both the file and fallback log.
        // Warnings are independent of verbosity, so quiet mode cannot hide failures.
        public void Detail(string message)
        { if (Enabled && Verbose) Write("DETAIL", message); }

        public SessionLog(string modDirectory, Action<string, string> normalLog)
        {
            this.normalLog = normalLog;
            // Constructing an off-only session performs no filesystem operations.
            directory = Path.Combine(modDirectory, "Logs", "NeatFreaks");
        }

        public void SetEnabled(bool enabled)
        {
            lock (gate)
            {
                if (ended || enabled == Enabled) return;
                if (!enabled)
                {
                    Write("INFO", "Mod logging disabled.");
                    Enabled = false;
                    CloseWriter();
                    return;
                }
                Enabled = true;
                failed = false; // Explicit off/on permits retry after a disk error.
                try
                {
                    IconLibrary<int>.CheckNoLinks(directory);
                    Directory.CreateDirectory(directory);
                    string current = Path.Combine(directory, "NF.log");
                    string previous = Path.Combine(directory, "NF-prev.log");
                    IconLibrary<int>.CheckNoLinks(current);
                    IconLibrary<int>.CheckNoLinks(previous);
                    // Lock current before touching previous. Failed backup leaves current
                    // intact. FileShare.Read permits viewing but excludes a second writer.
                    FileStream stream = new FileStream(current, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                    bool resume = rotated;
                    try
                    {
                        if (!rotated && stream.Length > 0)
                        {
                            using (var backup = new FileStream(previous, FileMode.Create, FileAccess.Write, FileShare.Read))
                                stream.CopyTo(backup);
                        }
                        if (rotated) stream.Position = stream.Length;
                        else { stream.Position = 0; stream.SetLength(0); }
                        // Survives toggles and subsequent write failures. Never rotate
                        // a partially written current session into the previous-session log.
                        rotated = true;
                        writer = new StreamWriter(stream, new UTF8Encoding(false));
                        stream = null;
                        // Per-line flushes turn verbose diagnostics into repeated disk IO.
                        // Batch routine lines; warnings and operation boundaries flush promptly.
                        writer.AutoFlush = false;
                        Write("INFO", resume ? "Logging resumed (same session)." : "Neat Freaks 0.1.6 log session opened.");
                    }
                    finally { if (stream != null) stream.Dispose(); }
                    Flush();
                    if (!failed) normalLog("INFO", "Debug log: " + current);
                }
                catch (Exception ex) { Fail(ex); }
            }
        }

        public void Write(string level, string message, bool alwaysNormal = false)
        {
            lock (gate)
            {
                if (ended) return;
                // Warnings/errors remain visible in DFU's normal log regardless of
                // preference or disk health. Routine diagnostics are quiet when off.
                bool normal = alwaysNormal || level == "WARN" || level == "ERROR";
                if (normal) normalLog(level, message);
                if (!Enabled) return;
                if (!failed && writer != null)
                {
                    try
                    {
                        writer.WriteLine(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " [" + level + "] " + message);
                        if (++bufferedLines >= 32 || level == "WARN" || level == "ERROR" || alwaysNormal) Flush();
                        return;
                    }
                    catch (Exception ex) { Fail(ex); }
                }
                if (!normal) normalLog(level, message); // Enabled but unavailable: use Unity.
            }
        }

        // Explicit checkpoints keep completed work visible without a frame timer.
        // Dispose/disable also flush; an abrupt crash can lose the unfinished tail.
        public void Flush()
        {
            lock (gate)
            {
                if (writer == null || failed || bufferedLines == 0) return;
                try { writer.Flush(); bufferedLines = 0; }
                catch (Exception ex) { Fail(ex); }
            }
        }

        void Fail(Exception ex)
        {
            if (failed) return;
            failed = true;
            CloseWriter();
            normalLog("WARN", "Dedicated logging unavailable; using Unity log until disabled or retried. " + ex.Message);
        }

        void CloseWriter()
        {
            var previous = writer;
            writer = null;
            bufferedLines = 0;
            if (previous != null)
            {
                try { previous.Dispose(); }
                catch (Exception) { /* Optional logging cannot break game shutdown. */ }
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (ended) return;
                Write("INFO", "Log session ended.");
                ended = true;
                Enabled = false;
                CloseWriter();
            }
        }
    }

    // Unity adapter keeps the file lifecycle testable without replacing Unity APIs.
    public static class NeatFreaksLog
    {
        static SessionLog session;
        public static int WarningCount { get; private set; }
        public static void SetVerbose(bool enabled) { if (session != null) session.Verbose = enabled; }
        public static void Detail(string message) { if (session != null) session.Detail(message); }
        public static void BeginSession(string modDirectory)
        {
            if (session == null) session = new SessionLog(modDirectory, Normal);
        }
        public static void SetEnabled(bool enabled)
        {
            if (session != null)
            {
                session.SetEnabled(enabled);
                if (enabled) Detail("Runtime Unity=" + Application.unityVersion + "; texture root=" +
                    Path.Combine(Application.streamingAssetsPath, "Textures", "NeatFreaks") + "; registered icons=" + Icons.Count);
            }
        }
        static void Normal(string level, string message)
        {
            string text = "[Neat Freaks] " + message;
            if (level == "ERROR") Debug.LogError(text);
            else if (level == "WARN") Debug.LogWarning(text);
            else Debug.Log(text);
        }
        static void Write(string level, string message, bool notice = false)
        {
            if (session != null) session.Write(level, message, notice);
            else if (notice || level != "INFO") Normal(level, message);
        }
        public static void Flush() { if (session != null) session.Flush(); }
        public static void Info(string message) { Write("INFO", message); }
        public static void Notice(string message) { Write("INFO", message, true); }
        public static void Warning(string message) { WarningCount++; Write("WARN", message); }
        public static void Error(string message) { Write("ERROR", message); }
        public static void EndSession()
        {
            if (session != null) session.Dispose();
            // Retain the ended instance: late callbacks cannot reopen this session.
            // A fresh process/domain gets a fresh writer on the next game launch.
        }
    }
}
