namespace SchoolProject.Models
{
    public class FeeReceipt
    {
        public int ReceiptId { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public int ReceiptYear { get; set; }

        public int SessionId { get; set; }
        public int StudentId { get; set; }

        public decimal TotalDue { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceAmount { get; set; }
        public decimal AdvanceAmount { get; set; }

        public string PaymentMode { get; set; } = string.Empty;
        public string? PaymentReference { get; set; }
        public int ReceivedBy { get; set; }

        public DateTime ReceiptDate { get; set; }
        public bool IsCancelled { get; set; }
    }

}
