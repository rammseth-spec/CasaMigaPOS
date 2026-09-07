-- Operational authorization only. Cart remains transient; no sale/payment tables.
INSERT INTO permission VALUES('pos.use');
INSERT INTO role_permission VALUES('Administrator','pos.use'),('Manager','pos.use'),('Cashier','pos.use');
