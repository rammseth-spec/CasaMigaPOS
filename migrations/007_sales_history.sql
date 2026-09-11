INSERT INTO permission VALUES('sales.view');
INSERT INTO role_permission VALUES('Administrator','sales.view'),('Manager','sales.view');
CREATE INDEX ix_sale_history ON sale(confirmed_utc DESC,id DESC);
