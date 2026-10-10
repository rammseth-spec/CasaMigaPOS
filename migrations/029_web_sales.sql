-- Ventas de las cajas web en los reportes de la caja (docs/PLAN_COMPLETAR.md, D5).
-- Copia de sólo lectura de lo que registraron las cajas web de la sucursal (la caja la baja de la nube con /api/sync/web-sales).
-- No se sube (sin disparadores de sync_outbox): la nube ya la tiene. Los reportes leen las vistas report_*, que suman lo local y lo web.
CREATE TABLE web_terminal(
 id TEXT PRIMARY KEY NOT NULL CHECK(length(id)=36),
 name TEXT NOT NULL,
 seq INTEGER NOT NULL DEFAULT 0 CHECK(seq>=0)
) STRICT;
CREATE TABLE web_sale(
 id TEXT PRIMARY KEY NOT NULL,
 terminal_id TEXT NOT NULL REFERENCES web_terminal(id),
 user_id TEXT NOT NULL,
 cash_session_id TEXT NOT NULL,
 sequence INTEGER NOT NULL,
 confirmed_utc TEXT NOT NULL,
 subtotal_cents INTEGER NOT NULL,
 total_cents INTEGER NOT NULL
) STRICT;
CREATE INDEX ix_web_sale_confirmed ON web_sale(confirmed_utc);
CREATE INDEX ix_web_sale_cash ON web_sale(cash_session_id);
CREATE TABLE web_sale_item(
 sale_id TEXT NOT NULL REFERENCES web_sale(id) ON DELETE CASCADE,
 position INTEGER NOT NULL,
 product_id TEXT NOT NULL,
 product_name TEXT NOT NULL,
 unit_price_cents INTEGER NOT NULL,
 quantity INTEGER NOT NULL,
 line_total_cents INTEGER NOT NULL,
 PRIMARY KEY(sale_id,position)
) STRICT;
CREATE TABLE web_payment(
 sale_id TEXT PRIMARY KEY NOT NULL REFERENCES web_sale(id) ON DELETE CASCADE,
 method TEXT NOT NULL CHECK(method IN('CASH','CARD','TRANSFER')),
 applied_cents INTEGER NOT NULL,
 received_cents INTEGER NOT NULL,
 change_cents INTEGER NOT NULL,
 reference TEXT
) STRICT;
CREATE TABLE web_sale_refund(
 id TEXT PRIMARY KEY NOT NULL,
 sale_id TEXT NOT NULL,
 kind TEXT NOT NULL CHECK(kind IN('CANCELLATION','RETURN')),
 reason TEXT NOT NULL,
 user_id TEXT NOT NULL,
 terminal_id TEXT NOT NULL REFERENCES web_terminal(id),
 cash_session_id TEXT NOT NULL,
 refunded_utc TEXT NOT NULL,
 total_cents INTEGER NOT NULL
) STRICT;
CREATE INDEX ix_web_sale_refund_sale ON web_sale_refund(sale_id);
CREATE INDEX ix_web_sale_refund_date ON web_sale_refund(refunded_utc);
CREATE TABLE web_sale_refund_item(
 refund_id TEXT NOT NULL REFERENCES web_sale_refund(id) ON DELETE CASCADE,
 sale_id TEXT NOT NULL,
 position INTEGER NOT NULL,
 quantity INTEGER NOT NULL,
 line_total_cents INTEGER NOT NULL,
 PRIMARY KEY(refund_id,position)
) STRICT;
CREATE TABLE web_cash_session(
 id TEXT PRIMARY KEY NOT NULL,
 terminal_id TEXT NOT NULL REFERENCES web_terminal(id),
 opened_by TEXT NOT NULL,
 opened_utc TEXT NOT NULL,
 opening_cents INTEGER NOT NULL,
 state TEXT NOT NULL CHECK(state IN('OPEN','CLOSED')),
 closed_utc TEXT,
 counted_cents INTEGER
) STRICT;
CREATE INDEX ix_web_cash_session_opened ON web_cash_session(opened_utc);
CREATE TABLE web_cash_count(
 session_id TEXT PRIMARY KEY NOT NULL,
 counted_cents INTEGER NOT NULL
) STRICT;
CREATE TABLE web_cash_movement(
 id TEXT PRIMARY KEY NOT NULL,
 cash_session_id TEXT NOT NULL,
 kind TEXT NOT NULL CHECK(kind IN('EXPENSE','WITHDRAWAL','DEPOSIT')),
 amount_cents INTEGER NOT NULL,
 occurred_utc TEXT NOT NULL
) STRICT;
CREATE INDEX ix_web_cash_movement_session ON web_cash_movement(cash_session_id);

CREATE VIEW report_sale AS
 SELECT id,user_id,terminal_id,cash_session_id,confirmed_utc,total_cents FROM sale
 UNION ALL SELECT id,user_id,terminal_id,cash_session_id,confirmed_utc,total_cents FROM web_sale;
CREATE VIEW report_sale_item AS
 SELECT sale_id,position,product_id,product_name,quantity,line_total_cents FROM sale_item
 UNION ALL SELECT sale_id,position,product_id,product_name,quantity,line_total_cents FROM web_sale_item;
CREATE VIEW report_payment AS
 SELECT sale_id,method,applied_cents FROM payment
 UNION ALL SELECT sale_id,method,applied_cents FROM web_payment;
CREATE VIEW report_sale_refund AS
 SELECT id,sale_id,kind,reason,cash_session_id,refunded_utc,total_cents FROM sale_refund
 UNION ALL SELECT id,sale_id,kind,reason,cash_session_id,refunded_utc,total_cents FROM web_sale_refund;
CREATE VIEW report_sale_refund_item AS
 SELECT refund_id,sale_id,position,quantity,line_total_cents FROM sale_refund_item
 UNION ALL SELECT refund_id,sale_id,position,quantity,line_total_cents FROM web_sale_refund_item;
CREATE VIEW report_cash_session AS
 SELECT c.id,c.terminal_id,c.opened_by,c.opened_utc,c.opening_cents,c.state,c.closed_utc,COALESCE(c.counted_cents,n.counted_cents) AS counted_cents
 FROM cash_session c LEFT JOIN cash_count n ON n.session_id=c.id
 UNION ALL SELECT w.id,w.terminal_id,w.opened_by,w.opened_utc,w.opening_cents,w.state,w.closed_utc,COALESCE(w.counted_cents,k.counted_cents)
 FROM web_cash_session w LEFT JOIN web_cash_count k ON k.session_id=w.id;
CREATE VIEW report_cash_movement AS
 SELECT cash_session_id,kind,amount_cents FROM cash_movement
 UNION ALL SELECT cash_session_id,kind,amount_cents FROM web_cash_movement;
