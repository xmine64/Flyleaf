﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;

using Microsoft.Win32;

namespace FlyleafLib
{
    public static partial class Utils
    {
        // VLC : https://github.com/videolan/vlc/blob/master/modules/gui/qt/dialogs/preferences/simple_preferences.cpp
        // Kodi: https://github.com/xbmc/xbmc/blob/master/xbmc/settings/AdvancedSettings.cpp

        public static List<string> ExtensionsAudio = new List<string>()
        {
            // VLC
              "3ga" , "669" , "a52" , "aac" , "ac3"
            , "adt" , "adts", "aif" , "aifc", "aiff"
            , "au"  , "amr" , "aob" , "ape" , "caf"
            , "cda" , "dts" , "flac", "it"  , "m4a"
            , "m4p" , "mid" , "mka" , "mlp" , "mod"
            , "mp1" , "mp2" , "mp3" , "mpc" , "mpga"
            , "oga" , "oma" , "opus", "qcp" , "ra"
            , "rmi" , "snd" , "s3m" , "spx" , "tta"
            , "voc" , "vqf" , "w64" , "wav" , "wma"
            , "wv"  , "xa"  , "xm"
        };

        public static List<string> ExtensionsPictures = new List<string>()
        {
            "apng", "bmp", "gif", "jpg", "jpeg", "png", "ico", "tif", "tiff", "tga"
        };

        public static List<string> ExtensionsSubtitles = new List<string>()
        {
            "ass", "ssa", "srt", "sub", "txt", "text", "vtt"
        };

        public static List<string> ExtensionsVideo = new List<string>()
        {
            // VLC
              "3g2" , "3gp" , "3gp2", "3gpp", "amrec"
            , "amv" , "asf" , "avi" , "bik" , "divx"
            , "drc" , "dv"  , "f4v" , "flv" , "gvi"
            , "gxf" , "m1v" , "m2t" , "m2v" , "m2ts"
            , "m4v" , "mkv" , "mov" , "mp2v", "mp4"
            , "mp4v", "mpa" , "mpe" , "mpeg", "mpeg1"
            , "mpeg2","mpeg4","mpg" , "mpv2", "mts"
            , "mtv" , "mxf" , "nsv" , "nuv" , "ogg"
            , "ogm" , "ogx" , "ogv" , "rec" , "rm"
            , "rmvb", "rpl" , "thp" , "tod" , "ts"
            , "tts" , "vob" , "vro" , "webm", "wmv"
            , "xesc"

            // Additional
            , "dav"
        };

        private static int uniqueId;
        public static int GetUniqueId() { Interlocked.Increment(ref uniqueId); return uniqueId; }

        /// <summary>
        /// Begin invokes the UI thread if required to execute the specified action
        /// </summary>
        /// <param name="action"></param>
        public static void UI(Action action)
        {
#if DEBUG
            if (Application.Current == null)
                return;
#endif

            Application.Current.Dispatcher.BeginInvoke(action, System.Windows.Threading.DispatcherPriority.DataBind);
        }

        /// <summary>
        /// Invokes the UI thread to execute the specified action
        /// </summary>
        /// <param name="action"></param>
        public static void UIInvoke(Action action) => Application.Current.Dispatcher.Invoke(action);

        /// <summary>
        /// Invokes the UI thread if required to execute the specified action
        /// </summary>
        /// <param name="action"></param>
        public static void UIInvokeIfRequired(Action action)
        {
            if (Thread.CurrentThread.ManagedThreadId == Application.Current.Dispatcher.Thread.ManagedThreadId)
                action();
            else
                Application.Current.Dispatcher.Invoke(action);
        }

        public static int Align(int num, int align)
        {
            int mod = num % align;
            if (mod == 0)
                return num;

            return num + (align - num % align);
        }
        public static float Scale(float value, float inMin, float inMax, float outMin, float outMax)
            => (value - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;

        /// <summary>
        /// Adds a windows firewall rule if not already exists for the specified program path
        /// </summary>
        /// <param name="ruleName">Default value is Flyleaf</param>
        /// <param name="path">Default value is current executable path</param>
        public static void AddFirewallRule(string ruleName = null, string path = null)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(ruleName))
                        ruleName = "Flyleaf";

                    if (string.IsNullOrEmpty(path))
                        path = Process.GetCurrentProcess().MainModule.FileName;

                    path = $"\"{path}\"";

