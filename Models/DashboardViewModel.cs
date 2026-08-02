using System.Collections.Generic;

namespace KeystoneLogistics.Models
{
    public class DashboardViewModel
    {
        public int TotalActiveLoads { get; set; }
        public int PendingDeliveryLoads { get; set; }
        public int AvailableDriversCount { get; set; }
        public int TotalCustomersCount { get; set; }
        public List<Load> RecentLoads { get; set; }
    }
}