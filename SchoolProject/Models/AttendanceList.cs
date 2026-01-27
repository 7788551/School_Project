namespace SchoolProject.Models
{
    public class AttendanceList
    {
        public int AttendanceId { get; set; }

        // ✅ ADD THIS (REQUIRED FOR INLINE EDIT)
        public int StudentId { get; set; }

        public string ClassName { get; set; }
        public string SectionName { get; set; }
        public string StudentName { get; set; }
        public DateTime AttendanceDate { get; set; }
        public string Status { get; set; }
        public DateTime CreatedDate { get; set; }
    }


}
