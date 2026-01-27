namespace SchoolProject.Models
{
    public class TransportSlab
    {
        public int SlabId { get; set; }
        public decimal FromKM { get; set; }
        public decimal ToKM { get; set; }
        public decimal Amount { get; set; }
        public bool IsActive { get; set; }
    }

}
