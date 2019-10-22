using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using bgogoApi;
using System.Linq;

namespace ArbitrationCrypto
{
    internal class BgogoStock : IStock
    {
        private static Bgogo bgogo;
        private bool WasAuth = false;
        private Dictionary<string, decimal> PriceSteps = new Dictionary<string, decimal>();
        private readonly Dictionary<string, decimal> AmountSteps = new Dictionary<string, decimal>();

        public bool IsGroupUptade { get; } = true;
        private readonly InvokePrint Print = Form1.Print;
        private int CountSubscribed = 0;
        private System.Threading.Timer timerPricesUpdate; //обновление Ask, Bid
        private event Action<bool> GroupUpdate;
        private readonly object GetAllPricesLock = new object();
        private bool InGetAllPricesLock = false;

        public int FrequencyGroupUpdate { get; set; }
        public Dictionary<string, decimal[]> GroupPrices { get; set; } = new Dictionary<string, decimal[]>();

        public BgogoStock()
        {
            if (bgogo == null)
            {
                bgogo = new Bgogo();
            }
        }

        public BgogoStock(string apiKey, string apiSecret)
        {
            if (bgogo == null)
            {
                bgogo = new Bgogo(apiKey, apiSecret);
            }
        }

        public static void SetApiKeys(string apiKey, string apiSecret)
        {
            bgogo = new Bgogo(apiKey, apiSecret);
        }

        public string GetStockName()
        {
            return "Bgogo";
        }

        public decimal GetPriceStep(Market Market)
        {
            try
            {
                if (PriceSteps.ContainsKey(Market.MarketName))
                {
                    return PriceSteps[Market.MarketName];
                }
                else
                {
                    var snapshot = bgogo.GetSnapshot(Market.MarketName.Replace("-", "/"), false).Result;
                    PriceSteps.Add(Market.MarketName, snapshot.price_step);
                    return snapshot.price_step;
                }
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Beep.Play();
                Print("Ошибка BgogoApi GetPriceStep: " + ex.Message);
                return 0.00000001m;
            }
        }

        public decimal GetAmountStep(Market Market)
        {
            try
            {
                if (AmountSteps.ContainsKey(Market.MarketName))
                {
                    return AmountSteps[Market.MarketName];
                }
                else
                {
                    var snapshot = bgogo.GetSnapshot(Market.MarketName.Replace("-", "/"), false).Result;
                    AmountSteps.Add(Market.MarketName, snapshot.amount_step);
                    return snapshot.amount_step;
                }
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Beep.Play();
                Print("Ошибка BgogoApi GetAmountStep: " + ex.Message);
                return 0.00000001m;
            }
        }

        public async Task<List<Market>> GetMarkets()
        {
            try
            {
                var tickers = GetTickers();

                var resMarkets = new List<Market>();
                foreach (string ms in tickers.Keys)
                {
                    string[] sarr = ms.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    resMarkets.Add(new Market(sarr[0], sarr[1]));
                }
                return resMarkets;
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Beep.Play();
                Print("Ошибка BgogoApi GetMarkets: " + ex.Message);
                return new List<Market>();
            }
        }

        public async Task<string> ExecuteMarket(Market Market, decimal Amount, bool DirectBuy)
        {
            try
            {
                if (!WasAuth)
                {
                    var task = Task.Run(() => Signin(""));
                    if (!task.Result)
                    {
                        return "";
                    }
                }

                decimal price = await GetGroupLastPrice(Market, DirectBuy);
                decimal rate = price;
                if (DirectBuy)
                {
                    rate += (decimal)((double)rate * 0.02);
                }
                else
                {
                    rate -= (decimal)((double)rate * 0.1);
                    if (rate < 0)
                    {
                        rate = (decimal)0.00000001;
                    }
                }
                rate = Math.Round(rate, 8);
                return await ExecuteOrder(DirectBuy, Market.MarketName.Replace("-", "/"), Amount, rate);
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Beep.Play();
                Print("Ошибка BgogoApi ExecuteMarket: " + ex.Message);
                return "";
            }
        }

        public async Task<string> SetLimit(Market Market, decimal Amount, decimal Price, bool IsDirectBuy)
        {
            try
            {
                string side = IsDirectBuy ? "buy" : "sell";
                string uuid = await bgogo.OrderLimit(Market.MarketName.Replace("-", "/"), side, Price, Amount);
                return uuid;
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Beep.Play();
                Print("Ошибка BgogoApi SetLimit: " + ex.Message);
                return "";
            }
        }

