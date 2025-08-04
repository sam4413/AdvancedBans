using NLog;
using ProtoBuf;
using Sandbox.Game;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Utils;

namespace AdvancedBans
{
	class WebServer
	{
		public static AdvancedBans Plugin = AdvancedBans.Instance;

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
		private static ConcurrentDictionary<string, DateTime> rateLimits = new ConcurrentDictionary<string, DateTime>();

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

		public async Task StartWebServer()
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
					HttpListenerContext context = await listener.GetContextAsync();
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
			var remoteAddr = context.Request.RemoteEndPoint.ToString();
			if (IsRateLimited(remoteAddr))
			{
				context.Response.StatusCode = 429; // Too Many Requests
				context.Response.Close();
				return;
			}

			HttpListenerRequest request = context.Request;
			HttpListenerResponse response = context.Response;
			string caseId = request.RawUrl.TrimStart('/');
			try
			{
                string responseString = GetBanDetails(caseId);
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            } catch (Exception ex)
			{
				if (AdvancedBans.Instance.Config.Debug)
				{
					Log.Error($"Error processing request for case ID {caseId}: {ex.Message}");
                }
            }



            
		}

		private static bool IsRateLimited(string ipAddress)
		{
			AdvancedBansConfig config = new AdvancedBansConfig();
			if (config.WebEnforceRateLimits)
			{			
				if (rateLimits.TryGetValue(ipAddress, out DateTime lastRequestTime))
				{
					if ((DateTime.Now - lastRequestTime).TotalMinutes < 5)
					{
						return true;
					}
				rateLimits[ipAddress] = DateTime.Now;
				}
			}
			return false;
		}

        private static string GetBanDetails(string caseId)
        {
            string myQuery = Plugin.banRepo.GetBannedByCaseID(caseId);

            if (myQuery == null)
            {
                return WMBanError;
            }

            var banInfoList = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(myQuery);

            if (banInfoList == null || banInfoList.Count == 0)
            {
                return WMBanError;
            }

            var banInfo = banInfoList[0];

            ulong steamid = banInfo["SteamID"].GetUInt64();
            string dbCaseId = banInfo["CaseID"].ValueKind != JsonValueKind.Null ? banInfo["CaseID"].GetString() : "N/A";
            DateTime bannedDate = banInfo["BannedDate"].ValueKind != JsonValueKind.Null ? banInfo["BannedDate"].GetDateTime() : DateTime.MinValue;
            DateTime expireDate = banInfo["ExpireDate"].ValueKind != JsonValueKind.Null ? banInfo["ExpireDate"].GetDateTime() : DateTime.MinValue;
            string reason = banInfo["Reason"].ValueKind != JsonValueKind.Null ? banInfo["Reason"].GetString() : "N/A";
            byte isPermanentBan = banInfo["IsPermanentBan"].ValueKind != JsonValueKind.Null ? banInfo["IsPermanentBan"].GetByte() : (byte)0;
            byte isExpired = banInfo["IsExpired"].ValueKind != JsonValueKind.Null ? banInfo["IsExpired"].GetByte() : (byte)0;
            byte isIPBanned = banInfo["EnforceIP"].ValueKind != JsonValueKind.Null ? banInfo["EnforceIP"].GetByte() : (byte)0;
            TimeSpan remainingTime = expireDate - DateTime.Now;
            string formattedTimeSpan = FormatTimeSpan(remainingTime);

            var placeholders = new Dictionary<string, string>
    {
        { "{{SteamID}}", steamid.ToString() },
        { "{{CaseID}}", dbCaseId },
        { "{{BannedDate}}", bannedDate != DateTime.MinValue ? bannedDate.ToString("MM-dd-yyyy") : "N/A" },
        { "{{ExpireDate}}", isPermanentBan == 1 ? "Never (Permanent)" : (expireDate != DateTime.MinValue ? expireDate.ToString("MM-dd-yyyy") : "N/A") },
        { "{{Reason}}", HttpUtility.UrlDecode(reason) },
        { "{{IsPermanent}}", isPermanentBan == 1 ? "Yes" : "No" },
        { "{{IsExpired}}", isExpired == 1 ? "Yes" : "No" },
        { "{{RemainingTime}}", formattedTimeSpan },
        { "{{IPBanned}}", isIPBanned == 1 ? "Yes" : "No" }
    };

            string responseString = WMBanMessage;

            foreach (var placeholder in placeholders)
            {
                responseString = responseString.Replace(placeholder.Key, placeholder.Value);
            }

            return responseString;
        }

        internal static string FormatTimeSpan(TimeSpan timeSpan)
		{
			if (timeSpan.TotalSeconds <= 0)
			{
				return "0d 0h 0m 0s";
			}

			int years = timeSpan.Days / 365;
			int days = timeSpan.Days % 365;
			int hours = timeSpan.Hours;
			int minutes = timeSpan.Minutes;
			int seconds = timeSpan.Seconds;

			if (years > 0)
			{
				return $"{years}y {days}d {hours}h {minutes}m {seconds}s";
			}
			else
			{
				return $"{days}d {hours}h {minutes}m {seconds}s";
			}
		}


		/// <summary>
		/// Opens a webpage with url to a target playerid.
		/// </summary>
		/// <param name="caseId"></param>
		/// <param name="playerid"></param>
		public static void ShowBanMessage(string caseId, long playerid)
		{
            //We need to check if the server is steam. If so, use the torchmod to open the webpage.
            if (MyPlatformGameSettings.CONSOLE_COMPATIBLE == false)
			{
				if (AdvancedBans.Instance.Config.Debug)
					Log.Warn($"Opening ban message for case ID {caseId} to player {playerid}.");
                string connectionString = $"https://steamcommunity.com/linkfilter/?url={AdvancedBans.Instance.Config.WebPublicAddress}:{WMPort}/{caseId}";
                ModCommunication.SendMessageTo(new WebServerMessage(connectionString), MySession.Static.Players.TryGetSteamId(playerid));
            }
			else
			{
                if (AdvancedBans.Instance.Config.Debug)
                    Log.Warn($"Normally ban message for case ID {caseId} to player {playerid}.");
                MyVisualScriptLogicProvider.OpenSteamOverlay($"https://steamcommunity.com/linkfilter/?url={AdvancedBans.Instance.Config.WebPublicAddress}:{WMPort}/{caseId}", playerid);
            }
            

            //Else open with default method if EOS (because linkfilter is not available in EOS)

        }

        [ProtoContract]
        public class WebServerMessage : MessageBase
		{
            [ProtoMember(201)]
            public string Address { get; set; }
			public WebServerMessage(string address)
			{
				Address = address;
			}
            public override void ProcessClient()
            {
				MyGuiSandbox.OpenUrlWithFallback(Address, "You have been banned!", false);
            }

            public override void ProcessServer()
            {
            }
        }
		
    }
}
