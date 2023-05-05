
namespace MailProxy
{

    public static class LogHelper
    {

        private static string JsonLogFile = "messageLog.json";
        private static string LogFile = "messageLog.txt";


       private static bool hasNotStarted = true;


        public static void StartLog()
        {
            if(hasNotStarted)
                System.IO.File.AppendAllText(JsonLogFile, "[" + System.Environment.NewLine, System.Text.Encoding.UTF8);
            
            hasNotStarted = false;
        }


        // MailProxy.LogHelper.LogLine(who, raw, "hell");
        public static void LogLine(string who, System.ReadOnlySpan<byte> raw, string text)
        {
            System.IO.File.AppendAllText(LogFile, who + ":" + System.Environment.NewLine, System.Text.Encoding.UTF8);
            System.IO.File.AppendAllText(LogFile, text + System.Environment.NewLine, System.Text.Encoding.UTF8);

            string hexData = MailProxy.ByteHelper.ByteArrayToHexViaLookup32(raw);


            bool stringEscapedJson = true;

            if(stringEscapedJson)
                text = text.Replace(@"\", @"\\");

            text=text.Replace("\r", "\\r").Replace("\n", "\\n");

            if (stringEscapedJson)
                text = text.Replace("\"", "\\\""); ;


            System.IO.File.AppendAllText(JsonLogFile, "{" + System.Environment.NewLine, System.Text.Encoding.UTF8);
            System.IO.File.AppendAllText(JsonLogFile, "\"who\": " + System.Web.HttpUtility.JavaScriptStringEncode(who, true) + "," + System.Environment.NewLine, System.Text.Encoding.UTF8);
            System.IO.File.AppendAllText(JsonLogFile, "\"text\": " + System.Web.HttpUtility.JavaScriptStringEncode(text, true) + "," + System.Environment.NewLine, System.Text.Encoding.UTF8);
            System.IO.File.AppendAllText(JsonLogFile, "\"byte\": " + System.Web.HttpUtility.JavaScriptStringEncode(hexData, true) + System.Environment.NewLine, System.Text.Encoding.UTF8);
            System.IO.File.AppendAllText(JsonLogFile, "}," + System.Environment.NewLine, System.Text.Encoding.UTF8);


            
        }


        public static void EndLog()
        {
            System.IO.File.AppendAllText(JsonLogFile, System.Environment.NewLine+"]" + System.Environment.NewLine, System.Text.Encoding.UTF8);
        }
    }



    public static class ByteHelper
    {


        private static readonly uint[] _lookup32;


        private static uint[] CreateLookup32()
        {
            var result = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                string s = i.ToString("X2");
                result[i] = ((uint)s[0]) + ((uint)s[1] << 16);
            }
            return result;
        }


        static ByteHelper()
        {
            _lookup32 = CreateLookup32();
        }


        // ByteHelper.ByteArrayToHexViaLookup32
        public static string ByteArrayToHexViaLookup32(System.ReadOnlySpan<byte> bytes)
        {
            uint[] lookup32 = _lookup32;
            char[] result = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                uint val = lookup32[bytes[i]];
                result[2 * i] = (char)val;
                result[2 * i + 1] = (char)(val >> 16);
            }

            return new string(result);
        }


        // ByteHelper.ByteArrayToHexViaLookup32
        public static string ByteArrayToHexViaLookup32(byte[] bytes)
        {
            uint[] lookup32 = _lookup32;
            char[] result = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                uint val = lookup32[bytes[i]];
                result[2 * i] = (char)val;
                result[2 * i + 1] = (char)(val >> 16);
            }

            return new string(result);
        }



    }
}
