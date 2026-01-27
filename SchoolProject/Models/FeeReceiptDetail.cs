namespace SchoolProject.Models
{
    public class FeeReceiptDetail
    {
        public int ReceiptDetailId { get; set; }
        public int ReceiptId { get; set; }

        public int FeeHeadId { get; set; }
        public int? FeeMonth { get; set; }

        public decimal DueAmount { get; set; }
        public decimal ConcessionAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceAmount { get; set; }
    }

}
