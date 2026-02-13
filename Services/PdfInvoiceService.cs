using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing;
using EcommerceApp.Models;
using System.IO;

namespace EcommerceApp.Services
{
    public class PdfInvoiceService
    {
        private readonly string _fontPath;

        public PdfInvoiceService(IWebHostEnvironment env)
        {
            _fontPath = Path.Combine(env.WebRootPath, "fonts", "Cairo-Regular.ttf");
        }

        public byte[] GenerateInvoice(Order order)
        {
            if (File.Exists(_fontPath))
            {
                using var fontStream = File.OpenRead(_fontPath);
                FontManager.RegisterFont(fontStream);
            }

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Cairo"));

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

                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Column(col =>
                                {
                                    col.Item().Text($"رقم الطلب: #{order.Id}").Bold();
                                    col.Item().Text($"التاريخ: {order.CreatedAt:dd/MM/yyyy hh:mm tt}");
                                });
                            });

                            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                            column.Item().Text("معلومات العميل").FontSize(14).Bold();
                            column.Item().Column(col =>
                            {
                                col.Item().Text($"الاسم: {order.FullName}");
                                col.Item().Text($"الهاتف: {order.Phone}");
                            });

                            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                            column.Item().Text("عنوان التوصيل").FontSize(14).Bold();
                            column.Item().Column(col =>
                            {
                                col.Item().Text($"المدينة/المحافظة: {order.City}");
                                col.Item().Text($"المنطقة: {order.Area}");
                                col.Item().Text($"الشارع: {order.Street}");
                                col.Item().Text($"المبنى: {order.Building}");
                                if (!string.IsNullOrEmpty(order.Notes))
                                {
                                    col.Item().Text($"ملاحظات: {order.Notes}");
                                }
                            });

                            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                            column.Item().Text("المنتجات المطلوبة").FontSize(14).Bold();
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3); 
                                    columns.RelativeColumn(1); 
                                    columns.RelativeColumn(1); 
                                    columns.RelativeColumn(1); 
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
                                    table.Cell().Element(CellStyle).Column(col => 
                                    {
                                        col.Item().Text(item.ProductName);
                                        if (item.SelectedWeightKg.HasValue)
                                        {
                                            col.Item().Text($"الوزن: {item.SelectedWeightKg} كجم").FontSize(9).FontColor(Colors.Grey.Darken2);
                                            col.Item().Text($"سعر الكيلو: {item.SelectedPricePerKg} ج.م").FontSize(9).FontColor(Colors.Grey.Darken2);
                                            if (item.CuttingFeeApplied > 0)
                                            {
                                                col.Item().Text($"خدمة تقطيع (#{item.CuttingFeeApplied})").FontSize(9).FontColor(Colors.Grey.Darken2);
                                            }
                                        }
                                    });
                                    table.Cell().Element(CellStyle).AlignCenter().Text($"{item.UnitPrice:N2} ج.م");
                                    table.Cell().Element(CellStyle).AlignCenter().Text(item.Quantity.ToString());
                                    table.Cell().Element(CellStyle).AlignCenter().Text($"{item.LineTotal:N2} ج.م");
                                }

                                static IContainer CellStyle(IContainer container)
                                {
                                    return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                                }
                            });

                            column.Item().PaddingTop(10);

                            column.Item().AlignRight().Column(col =>
                            {
                                col.Item().Row(row =>
                                {
                                    row.RelativeItem().Text("المجموع الفرعي:");
                                    row.RelativeItem().AlignRight().Text($"{order.Subtotal:N2} ج.م").Bold();
                                });
                                col.Item().Row(row =>
                                {
                                    row.RelativeItem().Text("رسوم التوصيل:");
                                    row.RelativeItem().AlignRight().Text($"{order.DeliveryFee:N2} ج.م").Bold();
                                });
                                col.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                                col.Item().Row(row =>
                                {
                                    row.RelativeItem().Text("الإجمالي الكلي:").FontSize(14).Bold();
                                    row.RelativeItem().AlignRight().Text($"{order.Total:N2} ج.م").FontSize(14).Bold().FontColor(Colors.Blue.Medium);
                                });
                            });
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("شكراً لتسوقكم معنا").FontSize(10);
                        });
                });
            });

            return document.GeneratePdf();
        }
    }
}