        private async Task<string> ExecuteOrder(bool DirectBuy, string Symbol, decimal Amount, decimal Price)
        {
            try
            {
                string side = DirectBuy ? "buy" : "sell";
                string uuid = await bgogo.OrderLimit(Symbol, side, Price, Amount);
                return uuid;
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Beep.Play();
                Print("Ошибка BgogoApi ExecuteOrder: " + ex.Message);
                return "";
            }
        }

        public async Task<TradeResult> GetFullSum(string OrderID)
        {
            try
            {
                //string[] arrtmp = OrderID.Split(new char[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
                //long orderID = Convert.ToInt64(arrtmp[1]);

                //Order ResOrder = await binanceClient.GetOrder(arrtmp[0], orderID);
                //TradeResult tresult = new TradeResult();
                //tresult.IsFilled = ResOrder.Status == "FILLED";
                //if (ResOrder.Side == "BUY")
                //{
                //    tresult.OrigQty = ResOrder.СummulativeQuoteQty;
                //    tresult.ExecutedQty = ResOrder.ExecutedQty;
                //}
                //else
                //{
                //    tresult.OrigQty = ResOrder.OrigQty;
                //    tresult.ExecutedQty = ResOrder.СummulativeQuoteQty;
                //}
                return new TradeResult(); // tresult;
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Beep.Play();
                Print("Ошибка BgogoApi GetFullSum: " + ex.Message);
                return new TradeResult();
            }
        }

        public async Task<Dictionary<string, BOrder[]>> GetSnapshot(Market Market)
        {
            try
            {
                var snapshot = await bgogo.GetSnapshot(Market.MarketName.Replace("-", "/"), false);

                var order_book = new Dictionary<string, BOrder[]>();
                foreach (var orderb in snapshot.order_book)
                {
                    var orders = new BOrder[orderb.Value.Length];
                    for (int i = 0; i < orderb.Value.Length; i++)
                    {
                        orders[i] = new BOrder { amount = orderb.Value[i].amount, price = orderb.Value[i].price };
                    }
                    order_book.Add(orderb.Key, orders);
                }
                return order_book;
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Beep.Play();
                Print("Ошибка BgogoApi GetSnapshot: " + ex.Message);
                return null;
            }
        }

