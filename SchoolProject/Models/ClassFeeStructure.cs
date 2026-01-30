namespace SchoolProject.Models
{
    public class ClassFeeStructure
    {
        public int StructureId { get; set; }

        // 🔹 Session (always current in usage)
        public int SessionId { get; set; }
        public string? SessionName { get; set; }

        // 🔹 Class
        public int ClassId { get; set; }
        public string? ClassName { get; set; }

        // 🔹 Fee Head
        public int FeeHeadId { get; set; }
        public string? FeeHeadName { get; set; }

        // 🔹 Amount
        public decimal Amount { get; set; }

        // 🔹 Month configuration (NEW – critical)
        // Example: "April,May,June,July"
        public string? ApplicableMonths { get; set; }

        // 🔹 Status
        public bool IsActive { get; set; }

        // 🔹 Audit
        public DateTime CreatedDate { get; set; }
    }
}
