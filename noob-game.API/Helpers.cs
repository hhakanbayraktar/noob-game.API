using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace noob_game.API
{
    public static class Helpers
    {
        public static string UTSNow()
        {
            DateTimeOffset todayDt = new DateTimeOffset(DateTime.Now);
            return todayDt.ToUnixTimeSeconds().ToString(); ;
        }
        public static string UTSBeginningOfYear()
        {
            DateTimeOffset beginningOfYear = new DateTimeOffset(new DateTime(DateTime.Now.Year, 01,01));
            return beginningOfYear.ToUnixTimeSeconds().ToString(); ;
        }

        public static string UTSOneMonthAgo()
        {
            DateTimeOffset monthAgoDt = new DateTimeOffset(DateTime.Now.AddMonths(-1));
            return monthAgoDt.ToUnixTimeSeconds().ToString();
        }
        public static string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256   
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string   
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

    }
}
