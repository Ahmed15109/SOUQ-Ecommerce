using EcommerceApp.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EcommerceApp.Services
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly AppDbContext _context;

        public DatabaseHealthCheck(AppDbContext context)
        {
            _context = context;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Database.CanConnectAsync(cancellationToken)
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy("The application database is unavailable.");
            }
            catch (Exception exception)
            {
                return HealthCheckResult.Unhealthy("The application database is unavailable.", exception);
            }
        }
    }
}
