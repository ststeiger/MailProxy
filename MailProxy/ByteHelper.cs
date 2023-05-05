
namespace MailProxy
{

    public static class LogHelper
    {

        private static string LogFile = "messageLog.txt";


        public static void StartLog()
        {

            System.IO.File.AppendAllText(LogFile, "[", System.Text.Encoding.UTF8);
        }


        public static void foo()
        {
            System.IO.File.AppendAllText(LogFile, "[", System.Text.Encoding.UTF8);
        }


        public static void EndLog()
        { }
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
