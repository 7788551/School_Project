namespace SchoolProject.Models
{
    public class FeeSummary
    {
        public decimal TotalDue { get; set; }           // Base + Transport
        public decimal TotalConcession { get; set; }    // Discount entered by Accountant
        public decimal NetFee { get; set; }             // TotalDue - TotalConcession
        public decimal Paid { get; set; }               // Total Paid Till Now
        public decimal Outstanding { get; set; }        // NetFee - Paid
    }
}
