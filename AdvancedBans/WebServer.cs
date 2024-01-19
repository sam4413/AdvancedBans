using System;
using System.Net;
using System.Text;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace AdvancedBans.AdvancedBans
{
    class WebServer
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static string WMAddress;
        public static string WMPort;
        public static string WMBanMessage;
        public static string WMBanError;

        public WebServer(string address, string port, string banMessage, string banError)
        {
            WMAddress = address;
            WMPort = port;
            WMBanMessage = banMessage;
            WMBanError = banError;
        }
        public static async Task StartWebServer()
        {
            Log.Warn("Starting webserver...");
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://{WMAddress}:{WMPort}/");
            listener.Start();
            Log.Warn($"Listening for connections on http://{WMAddress}:{WMPort}/");

            while (true)
            {
                try
                {
                    // Asynchronously wait for an incoming request
                    HttpListenerContext context = await listener.GetContextAsync();

                    // Process the request in a new task
                    await Task.Run(() => ProcessRequestAsync(context));
                }
                catch (Exception ex)
                {
                    Log.Error($"Error: {ex.Message}");
                }
            }
        }

        private static async Task ProcessRequestAsync(HttpListenerContext context)
        {
            try
            {
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                string responseString = ConstructHtmlResponse(request.QueryString);
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);

                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;

                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Log.Error($"Request Error: {ex.Message}");
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }
        private static string ConstructHtmlResponse(System.Collections.Specialized.NameValueCollection query)
        {
            AdvancedBansConfig MyConfig = new AdvancedBansConfig();
            if (query["BanNumber"] != null && query["SteamID"] != null && query["ExpireDate"] != null && query["Reason"] != null)
            {
                var MyReturningValue = WMBanMessage;

                MyReturningValue = MyReturningValue.Replace("{{BanNumber}}", query["BanNumber"] ?? "N/A");
                MyReturningValue = MyReturningValue.Replace("{{SteamID}}", query["SteamID"] ?? "N/A");
                MyReturningValue = MyReturningValue.Replace("{{ExpireDate}}", query["IsPermanent"] == "true" ? "Never (Permanent)" : (query["ExpireDate"] ?? "N/A"));
                MyReturningValue = MyReturningValue.Replace("{{Reason}}", HttpUtility.UrlDecode(query["Reason"] ?? "N/A"));

                return MyReturningValue;

            }
            return WMBanError;
        }
    }
}
