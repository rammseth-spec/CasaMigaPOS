using System.Diagnostics;
using System.Text;
using CasaMiga.Application;
using CasaMiga.Domain;
using CasaMiga.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CasaMiga.Tests;

/// <summary>
/// Prueba de volumen de la caja (docs/VOLUME.md): un año de ventas en la base local y lo que tardan reportes, ganancia e historial.
/// Tarda minutos, así que sólo corre con CASAMIGA_VOLUME=1 (ventas por día en CASAMIGA_VOLUME_PER_DAY, 300 por omisión);
/// CASAMIGA_VOLUME_OUT guarda la tabla de tiempos. Cada pantalla debe responder en menos de 3 s.
/// </summary>
[TestClass, DoNotParallelize]
public sealed class VolumeTests : SalesFixture
{
    [TestMethod]
    public void AYearOfSalesKeepsReportsAndHistoryResponsive()
    {
        if (Environment.GetEnvironmentVariable("CASAMIGA_VOLUME") != "1") { Assert.Inconclusive("Prueba de volumen: define CASAMIGA_VOLUME=1 para correrla."); return; }
        var perDay = int.TryParse(Environment.GetEnvironmentVariable("CASAMIGA_VOLUME_PER_DAY"), out var n) ? n : 300;
        const int days = 365;
        var category = catalog.CreateCategory("Volumen").Id;
        var products = Enumerable.Range(1, 60).Select(i => catalog.CreateProduct(category, $"V{i}", $"Pan {i:00}", "", new Money(800 + i % 12 * 150))).ToList();

        // Como en la sucursal: un turno por día (abre a las 7:00, conteo exacto y cierre a las 19:10). El turno del fixture se cierra antes.
        var admin = user.Current!.Id.ToString("D"); var till = terminal.Id.ToString("D");
        void Close(string session, DateTimeOffset at, long expected)
        {
            Sql($"INSERT INTO cash_count(session_id,counted_by,counted_utc,counted_cents) VALUES('{session}','{admin}','{at:O}',{expected})");
            Sql($"UPDATE cash_session SET state='CLOSED',closed_utc='{at:O}',closed_by='{admin}',expected_cents={expected},counted_cents={expected},difference_cents=0 WHERE id='{session}'");
        }
        Close(cash.Id.ToString("D"), DateTimeOffset.UtcNow, 50000);
        var load = Stopwatch.StartNew();
        var random = new Random(7);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var count = 0;
        for (var d = days - 1; d >= 0; d--)
        {
            var start = new DateTimeOffset(today.AddDays(-d).ToDateTime(new TimeOnly(13, 0)), TimeSpan.Zero);
            var session = Guid.NewGuid();
            Sql($"INSERT INTO cash_session(id,terminal_id,opened_by,opened_utc,opening_cents,state) VALUES('{session:D}','{till}','{admin}','{start:O}',50000,'OPEN')");
            var cashSales = work.Run(store =>
            {
                long cashTotal = 0;
                for (var s = 0; s < perDay; s++)
                {
                    var lines = products.OrderBy(_ => random.Next()).Take(1 + random.Next(4))
                        .Select(p => new SaleItem(p.Id, p.Name, p.SalePrice, 1 + random.Next(3))).ToArray();
                    var total = lines.Sum(l => l.LineTotal.Cents);
                    var r = random.NextDouble(); var method = r < .85 ? PaymentMethod.Cash : r < .95 ? PaymentMethod.Card : PaymentMethod.Transfer;
                    if (method == PaymentMethod.Cash) cashTotal += total;
                    store.AddSale(new Sale(Guid.NewGuid(), Guid.NewGuid(), user.Current!.Id, terminal.Id, session, store.NextSaleSequence(terminal.Id),
                        start.AddMinutes(720.0 * s / perDay), lines, Guid.NewGuid(), new Money(total), method, method == PaymentMethod.Cash ? null : "4242"));
                    count++;
                }
                return cashTotal;
            });
            if (d > 0) Close(session.ToString("D"), start.AddMinutes(730), 50000 + cashSales);
        }
        load.Stop();

        var reports = new ReportService(work, user, TimeZoneInfo.Utc);
        var profit = new ProfitService(work, new SqliteProductionUnitOfWork(db), user, TimeZoneInfo.Utc);
        var history = new SalesHistoryService(work, user, TimeZoneInfo.Utc);
        var rows = new List<string>();
        var slow = new List<string>();
        void Time(string screen, string period, Action action)
        {
            var watch = Stopwatch.StartNew(); action(); watch.Stop();
            rows.Add($"| {screen} | {period} | {watch.ElapsedMilliseconds} ms |");
            if (watch.ElapsedMilliseconds >= 3000) slow.Add($"{screen} ({period}): {watch.ElapsedMilliseconds} ms");
        }
        foreach (var (period, from) in new[] { ("hoy", today), ("30 días", today.AddDays(-29)), ("92 días", today.AddDays(-91)), ("365 días", today.AddDays(-364)) })
        {
            Time("Reportes · Resumen", period, () => Assert.IsGreaterThan(0, reports.Sales(from, today).Data.SaleCount));
            Time("Reportes · Comparativo", period, () => reports.Compare(from, today));
            Time("Reportes · Ventas por hora", period, () => reports.Heatmap(from, today));
            Time("Reportes · Control de caja", period, () => reports.CashControl(from, today));
            Time("Reportes · Ganancia", period, () => profit.Profit(from, today));
            Time("Historial · por fechas", period, () => history.Search(new(SalesHistoryMode.DateRange, "", from, today)));
            Time("Historial · tarjeta + producto", period, () => history.Search(new(SalesHistoryMode.DateRange, "", from, today, Method: "CARD", Product: "pan 07")));
        }
        Time("Historial · Todos", "—", () => history.Search(new()));
        Time("Historial · última página", "—", () => history.Search(new(Page: count / 50)));
        Time("Producción · pronóstico", "12 semanas", () => reports.Forecast(today.AddDays(1), 12));

        var table = new StringBuilder($"{count:N0} ventas en {days} días cargadas en {load.Elapsed.TotalSeconds:0} s · base {new FileInfo(Path.Combine(directory, "casamiga.db")).Length / 1_048_576.0:0} MB\n\n")
            .AppendLine("| Pantalla | Periodo | Tiempo |").AppendLine("|---|---|---|");
        foreach (var row in rows) table.AppendLine(row);
        Console.WriteLine(table);
        if (Environment.GetEnvironmentVariable("CASAMIGA_VOLUME_OUT") is { Length: > 0 } output) File.WriteAllText(output, table.ToString());
        Assert.IsEmpty(slow, "Más de 3 s: " + string.Join("; ", slow));
    }
}
