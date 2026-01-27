namespace SchoolProject.Models
{
    public class StudentFeeCollect
    {
        public int StudentId { get; set; }
        public int SessionId { get; set; }
        public string SessionName { get; set; }   // 👈 OK
        public int ClassId { get; set; }

        public string StudentName { get; set; }
        public string AdmissionNumber { get; set; }
        public string ClassName { get; set; }
        public string SectionName { get; set; }

        // 💰 FEE CALCULATION
        public decimal TotalDue { get; set; }           // Base Fee
        public decimal TotalConcession { get; set; }    // 👈 NEW (Accountant enters)
        public decimal NetFee { get; set; }             // 👈 NEW (Due - Concession)
        public decimal TotalPaid { get; set; }
        public decimal Outstanding { get; set; }

        // 📋 Fee Head Breakdown
        // 🔁 NEW: Month-wise ledger
        public List<StudentMonthlyFee> MonthlyLedger { get; set; } = new();
    }
}
