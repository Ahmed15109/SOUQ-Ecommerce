using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services
{
    public interface INotificationService
    {
        Task CreateOrderNotificationsAsync(int orderId, string userId);
        Task<List<Notification>> GetAdminNotificationsAsync(int count = 50);
        Task<List<Notification>> GetUserNotificationsAsync(string userId, int count = 50);
        Task<int> GetUnreadCountAsync(string? userId, bool isAdmin);
        Task MarkAsReadAsync(int notificationId);
    }

    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateOrderNotificationsAsync(int orderId, string userId)
        {
            var adminNotification = new Notification
            {
                Title = "طلب جديد",
                Message = $"تم إنشاء طلب جديد رقم #{orderId}",
                IsForAdmin = true,
                OrderId = orderId,
                CreatedAt = DateTime.Now
            };

            var userNotification = new Notification
            {
                Title = "تم استلام طلبك",
                Message = $"تم استلام طلبك رقم #{orderId} وسيتم التواصل معك قريبًا",
                UserId = userId,
                IsForAdmin = false,
                OrderId = orderId,
                CreatedAt = DateTime.Now
            };

            _context.Notifications.Add(adminNotification);
            _context.Notifications.Add(userNotification);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Notification>> GetAdminNotificationsAsync(int count = 50)
        {
            return await _context.Notifications
                .Where(n => n.IsForAdmin)
                .Include(n => n.Order)
                .OrderByDescending(n => n.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(string userId, int count = 50)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsForAdmin)
                .Include(n => n.Order)
                .OrderByDescending(n => n.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(string? userId, bool isAdmin)
        {
            if (isAdmin)
            {
                return await _context.Notifications
                    .Where(n => n.IsForAdmin && !n.IsRead)
                    .CountAsync();
            }
            else
            {
                return await _context.Notifications
                    .Where(n => n.UserId == userId && !n.IsForAdmin && !n.IsRead)
                    .CountAsync();
            }
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }
    }
}
