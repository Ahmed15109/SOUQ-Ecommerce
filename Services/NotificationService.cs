using EcommerceApp.Data;
using EcommerceApp.Extensions;
using EcommerceApp.Helpers;
using EcommerceApp.Models;
using EcommerceApp.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services
{
    public interface INotificationService
    {
        void AddOrderNotifications(Order order, string userId);
        void AddOrderStatusNotification(Order order);
        void AddPharmacyStatusNotification(PharmacyRequest request);
        Task<PagedResult<Notification>> GetAdminNotificationsPagedAsync(string adminUserId, int page = 1, int pageSize = 20);
        Task<PagedResult<Notification>> GetUserNotificationsPagedAsync(string userId, int page = 1, int pageSize = 20);
        Task<int> GetUnreadCountAsync(string? userId, bool isAdmin);
        Task MarkAsReadAsync(int notificationId, string? userId, bool isAdmin);
    }

    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        public void AddOrderNotifications(Order order, string userId)
        {
            _context.Notifications.AddRange(
                new Notification
                {
                    Title = "طلب جديد",
                    Message = "تم إنشاء طلب جديد.",
                    IsForAdmin = true,
                    Order = order,
                    CreatedAt = DateTime.UtcNow
                },
                new Notification
                {
                    Title = "تم استلام طلبك",
                    Message = "تم استلام طلبك وسيتم التواصل معك قريبًا.",
                    UserId = userId,
                    IsForAdmin = false,
                    Order = order,
                    CreatedAt = DateTime.UtcNow
                });
        }

        public void AddOrderStatusNotification(Order order)
        {
            if (string.IsNullOrWhiteSpace(order.UserId))
            {
                return;
            }

            _context.Notifications.Add(new Notification
            {
                Title = "تحديث حالة الطلب",
                Message = $"تم تحديث حالة الطلب #{order.Id} إلى: {order.Status.GetDisplayName()}",
                UserId = order.UserId,
                IsForAdmin = false,
                OrderId = order.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        public void AddPharmacyStatusNotification(PharmacyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                return;
            }

            _context.Notifications.Add(new Notification
            {
                Title = "تحديث حالة طلب الصيدلية",
                Message = $"تم تحديث حالة طلب الصيدلية #{request.Id} إلى: {request.Status.GetDisplayName()}",
                UserId = request.UserId,
                IsForAdmin = false,
                PharmacyRequestId = request.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        public async Task<PagedResult<Notification>> GetAdminNotificationsPagedAsync(
            string adminUserId,
            int page = 1,
            int pageSize = 20)
        {
            var result = await _context.Notifications
                .AsNoTracking()
                .Where(notification => notification.IsForAdmin)
                .Include(notification => notification.Order)
                .OrderByDescending(notification => notification.CreatedAt)
                .ThenByDescending(notification => notification.Id)
                .ToPagedListAsync(page, pageSize, defaultPageSize: 20, maxPageSize: 100);

            await ApplyAdminReadStateAsync(result.Items, adminUserId);
            return result;
        }

        public async Task<PagedResult<Notification>> GetUserNotificationsPagedAsync(
            string userId,
            int page = 1,
            int pageSize = 20)
        {
            return await _context.Notifications
                .AsNoTracking()
                .Where(notification => notification.UserId == userId && !notification.IsForAdmin)
                .Include(notification => notification.Order)
                .OrderByDescending(notification => notification.CreatedAt)
                .ThenByDescending(notification => notification.Id)
                .ToPagedListAsync(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
        }

        public async Task<int> GetUnreadCountAsync(string? userId, bool isAdmin)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return 0;
            }

            if (isAdmin)
            {
                return await _context.Notifications.CountAsync(notification =>
                    notification.IsForAdmin &&
                    !notification.Reads.Any(read => read.UserId == userId));
            }

            return await _context.Notifications.CountAsync(notification =>
                notification.UserId == userId &&
                !notification.IsForAdmin &&
                !notification.IsRead);
        }

        public async Task MarkAsReadAsync(int notificationId, string? userId, bool isAdmin)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            var notification = await _context.Notifications
                .SingleOrDefaultAsync(item => item.Id == notificationId);

            if (notification == null)
            {
                return;
            }

            if (isAdmin && notification.IsForAdmin)
            {
                if (!await _context.NotificationReads.AnyAsync(read =>
                        read.NotificationId == notificationId && read.UserId == userId))
                {
                    _context.NotificationReads.Add(new NotificationRead
                    {
                        NotificationId = notificationId,
                        UserId = userId,
                        ReadAtUtc = DateTime.UtcNow
                    });

                    try
                    {
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateException)
                    {
                        var addedRead = _context.ChangeTracker.Entries<NotificationRead>()
                            .FirstOrDefault(entry => entry.State == EntityState.Added);
                        if (addedRead != null)
                        {
                            addedRead.State = EntityState.Detached;
                        }

                        var persisted = await _context.NotificationReads
                            .AsNoTracking()
                            .AnyAsync(read =>
                                read.NotificationId == notificationId &&
                                read.UserId == userId);
                        if (!persisted)
                        {
                            throw;
                        }
                    }
                }

                return;
            }

            if (!notification.IsForAdmin && notification.UserId == userId && !notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        private async Task ApplyAdminReadStateAsync(
            IEnumerable<Notification> notifications,
            string adminUserId)
        {
            var materialized = notifications.ToList();
            var notificationIds = materialized.Select(notification => notification.Id).ToList();
            if (notificationIds.Count == 0)
            {
                return;
            }

            var readIds = await _context.NotificationReads
                .AsNoTracking()
                .Where(read => read.UserId == adminUserId && notificationIds.Contains(read.NotificationId))
                .Select(read => read.NotificationId)
                .ToListAsync();

            var readSet = readIds.ToHashSet();
            materialized.ForEach(notification => notification.IsRead = readSet.Contains(notification.Id));
        }
    }
}
