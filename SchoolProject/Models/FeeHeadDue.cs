namespace SchoolProject.Models
{
    public class FeeHeadDue
    {
        public int FeeHeadId { get; set; }
        public string FeeHeadName { get; set; }

        public decimal Due { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
    }
}
