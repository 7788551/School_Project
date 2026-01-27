namespace SchoolProject.Models
{
    public class StudentAttendance
    {
        public int AttendanceId { get; set; }
        public int SessionId { get; set; }
        public int ClassId { get; set; }
        public int SectionId { get; set; }
        public int StudentId { get; set; }
        public DateTime AttendanceDate { get; set; }
        public string Status { get; set; } // Present / Absent
        public int MarkedBy { get; set; }
      
    }
}
