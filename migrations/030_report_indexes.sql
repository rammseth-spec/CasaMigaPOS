-- Rendimiento con un año de ventas (docs/VOLUME.md): los reportes, Ganancia y el pronóstico filtran por fecha de venta, de devolución
-- y de apertura de turno; sin estos índices cada consulta recorría todas las ventas (más de 100 mil en un año).
CREATE INDEX ix_sale_confirmed ON sale(confirmed_utc);
CREATE INDEX ix_sale_refund_refunded ON sale_refund(refunded_utc);
CREATE INDEX ix_cash_session_opened ON cash_session(opened_utc);
