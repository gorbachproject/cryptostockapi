using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArbitrationCrypto
{
    public interface IStock
    {
        /// <summary>
        /// Название биржи
        /// </summary>
        /// <returns></returns>
        string GetStockName();

        bool IsGroupUptade { get; }

        /// <summary>
        /// Получить список торговых пар
        /// </summary>
        /// <returns></returns>
        Task<List<Market>> GetMarkets();

        decimal GetPriceStep(Market Market);

        decimal GetAmountStep(Market Market);

        Task<string> ExecuteMarket(Market Market, decimal Amount, bool DirectBuy);

        Task<string> SetLimit(Market Market, decimal Amount, decimal Price, bool IsDirectBuy);

        Task<decimal> GetLastPrice(Market Market, bool ForBuy);

        Task<decimal> GetGroupLastPrice(Market Market, bool ForBuy);

        Task GetAllPrices();

        void Subscribe(Action<bool> action);

        void Unsubscribe(Action<bool> action);

        int FrequencyGroupUpdate { get; set; }

        Dictionary<string, decimal[]> GroupPrices { get; set; }

        Task<TradeResult> GetFullSum(string OrderID);

        Task<Dictionary<string, BOrder[]>> GetSnapshot(Market Market);

        Task<List<Balance>> GetBalances();

        Task<Balance> GetBalance(string Symbol);
    }

    public class BOrder
    {
        public decimal price { get; set; }
        public decimal amount { get; set; }
    }

    //public class Snapshot
    //{
    //    public decimal price_step { get; set; }
    //    public decimal amount_step { get; set; }
    //    public Dictionary<string, BOrder[]> order_book { get; set; }
    //    public decimal my_fee_rate { get; set; }
    //}

    public class Balance
    {
        public string Currency { get; set; }
        public decimal Available { get; set; }
        public decimal Frozen { get; set; }
    }
}
