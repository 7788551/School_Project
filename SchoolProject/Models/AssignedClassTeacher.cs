namespace SchoolProject.Models
{
    public class AssignedClassTeacher
    {
        public int AssignmentId { get; set; }
        public string SessionName { get; set; }
        public string ClassName { get; set; }
        public string SectionName { get; set; }
        public string TeacherName { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
