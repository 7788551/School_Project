namespace SchoolProject.Models
{
    public class FeeSchedules
    {
        public int FeeHeadId { get; set; }
        public string FeeHeadName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<string> Schedules { get; set; }
    }
}
