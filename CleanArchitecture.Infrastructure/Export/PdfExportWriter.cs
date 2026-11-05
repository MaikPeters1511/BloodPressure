using System.Globalization;
using CleanArchitecture.Application.Export;
using CleanArchitecture.Contracts;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CleanArchitecture.Infrastructure.Export;

/// <summary>PDF-Bericht: Zeitraum, Messtabelle, Kennzahlen und Haftungshinweis (siehe ADR 0002).</summary>
public sealed class PdfExportWriter : IExportWriter
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    static PdfExportWriter() => QuestPDF.Settings.License = LicenseType.Community;

    public string Format => ExportQuery.Pdf;
    public string ContentType => "application/pdf";
    public string FileExtension => "pdf";

    public Task WriteAsync(ExportData data, Stream output, CancellationToken cancellationToken)
    {
        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.DefaultTextStyle(style => style.FontSize(10));

            page.Header().Column(column =>
            {
                column.Item().Text("Blutdruckbericht").FontSize(18).SemiBold();
                column.Item().Text(Period(data)).FontColor(Colors.Grey.Darken2);
            });

            page.Content().PaddingVertical(10).Column(column =>
            {
                column.Spacing(10);
                column.Item().Element(container => Summary(container, data));
                column.Item().Element(container => Table(container, data));
                column.Item().Text(
                        "Hinweis: Die Kategorie ist eine Einordnung nach ESC 2024 und keine Diagnose. " +
                        "Bei Beschwerden oder auffälligen Werten ärztlichen Rat einholen.")
                    .FontSize(8).FontColor(Colors.Grey.Darken2);
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Seite ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        })).GeneratePdf(output);

        return Task.CompletedTask;
    }

    private static string Period(ExportData data)
    {
        var first = data.From ?? data.Items.FirstOrDefault()?.DateTime;
        var last = data.To ?? data.Items.LastOrDefault()?.DateTime;
        return first is null || last is null
            ? "Keine Messungen im Zeitraum"
            : $"Zeitraum: {first.Value.ToString("d", De)} – {last.Value.ToString("d", De)} (UTC), {data.Items.Count} Messungen";
    }

    private static void Summary(IContainer container, ExportData data)
    {
        if (data.Items.Count == 0)
        {
            return;
        }

        static string Line(string name, IEnumerable<int> values)
        {
            var list = values.ToList();
            return string.Create(De, $"{name}: Ø {list.Average():0.#}, min {list.Min()}, max {list.Max()}");
        }

        container.Column(column =>
        {
            column.Item().Text("Kennzahlen").SemiBold();
            column.Item().Text(Line("Systolisch (mmHg)", data.Items.Select(i => i.Systolisch)));
            column.Item().Text(Line("Diastolisch (mmHg)", data.Items.Select(i => i.Diastolisch)));
            column.Item().Text(Line("Puls (1/min)", data.Items.Select(i => i.Pulse)));
        });
    }

    private static void Table(IContainer container, ExportData data)
    {
        if (data.Items.Count == 0)
        {
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn(2);
            });

            table.Header(header =>
            {
                foreach (var title in new[] { "Datum (UTC)", "Sys", "Dia", "Puls", "SpO2 %", "Kategorie" })
                {
                    header.Cell().BorderBottom(1).PaddingVertical(2).Text(title).SemiBold();
                }
            });

            foreach (var item in data.Items)
            {
                table.Cell().PaddingVertical(1).Text(item.DateTime.ToUniversalTime().ToString("g", De));
                table.Cell().PaddingVertical(1).Text(item.Systolisch.ToString(De));
                table.Cell().PaddingVertical(1).Text(item.Diastolisch.ToString(De));
                table.Cell().PaddingVertical(1).Text(item.Pulse.ToString(De));
                table.Cell().PaddingVertical(1).Text(item.Oxygen.ToString(De));
                table.Cell().PaddingVertical(1).Text(item.Category);
            }
        });
    }
}
