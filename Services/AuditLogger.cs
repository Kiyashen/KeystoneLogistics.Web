using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using KeystoneLogistics.Models;

namespace KeystoneLogistics.Services
{
    public static class AuditLogger
    {
        public static void Log(int loadId, string action, string performedBy)
        {
            using (var db = new KeystoneLogisticsDBEntities())
            {
                var auditLog = new AuditLog
                {
                    LoadId = loadId,
                    Action = action,
                    PerformedBy = string.IsNullOrEmpty(performedBy) ? "System" : performedBy,
                    Timestamp = DateTime.Now
                };

                db.AuditLogs.Add(auditLog);
                db.SaveChanges();
            }
        }
    }
}