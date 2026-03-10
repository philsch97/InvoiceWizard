using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InvoiceWizard
{
    public class InvoiceLine
    {
        public int Position { get; set; }
        public string ArticleNumber { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "";
        public decimal LineTotal { get; set; }

        public string Ean { get; set; } = "";
        public decimal GrossListPrice { get; set; }   // Bruttolistenpreis (GrossPriceProductTradePrice)
        public decimal NetUnitPrice { get; set; }     // Netto-Einzelpreis (NetPriceProductTradePrice)
        public decimal PriceBasisQuantity { get; set; } = 1; // PE (1, 100, ...)
        public string PriceBasisUnit { get; set; } = "";

    }
}
