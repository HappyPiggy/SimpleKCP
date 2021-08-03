using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace SimpleKCP
{
    public class Tools {
        public static Action<string> LogFunc;
        public static Action<KCPLogColor, string> ColorLogFunc;
        public static Action<string> WarnFunc;
        public static Action<string> ErrorFunc;

        public static void Log(string msg, params object[] args) {
            msg = string.Format(msg, args);
            if(LogFunc != null) {
                LogFunc(msg);
            }
            else {
                ConsoleLog(msg, KCPLogColor.None);
            }
        }
        public static void ColorLog(KCPLogColor color, string msg, params object[] args) {
            msg = string.Format(msg, args);
            if(ColorLogFunc != null) {
                ColorLogFunc(color, msg);
            }
            else {
                ConsoleLog(msg, color);
            }
        }
        public static void Warn(string msg, params object[] args) {
            msg = string.Format(msg, args);
            if(WarnFunc != null) {
                WarnFunc(msg);
            }
            else {
                ConsoleLog(msg, KCPLogColor.Yellow);
            }
        }
        public static void Error(string msg, params object[] args) {
            msg = string.Format(msg, args);
            if(ErrorFunc != null) {
                ErrorFunc(msg);
            }
            else {
                ConsoleLog(msg, KCPLogColor.Red);
            }
        }
        private static void ConsoleLog(string msg, KCPLogColor color) {
            int threadID = Thread.CurrentThread.ManagedThreadId;
            msg = string.Format("Thread:{0} {1}", threadID, msg);

            switch(color) {
                case KCPLogColor.Red:
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case KCPLogColor.Green:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case KCPLogColor.Blue:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case KCPLogColor.Cyan:
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case KCPLogColor.Magentna:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case KCPLogColor.Yellow:
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case KCPLogColor.None:
                default:
                    break;
            }

        }

        public static byte[] Serialize<T>(T msg) where T : KCPMsg {
            using(MemoryStream ms = new MemoryStream()) {
                try {
                    BinaryFormatter bf = new BinaryFormatter();
                    bf.Serialize(ms, msg);
                    ms.Seek(0, SeekOrigin.Begin);
                    return ms.ToArray();
                }
                catch(SerializationException e) {
                    Error("Failed to serialize.Reason:{0}", e.Message);
                    throw;
                }
            }
        }
        public static T DeSerialize<T>(byte[] bytes) where T : KCPMsg {
            using(MemoryStream ms = new MemoryStream(bytes)) {
                try {
                    BinaryFormatter bf = new BinaryFormatter();
                    T msg = (T)bf.Deserialize(ms);
                    return msg;
                }
                catch(SerializationException e) {
                    Error("Failed to Deserialize.Reason:{0} bytesLen:{1}", e.Message, bytes.Length);
                    throw;
                }
            }
        }

        public static byte[] Compress(byte[] input) {
            using(MemoryStream outMS = new MemoryStream()) {
                using(GZipStream gzs = new GZipStream(outMS, CompressionMode.Compress, true)) {
                    gzs.Write(input, 0, input.Length);
                    gzs.Close();
                    return outMS.ToArray();
                }
            }
        }
        public static byte[] DeCompress(byte[] input) {
            using(MemoryStream inputMS = new MemoryStream(input)) {
                using(MemoryStream outMs = new MemoryStream()) {
                    using(GZipStream gzs = new GZipStream(inputMS, CompressionMode.Decompress)) {
                        byte[] bytes = new byte[1024];
                        int len = 0;
                        while((len = gzs.Read(bytes, 0, bytes.Length)) > 0) {
                            outMs.Write(bytes, 0, len);
                        }
                        gzs.Close();
                        return outMs.ToArray();
                    }
                }
            }
        }

        static readonly DateTime utcStart = new DateTime(1970, 1, 1);
        public static ulong GetUTCStartMilliseconds() {
            TimeSpan ts = DateTime.UtcNow - utcStart;
            return (ulong)ts.TotalMilliseconds;
        }
    }

    public enum KCPLogColor {
        None,
        Red,
        Green,
        Blue,
        Cyan,
        Magentna,
        Yellow
    }
}
