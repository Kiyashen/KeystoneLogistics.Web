using System.ComponentModel.DataAnnotations.Schema;

namespace KeystoneLogistics.Models
{
    public partial class Load
    {
        [NotMapped]
        public decimal QuotedAmount
        {
            get
            {
                decimal km = 80m;
                string route = ((PickupLocation ?? "") + " " + (DropoffLocation ?? "")).ToLowerInvariant();
                if (route.Contains("pietermaritzburg") || route.Contains("pmb")) km = 80m;
                else if (route.Contains("pinetown")) km = 20m;
                else if (route.Contains("umhlanga")) km = 18m;
                else if (route.Contains("chatsworth")) km = 15m;
                else if (route.Contains("ballito")) km = 45m;
                else if (route.Contains("howick")) km = 110m;
                decimal amount = 500m + (km * 12m) + (800m * 1.50m);
                if (amount < 750m) amount = 750m;
                return amount;
            }
        }

        [NotMapped]
        public decimal DriverCut
        {
            get
            {
                decimal km = 80m;
                string route = ((PickupLocation ?? "") + " " + (DropoffLocation ?? "")).ToLowerInvariant();
                if (route.Contains("pietermaritzburg") || route.Contains("pmb")) km = 80m;
                else if (route.Contains("pinetown")) km = 20m;
                else if (route.Contains("umhlanga")) km = 18m;
                else if (route.Contains("chatsworth")) km = 15m;
                else if (route.Contains("ballito")) km = 45m;
                else if (route.Contains("howick")) km = 110m;
                decimal amount = 400m + (km * 8m);
                if (amount < 500m) amount = 500m;
                return amount;
            }
        }
    }
}