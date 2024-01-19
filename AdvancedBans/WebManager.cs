using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Web;
using NLog;
using Torch;

namespace AdvancedBans.AdvancedBans
{
    class WebManager
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static string WMAddress;
        public static string WMPort;

        public WebManager(string address, string port)
        {
            WMAddress = address;
            WMPort = port;
        }

        public void StartWebManager()
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://{WMAddress}:{WMPort}/");
            listener.Start();
            Log.Warn($"Listening for connections on http://{WMAddress}:{WMPort}/");

            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                string responseString = ConstructHtmlResponse(request.QueryString);

                byte[] buffer = Encoding.UTF8.GetBytes(responseString);

                response.ContentType = "text/html"; // Set the Content-Type to 'text/html'
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
        }

        static string ConstructHtmlResponse(System.Collections.Specialized.NameValueCollection query)
        {
            if (query["BanNumber"] != null && query["SteamID"] != null && query["ExpireDate"] != null && query["Reason"] != null)
            {
                string isPermanent = query["IsPermanent"];
                string expireDate = isPermanent == "true" ? "Never (Permanent)" : query["ExpireDate"];

                return $@"
<!DOCTYPE html>
<html>
<head>
    <title>Ban Notice</title>
</head>
<body>
    <h1>You have been banned!</h1>
    <p><strong>Ban Number:</strong> {query["BanNumber"]}</p>
    <p><strong>SteamID:</strong> {query["SteamID"]}</p>
    <p><strong>ExpireDate:</strong> {expireDate}</p>
    <p><strong>Reason:</strong> {HttpUtility.UrlDecode(query["Reason"])}</p>
</body>
</html>";
            }

            // Return a simple HTML page for empty or invalid requests
            return "<!DOCTYPE html><html><head><title>Access</title></head><body><p>No ban details provided or request is invalid.</p></body></html>";
        }


    }
}
