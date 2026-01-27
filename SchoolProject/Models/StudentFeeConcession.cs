namespace SchoolProject.Models
{
    public class StudentFeeConcession
    {
        public int ConcessionId { get; set; }
        public int SessionId { get; set; }
        public int StudentId { get; set; }
        public int FeeHeadId { get; set; }

        // DISCOUNT AMOUNT
        public decimal ConcessionAmount { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
