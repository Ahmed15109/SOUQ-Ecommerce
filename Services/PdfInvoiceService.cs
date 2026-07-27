using EcommerceApp.Helpers;
using EcommerceApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EcommerceApp.Services
{
    public class PdfInvoiceService
    {
        public byte[] GenerateInvoice(Order order)
        {
            var localCreatedAt = order.CreatedAt.ToCairoTime();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.ContentFromRightToLeft();
                    page.DefaultTextStyle(style => style.FontSize(11).FontFamily("Cairo"));

                    page.Header()
                        .AlignCenter()
                        .Text("فاتورة الطلب")
                        .FontSize(24)
                        .Bold()
                        .FontColor(Colors.Blue.Medium);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Spacing(15);
                            column.Item().Text($"رقم الطلب: #{order.Id}").Bold();
                            column.Item().Text($"التاريخ: {localCreatedAt:dd/MM/yyyy hh:mm tt}");
                            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                            column.Item().Text("معلومات العميل").FontSize(14).Bold();
                            column.Item().Text($"الاسم: {order.FullName}");
                            column.Item().Text($"الهاتف: {order.Phone}");

                            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                            column.Item().Text("عنوان التوصيل").FontSize(14).Bold();
                            column.Item().Text($"المدينة/المحافظة: {order.City}");
                            column.Item().Text($"المنطقة: {order.Area}");
                            column.Item().Text($"الشارع: {order.Street}");
                            column.Item().Text($"المبنى: {order.Building}");
                            if (!string.IsNullOrWhiteSpace(order.Notes))
                            {
                                column.Item().Text($"ملاحظات: {order.Notes}");
                            }

                            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                            column.Item().Text("المنتجات المطلوبة").FontSize(14).Bold();
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                    columns.RelativeColumn();
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(CellStyle).Text("اسم المنتج").Bold();
                                    header.Cell().Element(CellStyle).AlignCenter().Text("السعر").Bold();
                                    header.Cell().Element(CellStyle).AlignCenter().Text("الكمية").Bold();
                                    header.Cell().Element(CellStyle).AlignCenter().Text("الإجمالي").Bold();
                                });

                                foreach (var item in order.OrderItems)
                                {
                                    table.Cell().Element(CellStyle).Column(itemColumn =>
                                    {
                                        itemColumn.Item().Text(item.ProductName);
                                        if (item.SelectedWeightKg.HasValue)
                                        {
                                            itemColumn.Item()
                                                .Text($"الوزن: {item.SelectedWeightKg:0.##} كجم")
                                                .FontSize(9)
                                                .FontColor(Colors.Grey.Darken2);
                                            itemColumn.Item()
                                                .Text($"سعر الكيلو: {item.SelectedPricePerKg:N2} ج.م")
                                                .FontSize(9)
                                                .FontColor(Colors.Grey.Darken2);
                                            if (item.CuttingSelected)
                                            {
                                                itemColumn.Item()
                                                    .Text($"خدمة التقطيع: {item.CuttingFeeApplied:N2} ج.م")
                                                    .FontSize(9)
                                                    .FontColor(Colors.Grey.Darken2);
                                            }
                                        }
                                    });
                                    table.Cell().Element(CellStyle).AlignCenter().Text($"{item.UnitPrice:N2} ج.م");
                                    table.Cell().Element(CellStyle).AlignCenter().Text(item.Quantity.ToString());
                                    table.Cell().Element(CellStyle).AlignCenter().Text($"{item.LineTotal:N2} ج.م");
                                }
                            });

                            column.Item().AlignLeft().Column(totals =>
                            {
                                totals.Item().Text($"المجموع الفرعي: {order.Subtotal:N2} ج.م");
                                totals.Item().Text($"رسوم التوصيل: {order.DeliveryFee:N2} ج.م");
                                totals.Item().Text($"الإجمالي: {order.Total:N2} ج.م").FontSize(14).Bold();
                            });
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text("شكرًا لتسوقكم معنا")
                        .FontSize(10);
                });
            });

            return document.GeneratePdf();
        }

        private static IContainer CellStyle(IContainer container) =>
            container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);

    }
}
