using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbitrationCrypto
{
    public class Market
    {
        public string MarketName { get; set; }
        public string MarketCurrency { get; set; }
        public string BaseCurrency { get; set; }

        public Market()
        {

        }

        public Market(string Symbol)
        {
            string[] tstr = Symbol.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            MarketName = Symbol;
            BaseCurrency = tstr[0];
            MarketCurrency = tstr[1];
        }

        public Market(string Base, string Market)
        {
            this.BaseCurrency = Base.ToUpper();
            this.MarketCurrency = Market.ToUpper();
            this.MarketName = BaseCurrency + "-" + MarketCurrency;
        }
    }
}
