namespace SchoolProject.Models
{
    public class Accountant
    {
        public int UserId { get; set; }
        public string Name { get; set; } = null!;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }

        // ✅ NEW: Profile Image
        public string? AccountantImage { get; set; }
    }
}
