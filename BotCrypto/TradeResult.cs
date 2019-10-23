using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbitrationCrypto
{
    public class TradeResult
    {
        public bool IsFilled { get; set; } //выполнен полностью или нет
        public decimal OrigQty { get; set; } //сколько передавали на ордер
        public decimal ExecutedQty { get; set; }  //сколько получили при выполнении ордера
    }
}
