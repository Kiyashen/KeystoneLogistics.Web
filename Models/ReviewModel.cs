using System;

namespace KeystoneLogistics.Models
{
    public class ReviewModel
    {
        public int Id { get; set; }
        public string CustomerName { get; set; }
        public int Rating { get; set; } // 1 to 5 stars
        public string Comment { get; set; }
        public DateTime DatePosted { get; set; }
        public bool IsBadReview { get; set; } // Flags ratings <= 2
        public bool AdminNotified { get; set; } // Tracks admin alert status
    }

    public class DeliveryOrderModel
    {
        public int OrderId { get; set; }
        public string CustomerId { get; set; }
        public bool IsDelivered { get; set; }
        public bool PodGenerated { get; set; } // Proof of Delivery receipt issued
        public bool HasReviewed { get; set; }
    }
}