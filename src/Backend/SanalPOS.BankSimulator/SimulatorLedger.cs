namespace SanalPOS.BankSimulator;

/// <summary>
/// Simülatörün "banka defteri": onayladığı finansal işlemlerin para birimi bazında
/// sayı/tutar toplamları. Gün sonu mutabakatında (0500) gelen toplamlar bu defterle
/// karşılaştırılır; eşleşmezse 95 (out-of-balance) dönülür.
/// </summary>
public sealed class SimulatorLedger
{
    private sealed class Totals
    {
        public int SaleCount;
        public long SaleMinor;
        public int RefundCount;
        public long RefundMinor;
        public int VoidCount;
        public long VoidMinor;
    }

    private readonly object _sync = new();
    private readonly Dictionary<string, Totals> _byCurrency = new();

    public void RecordSale(string currency, long amountMinor)
    {
        lock (_sync)
        {
            var totals = GetTotals(currency);
            totals.SaleCount++;
            totals.SaleMinor += amountMinor;
        }
    }

    public void RecordRefund(string currency, long amountMinor)
    {
        lock (_sync)
        {
            var totals = GetTotals(currency);
            totals.RefundCount++;
            totals.RefundMinor += amountMinor;
        }
    }

    public void RecordVoid(string currency, long amountMinor)
    {
        lock (_sync)
        {
            var totals = GetTotals(currency);
            totals.VoidCount++;
            totals.VoidMinor += amountMinor;
        }
    }

    public bool Matches(string currency, int saleCount, long saleMinor, int refundCount, long refundMinor, int voidCount, long voidMinor)
    {
        lock (_sync)
        {
            var totals = GetTotals(currency);
            return totals.SaleCount == saleCount && totals.SaleMinor == saleMinor
                && totals.RefundCount == refundCount && totals.RefundMinor == refundMinor
                && totals.VoidCount == voidCount && totals.VoidMinor == voidMinor;
        }
    }

    private Totals GetTotals(string currency)
    {
        if (!_byCurrency.TryGetValue(currency, out var totals))
            _byCurrency[currency] = totals = new Totals();
        return totals;
    }
}
