using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace bgogoApi
{
    public class Bgogo
    {
        public readonly string BaseUrl = "https://bgogo.com/api";
        private readonly Encoding encoding = Encoding.UTF8;

        private HttpClient httpClient;
        private readonly string apiKey;  //email
        private readonly string apiSecret;   //password

        private string Token;
        private readonly string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:61.0) Gecko/20100101 Firefox/61.0";

        public Bgogo()
        {
            apiKey = null;
            apiSecret = null;
            httpClient = new HttpClient();
            SetupHttp();
        }

        public Bgogo(string apiKey, string apiSecret)
        {
            this.apiKey = apiKey;
            this.apiSecret = Hash(apiSecret);
            httpClient = new HttpClient();
            SetupHttp();
        }

        private void SetupHttp()
        {
            httpClient.DefaultRequestHeaders
                .Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));  //ACCEPT header
            httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        }

        #region Helper
        private string convertParameterListToString(IDictionary<string, string> parameters)
        {
            if (parameters.Count == 0)
            {
                return "";
            }

            return parameters.Select(param => param.Key.ToString().Replace(',', '.') + "=" +  //WebUtility.UrlEncode(param.Value)
            param.Value.ToString().Replace(',', '.')).Aggregate((l, r) => l + "&" + r);
        }

        private async Task<string> DoRequest(string url, HttpMethod httpMethod, bool isAuth, Dictionary<string, string> parameters)
        {
            string completeUri = "";
            //string parameterString = convertParameterListToString(parameters);
            //completeUri = BaseUrl + url + "?" + parameterString;

            if (httpMethod == HttpMethod.Get && parameters != null)
            {
                string parameterString = convertParameterListToString(parameters);
                completeUri = BaseUrl + url + "?" + parameterString;
            }
            else
            {
                completeUri = BaseUrl + url;
            }

            var request = new HttpRequestMessage(httpMethod, completeUri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (isAuth)
            {
                var authHeader = new AuthenticationHeaderValue("Bearer", Token);
                request.Headers.Authorization = authHeader;
            }

            if (httpMethod == HttpMethod.Post)
            {
                string jparam = JsonConvert.SerializeObject(parameters);
                request.Content = new StringContent(jparam, Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response = null;
            response = await httpClient.SendAsync(request);
            //response.EnsureSuccessStatusCode();
            string content = await response.Content.ReadAsStringAsync();
            return content;
        }

        private static string Hash(string input)
        {
            using (var sha1 = new SHA1Managed())
            {
                byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }
        #endregion

        public async Task<string> Signin()
        {
            string answer = await DoRequest("/sign-in", HttpMethod.Post, false,
                new Dictionary<string, string>() { { "email", apiKey }, { "password", apiSecret } });

            var jobj = JObject.Parse(answer);
            if (jobj["status"].ToString() == "error" && jobj["reason"].ToString() == "unconfirmed device")
            {
                return "UnconfirmedDevice";
            }
            else if (jobj["status"].ToString() == "error")
            {
                return "Error: " + jobj["reason"].ToString();
            }
            else if (jobj["status"].ToString() == "ok")
            {
                Token = jobj["token"].ToString();
                return "Success";
            }
            return "Error";
        }

        public async Task<bool> ConfirmCurrentDevice(string security_code)
        {
            string answer = await DoRequest("/confirm-current-device", HttpMethod.Post, false,
                new Dictionary<string, string>() { { "email", apiKey }, { "security_code", security_code }, { "password", apiSecret } });

            var jobj = JObject.Parse(answer);
            if (jobj["status"].ToString() == "ok")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> SendSecurityCode()
        {
            string answer = await DoRequest("/send-security-code", HttpMethod.Post, false,
                new Dictionary<string, string>() { { "email", apiKey } });

            var jobj = JObject.Parse(answer);
            if (jobj["status"].ToString() == "ok")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private void CheckError(JObject jsonAnswer)
        {
            if (jsonAnswer.ContainsKey("status"))
            {
                if (jsonAnswer["status"].ToString() == "error")
                {
                    throw new Exception(jsonAnswer["reason"].ToString());
                }
            }
        }

        public async Task<string> GetVersion()
        {
            string answer = await DoRequest("/version", HttpMethod.Get, false, null);

            var jobj = JObject.Parse(answer);
            CheckError(jobj);
            return jobj["version"].ToString();
        }

        public async Task<List<Balance>> GetBalance()
        {
            string answer = await DoRequest("/balances", HttpMethod.Get, true, null);

            var jobj = JObject.Parse(answer);
            CheckError(jobj);
            string tstr = jobj["balances"].ToString();

            var balances = JsonConvert.DeserializeObject<List<Balance>>(tstr);
            return balances;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbol">ETH/BTC</param>
        /// <returns></returns>
        public async Task<Snapshot> GetSnapshot(string symbol, bool isAuth)
        {
            string answer = await DoRequest("/snapshot/" + symbol, HttpMethod.Get, isAuth, null);

            var jobj = JObject.Parse(answer);
            CheckError(jobj);

            var snapshot = JsonConvert.DeserializeObject<Snapshot>(jobj.ToString());
            return snapshot;
        }

        public async Task<Dictionary<string, Ticker>> GetTickers()
        {
            string answer = await DoRequest("/tickers", HttpMethod.Get, false, null);

            var jobj = JObject.Parse(answer);
            CheckError(jobj);
            jobj["status"].Parent.Remove();

            //var tickers = JsonConvert.DeserializeObject<Dictionary<string, Ticker>>(tstr);
            var result = new Dictionary<string, Ticker>();
            foreach (var item in jobj)
            {
                string value = item.Value.ToString();
                var ticker = JsonConvert.DeserializeObject<Ticker>(value);
                result.Add(item.Key.ToString(), ticker);
            }
            return result;
        }

        public async Task<string> OrderLimit(string Symbol, string Side, decimal Price, decimal Amount)
        {
            string sPrice = Price.ToString().Replace(",", ".");
            string sAmount = Amount.ToString().Replace(",", ".");

            string answer = await DoRequest("/place-order", HttpMethod.Post, true, new Dictionary<string, string>()
            { {"symbol", Symbol } ,{"side", Side }, { "price", sPrice }, {"amount", sAmount} });

            var jobj = JObject.Parse(answer);
            CheckError(jobj);
            string orderId = jobj["order_id"].ToString();
            return orderId;
        }

        public async Task CancelOrder(string Orderid)
        {
            string answer = await DoRequest("/cancel-order/" + Orderid, HttpMethod.Post, true, null);
            var jobj = JObject.Parse(answer);
            CheckError(jobj);
            return;
        }

        #region Model
        public class MyTrade
        {
            public decimal price { get; set; }
            public decimal amount { get; set; }
            public string side { get; set; }
            public string timestamp { get; set; }
            public decimal reward_rate { get; set; }
        }

        public class AccountBalance
        {
            public string currency { get; set; }
            public decimal available { get; set; }
        }

        public class Balance
        {
            public string Currency { get; set; }
            public decimal Available { get; set; }
            public decimal Frozen { get; set; }
            public decimal Step { get; set; }
        }

        public class Ticker
        {
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public decimal? last_price { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public decimal? lowest_ask_price { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public decimal? highest_bid_price { get; set; }

            public string past_24hrs_price_change { get; set; }
            //public decimal past_24hrs_base_volume { get; set; }
            //public decimal past_24hrs_quote_turnover { get; set; }
            //public decimal past_24hrs_high_price { get; set; }
            //public decimal past_24hrs_low_price { get; set; }
        }
        #endregion
    }

    public class BOrder
    {
        public decimal price { get; set; }
        public decimal amount { get; set; }
    }

    public class Snapshot
    {
        public decimal price_step { get; set; }
        public decimal amount_step { get; set; }
        public Dictionary<string, BOrder[]> order_book { get; set; }

        //public MyTrade[] trade_history { get; set; }
        //public AccountBalance[] my_account_balances { get; set; }
        public decimal my_fee_rate { get; set; }

        //public string[] all_symbols { get; set; }
        //public decimal[] last_prices { get; set; }
        //public string[] past_24hrs_price_changes { get; set; }
        //public decimal[] past_24hrs_high_price { get; set; }
        //public decimal[] past_24hrs_low_price { get; set; }
        //public decimal[] past_24hrs_volumes { get; set; }
        //public decimal[] past_24hrs_turnovers { get; set; }
    }
}
