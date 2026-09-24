using System;

namespace KeystoneLogistics.Models
{
    public class Review
    {
        public int ReviewId { get; set; }
        public string CustomerName { get; set; }
        public int Rating { get; set; } // 1 to 5 stars
        public string Comment { get; set; }
        public DateTime DatePosted { get; set; } = DateTime.Now;
    }
}