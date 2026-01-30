namespace SchoolProject.Models
{
    public class MonthlyFeeStatus
    {
        public int StudentId { get; set; }
        public string AdmissionNumber { get; set; }
        public string StudentName { get; set; }

        public string ClassName { get; set; }
        public string SectionName { get; set; }

        public string FeeMonth { get; set; }
        public decimal TotalDue { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Balance { get; set; }

        public string FeeStatus { get; set; }
        public int? ReceiptId { get; set; }
    }

}
