namespace SchoolProject.Models
{
    public class StudentMonthlyFee
    {
        public int MonthlyFeeId { get; set; }
        public int StudentId { get; set; }
        public int SessionId { get; set; }
        public int ClassId { get; set; }

        public int FeeHeadId { get; set; }
        public string FeeHeadName { get; set; }
        public string FeeMonth { get; set; }

        public decimal DueAmount { get; set; }
        public decimal PaidAmount { get; set; }

        // ✅ Derived field (not stored)
        public decimal Balance => DueAmount - PaidAmount;

        public DateTime CreatedDate { get; set; }
    }
}
