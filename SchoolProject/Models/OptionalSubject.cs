namespace SchoolProject.Models
{
    public class OptionalSubject
    {

        public int OptionalSubjectId { get; set; }

        public int SessionId { get; set; }

        public int SubjectId { get; set; }

        public string OptionalSubjectName { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}
