namespace SchoolProject.Models
{
    public class ClassFeeStructure
    {
        public int StructureId { get; set; }

        public int SessionId { get; set; }
        public string SessionName { get; set; }

        public int ClassId { get; set; }
        public string ClassName { get; set; }

        public int FeeHeadId { get; set; }
        public string FeeHeadName { get; set; }

        public decimal Amount { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}
