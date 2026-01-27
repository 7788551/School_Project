namespace SchoolProject.Models
{
    public class Attendance
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public bool IsPresent { get; set; }
        public DateTime AttendanceDate { get; set; }    

    }
}