                    // Check if rule already exists
                    Process proc = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd",
                            Arguments = $"/C netsh advfirewall firewall show rule name={ruleName} verbose | findstr /L {path}",
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput
                                            = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        }
                    };

                    proc.Start();
                    proc.WaitForExit();

                    if (proc.StandardOutput.Read() > 0)
                        return;

                    // Add rule with admin rights
                    proc = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd",
                            Arguments = $"/C netsh advfirewall firewall add rule name={ruleName} dir=in  action=allow enable=yes program={path} profile=any &" +
                                                 $"netsh advfirewall firewall add rule name={ruleName} dir=out action=allow enable=yes program={path} profile=any",
                            Verb = "runas",
                            CreateNoWindow = true,
                            UseShellExecute = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        }
                    };

                    proc.Start();
                    proc.WaitForExit();

                    Log($"Firewall rule \"{ruleName}\" added for {path}");
                }
                catch { }
            });
        }

        // We can't trust those
        //public static private bool    IsDesignMode=> (bool) DesignerProperties.IsInDesignModeProperty.GetMetadata(typeof(DependencyObject)).DefaultValue;
        //public static bool            IsDesignMode    = LicenseManager.UsageMode == LicenseUsageMode.Designtime; // Will not work properly (need to be called from non-static class constructor)

        //public static bool          IsWin11         = Regex.IsMatch(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", "").ToString(), "Windows 11");
        //public static bool          IsWin10         = Regex.IsMatch(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", "").ToString(), "Windows 10");
        //public static bool          IsWin8          = Regex.IsMatch(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", "").ToString(), "Windows 8");
        //public static bool          IsWin7          = Regex.IsMatch(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", "").ToString(), "Windows 7");

        public static List<string> GetMoviesSorted(List<string> movies)
        {
            List<string> moviesSorted = new List<string>();

            for (int i = 0; i < movies.Count; i++)
            {
                string ext = Path.GetExtension(movies[i]);

                if (ext == null || ext.Trim() == "")
                    continue;

                if (ExtensionsVideo.Contains(ext.Substring(1, ext.Length - 1).ToLower()))
                    moviesSorted.Add(movies[i]);
            }

            moviesSorted.Sort(new NaturalStringComparer());

            return moviesSorted;
        }
        public sealed class NaturalStringComparer : IComparer<string> { public int Compare(string a, string b) { return NativeMethods.StrCmpLogicalW(a, b); } }

        public static string GetRecInnerException(Exception e)
        {
            string dump = "";
            var cur = e.InnerException;

            for (int i = 0; i < 4; i++)
            {
                if (cur == null) break;
                dump += "\r\n - " + cur.Message;
                cur = cur.InnerException;
            }

            return dump;
        }
        public static string GetUrlExtention(string url) { return url.LastIndexOf(".") > 0 ? url.Substring(url.LastIndexOf(".") + 1).ToLower() : ""; }
        public static List<Language> GetSystemLanguages()
        {
            List<Language> Languages = new List<Language>();
            if (CultureInfo.CurrentCulture.ThreeLetterISOLanguageName != "eng")
                Languages.Add(Language.Get(CultureInfo.CurrentCulture));

            foreach (System.Windows.Forms.InputLanguage lang in System.Windows.Forms.InputLanguage.InstalledInputLanguages)
                if (lang.Culture.ThreeLetterISOLanguageName != CultureInfo.CurrentCulture.ThreeLetterISOLanguageName && lang.Culture.ThreeLetterISOLanguageName != "eng")
                    Languages.Add(Language.Get(lang.Culture));

            Languages.Add(Language.English);

            return Languages;
        }

        public class MediaParts
        {
            public int Season { get; set; }
            public int Episode { get; set; }
            public string Title { get; set; }
            public int Year { get; set; }
        }
        public static MediaParts GetMediaParts(string title, bool movieOnly = false)
        {
            Match res;
            MediaParts mp = new MediaParts();
            List<int> indices = new List<int>();

            // s|season 01 ... e|episode 01
            res = Regex.Match(title, @"(^|[^a-z0-9])(s|season)[^a-z0-9]*(?<season>[0-9]{1,2})[^a-z0-9]*(e|episode)[^a-z0-9]*(?<episode>[0-9]{1,2})($|[^a-z0-9])", RegexOptions.IgnoreCase);
            if (!res.Success) // 01x01
                res = Regex.Match(title, @"(^|[^a-z0-9])(?<season>[0-9]{1,2})x(?<episode>[0-9]{1,2})($|[^a-z0-9])", RegexOptions.IgnoreCase);

            if (res.Success && res.Groups["season"].Value != "" && res.Groups["episode"].Value != "")
            {
                mp.Season = int.Parse(res.Groups["season"].Value);
                mp.Episode = int.Parse(res.Groups["episode"].Value);

                if (movieOnly)
                    return mp;

                indices.Add(res.Index);
            }

            // non-movie words, 1080p, 2015
            indices.Add(Regex.Match(title, "[^a-z0-9]extended", RegexOptions.IgnoreCase).Index);
            indices.Add(Regex.Match(title, "[^a-z0-9]directors.cut", RegexOptions.IgnoreCase).Index);
            indices.Add(Regex.Match(title, "[^a-z0-9]brrip", RegexOptions.IgnoreCase).Index);
            indices.Add(Regex.Match(title, "[^a-z0-9][0-9]{3,4}p", RegexOptions.IgnoreCase).Index);

            res = Regex.Match(title, @"[^a-z0-9](?<year>(19|20)[0-9][0-9])($|[^a-z0-9])", RegexOptions.IgnoreCase);
            if (res.Success)
            {
                indices.Add(res.Index);
                mp.Year = int.Parse(res.Groups["year"].Value);
            }

            var sorted = indices.OrderBy(x => x);

            foreach (var index in sorted)
                if (index > 0)
                {
                    title = title.Substring(0, index);
                    break;
                }

            title = title.Replace(".", " ").Replace("_", " ");
            title = Regex.Replace(title, @"\s{2,}", " ");
            title = Regex.Replace(title, @"[^a-z0-9]$", "", RegexOptions.IgnoreCase);

            mp.Title = title.Trim();

            return mp;
        }

        public static string FindNextAvailableFile(string fileName)
        {
            if (!File.Exists(fileName)) return fileName;

            string tmp = Path.Combine(Path.GetDirectoryName(fileName), Regex.Replace(Path.GetFileNameWithoutExtension(fileName), @"(.*) (\([0-9]+)\)$", "$1"));
            string newName;

            for (int i = 1; i < 101; i++)
            {
                newName = tmp + " (" + i + ")" + Path.GetExtension(fileName);
                if (!File.Exists(newName)) return newName;
            }

            return null;
        }
        public static string GetValidFileName(string name) { return string.Join("_", name.Split(Path.GetInvalidFileNameChars())); }

        public static string FindFileBelow(string filename)
        {
            string current = Directory.GetCurrentDirectory();

            while (current != null)
            {
                if (File.Exists(Path.Combine(current, filename)))
                    return Path.Combine(current, filename);

                current = Directory.GetParent(current)?.FullName;
            }

            return null;
        }
        public static string GetFolderPath(string folder)
        {
            if (folder.StartsWith(":"))
            {
                folder = folder.Substring(1);
                return FindFolderBelow(folder);
            }

            if (Path.IsPathRooted(folder))
                return folder;

            return Path.GetFullPath(folder);
        }

        public static string FindFolderBelow(string folder)
        {
            string current = Directory.GetCurrentDirectory();

            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current, folder)))
                    return Path.Combine(current, folder);

                current = Directory.GetParent(current)?.FullName;
            }

            return null;
        }
        public static string GetUserDownloadPath() { try { return Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders\").GetValue("{374DE290-123F-4565-9164-39C4925E467B}").ToString(); } catch (Exception) { return null; } }
        public static string DownloadToString(string url, int timeoutMs = 30000)
        {
            try
            {
                using (HttpClient client = new HttpClient() { Timeout = TimeSpan.FromMilliseconds(timeoutMs) })
                    return client.GetAsync(url).Result.Content.ReadAsStringAsync().Result;
            }
            catch (Exception e)
            {
                Log($"Download failed {e.Message} [Url: {(url != null ? url : "Null")}]");
            }

            return null;
        }

        public static MemoryStream DownloadFile(string url, int timeoutMs = 30000)
        {
            MemoryStream ms = new MemoryStream();

            try
            {
                using (HttpClient client = new HttpClient() { Timeout = TimeSpan.FromMilliseconds(timeoutMs) })
                    client.GetAsync(url).Result.Content.CopyToAsync(ms).Wait();
            }
            catch (Exception e)
            {
                Log($"Download failed {e.Message} [Url: {(url != null ? url : "Null")}]");
            }

            return ms;
        }

        public static bool DownloadFile(string url, string filename, int timeoutMs = 30000, bool overwrite = true)
        {
            try
            {
                using (HttpClient client = new HttpClient() { Timeout = TimeSpan.FromMilliseconds(timeoutMs) })
                {
                    using (FileStream fs = new FileStream(filename, overwrite ? FileMode.Create : FileMode.CreateNew))
                        client.GetAsync(url).Result.Content.CopyToAsync(fs).Wait();

                    return true;
                }
            }
            catch (Exception e)
            {
                Log($"Download failed {e.Message} [Url: {(url != null ? url : "Null")}, Path: {(filename ?? "Null")}]");
            }

            return false;
        }
        public static string FixFileUrl(string url)
        {
            try
            {
                if (url == null || url.Length < 5)
                    return url;

                if (url.Substring(0, 5).ToLower() == "file:")
                    return new Uri(url).LocalPath;
            }
            catch { }

            return url;
        }

        public static string GetBytesReadable(long i)
        {
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (i >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = (i >> 50);
            }
            else if (i >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = (i >> 40);
            }
            else if (i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = (i >> 30);
            }
            else if (i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = (i >> 20);
            }
            else if (i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = (i >> 10);
            }
            else if (i >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = i;
            }
            else
            {
                return i.ToString("0 B"); // Byte
            }
            // Divide by 1024 to get fractional value
            readable = (readable / 1024);
            // Return formatted number with suffix
            return readable.ToString("0.## ") + suffix;
        }
        static List<PerformanceCounter> gpuCounters;
        public static void GetGPUCounters()
        {
            var category = new PerformanceCounterCategory("GPU Engine");
            var counterNames = category.GetInstanceNames();
            gpuCounters = new List<PerformanceCounter>();

            foreach (string counterName in counterNames)
                if (counterName.EndsWith("engtype_3D"))
                    foreach (PerformanceCounter counter in category.GetCounters(counterName))
                        if (counter.CounterName == "Utilization Percentage")
                            gpuCounters.Add(counter);
        }
        public static float GetGPUUsage()
        {
            float result = 0f;

            try
            {
                if (gpuCounters == null) GetGPUCounters();

                gpuCounters.ForEach(x => { _ = x.NextValue(); });
                Thread.Sleep(1000);
                gpuCounters.ForEach(x => { result += x.NextValue(); });

            }
            catch (Exception e) { Log($"[GPUUsage] Error {e.Message}"); result = -1f; GetGPUCounters(); }

            return result;
        }
        public static string GZipDecompress(string filename)
        {
            string newFileName = "";

            FileInfo fileToDecompress = new FileInfo(filename);
            using (FileStream originalFileStream = fileToDecompress.OpenRead())
            {
                string currentFileName = fileToDecompress.FullName;
                newFileName = currentFileName.Remove(currentFileName.Length - fileToDecompress.Extension.Length);

                using (FileStream decompressedFileStream = File.Create(newFileName))
                {
                    using (GZipStream decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                    {
                        decompressionStream.CopyTo(decompressedFileStream);
                    }
                }
            }

            return newFileName;
        }

        public unsafe static string BytePtrToStringUTF8(byte* bytePtr)
        {
            if (bytePtr == null) return null;
            if (*bytePtr == 0) return string.Empty;

            var byteBuffer = new List<byte>(1024);
            var currentByte = default(byte);

            while (true)
            {
                currentByte = *bytePtr;
                if (currentByte == 0)
                    break;

                byteBuffer.Add(currentByte);
                bytePtr++;
            }

            return Encoding.UTF8.GetString(byteBuffer.ToArray());
        }

        public static System.Windows.Media.Color WinFormsToWPFColor(System.Drawing.Color sColor) { return System.Windows.Media.Color.FromArgb(sColor.A, sColor.R, sColor.G, sColor.B); }
        public static System.Drawing.Color WPFToWinFormsColor(System.Windows.Media.Color wColor) { return System.Drawing.Color.FromArgb(wColor.A, wColor.R, wColor.G, wColor.B); }

        public static System.Windows.Media.Color VorticeToWPFColor(Vortice.Mathematics.Color sColor) { return System.Windows.Media.Color.FromArgb(sColor.A, sColor.R, sColor.G, sColor.B); }
        public static Vortice.Mathematics.Color WPFToVorticeColor(System.Windows.Media.Color wColor) { return new Vortice.Mathematics.Color(wColor.R, wColor.G, wColor.B, wColor.A); }

        public static string ToHexadecimal(byte[] bytes)
        {
            StringBuilder hexBuilder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                hexBuilder.Append(bytes[i].ToString("x2"));
            }
            return hexBuilder.ToString();
        }
        public static int GCD(int a, int b) => b == 0 ? a : GCD(b, a % b);
        public static string TicksToTime(long ticks) => new TimeSpan(ticks).ToString();
        public static void Log(string msg) { try { Debug.WriteLine($"[{DateTime.Now.ToString("hh.mm.ss.fff")}] {msg}"); } catch (Exception) { Debug.WriteLine($"[............] [MediaFramework] {msg}"); } }
    }
}