        public async Task<decimal> GetLastPrice(Market Market, bool ForBuy)
        {
            try
            {
                var tickers = GetTickers();
                decimal result = 0;
                foreach (var tmp in tickers)
                {
                    if (tmp.Key == Market.MarketName.Replace("-", "/"))
                    {
                        if (tmp.Value.lowest_ask_price.HasValue && tmp.Value.highest_bid_price.HasValue)
                        {
                            result = ForBuy ? tmp.Value.lowest_ask_price.Value : tmp.Value.highest_bid_price.Value;
                        }
                        break;
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Beep.Play();
                Print("Ошибка BgogoApi GetLastPrice: " + ex.Message);
                return 0;
            }
        }

        public async Task<decimal> GetGroupLastPrice(Market Market, bool ForBuy)
        {
            try
            {
                string skey = Market.MarketName.Replace("-", "/");
                if (GroupPrices.ContainsKey(skey))
                {
                    return ForBuy ? GroupPrices[skey][0] : GroupPrices[skey][1];
                }
                return await GetLastPrice(Market, ForBuy);
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Beep.Play();
                Print("Ошибка BgogoApi GetLastPrice: " + ex.Message);
                return 0;
            }
        }

        public async Task GetAllPrices()
        {
            Dictionary<string, Bgogo.Ticker> tickers;
            try
            {
                tickers = GetTickers();
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Beep.Play();
                Print("Ошибка BgogoApi GetAllPrices: " + ex.Message);
                return;
            }
            GroupPrices.Clear();
            foreach (var tmp in tickers)
            {
                if (tmp.Value.lowest_ask_price.HasValue && tmp.Value.highest_bid_price.HasValue)
                {
                    GroupPrices.Add(tmp.Key, new decimal[] { tmp.Value.lowest_ask_price.Value, tmp.Value.highest_bid_price.Value });
                }
            }
        }

        private void TPricesUpdate(object state)
        {
            if (InGetAllPricesLock)
            {
                return;
            }

            InGetAllPricesLock = true;

            GetAllPrices().Wait();
            GroupUpdate?.Invoke(true);

            InGetAllPricesLock = false;
        }

        public void Subscribe(Action<bool> action)
        {
            GroupUpdate += action;

            CountSubscribed += 1;
            if (CountSubscribed == 1)
            {
                if (timerPricesUpdate != null)
                {
                    timerPricesUpdate.Dispose();
                }
                timerPricesUpdate = new System.Threading.Timer(TPricesUpdate);
                timerPricesUpdate.Change(0, FrequencyGroupUpdate);
                //MessageBox.Show("Start subscribe!");
            }
        }

        public void Unsubscribe(Action<bool> action)
        {
            GroupUpdate -= action;

            CountSubscribed -= 1;
            if (CountSubscribed == 0)
            {
                if (timerPricesUpdate != null)
                {
                    timerPricesUpdate.Dispose();
                }
                //MessageBox.Show("Stop subscribe!");
            }
        }

        /// /////////////////////////////////////////////////////

        public bool Signin(string emailCode)
        {
            try
            {
                var task = Task.Run(() => bgogo.Signin());
                string res = task.Result;

                if (res == "UnconfirmedDevice")
                {
                    Print("Unconfirmed device!");

                    if (String.IsNullOrEmpty(emailCode))
                    {
                        Print("Sending security code.");
                        var task2 = Task.Run(() => bgogo.SendSecurityCode());
                        bool res2 = task2.Result;

                        if (res2)
                        {
                            Print("A confirm email has been sent.\r\n");
                        }
                        else
                        {
                            Print("E-mail send failed!\r\n");
                        }
                    }
                    else
                    {
                        Print("Confirm current device.");
                        var task3 = Task.Run(() => bgogo.ConfirmCurrentDevice(emailCode));
                        bool res3 = task3.Result;
                        if (res3)
                        {
                            Print("Success confirm device.\r\n");
                        }
                        else
                        {
                            Print("Confirm current device failed!\r\n");
                        }
                    }
                    return false;
                }
                else if (res == "Success")
                {
                    Print("Sign is success!\r\n");
                    WasAuth = true;
                    return true;
                }
                else if (res.Contains("Error"))
                {
                    Print("An error occurred while sign in: " + res + "\r\n");
                    return false;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Beep.Play();
                Print("Ошибка BgogoApi Signin: " + ex.Message);
                return false;
            }
        }

        public async Task<List<Balance>> GetBalances()
        {
            try
            {
                if (WasAuth == false)
                {
                    bool signin = Signin("");
                    if (!signin)
                    {
                        return null;
                    }
                }
                var balance = await bgogo.GetBalance();
                var resBalances = new List<Balance>();
                foreach (var item in balance)
                {
                    resBalances.Add(new Balance { Available = item.Available, Currency = item.Currency, Frozen = item.Frozen });
                }
                return resBalances;
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Beep.Play();
                Print("Ошибка BgogoApi GetBalances: " + ex.Message);
                return null;
            }
        }

        public async Task<Balance> GetBalance(string Symbol)
        {
            try
            {
                var balances = await GetBalances();
                return balances.First(x => x.Currency == Symbol);
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Beep.Play();
                Print("Ошибка BgogoApi GetBalance: " + ex.Message);
                return null;
            }
        }

        //private Snapshot GetSnapshot()
        //{
        //    try
        //    {
        //        var task = Task.Run(() => bgogo.GetSnapshot("ETH/BTC", false));
        //        var res = task.Result;

        //        //var bids = res.order_book["bids"];    //0 - best
        //        //var asks = res.order_book["asks"];    //0 - best
        //        return res;
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Media.SystemSounds.Beep.Play();
        //        Print("Ошибка BgogoApi GetSnapshot: " + ex.Message);
        //        return null;
        //    }
        //}

        private Dictionary<string, Bgogo.Ticker> GetTickers()
        {
            try
            {
                var task = Task.Run(() => bgogo.GetTickers());
                var res = task.Result;
                return res;
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Beep.Play();
                Print("Ошибка BgogoApi GetTickers: " + ex.Message);
                return null;
            }
        }
    }
}
