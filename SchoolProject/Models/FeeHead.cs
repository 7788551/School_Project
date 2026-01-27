namespace SchoolProject.Models
{
    public class FeeHead
    {
        public int FeeHeadId { get; set; }
        public string FeeHeadName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
